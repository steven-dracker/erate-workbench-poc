using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using ErateWorkbench.Infrastructure.Csv;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests for retry classification, UsacCsvClient retry behavior,
/// and end-to-end FundingCommitmentImportService pipeline.
/// </summary>
public class FundingCommitmentImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public FundingCommitmentImportServiceTests()
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

    // ── IsTransientError unit tests ─────────────────────────────────────────

    [Fact]
    public void IsTransientError_ReturnsTrueForIOException() =>
        Assert.True(FundingCommitmentImportService.IsTransientError(new IOException("stream reset")));

    [Fact]
    public void IsTransientError_ReturnsTrueForHttpRequestException() =>
        Assert.True(FundingCommitmentImportService.IsTransientError(new HttpRequestException("connection refused")));

    [Fact]
    public void IsTransientError_ReturnsTrueForSocketException() =>
        Assert.True(FundingCommitmentImportService.IsTransientError(new SocketException()));

    [Fact]
    public void IsTransientError_ReturnsTrueWhenInnerExceptionIsIOException() =>
        Assert.True(FundingCommitmentImportService.IsTransientError(
            new InvalidOperationException("outer", new IOException("EOF inner"))));

    [Fact]
    public void IsTransientError_ReturnsFalseForOperationCanceledException() =>
        Assert.False(FundingCommitmentImportService.IsTransientError(new OperationCanceledException()));

    [Fact]
    public void IsTransientError_ReturnsFalseForUnrelatedExceptions() =>
        Assert.False(FundingCommitmentImportService.IsTransientError(new ArgumentNullException("x")));

    // ── UsacCsvClient retry tests ───────────────────────────────────────────

    [Fact]
    public async Task UsacCsvClient_RetriesOnHttpRequestException_AndSucceeds()
    {
        var callCount = 0;
        var delayCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            if (callCount == 1)
                throw new HttpRequestException("transient network error");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain")
            });
        });

        var client = new UsacCsvClient(new HttpClient(handler), NullLogger<UsacCsvClient>.Instance,
            (_, _) => { delayCount++; return Task.CompletedTask; });
        using var stream = await client.DownloadStreamAsync("https://test.example.com/data.csv");
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        Assert.Equal("ok", content);
        Assert.Equal(2, callCount);   // initial attempt failed; retry succeeded
        Assert.Equal(1, delayCount);  // one backoff wait between attempts
    }

    // ── FundingCommitmentImportService integration tests ────────────────────

    private FundingCommitmentImportService BuildService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var csvClient = new UsacCsvClient(httpClient, NullLogger<UsacCsvClient>.Instance,
            (_, _) => Task.CompletedTask); // no-op delay — tests run in milliseconds
        var parser = new FundingCommitmentCsvParser();
        var repo = new FundingCommitmentRepository(_db);
        return new FundingCommitmentImportService(
            _db, csvClient, parser, repo,
            NullLogger<FundingCommitmentImportService>.Instance);
    }

    // Socrata-style empty page: header row only, no data — signals end of dataset.
    private const string EmptyPage = "funding_request_number,funding_year\n";

    private static StringContent CsvContent(string csv) =>
        new(csv, Encoding.UTF8, "text/csv");

    // Returns a successful 200 response with no body — used for pre-flight probe calls.
    private static HttpResponseMessage ProbeOk() =>
        new(HttpStatusCode.OK);

    [Fact]
    public async Task RunAsync_Succeeds_WhenImportCompletesNormally()
    {
        const string page1 = """
            funding_request_number,funding_year,organization_name
            FRN1000001,2024,Springfield Elementary
            FRN1000002,2024,Shelbyville Middle
            """;

        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            if (callCount == 1) return Task.FromResult(ProbeOk()); // pre-flight probe
            var body = callCount == 2 ? page1 : EmptyPage;
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
        Assert.Equal(3, callCount); // probe + page 1 + empty page terminator
    }

    [Fact]
    public async Task RunAsync_IsIdempotent_ReImportUpdatesExistingRows()
    {
        const string page = """
            funding_request_number,funding_year,organization_name
            FRN2000001,2024,Lincoln High
            FRN2000002,2024,Washington Elementary
            """;

        // Each run: probe (200) + page + empty
        var responses = new Queue<string>([page, EmptyPage, page, EmptyPage]);
        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            if (callCount % 3 == 1) return Task.FromResult(ProbeOk()); // probe on calls 1 and 4
            var body = responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CsvContent(body)
            });
        });

        var service = BuildService(handler);

        var first = await service.RunAsync("https://test.usac.org/resource/test.csv", pageSize: 100);
        var second = await service.RunAsync("https://test.usac.org/resource/test.csv", pageSize: 100);

        // First run: all rows are new.
        Assert.Equal(2, first.RecordsInserted);
        Assert.Equal(0, first.RecordsUpdated);

        // Second run with same keys: all rows are updates, no duplicates.
        Assert.Equal(0, second.RecordsInserted);
        Assert.Equal(2, second.RecordsUpdated);
        Assert.Equal(2, second.RecordsProcessed);

        // Database has exactly 2 rows — no duplicates.
        Assert.Equal(2, _db.FundingCommitments.Count());
    }

    [Fact]
    public async Task RunAsync_MarksJobFailed_WhenNonTransientErrorOccurs()
    {
        // Simulate a Socrata JSON error payload returned with HTTP 200.
        // The service's first-byte sniff detects '{' and throws InvalidOperationException,
        // which is non-transient — no retries fire, and the job is immediately failed.
        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            if (callCount == 1) return Task.FromResult(ProbeOk()); // pre-flight probe
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CsvContent("{\"error\":true,\"message\":\"query timed out\"}")
            });
        });

        var result = await BuildService(handler)
            .RunAsync("https://test.usac.org/resource/test.csv", pageSize: 100);

        Assert.Equal(ImportJobStatus.Failed, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_AbortsEarly_WhenUpstreamIsUnavailable()
    {
        // Probe returns 503 — import should abort before any paging begins.
        // CheckAvailabilityAsync retries once on 503, so expect 2 HTTP calls total.
        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        var result = await BuildService(handler)
            .RunAsync("https://test.usac.org/resource/test.csv", pageSize: 100);

        Assert.Equal(ImportJobStatus.Failed, result.Status);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("unavailable", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.RecordsProcessed);
        Assert.True(callCount <= 2, $"Expected at most 2 probe calls, got {callCount}");

        // A job record should exist in the DB with Failed status.
        var job = _db.ImportJobs.SingleOrDefault();
        Assert.NotNull(job);
        Assert.Equal(ImportJobStatus.Failed, job.Status);
    }

    // ── URL construction tests (CC-ERATE-000007) ────────────────────────────

    [Fact]
    public async Task RunAsync_ConstructedPageUrls_ContainLimitAndOffset()
    {
        // Capture every URL the service fetches from Socrata.
        var requestedUrls = new List<string>();
        var callCount = 0;
        var handler = new StubHttpHandler(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            callCount++;
            if (callCount == 1) return Task.FromResult(ProbeOk()); // pre-flight probe
            var body = callCount == 2
                ? "funding_request_number,funding_year\nFRN1,2024\n"
                : EmptyPage;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CsvContent(body)
            });
        });

        await BuildService(handler).RunAsync("https://test.usac.org/resource/test.csv", pageSize: 500);

        // Skip the probe URL (which has $limit=1 but no $offset); check only page URLs.
        var pageUrls = requestedUrls.Skip(1).ToList();
        Assert.True(pageUrls.Count >= 2, "Expected at least a data page and an empty-page terminator.");
        foreach (var url in pageUrls)
        {
            Assert.Contains("$limit=", url);
            Assert.Contains("$offset=", url);
        }
    }

    [Fact]
    public async Task RunAsync_ConstructedPageUrls_DoNotContainFundingYearFilter()
    {
        // Documents that funding commitment imports are always full-dataset.
        // The import service never appends a year filter to its Socrata requests;
        // year-scoped processing begins at the summary rebuild stage.
        var requestedUrls = new List<string>();
        var callCount = 0;
        var handler = new StubHttpHandler(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            callCount++;
            if (callCount == 1) return Task.FromResult(ProbeOk()); // pre-flight probe
            var body = callCount == 2
                ? "funding_request_number,funding_year\nFRN1,2024\n"
                : EmptyPage;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CsvContent(body)
            });
        });

        await BuildService(handler).RunAsync("https://test.usac.org/resource/test.csv", pageSize: 500);

        Assert.True(requestedUrls.Count >= 1);
        // Skip probe URL; page URLs must not contain a year filter.
        foreach (var url in requestedUrls.Skip(1))
            Assert.DoesNotContain("funding_year=", url);
    }
}

/// <summary>
/// Minimal delegating handler backed by a Func — no Moq dependency required.
/// </summary>
file sealed class StubHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
}
