using ErateWorkbench.Infrastructure.Csv;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests that the Form 471 CSV parser correctly maps headers to the domain model,
/// builds the RawSourceKey from ApplicationNumber + FundingYear, skips invalid rows,
/// and handles missing/unknown columns without throwing.
///
/// Header format: snake_case as exported by Socrata dataset 9s6i-myen.
/// CategoryOfService compact values ("Category1", "Category2") are normalized to
/// "Category 1" / "Category 2" by the parser.
/// </summary>
public class Form471CsvParserTests
{
    private static Stream ToCsvStream(string csv) =>
        new MemoryStream(Encoding.UTF8.GetBytes(csv));

    private readonly Form471CsvParser _parser = new();

    [Fact]
    public void Parse_MapsAllFieldsCorrectly()
    {
        const string csv = """
            application_number,funding_year,applicant_name,ben,applicant_state,category_of_service,total_pre_discount_eligible_amount,application_status,certified_datetime
            471-12345678,2024,Springfield Elementary,100001,IL,Category1,50000.00,Funded,2024-02-15T10:00:00
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var app = results[0];
        Assert.Equal("471-12345678", app.ApplicationNumber);
        Assert.Equal(2024, app.FundingYear);
        Assert.Equal("471-12345678-2024", app.RawSourceKey);
        Assert.Equal("Springfield Elementary", app.ApplicantName);
        Assert.Equal("100001", app.ApplicantEntityNumber);
        Assert.Equal("IL", app.ApplicantState);
        Assert.Equal("Category 1", app.CategoryOfService);
        Assert.Null(app.ServiceType);   // not in 9s6i-myen
        Assert.Equal(50000.00m, app.RequestedAmount);
        Assert.Equal("Funded", app.ApplicationStatus);
        Assert.NotNull(app.CertificationDate);
        Assert.Equal(new DateTime(2024, 2, 15, 10, 0, 0), app.CertificationDate!.Value);
    }

    [Fact]
    public void Parse_RawSourceKey_IsCompositeOfApplicationNumberAndYear()
    {
        const string csv = """
            application_number,funding_year
            APP-001,2023
            APP-001,2024
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("APP-001-2023", results[0].RawSourceKey);
        Assert.Equal("APP-001-2024", results[1].RawSourceKey);
    }

    [Fact]
    public void Parse_SkipsRowsWithBlankApplicationNumber()
    {
        const string csv = """
            application_number,funding_year
            APP-001,2024
            ,2024
               ,2024
            APP-002,2024
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Parse_SkipsRowsWithZeroFundingYear()
    {
        const string csv = """
            application_number,funding_year
            APP-001,0
            APP-002,2024
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("APP-002", results[0].ApplicationNumber);
    }

    [Fact]
    public void Parse_ApplicantState_NormalizedToUppercase()
    {
        const string csv = """
            application_number,funding_year,applicant_state
            APP-001,2024,tx
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal("TX", results[0].ApplicantState);
    }

    [Fact]
    public void Parse_EmptyOptionalFields_AreStoredAsNull()
    {
        const string csv = """
            application_number,funding_year,applicant_name,category_of_service,total_pre_discount_eligible_amount,application_status,certified_datetime
            APP-001,2024,,,,,
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var app = results[0];
        Assert.Null(app.ApplicantName);
        Assert.Null(app.CategoryOfService);
        Assert.Null(app.ServiceType);
        Assert.Null(app.RequestedAmount);
        Assert.Null(app.ApplicationStatus);
        Assert.Null(app.CertificationDate);
    }

    [Fact]
    public void Parse_IgnoresUnknownColumns_DoesNotThrow()
    {
        const string csv = """
            application_number,funding_year,some_future_column
            APP-001,2024,some value
            """;

        var ex = Record.Exception(() => _parser.Parse(ToCsvStream(csv)).ToList());
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("Category1", "Category 1")]
    [InlineData("Category2", "Category 2")]
    [InlineData("Category 1", "Category 1")]   // already normalized — pass through
    [InlineData("Category 2", "Category 2")]
    public void Parse_NormalizesCategory(string raw, string expected)
    {
        var csv = $"application_number,funding_year,category_of_service\nAPP-001,2024,{raw}\n";

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal(expected, results[0].CategoryOfService);
    }
}
