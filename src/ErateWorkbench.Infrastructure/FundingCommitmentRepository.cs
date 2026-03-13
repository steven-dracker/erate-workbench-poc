using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

public class FundingCommitmentRepository(AppDbContext db)
{
    /// <summary>
    /// Upserts a batch of funding commitments keyed on <see cref="FundingCommitment.RawSourceKey"/>.
    /// Deduplicates within the batch (last occurrence wins) before hitting the database.
    /// Returns the number of records inserted and updated in this batch.
    /// </summary>
    public async Task<(int Inserted, int Updated)> UpsertBatchAsync(
        IEnumerable<FundingCommitment> incoming,
        CancellationToken cancellationToken = default)
    {
        var deduplicated = incoming
            .GroupBy(c => c.RawSourceKey)
            .Select(g => g.Last())
            .ToList();

        var keys = deduplicated.Select(c => c.RawSourceKey).ToHashSet();

        var existing = await db.FundingCommitments
            .Where(c => keys.Contains(c.RawSourceKey))
            .ToDictionaryAsync(c => c.RawSourceKey, cancellationToken);

        int inserted = 0;
        int updated = 0;

        foreach (var record in deduplicated)
        {
            if (existing.TryGetValue(record.RawSourceKey, out var current))
            {
                current.ApplicantEntityNumber = record.ApplicantEntityNumber;
                current.ApplicantName = record.ApplicantName;
                current.ApplicationNumber = record.ApplicationNumber;
                current.FundingYear = record.FundingYear;
                current.ServiceProviderName = record.ServiceProviderName;
                current.ServiceProviderSpin = record.ServiceProviderSpin;
                current.CategoryOfService = record.CategoryOfService;
                current.TypeOfService = record.TypeOfService;
                current.CommitmentStatus = record.CommitmentStatus;
                current.CommittedAmount = record.CommittedAmount;
                current.TotalEligibleAmount = record.TotalEligibleAmount;
                current.UpdatedAtUtc = DateTime.UtcNow;
                updated++;
            }
            else
            {
                db.FundingCommitments.Add(record);
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return (inserted, updated);
    }

    // --- Analytics ---

    /// <summary>
    /// Total committed amount and commitment count grouped by funding year, newest year first.
    /// Source: FundingCommitments only — no join required.
    /// SQLite does not support Sum on decimal; cast to double in SQL, convert back in memory.
    /// </summary>
    public async Task<List<(int FundingYear, int CommitmentCount, decimal TotalCommitted)>>
        GetFundingByYearAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.FundingCommitments
            .GroupBy(c => c.FundingYear)
            .Select(g => new
            {
                FundingYear = g.Key,
                CommitmentCount = g.Count(),
                TotalDouble = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
            })
            .OrderByDescending(x => x.FundingYear)
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.FundingYear, r.CommitmentCount, (decimal)r.TotalDouble)).ToList();
    }

    /// <summary>
    /// Total committed amount and count grouped by category of service (Category 1 / Category 2).
    /// Rows where CategoryOfService is null are excluded.
    /// Source: FundingCommitments only — no join required.
    /// </summary>
    public async Task<List<(string Category, int CommitmentCount, decimal TotalCommitted)>>
        GetFundingByCategoryAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.FundingCommitments
            .Where(c => c.CategoryOfService != null)
            .GroupBy(c => c.CategoryOfService!)
            .Select(g => new
            {
                Category = g.Key,
                CommitmentCount = g.Count(),
                TotalDouble = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
            })
            .OrderByDescending(x => x.TotalDouble)
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Category, r.CommitmentCount, (decimal)r.TotalDouble)).ToList();
    }

    /// <summary>
    /// Total committed amount and commitment count grouped by applicant state.
    /// Join assumption: FundingCommitments.ApplicantEntityNumber → EpcEntities.EntityNumber (left join).
    /// Rows where ApplicantEntityNumber is null, or where no matching EpcEntity exists, are excluded
    /// because the state comes from EpcEntities.PhysicalState. The FundingCommitment itself does not
    /// store a state field directly.
    /// </summary>
    public async Task<List<(string State, int CommitmentCount, decimal TotalCommitted)>>
        GetFundingByStateAsync(CancellationToken cancellationToken = default)
    {
        // Two-step: aggregate commitments per entity number, then join to EpcEntities for state.
        var byEntity = await db.FundingCommitments
            .Where(c => c.ApplicantEntityNumber != null)
            .GroupBy(c => c.ApplicantEntityNumber!)
            .Select(g => new
            {
                EntityNumber = g.Key,
                CommitmentCount = g.Count(),
                TotalDouble = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
            })
            .ToListAsync(cancellationToken);

        var entityNumbers = byEntity.Select(r => r.EntityNumber).ToHashSet();

        var entityStates = await db.EpcEntities
            .Where(e => entityNumbers.Contains(e.EntityNumber) && e.PhysicalState != null)
            .Select(e => new { e.EntityNumber, e.PhysicalState })
            .ToListAsync(cancellationToken);

        var stateLookup = entityStates.ToDictionary(e => e.EntityNumber, e => e.PhysicalState!);

        return byEntity
            .Where(r => stateLookup.ContainsKey(r.EntityNumber))
            .GroupBy(r => stateLookup[r.EntityNumber])
            .Select(g => (
                State: g.Key,
                CommitmentCount: g.Sum(r => r.CommitmentCount),
                TotalCommitted: (decimal)g.Sum(r => r.TotalDouble)
            ))
            .OrderByDescending(r => r.TotalCommitted)
            .ToList();
    }

    /// <summary>
    /// Top entities by total committed amount.
    /// Join assumption: FundingCommitments.ApplicantEntityNumber → EpcEntities.EntityNumber.
    /// Step 1: aggregate committed amount per entity number in SQL.
    /// Step 2: fetch entity name, type, and state from EpcEntities in a second query.
    /// Entities not found in EpcEntities are excluded (inner join semantics).
    /// Use <paramref name="topN"/> to limit results (default 20).
    /// </summary>
    public async Task<List<(string EntityNumber, string EntityName, string EntityType, string? State, int CommitmentCount, decimal TotalCommitted)>>
        GetTopFundedEntitiesAsync(int topN = 20, CancellationToken cancellationToken = default)
    {
        var byEntity = await db.FundingCommitments
            .Where(c => c.ApplicantEntityNumber != null)
            .GroupBy(c => c.ApplicantEntityNumber!)
            .Select(g => new
            {
                EntityNumber = g.Key,
                CommitmentCount = g.Count(),
                TotalDouble = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
            })
            .OrderByDescending(x => x.TotalDouble)
            .Take(topN)
            .ToListAsync(cancellationToken);

        var entityNumbers = byEntity.Select(r => r.EntityNumber).ToHashSet();

        var entities = await db.EpcEntities
            .Where(e => entityNumbers.Contains(e.EntityNumber))
            .Select(e => new { e.EntityNumber, e.EntityName, e.EntityType, e.PhysicalState })
            .ToListAsync(cancellationToken);

        var lookup = entities.ToDictionary(e => e.EntityNumber);

        return byEntity
            .Where(r => lookup.ContainsKey(r.EntityNumber))
            .Select(r =>
            {
                var e = lookup[r.EntityNumber];
                return (r.EntityNumber, e.EntityName, e.EntityType.ToString(), e.PhysicalState, r.CommitmentCount, (decimal)r.TotalDouble);
            })
            .ToList();
    }

    /// <summary>
    /// Entities with a high Category 1 discount rate relative to their state peers but below-average
    /// committed funding for that state — a signal that high-need entities may be under-utilizing E-Rate.
    ///
    /// Algorithm (simple, documented):
    ///   1. Compute per-entity totals: committed funding from FundingCommitments, Cat1 discount from EpcEntities.
    ///   2. Group by PhysicalState to compute state-level averages for both metrics.
    ///   3. Return entities where:
    ///        - CategoryOneDiscountRate ≥ <paramref name="minDiscountRate"/> (default 70)
    ///        - CommittedAmount &lt; state average committed amount
    ///
    /// Join assumption: FundingCommitments.ApplicantEntityNumber → EpcEntities.EntityNumber.
    /// Entities in EpcEntities with no funding commitments are included with CommittedAmount = 0,
    /// so they will almost always appear as under-utilizing.
    /// Entities not in EpcEntities (no discount rate) are excluded.
    /// </summary>
    public async Task<List<(string EntityNumber, string EntityName, string State, decimal DiscountRate, decimal TotalCommitted, decimal StateAvgCommitted)>>
        GetHighDiscountLowUtilizationAsync(decimal minDiscountRate = 70m, CancellationToken cancellationToken = default)
    {
        // Step 1: funding totals per entity from FundingCommitments
        var fundingByEntity = await db.FundingCommitments
            .Where(c => c.ApplicantEntityNumber != null)
            .GroupBy(c => c.ApplicantEntityNumber!)
            .Select(g => new
            {
                EntityNumber = g.Key,
                TotalDouble = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
            })
            .ToListAsync(cancellationToken);

        var fundingLookup = fundingByEntity.ToDictionary(r => r.EntityNumber, r => r.TotalDouble);

        // Step 2: all EPC entities with high discount and a known state
        var candidates = await db.EpcEntities
            .Where(e => e.CategoryOneDiscountRate >= minDiscountRate && e.PhysicalState != null)
            .Select(e => new
            {
                e.EntityNumber,
                e.EntityName,
                e.PhysicalState,
                e.CategoryOneDiscountRate,
            })
            .ToListAsync(cancellationToken);

        // Step 3: attach committed amount (0 if no commitments found)
        var annotated = candidates.Select(e => new
        {
            e.EntityNumber,
            e.EntityName,
            State = e.PhysicalState!,
            DiscountRate = e.CategoryOneDiscountRate!.Value,
            TotalCommitted = (decimal)(fundingLookup.GetValueOrDefault(e.EntityNumber, 0.0)),
        }).ToList();

        // Step 4: compute state average committed amount
        var stateAvg = annotated
            .GroupBy(e => e.State)
            .ToDictionary(g => g.Key, g => g.Average(e => (double)e.TotalCommitted));

        // Step 5: filter to those below their state average
        return annotated
            .Where(e => (double)e.TotalCommitted < stateAvg.GetValueOrDefault(e.State, 0.0))
            .OrderBy(e => e.State)
            .ThenBy(e => (double)e.TotalCommitted)
            .Select(e => (
                e.EntityNumber,
                e.EntityName,
                e.State,
                e.DiscountRate,
                e.TotalCommitted,
                StateAvgCommitted: (decimal)stateAvg[e.State]
            ))
            .ToList();
    }
}
