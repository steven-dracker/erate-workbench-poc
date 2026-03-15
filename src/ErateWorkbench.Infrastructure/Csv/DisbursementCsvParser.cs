using CsvHelper;
using CsvHelper.Configuration;
using ErateWorkbench.Domain;
using System.Globalization;

namespace ErateWorkbench.Infrastructure.Csv;

public class DisbursementCsvParser
{
    public IEnumerable<Disbursement> Parse(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        foreach (var row in csv.GetRecords<DisbursementCsvRow>())
        {
            if (string.IsNullOrWhiteSpace(row.FundingRequestNumber))
                continue;

            if (!int.TryParse(row.FundingYear, out var fundingYear))
                continue;

            var frn = row.FundingRequestNumber.Trim();
            var invoiceId = NullIfEmpty(row.InvoiceId);
            var lineNum = NullIfEmpty(row.InvoiceLineNumber);

            // Prefer invoice_id-based key; fall back to FRN-based key when absent.
            var rawKey = invoiceId != null
                ? $"{invoiceId}-{lineNum ?? "0"}"
                : $"{frn}-{lineNum ?? "0"}";

            var now = DateTime.UtcNow;

            yield return new Disbursement
            {
                RawSourceKey = rawKey,
                FundingRequestNumber = frn,
                InvoiceId = invoiceId,
                InvoiceLineNumber = lineNum,
                InvoiceType = NullIfEmpty(row.InvoiceType),
                InvoiceLineStatus = NullIfEmpty(row.InvoiceLineStatus),
                ApplicationNumber = NullIfEmpty(row.ApplicationNumber),
                ApplicantEntityNumber = NullIfEmpty(row.ApplicantEntityNumber),
                ApplicantEntityName = NullIfEmpty(row.ApplicantEntityName),
                ServiceProviderSpin = NullIfEmpty(row.ServiceProviderSpin),
                ServiceProviderName = NullIfEmpty(row.ServiceProviderName),
                FundingYear = fundingYear,
                CategoryOfService = NullIfEmpty(row.CategoryOfService),
                RequestedAmount = row.RequestedAmount,
                ApprovedAmount = row.ApprovedAmount,
                InvoiceReceivedDate = ParseDate(row.InvoiceReceivedDate),
                LineCompletionDate = ParseDate(row.LineCompletionDate),
                ImportedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }
    }

    private static DateTime? ParseDate(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null
        : DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt)
            ? dt
            : null;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
