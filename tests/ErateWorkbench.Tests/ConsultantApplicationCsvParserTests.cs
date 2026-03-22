using ErateWorkbench.Infrastructure.Csv;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests that the ConsultantApplicationCsvParser correctly maps x5px-esft CSV fields
/// to the domain model, builds RawSourceKey from ApplicationNumber + ConsultantEpcOrganizationId,
/// skips rows with missing identity fields, and handles null optional columns.
///
/// Header names match the live Socrata API response (CC-ERATE-000038A confirmed schema).
/// </summary>
public class ConsultantApplicationCsvParserTests
{
    private static Stream ToCsvStream(string csv) =>
        new MemoryStream(Encoding.UTF8.GetBytes(csv));

    private readonly ConsultantApplicationCsvParser _parser = new();

    [Fact]
    public void Parse_MapsAllFieldsCorrectly()
    {
        const string csv = """
            application_number,funding_year,state,form_version,is_certified_in_window,epc_organization_id,organization_name,applicant_type,cnct_email,cnslt_name,cnslt_epc_organization_id,cnslt_city,cnslt_state,cnslt_zipcode,cnslt_phone,cnslt_email
            211007579,2021,OH,Original,In Window,130138,UPPER SCIOTO VALLEY LSD,School District,ruthie@effsd.com,ERATE FUNDING FOR SCHOOL DISTRICTS,17011235,TROY,OH,45373,937-440-0444,marv@effsd.com
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var r = results[0];

        Assert.Equal("211007579-17011235", r.RawSourceKey);
        Assert.Equal("211007579", r.ApplicationNumber);
        Assert.Equal(2021, r.FundingYear);
        Assert.Equal("OH", r.ApplicantState);
        Assert.Equal("Original", r.FormVersion);
        Assert.Equal("In Window", r.IsCertifiedInWindow);
        Assert.Equal("130138", r.ApplicantEpcOrganizationId);
        Assert.Equal("UPPER SCIOTO VALLEY LSD", r.OrganizationName);
        Assert.Equal("School District", r.ApplicantType);
        Assert.Equal("ruthie@effsd.com", r.ContactEmail);
        Assert.Equal("ERATE FUNDING FOR SCHOOL DISTRICTS", r.ConsultantName);
        Assert.Equal("17011235", r.ConsultantEpcOrganizationId);
        Assert.Equal("TROY", r.ConsultantCity);
        Assert.Equal("OH", r.ConsultantState);
        Assert.Equal("45373", r.ConsultantZipCode);
        Assert.Equal("937-440-0444", r.ConsultantPhone);
        Assert.Equal("marv@effsd.com", r.ConsultantEmail);
    }

    [Fact]
    public void Parse_RawSourceKey_IsCompositeOfApplicationNumberAndConsultantEpcId()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id
            APP001,2023,EPC999
            APP001,2023,EPC888
            APP002,2024,EPC999
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(3, results.Count);
        Assert.Equal("APP001-EPC999", results[0].RawSourceKey);
        Assert.Equal("APP001-EPC888", results[1].RawSourceKey);
        Assert.Equal("APP002-EPC999", results[2].RawSourceKey);
    }

    [Fact]
    public void Parse_SkipsRowsWithBlankApplicationNumber()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id
            APP001,2024,EPC1
            ,2024,EPC1
               ,2024,EPC1
            APP002,2024,EPC1
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Parse_SkipsRowsWithBlankConsultantEpcId()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id
            APP001,2024,EPC1
            APP002,2024,
            APP003,2024,EPC1
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("EPC1", r.ConsultantEpcOrganizationId));
    }

    [Fact]
    public void Parse_SkipsRowsWithInvalidOrZeroFundingYear()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id
            APP001,2024,EPC1
            APP002,0,EPC1
            APP003,notanumber,EPC1
            APP004,2023,EPC1
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(2024, results[0].FundingYear);
        Assert.Equal(2023, results[1].FundingYear);
    }

    [Fact]
    public void Parse_ApplicantStateAndConsultantState_NormalizedToUppercase()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,state,cnslt_state
            APP001,2024,EPC1,oh,md
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("OH", results[0].ApplicantState);
        Assert.Equal("MD", results[0].ConsultantState);
    }

    [Fact]
    public void Parse_EmptyOptionalFields_AreStoredAsNull()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,organization_name,cnslt_name,cnslt_city,cnslt_state,cnslt_zipcode,cnslt_phone,cnslt_email,cnct_email
            APP001,2024,EPC1,,,,,,,,
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        var r = results[0];
        Assert.Null(r.OrganizationName);
        Assert.Null(r.ConsultantName);
        Assert.Null(r.ConsultantCity);
        Assert.Null(r.ConsultantState);
        Assert.Null(r.ConsultantZipCode);
        Assert.Null(r.ConsultantPhone);
        Assert.Null(r.ConsultantEmail);
        Assert.Null(r.ContactEmail);
    }

    [Fact]
    public void Parse_IgnoresUnknownColumns_DoesNotThrow()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,some_future_column
            APP001,2024,EPC1,unexpected_value
            """;

        var ex = Record.Exception(() => _parser.Parse(ToCsvStream(csv)).ToList());
        Assert.Null(ex);
    }

    [Fact]
    public void Parse_PreservesIdentityFields_Untransformed()
    {
        // Identity fields (ApplicationNumber, ConsultantEpcOrganizationId) must not be
        // normalized beyond trimming — they are the canonical join keys.
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id
            211007579,2021,17011235
            """;

        var results = _parser.Parse(ToCsvStream(csv)).ToList();

        Assert.Single(results);
        Assert.Equal("211007579", results[0].ApplicationNumber);
        Assert.Equal("17011235", results[0].ConsultantEpcOrganizationId);
    }
}
