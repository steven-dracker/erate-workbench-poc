using ErateWorkbench.Infrastructure.Csv;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests that the ConsultantFrnStatusCsvParser correctly maps mihb-jfex CSV fields
/// to the domain model, builds RawSourceKey from ApplicationNumber + FundingRequestNumber,
/// safely parses decimal and date fields, and skips rows with missing identity fields.
///
/// Header names match the live Socrata API response (CC-ERATE-000038A confirmed schema).
/// </summary>
public class ConsultantFrnStatusCsvParserTests
{
    private static Stream ToCsvStream(string csv) =>
        new MemoryStream(Encoding.UTF8.GetBytes(csv));

    private readonly ConsultantFrnStatusCsvParser _parser = new();

    [Fact]
    public void Parse_MapsAllFieldsCorrectly()
    {
        const string csv = """
            application_number,funding_year,state,form_version,is_certified_in_window,ben,organization_name,organization_entity_type_name,cnct_email,cnslt_name,cnslt_epc_organization_id,funding_request_number,form_471_frn_status_name,nickname,form_471_service_type_name,contract_type_name,spin_name,pending_reason,invoicing_mode,dis_pct,total_pre_discount_costs,funding_commitment_request,total_authorized_disbursement,service_start_date,fcdl_letter_date
            161011863,2016,TX,Current,In Window,140028,Texhoma Independent Sch Dist,School District,contact@example.com,ESC Region 12 E-Rate Consulting,16062048,1699028290,Funded,Internet Access,Data Transmission and/or Internet Access,Contract,Region 16 ESC,FCDL Issued,SPI,0.8,57499.92,45999.94,45615.94,2016-07-01T00:00:00.000,2016-10-17T00:00:00.000
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var r = results[0];

        Assert.Equal("161011863-1699028290", r.RawSourceKey);
        Assert.Equal("1699028290", r.FundingRequestNumber);
        Assert.Equal("161011863", r.ApplicationNumber);
        Assert.Equal(2016, r.FundingYear);
        Assert.Equal("TX", r.ApplicantState);
        Assert.Equal("Current", r.FormVersion);
        Assert.Equal("In Window", r.IsCertifiedInWindow);
        Assert.Equal("140028", r.Ben);
        Assert.Equal("Texhoma Independent Sch Dist", r.OrganizationName);
        Assert.Equal("School District", r.OrganizationEntityTypeName);
        Assert.Equal("contact@example.com", r.ContactEmail);
        Assert.Equal("ESC Region 12 E-Rate Consulting", r.ConsultantName);
        Assert.Equal("16062048", r.ConsultantEpcOrganizationId);
        Assert.Equal("Funded", r.FrnStatusName);
        Assert.Equal("Internet Access", r.Nickname);
        Assert.Equal("Data Transmission and/or Internet Access", r.ServiceTypeName);
        Assert.Equal("Contract", r.ContractTypeName);
        Assert.Equal("Region 16 ESC", r.SpinName);
        Assert.Equal("FCDL Issued", r.PendingReason);
        Assert.Equal("SPI", r.InvoicingMode);
        Assert.Equal(0.8m, r.DiscountPct);
        Assert.Equal(57499.92m, r.TotalPreDiscountCosts);
        Assert.Equal(45999.94m, r.FundingCommitmentRequest);
        Assert.Equal(45615.94m, r.TotalAuthorizedDisbursement);
        Assert.NotNull(r.ServiceStartDate);
        Assert.Equal(new DateTime(2016, 7, 1, 0, 0, 0, DateTimeKind.Utc), r.ServiceStartDate!.Value);
        Assert.NotNull(r.FcdlLetterDate);
        Assert.Equal(new DateTime(2016, 10, 17, 0, 0, 0, DateTimeKind.Utc), r.FcdlLetterDate!.Value);
    }

    [Fact]
    public void Parse_RawSourceKey_IsCompositeOfApplicationNumberAndFrn()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number
            APP001,2023,EPC1,FRN111
            APP001,2023,EPC1,FRN222
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("APP001-FRN111", results[0].RawSourceKey);
        Assert.Equal("APP001-FRN222", results[1].RawSourceKey);
    }

    [Fact]
    public void Parse_SkipsRowsWithBlankApplicationNumber()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number
            APP001,2024,EPC1,FRN1
            ,2024,EPC1,FRN2
            APP002,2024,EPC1,FRN3
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Parse_SkipsRowsWithBlankFundingRequestNumber()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number
            APP001,2024,EPC1,FRN1
            APP002,2024,EPC1,
            APP003,2024,EPC1,FRN3
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Parse_SkipsRowsWithBlankConsultantEpcId()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number
            APP001,2024,EPC1,FRN1
            APP002,2024,,FRN2
            APP003,2024,EPC1,FRN3
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Parse_DecimalFields_ParsedSafely_BlankProducesNull()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number,dis_pct,funding_commitment_request,total_authorized_disbursement,total_pre_discount_costs
            APP001,2024,EPC1,FRN1,,,,
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var r = results[0];
        Assert.Null(r.DiscountPct);
        Assert.Null(r.FundingCommitmentRequest);
        Assert.Null(r.TotalAuthorizedDisbursement);
        Assert.Null(r.TotalPreDiscountCosts);
    }

    [Fact]
    public void Parse_DecimalFields_InvalidValue_ProducesNull()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number,funding_commitment_request
            APP001,2024,EPC1,FRN1,notanumber
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Null(results[0].FundingCommitmentRequest);
    }

    [Fact]
    public void Parse_DateFields_BlankProducesNull()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number,service_start_date,fcdl_letter_date
            APP001,2024,EPC1,FRN1,,
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Null(results[0].ServiceStartDate);
        Assert.Null(results[0].FcdlLetterDate);
    }

    [Fact]
    public void Parse_ApplicantState_NormalizedToUppercase()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number,state
            APP001,2024,EPC1,FRN1,tx
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("TX", results[0].ApplicantState);
    }

    [Fact]
    public void Parse_IgnoresUnknownColumns_DoesNotThrow()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number,fcdl_comment_frn,narrative
            APP001,2024,EPC1,FRN1,MR1:Approved as submitted.,internet access
            """;

        var ex = Record.Exception(() => _parser.Parse(ToCsvStream(csv)).ToList());
        Assert.Null(ex);
    }

    [Fact]
    public void Parse_PreservesIdentityFields_Untransformed()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number
            161011863,2016,16062048,1699028290
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("161011863", results[0].ApplicationNumber);
        Assert.Equal("1699028290", results[0].FundingRequestNumber);
        Assert.Equal("16062048", results[0].ConsultantEpcOrganizationId);
    }
}
