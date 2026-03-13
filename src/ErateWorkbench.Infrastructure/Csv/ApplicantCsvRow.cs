using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps to the USAC Open Data "Applicants" CSV export.
/// Column names match the headers in the downloaded file.
/// </summary>
public class ApplicantCsvRow
{
    [Name("BEN")]
    public string Ben { get; set; } = "";

    [Name("Organization Name")]
    public string Name { get; set; } = "";

    [Name("Entity Type")]
    public string EntityType { get; set; } = "";

    [Name("Physical Address")]
    public string? Address { get; set; }

    [Name("Physical City")]
    public string? City { get; set; }

    [Name("Physical State")]
    public string? State { get; set; }

    [Name("Physical Zip Code")]
    public string? Zip { get; set; }

    [Name("Funding Year")]
    public int FundingYear { get; set; }
}
