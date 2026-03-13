using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

public class ServiceProviderRepository(AppDbContext db)
{
    /// <summary>
    /// Upserts a batch of service providers keyed on <see cref="ServiceProvider.Spin"/>.
    /// Deduplicates within the batch (last occurrence wins) before hitting the database.
    /// Returns the number of records inserted and updated in this batch.
    /// </summary>
    public async Task<(int Inserted, int Updated)> UpsertBatchAsync(
        IEnumerable<ServiceProvider> incoming,
        CancellationToken cancellationToken = default)
    {
        var deduplicated = incoming
            .GroupBy(sp => sp.Spin)
            .Select(g => g.Last())
            .ToList();

        var spins = deduplicated.Select(sp => sp.Spin).ToHashSet();

        var existing = await db.ServiceProviders
            .Where(sp => spins.Contains(sp.Spin))
            .ToDictionaryAsync(sp => sp.Spin, cancellationToken);

        int inserted = 0;
        int updated = 0;

        foreach (var record in deduplicated)
        {
            if (existing.TryGetValue(record.Spin, out var current))
            {
                current.ProviderName = record.ProviderName;
                current.Status = record.Status;
                current.Phone = record.Phone;
                current.Email = record.Email;
                current.Website = record.Website;
                current.Address = record.Address;
                current.City = record.City;
                current.State = record.State;
                current.Zip = record.Zip;
                current.UpdatedAtUtc = DateTime.UtcNow;
                updated++;
            }
            else
            {
                db.ServiceProviders.Add(record);
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return (inserted, updated);
    }

    // --- Analytics queries ---

    /// <summary>
    /// Top service providers by total committed amount, joined from FundingCommitments.
    /// Providers with no linked commitments are excluded.
    /// </summary>
    public async Task<List<(string Spin, string ProviderName, int CommitmentCount, decimal TotalCommitted)>>
        GetTopByCommittedAmountAsync(int topN = 20, CancellationToken cancellationToken = default)
    {
        // Step 1: aggregate from FundingCommitments — pushed to SQL.
        // Cast to double because SQLite's EF Core provider does not support Sum on decimal.
        var stats = await db.FundingCommitments
            .Where(fc => fc.ServiceProviderSpin != null)
            .GroupBy(fc => fc.ServiceProviderSpin!)
            .Select(g => new
            {
                Spin = g.Key,
                CommitmentCount = g.Count(),
                TotalCommittedDouble = g.Sum(fc => (double?)fc.CommittedAmount ?? 0.0),
            })
            .OrderByDescending(x => x.TotalCommittedDouble)
            .Take(topN)
            .ToListAsync(cancellationToken);

        if (stats.Count == 0)
            return [];

        // Step 2: fetch provider names for those SPINs in a single query
        var spins = stats.Select(s => s.Spin).ToList();
        var names = await db.ServiceProviders
            .Where(sp => spins.Contains(sp.Spin))
            .ToDictionaryAsync(sp => sp.Spin, sp => sp.ProviderName, cancellationToken);

        return stats
            .Select(s => (s.Spin, names.GetValueOrDefault(s.Spin, s.Spin), s.CommitmentCount, (decimal)s.TotalCommittedDouble))
            .ToList();
    }

    /// <summary>
    /// Count of distinct service providers and commitments per applicant state.
    /// Joins FundingCommitments → EpcEntities on ApplicantEntityNumber = EntityNumber,
    /// then groups by the applicant's PhysicalState.
    /// Only rows where ServiceProviderSpin, ApplicantEntityNumber, and PhysicalState are all non-null are included.
    /// </summary>
    public async Task<List<(string State, int ProviderCount, int CommitmentCount)>>
        GetProvidersByApplicantStateAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.FundingCommitments
            .Where(fc => fc.ServiceProviderSpin != null && fc.ApplicantEntityNumber != null)
            .Join(db.EpcEntities,
                fc => fc.ApplicantEntityNumber,
                e => e.EntityNumber,
                (fc, e) => new { e.PhysicalState, fc.ServiceProviderSpin })
            .Where(x => x.PhysicalState != null)
            .GroupBy(x => x.PhysicalState!)
            .Select(g => new
            {
                State = g.Key,
                ProviderCount = g.Select(x => x.ServiceProviderSpin).Distinct().Count(),
                CommitmentCount = g.Count(),
            })
            .OrderByDescending(x => x.ProviderCount)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => (r.State, r.ProviderCount, r.CommitmentCount))
            .ToList();
    }
}
