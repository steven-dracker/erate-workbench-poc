using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests idempotent upsert, batch deduplication, and SPIN-based analytics joins
/// against an in-memory SQLite database.
/// </summary>
public class ServiceProviderRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ServiceProviderRepository _repo;

    public ServiceProviderRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ServiceProviderRepository(_db);
    }

    // --- Helpers ---

    private static ServiceProvider Provider(string spin, string name = "Test Provider", string? state = null) =>
        new()
        {
            Spin = spin,
            ProviderName = name,
            State = state,
            RawSourceKey = spin,
            ImportedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

    private static FundingCommitment Commitment(
        string rawKey, string? spin, string? entityNumber,
        decimal amount = 1000m) =>
        new()
        {
            FundingRequestNumber = rawKey,
            RawSourceKey = rawKey,
            ServiceProviderSpin = spin,
            ApplicantEntityNumber = entityNumber,
            FundingYear = 2024,
            CommittedAmount = amount,
            ImportedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

    private static EpcEntity Entity(string number, string state) =>
        new()
        {
            EntityNumber = number,
            EntityName = "Test School",
            PhysicalState = state,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    // --- Insert / duplicate handling ---

    [Fact]
    public async Task UpsertBatch_NewProviders_InsertsCorrectly()
    {
        var (inserted, updated) = await _repo.UpsertBatchAsync(
            [Provider("143000001"), Provider("143000002")]);

        Assert.Equal(2, inserted);
        Assert.Equal(0, updated);
        Assert.Equal(2, _db.ServiceProviders.Count());
    }

    [Fact]
    public async Task UpsertBatch_SameSpinTwice_DoesNotDuplicate()
    {
        await _repo.UpsertBatchAsync([Provider("143000001", "Original Name")]);

        var (inserted, updated) = await _repo.UpsertBatchAsync(
            [Provider("143000001", "Updated Name")]);

        Assert.Equal(0, inserted);
        Assert.Equal(1, updated);
        Assert.Equal(1, _db.ServiceProviders.Count());
        Assert.Equal("Updated Name", _db.ServiceProviders.Single().ProviderName);
    }

    [Fact]
    public async Task UpsertBatch_DuplicateSpinsWithinBatch_LastWins()
    {
        var batch = new[]
        {
            Provider("143000001", "First"),
            Provider("143000001", "Second"), // same SPIN, wins
        };

        var (inserted, updated) = await _repo.UpsertBatchAsync(batch);

        Assert.Equal(1, inserted);
        Assert.Equal(0, updated);
        Assert.Equal("Second", _db.ServiceProviders.Single().ProviderName);
    }

    // --- SPIN-based linking: top-service-providers ---

    [Fact]
    public async Task GetTopByCommittedAmount_JoinsCorrectlyToFundingCommitments()
    {
        await _repo.UpsertBatchAsync([
            Provider("SPIN001", "Big Telecom"),
            Provider("SPIN002", "Small ISP"),
        ]);

        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "SPIN001", null, 50_000m),
            Commitment("FRN002", "SPIN001", null, 30_000m),
            Commitment("FRN003", "SPIN002", null, 5_000m)
        );
        await _db.SaveChangesAsync();

        var results = await _repo.GetTopByCommittedAmountAsync(topN: 10);

        Assert.Equal(2, results.Count);
        Assert.Equal("SPIN001", results[0].Spin);
        Assert.Equal("Big Telecom", results[0].ProviderName);
        Assert.Equal(2, results[0].CommitmentCount);
        Assert.Equal(80_000m, results[0].TotalCommitted);

        Assert.Equal("SPIN002", results[1].Spin);
        Assert.Equal(5_000m, results[1].TotalCommitted);
    }

    [Fact]
    public async Task GetTopByCommittedAmount_ProviderNotInServiceProviderTable_FallsBackToSpin()
    {
        // Commitment references a SPIN not yet imported into ServiceProviders
        _db.FundingCommitments.Add(Commitment("FRN001", "SPIN999", null, 10_000m));
        await _db.SaveChangesAsync();

        var results = await _repo.GetTopByCommittedAmountAsync(topN: 10);

        Assert.Single(results);
        // ProviderName falls back to the SPIN value itself when no ServiceProvider row exists
        Assert.Equal("SPIN999", results[0].ProviderName);
    }

    [Fact]
    public async Task GetTopByCommittedAmount_CommitmentsWithNullSpin_AreExcluded()
    {
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", null, null, 99_999m),  // null SPIN — excluded
            Commitment("FRN002", "SPIN001", null, 1_000m)
        );
        await _db.SaveChangesAsync();

        var results = await _repo.GetTopByCommittedAmountAsync(topN: 10);

        Assert.Single(results);
        Assert.Equal("SPIN001", results[0].Spin);
    }

    // --- SPIN-based linking: providers-by-state (via FundingCommitments → EpcEntities) ---

    [Fact]
    public async Task GetProvidersByApplicantState_JoinsCorrectlyThroughCommitmentsAndEntities()
    {
        _db.EpcEntities.AddRange(
            Entity("100001", "IL"),
            Entity("100002", "IL"),
            Entity("100003", "NY")
        );
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "SPIN001", "100001", 1000m),  // IL
            Commitment("FRN002", "SPIN002", "100001", 2000m),  // IL (different provider)
            Commitment("FRN003", "SPIN001", "100002", 3000m),  // IL (same provider as FRN001)
            Commitment("FRN004", "SPIN001", "100003", 4000m)   // NY
        );
        await _db.SaveChangesAsync();

        var results = await _repo.GetProvidersByApplicantStateAsync();

        var il = results.SingleOrDefault(r => r.State == "IL");
        Assert.NotNull(il);
        Assert.Equal(2, il.ProviderCount);   // SPIN001 and SPIN002
        Assert.Equal(3, il.CommitmentCount); // FRN001, FRN002, FRN003

        var ny = results.SingleOrDefault(r => r.State == "NY");
        Assert.NotNull(ny);
        Assert.Equal(1, ny.ProviderCount);
        Assert.Equal(1, ny.CommitmentCount);
    }

    [Fact]
    public async Task GetProvidersByApplicantState_CommitmentsWithNoMatchingEntity_AreExcluded()
    {
        // ApplicantEntityNumber does not match any EpcEntity
        _db.FundingCommitments.Add(Commitment("FRN001", "SPIN001", "UNKNOWN-BEN", 1000m));
        await _db.SaveChangesAsync();

        var results = await _repo.GetProvidersByApplicantStateAsync();

        Assert.Empty(results);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
