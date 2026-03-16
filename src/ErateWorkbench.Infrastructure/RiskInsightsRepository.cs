using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure;

// ---------------------------------------------------------------------------
// Risk calculation helpers — public so tests and pages can use them directly.
// ---------------------------------------------------------------------------

/// <summary>
/// Pure static risk calculations used by <see cref="RiskInsightsRepository"/>.
/// All inputs are doubles so EF Core double-based sums feed in without casting noise.
/// </summary>
public static class RiskCalculator
{
    /// <summary>
    /// Fraction of requested (eligible) funding that was NOT committed.
    /// A 100% reduction means the FRN was fully denied; 0% means fully approved.
    /// Clamped to [0, 1]. Returns 0 when requested is zero (no data).
    /// </summary>
    public static double ReductionPct(double requested, double committed)
    {
        if (requested <= 0) return 0.0;
        return Math.Clamp(1.0 - committed / requested, 0.0, 1.0);
    }

    /// <summary>
    /// Fraction of committed E-Rate funding that was actually disbursed.
    /// A 100% rate means fully drawn down; 0% means nothing was ever invoiced.
    /// Clamped to [0, 1]. Returns 0 when committed is zero (no funds to disburse).
    /// </summary>
    public static double DisbursementPct(double committed, double disbursed)
    {
        if (committed <= 0) return 0.0;
        return Math.Clamp(disbursed / committed, 0.0, 1.0);
    }

    /// <summary>
    /// Composite risk score weighted equally between reduction risk and disbursement gap risk.
    /// Higher score = higher execution risk.
    ///   Score = 0.5 × ReductionPct + 0.5 × (1 − DisbursementPct)
    /// Clamped to [0, 1].
    /// </summary>
    public static double ComputeRiskScore(double reductionPct, double disbursementPct)
        => Math.Clamp(0.5 * reductionPct + 0.5 * (1.0 - disbursementPct), 0.0, 1.0);

    /// <summary>High: score &gt; 0.6 · Moderate: 0.3–0.6 · Low: &lt; 0.3</summary>
    public static string ClassifyRisk(double score) => score switch
    {
        > 0.6 => "High",
        > 0.3 => "Moderate",
        _     => "Low",
    };
}

// ---------------------------------------------------------------------------
// Result record types
// ---------------------------------------------------------------------------

public record RiskSnapshot(
    decimal TotalRequested,
    decimal TotalCommitted,
    decimal TotalDisbursed,
    double CommitmentFulfillmentRate,
    double DisbursementCompletionRate)
{
    /// <summary>Funding lost between requested and committed (application review reductions).</summary>
    public decimal ReductionAmount => Math.Max(0m, TotalRequested - TotalCommitted);

    /// <summary>Committed funds not yet reflected in disbursements (execution/invoicing gap).</summary>
    public decimal UndisbursedAmount => Math.Max(0m, TotalCommitted - TotalDisbursed);
};

public record ApplicantRiskRow(
    string EntityNumber,
    string? ApplicantName,
    string? State,
    decimal Requested,
    decimal Committed,
    decimal Disbursed,
    double ReductionPct,
    double DisbursementPct,
    double RiskScore,
    string RiskLevel);

public record CommitmentGapRow(
    string EntityNumber,
    string? ApplicantName,
    string? State,
    decimal Committed,
    decimal Disbursed,
    decimal Gap);

public record ReductionRateRow(
    string EntityNumber,
    string? ApplicantName,
    string? State,
    decimal Requested,
    decimal Committed,
    double ReductionPct);

// ---------------------------------------------------------------------------
// Repository
// ---------------------------------------------------------------------------

/// <summary>
/// Cross-dataset risk queries joining FundingCommitments, Disbursements, and EpcEntities.
///
/// Query strategy:
///   1. Aggregate FundingCommitments and Disbursements per BEN in SQL (GROUP BY).
///   2. Join and compute risk metrics in memory.
///   3. Fetch state from EpcEntities only for the final result rows to keep the WHERE IN small.
///
/// All monetary aggregations use Sum((double?)amount ?? 0.0) to work around SQLite's
/// lack of native decimal SUM. Results are converted back to decimal in memory.
/// </summary>
public class RiskInsightsRepository(AppDbContext db, ILogger<RiskInsightsRepository>? logger = null)
{
    // Minimum eligible amount to include an entity in reduction-rate analysis.
    // Filters out rows with no meaningful data.
    private const double MinRequestedThreshold = 100.0;

