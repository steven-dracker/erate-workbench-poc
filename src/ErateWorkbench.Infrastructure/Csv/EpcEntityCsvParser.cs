using CsvHelper;
using CsvHelper.Configuration;
using ErateWorkbench.Domain;
using System.Globalization;

namespace ErateWorkbench.Infrastructure.Csv;

public class EpcEntityCsvParser
{
    public IEnumerable<EpcEntity> Parse(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        foreach (var row in csv.GetRecords<EpcEntityCsvRow>())
        {
            if (string.IsNullOrWhiteSpace(row.EntityNumber) || string.IsNullOrWhiteSpace(row.EntityName))
                continue;

            yield return new EpcEntity
            {
                EntityNumber = row.EntityNumber.Trim(),
                EntityName = row.EntityName.Trim(),
                EntityType = ParseEntityType(row.EntityType),
                Status = row.Status?.Trim(),
                ParentEntityNumber = NullIfEmpty(row.ParentEntityNumber),
                ParentEntityName = NullIfEmpty(row.ParentEntityName),
                PhysicalAddress = NullIfEmpty(row.PhysicalAddress),
                PhysicalCity = NullIfEmpty(row.PhysicalCity),
                PhysicalCounty = NullIfEmpty(row.PhysicalCounty),
                PhysicalState = row.PhysicalState?.Trim().ToUpperInvariant(),
                PhysicalZip = NullIfEmpty(row.PhysicalZipcode),
                Phone = NullIfEmpty(row.PhoneNumber),
                Email = NullIfEmpty(row.Email),
                Website = NullIfEmpty(row.WebsiteUrl),
                Latitude = row.Latitude,
                Longitude = row.Longitude,
                UrbanRuralStatus = NullIfEmpty(row.UrbanRuralStatus),
                CategoryOneDiscountRate = row.CategoryOneDiscountRate,
                CategoryTwoDiscountRate = row.CategoryTwoDiscountRate,
                LocaleCode = NullIfEmpty(row.LocaleCode),
                StudentCount = row.C2SchoolStudentCount,
                FccRegistrationNumber = NullIfEmpty(row.FccRegistrationNumber),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }
    }

    private static EpcEntityType ParseEntityType(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "school" => EpcEntityType.School,
            "library" => EpcEntityType.Library,
            "school district" => EpcEntityType.SchoolDistrict,
            "library system" => EpcEntityType.LibrarySystem,
            "consortium" => EpcEntityType.Consortium,
            "non-instructional facility" or "non instructional facility" => EpcEntityType.NonInstructionalFacility,
            _ => EpcEntityType.Unknown,
        };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
