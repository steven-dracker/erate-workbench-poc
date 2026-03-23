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
/// Integration tests for EntityImportService using an in-memory SQLite database
/// and a controllable HttpMessageHandler.
/// </summary>
public class EntityImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public EntityImportServiceTests()
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

    private EntityImportService BuildService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var csvClient = new UsacCsvClient(httpClient, NullLogger<UsacCsvClient>.Instance,
            (_, _) => Task.CompletedTask); // no-op delay — tests run in milliseconds
        var parser = new EntityCsvParser();
        var repo = new EntityRepository(_db);
        return new EntityImportService(
            _db, csvClient, parser, repo,
            NullLogger<EntityImportService>.Instance);
    }

    private const string EmptyPage =
        "ros_entity_number,ros_entity_name,ros_entity_type,ros_physical_state\n";

    private static StringContent CsvContent(string csv) =>
        new(csv, Encoding.UTF8, "text/csv");

    private static HttpResponseMessage ProbeOk() =>
        new(HttpStatusCode.OK);

    [Fact]
    public async Task RunAsync_Succeeds_WhenImportCompletesNormally()
    {
        const string page1 = """
            ros_entity_number,ros_entity_name,ros_entity_type,ros_urban_rural_status,ros_physical_state,ros_number_of_full_time_students,ros_total_number_of_part_time_students,ros_number_of_nslp_students
            100001,Springfield Elementary,School,Rural,IL,450,0,380
            100002,Shelbyville Library,Library,Urban,IL,0,0,0
            """;

        var callCount = 0;
        var handler = new EntityStubHandler(_ =>
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
        Assert.Equal(3, callCount); // probe + page 1 + empty page

        // Verify DB state
        Assert.Equal(2, _db.Entities.Count());
        var school = _db.Entities.Single(e => e.EntityNumber == "100001");
        Assert.Equal("Springfield Elementary", school.EntityName);
        Assert.Equal("IL", school.State);
        Assert.Equal(450, school.FullTimeStudentCount);
        Assert.Equal(380, school.NslpStudentCount);
    }

    [Fact]
    public async Task RunAsync_IsIdempotent_ReImportUpdatesExistingRows()
    {
        const string page = """
            ros_entity_number,ros_entity_name,ros_entity_type,ros_physical_state
            200001,Lincoln High,School,TX
            200002,Washington Elementary,School,TX
            """;

        // Each run: probe (200) + page + empty
        var responses = new Queue<string>([page, EmptyPage, page, EmptyPage]);
        var callCount = 0;
        var handler = new EntityStubHandler(_ =>
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

        Assert.Equal(2, first.RecordsInserted);
        Assert.Equal(0, first.RecordsUpdated);

        Assert.Equal(0, second.RecordsInserted);
        Assert.Equal(2, second.RecordsUpdated);
        Assert.Equal(2, second.RecordsProcessed);

        // No duplicates in DB
        Assert.Equal(2, _db.Entities.Count());
    }

    [Fact]
    public async Task RunAsync_DeduplicatesWithinImport_WhenEntityAppearsOnMultiplePages()
    {
        // Simulates the real-world case: the same entity appears in many commitment rows.
        // Each page will have 2 rows for entity 300001; after import only 1 DB record exists.
        const string page1 = """
            ros_entity_number,ros_entity_name,ros_entity_type,ros_physical_state
            300001,Elm Street School,School,CA
            300001,Elm Street School,School,CA
            """;

        var responses = new Queue<string>([page1, EmptyPage]);
        var callCount = 0;
        var handler = new EntityStubHandler(_ =>
        {
            callCount++;
            if (callCount == 1) return Task.FromResult(ProbeOk()); // pre-flight probe
            var body = responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CsvContent(body)
            });
        });

        await BuildService(handler)
            .RunAsync("https://test.usac.org/resource/test.csv", pageSize: 100);

        Assert.Equal(1, _db.Entities.Count());
    }

    [Fact]
    public async Task RunAsync_MarksJobFailed_WhenNonTransientErrorOccurs()
    {
        // JSON payload with HTTP 200 triggers the non-CSV detection — non-transient,
        // no retries, job is failed immediately.
        var callCount = 0;
        var handler = new EntityStubHandler(_ =>
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
        // Probe returns 503 — import should abort before paging begins.
        // CheckAvailabilityAsync retries once on 503, so expect 2 HTTP calls total.
        var callCount = 0;
        var handler = new EntityStubHandler(_ =>
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
    }
}

file sealed class EntityStubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
}
