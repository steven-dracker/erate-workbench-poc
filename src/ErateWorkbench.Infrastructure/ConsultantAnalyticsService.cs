using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

// ── Output DTOs ──────────────────────────────────────────────────────────────

/// <summary>Aggregated view of a single consultant's market activity.</summary>
public record ConsultantSummaryDto(
    string ConsultantEpcOrganizationId,
    string? ConsultantName,
    int TotalApplications,
    int TotalFrns,
    decimal? TotalFundingAmount);

/// <summary>Application and FRN activity for one consultant in one funding year.</summary>
public record ConsultantTrendDto(
    int Year,
    int ApplicationCount,
    int FrnCount);

/// <summary>Applicant state breakdown for one consultant.</summary>
public record ConsultantStateBreakdownDto(
    string State,
    int ApplicationCount);

/// <summary>Service type distribution (FRN grain) for one consultant.</summary>
public record ConsultantServiceTypeDto(
    string ServiceTypeName,
    int FrnCount);

/// <summary>Full detail view for a single consultant.</summary>
public record ConsultantDetailDto(
    string ConsultantEpcOrganizationId,
    string? ConsultantName,
    int TotalApplications,
    int TotalFrns,
    decimal? TotalFundingAmount,
    IReadOnlyList<ConsultantTrendDto> Trends,
    IReadOnlyList<ConsultantStateBreakdownDto> StateBreakdown,
    IReadOnlyList<ConsultantServiceTypeDto> ServiceTypes);

// ── Service ──────────────────────────────────────────────────────────────────

