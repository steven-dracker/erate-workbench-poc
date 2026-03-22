using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

public class ConsultantApplicationRepository(AppDbContext db)
{
    /// <summary>
    /// Upserts a batch of consultant application records keyed on
    /// <see cref="ConsultantApplication.RawSourceKey"/>.
    /// Deduplicates within the batch (last occurrence wins) before hitting the database.
    /// Returns the number of records inserted and updated.
    /// </summary>
    public async Task<(int Inserted, int Updated)> UpsertBatchAsync(
        IEnumerable<ConsultantApplication> incoming,
        CancellationToken cancellationToken = default)
    {
        var deduplicated = incoming
            .GroupBy(r => r.RawSourceKey)
            .Select(g => g.Last())
            .ToList();

        var keys = deduplicated.Select(r => r.RawSourceKey).ToHashSet();

        var existing = await db.ConsultantApplications
            .Where(r => keys.Contains(r.RawSourceKey))
            .ToDictionaryAsync(r => r.RawSourceKey, cancellationToken);

        int inserted = 0;
        int updated = 0;

        foreach (var record in deduplicated)
        {
            if (existing.TryGetValue(record.RawSourceKey, out var current))
            {
                // Preserve identity fields; update mutable fields
                current.FundingYear = record.FundingYear;
                current.FormVersion = record.FormVersion;
                current.IsCertifiedInWindow = record.IsCertifiedInWindow;
                current.ApplicantEpcOrganizationId = record.ApplicantEpcOrganizationId;
                current.OrganizationName = record.OrganizationName;
                current.ApplicantType = record.ApplicantType;
                current.ApplicantState = record.ApplicantState;
                current.ContactEmail = record.ContactEmail;
                current.ConsultantName = record.ConsultantName;
                current.ConsultantCity = record.ConsultantCity;
                current.ConsultantState = record.ConsultantState;
                current.ConsultantZipCode = record.ConsultantZipCode;
                current.ConsultantPhone = record.ConsultantPhone;
                current.ConsultantEmail = record.ConsultantEmail;
                current.UpdatedAtUtc = DateTime.UtcNow;
                updated++;
            }
            else
            {
                db.ConsultantApplications.Add(record);
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return (inserted, updated);
    }
}
