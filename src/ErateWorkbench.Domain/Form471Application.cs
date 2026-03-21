namespace ErateWorkbench.Domain;

/// <summary>
/// Represents a single FCC Form 471 funding request line item from the USAC E-Rate
/// open dataset. Form 471 captures demand — what applicants are requesting before
/// USAC review. It differs from <see cref="FundingCommitment"/> (what was approved).
///
/// Natural key: (ApplicationNumber, FundingYear). Encoded together in
/// <see cref="RawSourceKey"/> as "{ApplicationNumber}-{FundingYear}" for traceability.
/// </summary>
public class Form471Application
{
    public int Id { get; set; }

    public required string ApplicationNumber { get; set; }
    public int FundingYear { get; set; }

    /// <summary>Composite source key: "{ApplicationNumber}-{FundingYear}".</summary>
    public required string RawSourceKey { get; set; }

    // Applicant linkage — joins to EpcEntity.EntityNumber
    public string? ApplicantEntityNumber { get; set; }
    public string? ApplicantName { get; set; }
    public string? ApplicantState { get; set; }

    // Service details
    public string? CategoryOfService { get; set; }    // "Category 1" or "Category 2"
    public string? ServiceType { get; set; }

    // Financials
    public decimal? RequestedAmount { get; set; }

    public string? ApplicationStatus { get; set; }

    /// <summary>Date the application was certified by the applicant. Source: certified_datetime column.</summary>
    public DateTime? CertificationDate { get; set; }

    public DateTime ImportedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
