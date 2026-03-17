using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErateWorkbench.Infrastructure.Reconciliation;

/// <summary>
/// Writes reconciliation results to markdown and/or JSON files.
/// Call <see cref="BuildMarkdown"/> to get the markdown string directly (useful for testing).
/// When summary data is present the report includes a three-layer comparison:
/// Source → Raw → Summary, with variance columns for each transition.
/// </summary>
public sealed class ReconciliationReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
    };

    public async Task WriteMarkdownAsync(
        DatasetReconciliationResult result, string path, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, BuildMarkdown(result), Encoding.UTF8, ct);
    }

    public async Task WriteJsonAsync(
        DatasetReconciliationResult result, string path, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(result, JsonOptions), Encoding.UTF8, ct);
    }

    public string BuildMarkdown(DatasetReconciliationResult result)
    {
        var sb          = new StringBuilder();
        var hasSummary  = result.Rows.Any(r => r.LocalSummaryRowCount.HasValue);
        var amountKeys  = result.AmountDisplayNames.Keys.ToList();

        // ── Header ───────────────────────────────────────────────────────────
        sb.AppendLine($"# Reconciliation Report: {result.DatasetName}");
        sb.AppendLine();
        sb.AppendLine($"**Run at:** {result.RunAtUtc:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Status:** {(result.HasAnyVariance ? "⚠ Variance detected" : "✓ No variance")}");
        if (hasSummary) sb.AppendLine($"**Layers:** Source · Raw · Summary");
        sb.AppendLine();

        // ── Notes ─────────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(result.Notes))
        {
            sb.AppendLine("## Notes");
            sb.AppendLine();
            sb.AppendLine($"> {result.Notes}");
            sb.AppendLine();
        }

        // ── Summary totals ───────────────────────────────────────────────────
        sb.AppendLine("## Summary");
        sb.AppendLine();
        if (hasSummary)
        {
            sb.AppendLine("| | Source | Raw | Summary |");
            sb.AppendLine("|---|---:|---:|---:|");
            var sumTotal = result.Rows.Sum(r => r.LocalSummaryRowCount ?? 0);
            sb.AppendLine($"| Total rows | {result.SourceTotalRowCount:N0} | {result.LocalRawTotalRowCount:N0} | {sumTotal:N0} |");
        }
        else
        {
            sb.AppendLine("| | Source | Raw | Src→Raw Δ |");
            sb.AppendLine("|---|---:|---:|---:|");
            var totalVar  = result.LocalRawTotalRowCount - result.SourceTotalRowCount;
            var totalSign = totalVar >= 0 ? "+" : "";
            sb.AppendLine($"| Total rows | {result.SourceTotalRowCount:N0} | {result.LocalRawTotalRowCount:N0} | {totalSign}{totalVar:N0} |");
        }
        sb.AppendLine();

        // ── Row Counts ───────────────────────────────────────────────────────
        sb.AppendLine("## Row Counts by Funding Year");
        sb.AppendLine();
        AppendRowCountTable(sb, result, hasSummary);
        sb.AppendLine();

        // ── Distinct Applicants ──────────────────────────────────────────────
        var hasApplicants = result.Rows.Any(r =>
            r.SourceDistinctApplicants.HasValue || r.LocalRawDistinctApplicants.HasValue);
        if (hasApplicants)
        {
            sb.AppendLine("## Distinct Applicants by Funding Year");
            sb.AppendLine();
            AppendApplicantTable(sb, result, hasSummary);
            sb.AppendLine();
        }

        // ── Per-amount metric tables ─────────────────────────────────────────
        foreach (var key in amountKeys)
        {
            var displayName = result.AmountDisplayNames.TryGetValue(key, out var d) ? d : key;
            sb.AppendLine($"## {displayName} by Funding Year");
            sb.AppendLine();
            AppendAmountTable(sb, result, key, displayName, hasSummary);
            sb.AppendLine();
        }

        // ── Legend ───────────────────────────────────────────────────────────
        sb.AppendLine("---");
        sb.AppendLine();
        if (hasSummary)
        {
            sb.AppendLine("**Src→Raw Δ** = Raw − Source  |  **Raw→Sum Δ** = Summary − Raw  |  **Src→Sum Δ** = Summary − Source  ");
            sb.AppendLine("Positive Δ = local/summary has more than source; negative Δ = local/summary is missing rows/amount.  ");
        }
        else
        {
            sb.AppendLine("**Src→Raw Δ** = Raw − Source. Positive: local has more than source; negative: local is missing rows/amount.  ");
        }
        sb.AppendLine("Amounts shown in $M (millions).");

        return sb.ToString();
    }

    // ── Table builders ───────────────────────────────────────────────────────

    private static void AppendRowCountTable(
        StringBuilder sb, DatasetReconciliationResult result, bool hasSummary)
    {
        if (hasSummary)
        {
            sb.AppendLine("| Year | Source | Raw | Summary | Src→Raw Δ | Src→Raw Δ% | Raw→Sum Δ | Src→Sum Δ |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var row in result.Rows.OrderBy(r => r.FundingYear))
            {
                var srcRaw  = row.RowCountVariance;
                var rawSum  = row.RawVsSummaryRowCountVariance;
                var srcSum  = row.SourceVsSummaryRowCountVariance;
                sb.AppendLine($"| {row.FundingYear} | {row.SourceRowCount:N0} | {row.LocalRawRowCount:N0} " +
                              $"| {row.LocalSummaryRowCount?.ToString("N0") ?? "—"} " +
                              $"| {Sign(srcRaw)}{srcRaw:N0} | {Sign(srcRaw)}{row.RowCountVariancePct:F1}% " +
                              $"| {(rawSum.HasValue ? $"{Sign(rawSum.Value)}{rawSum:N0}" : "—")} " +
                              $"| {(srcSum.HasValue ? $"{Sign(srcSum.Value)}{srcSum:N0}" : "—")} |");
            }
        }
        else
        {
            sb.AppendLine("| Year | Source | Raw | Src→Raw Δ | Src→Raw Δ% |");
            sb.AppendLine("|---:|---:|---:|---:|---:|");
            foreach (var row in result.Rows.OrderBy(r => r.FundingYear))
            {
                var v = row.RowCountVariance;
                sb.AppendLine($"| {row.FundingYear} | {row.SourceRowCount:N0} | {row.LocalRawRowCount:N0} " +
                              $"| {Sign(v)}{v:N0} | {Sign(v)}{row.RowCountVariancePct:F1}% |");
            }
        }
    }

    private static void AppendApplicantTable(
        StringBuilder sb, DatasetReconciliationResult result, bool hasSummary)
    {
        if (hasSummary)
        {
            sb.AppendLine("| Year | Source | Raw | Summary | Src→Raw Δ | Raw→Sum Δ | Src→Sum Δ |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var row in result.Rows.OrderBy(r => r.FundingYear))
            {
                var srcRaw = row.ApplicantCountVariance;
                var rawSum = row.RawVsSummaryApplicantVariance;
                var srcSum = row.LocalSummaryDistinctApplicants.HasValue && row.SourceDistinctApplicants.HasValue
                    ? (long?)(row.LocalSummaryDistinctApplicants.Value - row.SourceDistinctApplicants.Value)
                    : null;
                sb.AppendLine($"| {row.FundingYear} " +
                              $"| {row.SourceDistinctApplicants?.ToString("N0") ?? "—"} " +
                              $"| {row.LocalRawDistinctApplicants?.ToString("N0") ?? "—"} " +
                              $"| {row.LocalSummaryDistinctApplicants?.ToString("N0") ?? "—"} " +
                              $"| {(srcRaw.HasValue ? $"{Sign(srcRaw.Value)}{srcRaw:N0}" : "—")} " +
                              $"| {(rawSum.HasValue ? $"{Sign(rawSum.Value)}{rawSum:N0}" : "—")} " +
                              $"| {(srcSum.HasValue ? $"{Sign(srcSum.Value)}{srcSum:N0}" : "—")} |");
            }
        }
        else
        {
            sb.AppendLine("| Year | Source | Raw | Src→Raw Δ |");
            sb.AppendLine("|---:|---:|---:|---:|");
            foreach (var row in result.Rows.OrderBy(r => r.FundingYear))
            {
                var v = row.ApplicantCountVariance;
                sb.AppendLine($"| {row.FundingYear} " +
                              $"| {row.SourceDistinctApplicants?.ToString("N0") ?? "—"} " +
                              $"| {row.LocalRawDistinctApplicants?.ToString("N0") ?? "—"} " +
                              $"| {(v.HasValue ? $"{Sign(v.Value)}{v:N0}" : "—")} |");
            }
        }
    }

    private static void AppendAmountTable(
        StringBuilder sb, DatasetReconciliationResult result,
        string key, string displayName, bool hasSummary)
    {
        if (hasSummary)
        {
            sb.AppendLine($"| Year | Source {displayName} | Raw {displayName} | Summary {displayName} | Src→Raw Δ | Raw→Sum Δ | Src→Sum Δ |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var row in result.Rows.OrderBy(r => r.FundingYear))
            {
                var src    = row.SourceAmounts.TryGetValue(key, out var s)   ? s : 0m;
                var raw    = row.LocalRawAmounts.TryGetValue(key, out var r2) ? r2 : 0m;
                var sum    = row.LocalSummaryAmounts?.TryGetValue(key, out var su) == true ? su : (decimal?)null;
                var srcRaw = row.AmountVariances.TryGetValue(key, out var v) ? v : 0m;
                var rawSum = row.RawVsSummaryAmountVariances?.TryGetValue(key, out var rv) == true ? rv : (decimal?)null;
                var srcSum = row.SourceVsSummaryAmountVariances?.TryGetValue(key, out var sv) == true ? sv : (decimal?)null;
                sb.AppendLine($"| {row.FundingYear} " +
                              $"| {FormatM(src)} | {FormatM(raw)} " +
                              $"| {(sum.HasValue ? FormatM(sum.Value) : "—")} " +
                              $"| {SignM(srcRaw)}{FormatM(srcRaw)} " +
                              $"| {(rawSum.HasValue ? $"{SignM(rawSum.Value)}{FormatM(rawSum.Value)}" : "—")} " +
                              $"| {(srcSum.HasValue ? $"{SignM(srcSum.Value)}{FormatM(srcSum.Value)}" : "—")} |");
            }
        }
        else
        {
            sb.AppendLine($"| Year | Source {displayName} | Raw {displayName} | Src→Raw Δ |");
            sb.AppendLine("|---:|---:|---:|---:|");
            foreach (var row in result.Rows.OrderBy(r => r.FundingYear))
            {
                var src = row.SourceAmounts.TryGetValue(key, out var s)  ? s : 0m;
                var raw = row.LocalRawAmounts.TryGetValue(key, out var r2) ? r2 : 0m;
                var v   = row.AmountVariances.TryGetValue(key, out var av) ? av : 0m;
                sb.AppendLine($"| {row.FundingYear} | {FormatM(src)} | {FormatM(raw)} | {SignM(v)}{FormatM(v)} |");
            }
        }
    }

    private static string Sign(long v)    => v >= 0 ? "+" : "";
    private static string SignM(decimal v) => v >= 0 ? "+" : "";
    private static string FormatM(decimal amount) =>
        amount == 0m ? "$0" : $"${amount / 1_000_000m:N1}M";
}
