using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

// ── Constants ─────────────────────────────────────────────────────────────────

/// <summary>Well-known consultant EPC IDs confirmed during CC-ERATE-000038C audit.</summary>
public static class ConsultantConstants
{
    /// <summary>
    /// E-Rate Central EPC Organization ID — confirmed in CC-ERATE-000038C data audit.
    /// Used for highlighting in rankings and detail pages.
    /// </summary>
    public const string ERateCentralEpcId = "16060891";
    public const string ERateCentralDisplayName = "E-Rate Central";
}

// ── Filter types ──────────────────────────────────────────────────────────────

/// <summary>
/// Optional filter parameters for consultant analytics queries.
/// All fields are nullable — omitted fields are treated as "all".
/// </summary>
public record ConsultantFilterParams(
    int[]? FundingYears = null,
    string? State = null,
    string? ServiceType = null)
{
    public bool IsEmpty =>
        (FundingYears is null or { Length: 0 }) &&
        string.IsNullOrWhiteSpace(State) &&
        string.IsNullOrWhiteSpace(ServiceType);
}

/// <summary>Available filter option values for populating filter dropdowns.</summary>
public record ConsultantFilterOptionsDto(
    IReadOnlyList<int> FundingYears,
    IReadOnlyList<string> States,
    IReadOnlyList<string> ServiceTypes);

// ── Output DTOs ───────────────────────────────────────────────────────────────

/// <summary>Aggregated view of a single consultant's market activity.</summary>
public record ConsultantSummaryDto(
    string ConsultantEpcOrganizationId,
    string? ConsultantName,
    int TotalApplications,
    int TotalFrns,
    decimal? TotalFundingAmount,
    /// <summary>% share of total applications in the current filter context.</summary>
    decimal ApplicationSharePct,
    /// <summary>% share of total FRNs in the current filter context.</summary>
    decimal FrnSharePct,
    /// <summary>Count of distinct applicant states served (always unfiltered — shows total reach).</summary>
    int DistinctStateCount);

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

// ── Service ───────────────────────────────────────────────────────────────────

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
///
/// Filtering (CC-ERATE-000038E): Year and State filters apply to ConsultantApplications.
/// ServiceType filter applies to ConsultantFrnStatuses and restricts the application
/// side to EPC IDs that have matching FRNs (cross-dataset filter without SQL join).
/// </summary>
public class ConsultantAnalyticsService(AppDbContext db)
{
    // ── Filter helpers ────────────────────────────────────────────────────────

    private static IQueryable<ConsultantApplication> ApplyAppFilters(
        IQueryable<ConsultantApplication> query, ConsultantFilterParams? filters)
    {
        if (filters is null || filters.IsEmpty) return query;
        if (filters.FundingYears is { Length: > 0 } years)
            query = query.Where(a => years.Contains(a.FundingYear));
        if (!string.IsNullOrWhiteSpace(filters.State))
            query = query.Where(a => a.ApplicantState == filters.State);
        return query;
    }

    private static IQueryable<ConsultantFrnStatus> ApplyFrnFilters(
        IQueryable<ConsultantFrnStatus> query, ConsultantFilterParams? filters)
    {
        if (filters is null || filters.IsEmpty) return query;
        if (filters.FundingYears is { Length: > 0 } years)
            query = query.Where(f => years.Contains(f.FundingYear));
        if (!string.IsNullOrWhiteSpace(filters.State))
            query = query.Where(f => f.ApplicantState == filters.State);
        if (!string.IsNullOrWhiteSpace(filters.ServiceType))
            query = query.Where(f => f.ServiceTypeName == filters.ServiceType);
        return query;
    }

    // ── GetAvailableFiltersAsync ──────────────────────────────────────────────

    /// <summary>
    /// Returns distinct values for all filter dropdowns.
    /// Intended to be cached at the page level.
    /// </summary>
    public async Task<ConsultantFilterOptionsDto> GetAvailableFiltersAsync(
        CancellationToken ct = default)
    {
        var years = await db.ConsultantApplications
            .Select(a => a.FundingYear)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(ct);

        var states = await db.ConsultantApplications
            .Where(a => a.ApplicantState != null)
            .Select(a => a.ApplicantState!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);

        var serviceTypes = await db.ConsultantFrnStatuses
            .Where(f => f.ServiceTypeName != null)
            .Select(f => f.ServiceTypeName!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);

        return new ConsultantFilterOptionsDto(years, states, serviceTypes);
    }

    // ── GetTopConsultantsAsync ────────────────────────────────────────────────

