using ErateWorkbench.Infrastructure.Csv;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests that the Form 471 CSV parser correctly maps headers to the domain model,
/// builds the RawSourceKey from ApplicationNumber + FundingYear, skips invalid rows,
/// and handles missing/unknown columns without throwing.
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
            Application Number,Funding Year,Applicant Name,Applicant Entity Number,Applicant State,Category of Service,Type of Service,Total Pre-Discount Eligible Amount,Application Status
            471-12345678,2024,Springfield Elementary,100001,IL,Category 1,Internet Access,50000.00,Funded
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
        Assert.Equal("Internet Access", app.ServiceType);
        Assert.Equal(50000.00m, app.RequestedAmount);
        Assert.Equal("Funded", app.ApplicationStatus);
    }

    [Fact]
    public void Parse_RawSourceKey_IsCompositeOfApplicationNumberAndYear()
    {
        const string csv = """
            Application Number,Funding Year
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
            Application Number,Funding Year
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
            Application Number,Funding Year
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
            Application Number,Funding Year,Applicant State
            APP-001,2024,tx
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal("TX", results[0].ApplicantState);
    }

    [Fact]
    public void Parse_EmptyOptionalFields_AreStoredAsNull()
    {
        const string csv = """
            Application Number,Funding Year,Applicant Name,Category of Service,Type of Service,Total Pre-Discount Eligible Amount,Application Status
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
    }

    [Fact]
    public void Parse_IgnoresUnknownColumns_DoesNotThrow()
    {
        const string csv = """
            Application Number,Funding Year,Some Future Column
            APP-001,2024,some value
            """;

        var ex = Record.Exception(() => _parser.Parse(ToCsvStream(csv)).ToList());
        Assert.Null(ex);
    }
}
