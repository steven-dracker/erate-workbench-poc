using CsvHelper;
using CsvHelper.Configuration;
using ErateWorkbench.Domain;
using System.Globalization;

namespace ErateWorkbench.Infrastructure.Csv;

public class ServiceProviderCsvParser
{
    public IEnumerable<ServiceProvider> Parse(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        foreach (var row in csv.GetRecords<ServiceProviderCsvRow>())
        {
            if (string.IsNullOrWhiteSpace(row.Spin) || string.IsNullOrWhiteSpace(row.ProviderName))
                continue;

            var spin = row.Spin.Trim();
            var now = DateTime.UtcNow;

            yield return new ServiceProvider
            {
                Spin = spin,
                ProviderName = row.ProviderName.Trim(),
                Status = NullIfEmpty(row.Status),
                Phone = NullIfEmpty(row.Phone),
                Email = NullIfEmpty(row.Email),
                Website = NullIfEmpty(row.Website),
                Address = NullIfEmpty(row.Address),
                City = NullIfEmpty(row.City),
                State = row.State?.Trim().ToUpperInvariant() is { Length: > 0 } s ? s : null,
                Zip = NullIfEmpty(row.Zip),
                RawSourceKey = spin,
                ImportedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
