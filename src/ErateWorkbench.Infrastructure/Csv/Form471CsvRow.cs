using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps to the Socrata CSV export of the USAC E-Rate FCC Form 471 dataset.
/// Dataset: https://datahub.usac.org/d/9s85-xeem
/// Download: https://datahub.usac.org/api/views/9s85-xeem/rows.csv?accessType=DOWNLOAD
///
/// Column names match the Socrata display-name headers. Unknown columns are silently
/// ignored (MissingFieldFound = null in parser config).
/// </summary>
public class Form471CsvRow
{
    [Name("Application Number")]
    public string ApplicationNumber { get; set; } = "";

    [Name("Funding Year")]
    public int FundingYear { get; set; }

    [Name("Applicant Name")]
    public string? ApplicantName { get; set; }

    [Name("Applicant Entity Number")]
    public string? ApplicantEntityNumber { get; set; }

    [Name("Applicant State")]
    public string? ApplicantState { get; set; }

    [Name("Category of Service")]
    public string? CategoryOfService { get; set; }

    [Name("Type of Service")]
    public string? TypeOfService { get; set; }

    [Name("Total Pre-Discount Eligible Amount")]
    public decimal? RequestedAmount { get; set; }

    [Name("Application Status")]
    public string? ApplicationStatus { get; set; }
}
