namespace ErateWorkbench.Infrastructure.Reconciliation;

/// <summary>
/// Full reconciliation result for one dataset run, covering all funding years found in source or local data.
/// </summary>
public sealed class DatasetReconciliationResult
{
    public required string DatasetName { get; init; }
    public DateTime RunAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Total row count returned by the Socrata source.</summary>
    public long SourceTotalRowCount { get; init; }

    /// <summary>Total row count in the local raw table.</summary>
    public long LocalRawTotalRowCount { get; init; }

    /// <summary>Per-year rows, ordered by FundingYear ascending.</summary>
    public IReadOnlyList<YearReconciliationRow> Rows { get; init; } = [];

    /// <summary>Maps LocalProperty keys (e.g. "TotalEligibleAmount") to display names for reports.</summary>
    public IReadOnlyDictionary<string, string> AmountDisplayNames { get; init; } = new Dictionary<string, string>();

    /// <summary>Dataset-level notes from the manifest.</summary>
    public string? Notes { get; init; }

    public bool HasAnyVariance => Rows.Any(r => r.HasVariance);
}

/// <summary>
/// Source vs local comparison for a single funding year within a dataset.
/// </summary>
public sealed class YearReconciliationRow
{
    public required int FundingYear { get; init; }

    // ── Source totals ────────────────────────────────────────────────────────
    public long SourceRowCount { get; init; }
    public long? SourceDistinctApplicants { get; init; }

    /// <summary>Keyed by <see cref="AmountMetricDefinition.LocalProperty"/>.</summary>
    public IReadOnlyDictionary<string, decimal> SourceAmounts { get; init; } = new Dictionary<string, decimal>();

    // ── Local raw totals ─────────────────────────────────────────────────────
    public long LocalRawRowCount { get; init; }
    public long? LocalRawDistinctApplicants { get; init; }

    /// <summary>Keyed by <see cref="AmountMetricDefinition.LocalProperty"/>.</summary>
    public IReadOnlyDictionary<string, decimal> LocalRawAmounts { get; init; } = new Dictionary<string, decimal>();

    // ── Local summary totals (populated once summary tables exist) ───────────
    public long? LocalSummaryRowCount { get; init; }
    public long? LocalSummaryDistinctApplicants { get; init; }
    public IReadOnlyDictionary<string, decimal>? LocalSummaryAmounts { get; init; }

    // ── Derived variance: Source vs Raw ──────────────────────────────────────

    /// <summary>Raw − Source. Positive = local has more rows than source.</summary>
    public long RowCountVariance => LocalRawRowCount - SourceRowCount;

    public double RowCountVariancePct =>
        SourceRowCount == 0 ? 0.0
        : Math.Round((double)RowCountVariance / SourceRowCount * 100.0, 2);

    public long? ApplicantCountVariance =>
        SourceDistinctApplicants.HasValue && LocalRawDistinctApplicants.HasValue
            ? LocalRawDistinctApplicants.Value - SourceDistinctApplicants.Value
            : null;

    /// <summary>Per-metric amount variance (Raw − Source), keyed by LocalProperty.</summary>
    public IReadOnlyDictionary<string, decimal> AmountVariances =>
        SourceAmounts.Keys
            .Concat(LocalRawAmounts.Keys)
            .Distinct()
            .ToDictionary(k => k, k =>
                (LocalRawAmounts.TryGetValue(k, out var loc) ? loc : 0m) -
                (SourceAmounts.TryGetValue(k, out var src) ? src : 0m));

    // ── Derived variance: Raw vs Summary ─────────────────────────────────────

    /// <summary>Summary − Raw. Null when no summary data is available.</summary>
    public long? RawVsSummaryRowCountVariance =>
        LocalSummaryRowCount.HasValue ? LocalSummaryRowCount.Value - LocalRawRowCount : null;

    public long? RawVsSummaryApplicantVariance =>
        LocalSummaryDistinctApplicants.HasValue && LocalRawDistinctApplicants.HasValue
            ? LocalSummaryDistinctApplicants.Value - LocalRawDistinctApplicants.Value
            : null;

    public IReadOnlyDictionary<string, decimal>? RawVsSummaryAmountVariances =>
        LocalSummaryAmounts is null ? null
        : LocalRawAmounts.Keys
            .Concat(LocalSummaryAmounts.Keys)
            .Distinct()
            .ToDictionary(k => k, k =>
                (LocalSummaryAmounts.TryGetValue(k, out var sum) ? sum : 0m) -
                (LocalRawAmounts.TryGetValue(k, out var raw)     ? raw : 0m));

    // ── Derived variance: Source vs Summary ──────────────────────────────────

    public long? SourceVsSummaryRowCountVariance =>
        LocalSummaryRowCount.HasValue ? LocalSummaryRowCount.Value - SourceRowCount : null;

    public IReadOnlyDictionary<string, decimal>? SourceVsSummaryAmountVariances =>
        LocalSummaryAmounts is null ? null
        : SourceAmounts.Keys
            .Concat(LocalSummaryAmounts.Keys)
            .Distinct()
            .ToDictionary(k => k, k =>
                (LocalSummaryAmounts.TryGetValue(k, out var sum) ? sum : 0m) -
                (SourceAmounts.TryGetValue(k, out var src)       ? src : 0m));

    public bool HasVariance =>
        RowCountVariance != 0 ||
        (ApplicantCountVariance.HasValue && ApplicantCountVariance.Value != 0) ||
        AmountVariances.Values.Any(v => v != 0m);
}

/// <summary>
/// Local aggregate totals for one year, returned by <see cref="ILocalDataProvider"/>.
/// Amounts keyed by <see cref="AmountMetricDefinition.LocalProperty"/>.
/// </summary>
public sealed class LocalYearTotals
{
    public required int FundingYear { get; init; }
    public long RowCount { get; init; }
    public long? DistinctApplicants { get; init; }
    public IReadOnlyDictionary<string, decimal> Amounts { get; init; } = new Dictionary<string, decimal>();
}

/// <summary>
/// Source aggregate totals for one year, parsed from a Socrata aggregate response.
/// Internal to <see cref="SocrataReconciliationService"/>.
/// </summary>
internal sealed class SourceYearTotals
{
    public required int FundingYear { get; init; }
    public long SourceRowCount { get; init; }
    public long? SourceDistinctApplicants { get; init; }
    public IReadOnlyDictionary<string, decimal> SourceAmounts { get; init; } = new Dictionary<string, decimal>();
}
