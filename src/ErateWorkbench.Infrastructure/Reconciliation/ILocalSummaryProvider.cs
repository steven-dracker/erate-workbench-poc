namespace ErateWorkbench.Infrastructure.Reconciliation;

/// <summary>
/// Supplies local pre-aggregated summary table totals for a dataset so that
/// <see cref="SocrataReconciliationService"/> can include a third comparison layer
/// (source → raw → summary) in the reconciliation report.
///
/// Implement one class per summary table and pass it as the optional
/// <c>summaryProvider</c> argument to <see cref="SocrataReconciliationService.ReconcileAsync"/>.
/// </summary>
public interface ILocalSummaryProvider
{
    /// <summary>Must match <see cref="SourceDatasetManifest.Name"/> for the corresponding manifest.</summary>
    string DatasetName { get; }

    /// <summary>Returns per-year totals from the local summary table.</summary>
    Task<IReadOnlyList<LocalYearTotals>> GetLocalSummaryTotalsAsync(CancellationToken ct = default);
}
