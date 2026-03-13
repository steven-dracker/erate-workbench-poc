using ErateWorkbench.Infrastructure.Csv;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests that the CSV parser correctly maps column headers to the ServiceProvider domain model,
/// skips rows with blank SPIN or provider name, and normalizes state to uppercase.
/// </summary>
public class ServiceProviderCsvParserTests
{
    private static Stream ToCsvStream(string csv) =>
        new MemoryStream(Encoding.UTF8.GetBytes(csv));

    private readonly ServiceProviderCsvParser _parser = new();

    [Fact]
    public void Parse_MapsAllFieldsCorrectly()
    {
        const string csv = """
            SPIN,Service Provider Name,Status,Phone Number,Email,Website,Address,City,State,Zip
            143026296,Acme Telecom,Active,555-1234,info@acme.com,https://acme.com,123 Main St,Springfield,IL,62701
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var sp = results[0];
        Assert.Equal("143026296", sp.Spin);
        Assert.Equal("143026296", sp.RawSourceKey);
        Assert.Equal("Acme Telecom", sp.ProviderName);
        Assert.Equal("Active", sp.Status);
        Assert.Equal("555-1234", sp.Phone);
        Assert.Equal("info@acme.com", sp.Email);
        Assert.Equal("https://acme.com", sp.Website);
        Assert.Equal("123 Main St", sp.Address);
        Assert.Equal("Springfield", sp.City);
        Assert.Equal("IL", sp.State);
        Assert.Equal("62701", sp.Zip);
    }

    [Fact]
    public void Parse_StateIsNormalizedToUppercase()
    {
        const string csv = """
            SPIN,Service Provider Name,State
            143026296,Acme Telecom,il
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal("IL", results[0].State);
    }

    [Fact]
    public void Parse_SkipsRowsWithBlankSpin()
    {
        const string csv = """
            SPIN,Service Provider Name
            143026296,Acme Telecom
            ,Missing Spin Corp
               ,Whitespace Spin LLC
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("143026296", results[0].Spin);
    }

    [Fact]
    public void Parse_SkipsRowsWithBlankProviderName()
    {
        const string csv = """
            SPIN,Service Provider Name
            143026296,
            143026297,Valid Provider
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("143026297", results[0].Spin);
    }

    [Fact]
    public void Parse_EmptyOptionalFields_AreStoredAsNull()
    {
        const string csv = """
            SPIN,Service Provider Name,Status,Phone Number,Email,Website,Address,City,State,Zip
            143026296,Acme Telecom,,,,,,,,
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var sp = results[0];
        Assert.Null(sp.Status);
        Assert.Null(sp.Phone);
        Assert.Null(sp.Email);
        Assert.Null(sp.Website);
        Assert.Null(sp.State);
    }

    [Fact]
    public void Parse_IgnoresUnknownColumns_DoesNotThrow()
    {
        const string csv = """
            SPIN,Service Provider Name,Future Column
            143026296,Acme Telecom,some future value
            """;

        var ex = Record.Exception(() => _parser.Parse(ToCsvStream(csv)).ToList());
        Assert.Null(ex);
    }

    [Fact]
    public void Parse_RawSourceKeyEqualsSpin()
    {
        const string csv = """
            SPIN,Service Provider Name
            143026296,Acme Telecom
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(results[0].Spin, results[0].RawSourceKey);
    }
}
