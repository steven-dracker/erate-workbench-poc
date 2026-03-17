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

public record AdvisorySignalDto(
    string EntityNumber,
    string? ApplicantEntityName,
    int FundingYear,
    decimal TotalCommittedAmount,
    decimal TotalApprovedDisbursementAmount,
    double ReductionPct,
    double DisbursementPct,
    string RiskLevel,
    string AnomalyType);

// ---------------------------------------------------------------------------
// Repository
// ---------------------------------------------------------------------------

/// <summary>
/// Risk queries backed entirely by <see cref="Domain.ApplicantYearRiskSummary"/>.
///
/// The summary table is the single authoritative source for all risk page data.
/// It is produced by <see cref="ApplicantYearRiskSummaryBuilder"/>, which merges
/// ApplicantYearCommitmentSummary and ApplicantYearDisbursementSummary with full-
/// outer-join semantics and pre-computes ReductionPct, DisbursementPct, RiskScore,
/// and RiskLevel using <see cref="RiskCalculator"/>.
///
/// This repository does NOT query FundingCommitments or Disbursements directly.
/// InvoiceLineStatus filtering and disbursement inclusion rules are responsibilities
/// of the builder pipeline (tested in DisbursementSummaryBuilderTests).
///
/// State lookup (PhysicalState) is still fetched from EpcEntities for the top-N
/// result rows, keeping the WHERE IN small.
/// </summary>
public class RiskInsightsRepository(AppDbContext db, ILogger<RiskInsightsRepository>? logger = null)
{
    // Minimum eligible amount to include an entity in reduction-rate analysis.
    private const double MinRequestedThreshold = 100.0;

    // -----------------------------------------------------------------------
    // Available years (for filter dropdown)
    // -----------------------------------------------------------------------

