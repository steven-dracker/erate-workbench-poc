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
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public int ConsultantCount { get; private set; }
    public int ApplicationCount { get; private set; }

    /// <summary>JSON: [{consultantEpcOrganizationId, consultantName, totalApplications, totalFrns, totalFundingAmount}, …]</summary>
    public string TopConsultantsJson { get; private set; } = "[]";

    public async Task OnGetAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        var (consultantCount, applicationCount) = await cache.GetOrCreateAsync(
            "consultants:overview",
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
                return analytics.GetOverviewStatsAsync(ct);
            });

        ConsultantCount = consultantCount;
        ApplicationCount = applicationCount;

        var top = await cache.GetOrCreateAsync("consultants:top25", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return analytics.GetTopConsultantsAsync(25, ct);
        });

        TopConsultantsJson = JsonSerializer.Serialize(top, JsonOpts);

        logger.LogInformation("ConsultantIntelligence loaded in {Ms}ms", sw.ElapsedMilliseconds);
    }
}
