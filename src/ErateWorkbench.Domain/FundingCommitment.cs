namespace ErateWorkbench.Domain;

/// <summary>
/// Represents a single funded line item from the USAC E-Rate Funding Request Commitments dataset
/// (datahub.usac.org). The natural key from USAC is (FundingRequestNumber, FrnLineItemNumber),
/// captured together in <see cref="RawSourceKey"/> for deduplication and traceability.
/// </summary>
public class FundingCommitment
{
    public int Id { get; set; }

    /// <summary>USAC Funding Request Number (FRN).</summary>
    public required string FundingRequestNumber { get; set; }

    /// <summary>FRN Line Item Number within the request. Null for datasets that don't split by line.</summary>
    public int? FrnLineItemNumber { get; set; }

    /// <summary>
    /// Composite source key: "{FRN}-{LineItem}" (or just FRN when no line item).
    /// Unique constraint — used for idempotent upsert.
    /// </summary>
    public required string RawSourceKey { get; set; }

    // Applicant / entity linkage
    public string? ApplicantEntityNumber { get; set; }   // BEN — links to EpcEntity.EntityNumber
    public string? ApplicantName { get; set; }
    public string? ApplicationNumber { get; set; }

    public int FundingYear { get; set; }

    // Service provider
    public string? ServiceProviderName { get; set; }
    public string? ServiceProviderSpin { get; set; }

    // Service details
    public string? CategoryOfService { get; set; }
    public string? TypeOfService { get; set; }

    // Commitment outcome
    public string? CommitmentStatus { get; set; }
    public decimal? CommittedAmount { get; set; }
    public decimal? TotalEligibleAmount { get; set; }

    public DateTime ImportedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
