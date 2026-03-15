namespace ErateWorkbench.Domain;

/// <summary>
/// A USAC E-Rate Recipient of Service (ROS) entity: a school, library, or other
/// eligible institution that appears in E-Rate funding commitments.
///
/// Sourced from the ros_* columns in the USAC E-Rate Funding Request Commitments
/// dataset (avi8-svp9). Each funding commitment row carries entity-level metadata;
/// this table deduplicates by EntityNumber to form a joinable entity dimension.
///
/// Natural key: <see cref="EntityNumber"/> — unique per USAC entity registration.
/// </summary>
public class Entity
{
    public int Id { get; set; }

    /// <summary>USAC Entity Number. Unique per registered entity. Natural key for upsert.</summary>
    public required string EntityNumber { get; set; }

    /// <summary>Registered name of the school, library, or institution.</summary>
    public string? EntityName { get; set; }

    /// <summary>Entity category: e.g. "School", "Library", "School District", "Library System".</summary>
    public string? EntityType { get; set; }

    /// <summary>NCES urban/rural locale classification: e.g. "Urban", "Suburban", "Rural".</summary>
    public string? UrbanRuralStatus { get; set; }

    /// <summary>Two-letter physical state code.</summary>
    public string? State { get; set; }

    /// <summary>Full-time student enrollment count (ros_number_of_full_time_students).</summary>
    public int? FullTimeStudentCount { get; set; }

    /// <summary>Part-time student enrollment count (ros_total_number_of_part_time_students).</summary>
    public int? PartTimeStudentCount { get; set; }

    /// <summary>National School Lunch Program student count (ros_number_of_nslp_students).</summary>
    public int? NslpStudentCount { get; set; }

    public DateTime ImportedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
