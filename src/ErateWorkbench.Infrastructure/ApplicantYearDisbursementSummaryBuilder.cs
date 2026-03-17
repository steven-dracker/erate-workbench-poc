using System.Diagnostics;
using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure;

/// <summary>
/// Rebuilds the <see cref="ApplicantYearDisbursementSummary"/> table from raw Disbursements data.
/// Groups by (FundingYear, ApplicantEntityNumber) and writes one summary row per pair.
///
/// Inclusion rule: only rows where ApprovedAmount > 0 are aggregated.
/// Rows with null, zero, or unparseable ApprovedAmount are skipped and logged.
/// No InvoiceLineStatus filter is applied.
///
/// Usage:
///   Rebuild one year:  await builder.RebuildAsync(fundingYear: 2021, ct)
///   Rebuild all years: await builder.RebuildAsync(ct: ct)
/// </summary>
public sealed class ApplicantYearDisbursementSummaryBuilder
{
    private readonly AppDbContext db;
    private readonly ILogger<ApplicantYearDisbursementSummaryBuilder>? logger;

    public ApplicantYearDisbursementSummaryBuilder(
        AppDbContext db,
        ILogger<ApplicantYearDisbursementSummaryBuilder>? logger = null)
    {
        this.db     = db;
        this.logger = logger;
    }

    public async Task<DisbursementSummaryBuildResult> RebuildAsync(
        int? fundingYear = null,
        CancellationToken cancellationToken = default)
    {
        var sw    = Stopwatch.StartNew();
        var scope = fundingYear.HasValue ? $"FY{fundingYear}" : "all years";
        logger?.LogInformation("[summary-build] Starting disbursement summary rebuild — scope={Scope}", scope);

        // ── 1. Load raw rows ─────────────────────────────────────────────────
        // Pull only the fields needed for aggregation.
        // In-memory grouping avoids EF Core translation limitations for
        // COUNT(DISTINCT ...) inside GroupBy + Select projections.
        var rawQuery = db.Disbursements.AsNoTracking();
        if (fundingYear.HasValue)
            rawQuery = rawQuery.Where(d => d.FundingYear == fundingYear.Value);

        var rawRows = await rawQuery
            .Select(d => new
            {
                d.FundingYear,
                d.ApplicantEntityNumber,
                d.ApplicantEntityName,
                d.FundingRequestNumber,
                d.InvoiceId,
                RequestedAmount = d.RequestedAmount ?? 0m,
                ApprovedAmount  = d.ApprovedAmount  ?? 0m,
            })
            .ToListAsync(cancellationToken);

        var rawRowsScanned = rawRows.Count;

        // ── 2. Apply inclusion rule: ApprovedAmount > 0 ──────────────────────
        var validRows   = rawRows.Where(r => r.ApprovedAmount > 0).ToList();
        var skippedRows = rawRowsScanned - validRows.Count;

        if (skippedRows > 0)
            logger?.LogInformation(
                "[summary-build] Skipped rows with ApprovedAmount <= 0 — scope={Scope}, skipped={Skipped}",
                scope, skippedRows);

        logger?.LogInformation(
            "[summary-build] Raw rows loaded — scope={Scope}, total={Total}, included={Included}",
            scope, rawRowsScanned, validRows.Count);

        // ── 3. Group in memory ───────────────────────────────────────────────
        var now = DateTime.UtcNow;
        var summaryRows = validRows
            .GroupBy(r => (r.FundingYear, r.ApplicantEntityNumber))
            .Select(g => new ApplicantYearDisbursementSummary
            {
                FundingYear           = g.Key.FundingYear,
                ApplicantEntityNumber = g.Key.ApplicantEntityNumber,
                ApplicantEntityName   = g.Min(r => r.ApplicantEntityName),
                TotalRequestedAmount  = g.Sum(r => r.RequestedAmount),
                TotalApprovedAmount   = g.Sum(r => r.ApprovedAmount),
                DisbursementRowCount  = g.Count(),
                DistinctFrnCount      = g.Select(r => r.FundingRequestNumber).Distinct().Count(),
                DistinctInvoiceCount  = g.Select(r => r.InvoiceId)
                                         .Where(id => id != null)
                                         .Distinct()
                                         .Count(),
                ImportedAtUtc         = now,
            })
            .ToList();

        var totalRequested = summaryRows.Sum(s => s.TotalRequestedAmount);
        var totalApproved  = summaryRows.Sum(s => s.TotalApprovedAmount);

        logger?.LogInformation(
            "[summary-build] Grouped — scope={Scope}, summaryRows={Rows}, " +
            "totalRequested={TotalRequested:N0}, totalApproved={TotalApproved:N0}",
            scope, summaryRows.Count, totalRequested, totalApproved);

        // ── 4. Delete existing rows for the target scope ─────────────────────
        var deleteQuery = db.ApplicantYearDisbursementSummaries.AsQueryable();
        if (fundingYear.HasValue)
            deleteQuery = deleteQuery.Where(s => s.FundingYear == fundingYear.Value);

        await deleteQuery.ExecuteDeleteAsync(cancellationToken);
        logger?.LogInformation("[summary-build] Existing rows deleted — scope={Scope}", scope);

        // ── 5. Insert rebuilt rows in batches ────────────────────────────────
        const int batchSize = 1000;
        var written = 0;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            for (var i = 0; i < summaryRows.Count; i += batchSize)
            {
                var batch = summaryRows.GetRange(i, Math.Min(batchSize, summaryRows.Count - i));
                db.ApplicantYearDisbursementSummaries.AddRange(batch);
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
            "[summary-build] Completed — scope={Scope}, rawScanned={Raw}, included={Included}, " +
            "summaryWritten={Written}, totalRequested={TotalRequested:N0}, totalApproved={TotalApproved:N0}, " +
            "elapsed={ElapsedMs}ms",
            scope, rawRowsScanned, validRows.Count, written,
            totalRequested, totalApproved, sw.ElapsedMilliseconds);

        return new DisbursementSummaryBuildResult
        {
            FundingYearScope     = fundingYear,
            RawRowsScanned       = rawRowsScanned,
            IncludedRows         = validRows.Count,
            SummaryRowsWritten   = written,
            TotalRequestedAmount = totalRequested,
            TotalApprovedAmount  = totalApproved,
            ElapsedMs            = sw.ElapsedMilliseconds,
        };
    }
}

public sealed class DisbursementSummaryBuildResult
{
    /// <summary>Null means all years were rebuilt.</summary>
    public int? FundingYearScope { get; init; }

    /// <summary>Total raw rows loaded from Disbursements (before ApprovedAmount > 0 filter).</summary>
    public int RawRowsScanned { get; init; }

    /// <summary>Rows that passed the ApprovedAmount > 0 inclusion rule.</summary>
    public int IncludedRows { get; init; }

    public int SummaryRowsWritten { get; init; }
    public decimal TotalRequestedAmount { get; init; }
    public decimal TotalApprovedAmount { get; init; }
    public long ElapsedMs { get; init; }
}
