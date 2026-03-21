using System.Diagnostics;
using System.Text.Json;
using ErateWorkbench.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;

namespace ErateWorkbench.Api.Pages;

public class FilingWindowModel(
    FilingWindowRepository repo,
    IMemoryCache cache,
    ILogger<FilingWindowModel> logger) : PageModel
{
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24);

    /// <summary>
    /// JSON: [{year, day, count}, …] — daily certification counts per funding year.
    /// Client JS accumulates these into cumulative series for the timing chart.
    /// </summary>
    public string TimingJson { get; private set; } = "[]";

    /// <summary>
    /// JSON: [{year, appCount, requested, committed}, …] — newest year first.
    /// </summary>
    public string RequestedVsCommittedJson { get; private set; } = "[]";

    /// <summary>
    /// JSON: [{year, status, count}, …] — newest year first.
    /// </summary>
    public string AppStatusJson { get; private set; } = "[]";

    /// <summary>FY2026 progress vs FY2025 for the progress cards.</summary>
    public Fy2026ProgressRow Progress { get; private set; } =
        new(0, 0m, 0, 0m);

    public async Task OnGetAsync(CancellationToken ct)
    {
        bool cacheHit = cache.TryGetValue("filingwindow:timing", out _);
        var sw = Stopwatch.StartNew();

        var timing = await cache.GetOrCreateAsync("filingwindow:timing", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return repo.GetSubmissionTimingAsync(ct);
        });

        var reqVsComm = await cache.GetOrCreateAsync("filingwindow:req-vs-comm", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return repo.GetRequestedVsCommittedByYearAsync(ct);
        });

        var statuses = await cache.GetOrCreateAsync("filingwindow:statuses", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return repo.GetApplicationStatusByYearAsync(ct);
        });

        var progress = await cache.GetOrCreateAsync("filingwindow:progress", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
            return repo.GetFy2026ProgressAsync(ct);
        });

        sw.Stop();
        logger.LogInformation(
            "FilingWindow data loaded ({CacheHit}, {ElapsedMs}ms)",
            cacheHit ? "cache-hit" : "db-query",
            sw.ElapsedMilliseconds);

        TimingJson = JsonSerializer.Serialize(
            timing!.Select(r => new { year = r.FundingYear, day = r.DayOfYear, count = r.Count }));

        RequestedVsCommittedJson = JsonSerializer.Serialize(
            reqVsComm!.Select(r => new
            {
                year = r.FundingYear,
                appCount = r.ApplicationCount,
                requested = (double)r.TotalRequested,
                committed = (double)r.TotalCommitted,
            }));

        AppStatusJson = JsonSerializer.Serialize(
            statuses!.Select(r => new { year = r.FundingYear, status = r.Status, count = r.Count }));

        Progress = progress!;
    }
}
