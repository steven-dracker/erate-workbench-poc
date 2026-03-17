using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure.Reconciliation;

/// <summary>
/// Queries the local FundingCommitments table for reconciliation totals.
/// All commitment statuses are included (no filter) so the count matches the raw source.
/// </summary>
public sealed class FundingCommitmentLocalDataProvider : ILocalDataProvider
{
    private readonly AppDbContext db;

    public FundingCommitmentLocalDataProvider(AppDbContext db) => this.db = db;

    public string DatasetName => DatasetManifests.FundingCommitments.Name;

    public async Task<IReadOnlyList<LocalYearTotals>> GetLocalRawTotalsAsync(CancellationToken ct = default)
    {
        var rows = await db.FundingCommitments
            .GroupBy(c => c.FundingYear)
            .Select(g => new
            {
                FundingYear         = g.Key,
                RowCount            = (long)g.Count(),
                DistinctApplicants  = (long)g.Select(c => c.ApplicantEntityNumber).Distinct().Count(),
                TotalEligibleAmount = (double?)g.Sum(c => (double?)c.TotalEligibleAmount) ?? 0.0,
                CommittedAmount     = (double?)g.Sum(c => (double?)c.CommittedAmount)     ?? 0.0,
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
                ["TotalEligibleAmount"] = (decimal)r.TotalEligibleAmount,
                ["CommittedAmount"]     = (decimal)r.CommittedAmount,
            },
        }).ToList();
    }
}
