using System.Diagnostics;
using System.Text.Json;
using ErateWorkbench.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;

namespace ErateWorkbench.Api.Pages;

public class AnalyticsModel(
    EpcEntityRepository repo,
    ImportJobService importJobService,
    FundingCommitmentRepository fundingRepo,
    IMemoryCache cache,
    ILogger<AnalyticsModel> logger) : PageModel
{
    // Analytics data is derived from imports that are triggered explicitly.
    // Cache for 24 hours — a new import will not invalidate the cache mid-session,
    // which is acceptable for a demo tool. Restart the app to force a cache flush.
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24);

    public string EntitiesByStateJson { get; private set; } = "[]";
    public string EntitiesByTypeJson { get; private set; } = "[]";
    public string TopDiscountStatesJson { get; private set; } = "[]";
    public string FundingByYearJson { get; private set; } = "[]";
    public string FundingByCategoryJson { get; private set; } = "[]";
    public string TopFundedEntitiesJson { get; private set; } = "[]";
    public ImportSummaryDto Summary { get; private set; } = new(0, 0, 0, null, null, null, null);

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Determine cache status before the first GetOrCreate so we can log it accurately.
        // All six keys are populated together, so checking one is sufficient.
        bool cacheHit = cache.TryGetValue("analytics:by-state", out _);
        var sw = Stopwatch.StartNew();

        // Each query is wrapped in cache-aside. On a cache hit the DB is not touched.
        // Queries remain sequential to avoid concurrent-operation exceptions on the
        // shared scoped AppDbContext (SQLite EF Core does not support parallel ops).

        var byState = await cache.GetOrCreateAsync("analytics:by-state", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return repo.GetCountByStateAsync(ct);
        });

        var byType = await cache.GetOrCreateAsync("analytics:by-type", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return repo.GetCountByTypeAsync(ct);
        });

        var discount = await cache.GetOrCreateAsync("analytics:discount-by-state", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return repo.GetDiscountRatesByStateAsync(20, ct);
        });

        var fundingByYear = await cache.GetOrCreateAsync("analytics:funding-by-year", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return fundingRepo.GetFundingByYearAsync(ct);
        });

        var fundingByCategory = await cache.GetOrCreateAsync("analytics:funding-by-category", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return fundingRepo.GetFundingByCategoryAsync(ct);
        });

        var topEntities = await cache.GetOrCreateAsync("analytics:top-entities", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return fundingRepo.GetTopFundedEntitiesAsync(10, ct);
        });

        // Import summary reflects live job state — not cached
        var (total, succeeded, failed, last) = await importJobService.GetSummaryAsync(ct);

        sw.Stop();
        logger.LogInformation(
            "Analytics page rendered in {ElapsedMs}ms ({CacheStatus})",
            sw.ElapsedMilliseconds,
            cacheHit ? "cache hit" : "cache miss — queries executed");

        EntitiesByStateJson = JsonSerializer.Serialize(
            byState!.Take(15)
                   .Where(r => !string.IsNullOrEmpty(r.Item1))
                   .Select(r => new { state = r.Item1, count = r.Item2 }));

        EntitiesByTypeJson = JsonSerializer.Serialize(
            byType!.Select(r => new { type = r.Item1.ToString(), count = r.Item2 }));

        TopDiscountStatesJson = JsonSerializer.Serialize(
            discount!.Select(r => new
            {
                state = r.Item1,
                avgCat1 = Math.Round(r.Item3, 1),
                avgCat2 = Math.Round(r.Item4, 1),
            }));

        FundingByYearJson = JsonSerializer.Serialize(
            fundingByYear!.Select(r => new { year = r.FundingYear, total = r.TotalCommitted, count = r.CommitmentCount }));

        FundingByCategoryJson = JsonSerializer.Serialize(
            fundingByCategory!.Select(r => new { category = r.Category, total = r.TotalCommitted }));

        TopFundedEntitiesJson = JsonSerializer.Serialize(
            topEntities!.Select(r => new { name = r.EntityName, state = r.State ?? "—", total = r.TotalCommitted }));

        Summary = new ImportSummaryDto(
            total, succeeded, failed,
            last?.StartedAt, last?.CompletedAt,
            last?.RecordsProcessed, last?.DatasetName);
    }
}
