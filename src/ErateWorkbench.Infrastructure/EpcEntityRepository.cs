using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

public class EpcEntityRepository(AppDbContext db)
{
    public async Task UpsertBatchAsync(IEnumerable<EpcEntity> entities, CancellationToken cancellationToken = default)
    {
        // Deduplicate within the batch — the source CSV can contain multiple rows for the same entity number.
        // Last occurrence wins, matching upsert semantics.
        var deduplicated = entities
            .GroupBy(e => e.EntityNumber)
            .Select(g => g.Last());

        foreach (var incoming in deduplicated)
        {
            var existing = await db.EpcEntities
                .FirstOrDefaultAsync(e => e.EntityNumber == incoming.EntityNumber, cancellationToken);

            if (existing is null)
            {
                db.EpcEntities.Add(incoming);
            }
            else
            {
                existing.EntityName = incoming.EntityName;
                existing.EntityType = incoming.EntityType;
                existing.Status = incoming.Status;
                existing.ParentEntityNumber = incoming.ParentEntityNumber;
                existing.ParentEntityName = incoming.ParentEntityName;
                existing.PhysicalAddress = incoming.PhysicalAddress;
                existing.PhysicalCity = incoming.PhysicalCity;
                existing.PhysicalCounty = incoming.PhysicalCounty;
                existing.PhysicalState = incoming.PhysicalState;
                existing.PhysicalZip = incoming.PhysicalZip;
                existing.Phone = incoming.Phone;
                existing.Email = incoming.Email;
                existing.Website = incoming.Website;
                existing.Latitude = incoming.Latitude;
                existing.Longitude = incoming.Longitude;
                existing.UrbanRuralStatus = incoming.UrbanRuralStatus;
                existing.CategoryOneDiscountRate = incoming.CategoryOneDiscountRate;
                existing.CategoryTwoDiscountRate = incoming.CategoryTwoDiscountRate;
                existing.LocaleCode = incoming.LocaleCode;
                existing.StudentCount = incoming.StudentCount;
                existing.FccRegistrationNumber = incoming.FccRegistrationNumber;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(List<EpcEntity> Items, int TotalCount)> SearchAsync(
        string? search,
        string? state,
        EpcEntityType? entityType,
        string? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = db.EpcEntities.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => e.EntityName.Contains(search));

        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(e => e.PhysicalState == state.ToUpperInvariant());

        if (entityType.HasValue)
            query = query.Where(e => e.EntityType == entityType.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.EntityName)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<EpcEntity?> FindByEntityNumberAsync(
        string entityNumber,
        CancellationToken cancellationToken = default) =>
        await db.EpcEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EntityNumber == entityNumber, cancellationToken);

    public IQueryable<EpcEntity> Query() => db.EpcEntities.AsNoTracking();

    public async Task<List<(string State, int Count)>> GetCountByStateAsync(CancellationToken cancellationToken = default) =>
        await db.EpcEntities
            .AsNoTracking()
            .Where(e => e.PhysicalState != null)
            .GroupBy(e => e.PhysicalState!)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => ValueTuple.Create(x.State, x.Count))
            .ToListAsync(cancellationToken);

    public async Task<List<(EpcEntityType Type, int Count)>> GetCountByTypeAsync(CancellationToken cancellationToken = default) =>
        await db.EpcEntities
            .AsNoTracking()
            .GroupBy(e => e.EntityType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => ValueTuple.Create(x.Type, x.Count))
            .ToListAsync(cancellationToken);

    public async Task<List<(string State, int Count, double AvgCat1, double AvgCat2)>> GetDiscountRatesByStateAsync(
        int topN = 20,
        CancellationToken cancellationToken = default) =>
        await db.EpcEntities
            .AsNoTracking()
            .Where(e => e.PhysicalState != null &&
                        e.CategoryOneDiscountRate != null &&
                        e.CategoryTwoDiscountRate != null)
            .GroupBy(e => e.PhysicalState!)
            .Select(g => new
            {
                State = g.Key,
                Count = g.Count(),
                AvgCat1 = g.Average(e => (double)e.CategoryOneDiscountRate!.Value),
                AvgCat2 = g.Average(e => (double)e.CategoryTwoDiscountRate!.Value),
            })
            .OrderByDescending(x => x.AvgCat1)
            .Take(topN)
            .Select(x => ValueTuple.Create(x.State, x.Count, x.AvgCat1, x.AvgCat2))
            .ToListAsync(cancellationToken);
}
