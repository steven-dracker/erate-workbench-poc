using System.Diagnostics;
using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure;

/// <summary>
/// Rebuilds the <see cref="ApplicantYearRiskSummary"/> table by merging
/// <see cref="ApplicantYearCommitmentSummary"/> and <see cref="ApplicantYearDisbursementSummary"/>
/// at the grain (FundingYear, ApplicantEntityNumber).
///
/// Merge semantics: full outer join — rows that exist on only one side are preserved
/// with zero-filled absent fields and presence flags set accordingly.
/// SQLite does not support FULL OUTER JOIN, so the merge is performed in memory:
///   1. All commitment summary rows become the left side.
///   2. Matching disbursement rows are joined in.
///   3. Disbursement-only rows (no matching commitment) are appended.
///
/// Derived metrics delegate entirely to <see cref="RiskCalculator"/> so the model
/// stays consistent with the existing risk dashboard calculations:
///   CommitmentOnly (HasDisbursementData=false): DisbursementPct=0 → RiskScore ≥ 0.5
///   DisbursementOnly (HasCommitmentData=false): both inputs are 0 → RiskScore=0.5 (Moderate)
///
/// Name selection rule: commitment name is preferred; disbursement name is used as
/// fallback when no commitment row exists. If both are non-null and differ, the
/// alphabetically earlier (MIN) value is used for determinism.
/// </summary>
public sealed class ApplicantYearRiskSummaryBuilder
{
    private readonly AppDbContext db;
    private readonly ILogger<ApplicantYearRiskSummaryBuilder>? logger;

    public ApplicantYearRiskSummaryBuilder(
        AppDbContext db,
        ILogger<ApplicantYearRiskSummaryBuilder>? logger = null)
    {
        this.db     = db;
        this.logger = logger;
    }

