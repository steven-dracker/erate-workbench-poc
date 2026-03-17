using ErateWorkbench.Infrastructure.Csv;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests that the CSV parser correctly maps column headers to domain model fields,
/// skips rows with blank FRNs, and constructs the RawSourceKey correctly.
/// </summary>
public class FundingCommitmentCsvParserTests
{
    private static Stream ToCsvStream(string csv) =>
        new MemoryStream(Encoding.UTF8.GetBytes(csv));

    private readonly FundingCommitmentCsvParser _parser = new();

    [Fact]
    public void Parse_MapsAllFieldsCorrectly()
    {
        const string csv = """
            funding_request_number,form_471_line_item_number,organization_name,billed_entity_number,application_number,funding_year,spin_name,spin_number,chosen_category_of_service,form_471_service_type_name,form_471_frn_status_name,post_discount_extended_eligible_line_item_costs,pre_discount_extended_eligible_line_item_costs
            FRN1234567,1,Springfield Elementary,100001,APP-001,2024,Acme Telecom,143026296,Category 1,Internet Access,Funded,5000.00,6250.00
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var c = results[0];
        Assert.Equal("FRN1234567", c.FundingRequestNumber);
        Assert.Equal(1, c.FrnLineItemNumber);
        Assert.Equal("FRN1234567-1", c.RawSourceKey);
        Assert.Equal("Springfield Elementary", c.ApplicantName);
        Assert.Equal("100001", c.ApplicantEntityNumber);
        Assert.Equal("APP-001", c.ApplicationNumber);
        Assert.Equal(2024, c.FundingYear);
        Assert.Equal("Acme Telecom", c.ServiceProviderName);
        Assert.Equal("143026296", c.ServiceProviderSpin);
        Assert.Equal("Category 1", c.CategoryOfService);
        Assert.Equal("Internet Access", c.TypeOfService);
        Assert.Equal("Funded", c.CommitmentStatus);
        Assert.Equal(5000.00m, c.CommittedAmount);
        Assert.Equal(6250.00m, c.TotalEligibleAmount);
    }

    [Fact]
    public void Parse_WhenNoLineItemNumber_RawSourceKeyIsFrnOnly()
    {
        const string csv = """
            funding_request_number,form_471_line_item_number,organization_name,funding_year
            FRN9999999,,Some School,2024
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Null(results[0].FrnLineItemNumber);
        Assert.Equal("FRN9999999", results[0].RawSourceKey);
    }

    [Fact]
    public void Parse_SkipsRowsWithBlankFundingRequestNumber()
    {
        const string csv = """
            funding_request_number,form_471_line_item_number,funding_year
            FRN0000001,1,2024
            ,2,2024
               ,3,2024
            FRN0000002,1,2024
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.FundingRequestNumber)));
    }

    [Fact]
    public void Parse_EmptyOptionalFields_AreStoredAsNull()
    {
        // ros_entity_name, organization_name, and billed_entity_number all blank/absent
        // so ApplicantName has no fallback and must be null
        const string csv = """
            funding_request_number,form_471_line_item_number,ros_entity_name,organization_name,billed_entity_number,funding_year,spin_name,spin_number,form_471_frn_status_name,post_discount_extended_eligible_line_item_costs,pre_discount_extended_eligible_line_item_costs
            FRN0000001,1,,,,2024,,,,,
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var c = results[0];
        Assert.Null(c.ApplicantName);
        Assert.Null(c.ServiceProviderName);
        Assert.Null(c.ServiceProviderSpin);
        Assert.Null(c.CommitmentStatus);
        Assert.Null(c.CommittedAmount);
        Assert.Null(c.TotalEligibleAmount);
    }

    [Fact]
    public void Parse_PrefersRosEntityNameOverOrganizationName()
    {
        const string csv = """
            funding_request_number,funding_year,ros_entity_name,organization_name,billed_entity_number
            FRN1234567,2024,Springfield Elementary School,Springfield School District,100001
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("Springfield Elementary School", results[0].ApplicantName);
    }

    [Fact]
    public void Parse_FallsBackToOrganizationNameWhenRosEntityNameIsBlank()
    {
        const string csv = """
            funding_request_number,funding_year,ros_entity_name,organization_name,billed_entity_number
            FRN1234567,2024,,Springfield School District,100001
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("Springfield School District", results[0].ApplicantName);
    }

    [Fact]
    public void Parse_FallsBackToEntityLabelWhenBothNamesAreBlank()
    {
        const string csv = """
            funding_request_number,funding_year,ros_entity_name,organization_name,billed_entity_number
            FRN1234567,2024,,,100001
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("Entity 100001", results[0].ApplicantName);
    }

    [Fact]
    public void Parse_IgnoresUnknownColumns_DoesNotThrow()
    {
        const string csv = """
            funding_request_number,form_471_line_item_number,funding_year,some_future_column
            FRN0000001,1,2024,some value
            """;

        var ex = Record.Exception(() => _parser.Parse(ToCsvStream(csv)).ToList());
        Assert.Null(ex);
    }
}
