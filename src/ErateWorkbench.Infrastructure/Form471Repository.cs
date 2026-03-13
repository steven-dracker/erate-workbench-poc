using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

public class Form471Repository(AppDbContext db)
{
    /// <summary>
    /// Upserts a batch of Form 471 applications keyed on <see cref="Form471Application.RawSourceKey"/>.
    /// Deduplicates within the batch (last occurrence wins) before hitting the database.
    /// Returns the number of records inserted and updated in this batch.
    /// </summary>
    public async Task<(int Inserted, int Updated)> UpsertBatchAsync(
        IEnumerable<Form471Application> incoming,
        CancellationToken cancellationToken = default)
    {
        var deduplicated = incoming
            .GroupBy(a => a.RawSourceKey)
            .Select(g => g.Last())
            .ToList();

        var keys = deduplicated.Select(a => a.RawSourceKey).ToHashSet();

        var existing = await db.Form471Applications
            .Where(a => keys.Contains(a.RawSourceKey))
            .ToDictionaryAsync(a => a.RawSourceKey, cancellationToken);

        int inserted = 0;
        int updated = 0;

        foreach (var record in deduplicated)
        {
            if (existing.TryGetValue(record.RawSourceKey, out var current))
            {
                current.ApplicantEntityNumber = record.ApplicantEntityNumber;
                current.ApplicantName = record.ApplicantName;
                current.ApplicantState = record.ApplicantState;
                current.CategoryOfService = record.CategoryOfService;
                current.ServiceType = record.ServiceType;
                current.RequestedAmount = record.RequestedAmount;
                current.ApplicationStatus = record.ApplicationStatus;
                current.UpdatedAtUtc = DateTime.UtcNow;
                updated++;
            }
            else
            {
                db.Form471Applications.Add(record);
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return (inserted, updated);
    }

    // --- Analytics ---

    /// <summary>
    /// Total requested amount and application count grouped by funding year, newest year first.
    /// SQLite does not support Sum on decimal — cast to double in SQL, convert back to decimal in memory.
    /// </summary>
    public async Task<List<(int FundingYear, int ApplicationCount, decimal TotalRequested)>>
        GetDemandByYearAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Form471Applications
            .GroupBy(a => a.FundingYear)
            .Select(g => new
            {
                FundingYear = g.Key,
                ApplicationCount = g.Count(),
                TotalDouble = g.Sum(a => (double?)a.RequestedAmount ?? 0.0),
            })
            .OrderByDescending(x => x.FundingYear)
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.FundingYear, r.ApplicationCount, (decimal)r.TotalDouble)).ToList();
    }

    /// <summary>
    /// Total requested amount and count grouped by category of service (Category 1 / Category 2).
    /// Rows where CategoryOfService is null are excluded.
    /// </summary>
    public async Task<List<(string Category, int ApplicationCount, decimal TotalRequested)>>
        GetDemandByCategoryAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Form471Applications
            .Where(a => a.CategoryOfService != null)
            .GroupBy(a => a.CategoryOfService!)
            .Select(g => new
            {
                Category = g.Key,
                ApplicationCount = g.Count(),
                TotalDouble = g.Sum(a => (double?)a.RequestedAmount ?? 0.0),
            })
            .OrderByDescending(x => x.TotalDouble)
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Category, r.ApplicationCount, (decimal)r.TotalDouble)).ToList();
    }

    /// <summary>
    /// Top service types by total requested amount. Rows where ServiceType is null are excluded.
    /// </summary>
    public async Task<List<(string ServiceType, int ApplicationCount, decimal TotalRequested)>>
        GetTopServiceTypesAsync(int topN = 20, CancellationToken cancellationToken = default)
    {
        var rows = await db.Form471Applications
            .Where(a => a.ServiceType != null)
            .GroupBy(a => a.ServiceType!)
            .Select(g => new
            {
                ServiceType = g.Key,
                ApplicationCount = g.Count(),
                TotalDouble = g.Sum(a => (double?)a.RequestedAmount ?? 0.0),
            })
            .OrderByDescending(x => x.TotalDouble)
            .Take(topN)
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.ServiceType, r.ApplicationCount, (decimal)r.TotalDouble)).ToList();
    }
}
