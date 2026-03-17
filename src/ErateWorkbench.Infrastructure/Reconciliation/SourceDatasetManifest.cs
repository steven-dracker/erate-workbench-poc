namespace ErateWorkbench.Infrastructure.Reconciliation;

/// <summary>
/// Describes a USAC Socrata source dataset and how to reconcile it against local data.
/// Add entries to <see cref="DatasetManifests"/> to register additional datasets.
/// </summary>
public sealed record SourceDatasetManifest
{
    /// <summary>Human-readable name matching the local table / entity name.</summary>
    public required string Name { get; init; }

    /// <summary>Socrata dataset resource ID (e.g. "avi8-svp9").</summary>
    public required string DatasetId { get; init; }

    /// <summary>Name of the local EF Core table being compared.</summary>
    public required string LocalTableName { get; init; }

    /// <summary>Source column name for the funding year (used in GROUP BY).</summary>
    public required string YearColumn { get; init; }

    /// <summary>Source column name for the applicant/entity identifier. Null if not applicable.</summary>
    public string? ApplicantColumn { get; init; }

    /// <summary>Amount metrics to sum and compare between source and local.</summary>
    public IReadOnlyList<AmountMetricDefinition> AmountMetrics { get; init; } = [];

    /// <summary>Whether COUNT(DISTINCT applicant) queries are meaningful for this dataset.</summary>
    public bool SupportsDistinctApplicantCount { get; init; }

    /// <summary>Caveats about this dataset that should appear in reconciliation reports.</summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Maps a Socrata source column to a local EF Core property for amount comparison.
/// </summary>
public sealed record AmountMetricDefinition
{
    /// <summary>Socrata column name, snake_case (e.g. "total_eligible_amount").</summary>
    public required string SourceColumn { get; init; }

    /// <summary>
    /// Key used in <see cref="LocalYearTotals.Amounts"/> and result dictionaries
    /// (matches the C# property name, e.g. "TotalEligibleAmount").
    /// </summary>
    public required string LocalProperty { get; init; }

    /// <summary>Display name for reports (e.g. "Total Eligible Amount").</summary>
    public required string DisplayName { get; init; }
}

/// <summary>
/// Pre-configured manifests for the datasets currently imported into the workbench.
/// Add new entries here to extend reconciliation coverage without changing service logic.
/// </summary>
public static class DatasetManifests
{
    /// <summary>USAC E-Rate Funding Request Commitments — Socrata avi8-svp9.</summary>
    public static readonly SourceDatasetManifest FundingCommitments = new()
    {
        Name = "FundingCommitments",
        DatasetId = "avi8-svp9",
        LocalTableName = "FundingCommitments",
        YearColumn = "funding_year",
        ApplicantColumn = "applicant_entity_number",
        SupportsDistinctApplicantCount = true,
        AmountMetrics =
        [
            new() { SourceColumn = "total_eligible_amount", LocalProperty = "TotalEligibleAmount", DisplayName = "Total Eligible Amount" },
            new() { SourceColumn = "committed_amount",      LocalProperty = "CommittedAmount",      DisplayName = "Committed Amount"      },
        ],
        Notes = "USAC Open Data avi8-svp9. All commitment statuses included in source row count and sums.",
    };

    /// <summary>USAC E-Rate Invoices and Authorized Disbursements — Socrata jpiu-tj8h.</summary>
    public static readonly SourceDatasetManifest Disbursements = new()
    {
        Name = "Disbursements",
        DatasetId = "jpiu-tj8h",
        LocalTableName = "Disbursements",
        YearColumn = "funding_year",
        ApplicantColumn = "ben",
        SupportsDistinctApplicantCount = true,
        AmountMetrics =
        [
            new() { SourceColumn = "requested_inv_line_amt", LocalProperty = "RequestedAmount", DisplayName = "Requested Invoice Line Amount" },
            new() { SourceColumn = "approved_inv_line_amt",  LocalProperty = "ApprovedAmount",  DisplayName = "Approved Invoice Line Amount"  },
        ],
        Notes = "USAC Open Data jpiu-tj8h. All InvoiceLineStatus values included in source/raw comparison. " +
                "Summary totals reflect only rows where ApprovedAmount > 0 (inclusion rule).",
    };
}
