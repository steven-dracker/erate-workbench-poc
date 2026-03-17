using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure.Reconciliation;

/// <summary>
/// Queries the local Disbursements table for reconciliation totals.
/// All InvoiceLineStatus values are included so the counts match the raw source.
/// </summary>
public sealed class DisbursementLocalDataProvider : ILocalDataProvider
{
    private readonly AppDbContext db;

    public DisbursementLocalDataProvider(AppDbContext db) => this.db = db;

    public string DatasetName => DatasetManifests.Disbursements.Name;

    public async Task<IReadOnlyList<LocalYearTotals>> GetLocalRawTotalsAsync(CancellationToken ct = default)
    {
        var rows = await db.Disbursements
            .GroupBy(d => d.FundingYear)
            .Select(g => new
            {
                FundingYear        = g.Key,
                RowCount           = (long)g.Count(),
                DistinctApplicants = (long)g.Select(d => d.ApplicantEntityNumber).Distinct().Count(),
                RequestedAmount    = (double?)g.Sum(d => (double?)d.RequestedAmount) ?? 0.0,
                ApprovedAmount     = (double?)g.Sum(d => (double?)d.ApprovedAmount)  ?? 0.0,
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
                ["RequestedAmount"] = (decimal)r.RequestedAmount,
                ["ApprovedAmount"]  = (decimal)r.ApprovedAmount,
            },
        }).ToList();
    }
}
