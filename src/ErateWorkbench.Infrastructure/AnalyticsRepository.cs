using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

/// <summary>
/// Cross-dataset analytics queries joining FundingCommitments, Disbursements, and Entities.
///
/// All monetary aggregations use Sum((double?)amount ?? 0.0) to work around SQLite's
/// lack of a native decimal SUM, then convert back to decimal in memory.
/// </summary>
public class AnalyticsRepository(AppDbContext db)
{
    // -------------------------------------------------------------------------
    // 1. Commitment vs Disbursement by Funding Year
    // -------------------------------------------------------------------------

    /// <summary>
    /// Per-year comparison of committed vs disbursed (approved) amounts.
    /// Gap = Committed − Disbursed; positive gap means commitments not yet disbursed.
    /// FundingCommitments drives the year list; Disbursements are left-joined in memory.
    /// Years present only in Disbursements (unlikely) are not included.
    /// </summary>
    public async Task<List<CommitmentVsDisbursementByYearRow>> GetCommitmentVsDisbursementByYearAsync(
        CancellationToken cancellationToken = default)
    {
        var commitments = await db.FundingCommitments
            .GroupBy(c => c.FundingYear)
            .Select(g => new
            {
                FundingYear = g.Key,
                TotalDouble = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
            })
            .ToListAsync(cancellationToken);

        var disbursements = await db.Disbursements
            .GroupBy(d => d.FundingYear)
            .Select(g => new
            {
                FundingYear = g.Key,
                TotalDouble = g.Sum(d => (double?)d.ApprovedAmount ?? 0.0),
            })
            .ToListAsync(cancellationToken);

        var disbLookup = disbursements.ToDictionary(r => r.FundingYear, r => r.TotalDouble);

        return commitments
            .Select(c =>
            {
                var committed = (decimal)c.TotalDouble;
                var disbursed = (decimal)disbLookup.GetValueOrDefault(c.FundingYear, 0.0);
                return new CommitmentVsDisbursementByYearRow(c.FundingYear, committed, disbursed, committed - disbursed);
            })
            .OrderByDescending(r => r.FundingYear)
            .ToList();
    }

    // -------------------------------------------------------------------------
    // 2. Top applicants by total committed amount
    // -------------------------------------------------------------------------

    /// <summary>
    /// Top applicant entities by committed funding, with their total disbursements.
    /// ApplicantName comes from FundingCommitments (first non-null name found for that BEN).
    /// Disbursements joined by ApplicantEntityNumber.
    /// Entities with null ApplicantEntityNumber are excluded.
    /// </summary>
    public async Task<List<TopApplicantRow>> GetTopApplicantsAsync(
        int topN = 20,
        CancellationToken cancellationToken = default)
    {
        var commitments = await db.FundingCommitments
            .Where(c => c.ApplicantEntityNumber != null)
            .GroupBy(c => c.ApplicantEntityNumber!)
            .Select(g => new
            {
                EntityNumber = g.Key,
                TotalDouble = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
                // Pick a representative name — first non-null in the group.
                Name = g.Select(c => c.ApplicantName).FirstOrDefault(n => n != null),
            })
            .OrderByDescending(x => x.TotalDouble)
            .Take(topN)
            .ToListAsync(cancellationToken);

        var entityNumbers = commitments.Select(r => r.EntityNumber).ToHashSet();

        var disbursed = await db.Disbursements
            .Where(d => d.ApplicantEntityNumber != null && entityNumbers.Contains(d.ApplicantEntityNumber!))
            .GroupBy(d => d.ApplicantEntityNumber!)
            .Select(g => new { EntityNumber = g.Key, TotalDouble = g.Sum(d => (double?)d.ApprovedAmount ?? 0.0) })
            .ToListAsync(cancellationToken);

        var disbLookup = disbursed.ToDictionary(r => r.EntityNumber, r => r.TotalDouble);

        return commitments.Select(c => new TopApplicantRow(
            c.EntityNumber,
            c.Name,
            (decimal)c.TotalDouble,
            (decimal)disbLookup.GetValueOrDefault(c.EntityNumber, 0.0)
        )).ToList();
    }

