using CsvHelper;
using CsvHelper.Configuration;
using ErateWorkbench.Domain;
using System.Globalization;

namespace ErateWorkbench.Infrastructure.Csv;

public class FundingCommitmentCsvParser
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

        foreach (var row in csv.GetRecords<FundingCommitmentCsvRow>())
        {
            if (string.IsNullOrWhiteSpace(row.FundingRequestNumber))
                continue;

            var frn = row.FundingRequestNumber.Trim();
            var rawKey = row.FrnLineItemNumber.HasValue
                ? $"{frn}-{row.FrnLineItemNumber.Value}"
                : frn;

            var now = DateTime.UtcNow;

            yield return new FundingCommitment
            {
                FundingRequestNumber = frn,
                FrnLineItemNumber = row.FrnLineItemNumber,
                RawSourceKey = rawKey,
                ApplicantEntityNumber = NullIfEmpty(row.ApplicantEntityNumber),
                ApplicantName = NullIfEmpty(row.ApplicantName),
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
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
