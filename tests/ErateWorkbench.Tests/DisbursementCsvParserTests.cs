using ErateWorkbench.Infrastructure.Csv;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests that DisbursementCsvParser correctly maps column headers from the USAC
/// jpiu-tj8h dataset, constructs the RawSourceKey, skips blank FRN rows, and
/// handles missing optional columns gracefully.
/// </summary>
public class DisbursementCsvParserTests
{
    private static Stream ToCsvStream(string csv) =>
        new MemoryStream(Encoding.UTF8.GetBytes(csv));

    private readonly DisbursementCsvParser _parser = new();

    [Fact]
    public void Parse_MapsAllFieldsCorrectly()
    {
        const string csv = """
            funding_request_number,invoice_id,inv_line_num,invoice_type,inv_line_item_status,form_471_app_num,billed_entity_number,billed_entity_name,inv_service_provider_id_number_spin,inv_service_provider_name,funding_year,chosen_category_of_service,requested_inv_line_amt,approved_inv_line_amt,inv_received_date,inv_line_completion_date
            2390001234,INV20240001,1,BEAR,Approved,240001234,100001,Springfield Elementary,143002468,Acme Telecom,2024,Category 1,5000.00,4800.00,2024-03-15T00:00:00.000,2024-06-30T00:00:00.000
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var d = results[0];
        Assert.Equal("INV20240001-1", d.RawSourceKey);
        Assert.Equal("2390001234", d.FundingRequestNumber);
        Assert.Equal("INV20240001", d.InvoiceId);
        Assert.Equal("1", d.InvoiceLineNumber);
        Assert.Equal("BEAR", d.InvoiceType);
        Assert.Equal("Approved", d.InvoiceLineStatus);
        Assert.Equal("240001234", d.ApplicationNumber);
        Assert.Equal("100001", d.ApplicantEntityNumber);
        Assert.Equal("Springfield Elementary", d.ApplicantEntityName);
        Assert.Equal("143002468", d.ServiceProviderSpin);
        Assert.Equal("Acme Telecom", d.ServiceProviderName);
        Assert.Equal(2024, d.FundingYear);
        Assert.Equal("Category 1", d.CategoryOfService);
        Assert.Equal(5000.00m, d.RequestedAmount);
        Assert.Equal(4800.00m, d.ApprovedAmount);
        Assert.Equal(new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc), d.InvoiceReceivedDate);
        Assert.Equal(new DateTime(2024, 6, 30, 0, 0, 0, DateTimeKind.Utc), d.LineCompletionDate);
    }

    [Fact]
    public void Parse_RawSourceKey_UsesFrnFallback_WhenInvoiceIdIsBlank()
    {
        const string csv = """
            funding_request_number,invoice_id,inv_line_num,funding_year
            2390001234,,2,2024
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("2390001234-2", results[0].RawSourceKey);
        Assert.Null(results[0].InvoiceId);
    }

    [Fact]
    public void Parse_RawSourceKey_UsesZeroLineNum_WhenLineNumIsBlank()
    {
        const string csv = """
            funding_request_number,invoice_id,inv_line_num,funding_year
            2390001234,INV20240001,,2024
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("INV20240001-0", results[0].RawSourceKey);
    }

    [Fact]
    public void Parse_SkipsRowsWithBlankFundingRequestNumber()
    {
        const string csv = """
            funding_request_number,invoice_id,funding_year
            2390001234,INV001,2024
            ,INV002,2024
               ,INV003,2024
            2390001235,INV004,2024
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.False(string.IsNullOrWhiteSpace(d.FundingRequestNumber)));
    }

    [Fact]
    public void Parse_EmptyOptionalFields_StoredAsNull()
    {
        const string csv = """
            funding_request_number,invoice_id,inv_line_num,invoice_type,inv_line_item_status,form_471_app_num,billed_entity_number,billed_entity_name,inv_service_provider_id_number_spin,inv_service_provider_name,funding_year,chosen_category_of_service,requested_inv_line_amt,approved_inv_line_amt,inv_received_date,inv_line_completion_date
            2390001234,,,,,,,,,,2024,,,,
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var d = results[0];
        Assert.Null(d.InvoiceId);
        Assert.Null(d.InvoiceLineNumber);
        Assert.Null(d.InvoiceType);
        Assert.Null(d.InvoiceLineStatus);
        Assert.Null(d.ApplicationNumber);
        Assert.Null(d.ApplicantEntityNumber);
        Assert.Null(d.ApplicantEntityName);
        Assert.Null(d.ServiceProviderSpin);
        Assert.Null(d.ServiceProviderName);
        Assert.Null(d.CategoryOfService);
        Assert.Null(d.RequestedAmount);
        Assert.Null(d.ApprovedAmount);
        Assert.Null(d.InvoiceReceivedDate);
        Assert.Null(d.LineCompletionDate);
    }

    [Fact]
    public void Parse_MissingColumns_DoNotThrow()
    {
        const string csv = """
            funding_request_number,funding_year
            2390001234,2024
            """;

        var ex = Record.Exception(() => _parser.Parse(ToCsvStream(csv)).ToList());
        Assert.Null(ex);

        var results = _parser.Parse(ToCsvStream(csv)).ToList();
        Assert.Single(results);
        Assert.Equal("2390001234", results[0].FundingRequestNumber);
        Assert.Null(results[0].InvoiceId);
        Assert.Null(results[0].InvoiceType);
    }

    [Fact]
    public void Parse_DateParsing_HandlesIso8601WithTime()
    {
        const string csv = """
            funding_request_number,funding_year,inv_received_date,inv_line_completion_date
            2390001234,2024,2024-01-15T00:00:00.000,2024-12-31T00:00:00.000
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc), results[0].InvoiceReceivedDate);
        Assert.Equal(new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc), results[0].LineCompletionDate);
    }

    [Fact]
    public void Parse_BlankFundingYear_SkipsRow()
    {
        const string csv = """
            funding_request_number,invoice_id,funding_year
            2390001234,INV001,2024
            2390001235,INV002,
            2390001236,INV003,
            2390001237,INV004,2025
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal([2024, 2025], results.Select(d => d.FundingYear));
    }

    [Fact]
    public void Parse_ValidFundingYear_ParsedCorrectly()
    {
        const string csv = """
            funding_request_number,funding_year
            2390001234,2023
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal(2023, results[0].FundingYear);
    }

    [Fact]
    public void Parse_DeduplicationNotDoneByParser_BothRowsYielded()
    {
        // Parser yields all rows; deduplication is the repository's responsibility.
        const string csv = """
            funding_request_number,invoice_id,inv_line_num,funding_year
            2390001234,INV001,1,2024
            2390001234,INV001,1,2024
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
    }
}
