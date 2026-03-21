using CsvHelper;
using CsvHelper.Configuration;
using ErateWorkbench.Domain;
using System.Globalization;

namespace ErateWorkbench.Infrastructure.Csv;

public class Form471CsvParser
{
    public IEnumerable<Form471Application> Parse(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        foreach (var row in csv.GetRecords<Form471CsvRow>())
        {
            if (string.IsNullOrWhiteSpace(row.ApplicationNumber) || row.FundingYear == 0)
                continue;

            var appNumber = row.ApplicationNumber.Trim();
            var now = DateTime.UtcNow;

            yield return new Form471Application
            {
                ApplicationNumber = appNumber,
                FundingYear = row.FundingYear,
                RawSourceKey = $"{appNumber}-{row.FundingYear}",
                ApplicantEntityNumber = NullIfEmpty(row.ApplicantEntityNumber),
                ApplicantName = NullIfEmpty(row.ApplicantName),
                ApplicantState = row.ApplicantState?.Trim().ToUpperInvariant() is { Length: > 0 } s ? s : null,
                CategoryOfService = NormalizeCategory(row.CategoryOfService),
                ServiceType = null,   // not in 9s6i-myen; FRN line items are a separate dataset
                RequestedAmount = row.RequestedAmount,
                ApplicationStatus = NullIfEmpty(row.ApplicationStatus),
                CertificationDate = row.CertifiedDatetime,
                ImportedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Normalizes compact category values from 9s6i-myen ("Category1", "Category2")
    /// to the canonical display form ("Category 1", "Category 2").
    /// Returns null for blank values; passes through any other string unchanged.
    /// </summary>
    private static string? NormalizeCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim() switch
        {
            "Category1" => "Category 1",
            "Category2" => "Category 2",
            var v => v,
        };
    }
}
