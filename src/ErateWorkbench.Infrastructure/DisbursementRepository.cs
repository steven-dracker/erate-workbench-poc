using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

public class DisbursementRepository(AppDbContext db)
{
    /// <summary>
    /// Upserts a batch of disbursements keyed on <see cref="Disbursement.RawSourceKey"/>.
    /// Deduplicates within the batch (last occurrence wins) before hitting the database.
    /// Uses a bulk EXISTS lookup (WHERE IN) rather than per-row queries.
    /// </summary>
    public async Task<(int Inserted, int Updated)> UpsertBatchAsync(
        IEnumerable<Disbursement> incoming,
        CancellationToken cancellationToken = default)
    {
        var deduplicated = incoming
            .GroupBy(d => d.RawSourceKey)
            .Select(g => g.Last())
            .ToList();

        var keys = deduplicated.Select(d => d.RawSourceKey).ToHashSet();

        var existing = await db.Disbursements
            .Where(d => keys.Contains(d.RawSourceKey))
            .ToDictionaryAsync(d => d.RawSourceKey, cancellationToken);

        int inserted = 0;
        int updated = 0;
        var toInsert = new List<Disbursement>(deduplicated.Count);

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var record in deduplicated)
            {
                if (existing.TryGetValue(record.RawSourceKey, out var current))
                {
                    current.InvoiceType = record.InvoiceType;
                    current.InvoiceLineStatus = record.InvoiceLineStatus;
                    current.ApplicationNumber = record.ApplicationNumber;
                    current.ApplicantEntityNumber = record.ApplicantEntityNumber;
                    current.ApplicantEntityName = record.ApplicantEntityName;
                    current.ServiceProviderSpin = record.ServiceProviderSpin;
                    current.ServiceProviderName = record.ServiceProviderName;
                    current.FundingYear = record.FundingYear;
                    current.CategoryOfService = record.CategoryOfService;
                    current.RequestedAmount = record.RequestedAmount;
                    current.ApprovedAmount = record.ApprovedAmount;
                    current.InvoiceReceivedDate = record.InvoiceReceivedDate;
                    current.LineCompletionDate = record.LineCompletionDate;
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
                db.Disbursements.AddRange(toInsert);

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