    public async Task<List<int>> GetAvailableYearsAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = await db.ApplicantYearRiskSummaries
            .Select(r => r.FundingYear)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(ct);
        logger?.LogDebug("RiskInsights.GetAvailableYears completed in {ElapsedMs}ms ({Count} years)",
            sw.ElapsedMilliseconds, result.Count);
        return result;
    }

    // -----------------------------------------------------------------------
    // National snapshot
    // -----------------------------------------------------------------------

    public async Task<RiskSnapshot> GetSnapshotAsync(int? year = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var query = db.ApplicantYearRiskSummaries.AsQueryable();
        if (year.HasValue)
            query = query.Where(r => r.FundingYear == year.Value);

        // Three separate SUM queries — each translates to a single SQL aggregate.
        // The (double?) cast pattern works around EF Core + SQLite decimal-as-TEXT
        // by projecting to REAL before summing, consistent with the rest of the codebase.
        var totalEligD = (double?)await query.Select(r => (double?)r.TotalEligibleAmount).SumAsync(ct) ?? 0.0;
        var totalComD  = (double?)await query.Select(r => (double?)r.TotalCommittedAmount).SumAsync(ct) ?? 0.0;
        var totalDisbD = (double?)await query.Select(r => (double?)r.TotalApprovedDisbursementAmount).SumAsync(ct) ?? 0.0;

        var commitmentRate   = totalEligD > 0 ? Math.Clamp(totalComD  / totalEligD, 0.0, 1.0) : 0.0;
        var disbursementRate = totalComD  > 0 ? Math.Clamp(totalDisbD / totalComD,  0.0, 1.0) : 0.0;

        var snapshot = new RiskSnapshot(
            (decimal)totalEligD,
            (decimal)totalComD,
            (decimal)totalDisbD,
            commitmentRate,
            disbursementRate);

        logger?.LogDebug(
            "RiskInsights.GetSnapshot completed in {ElapsedMs}ms (year={Year}, req={Req:F0}, com={Com:F0}, disb={Disb:F0})",
            sw.ElapsedMilliseconds, year?.ToString() ?? "all", totalEligD, totalComD, totalDisbD);
        return snapshot;
    }

    // -----------------------------------------------------------------------
    // Top risk applicants
    // -----------------------------------------------------------------------

    public async Task<List<ApplicantRiskRow>> GetTopRiskApplicantsAsync(
        int topN = 20, int? year = null, string? severity = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var query = db.ApplicantYearRiskSummaries
            .Where(r => r.ApplicantEntityNumber != null);

        if (year.HasValue)
            query = query.Where(r => r.FundingYear == year.Value);

        if (!string.IsNullOrEmpty(severity))
        {
            var severityLower = severity.ToLowerInvariant();
            query = query.Where(r => r.RiskLevel.ToLower() == severityLower);
        }

        var topRows = await query
            .OrderByDescending(r => r.RiskScore)
            .Take(topN)
            .Select(r => new
            {
                r.ApplicantEntityNumber,
                r.ApplicantEntityName,
                r.TotalEligibleAmount,
                r.TotalCommittedAmount,
                r.TotalApprovedDisbursementAmount,
                r.ReductionPct,
                r.DisbursementPct,
                r.RiskScore,
                r.RiskLevel,
            })
            .ToListAsync(ct);

        var topBens    = topRows.Select(r => r.ApplicantEntityNumber!).ToHashSet();
        var stateLookup = await FetchStatesAsync(topBens, ct);

        var rows = topRows.Select(r => new ApplicantRiskRow(
            r.ApplicantEntityNumber!,
            r.ApplicantEntityName,
            stateLookup.GetValueOrDefault(r.ApplicantEntityNumber!),
            r.TotalEligibleAmount,
            r.TotalCommittedAmount,
            r.TotalApprovedDisbursementAmount,
            r.ReductionPct,
            r.DisbursementPct,
            r.RiskScore,
            r.RiskLevel
        )).ToList();

        logger?.LogDebug(
            "RiskInsights.GetTopRiskApplicants completed in {ElapsedMs}ms (year={Year}, severity={Severity}, rows={Rows})",
            sw.ElapsedMilliseconds, year?.ToString() ?? "all", severity ?? "all", rows.Count);
        return rows;
    }

    // -----------------------------------------------------------------------
    // Commitment vs disbursement gap
    // -----------------------------------------------------------------------

    public async Task<List<CommitmentGapRow>> GetTopCommitmentDisbursementGapsAsync(
        int topN = 15, int? year = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var query = db.ApplicantYearRiskSummaries
            .Where(r => r.ApplicantEntityNumber != null && r.TotalCommittedAmount > 0);

        if (year.HasValue)
            query = query.Where(r => r.FundingYear == year.Value);

        // Committed and approved amounts are loaded for the committed-only pre-filtered set.
        // Gap filtering and ordering are done in memory: EF Core + SQLite cannot translate
        // decimal column subtraction (TEXT affinity) in ORDER BY expressions to SQL.
        var items = await query
            .Select(r => new
            {
                r.ApplicantEntityNumber,
                r.ApplicantEntityName,
                r.TotalCommittedAmount,
                r.TotalApprovedDisbursementAmount,
            })
            .ToListAsync(ct);

        var topRows = items
            .Select(r => new
            {
                r.ApplicantEntityNumber,
                r.ApplicantEntityName,
                r.TotalCommittedAmount,
                r.TotalApprovedDisbursementAmount,
                Gap = r.TotalCommittedAmount - r.TotalApprovedDisbursementAmount,
            })
            .Where(r => r.Gap > 0)
            .OrderByDescending(r => r.Gap)
            .Take(topN)
            .ToList();

        var topBens    = topRows.Select(r => r.ApplicantEntityNumber!).ToHashSet();
        var stateLookup = await FetchStatesAsync(topBens, ct);

        var gapRows = topRows.Select(r => new CommitmentGapRow(
            r.ApplicantEntityNumber!,
            r.ApplicantEntityName,
            stateLookup.GetValueOrDefault(r.ApplicantEntityNumber!),
            r.TotalCommittedAmount,
            r.TotalApprovedDisbursementAmount,
            r.TotalCommittedAmount - r.TotalApprovedDisbursementAmount
        )).ToList();

        logger?.LogDebug(
            "RiskInsights.GetTopCommitmentDisbursementGaps completed in {ElapsedMs}ms (year={Year}, rows={Rows})",
            sw.ElapsedMilliseconds, year?.ToString() ?? "all", gapRows.Count);
        return gapRows;
    }

    // -----------------------------------------------------------------------
    // Reduction rate
    // -----------------------------------------------------------------------

    public async Task<List<ReductionRateRow>> GetTopReductionRatesAsync(
        int topN = 15, int? year = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var threshold = (decimal)MinRequestedThreshold;
        var query = db.ApplicantYearRiskSummaries
            .Where(r => r.ApplicantEntityNumber != null && r.TotalEligibleAmount >= threshold);

        if (year.HasValue)
            query = query.Where(r => r.FundingYear == year.Value);

        var topRows = await query
            .OrderByDescending(r => r.ReductionPct)
            .Take(topN)
            .Select(r => new
            {
                r.ApplicantEntityNumber,
                r.ApplicantEntityName,
                r.TotalEligibleAmount,
                r.TotalCommittedAmount,
                r.ReductionPct,
            })
            .ToListAsync(ct);

        var topBens    = topRows.Select(r => r.ApplicantEntityNumber!).ToHashSet();
        var stateLookup = await FetchStatesAsync(topBens, ct);

        var reductionRows = topRows.Select(r => new ReductionRateRow(
            r.ApplicantEntityNumber!,
            r.ApplicantEntityName,
            stateLookup.GetValueOrDefault(r.ApplicantEntityNumber!),
            r.TotalEligibleAmount,
            r.TotalCommittedAmount,
            r.ReductionPct
        )).ToList();

        logger?.LogDebug(
            "RiskInsights.GetTopReductionRates completed in {ElapsedMs}ms (year={Year}, rows={Rows})",
            sw.ElapsedMilliseconds, year?.ToString() ?? "all", reductionRows.Count);
        return reductionRows;
    }

    // -----------------------------------------------------------------------
    // Advisory signals
    // -----------------------------------------------------------------------

    public async Task<List<AdvisorySignalDto>> GetAdvisorySignalsAsync(
        int? year = null, int topN = 25, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var query = db.ApplicantYearRiskSummaries
            .Where(r => r.ApplicantEntityNumber != null);

        if (year.HasValue)
            query = query.Where(r => r.FundingYear == year.Value);

        // Load rows that match at least one advisory condition.
        // All filter predicates use double/bool columns — fully SQL-translated.
        var rows = await query
            .Where(r =>
                (!r.HasCommitmentData && r.HasDisbursementData) ||
                (r.HasCommitmentData  && !r.HasDisbursementData) ||
                r.ReductionPct > 0.5 ||
                (r.DisbursementPct < 0.5 && r.HasCommitmentData))
            .Select(r => new
            {
                r.ApplicantEntityNumber,
                r.ApplicantEntityName,
                r.FundingYear,
                r.TotalCommittedAmount,
                r.TotalApprovedDisbursementAmount,
                r.ReductionPct,
                r.DisbursementPct,
                r.RiskLevel,
                r.RiskScore,
                r.HasCommitmentData,
                r.HasDisbursementData,
            })
            .ToListAsync(ct);

        // Expand each row into one signal per matching condition.
        var signals = new List<(double Score, AdvisorySignalDto Dto)>(rows.Count * 2);
        foreach (var r in rows)
        {
            if (!r.HasCommitmentData && r.HasDisbursementData)
                signals.Add((r.RiskScore, new AdvisorySignalDto(
                    r.ApplicantEntityNumber!, r.ApplicantEntityName, r.FundingYear,
                    r.TotalCommittedAmount, r.TotalApprovedDisbursementAmount,
                    r.ReductionPct, r.DisbursementPct, r.RiskLevel, "No Commitment")));

            if (r.HasCommitmentData && !r.HasDisbursementData)
                signals.Add((r.RiskScore, new AdvisorySignalDto(
                    r.ApplicantEntityNumber!, r.ApplicantEntityName, r.FundingYear,
                    r.TotalCommittedAmount, r.TotalApprovedDisbursementAmount,
                    r.ReductionPct, r.DisbursementPct, r.RiskLevel, "No Disbursement")));

            if (r.ReductionPct > 0.5)
                signals.Add((r.RiskScore, new AdvisorySignalDto(
                    r.ApplicantEntityNumber!, r.ApplicantEntityName, r.FundingYear,
                    r.TotalCommittedAmount, r.TotalApprovedDisbursementAmount,
                    r.ReductionPct, r.DisbursementPct, r.RiskLevel, "High Reduction")));

            if (r.DisbursementPct < 0.5 && r.HasCommitmentData)
                signals.Add((r.RiskScore, new AdvisorySignalDto(
                    r.ApplicantEntityNumber!, r.ApplicantEntityName, r.FundingYear,
                    r.TotalCommittedAmount, r.TotalApprovedDisbursementAmount,
                    r.ReductionPct, r.DisbursementPct, r.RiskLevel, "Low Utilization")));
        }

        var result = signals
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Dto.TotalCommittedAmount)
            .Take(topN)
            .Select(x => x.Dto)
            .ToList();

        logger?.LogDebug(
            "RiskInsights.GetAdvisorySignals completed in {ElapsedMs}ms (year={Year}, signalRows={Rows})",
            sw.ElapsedMilliseconds, year?.ToString() ?? "all", result.Count);
        return result;
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
