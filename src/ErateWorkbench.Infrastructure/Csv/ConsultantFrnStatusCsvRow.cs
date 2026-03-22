using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps to the Socrata CSV export of the USAC Consultant FRN Status dataset.
/// Dataset: https://datahub.usac.org/resource/mihb-jfex
/// Download: https://datahub.usac.org/api/views/mihb-jfex/rows.csv?accessType=DOWNLOAD
///
/// Column names are snake_case as confirmed by live API fetch (CC-ERATE-000038A).
/// All 59 confirmed fields are present; only analytically relevant fields are mapped.
/// Unknown/unmapped columns are silently ignored (MissingFieldFound = null in parser config).
/// Numeric fields are string in Socrata JSON — parsed safely by the parser with fallback to null.
/// </summary>
public class ConsultantFrnStatusCsvRow
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

    [Name("ben")]
    public string? Ben { get; set; }

    [Name("organization_name")]
    public string? OrganizationName { get; set; }

    [Name("organization_entity_type_name")]
    public string? OrganizationEntityTypeName { get; set; }

    [Name("cnct_email")]
    public string? ContactEmail { get; set; }

    [Name("cnslt_name")]
    public string? ConsultantName { get; set; }

    [Name("cnslt_epc_organization_id")]
    public string ConsultantEpcOrganizationId { get; set; } = "";

    [Name("funding_request_number")]
    public string FundingRequestNumber { get; set; } = "";

    [Name("form_471_frn_status_name")]
    public string? FrnStatusName { get; set; }

    [Name("nickname")]
    public string? Nickname { get; set; }

    [Name("form_471_service_type_name")]
    public string? ServiceTypeName { get; set; }

    [Name("contract_type_name")]
    public string? ContractTypeName { get; set; }

    [Name("spin_name")]
    public string? SpinName { get; set; }

    [Name("pending_reason")]
    public string? PendingReason { get; set; }

    [Name("invoicing_mode")]
    public string? InvoicingMode { get; set; }

    // Financials — stored as string in Socrata, parsed to decimal by parser
    [Name("dis_pct")]
    public string? DiscountPct { get; set; }

    [Name("total_pre_discount_costs")]
    public string? TotalPreDiscountCosts { get; set; }

    [Name("funding_commitment_request")]
    public string? FundingCommitmentRequest { get; set; }

    [Name("total_authorized_disbursement")]
    public string? TotalAuthorizedDisbursement { get; set; }

    // Dates — ISO datetime strings
    [Name("service_start_date")]
    public string? ServiceStartDate { get; set; }

    [Name("fcdl_letter_date")]
    public string? FcdlLetterDate { get; set; }
}
