using ErateWorkbench.Infrastructure.Csv;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests that EntityCsvParser correctly maps ros_* column headers, skips rows
/// with a blank EntityNumber, and handles missing optional fields gracefully.
/// </summary>
public class EntityCsvParserTests
{
    private static Stream ToCsvStream(string csv) =>
        new MemoryStream(Encoding.UTF8.GetBytes(csv));

    private readonly EntityCsvParser _parser = new();

    [Fact]
    public void Parse_MapsAllFieldsCorrectly()
    {
        const string csv = """
            ros_entity_number,ros_entity_name,ros_entity_type,ros_urban_rural_status,ros_physical_state,ros_number_of_full_time_students,ros_total_number_of_part_time_students,ros_number_of_nslp_students
            100001,Springfield Elementary,School,Rural,IL,450,12,380
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var e = results[0];
        Assert.Equal("100001", e.EntityNumber);
        Assert.Equal("Springfield Elementary", e.EntityName);
        Assert.Equal("School", e.EntityType);
        Assert.Equal("Rural", e.UrbanRuralStatus);
        Assert.Equal("IL", e.State);
        Assert.Equal(450, e.FullTimeStudentCount);
        Assert.Equal(12, e.PartTimeStudentCount);
        Assert.Equal(380, e.NslpStudentCount);
    }

    [Fact]
    public void Parse_NormalizesStateToUpperCase()
    {
        const string csv = """
            ros_entity_number,ros_physical_state
            100002,il
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("IL", results[0].State);
    }

    [Fact]
    public void Parse_SkipsRowsWithBlankEntityNumber()
    {
        const string csv = """
            ros_entity_number,ros_entity_name,ros_physical_state
            100001,School A,IL
            ,School B,CA
               ,School C,TX
            100002,School D,NY
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.False(string.IsNullOrWhiteSpace(e.EntityNumber)));
    }

    [Fact]
    public void Parse_EmptyOptionalFields_StoredAsNull()
    {
        const string csv = """
            ros_entity_number,ros_entity_name,ros_entity_type,ros_urban_rural_status,ros_physical_state,ros_number_of_full_time_students,ros_total_number_of_part_time_students,ros_number_of_nslp_students
            100003,,,,,,,
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var e = results[0];
        Assert.Null(e.EntityName);
        Assert.Null(e.EntityType);
        Assert.Null(e.UrbanRuralStatus);
        Assert.Null(e.State);
        Assert.Null(e.FullTimeStudentCount);
        Assert.Null(e.PartTimeStudentCount);
        Assert.Null(e.NslpStudentCount);
    }

    [Fact]
    public void Parse_MissingColumns_DoNotThrow()
    {
        // Only the natural key column is present; all others are absent.
        const string csv = """
            ros_entity_number,some_unrelated_column
            100004,some value
            """;

        var ex = Record.Exception(() => _parser.Parse(ToCsvStream(csv)).ToList());
        Assert.Null(ex);

        var results = _parser.Parse(ToCsvStream(csv)).ToList();
        Assert.Single(results);
        Assert.Equal("100004", results[0].EntityNumber);
        Assert.Null(results[0].EntityName);
        Assert.Null(results[0].State);
    }

    [Fact]
    public void Parse_DeduplicatesAreNotDoneByParser_LastOccurrencePreservedByRepo()
    {
        // Parser yields ALL rows — deduplication is the repository's responsibility.
        // This test confirms the parser yields both rows even with the same EntityNumber.
        const string csv = """
            ros_entity_number,ros_entity_name
            100005,First Name
            100005,Second Name
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("First Name", results[0].EntityName);
        Assert.Equal("Second Name", results[1].EntityName);
    }
}
