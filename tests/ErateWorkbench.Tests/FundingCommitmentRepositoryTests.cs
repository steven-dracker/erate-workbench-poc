using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests use an in-memory SQLite database to verify idempotent upsert behaviour,
/// insert/update counting, and batch deduplication.
/// </summary>
public class FundingCommitmentRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly FundingCommitmentRepository _repo;

    public FundingCommitmentRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new FundingCommitmentRepository(_db);
    }

    // --- Helpers ---

    private static FundingCommitment Commitment(
        string frn, int? lineItem = null,
        string status = "Funded", decimal amount = 1000m) =>
    new()
    {
        FundingRequestNumber = frn,
        FrnLineItemNumber = lineItem,
        RawSourceKey = lineItem.HasValue ? $"{frn}-{lineItem}" : frn,
        FundingYear = 2024,
        CommitmentStatus = status,
        CommittedAmount = amount,
        ImportedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };

    // --- Insert behaviour ---

    [Fact]
    public async Task UpsertBatch_NewRecords_ReportsCorrectInsertCount()
    {
        var batch = new[] { Commitment("FRN001", 1), Commitment("FRN002", 1) };

        var (inserted, updated) = await _repo.UpsertBatchAsync(batch);

        Assert.Equal(2, inserted);
        Assert.Equal(0, updated);
        Assert.Equal(2, _db.FundingCommitments.Count());
    }

    // --- Idempotency ---

    [Fact]
    public async Task UpsertBatch_SameRecordTwice_DoesNotCreateDuplicates()
    {
        var commitment = Commitment("FRN001", 1);
        await _repo.UpsertBatchAsync([commitment]);

        // Run a second time with the same key
        var (inserted, updated) = await _repo.UpsertBatchAsync([commitment]);

        Assert.Equal(0, inserted);
        Assert.Equal(1, updated);
        Assert.Equal(1, _db.FundingCommitments.Count());
    }

    [Fact]
    public async Task UpsertBatch_UpdatesChangedFields_OnSecondRun()
    {
        await _repo.UpsertBatchAsync([Commitment("FRN001", 1, status: "Funded", amount: 1000m)]);

        await _repo.UpsertBatchAsync([Commitment("FRN001", 1, status: "Committed", amount: 1500m)]);

        var record = _db.FundingCommitments.Single();
        Assert.Equal("Committed", record.CommitmentStatus);
        Assert.Equal(1500m, record.CommittedAmount);
    }

    // --- Batch deduplication ---

    [Fact]
    public async Task UpsertBatch_DuplicateKeysWithinBatch_LastOneWins()
    {
        var batch = new[]
        {
            Commitment("FRN001", 1, status: "Funded", amount: 100m),
            Commitment("FRN001", 1, status: "Committed", amount: 999m), // same key, wins
        };

        var (inserted, updated) = await _repo.UpsertBatchAsync(batch);

        // Batch deduplication collapses to one insert
        Assert.Equal(1, inserted);
        Assert.Equal(0, updated);
        Assert.Equal(999m, _db.FundingCommitments.Single().CommittedAmount);
    }

    // --- Mixed insert + update ---

    [Fact]
    public async Task UpsertBatch_MixedNewAndExisting_CountsBothCorrectly()
    {
        await _repo.UpsertBatchAsync([Commitment("FRN001", 1)]);

        var batch = new[]
        {
            Commitment("FRN001", 1),   // existing → update
            Commitment("FRN002", 1),   // new → insert
            Commitment("FRN003", 1),   // new → insert
        };

        var (inserted, updated) = await _repo.UpsertBatchAsync(batch);

        Assert.Equal(2, inserted);
        Assert.Equal(1, updated);
        Assert.Equal(3, _db.FundingCommitments.Count());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
