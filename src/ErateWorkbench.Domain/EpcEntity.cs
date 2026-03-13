namespace ErateWorkbench.Domain;

/// <summary>
/// Represents a school, library, district, or consortium from the
/// USAC E-Rate Supplemental Entity Information dataset (datahub.usac.org/d/7i5i-83qf).
/// The EntityNumber is the BEN (Billing Entity Number) assigned by USAC EPC.
/// </summary>
public class EpcEntity
{
    public int Id { get; set; }

    /// <summary>BEN — Billing Entity Number. Natural key in USAC EPC.</summary>
    public required string EntityNumber { get; set; }

    public required string EntityName { get; set; }

    public EpcEntityType EntityType { get; set; }

    public string? Status { get; set; }

    // Parent (district or library system)
    public string? ParentEntityNumber { get; set; }
    public string? ParentEntityName { get; set; }

    // Physical address
    public string? PhysicalAddress { get; set; }
    public string? PhysicalCity { get; set; }
    public string? PhysicalCounty { get; set; }
    public string? PhysicalState { get; set; }
    public string? PhysicalZip { get; set; }

    // Contact
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    // Geography
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? UrbanRuralStatus { get; set; }

    // E-Rate discount rates (0–90)
    public decimal? CategoryOneDiscountRate { get; set; }
    public decimal? CategoryTwoDiscountRate { get; set; }

    // School-specific
    public string? LocaleCode { get; set; }
    public int? StudentCount { get; set; }

    public string? FccRegistrationNumber { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum EpcEntityType
{
    Unknown = 0,
    School = 1,
    Library = 2,
    SchoolDistrict = 3,
    LibrarySystem = 4,
    Consortium = 5,
    NonInstructionalFacility = 6,
}