    // -------------------------------------------------------------------------
    // 3. Top service providers by commitments and disbursements
    // -------------------------------------------------------------------------

    /// <summary>
    /// Top service providers by committed funding, with their total disbursements.
    /// ServiceProviderName comes from FundingCommitments.
    /// Disbursements joined by ServiceProviderSpin.
    /// </summary>
    public async Task<List<TopProviderRow>> GetTopServiceProvidersAsync(
        int topN = 20,
        CancellationToken cancellationToken = default)
    {
        var commitments = await db.FundingCommitments
            .Where(c => c.ServiceProviderSpin != null)
            .GroupBy(c => c.ServiceProviderSpin!)
            .Select(g => new
            {
                Spin = g.Key,
                TotalDouble = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
                Name = g.Select(c => c.ServiceProviderName).FirstOrDefault(n => n != null),
            })
            .OrderByDescending(x => x.TotalDouble)
            .Take(topN)
            .ToListAsync(cancellationToken);

        var spins = commitments.Select(r => r.Spin).ToHashSet();

        var disbursed = await db.Disbursements
            .Where(d => d.ServiceProviderSpin != null && spins.Contains(d.ServiceProviderSpin!))
            .GroupBy(d => d.ServiceProviderSpin!)
            .Select(g => new { Spin = g.Key, TotalDouble = g.Sum(d => (double?)d.ApprovedAmount ?? 0.0) })
            .ToListAsync(cancellationToken);

        var disbLookup = disbursed.ToDictionary(r => r.Spin, r => r.TotalDouble);

        return commitments.Select(c => new TopProviderRow(
            c.Spin,
            c.Name,
            (decimal)c.TotalDouble,
            (decimal)disbLookup.GetValueOrDefault(c.Spin, 0.0)
        )).ToList();
    }

    // -------------------------------------------------------------------------
    // 4. Rural vs Urban funding summary
    // -------------------------------------------------------------------------

    /// <summary>
    /// Funding commitments (and disbursements) grouped by the UrbanRuralStatus of the
    /// applicant entity.  Join path: FundingCommitments.ApplicantEntityNumber → Entities.EntityNumber.
    /// Rows where the entity is not in the Entities table, or UrbanRuralStatus is null,
    /// are counted under the literal "Unknown" bucket.
    /// </summary>
    public async Task<List<RuralUrbanSummaryRow>> GetRuralUrbanSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        // Aggregate commitments per entity number in SQL
        var commitmentsByEntity = await db.FundingCommitments
            .Where(c => c.ApplicantEntityNumber != null)
            .GroupBy(c => c.ApplicantEntityNumber!)
            .Select(g => new
            {
                EntityNumber = g.Key,
                Count = g.Count(),
                TotalDouble = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
            })
            .ToListAsync(cancellationToken);

        var entityNumbers = commitmentsByEntity.Select(r => r.EntityNumber).ToHashSet();

        // Fetch urban/rural status for those entities
        var entityStatus = await db.Entities
            .Where(e => entityNumbers.Contains(e.EntityNumber))
            .Select(e => new { e.EntityNumber, e.UrbanRuralStatus })
            .ToListAsync(cancellationToken);

        var statusLookup = entityStatus.ToDictionary(e => e.EntityNumber, e => e.UrbanRuralStatus ?? "Unknown");

        // Aggregate disbursements per entity number in SQL
        var disbByEntity = await db.Disbursements
            .Where(d => d.ApplicantEntityNumber != null && entityNumbers.Contains(d.ApplicantEntityNumber!))
            .GroupBy(d => d.ApplicantEntityNumber!)
            .Select(g => new { EntityNumber = g.Key, TotalDouble = g.Sum(d => (double?)d.ApprovedAmount ?? 0.0) })
            .ToListAsync(cancellationToken);

        var disbLookup = disbByEntity.ToDictionary(r => r.EntityNumber, r => r.TotalDouble);