    // -----------------------------------------------------------------------
    // Disbursement base query
    //
    // The USAC jpiu-tj8h dataset ("E-Rate Invoices and Authorized Disbursements")
    // stores invoice line items with an inv_line_item_status column. The
    // approved_inv_line_amt column is populated for ALL statuses — including
    // Pending lines (proposed/under-review amount) and Not Approved / Cancelled
    // lines (amount that was reviewed but ultimately denied). Summing all rows
    // therefore inflates disbursed totals far beyond committed amounts.
    //
    // This base query restricts to:
    //   - InvoiceLineStatus IS NULL    — older USAC exports that predate the
    //                                    status field; assumed to be paid records.
    //   - InvoiceLineStatus = "Approved" (case-insensitive) — confirmed payment.
    //
    // "Pending", "Not Approved", "Cancelled", and any other non-final statuses
    // are excluded so that only amounts USAC has actually authorized/paid are
    // included in risk calculations.
    // -----------------------------------------------------------------------
    private IQueryable<Domain.Disbursement> ApprovedDisbursements =>
        db.Disbursements.Where(d =>
            d.InvoiceLineStatus == null ||
            d.InvoiceLineStatus.ToLower() == "approved");

    // -----------------------------------------------------------------------
    // Section 0: Available years (for filter dropdown)
    // -----------------------------------------------------------------------

