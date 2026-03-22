using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using ErateWorkbench.Infrastructure.Csv;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Integration tests for ConsultantApplicationImportService and ConsultantFrnStatusImportService.
/// Covers: idempotent upsert, identity field preservation, abort-on-unavailable, and null handling.
/// Uses in-memory SQLite + StubHttpHandler (no Moq).
/// </summary>
public class ConsultantImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public ConsultantImportServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── ConsultantApplication import tests ─────────────────────────────────

    [Fact]
    public async Task ConsultantApplicationImport_InsertsRows_OnFirstRun()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,state,organization_name,applicant_type,cnslt_name,cnslt_state
            211007579,2021,17011235,OH,UPPER SCIOTO VALLEY LSD,School District,ERATE FUNDING FOR SCHOOL DISTRICTS,OH
            """;

        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            // call 1 = probe, call 2 = download
            var content = callCount == 1 ? "" : csv;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/csv"),
            });
        });

        var service = BuildConsultantApplicationService(handler);
        var result = await service.RunAsync("https://datahub.usac.org/resource/x5px-esft.csv");

        Assert.Equal(ImportJobStatus.Succeeded, result.Status);
        Assert.Equal(1, result.RecordsInserted);
        Assert.Equal(0, result.RecordsUpdated);
        Assert.Equal(1, _db.ConsultantApplications.Count());

        var stored = _db.ConsultantApplications.Single();
        Assert.Equal("211007579-17011235", stored.RawSourceKey);
        Assert.Equal("211007579", stored.ApplicationNumber);
        Assert.Equal("17011235", stored.ConsultantEpcOrganizationId);
        Assert.Equal("OH", stored.ApplicantState);
    }

    [Fact]
    public async Task ConsultantApplicationImport_IsIdempotent_OnRerun()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id
            APP001,2024,EPC1
            """;

        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            var content = callCount % 2 == 1 ? "" : csv; // odd = probe, even = download
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/csv"),
            });
        });

        var service = BuildConsultantApplicationService(handler);

        var result1 = await service.RunAsync("https://example.com/x5px-esft.csv");
        var result2 = await service.RunAsync("https://example.com/x5px-esft.csv");

        Assert.Equal(1, result1.RecordsInserted);
        Assert.Equal(0, result2.RecordsInserted);
        Assert.Equal(1, result2.RecordsUpdated);
        Assert.Equal(1, _db.ConsultantApplications.Count());
    }

    [Fact]
    public async Task ConsultantApplicationImport_AbortsEarly_WhenUpstreamIsUnavailable()
    {
        var handler = new StubHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var service = BuildConsultantApplicationService(handler);
        var result = await service.RunAsync("https://example.com/x5px-esft.csv");

        Assert.Equal(ImportJobStatus.Failed, result.Status);
        Assert.Contains("unavailable", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _db.ConsultantApplications.Count());
    }

    [Fact]
    public async Task ConsultantApplicationImport_PreservesIdentityFields()
    {
        // Verifies ConsultantEpcOrganizationId and ApplicationNumber are stored as-is (no transformation)
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,cnslt_name
            161001922,2016,16043595,Erate Exchange LLC
            """;

        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            var content = callCount == 1 ? "" : csv;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/csv"),
            });
        });

        var service = BuildConsultantApplicationService(handler);
        await service.RunAsync("https://example.com/x5px-esft.csv");

        var stored = _db.ConsultantApplications.Single();
        Assert.Equal("161001922", stored.ApplicationNumber);
        Assert.Equal("16043595", stored.ConsultantEpcOrganizationId);
        // ConsultantName preserved as-is — no casing normalization
        Assert.Equal("Erate Exchange LLC", stored.ConsultantName);
    }

    // ── ConsultantFrnStatus import tests ───────────────────────────────────

    [Fact]
    public async Task ConsultantFrnStatusImport_InsertsRows_OnFirstRun()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number,state,organization_name,form_471_frn_status_name,form_471_service_type_name,dis_pct,funding_commitment_request,total_authorized_disbursement
            161011863,2016,16062048,1699028290,TX,Texhoma Independent Sch Dist,Funded,Data Transmission and/or Internet Access,0.8,45999.94,45615.94
            """;

        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            var content = callCount == 1 ? "" : csv;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/csv"),
            });
        });

        var service = BuildConsultantFrnStatusService(handler);
        var result = await service.RunAsync("https://example.com/mihb-jfex.csv");

        Assert.Equal(ImportJobStatus.Succeeded, result.Status);
        Assert.Equal(1, result.RecordsInserted);
        Assert.Equal(0, result.RecordsUpdated);
        Assert.Equal(1, _db.ConsultantFrnStatuses.Count());

        var stored = _db.ConsultantFrnStatuses.Single();
        Assert.Equal("161011863-1699028290", stored.RawSourceKey);
        Assert.Equal("1699028290", stored.FundingRequestNumber);
        Assert.Equal("161011863", stored.ApplicationNumber);
        Assert.Equal("16062048", stored.ConsultantEpcOrganizationId);
        Assert.Equal("Data Transmission and/or Internet Access", stored.ServiceTypeName);
        Assert.Equal(0.8m, stored.DiscountPct);
        Assert.Equal(45999.94m, stored.FundingCommitmentRequest);
        Assert.Equal(45615.94m, stored.TotalAuthorizedDisbursement);
    }

    [Fact]
    public async Task ConsultantFrnStatusImport_IsIdempotent_OnRerun()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number,form_471_frn_status_name
            APP001,2024,EPC1,FRN1,Funded
            """;

        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            var content = callCount % 2 == 1 ? "" : csv;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/csv"),
            });
        });

        var service = BuildConsultantFrnStatusService(handler);

        var result1 = await service.RunAsync("https://example.com/mihb-jfex.csv");
        var result2 = await service.RunAsync("https://example.com/mihb-jfex.csv");

        Assert.Equal(1, result1.RecordsInserted);
        Assert.Equal(0, result2.RecordsInserted);
        Assert.Equal(1, result2.RecordsUpdated);
        Assert.Equal(1, _db.ConsultantFrnStatuses.Count());
    }

    [Fact]
    public async Task ConsultantFrnStatusImport_AbortsEarly_WhenUpstreamIsUnavailable()
    {
        var handler = new StubHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var service = BuildConsultantFrnStatusService(handler);
        var result = await service.RunAsync("https://example.com/mihb-jfex.csv");

        Assert.Equal(ImportJobStatus.Failed, result.Status);
        Assert.Contains("unavailable", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _db.ConsultantFrnStatuses.Count());
    }

    [Fact]
    public async Task ConsultantFrnStatusImport_NullDecimalsAndDates_StoredAsNull()
    {
        const string csv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number,dis_pct,funding_commitment_request,total_authorized_disbursement,service_start_date,fcdl_letter_date
            APP001,2024,EPC1,FRN1,,,,,
            """;

        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            var content = callCount == 1 ? "" : csv;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/csv"),
            });
        });

        var service = BuildConsultantFrnStatusService(handler);
        await service.RunAsync("https://example.com/mihb-jfex.csv");

        var stored = _db.ConsultantFrnStatuses.Single();
        Assert.Null(stored.DiscountPct);
        Assert.Null(stored.FundingCommitmentRequest);
        Assert.Null(stored.TotalAuthorizedDisbursement);
        Assert.Null(stored.ServiceStartDate);
        Assert.Null(stored.FcdlLetterDate);
    }

    [Fact]
    public async Task ConsultantApplications_And_FrnStatuses_StoredIndependently()
    {
        // Verify the two tables are independent — no joins happen during ETL
        const string appCsv = """
            application_number,funding_year,cnslt_epc_organization_id
            APP001,2024,EPC1
            """;

        const string frnCsv = """
            application_number,funding_year,cnslt_epc_organization_id,funding_request_number
            APP001,2024,EPC1,FRN1
            APP001,2024,EPC1,FRN2
            """;

        var appCallCount = 0;
        var appHandler = new StubHttpHandler(_ =>
        {
            appCallCount++;
            var content = appCallCount == 1 ? "" : appCsv;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/csv"),
            });
        });

        var frnCallCount = 0;
        var frnHandler = new StubHttpHandler(_ =>
        {
            frnCallCount++;
            var content = frnCallCount == 1 ? "" : frnCsv;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/csv"),
            });
        });

        var appService = BuildConsultantApplicationService(appHandler);
        var frnService = BuildConsultantFrnStatusService(frnHandler);

        await appService.RunAsync("https://example.com/apps.csv");
        await frnService.RunAsync("https://example.com/frns.csv");

        // Tables are independent — no join, no fan-out
        Assert.Equal(1, _db.ConsultantApplications.Count());
        Assert.Equal(2, _db.ConsultantFrnStatuses.Count());
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private ConsultantApplicationImportService BuildConsultantApplicationService(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler);
        var csvClient = new UsacCsvClient(http, NullLogger<UsacCsvClient>.Instance);
        var parser = new ConsultantApplicationCsvParser();
        var repo = new ConsultantApplicationRepository(_db);
        return new ConsultantApplicationImportService(
            _db, csvClient, parser, repo,
            NullLogger<ConsultantApplicationImportService>.Instance);
    }

    private ConsultantFrnStatusImportService BuildConsultantFrnStatusService(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler);
        var csvClient = new UsacCsvClient(http, NullLogger<UsacCsvClient>.Instance);
        var parser = new ConsultantFrnStatusCsvParser();
        var repo = new ConsultantFrnStatusRepository(_db);
        return new ConsultantFrnStatusImportService(
            _db, csvClient, parser, repo,
            NullLogger<ConsultantFrnStatusImportService>.Instance);
    }
}

/// <summary>
/// Minimal stub HTTP handler for testing — avoids Moq dependency.
/// </summary>
file sealed class StubHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
}
