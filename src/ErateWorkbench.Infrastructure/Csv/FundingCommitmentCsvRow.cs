using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps to the display-header CSV export of the USAC E-Rate Recipient Details and Commitments dataset.
/// Dataset: https://datahub.usac.org/d/avi8-svp9
/// Download: https://datahub.usac.org/api/views/avi8-svp9/rows.csv?accessType=DOWNLOAD
///
/// Column names match the snake_case headers returned by the Socrata resource API for avi8-svp9:
///   https://datahub.usac.org/resource/avi8-svp9.csv?$limit=N&amp;$offset=N
/// If a column is absent from a particular export, CsvHelper silently leaves the property null
/// (MissingFieldFound = null in parser config).
/// </summary>
public class FundingCommitmentCsvRow
{
    [Name("funding_request_number")]
    public string FundingRequestNumber { get; set; } = "";

    [Name("form_471_line_item_number")]
    public string? FrnLineItemNumber { get; set; }

    [Name("organization_name")]
    public string? ApplicantName { get; set; }

    [Name("billed_entity_number")]
    public string? ApplicantEntityNumber { get; set; }

    [Name("application_number")]
    public string? ApplicationNumber { get; set; }

    [Name("funding_year")]
    public int FundingYear { get; set; }

    [Name("spin_name")]
    public string? ServiceProviderName { get; set; }

    [Name("spin_number")]
    public string? Spin { get; set; }

    [Name("chosen_category_of_service")]
    public string? CategoryOfService { get; set; }

    [Name("form_471_service_type_name")]
    public string? TypeOfService { get; set; }

    [Name("form_471_frn_status_name")]
    public string? CommitmentStatus { get; set; }

    [Name("post_discount_extended_eligible_line_item_costs")]
    public decimal? CommittedAmount { get; set; }

    [Name("pre_discount_extended_eligible_line_item_costs")]
    public decimal? TotalEligibleAmount { get; set; }
}
