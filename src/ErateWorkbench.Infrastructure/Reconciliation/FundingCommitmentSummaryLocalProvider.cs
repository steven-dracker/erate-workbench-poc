using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure.Reconciliation;

/// <summary>
/// Reads the <c>ApplicantYearCommitmentSummaries</c> table to produce reconciliation
/// summary totals grouped by funding year.
///
/// Row count = number of summary rows (= distinct BENs that had at least one commitment).
/// Distinct applicants = same as row count, minus any null-BEN catch-all row per year.
/// </summary>
public sealed class FundingCommitmentSummaryLocalProvider : ILocalSummaryProvider
{
    private readonly AppDbContext db;

    public FundingCommitmentSummaryLocalProvider(AppDbContext db) => this.db = db;

    public string DatasetName => DatasetManifests.FundingCommitments.Name;

    public async Task<IReadOnlyList<LocalYearTotals>> GetLocalSummaryTotalsAsync(CancellationToken ct = default)
    {
        var rows = await db.ApplicantYearCommitmentSummaries
            .GroupBy(s => s.FundingYear)
            .Select(g => new
            {
                FundingYear          = g.Key,
                RowCount             = (long)g.Count(),
                // By construction, each non-null BEN has exactly one summary row per year.
                DistinctApplicants   = (long)g.Count(s => s.ApplicantEntityNumber != null),
                TotalEligibleAmount  = (double)g.Sum(s => (double)s.TotalEligibleAmount),
                TotalCommittedAmount = (double)g.Sum(s => (double)s.TotalCommittedAmount),
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
                ["CommittedAmount"]     = (decimal)r.TotalCommittedAmount,
            },
        }).ToList();
    }
}
