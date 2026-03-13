using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

public class ApplicantRepository(AppDbContext db)
{
    public async Task UpsertBatchAsync(IEnumerable<Applicant> applicants, CancellationToken cancellationToken = default)
    {
        foreach (var incoming in applicants)
        {
            var existing = await db.Applicants
                .FirstOrDefaultAsync(a => a.Ben == incoming.Ben && a.FundingYear == incoming.FundingYear, cancellationToken);

            if (existing is null)
            {
                db.Applicants.Add(incoming);
            }
            else
            {
                existing.Name = incoming.Name;
                existing.EntityType = incoming.EntityType;
                existing.Address = incoming.Address;
                existing.City = incoming.City;
                existing.State = incoming.State;
                existing.Zip = incoming.Zip;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public IQueryable<Applicant> Query() => db.Applicants.AsNoTracking();
}
