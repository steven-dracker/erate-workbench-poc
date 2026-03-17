namespace ErateWorkbench.Infrastructure.Reconciliation;

/// <summary>
/// Supplies local database aggregate totals for a single dataset so that
/// <see cref="SocrataReconciliationService"/> can compare them against Socrata source data.
/// Implement one class per dataset and register it in DI.
/// </summary>
public interface ILocalDataProvider
{
    /// <summary>Must match <see cref="SourceDatasetManifest.Name"/> for the corresponding manifest.</summary>
    string DatasetName { get; }

    /// <summary>Returns per-year aggregate totals from the local raw table.</summary>
    Task<IReadOnlyList<LocalYearTotals>> GetLocalRawTotalsAsync(CancellationToken ct = default);
}
