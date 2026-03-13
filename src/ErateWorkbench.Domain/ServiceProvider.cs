namespace ErateWorkbench.Domain;

/// <summary>
/// Represents a USAC-registered E-Rate service provider identified by SPIN
/// (Service Provider Identification Number). SPIN is the stable natural key
/// assigned by USAC and is used to link providers to funding commitments.
/// </summary>
public class ServiceProvider
{
    public int Id { get; set; }

    /// <summary>SPIN — Service Provider Identification Number. Natural key assigned by USAC.</summary>
    public required string Spin { get; set; }

    public required string ProviderName { get; set; }

    public string? Status { get; set; }

    // Contact
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    // Physical location
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }

    /// <summary>
    /// Traceability key — equals <see cref="Spin"/> for this dataset.
    /// Included for consistency with the FundingCommitment pattern.
    /// </summary>
    public required string RawSourceKey { get; set; }

    public DateTime ImportedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
