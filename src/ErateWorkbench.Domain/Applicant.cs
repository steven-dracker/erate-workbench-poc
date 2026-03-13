namespace ErateWorkbench.Domain;

public class Applicant
{
    public int Id { get; set; }

    /// <summary>Billing Entity Number — unique identifier assigned by USAC.</summary>
    public required string Ben { get; set; }

    public required string Name { get; set; }

    public ApplicantEntityType EntityType { get; set; }

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }

    public int FundingYear { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum ApplicantEntityType
{
    Unknown = 0,
    School = 1,
    Library = 2,
    SchoolDistrict = 3,
    LibrarySystem = 4,
    Consortium = 5,
}
