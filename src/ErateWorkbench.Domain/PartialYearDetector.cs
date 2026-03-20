namespace ErateWorkbench.Domain;

/// <summary>
/// Determines whether a selected funding year is the most recent (partial) year.
/// The latest year in the dataset is always treated as partial — it may represent
/// an in-progress funding cycle where signals like "No Disbursement" are expected
/// and do not indicate anomalies.
/// </summary>
public static class PartialYearDetector
{
    /// <summary>
    /// Returns true when <paramref name="selectedYear"/> is the maximum year in
    /// <paramref name="availableYears"/>, indicating it is likely a partial year.
    /// Returns false when no year is selected (null) or the year list is empty.
    /// </summary>
    public static bool IsPartialYear(IReadOnlyCollection<int> availableYears, int? selectedYear)
    {
        if (availableYears.Count == 0 || !selectedYear.HasValue)
            return false;

        return selectedYear.Value == availableYears.Max();
    }
}
