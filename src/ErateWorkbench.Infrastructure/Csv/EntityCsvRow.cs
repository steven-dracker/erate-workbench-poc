using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps the ros_* columns from the USAC E-Rate Funding Request Commitments dataset
/// (Socrata resource avi8-svp9) to an entity-level record.
///
/// These columns repeat on every commitment row; the import deduplicates by
/// EntityNumber when upserting into the Entities table.
///
/// MissingFieldFound = null is set in the parser config, so columns absent from
/// a particular export are silently left as null.
/// </summary>
public class EntityCsvRow
{
    [Name("ros_entity_number")]
    public string EntityNumber { get; set; } = "";

    [Name("ros_entity_name")]
    public string? EntityName { get; set; }

    [Name("ros_entity_type")]
    public string? EntityType { get; set; }

    [Name("ros_urban_rural_status")]
    public string? UrbanRuralStatus { get; set; }

    [Name("ros_physical_state")]
    public string? State { get; set; }

    [Name("ros_number_of_full_time_students")]
    public int? FullTimeStudentCount { get; set; }

    [Name("ros_total_number_of_part_time_students")]
    public int? PartTimeStudentCount { get; set; }

    [Name("ros_number_of_nslp_students")]
    public int? NslpStudentCount { get; set; }
}
