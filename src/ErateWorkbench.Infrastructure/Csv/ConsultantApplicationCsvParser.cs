using CsvHelper;
using CsvHelper.Configuration;
using ErateWorkbench.Domain;
using System.Globalization;

namespace ErateWorkbench.Infrastructure.Csv;

public class ConsultantApplicationCsvParser
{
    /// <summary>
    /// Parses a CSV stream from the x5px-esft dataset into <see cref="ConsultantApplication"/> records.
    ///
    /// Rows are skipped when ApplicationNumber or ConsultantEpcOrganizationId is blank —
    /// both are required to form a valid RawSourceKey and preserve the identity model.
    /// </summary>
    public IEnumerable<ConsultantApplication> Parse(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        foreach (var row in csv.GetRecords<ConsultantApplicationCsvRow>())
        {
            if (string.IsNullOrWhiteSpace(row.ApplicationNumber) ||
                string.IsNullOrWhiteSpace(row.ConsultantEpcOrganizationId))
                continue;

            var appNumber = row.ApplicationNumber.Trim();
            var cnsltEpcId = row.ConsultantEpcOrganizationId.Trim();

            if (!int.TryParse(row.FundingYear?.Trim(), out var fundingYear) || fundingYear == 0)
                continue;

            var now = DateTime.UtcNow;

            yield return new ConsultantApplication
            {
                RawSourceKey = $"{appNumber}-{cnsltEpcId}",
                ApplicationNumber = appNumber,
                FundingYear = fundingYear,
                FormVersion = NullIfEmpty(row.FormVersion),
                IsCertifiedInWindow = NullIfEmpty(row.IsCertifiedInWindow),
                ApplicantEpcOrganizationId = NullIfEmpty(row.ApplicantEpcOrganizationId),
                OrganizationName = NullIfEmpty(row.OrganizationName),
                ApplicantType = NullIfEmpty(row.ApplicantType),
                ApplicantState = NormalizeState(row.ApplicantState),
                ContactEmail = NullIfEmpty(row.ContactEmail),
                ConsultantEpcOrganizationId = cnsltEpcId,
                ConsultantName = NullIfEmpty(row.ConsultantName),
                ConsultantCity = NullIfEmpty(row.ConsultantCity),
                ConsultantState = NormalizeState(row.ConsultantState),
                ConsultantZipCode = NullIfEmpty(row.ConsultantZipCode),
                ConsultantPhone = NullIfEmpty(row.ConsultantPhone),
                ConsultantEmail = NullIfEmpty(row.ConsultantEmail),
                ImportedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeState(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
