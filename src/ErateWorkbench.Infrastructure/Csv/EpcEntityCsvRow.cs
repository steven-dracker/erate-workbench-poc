using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps to the Socrata CSV export of the USAC E-Rate Supplemental Entity Information dataset.
/// Dataset: https://datahub.usac.org/d/7i5i-83qf
/// Download: https://datahub.usac.org/api/views/7i5i-83qf/rows.csv?accessType=DOWNLOAD
/// Column names match the Socrata display-name headers in the CSV export.
/// </summary>
public class EpcEntityCsvRow
{
    [Name("Entity Number")]
    public string EntityNumber { get; set; } = "";

    [Name("Entity Name")]
    public string EntityName { get; set; } = "";

    [Name("Entity Type")]
    public string EntityType { get; set; } = "";

    [Name("Status")]
    public string? Status { get; set; }

    [Name("Parent Entity Number")]
    public string? ParentEntityNumber { get; set; }

    [Name("Parent Entity Name")]
    public string? ParentEntityName { get; set; }

    [Name("Physical Address 1")]
    public string? PhysicalAddress { get; set; }

    [Name("Physical City")]
    public string? PhysicalCity { get; set; }

    [Name("Physical County")]
    public string? PhysicalCounty { get; set; }

    [Name("Physical State")]
    public string? PhysicalState { get; set; }

    [Name("Physical Zip Code")]
    public string? PhysicalZipcode { get; set; }

    [Name("Phone Number")]
    public string? PhoneNumber { get; set; }

    [Name("Email")]
    public string? Email { get; set; }

    [Name("Website URL")]
    public string? WebsiteUrl { get; set; }

    [Name("Latitude")]
    public double? Latitude { get; set; }

    [Name("Longitude")]
    public double? Longitude { get; set; }

    [Name("Urban/Rural Status")]
    public string? UrbanRuralStatus { get; set; }

    [Name("Category One Discount Rate")]
    public decimal? CategoryOneDiscountRate { get; set; }

    [Name("Category Two Discount Rate")]
    public decimal? CategoryTwoDiscountRate { get; set; }

    [Name("Locale Code")]
    public string? LocaleCode { get; set; }

    [Name("C2 School Student Count")]
    public int? C2SchoolStudentCount { get; set; }

    [Name("FCC Registration Number")]
    public string? FccRegistrationNumber { get; set; }
}
