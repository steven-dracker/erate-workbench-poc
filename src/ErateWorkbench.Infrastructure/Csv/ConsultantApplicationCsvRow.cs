using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps to the Socrata CSV export of the USAC E-Rate Form 471 Consultants dataset.
/// Dataset: https://datahub.usac.org/resource/x5px-esft
/// Download: https://datahub.usac.org/api/views/x5px-esft/rows.csv?accessType=DOWNLOAD
///
/// Column names are snake_case as confirmed by live API fetch (CC-ERATE-000038A).
/// Unknown columns are silently ignored (MissingFieldFound = null in parser config).
/// cnslt_phone_ext may be absent in JSON/CSV responses when null (Socrata omits null fields).
/// </summary>
public class ConsultantApplicationCsvRow
{
    [Name("application_number")]
    public string ApplicationNumber { get; set; } = "";

    [Name("funding_year")]
    public string FundingYear { get; set; } = "";

    [Name("state")]
    public string? ApplicantState { get; set; }

    [Name("form_version")]
    public string? FormVersion { get; set; }

    [Name("is_certified_in_window")]
    public string? IsCertifiedInWindow { get; set; }

    [Name("epc_organization_id")]
    public string? ApplicantEpcOrganizationId { get; set; }

    [Name("organization_name")]
    public string? OrganizationName { get; set; }

    [Name("applicant_type")]
    public string? ApplicantType { get; set; }

    [Name("cnct_email")]
    public string? ContactEmail { get; set; }

    [Name("cnslt_name")]
    public string? ConsultantName { get; set; }

    [Name("cnslt_epc_organization_id")]
    public string ConsultantEpcOrganizationId { get; set; } = "";

    [Name("cnslt_city")]
    public string? ConsultantCity { get; set; }

    [Name("cnslt_state")]
    public string? ConsultantState { get; set; }

    [Name("cnslt_zipcode")]
    public string? ConsultantZipCode { get; set; }

    [Name("cnslt_phone")]
    public string? ConsultantPhone { get; set; }

    [Name("cnslt_email")]
    public string? ConsultantEmail { get; set; }
}