    public async Task<RiskSummaryBuildResult> RebuildAsync(
        int? fundingYear = null,
        CancellationToken cancellationToken = default)
    {
        var sw    = Stopwatch.StartNew();
        var scope = fundingYear.HasValue ? $"FY{fundingYear}" : "all years";
        logger?.LogInformation("[risk-build] Starting risk summary rebuild — scope={Scope}", scope);

        // ── 1. Load commitment summaries ──────────────────────────────────────
        var commitQuery = db.ApplicantYearCommitmentSummaries.AsNoTracking();
        if (fundingYear.HasValue)
            commitQuery = commitQuery.Where(s => s.FundingYear == fundingYear.Value);

        var commitRows = await commitQuery
            .Select(s => new
            {
                s.FundingYear,
                s.ApplicantEntityNumber,
                s.ApplicantEntityName,
                s.TotalEligibleAmount,
                s.TotalCommittedAmount,
                s.CommitmentRowCount,
                DistinctFrnCount = s.DistinctFrnCount,
            })
            .ToListAsync(cancellationToken);

        // ── 2. Load disbursement summaries ────────────────────────────────────
        var disbQuery = db.ApplicantYearDisbursementSummaries.AsNoTracking();
        if (fundingYear.HasValue)
            disbQuery = disbQuery.Where(s => s.FundingYear == fundingYear.Value);

        var disbRows = await disbQuery
            .Select(s => new
            {
                s.FundingYear,
                s.ApplicantEntityNumber,
                s.ApplicantEntityName,
                s.TotalRequestedAmount,
                s.TotalApprovedAmount,
                s.DisbursementRowCount,
                s.DistinctFrnCount,
                s.DistinctInvoiceCount,
            })
            .ToListAsync(cancellationToken);

        logger?.LogInformation(
            "[risk-build] Loaded source rows — scope={Scope}, commitmentRows={CR}, disbursementRows={DR}",
            scope, commitRows.Count, disbRows.Count);

        // ── 3. Build lookup for disbursement rows ─────────────────────────────
        // Key: (FundingYear, ApplicantEntityNumber)
        var disbLookup = disbRows
            .ToDictionary(r => (r.FundingYear, r.ApplicantEntityNumber));

        // ── 4. Merge in memory: start with commitment side ────────────────────
        var now         = DateTime.UtcNow;
        var riskRows    = new List<ApplicantYearRiskSummary>(commitRows.Count + disbRows.Count);
        var matchedKeys = new HashSet<(int, string?)>();

        var matchedCount    = 0;
        var commitOnlyCount = 0;

        foreach (var c in commitRows)
        {
            var key = (c.FundingYear, c.ApplicantEntityNumber);
            matchedKeys.Add(key);

            disbLookup.TryGetValue(key, out var d);

            var hasDisb      = d is not null;
            var eligD        = (double)c.TotalEligibleAmount;
            var committedD   = (double)c.TotalCommittedAmount;
            var approvedDisb = d is not null ? (double)d.TotalApprovedAmount : 0.0;

            var redPct  = RiskCalculator.ReductionPct(eligD, committedD);
            var disbPct = RiskCalculator.DisbursementPct(committedD, approvedDisb);
            var score   = RiskCalculator.ComputeRiskScore(redPct, disbPct);
            var level   = RiskCalculator.ClassifyRisk(score);

            // Name: prefer commitment name; fall back to disbursement name.
            // When both are present and differ, use MIN for determinism.
            var name = PickName(c.ApplicantEntityName, d?.ApplicantEntityName);

            riskRows.Add(new ApplicantYearRiskSummary
            {
                FundingYear                      = c.FundingYear,
                ApplicantEntityNumber            = c.ApplicantEntityNumber,
                ApplicantEntityName              = name,
                TotalEligibleAmount              = c.TotalEligibleAmount,
                TotalCommittedAmount             = c.TotalCommittedAmount,
                CommitmentRowCount               = c.CommitmentRowCount,
                DistinctCommitmentFrnCount       = c.DistinctFrnCount,
                TotalRequestedDisbursementAmount = d?.TotalRequestedAmount ?? 0m,
                TotalApprovedDisbursementAmount  = d?.TotalApprovedAmount  ?? 0m,
                DisbursementRowCount             = d?.DisbursementRowCount ?? 0,
                DistinctDisbursementFrnCount     = d?.DistinctFrnCount     ?? 0,
                DistinctInvoiceCount             = d?.DistinctInvoiceCount ?? 0,
                HasCommitmentData                = true,
                HasDisbursementData              = hasDisb,
                ReductionPct                     = redPct,
                DisbursementPct                  = disbPct,
                RiskScore                        = score,
                RiskLevel                        = level,
                ImportedAtUtc                    = now,
            });

            if (hasDisb) matchedCount++;
            else         commitOnlyCount++;
        }

        // ── 5. Append disbursement-only rows ──────────────────────────────────
        var disbOnlyCount = 0;
        foreach (var d in disbRows)
        {
            var key = (d.FundingYear, d.ApplicantEntityNumber);
            if (matchedKeys.Contains(key)) continue;

            // No commitment data: both eligible and committed are 0.
            // RiskCalculator yields: ReductionPct=0, DisbursementPct=0, Score=0.5 → Moderate.
            const double redPct  = 0.0;
            const double disbPct = 0.0;
            var          score   = RiskCalculator.ComputeRiskScore(redPct, disbPct);
            var          level   = RiskCalculator.ClassifyRisk(score);

            riskRows.Add(new ApplicantYearRiskSummary
            {
                FundingYear                      = d.FundingYear,
                ApplicantEntityNumber            = d.ApplicantEntityNumber,
                ApplicantEntityName              = d.ApplicantEntityName,
                TotalEligibleAmount              = 0m,
                TotalCommittedAmount             = 0m,
                CommitmentRowCount               = 0,
                DistinctCommitmentFrnCount       = 0,
                TotalRequestedDisbursementAmount = d.TotalRequestedAmount,
                TotalApprovedDisbursementAmount  = d.TotalApprovedAmount,
                DisbursementRowCount             = d.DisbursementRowCount,
                DistinctDisbursementFrnCount     = d.DistinctFrnCount,
                DistinctInvoiceCount             = d.DistinctInvoiceCount,
                HasCommitmentData                = false,
                HasDisbursementData              = true,
                ReductionPct                     = redPct,
                DisbursementPct                  = disbPct,
                RiskScore                        = score,
                RiskLevel                        = level,
                ImportedAtUtc                    = now,
            });

            disbOnlyCount++;
        }

        logger?.LogInformation(
            "[risk-build] Merge complete — scope={Scope}, matched={Matched}, " +
            "commitmentOnly={CommitOnly}, disbursementOnly={DisbOnly}, total={Total}",
            scope, matchedCount, commitOnlyCount, disbOnlyCount, riskRows.Count);

        // ── 6. Delete existing rows for the target scope ──────────────────────
        var deleteQuery = db.ApplicantYearRiskSummaries.AsQueryable();
        if (fundingYear.HasValue)
            deleteQuery = deleteQuery.Where(s => s.FundingYear == fundingYear.Value);
        await deleteQuery.ExecuteDeleteAsync(cancellationToken);
        logger?.LogInformation("[risk-build] Existing rows deleted — scope={Scope}", scope);

        // ── 7. Insert rebuilt rows in batches ─────────────────────────────────
        const int batchSize = 1000;
        var written = 0;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            for (var i = 0; i < riskRows.Count; i += batchSize)
            {
                var batch = riskRows.GetRange(i, Math.Min(batchSize, riskRows.Count - i));
                db.ApplicantYearRiskSummaries.AddRange(batch);
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
                written += batch.Count;
            }
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        sw.Stop();
        logger?.LogInformation(
            "[risk-build] Completed — scope={Scope}, commitmentRowsRead={CR}, disbursementRowsRead={DR}, " +
            "matched={Matched}, commitmentOnly={CommitOnly}, disbursementOnly={DisbOnly}, " +
            "riskRowsWritten={Written}, elapsed={ElapsedMs}ms",
            scope, commitRows.Count, disbRows.Count,
            matchedCount, commitOnlyCount, disbOnlyCount, written, sw.ElapsedMilliseconds);

        return new RiskSummaryBuildResult
        {
            FundingYearScope          = fundingYear,
            CommitmentSummaryRowsRead = commitRows.Count,
            DisbursementSummaryRowsRead = disbRows.Count,
            MatchedRows               = matchedCount,
            CommitmentOnlyRows        = commitOnlyCount,
            DisbursementOnlyRows      = disbOnlyCount,
            RiskSummaryRowsWritten    = written,
            ElapsedMs                 = sw.ElapsedMilliseconds,
        };
    }

