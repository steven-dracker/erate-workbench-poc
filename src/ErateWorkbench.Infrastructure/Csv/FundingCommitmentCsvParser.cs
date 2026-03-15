using CsvHelper;
using CsvHelper.Configuration;
using ErateWorkbench.Domain;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace ErateWorkbench.Infrastructure.Csv;

public class FundingCommitmentCsvParser(ILogger<FundingCommitmentCsvParser>? logger = null)
{
    public IEnumerable<FundingCommitment> Parse(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        // DIAGNOSTIC: log actual CSV headers so we can verify column name mapping
        csv.Read();
        csv.ReadHeader();
        var headers = csv.Context.Reader.HeaderRecord ?? Array.Empty<string>();
        logger?.LogWarning("[DIAG] CSV headers ({Count}): {Headers}",
            headers.Length, string.Join(" | ", headers));

        int rawRowCount = 0;
        int skippedCount = 0;

        foreach (var row in csv.GetRecords<FundingCommitmentCsvRow>())
        {
            rawRowCount++;

            // DIAGNOSTIC: log first 5 rows to confirm binding
            if (rawRowCount <= 5)
                logger?.LogWarning("[DIAG] Row {N}: FRN={FRN}, EntityNum={Entity}, CommittedAmt={Amt}",
                    rawRowCount, row.FundingRequestNumber, row.ApplicantEntityNumber, row.CommittedAmount);

            if (string.IsNullOrWhiteSpace(row.FundingRequestNumber))
            {
                skippedCount++;
                continue;
            }

            var frn = row.FundingRequestNumber.Trim();
            var lineItemRaw = string.IsNullOrWhiteSpace(row.FrnLineItemNumber) ? null : row.FrnLineItemNumber.Trim();
            var rawKey = lineItemRaw != null ? $"{frn}-{lineItemRaw}" : frn;

            var entityNumber = NullIfEmpty(row.ApplicantEntityNumber);
            // Fallback chain: ros_entity_name → organization_name → "Entity {BEN}"
            var applicantName = NullIfEmpty(row.RosEntityName)
                ?? NullIfEmpty(row.OrganizationName)
                ?? (entityNumber != null ? $"Entity {entityNumber}" : null);

            var now = DateTime.UtcNow;

            yield return new FundingCommitment
            {
                FundingRequestNumber = frn,
                FrnLineItemNumber = int.TryParse(lineItemRaw, out var li) ? li : null,
                RawSourceKey = rawKey,
                ApplicantEntityNumber = entityNumber,
                ApplicantName = applicantName,
                ApplicationNumber = NullIfEmpty(row.ApplicationNumber),
                FundingYear = row.FundingYear,
                ServiceProviderName = NullIfEmpty(row.ServiceProviderName),
                ServiceProviderSpin = NullIfEmpty(row.Spin),
                CategoryOfService = NullIfEmpty(row.CategoryOfService),
                TypeOfService = NullIfEmpty(row.TypeOfService),
                CommitmentStatus = NullIfEmpty(row.CommitmentStatus),
                CommittedAmount = row.CommittedAmount,
                TotalEligibleAmount = row.TotalEligibleAmount,
                ImportedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }

        logger?.LogWarning("[DIAG] Parse complete: {Raw} raw rows, {Skipped} skipped (blank FRN), {Yielded} yielded",
            rawRowCount, skippedCount, rawRowCount - skippedCount);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
