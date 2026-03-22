using CsvHelper;
using CsvHelper.Configuration;
using ErateWorkbench.Domain;
using System.Globalization;

namespace ErateWorkbench.Infrastructure.Csv;

public class ConsultantFrnStatusCsvParser
{
    /// <summary>
    /// Parses a CSV stream from the mihb-jfex dataset into <see cref="ConsultantFrnStatus"/> records.
    ///
    /// Rows are skipped when ApplicationNumber, FundingRequestNumber, or ConsultantEpcOrganizationId
    /// is blank — all three are required to form a valid RawSourceKey and preserve the identity model.
    ///
    /// Numeric fields (amounts, discount) are parsed safely — invalid values produce null.
    /// Date fields use ISO 8601 format as returned by Socrata ("2016-02-08T00:00:00.000").
    /// </summary>
    public IEnumerable<ConsultantFrnStatus> Parse(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        foreach (var row in csv.GetRecords<ConsultantFrnStatusCsvRow>())
        {
            if (string.IsNullOrWhiteSpace(row.ApplicationNumber) ||
                string.IsNullOrWhiteSpace(row.FundingRequestNumber) ||
                string.IsNullOrWhiteSpace(row.ConsultantEpcOrganizationId))
                continue;

            var appNumber = row.ApplicationNumber.Trim();
            var frn = row.FundingRequestNumber.Trim();
            var cnsltEpcId = row.ConsultantEpcOrganizationId.Trim();

            if (!int.TryParse(row.FundingYear?.Trim(), out var fundingYear) || fundingYear == 0)
                continue;

            var now = DateTime.UtcNow;

            yield return new ConsultantFrnStatus
            {
                RawSourceKey = $"{appNumber}-{frn}",
                FundingRequestNumber = frn,
                ApplicationNumber = appNumber,
                FundingYear = fundingYear,
                FormVersion = NullIfEmpty(row.FormVersion),
                IsCertifiedInWindow = NullIfEmpty(row.IsCertifiedInWindow),
                Nickname = NullIfEmpty(row.Nickname),
                ApplicantState = NormalizeState(row.ApplicantState),
                Ben = NullIfEmpty(row.Ben),
                OrganizationName = NullIfEmpty(row.OrganizationName),
                OrganizationEntityTypeName = NullIfEmpty(row.OrganizationEntityTypeName),
                ContactEmail = NullIfEmpty(row.ContactEmail),
                ConsultantEpcOrganizationId = cnsltEpcId,
                ConsultantName = NullIfEmpty(row.ConsultantName),
                ServiceTypeName = NullIfEmpty(row.ServiceTypeName),
                ContractTypeName = NullIfEmpty(row.ContractTypeName),
                SpinName = NullIfEmpty(row.SpinName),
                FrnStatusName = NullIfEmpty(row.FrnStatusName),
                PendingReason = NullIfEmpty(row.PendingReason),
                InvoicingMode = NullIfEmpty(row.InvoicingMode),
                DiscountPct = ParseDecimal(row.DiscountPct),
                TotalPreDiscountCosts = ParseDecimal(row.TotalPreDiscountCosts),
                FundingCommitmentRequest = ParseDecimal(row.FundingCommitmentRequest),
                TotalAuthorizedDisbursement = ParseDecimal(row.TotalAuthorizedDisbursement),
                ServiceStartDate = ParseDate(row.ServiceStartDate),
                FcdlLetterDate = ParseDate(row.FcdlLetterDate),
                ImportedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeState(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Socrata returns ISO 8601: "2016-02-08T00:00:00.000"
        return DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result)
            ? result
            : null;
    }
}
