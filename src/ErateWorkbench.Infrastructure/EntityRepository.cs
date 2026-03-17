using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

public class EntityRepository(AppDbContext db)
{
    /// <summary>
    /// Upserts a batch of ROS entities keyed on <see cref="Entity.EntityNumber"/>.
    /// Deduplicates within the batch (last occurrence wins) before hitting the database,
    /// which is important because the source dataset repeats entity data on every
    /// commitment row.
    ///
    /// Uses a bulk EXISTS lookup (WHERE IN) rather than per-row queries to keep batch
    /// processing efficient across the large avi8-svp9 dataset.
    /// </summary>
    public async Task<(int Inserted, int Updated)> UpsertBatchAsync(
        IEnumerable<Entity> incoming,
        CancellationToken cancellationToken = default)
    {
        var deduplicated = incoming
            .GroupBy(e => e.EntityNumber)
            .Select(g => g.Last())
            .ToList();

        var numbers = deduplicated.Select(e => e.EntityNumber).ToHashSet();

        var existing = await db.Entities
            .Where(e => numbers.Contains(e.EntityNumber))
            .ToDictionaryAsync(e => e.EntityNumber, cancellationToken);

        int inserted = 0;
        int updated = 0;
        var toInsert = new List<Entity>(deduplicated.Count);

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var record in deduplicated)
            {
                if (existing.TryGetValue(record.EntityNumber, out var current))
                {
                    current.EntityName = record.EntityName;
                    current.EntityType = record.EntityType;
                    current.UrbanRuralStatus = record.UrbanRuralStatus;
                    current.State = record.State;
                    current.FullTimeStudentCount = record.FullTimeStudentCount;
                    current.PartTimeStudentCount = record.PartTimeStudentCount;
                    current.NslpStudentCount = record.NslpStudentCount;
                    current.UpdatedAtUtc = DateTime.UtcNow;
                    updated++;
                }
                else
                {
                    toInsert.Add(record);
                    inserted++;
                }
            }

            if (toInsert.Count > 0)
                db.Entities.AddRange(toInsert);

            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        return (inserted, updated);
    }
}
