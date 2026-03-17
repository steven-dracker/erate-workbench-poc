using System.Diagnostics;
using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure;

/// <summary>
/// Rebuilds the <see cref="ApplicantYearCommitmentSummary"/> table from raw FundingCommitments data.
/// Groups raw rows by (FundingYear, ApplicantEntityNumber) and writes one summary row per pair.
///
/// Usage:
///   - Rebuild one year: await builder.RebuildAsync(fundingYear: 2021, ct)
///   - Rebuild all years: await builder.RebuildAsync(ct: ct)
///
/// The build is destructive for the targeted year(s): existing summary rows are deleted
/// before new rows are inserted. This keeps the table consistent after partial imports.
/// </summary>
public sealed class ApplicantYearCommitmentSummaryBuilder
{
    private readonly AppDbContext db;
    private readonly ILogger<ApplicantYearCommitmentSummaryBuilder>? logger;

    public ApplicantYearCommitmentSummaryBuilder(
        AppDbContext db,
        ILogger<ApplicantYearCommitmentSummaryBuilder>? logger = null)
    {
        this.db     = db;
        this.logger = logger;
    }

    public async Task<CommitmentSummaryBuildResult> RebuildAsync(
        int? fundingYear = null,
        CancellationToken cancellationToken = default)
    {
        var sw    = Stopwatch.StartNew();
        var scope = fundingYear.HasValue ? $"FY{fundingYear}" : "all years";
        logger?.LogInformation("[summary-build] Starting commitment summary rebuild — scope={Scope}", scope);

        // ── 1. Load raw rows ─────────────────────────────────────────────────
        // Pull only the fields needed for aggregation (lightweight projection).
        // In-memory grouping avoids EF Core translation limitations for
        // COUNT(DISTINCT ...) inside GroupBy + Select projections.
        var rawQuery = db.FundingCommitments.AsNoTracking();
        if (fundingYear.HasValue)
            rawQuery = rawQuery.Where(c => c.FundingYear == fundingYear.Value);

        var rawRows = await rawQuery
            .Select(c => new
            {
                c.FundingYear,
                c.ApplicantEntityNumber,
                c.ApplicantName,
                c.FundingRequestNumber,
                TotalEligibleAmount = c.TotalEligibleAmount ?? 0m,
                CommittedAmount     = c.CommittedAmount     ?? 0m,
            })
            .ToListAsync(cancellationToken);

        var rawRowsScanned = rawRows.Count;
        logger?.LogInformation(
            "[summary-build] Raw rows loaded — scope={Scope}, count={Count}", scope, rawRowsScanned);

        // ── 2. Group in memory ───────────────────────────────────────────────
        var now = DateTime.UtcNow;
        var summaryRows = rawRows
            .GroupBy(r => (r.FundingYear, r.ApplicantEntityNumber))
            .Select(g => new ApplicantYearCommitmentSummary
            {
                FundingYear           = g.Key.FundingYear,
                ApplicantEntityNumber = g.Key.ApplicantEntityNumber,
                // MIN gives a deterministic, stable name without pulling all distinct values.
                ApplicantEntityName   = g.Min(r => r.ApplicantName),
                TotalEligibleAmount   = g.Sum(r => r.TotalEligibleAmount),
                TotalCommittedAmount  = g.Sum(r => r.CommittedAmount),
                CommitmentRowCount    = g.Count(),
                DistinctFrnCount      = g.Select(r => r.FundingRequestNumber).Distinct().Count(),
                ImportedAtUtc         = now,
            })
            .ToList();

        var totalEligible  = summaryRows.Sum(s => s.TotalEligibleAmount);
        var totalCommitted = summaryRows.Sum(s => s.TotalCommittedAmount);

        logger?.LogInformation(
            "[summary-build] Grouped — scope={Scope}, summaryRows={Rows}, " +
            "totalEligible={TotalEligible:N0}, totalCommitted={TotalCommitted:N0}",
            scope, summaryRows.Count, totalEligible, totalCommitted);

        // ── 3. Delete existing rows for the target scope ─────────────────────
        var deleteQuery = db.ApplicantYearCommitmentSummaries.AsQueryable();
        if (fundingYear.HasValue)
            deleteQuery = deleteQuery.Where(s => s.FundingYear == fundingYear.Value);

        await deleteQuery.ExecuteDeleteAsync(cancellationToken);
        logger?.LogInformation("[summary-build] Existing rows deleted — scope={Scope}", scope);

        // ── 4. Insert rebuilt rows in batches ────────────────────────────────
        const int batchSize = 1000;
        var written = 0;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            for (var i = 0; i < summaryRows.Count; i += batchSize)
            {
                var batch = summaryRows.GetRange(i, Math.Min(batchSize, summaryRows.Count - i));
                db.ApplicantYearCommitmentSummaries.AddRange(batch);
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
            "[summary-build] Completed — scope={Scope}, rawScanned={Raw}, summaryWritten={Written}, " +
            "totalEligible={TotalEligible:N0}, totalCommitted={TotalCommitted:N0}, elapsed={ElapsedMs}ms",
            scope, rawRowsScanned, written, totalEligible, totalCommitted, sw.ElapsedMilliseconds);

        return new CommitmentSummaryBuildResult
        {
            FundingYearScope      = fundingYear,
            RawRowsScanned        = rawRowsScanned,
            SummaryRowsWritten    = written,
            TotalEligibleAmount   = totalEligible,
            TotalCommittedAmount  = totalCommitted,
            ElapsedMs             = sw.ElapsedMilliseconds,
        };
    }
}

public sealed class CommitmentSummaryBuildResult
{
    /// <summary>Null means all years were rebuilt.</summary>
    public int? FundingYearScope { get; init; }
    public int RawRowsScanned { get; init; }
    public int SummaryRowsWritten { get; init; }
    public decimal TotalEligibleAmount { get; init; }
    public decimal TotalCommittedAmount { get; init; }
    public long ElapsedMs { get; init; }
}
