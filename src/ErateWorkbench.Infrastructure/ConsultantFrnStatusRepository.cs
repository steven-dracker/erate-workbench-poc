using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

public class ConsultantFrnStatusRepository(AppDbContext db)
{
    /// <summary>
    /// Upserts a batch of consultant FRN status records keyed on
    /// <see cref="ConsultantFrnStatus.RawSourceKey"/>.
    /// Deduplicates within the batch (last occurrence wins) before hitting the database.
    /// Returns the number of records inserted and updated.
    /// </summary>
    public async Task<(int Inserted, int Updated)> UpsertBatchAsync(
        IEnumerable<ConsultantFrnStatus> incoming,
        CancellationToken cancellationToken = default)
    {
        var deduplicated = incoming
            .GroupBy(r => r.RawSourceKey)
            .Select(g => g.Last())
            .ToList();

        var keys = deduplicated.Select(r => r.RawSourceKey).ToHashSet();

        var existing = await db.ConsultantFrnStatuses
            .Where(r => keys.Contains(r.RawSourceKey))
            .ToDictionaryAsync(r => r.RawSourceKey, cancellationToken);

        int inserted = 0;
        int updated = 0;

        foreach (var record in deduplicated)
        {
            if (existing.TryGetValue(record.RawSourceKey, out var current))
            {
                // Preserve identity fields; update mutable/status fields
                current.FundingYear = record.FundingYear;
                current.FormVersion = record.FormVersion;
                current.IsCertifiedInWindow = record.IsCertifiedInWindow;
                current.Nickname = record.Nickname;
                current.ApplicantState = record.ApplicantState;
                current.Ben = record.Ben;
                current.OrganizationName = record.OrganizationName;
                current.OrganizationEntityTypeName = record.OrganizationEntityTypeName;
                current.ContactEmail = record.ContactEmail;
                current.ConsultantName = record.ConsultantName;
                current.ServiceTypeName = record.ServiceTypeName;
                current.ContractTypeName = record.ContractTypeName;
                current.SpinName = record.SpinName;
                current.FrnStatusName = record.FrnStatusName;
                current.PendingReason = record.PendingReason;
                current.InvoicingMode = record.InvoicingMode;
                current.DiscountPct = record.DiscountPct;
                current.TotalPreDiscountCosts = record.TotalPreDiscountCosts;
                current.FundingCommitmentRequest = record.FundingCommitmentRequest;
                current.TotalAuthorizedDisbursement = record.TotalAuthorizedDisbursement;
                current.ServiceStartDate = record.ServiceStartDate;
                current.FcdlLetterDate = record.FcdlLetterDate;
                current.UpdatedAtUtc = DateTime.UtcNow;
                updated++;
            }
            else
            {
                db.ConsultantFrnStatuses.Add(record);
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return (inserted, updated);
    }
}
