using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure.Reconciliation;

/// <summary>
/// Reads the <c>ApplicantYearDisbursementSummaries</c> table to produce reconciliation
/// summary totals grouped by funding year.
///
/// Row count = number of summary rows (= distinct BENs with at least one ApprovedAmount > 0 row).
/// Distinct applicants = same as row count, minus any null-BEN catch-all row per year.
///
/// Note: amounts here reflect only rows where ApprovedAmount > 0 (the builder's inclusion rule),
/// so Raw vs Summary amount variance is expected and meaningful.
/// </summary>
public sealed class DisbursementSummaryLocalProvider : ILocalSummaryProvider
{
    private readonly AppDbContext db;

    public DisbursementSummaryLocalProvider(AppDbContext db) => this.db = db;

    public string DatasetName => DatasetManifests.Disbursements.Name;

    public async Task<IReadOnlyList<LocalYearTotals>> GetLocalSummaryTotalsAsync(CancellationToken ct = default)
    {
        var rows = await db.ApplicantYearDisbursementSummaries
            .GroupBy(s => s.FundingYear)
            .Select(g => new
            {
                FundingYear          = g.Key,
                RowCount             = (long)g.Count(),
                DistinctApplicants   = (long)g.Count(s => s.ApplicantEntityNumber != null),
                TotalRequestedAmount = (double)g.Sum(s => (double)s.TotalRequestedAmount),
                TotalApprovedAmount  = (double)g.Sum(s => (double)s.TotalApprovedAmount),
            })
            .OrderBy(r => r.FundingYear)
            .ToListAsync(ct);

        return rows.Select(r => new LocalYearTotals
        {
            FundingYear        = r.FundingYear,
            RowCount           = r.RowCount,
            DistinctApplicants = r.DistinctApplicants,
            Amounts = new Dictionary<string, decimal>
            {
                ["RequestedAmount"] = (decimal)r.TotalRequestedAmount,
                ["ApprovedAmount"]  = (decimal)r.TotalApprovedAmount,
            },
        }).ToList();
    }
}