/// <summary>
/// Provides aggregation-safe analytics over the consultant datasets.
///
/// Identity rule (CC-ERATE-000038C): ConsultantEpcOrganizationId is the canonical
/// grouping key. ConsultantName is display-only and is NEVER used for grouping.
///
/// Fan-out guard: ConsultantApplications and ConsultantFrnStatuses are queried
/// independently, then joined in memory by EPC ID. A direct SQL join between the
/// two tables would fan-out (multiple FRN rows per application row) and inflate counts.
///
/// FRN semantics: each row in ConsultantFrnStatuses is one unique FRN (the RawSourceKey
/// deduplicates on import), so COUNT(*) == distinct FRN count within a group.
/// Financial aggregations filter WHERE FrnStatusName = 'Funded' per validation finding.
/// </summary>
public class ConsultantAnalyticsService(AppDbContext db)
{
    /// <summary>
    /// Returns the top <paramref name="limit"/> consultants by application volume.
    /// Application count comes from ConsultantApplications; FRN count and funded
    /// total come from ConsultantFrnStatuses. The two datasets are joined in memory
    /// by EPC ID to prevent cross-dataset fan-out.
    /// </summary>
    public async Task<List<ConsultantSummaryDto>> GetTopConsultantsAsync(
        int limit = 25,
        CancellationToken ct = default)
    {
        // Step 1: rank consultants by application count (application grain dataset).
        // Group by EPC ID only — never by name.
        var appGroups = await db.ConsultantApplications
            .GroupBy(a => a.ConsultantEpcOrganizationId)
            .Select(g => new { EpcId = g.Key, TotalApplications = g.Count() })
            .OrderByDescending(x => x.TotalApplications)
            .Take(limit)
            .ToListAsync(ct);

        if (appGroups.Count == 0)
            return [];

        var topEpcIds = appGroups.Select(x => x.EpcId).ToList();

        // Step 2: representative display name per EPC ID (MAX is deterministic in SQLite).
        // Consultant names have inconsistent casing across rows — this is display-only.
        var nameDict = await db.ConsultantApplications
            .Where(a => topEpcIds.Contains(a.ConsultantEpcOrganizationId)
                        && a.ConsultantName != null)
            .GroupBy(a => a.ConsultantEpcOrganizationId)
            .Select(g => new { g.Key, Name = g.Max(a => a.ConsultantName) })
            .ToDictionaryAsync(x => x.Key, x => x.Name, ct);

        // Step 3: FRN count per EPC ID from the FRN grain dataset.
        // COUNT(*) == distinct FRN count because RawSourceKey deduplicates on upsert.
        var frnCountDict = await db.ConsultantFrnStatuses
            .Where(f => topEpcIds.Contains(f.ConsultantEpcOrganizationId))
            .GroupBy(f => f.ConsultantEpcOrganizationId)
            .Select(g => new { g.Key, TotalFrns = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.TotalFrns, ct);

        // Step 4: funded commitment total per EPC ID (filter on FrnStatusName = "Funded").
        // Cast to double for SQLite SUM compatibility; convert back to decimal for output.
        var fundedDict = await db.ConsultantFrnStatuses
            .Where(f => topEpcIds.Contains(f.ConsultantEpcOrganizationId)
                        && f.FrnStatusName == "Funded"
                        && f.FundingCommitmentRequest != null)
            .GroupBy(f => f.ConsultantEpcOrganizationId)
            .Select(g => new { g.Key, Total = (double?)g.Sum(f => (double)f.FundingCommitmentRequest!.Value) })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

        // Step 5: assemble results in the ranked order from step 1.
        return appGroups.Select(a => new ConsultantSummaryDto(
            ConsultantEpcOrganizationId: a.EpcId,
            ConsultantName: nameDict.GetValueOrDefault(a.EpcId),
            TotalApplications: a.TotalApplications,
            TotalFrns: frnCountDict.GetValueOrDefault(a.EpcId),
            TotalFundingAmount: fundedDict.TryGetValue(a.EpcId, out var total) && total.HasValue
                ? (decimal?)Math.Round((decimal)total.Value, 2)
                : null
        )).ToList();
    }

    /// <summary>
    /// Returns summary, year trends, state breakdown, and service type distribution
    /// for a single consultant identified by <paramref name="epcId"/>.
    /// Returns null if the consultant has no data in the system.
    /// </summary>
    public async Task<ConsultantDetailDto?> GetConsultantDetailsAsync(
        string epcId,
        CancellationToken ct = default)
    {
        var totalApps = await db.ConsultantApplications
            .CountAsync(a => a.ConsultantEpcOrganizationId == epcId, ct);

        if (totalApps == 0)
            return null;

        var displayName = await db.ConsultantApplications
            .Where(a => a.ConsultantEpcOrganizationId == epcId && a.ConsultantName != null)
            .Select(a => a.ConsultantName)
            .OrderByDescending(n => n)
            .FirstOrDefaultAsync(ct);

        var totalFrns = await db.ConsultantFrnStatuses
            .CountAsync(f => f.ConsultantEpcOrganizationId == epcId, ct);

        var fundedSum = await db.ConsultantFrnStatuses
            .Where(f => f.ConsultantEpcOrganizationId == epcId
                        && f.FrnStatusName == "Funded"
                        && f.FundingCommitmentRequest != null)
            .SumAsync(f => (double?)f.FundingCommitmentRequest!.Value, ct);

        var trends = await GetTrendsAsync(epcId, ct);
        var stateBreakdown = await GetStateBreakdownAsync(epcId, ct);
        var serviceTypes = await GetServiceTypesAsync(epcId, ct);

        return new ConsultantDetailDto(
            ConsultantEpcOrganizationId: epcId,
            ConsultantName: displayName,
            TotalApplications: totalApps,
            TotalFrns: totalFrns,
            TotalFundingAmount: fundedSum.HasValue ? (decimal?)Math.Round((decimal)fundedSum.Value, 2) : null,
            Trends: trends,
            StateBreakdown: stateBreakdown,
            ServiceTypes: serviceTypes);
    }

    /// <summary>
    /// Application and FRN activity by funding year for a single consultant.
    /// The two year series are joined in memory — no cross-dataset SQL join.
    /// </summary>
    public async Task<List<ConsultantTrendDto>> GetTrendsAsync(
        string epcId,
        CancellationToken ct = default)
    {
        var appByYear = await db.ConsultantApplications
            .Where(a => a.ConsultantEpcOrganizationId == epcId)
            .GroupBy(a => a.FundingYear)
            .Select(g => new { Year = g.Key, AppCount = g.Count() })
            .ToDictionaryAsync(x => x.Year, x => x.AppCount, ct);

        var frnByYear = await db.ConsultantFrnStatuses
            .Where(f => f.ConsultantEpcOrganizationId == epcId)
            .GroupBy(f => f.FundingYear)
            .Select(g => new { Year = g.Key, FrnCount = g.Count() })
            .ToDictionaryAsync(x => x.Year, x => x.FrnCount, ct);

        var allYears = appByYear.Keys.Union(frnByYear.Keys).OrderBy(y => y);

        return allYears.Select(year => new ConsultantTrendDto(
            Year: year,
            ApplicationCount: appByYear.GetValueOrDefault(year),
            FrnCount: frnByYear.GetValueOrDefault(year)
        )).ToList();
    }

    /// <summary>
    /// Applicant state distribution for a single consultant.
    /// Uses ApplicantState (where the school/library is located), not ConsultantState.
    /// </summary>
    public async Task<List<ConsultantStateBreakdownDto>> GetStateBreakdownAsync(
        string epcId,
        CancellationToken ct = default)
    {
        return await db.ConsultantApplications
            .Where(a => a.ConsultantEpcOrganizationId == epcId && a.ApplicantState != null)
            .GroupBy(a => a.ApplicantState)
            .Select(g => new { State = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => new ConsultantStateBreakdownDto(x.State, x.Count))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Service type distribution for a single consultant (FRN grain dataset).
    /// Uses mihb-jfex ServiceTypeName — this field is populated in the FRN dataset
    /// (unlike Form 471, where ServiceType is null per known data caveat).
    /// </summary>
    public async Task<List<ConsultantServiceTypeDto>> GetServiceTypesAsync(
        string epcId,
        CancellationToken ct = default)
    {
        return await db.ConsultantFrnStatuses
            .Where(f => f.ConsultantEpcOrganizationId == epcId && f.ServiceTypeName != null)
            .GroupBy(f => f.ServiceTypeName)
            .Select(g => new { ServiceType = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => new ConsultantServiceTypeDto(x.ServiceType, x.Count))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns total consultant count and total application count for dashboard summaries.
    /// </summary>
    public async Task<(int ConsultantCount, int ApplicationCount)> GetOverviewStatsAsync(
        CancellationToken ct = default)
    {
        var consultantCount = await db.ConsultantApplications
            .Select(a => a.ConsultantEpcOrganizationId)
            .Distinct()
            .CountAsync(ct);

        var applicationCount = await db.ConsultantApplications.CountAsync(ct);

        return (consultantCount, applicationCount);
    }
}
