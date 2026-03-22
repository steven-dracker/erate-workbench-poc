namespace ErateWorkbench.Domain;

/// <summary>
/// Raw-layer record for a consultant-assisted Form 471 application from the USAC
/// "E-Rate Request for Discount on Services: Consultants" dataset (x5px-esft).
///
/// Grain: one row per consultant per Form 471 application. An application with
/// multiple consultants produces multiple rows.
///
/// Natural key: (ApplicationNumber, ConsultantEpcOrganizationId).
/// RawSourceKey: "{ApplicationNumber}-{ConsultantEpcOrganizationId}"
///
/// Identity model: ConsultantEpcOrganizationId is the canonical consultant identity.
/// ConsultantName is preserved for display but must not be used for grouping due to
/// inconsistent casing (see docs/schema_consultants.md — Data Quality section).
/// </summary>
public class ConsultantApplication
{
    public int Id { get; set; }

    /// <summary>Composite source key: "{ApplicationNumber}-{ConsultantEpcOrganizationId}". Unique constraint.</summary>
    public required string RawSourceKey { get; set; }

    // --- Application identity ---
    public required string ApplicationNumber { get; set; }
    public int FundingYear { get; set; }
    public string? FormVersion { get; set; }
    public string? IsCertifiedInWindow { get; set; }

    // --- Applicant ---
    public string? ApplicantEpcOrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string? ApplicantType { get; set; }
    /// <summary>Applicant's state (2-char). Distinct from ConsultantState.</summary>
    public string? ApplicantState { get; set; }
    public string? ContactEmail { get; set; }

    // --- Consultant identity (canonical grouping key is ConsultantEpcOrganizationId) ---
    public required string ConsultantEpcOrganizationId { get; set; }
    /// <summary>Display name only — not normalized. Group by ConsultantEpcOrganizationId, not this field.</summary>
    public string? ConsultantName { get; set; }
    public string? ConsultantCity { get; set; }
    /// <summary>Consultant's HQ state. Distinct from ApplicantState.</summary>
    public string? ConsultantState { get; set; }
    public string? ConsultantZipCode { get; set; }
    public string? ConsultantPhone { get; set; }
    public string? ConsultantEmail { get; set; }

    public DateTime ImportedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
