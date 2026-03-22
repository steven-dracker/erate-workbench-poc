namespace ErateWorkbench.Domain;

/// <summary>
/// Raw-layer record for a consultant-assisted FRN (Funding Request Number) from the USAC
/// "Consultant Update to E-rate Request for Discount on Services: FRN Status" dataset (mihb-jfex).
///
/// Grain: one row per FRN per consultant-assisted application. More granular than
/// ConsultantApplication (FRN-level vs application-level). Applications with multiple
/// FRNs produce multiple rows.
///
/// Natural key: FundingRequestNumber (globally unique in E-Rate).
/// RawSourceKey: "{ApplicationNumber}-{FundingRequestNumber}" for human traceability.
///
/// Funding amounts (FundingCommitmentRequest, TotalAuthorizedDisbursement) are FRN-level.
/// Aggregation to application or consultant total requires SUMming across all FRNs.
///
/// ServiceTypeName is populated in this dataset (unlike dataset 9s6i-myen where it is null).
/// </summary>
public class ConsultantFrnStatus
{
    public int Id { get; set; }

    /// <summary>Composite source key: "{ApplicationNumber}-{FundingRequestNumber}". Unique constraint.</summary>
    public required string RawSourceKey { get; set; }

    // --- FRN identity ---
    public required string FundingRequestNumber { get; set; }
    public required string ApplicationNumber { get; set; }
    public int FundingYear { get; set; }
    public string? FormVersion { get; set; }
    public string? IsCertifiedInWindow { get; set; }
    public string? Nickname { get; set; }

    // --- Applicant ---
    /// <summary>Applicant state — 2-char code.</summary>
    public string? ApplicantState { get; set; }
    public string? Ben { get; set; }
    public string? OrganizationName { get; set; }
    public string? OrganizationEntityTypeName { get; set; }
    public string? ContactEmail { get; set; }

    // --- Consultant identity ---
    public required string ConsultantEpcOrganizationId { get; set; }
    /// <summary>Display name only — not normalized. Group by ConsultantEpcOrganizationId.</summary>
    public string? ConsultantName { get; set; }

    // --- Service details ---
    public string? ServiceTypeName { get; set; }
    public string? ContractTypeName { get; set; }
    public string? SpinName { get; set; }

    // --- Commitment status ---
    public string? FrnStatusName { get; set; }
    public string? PendingReason { get; set; }
    public string? InvoicingMode { get; set; }

    // --- Financials (FRN-level) ---
    public decimal? DiscountPct { get; set; }
    public decimal? TotalPreDiscountCosts { get; set; }
    /// <summary>E-Rate requested amount for this FRN (pre-discount × dis_pct).</summary>
    public decimal? FundingCommitmentRequest { get; set; }
    /// <summary>Actual disbursed amount — may differ from FundingCommitmentRequest after appeals.</summary>
    public decimal? TotalAuthorizedDisbursement { get; set; }

    // --- Dates ---
    public DateTime? ServiceStartDate { get; set; }
    public DateTime? FcdlLetterDate { get; set; }

    public DateTime ImportedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