        // Group by urban/rural status in memory
        return commitmentsByEntity
            .GroupBy(r => statusLookup.GetValueOrDefault(r.EntityNumber, "Unknown"))
            .Select(g => new RuralUrbanSummaryRow(
                UrbanRuralStatus: g.Key,
                RowCount: g.Sum(r => r.Count),
                TotalCommittedAmount: (decimal)g.Sum(r => r.TotalDouble),
                TotalDisbursedAmount: (decimal)g.Sum(r => disbLookup.GetValueOrDefault(r.EntityNumber, 0.0))
            ))
            .OrderByDescending(r => r.TotalCommittedAmount)
            .ToList();
    }

    // -------------------------------------------------------------------------
    // 5. Funding per student
    // -------------------------------------------------------------------------

    /// <summary>
    /// Per-entity funding-per-student ratio for entities that have student counts recorded.
    /// Join path: FundingCommitments.ApplicantEntityNumber → Entities.EntityNumber.
    /// StudentCount = FullTimeStudentCount + PartTimeStudentCount (both from Entities table).
    /// Only entities where total student count &gt; 0 are included.
    /// Results ordered by FundingPerStudent descending (highest spend per student first).
    /// </summary>
    public async Task<List<FundingPerStudentRow>> GetFundingPerStudentAsync(
        int topN = 50,
        CancellationToken cancellationToken = default)
    {
        // Aggregate committed amount per entity
        var commitmentsByEntity = await db.FundingCommitments
            .Where(c => c.ApplicantEntityNumber != null)
            .GroupBy(c => c.ApplicantEntityNumber!)
            .Select(g => new
            {
                EntityNumber = g.Key,
                TotalDouble = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
                Name = g.Select(c => c.ApplicantName).FirstOrDefault(n => n != null),
            })
            .ToListAsync(cancellationToken);

        var entityNumbers = commitmentsByEntity.Select(r => r.EntityNumber).ToHashSet();

        // Fetch student counts from Entities
        var entities = await db.Entities
            .Where(e => entityNumbers.Contains(e.EntityNumber)
                     && (e.FullTimeStudentCount > 0 || e.PartTimeStudentCount > 0))
            .Select(e => new
            {
                e.EntityNumber,
                e.EntityName,
                StudentCount = (e.FullTimeStudentCount ?? 0) + (e.PartTimeStudentCount ?? 0),
            })
            .ToListAsync(cancellationToken);

        var entityLookup = entities.ToDictionary(e => e.EntityNumber);

        var fundingLookup = commitmentsByEntity.ToDictionary(r => r.EntityNumber);

        return entities
            .Where(e => entityLookup.ContainsKey(e.EntityNumber) && fundingLookup.ContainsKey(e.EntityNumber))
            .Select(e =>
            {
                var committed = (decimal)fundingLookup[e.EntityNumber].TotalDouble;
                var name = fundingLookup[e.EntityNumber].Name ?? e.EntityName;
                var fpStudent = e.StudentCount > 0 ? committed / e.StudentCount : 0m;
                return new FundingPerStudentRow(e.EntityNumber, name, e.StudentCount, committed, fpStudent);
            })
            .OrderByDescending(r => r.FundingPerStudent)
            .Take(topN)
            .ToList();
    }
}

// -------------------------------------------------------------------------
// Result row types
// -------------------------------------------------------------------------

public record CommitmentVsDisbursementByYearRow(
    int FundingYear,
    decimal TotalCommittedAmount,
    decimal TotalDisbursedAmount,
    decimal GapAmount);

public record TopApplicantRow(
    string ApplicantEntityNumber,
    string? ApplicantName,
    decimal TotalCommittedAmount,
    decimal TotalDisbursedAmount);

public record TopProviderRow(
    string ServiceProviderSpin,
    string? ServiceProviderName,
    decimal TotalCommittedAmount,
    decimal TotalDisbursedAmount);

public record RuralUrbanSummaryRow(
    string UrbanRuralStatus,
    int RowCount,
    decimal TotalCommittedAmount,
    decimal TotalDisbursedAmount);

public record FundingPerStudentRow(
    string ApplicantEntityNumber,
    string? ApplicantName,
    int StudentCount,
    decimal TotalCommittedAmount,
    decimal FundingPerStudent);
