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
/// Integration tests for DisbursementImportService using an in-memory SQLite database
/// and a controllable HttpMessageHandler.
/// </summary>
public class DisbursementImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public DisbursementImportServiceTests()
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

    private DisbursementImportService BuildService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var csvClient = new UsacCsvClient(httpClient, NullLogger<UsacCsvClient>.Instance);
        var parser = new DisbursementCsvParser();
        var repo = new DisbursementRepository(_db);
        return new DisbursementImportService(
            _db, csvClient, parser, repo,
            NullLogger<DisbursementImportService>.Instance);
    }

    private const string EmptyPage =
        "funding_request_number,invoice_id,inv_line_num,funding_year\n";

    private static StringContent CsvContent(string csv) =>
        new(csv, Encoding.UTF8, "text/csv");

    [Fact]
    public async Task RunAsync_Succeeds_WhenImportCompletesNormally()
    {
        const string page1 = """
            funding_request_number,invoice_id,inv_line_num,invoice_type,billed_entity_number,billed_entity_name,inv_service_provider_id_number_spin,funding_year,chosen_category_of_service,requested_inv_line_amt,approved_inv_line_amt
            2390001234,INV20240001,1,BEAR,100001,Springfield Elementary,143002468,2024,Category 1,5000.00,4800.00
            2390001235,INV20240002,1,SPI,100002,Shelbyville Library,143002469,2024,Category 2,2000.00,1900.00
            """;

        var callCount = 0;
        var handler = new DisbursementStubHandler(_ =>
        {
            callCount++;
            var body = callCount == 1 ? page1 : EmptyPage;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CsvContent(body)
            });
        });

        var result = await BuildService(handler)
            .RunAsync("https://test.usac.org/resource/test.csv", pageSize: 100);

        Assert.Equal(ImportJobStatus.Succeeded, result.Status);
        Assert.Equal(2, result.RecordsProcessed);
        Assert.Equal(2, result.RecordsInserted);
        Assert.Equal(0, result.RecordsUpdated);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(2, callCount);

        // Verify DB state
        Assert.Equal(2, _db.Disbursements.Count());
        var bear = _db.Disbursements.Single(d => d.InvoiceId == "INV20240001");
        Assert.Equal("INV20240001-1", bear.RawSourceKey);
        Assert.Equal("2390001234", bear.FundingRequestNumber);
        Assert.Equal("BEAR", bear.InvoiceType);
        Assert.Equal("Springfield Elementary", bear.ApplicantEntityName);
        Assert.Equal(5000.00m, bear.RequestedAmount);
        Assert.Equal(4800.00m, bear.ApprovedAmount);
    }

    [Fact]
    public async Task RunAsync_IsIdempotent_ReImportUpdatesExistingRows()
    {
        const string page = """
            funding_request_number,invoice_id,inv_line_num,invoice_type,funding_year,approved_inv_line_amt
            2390001234,INV20240001,1,BEAR,2024,4800.00
            2390001235,INV20240002,1,SPI,2024,1900.00
            """;

        var responses = new Queue<string>([page, EmptyPage, page, EmptyPage]);
        var handler = new DisbursementStubHandler(_ =>
        {
            var body = responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CsvContent(body)
            });
        });

        var service = BuildService(handler);

        var first = await service.RunAsync("https://test.usac.org/resource/test.csv", pageSize: 100);
        var second = await service.RunAsync("https://test.usac.org/resource/test.csv", pageSize: 100);

        Assert.Equal(2, first.RecordsInserted);
        Assert.Equal(0, first.RecordsUpdated);

        Assert.Equal(0, second.RecordsInserted);
        Assert.Equal(2, second.RecordsUpdated);
        Assert.Equal(2, second.RecordsProcessed);

        // No duplicates in DB
        Assert.Equal(2, _db.Disbursements.Count());
    }

    [Fact]
    public async Task RunAsync_DeduplicatesWithinPage_WhenSameKeyAppearsMultipleTimes()
    {
        // Two rows with the same invoice_id + line_num should result in 1 DB record.
        const string page1 = """
            funding_request_number,invoice_id,inv_line_num,invoice_type,funding_year,approved_inv_line_amt
            2390001234,INV20240001,1,BEAR,2024,4800.00
            2390001234,INV20240001,1,BEAR,2024,4900.00
            """;

        var responses = new Queue<string>([page1, EmptyPage]);
        var handler = new DisbursementStubHandler(_ =>
        {
            var body = responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CsvContent(body)
            });
        });

        await BuildService(handler)
            .RunAsync("https://test.usac.org/resource/test.csv", pageSize: 100);

        Assert.Equal(1, _db.Disbursements.Count());
        // Last occurrence wins (4900.00)
        Assert.Equal(4900.00m, _db.Disbursements.Single().ApprovedAmount);
    }

    [Fact]
    public async Task RunAsync_MarksJobFailed_WhenNonTransientErrorOccurs()
    {
        // JSON payload with HTTP 200 triggers the non-CSV detection — non-transient,
        // no retries, job is failed immediately.
        var handler = new DisbursementStubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CsvContent("{\"error\":true,\"message\":\"query timed out\"}")
            }));

        var result = await BuildService(handler)
            .RunAsync("https://test.usac.org/resource/test.csv", pageSize: 100);

        Assert.Equal(ImportJobStatus.Failed, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    // ── URL construction tests (CC-ERATE-000007) ────────────────────────────

    [Fact]
    public async Task RunAsync_ConstructedPageUrls_ContainLimitAndOffset()
    {
        // Capture every URL the service fetches from Socrata.
        var requestedUrls = new List<string>();
        var callCount = 0;
        var handler = new DisbursementStubHandler(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            callCount++;
            var body = callCount == 1
                ? "funding_request_number,invoice_id,inv_line_num,funding_year\nFRN1,INV1,1,2024\n"
                : EmptyPage;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CsvContent(body)
            });
        });

        await BuildService(handler).RunAsync("https://test.usac.org/resource/test.csv", pageSize: 500);

        Assert.True(requestedUrls.Count >= 2, "Expected at least a data page and an empty-page terminator.");
        foreach (var url in requestedUrls)
        {
            Assert.Contains("$limit=", url);
            Assert.Contains("$offset=", url);
        }
    }

    [Fact]
    public async Task RunAsync_ConstructedPageUrls_DoNotContainFundingYearFilter()
    {
        // Documents that disbursement imports are always full-dataset.
        // The import service never appends a year filter to its Socrata requests;
        // year-scoped processing begins at the summary rebuild stage.
        var requestedUrls = new List<string>();
        var callCount = 0;
        var handler = new DisbursementStubHandler(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            callCount++;
            var body = callCount == 1
                ? "funding_request_number,invoice_id,inv_line_num,funding_year\nFRN1,INV1,1,2024\n"
                : EmptyPage;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CsvContent(body)
            });
        });

        await BuildService(handler).RunAsync("https://test.usac.org/resource/test.csv", pageSize: 500);

        Assert.True(requestedUrls.Count >= 1);
        foreach (var url in requestedUrls)
            Assert.DoesNotContain("funding_year=", url);
    }
}

file sealed class DisbursementStubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
}
