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
            Funding Request Number,FRN Line Item Number,Applicant Name,Applicant Entity Number,Application Number,Funding Year,Service Provider Name,SPIN,Category of Service,Type of Service,Commitment Status,Committed Amount,Total Eligible Pre-Discount Amount
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
            Funding Request Number,FRN Line Item Number,Applicant Name,Funding Year
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
            Funding Request Number,FRN Line Item Number,Funding Year
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
        const string csv = """
            Funding Request Number,FRN Line Item Number,Applicant Name,Funding Year,Service Provider Name,SPIN,Commitment Status,Committed Amount,Total Eligible Pre-Discount Amount
            FRN0000001,1,,2024,,,,,
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
    public void Parse_IgnoresUnknownColumns_DoesNotThrow()
    {
        const string csv = """
            Funding Request Number,FRN Line Item Number,Funding Year,Some Future Column
            FRN0000001,1,2024,some value
            """;

        var ex = Record.Exception(() => _parser.Parse(ToCsvStream(csv)).ToList());
        Assert.Null(ex);
    }
}
