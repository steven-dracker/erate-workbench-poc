using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure.Reconciliation;

/// <summary>
/// Fetches aggregate totals from a Socrata dataset using SoQL, then joins them with
/// local database totals from an <see cref="ILocalDataProvider"/> to produce a
/// <see cref="DatasetReconciliationResult"/> that surfaces row-count and amount variances.
/// </summary>
public sealed class SocrataReconciliationService
{
    private const string SocrataBase = "https://datahub.usac.org";

    private readonly HttpClient http;
    private readonly ILogger<SocrataReconciliationService>? logger;

    public SocrataReconciliationService(
        HttpClient http,
        ILogger<SocrataReconciliationService>? logger = null)
    {
        this.http   = http;
        this.logger = logger;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task<DatasetReconciliationResult> ReconcileAsync(
        SourceDatasetManifest manifest,
        ILocalDataProvider localProvider,
        ILocalSummaryProvider? summaryProvider = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        logger?.LogInformation("[reconcile:{Dataset}] Starting", manifest.Name);

        var (sourceTotalCount, sourceByYear) = await FetchSourceTotalsAsync(manifest, ct);
        var localByYear = await localProvider.GetLocalRawTotalsAsync(ct);

        var localByYearMap  = localByYear.ToDictionary(r => r.FundingYear);
        var localTotalCount = localByYear.Sum(r => r.RowCount);

        Dictionary<int, LocalYearTotals>? summaryByYearMap = null;
        if (summaryProvider is not null)
        {
            var summaryByYear = await summaryProvider.GetLocalSummaryTotalsAsync(ct);
            summaryByYearMap  = summaryByYear.ToDictionary(r => r.FundingYear);
            logger?.LogDebug("[reconcile:{Dataset}] Summary data loaded — years={Years}",
                manifest.Name, summaryByYearMap.Count);
        }

        var allYears = sourceByYear.Select(r => r.FundingYear)
            .Union(localByYear.Select(r => r.FundingYear))
            .OrderBy(y => y)
            .ToList();

        var rows = allYears.Select(year =>
        {
            var src = sourceByYear.FirstOrDefault(r => r.FundingYear == year);
            localByYearMap.TryGetValue(year, out var loc);
            LocalYearTotals? sumRow = null;
            summaryByYearMap?.TryGetValue(year, out sumRow);
            return new YearReconciliationRow
            {
                FundingYear                    = year,
                SourceRowCount                 = src?.SourceRowCount ?? 0,
                SourceDistinctApplicants       = src?.SourceDistinctApplicants,
                SourceAmounts                  = src?.SourceAmounts ?? new Dictionary<string, decimal>(),
                LocalRawRowCount               = loc?.RowCount ?? 0,
                LocalRawDistinctApplicants     = loc?.DistinctApplicants,
                LocalRawAmounts                = loc?.Amounts ?? new Dictionary<string, decimal>(),
                LocalSummaryRowCount           = sumRow?.RowCount,
                LocalSummaryDistinctApplicants = sumRow?.DistinctApplicants,
                LocalSummaryAmounts            = sumRow?.Amounts,
            };
        }).ToList();

        sw.Stop();
        var hasVariance = rows.Any(r => r.HasVariance);
        logger?.LogInformation(
            "[reconcile:{Dataset}] Done in {ElapsedMs}ms — Years={Years}, HasVariance={HasVariance}",
            manifest.Name, sw.ElapsedMilliseconds, rows.Count, hasVariance);

        return new DatasetReconciliationResult
        {
            DatasetName          = manifest.Name,
            RunAtUtc             = DateTime.UtcNow,
            SourceTotalRowCount  = sourceTotalCount,
            LocalRawTotalRowCount = localTotalCount,
            Rows                 = rows,
            Notes                = manifest.Notes,
            AmountDisplayNames   = manifest.AmountMetrics
                .ToDictionary(m => m.LocalProperty, m => m.DisplayName),
        };
    }

    // ── Socrata fetching ─────────────────────────────────────────────────────

    internal async Task<(long TotalCount, List<SourceYearTotals> ByYear)> FetchSourceTotalsAsync(
        SourceDatasetManifest manifest, CancellationToken ct)
    {
        var totalUrl  = BuildTotalCountUrl(manifest);
        var byYearUrl = BuildByYearUrl(manifest);

        logger?.LogDebug("[reconcile:{Dataset}] Total-count URL: {Url}", manifest.Name, totalUrl);
        logger?.LogDebug("[reconcile:{Dataset}] By-year URL: {Url}",     manifest.Name, byYearUrl);

        var totalCount = await FetchTotalCountAsync(totalUrl, ct);
        var byYear     = await FetchByYearAsync(manifest, byYearUrl, ct);
        return (totalCount, byYear);
    }

    internal string BuildTotalCountUrl(SourceDatasetManifest manifest)
    {
        var select = Uri.EscapeDataString("count(*) as row_count");
        return $"{SocrataBase}/resource/{manifest.DatasetId}.json?$select={select}";
    }

    internal string BuildByYearUrl(SourceDatasetManifest manifest)
    {
        var parts = new List<string> { manifest.YearColumn, "count(*) as row_count" };

        if (manifest.SupportsDistinctApplicantCount && manifest.ApplicantColumn is not null)
            parts.Add($"count(distinct {manifest.ApplicantColumn}) as distinct_applicants");

        foreach (var m in manifest.AmountMetrics)
            parts.Add($"sum({m.SourceColumn}) as {m.SourceColumn}");

        var select = Uri.EscapeDataString(string.Join(", ", parts));
        var group  = Uri.EscapeDataString(manifest.YearColumn);
        return $"{SocrataBase}/resource/{manifest.DatasetId}.json" +
               $"?$select={select}&$group={group}&$order={group}&$limit=100";
    }

    private async Task<long> FetchTotalCountAsync(string url, CancellationToken ct)
    {
        var json = await http.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;
        if (arr.GetArrayLength() == 0) return 0;
        return ParseLong(arr[0], "row_count");
    }

    private async Task<List<SourceYearTotals>> FetchByYearAsync(
        SourceDatasetManifest manifest, string url, CancellationToken ct)
    {
        var json = await http.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);
        var results = new List<SourceYearTotals>();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var year = (int)ParseLong(element, manifest.YearColumn);
            if (year == 0) continue; // skip rows with null/missing year

            var amounts = new Dictionary<string, decimal>();
            foreach (var m in manifest.AmountMetrics)
                amounts[m.LocalProperty] = ParseDecimal(element, m.SourceColumn);

            results.Add(new SourceYearTotals
            {
                FundingYear              = year,
                SourceRowCount           = ParseLong(element, "row_count"),
                SourceDistinctApplicants = manifest.SupportsDistinctApplicantCount
                    ? ParseLong(element, "distinct_applicants")
                    : null,
                SourceAmounts = amounts,
            });
        }

        return results;
    }

    // ── JSON parsing helpers (Socrata returns all values as strings) ─────────

    internal static long ParseLong(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var prop)) return 0;
        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.TryGetInt64(out var n) ? n : (long)prop.GetDouble(),
            JsonValueKind.String => long.TryParse(prop.GetString(), out var n) ? n : 0,
            _                   => 0,
        };
    }

    internal static decimal ParseDecimal(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var prop)) return 0m;
        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.TryGetDecimal(out var d) ? d : 0m,
            JsonValueKind.String => decimal.TryParse(
                prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m,
            _ => 0m,
        };
    }
}
