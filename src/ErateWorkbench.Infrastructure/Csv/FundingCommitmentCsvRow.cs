using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps to the Socrata CSV export of the USAC E-Rate Funding Request Commitments dataset.
/// Dataset: https://datahub.usac.org/d/i5j4-3rvr
/// Download: https://datahub.usac.org/api/views/i5j4-3rvr/rows.csv?accessType=DOWNLOAD
///
/// Column names match the Socrata display-name headers in the CSV export.
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

    [Name("Applicant Entity Number")]
    public string? ApplicantEntityNumber { get; set; }

    [Name("Application Number")]
    public string? ApplicationNumber { get; set; }

    [Name("Funding Year")]
    public int FundingYear { get; set; }

    [Name("Service Provider Name")]
    public string? ServiceProviderName { get; set; }

    [Name("SPIN")]
    public string? Spin { get; set; }

    [Name("Category of Service")]
    public string? CategoryOfService { get; set; }

    [Name("Type of Service")]
    public string? TypeOfService { get; set; }

    [Name("Commitment Status")]
    public string? CommitmentStatus { get; set; }

    [Name("Committed Amount")]
    public decimal? CommittedAmount { get; set; }

    [Name("Total Eligible Pre-Discount Amount")]
    public decimal? TotalEligibleAmount { get; set; }
}