    public async Task<List<int>> GetAvailableYearsAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = await db.FundingCommitments
            .Select(c => c.FundingYear)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(ct);
        logger?.LogDebug("RiskInsights.GetAvailableYears completed in {ElapsedMs}ms ({Count} years)",
            sw.ElapsedMilliseconds, result.Count);
        return result;
    }

    // -----------------------------------------------------------------------
    // Section 1: National snapshot
    // -----------------------------------------------------------------------

    public async Task<RiskSnapshot> GetSnapshotAsync(int? year = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var commitmentQuery = db.FundingCommitments.AsQueryable();
        if (year.HasValue)
            commitmentQuery = commitmentQuery.Where(c => c.FundingYear == year.Value);

        double totalReqD = (double?)await commitmentQuery
            .Select(c => (double?)c.TotalEligibleAmount).SumAsync(ct) ?? 0.0;

        double totalComD = (double?)await commitmentQuery
            .Select(c => (double?)c.CommittedAmount).SumAsync(ct) ?? 0.0;

        // Disbursements carry their own FundingYear — filter directly so the disbursed
        // total is year-consistent with the commitment totals above.
        // ApprovedDisbursements already excludes Pending/Not-Approved/Cancelled lines.
        var disbursementQuery = ApprovedDisbursements;
        if (year.HasValue)
            disbursementQuery = disbursementQuery.Where(d => d.FundingYear == year.Value);

        double totalDisbD = (double?)await disbursementQuery
            .Select(d => (double?)d.ApprovedAmount).SumAsync(ct) ?? 0.0;

        var totalRequested  = (decimal)totalReqD;
        var totalCommitted  = (decimal)totalComD;
        var totalDisbursed  = (decimal)totalDisbD;

        var commitmentRate  = totalReqD  > 0 ? totalComD  / totalReqD  : 0.0;
        var disbursementRate = totalComD > 0 ? totalDisbD / totalComD  : 0.0;

        var snapshot = new RiskSnapshot(totalRequested, totalCommitted, totalDisbursed,
            Math.Clamp(commitmentRate, 0.0, 1.0),
            Math.Clamp(disbursementRate, 0.0, 1.0));
        logger?.LogDebug(
            "RiskInsights.GetSnapshot completed in {ElapsedMs}ms (year={Year}, req={Req:F0}, com={Com:F0}, disb={Disb:F0})",
            sw.ElapsedMilliseconds, year?.ToString() ?? "all", totalReqD, totalComD, totalDisbD);
        return snapshot;
    }

    // -----------------------------------------------------------------------
    // Section 2: Top risk applicants
    // -----------------------------------------------------------------------

    public async Task<List<ApplicantRiskRow>> GetTopRiskApplicantsAsync(
        int topN = 20, int? year = null, string? severity = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        // Step 1: aggregate commitments by BEN in SQL.
        var commitmentQuery = db.FundingCommitments
            .Where(c => c.ApplicantEntityNumber != null);
        if (year.HasValue)
            commitmentQuery = commitmentQuery.Where(c => c.FundingYear == year.Value);

        var commitments = await commitmentQuery
            .GroupBy(c => c.ApplicantEntityNumber!)
            .Select(g => new
            {
                Ben    = g.Key,
                TotalReq = g.Sum(c => (double?)c.TotalEligibleAmount ?? 0.0),
                TotalCom = g.Sum(c => (double?)c.CommittedAmount     ?? 0.0),
                Name   = g.Select(c => c.ApplicantName).FirstOrDefault(n => n != null),
            })
            .ToListAsync(ct);

        var bens = commitments.Select(r => r.Ben).ToHashSet();

        // Step 2: aggregate disbursements for those BENs in SQL.
        // Filter by year when active — Disbursement.FundingYear keeps this year-consistent
        // with the commitment totals computed above.
        // ApprovedDisbursements already excludes Pending/Not-Approved/Cancelled lines.
        var disbQuery = ApprovedDisbursements
            .Where(d => d.ApplicantEntityNumber != null && bens.Contains(d.ApplicantEntityNumber!));
        if (year.HasValue)
            disbQuery = disbQuery.Where(d => d.FundingYear == year.Value);

        var disbursed = await disbQuery
            .GroupBy(d => d.ApplicantEntityNumber!)
            .Select(g => new { Ben = g.Key, TotalDisb = g.Sum(d => (double?)d.ApprovedAmount ?? 0.0) })
            .ToListAsync(ct);

        var disbLookup = disbursed.ToDictionary(r => r.Ben, r => r.TotalDisb);

        // Step 3: compute risk scores in memory; apply severity filter; take top N.
        var scored = commitments
            .Select(c =>
            {
                var disb    = disbLookup.GetValueOrDefault(c.Ben, 0.0);
                var redPct  = RiskCalculator.ReductionPct(c.TotalReq, c.TotalCom);
                var disbPct = RiskCalculator.DisbursementPct(c.TotalCom, disb);
                var score   = RiskCalculator.ComputeRiskScore(redPct, disbPct);
                var level   = RiskCalculator.ClassifyRisk(score);
                return (c.Ben, c.Name, c.TotalReq, c.TotalCom, disb, redPct, disbPct, score, level);
            })
            .OrderByDescending(r => r.score);

        var filtered = string.IsNullOrEmpty(severity)
            ? scored
            : scored.Where(r => r.level.Equals(severity, StringComparison.OrdinalIgnoreCase));

        var topRows = filtered.Take(topN).ToList();

        // Step 4: fetch states only for the top-N BENs.
        var topBens = topRows.Select(r => r.Ben).ToHashSet();
        var stateLookup = await FetchStatesAsync(topBens, ct);

        var rows = topRows.Select(r => new ApplicantRiskRow(
            r.Ben,
            r.Name,
            stateLookup.GetValueOrDefault(r.Ben),
            (decimal)r.TotalReq,
            (decimal)r.TotalCom,
            (decimal)r.disb,
            r.redPct,
            r.disbPct,
            r.score,
            r.level
        )).ToList();
        logger?.LogDebug(
            "RiskInsights.GetTopRiskApplicants completed in {ElapsedMs}ms (year={Year}, severity={Severity}, rows={Rows})",
            sw.ElapsedMilliseconds, year?.ToString() ?? "all", severity ?? "all", rows.Count);
        return rows;
    }

    // -----------------------------------------------------------------------
    // Section 3: Commitment vs disbursement gap
    // -----------------------------------------------------------------------

    public async Task<List<CommitmentGapRow>> GetTopCommitmentDisbursementGapsAsync(
        int topN = 15, int? year = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var commitmentQuery = db.FundingCommitments
            .Where(c => c.ApplicantEntityNumber != null && c.CommittedAmount > 0);
        if (year.HasValue)
            commitmentQuery = commitmentQuery.Where(c => c.FundingYear == year.Value);

        var commitments = await commitmentQuery
            .GroupBy(c => c.ApplicantEntityNumber!)
            .Select(g => new
            {
                Ben    = g.Key,
                TotalCom = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
                Name   = g.Select(c => c.ApplicantName).FirstOrDefault(n => n != null),
            })
            .ToListAsync(ct);

        var bens = commitments.Select(r => r.Ben).ToHashSet();

        // ApprovedDisbursements already excludes Pending/Not-Approved/Cancelled lines.
        var gapDisbQuery = ApprovedDisbursements
            .Where(d => d.ApplicantEntityNumber != null && bens.Contains(d.ApplicantEntityNumber!));
        if (year.HasValue)
            gapDisbQuery = gapDisbQuery.Where(d => d.FundingYear == year.Value);

        var disbursed = await gapDisbQuery
            .GroupBy(d => d.ApplicantEntityNumber!)
            .Select(g => new { Ben = g.Key, TotalDisb = g.Sum(d => (double?)d.ApprovedAmount ?? 0.0) })
            .ToListAsync(ct);

        var disbLookup = disbursed.ToDictionary(r => r.Ben, r => r.TotalDisb);

        var topRows = commitments
            .Select(c => (c.Ben, c.Name, c.TotalCom, Disb: disbLookup.GetValueOrDefault(c.Ben, 0.0)))
            .Select(r => (r.Ben, r.Name, r.TotalCom, r.Disb, Gap: r.TotalCom - r.Disb))
            .Where(r => r.Gap > 0)
            .OrderByDescending(r => r.Gap)
            .Take(topN)
            .ToList();

        var topBens = topRows.Select(r => r.Ben).ToHashSet();
        var stateLookup = await FetchStatesAsync(topBens, ct);

        var gapRows = topRows.Select(r => new CommitmentGapRow(
            r.Ben,
            r.Name,
            stateLookup.GetValueOrDefault(r.Ben),
            (decimal)r.TotalCom,
            (decimal)r.Disb,
            (decimal)r.Gap
        )).ToList();
        logger?.LogDebug(
            "RiskInsights.GetTopCommitmentDisbursementGaps completed in {ElapsedMs}ms (year={Year}, rows={Rows})",
            sw.ElapsedMilliseconds, year?.ToString() ?? "all", gapRows.Count);
        return gapRows;
    }

    // -----------------------------------------------------------------------
    // Section 4: Reduction rate
    // -----------------------------------------------------------------------

    public async Task<List<ReductionRateRow>> GetTopReductionRatesAsync(
        int topN = 15, int? year = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var commitmentQuery = db.FundingCommitments
            .Where(c => c.ApplicantEntityNumber != null && c.TotalEligibleAmount > 0);
        if (year.HasValue)
            commitmentQuery = commitmentQuery.Where(c => c.FundingYear == year.Value);

        var commitments = await commitmentQuery
            .GroupBy(c => c.ApplicantEntityNumber!)
            .Select(g => new
            {
                Ben    = g.Key,
                TotalReq = g.Sum(c => (double?)c.TotalEligibleAmount ?? 0.0),
                TotalCom = g.Sum(c => (double?)c.CommittedAmount     ?? 0.0),
                Name   = g.Select(c => c.ApplicantName).FirstOrDefault(n => n != null),
            })
            .ToListAsync(ct);

        var topRows = commitments
            .Where(c => c.TotalReq >= MinRequestedThreshold)
            .Select(c => (c.Ben, c.Name, c.TotalReq, c.TotalCom,
                         RedPct: RiskCalculator.ReductionPct(c.TotalReq, c.TotalCom)))
            .OrderByDescending(r => r.RedPct)
            .Take(topN)
            .ToList();

        var topBens = topRows.Select(r => r.Ben).ToHashSet();
        var stateLookup = await FetchStatesAsync(topBens, ct);

        var reductionRows = topRows.Select(r => new ReductionRateRow(
            r.Ben,
            r.Name,
            stateLookup.GetValueOrDefault(r.Ben),
            (decimal)r.TotalReq,
            (decimal)r.TotalCom,
            r.RedPct
        )).ToList();
        logger?.LogDebug(
            "RiskInsights.GetTopReductionRates completed in {ElapsedMs}ms (year={Year}, rows={Rows})",
            sw.ElapsedMilliseconds, year?.ToString() ?? "all", reductionRows.Count);
        return reductionRows;
    }

    // -----------------------------------------------------------------------
    // Shared helper
    // -----------------------------------------------------------------------

    private async Task<Dictionary<string, string?>> FetchStatesAsync(
        HashSet<string> bens, CancellationToken ct)
    {
        var rows = await db.EpcEntities
            .Where(e => bens.Contains(e.EntityNumber))
            .Select(e => new { e.EntityNumber, e.PhysicalState })
            .ToListAsync(ct);
        return rows.ToDictionary(e => e.EntityNumber, e => (string?)e.PhysicalState);
    }
}
