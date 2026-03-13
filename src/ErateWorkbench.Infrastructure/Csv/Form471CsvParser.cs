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
                CategoryOfService = NullIfEmpty(row.CategoryOfService),
                ServiceType = NullIfEmpty(row.TypeOfService),
                RequestedAmount = row.RequestedAmount,
                ApplicationStatus = NullIfEmpty(row.ApplicationStatus),
                ImportedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
