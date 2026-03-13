using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps to the display-header CSV export of the USAC E-Rate Recipient Details and Commitments dataset.
/// Dataset: https://datahub.usac.org/d/avi8-svp9
/// Download: https://datahub.usac.org/api/views/avi8-svp9/rows.csv?accessType=DOWNLOAD
///
/// Column names match the display-label headers returned by the Socrata CSV export for avi8-svp9.
/// If a column is absent from a particular export, CsvHelper silently leaves the property null
/// (MissingFieldFound = null in parser config).
/// </summary>
public class FundingCommitmentCsvRow
{
    [Name("Funding Request Number")]
    public string FundingRequestNumber { get; set; } = "";

    [Name("FRN Line Item Number")]
    public int? FrnLineItemNumber { get; set; }

    [Name("Applicant Name")]
    public string? ApplicantName { get; set; }

    [Name("Recipient Billed Entity Number")]
    public string? ApplicantEntityNumber { get; set; }

    [Name("Application Number")]
    public string? ApplicationNumber { get; set; }

    [Name("Funding Year")]
    public int FundingYear { get; set; }

    [Name("Service Provider Name")]
    public string? ServiceProviderName { get; set; }

    [Name("Service Provider Number")]
    public string? Spin { get; set; }

    [Name("Category of Service")]
    public string? CategoryOfService { get; set; }

    [Name("Service Type")]
    public string? TypeOfService { get; set; }

    [Name("FRN Status")]
    public string? CommitmentStatus { get; set; }

    [Name("Committed Funded")]
    public decimal? CommittedAmount { get; set; }

    [Name("Total Eligible Pre-Discount Amount")]
    public decimal? TotalEligibleAmount { get; set; }
}
