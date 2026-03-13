using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests idempotent upsert and analytics queries for Form471Repository
/// against an in-memory SQLite database.
/// </summary>
public class Form471RepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly Form471Repository _repo;

    public Form471RepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new Form471Repository(_db);
    }

    // --- Helpers ---

    private static Form471Application App(
        string appNumber, int year,
        string? category = "Category 1",
        string? serviceType = "Internet Access",
        decimal? amount = 10_000m,
        string? state = "TX") =>
    new()
    {
        ApplicationNumber = appNumber,
        FundingYear = year,
        RawSourceKey = $"{appNumber}-{year}",
        CategoryOfService = category,
        ServiceType = serviceType,
        RequestedAmount = amount,
        ApplicantState = state,
        ImportedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };

    // --- Upsert / idempotency ---

    [Fact]
    public async Task UpsertBatch_NewRecords_InsertsAll()
    {
        var (inserted, updated) = await _repo.UpsertBatchAsync(
            [App("APP-001", 2024), App("APP-002", 2024)]);

        Assert.Equal(2, inserted);
        Assert.Equal(0, updated);
        Assert.Equal(2, _db.Form471Applications.Count());
    }

    [Fact]
    public async Task UpsertBatch_SameKeyTwice_DoesNotDuplicate()
    {
        await _repo.UpsertBatchAsync([App("APP-001", 2024, amount: 1_000m)]);

        var (inserted, updated) = await _repo.UpsertBatchAsync(
            [App("APP-001", 2024, amount: 9_999m)]);

        Assert.Equal(0, inserted);
        Assert.Equal(1, updated);
        Assert.Equal(1, _db.Form471Applications.Count());
        Assert.Equal(9_999m, _db.Form471Applications.Single().RequestedAmount);
    }

    [Fact]
    public async Task UpsertBatch_SameAppNumberDifferentYear_BothInserted()
    {
        // Different funding years → different RawSourceKeys → two distinct rows
        var (inserted, updated) = await _repo.UpsertBatchAsync(
            [App("APP-001", 2023), App("APP-001", 2024)]);

        Assert.Equal(2, inserted);
        Assert.Equal(0, updated);
    }

    [Fact]
    public async Task UpsertBatch_DuplicateKeysWithinBatch_LastWins()
    {
        var batch = new[]
        {
            App("APP-001", 2024, amount: 100m),
            App("APP-001", 2024, amount: 999m), // same key — wins
        };

        var (inserted, updated) = await _repo.UpsertBatchAsync(batch);

        Assert.Equal(1, inserted);
        Assert.Equal(0, updated);
        Assert.Equal(999m, _db.Form471Applications.Single().RequestedAmount);
    }

    // --- Analytics: demand by year ---

    [Fact]
    public async Task GetDemandByYear_GroupsAndSumsCorrectly()
    {
        await _repo.UpsertBatchAsync([
            App("APP-001", 2023, amount: 10_000m),
            App("APP-002", 2023, amount: 5_000m),
            App("APP-003", 2024, amount: 20_000m),
        ]);

        var results = await _repo.GetDemandByYearAsync();

        Assert.Equal(2, results.Count);
        // Ordered newest first
        Assert.Equal(2024, results[0].FundingYear);
        Assert.Equal(1, results[0].ApplicationCount);
        Assert.Equal(20_000m, results[0].TotalRequested);

        Assert.Equal(2023, results[1].FundingYear);
        Assert.Equal(2, results[1].ApplicationCount);
        Assert.Equal(15_000m, results[1].TotalRequested);
    }

    // --- Analytics: category demand ---

    [Fact]
    public async Task GetDemandByCategory_SplitsCategoryOneAndTwo()
    {
        await _repo.UpsertBatchAsync([
            App("APP-001", 2024, category: "Category 1", amount: 30_000m),
            App("APP-002", 2024, category: "Category 1", amount: 20_000m),
            App("APP-003", 2024, category: "Category 2", amount: 5_000m),
        ]);

        var results = await _repo.GetDemandByCategoryAsync();

        Assert.Equal(2, results.Count);
        var cat1 = results.Single(r => r.Category == "Category 1");
        Assert.Equal(2, cat1.ApplicationCount);
        Assert.Equal(50_000m, cat1.TotalRequested);

        var cat2 = results.Single(r => r.Category == "Category 2");
        Assert.Equal(5_000m, cat2.TotalRequested);
    }

    [Fact]
    public async Task GetDemandByCategory_NullCategoryRows_AreExcluded()
    {
        await _repo.UpsertBatchAsync([
            App("APP-001", 2024, category: null, amount: 99_999m), // excluded
            App("APP-002", 2024, category: "Category 1", amount: 1_000m),
        ]);

        var results = await _repo.GetDemandByCategoryAsync();

        Assert.Single(results);
        Assert.Equal("Category 1", results[0].Category);
    }

    // --- Analytics: top service types ---

    [Fact]
    public async Task GetTopServiceTypes_OrdersByTotalDescending()
    {
        await _repo.UpsertBatchAsync([
            App("APP-001", 2024, serviceType: "Fiber", amount: 100_000m),
            App("APP-002", 2024, serviceType: "Wi-Fi",  amount: 10_000m),
            App("APP-003", 2024, serviceType: "Fiber",  amount: 50_000m),
        ]);

        var results = await _repo.GetTopServiceTypesAsync(topN: 10);

        Assert.Equal(2, results.Count);
        Assert.Equal("Fiber", results[0].ServiceType);
        Assert.Equal(150_000m, results[0].TotalRequested);
        Assert.Equal(2, results[0].ApplicationCount);
    }

    [Fact]
    public async Task GetTopServiceTypes_RespectsTopNLimit()
    {
        await _repo.UpsertBatchAsync([
            App("APP-001", 2024, serviceType: "A", amount: 3_000m),
            App("APP-002", 2024, serviceType: "B", amount: 2_000m),
            App("APP-003", 2024, serviceType: "C", amount: 1_000m),
        ]);

        var results = await _repo.GetTopServiceTypesAsync(topN: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal("A", results[0].ServiceType);
        Assert.Equal("B", results[1].ServiceType);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