    /// <summary>
    /// Returns the stable display name given a commitment-side name and a disbursement-side name.
    /// Rule: use the non-null value; if both are non-null and differ, prefer the commitment name
    /// (primary source) but compare with null-safe string.Compare to break exact-same-string ties.
    /// Falls back to MIN (alphabetically earlier) for full determinism.
    /// </summary>
    private static string? PickName(string? commitName, string? disbName)
    {
        if (commitName is null) return disbName;
        if (disbName   is null) return commitName;
        // Both present: commitment name is authoritative unless it is alphabetically later.
        // Using MIN keeps behaviour consistent with the summary builders.
        return string.Compare(commitName, disbName, StringComparison.OrdinalIgnoreCase) <= 0
            ? commitName
            : disbName;
    }
}

public sealed class RiskSummaryBuildResult
{
    /// <summary>Null means all years were rebuilt.</summary>
    public int? FundingYearScope { get; init; }

    public int CommitmentSummaryRowsRead   { get; init; }
    public int DisbursementSummaryRowsRead { get; init; }

    /// <summary>Rows present in both commitment and disbursement summaries.</summary>
    public int MatchedRows           { get; init; }

    /// <summary>Rows present only in the commitment summary (no matching disbursement row).</summary>
    public int CommitmentOnlyRows    { get; init; }

    /// <summary>Rows present only in the disbursement summary (no matching commitment row).</summary>
    public int DisbursementOnlyRows  { get; init; }

    public int  RiskSummaryRowsWritten { get; init; }
    public long ElapsedMs              { get; init; }
}
