using System.Diagnostics;
using System.Text.Json;
using ErateWorkbench.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;

namespace ErateWorkbench.Api.Pages;

public class ConsultantIntelligenceModel(
    ConsultantAnalyticsService analytics,
    IMemoryCache cache,
    ILogger<ConsultantIntelligenceModel> logger) : PageModel
{
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ── Filter binding ────────────────────────────────────────────────────────

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public string? State { get; set; }

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public string? ServiceType { get; set; }

    public bool HasActiveFilters =>
        Year.HasValue ||
        !string.IsNullOrWhiteSpace(State) ||
        !string.IsNullOrWhiteSpace(ServiceType);

    // ── Page data ─────────────────────────────────────────────────────────────

    public int ConsultantCount { get; private set; }
    public int ApplicationCount { get; private set; }
    public int FrnCount { get; private set; }

    public ConsultantFilterOptionsDto FilterOptions { get; private set; } =
        new([], [], []);

    /// <summary>JSON: [{consultantEpcOrganizationId, consultantName, totalApplications, totalFrns,
    /// totalFundingAmount, applicationSharePct, frnSharePct, distinctStateCount}, …]</summary>
    public string TopConsultantsJson { get; private set; } = "[]";

    // ── Insight cards ─────────────────────────────────────────────────────────

    public string? InsightLeaderName { get; private set; }
    public string? InsightLeaderDetail { get; private set; }

    public string? InsightFundingName { get; private set; }
    public string? InsightFundingDetail { get; private set; }

    public string? InsightReachName { get; private set; }
    public string? InsightReachDetail { get; private set; }

    public string? InsightConcentration { get; private set; }

    // ── E-Rate Central ────────────────────────────────────────────────────────

    public bool ERateCentralIsInResults { get; private set; }

    // ── OnGetAsync ────────────────────────────────────────────────────────────

    public async Task OnGetAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // Filter options are always cached (unaffected by active filters)
        FilterOptions = (await cache.GetOrCreateAsync("consultants:filterOptions", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return analytics.GetAvailableFiltersAsync(ct);
        }))!;

        // Build active filter params (null = no filters)
        var filters = HasActiveFilters
            ? new ConsultantFilterParams(Year, State, ServiceType)
            : null;

        // Overview stats — bypass cache when filters are active
        (int ConsultantCount, int ApplicationCount, int FrnCount) overview;
        if (HasActiveFilters)
        {
            overview = await analytics.GetOverviewStatsAsync(filters, ct);
        }
        else
        {
            overview = await cache.GetOrCreateAsync("consultants:overview", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
                return analytics.GetOverviewStatsAsync(null, ct);
            });
        }
        ConsultantCount = overview.ConsultantCount;
        ApplicationCount = overview.ApplicationCount;
        FrnCount = overview.FrnCount;

        // Top consultants — bypass cache when filters are active
        List<ConsultantSummaryDto>? top;
        if (HasActiveFilters)
        {
            top = await analytics.GetTopConsultantsAsync(25, filters, ct);
        }
        else
        {
            top = await cache.GetOrCreateAsync("consultants:top25", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
                return analytics.GetTopConsultantsAsync(25, null, ct);
            });
        }
        top ??= [];

        TopConsultantsJson = JsonSerializer.Serialize(top, JsonOpts);

        // E-Rate Central in results?
        ERateCentralIsInResults = top.Any(
            c => c.ConsultantEpcOrganizationId == ConsultantConstants.ERateCentralEpcId);

        // Insight cards
        ComputeInsights(top);

        logger.LogInformation(
            "ConsultantIntelligence loaded in {Ms}ms (filters active: {HasFilters})",
            sw.ElapsedMilliseconds, HasActiveFilters);
    }

    private void ComputeInsights(List<ConsultantSummaryDto> top)
    {
        if (top.Count == 0) return;

        // Market leader by application count (already ranked first)
        var leader = top[0];
        InsightLeaderName = leader.ConsultantName ?? leader.ConsultantEpcOrganizationId;
        InsightLeaderDetail = $"{leader.TotalApplications:N0} applications · {leader.ApplicationSharePct:F1}% share";

        // Top by funded total
        var topFunded = top.MaxBy(c => c.TotalFundingAmount ?? 0);
        if (topFunded?.TotalFundingAmount > 0)
        {
            InsightFundingName = topFunded.ConsultantName ?? topFunded.ConsultantEpcOrganizationId;
            InsightFundingDetail = $"${topFunded.TotalFundingAmount!.Value:N0} funded";
        }

        // Widest geographic reach
        var mostDistributed = top.MaxBy(c => c.DistinctStateCount);
        if (mostDistributed?.DistinctStateCount > 0)
        {
            InsightReachName = mostDistributed.ConsultantName ?? mostDistributed.ConsultantEpcOrganizationId;
            InsightReachDetail = $"{mostDistributed.DistinctStateCount} states";
        }

        // Top-5 market concentration
        if (top.Count >= 2)
        {
            var top5Share = top.Take(5).Sum(c => c.ApplicationSharePct);
            InsightConcentration = $"Top {Math.Min(5, top.Count)} hold {top5Share:F1}% of applications";
        }
    }
}
