using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps to the Socrata CSV export of the USAC E-Rate Service Provider (SPIN) dataset.
/// Dataset: https://datahub.usac.org/d/s8d5-n6qr
/// Download: https://datahub.usac.org/api/views/s8d5-n6qr/rows.csv?accessType=DOWNLOAD
///
/// Column names match the Socrata display-name headers. Unknown columns are silently
/// ignored (MissingFieldFound = null in parser config).
/// </summary>
public class ServiceProviderCsvRow
{
    [Name("SPIN")]
    public string Spin { get; set; } = "";

    [Name("Service Provider Name")]
    public string ProviderName { get; set; } = "";

    [Name("Status")]
    public string? Status { get; set; }

    [Name("Phone Number")]
    public string? Phone { get; set; }

    [Name("Email")]
    public string? Email { get; set; }

    [Name("Website")]
    public string? Website { get; set; }

    [Name("Address")]
    public string? Address { get; set; }

    [Name("City")]
    public string? City { get; set; }

    [Name("State")]
    public string? State { get; set; }

    [Name("Zip")]
    public string? Zip { get; set; }
}
