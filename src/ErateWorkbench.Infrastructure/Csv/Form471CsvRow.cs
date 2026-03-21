using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps to the Socrata CSV export of the USAC E-Rate FCC Form 471 dataset.
/// Dataset: https://datahub.usac.org/d/9s6i-myen
/// Download: https://datahub.usac.org/api/views/9s6i-myen/rows.csv?accessType=DOWNLOAD
///
/// Column names use snake_case as exported by the current Socrata dataset.
/// Unknown columns are silently ignored (MissingFieldFound = null in parser config).
/// CategoryOfService compact values ("Category1", "Category2") are normalized by the parser.
/// </summary>
public class Form471CsvRow
{
    [Name("application_number")]
    public string ApplicationNumber { get; set; } = "";

    [Name("funding_year")]
    public int FundingYear { get; set; }

    [Name("applicant_name")]
    public string? ApplicantName { get; set; }

    [Name("ben")]
    public string? ApplicantEntityNumber { get; set; }

    [Name("applicant_state")]
    public string? ApplicantState { get; set; }

    [Name("category_of_service")]
    public string? CategoryOfService { get; set; }

    [Name("total_pre_discount_eligible_amount")]
    public decimal? RequestedAmount { get; set; }

    [Name("application_status")]
    public string? ApplicationStatus { get; set; }

    [Name("certified_datetime")]
    public DateTime? CertifiedDatetime { get; set; }
}
