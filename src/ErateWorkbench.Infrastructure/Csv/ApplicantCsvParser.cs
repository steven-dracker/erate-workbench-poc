using CsvHelper;
using CsvHelper.Configuration;
using ErateWorkbench.Domain;
using System.Globalization;

namespace ErateWorkbench.Infrastructure.Csv;

public class ApplicantCsvParser
{
    public IEnumerable<Applicant> Parse(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        foreach (var row in csv.GetRecords<ApplicantCsvRow>())
        {
            if (string.IsNullOrWhiteSpace(row.Ben) || string.IsNullOrWhiteSpace(row.Name))
                continue;

            yield return new Applicant
            {
                Ben = row.Ben.Trim(),
                Name = row.Name.Trim(),
                EntityType = ParseEntityType(row.EntityType),
                Address = row.Address?.Trim(),
                City = row.City?.Trim(),
                State = row.State?.Trim().ToUpperInvariant(),
                Zip = row.Zip?.Trim(),
                FundingYear = row.FundingYear,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }
    }

    private static ApplicantEntityType ParseEntityType(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "school" => ApplicantEntityType.School,
            "library" => ApplicantEntityType.Library,
            "school district" => ApplicantEntityType.SchoolDistrict,
            "library system" => ApplicantEntityType.LibrarySystem,
            "consortium" => ApplicantEntityType.Consortium,
            _ => ApplicantEntityType.Unknown,
        };
}
