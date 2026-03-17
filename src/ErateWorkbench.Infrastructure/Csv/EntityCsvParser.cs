using CsvHelper;
using CsvHelper.Configuration;
using ErateWorkbench.Domain;
using System.Globalization;

namespace ErateWorkbench.Infrastructure.Csv;

public class EntityCsvParser
{
    public IEnumerable<Entity> Parse(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        foreach (var row in csv.GetRecords<EntityCsvRow>())
        {
            if (string.IsNullOrWhiteSpace(row.EntityNumber))
                continue;

            var now = DateTime.UtcNow;

            yield return new Entity
            {
                EntityNumber = row.EntityNumber.Trim(),
                EntityName = NullIfEmpty(row.EntityName),
                EntityType = NullIfEmpty(row.EntityType),
                UrbanRuralStatus = NullIfEmpty(row.UrbanRuralStatus),
                State = NullIfEmpty(row.State)?.ToUpperInvariant(),
                FullTimeStudentCount = row.FullTimeStudentCount,
                PartTimeStudentCount = row.PartTimeStudentCount,
                NslpStudentCount = row.NslpStudentCount,
                ImportedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