    /// <summary>
    /// Returns the top <paramref name="limit"/> consultants by application volume,
    /// with market share percentages and geographic reach.
    ///
    /// When <paramref name="filters"/> includes a ServiceType, the application side
    /// is restricted to EPC IDs that have FRNs of that service type (cross-dataset
    /// filter without a SQL join — aggregation safety is preserved).
    ///
    /// Market share denominators use the full filtered dataset (not just top-N).
    /// DistinctStateCount is always unfiltered — it reflects total geographic reach.
    /// </summary>
    public async Task<List<ConsultantSummaryDto>> GetTopConsultantsAsync(
        int limit = 25,
        ConsultantFilterParams? filters = null,
        CancellationToken ct = default)
    {
        var appQuery = ApplyAppFilters(db.ConsultantApplications.AsQueryable(), filters);
        var frnBaseQuery = ApplyFrnFilters(db.ConsultantFrnStatuses.AsQueryable(), filters);

        // If service type filter: restrict applications to EPC IDs that have
        // FRNs of the requested service type (cross-dataset filter without SQL join).
        if (!string.IsNullOrWhiteSpace(filters?.ServiceType))
        {
            var allowedEpcIds = await frnBaseQuery
                .Select(f => f.ConsultantEpcOrganizationId)
                .Distinct()
                .ToListAsync(ct);

            if (allowedEpcIds.Count == 0) return [];
            appQuery = appQuery.Where(a => allowedEpcIds.Contains(a.ConsultantEpcOrganizationId));
        }

        // Market share denominators (full filtered dataset)
        var totalApplications = await appQuery.CountAsync(ct);
        if (totalApplications == 0) return [];
        var totalFrns = await frnBaseQuery.CountAsync(ct);

        // Step 1: rank consultants by application count (application grain dataset).
        // Group by EPC ID only — never by name.
        var appGroups = await appQuery
            .GroupBy(a => a.ConsultantEpcOrganizationId)
            .Select(g => new { EpcId = g.Key, TotalApplications = g.Count() })
            .OrderByDescending(x => x.TotalApplications)
            .Take(limit)
            .ToListAsync(ct);

        if (appGroups.Count == 0) return [];
        var topEpcIds = appGroups.Select(x => x.EpcId).ToList();

        // Step 2: representative display name per EPC ID (MAX is deterministic in SQLite).
        var nameDict = await db.ConsultantApplications
            .Where(a => topEpcIds.Contains(a.ConsultantEpcOrganizationId)
                        && a.ConsultantName != null)
            .GroupBy(a => a.ConsultantEpcOrganizationId)
            .Select(g => new { g.Key, Name = g.Max(a => a.ConsultantName) })
            .ToDictionaryAsync(x => x.Key, x => x.Name, ct);

        // Step 3: FRN count per EPC ID (filtered FRN dataset).
        var frnCountDict = await frnBaseQuery
            .Where(f => topEpcIds.Contains(f.ConsultantEpcOrganizationId))
            .GroupBy(f => f.ConsultantEpcOrganizationId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        // Step 4: funded commitment total per EPC ID.
        var fundedDict = await frnBaseQuery
            .Where(f => topEpcIds.Contains(f.ConsultantEpcOrganizationId)
                        && f.FrnStatusName == "Funded"
                        && f.FundingCommitmentRequest != null)
            .GroupBy(f => f.ConsultantEpcOrganizationId)
            .Select(g => new { g.Key, Total = (double?)g.Sum(f => (double)f.FundingCommitmentRequest!.Value) })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

        // Step 5: distinct applicant state counts — unfiltered, shows total geographic reach.
        var stateCountDict = await db.ConsultantApplications
            .Where(a => topEpcIds.Contains(a.ConsultantEpcOrganizationId)
                        && a.ApplicantState != null)
            .Select(a => new { a.ConsultantEpcOrganizationId, a.ApplicantState })
            .Distinct()
            .GroupBy(x => x.ConsultantEpcOrganizationId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        // Step 6: assemble results in ranked order.
        return appGroups.Select(a =>
        {
            var frnCount = frnCountDict.GetValueOrDefault(a.EpcId);
            return new ConsultantSummaryDto(
                ConsultantEpcOrganizationId: a.EpcId,
                ConsultantName: nameDict.GetValueOrDefault(a.EpcId),
                TotalApplications: a.TotalApplications,
                TotalFrns: frnCount,
                TotalFundingAmount: fundedDict.TryGetValue(a.EpcId, out var total) && total.HasValue
                    ? (decimal?)Math.Round((decimal)total.Value, 2)
                    : null,
                ApplicationSharePct: totalApplications > 0
                    ? Math.Round((decimal)a.TotalApplications / totalApplications * 100, 1)
                    : 0m,
                FrnSharePct: totalFrns > 0 && frnCount > 0
                    ? Math.Round((decimal)frnCount / totalFrns * 100, 1)
                    : 0m,
                DistinctStateCount: stateCountDict.GetValueOrDefault(a.EpcId)
            );
        }).ToList();
    }

    // ── GetConsultantDetailsAsync ─────────────────────────────────────────────

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

    // ── GetTrendsAsync ────────────────────────────────────────────────────────

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

    // ── GetStateBreakdownAsync ────────────────────────────────────────────────

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

    // ── GetServiceTypesAsync ──────────────────────────────────────────────────

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

    // ── GetOverviewStatsAsync ─────────────────────────────────────────────────

    /// <summary>
    /// Returns total consultant count, application count, and FRN count
    /// for dashboard summary cards. Accepts optional filters.
    /// </summary>
    public async Task<(int ConsultantCount, int ApplicationCount, int FrnCount)> GetOverviewStatsAsync(
        ConsultantFilterParams? filters = null,
        CancellationToken ct = default)
    {
        var appQuery = ApplyAppFilters(db.ConsultantApplications.AsQueryable(), filters);
        var frnQuery = ApplyFrnFilters(db.ConsultantFrnStatuses.AsQueryable(), filters);

        // If service type filter, restrict consultant count to those with matching FRNs
        if (!string.IsNullOrWhiteSpace(filters?.ServiceType))
        {
            var allowedEpcIds = await frnQuery
                .Select(f => f.ConsultantEpcOrganizationId)
                .Distinct()
                .ToListAsync(ct);
            appQuery = appQuery.Where(a => allowedEpcIds.Contains(a.ConsultantEpcOrganizationId));
        }

        var consultantCount = await appQuery
            .Select(a => a.ConsultantEpcOrganizationId)
            .Distinct()
            .CountAsync(ct);

        var applicationCount = await appQuery.CountAsync(ct);
        var frnCount = await frnQuery.CountAsync(ct);

        return (consultantCount, applicationCount, frnCount);
    }
}
