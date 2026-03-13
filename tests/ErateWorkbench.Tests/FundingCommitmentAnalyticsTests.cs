using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests for cross-dataset analytics queries on FundingCommitmentRepository.
/// Uses an in-memory SQLite database seeded with both FundingCommitments and EpcEntities.
///
/// Join assumptions (mirroring production logic):
/// - FundingCommitments.ApplicantEntityNumber → EpcEntities.EntityNumber
/// - Rows with null ApplicantEntityNumber are excluded from join-dependent queries.
/// - Entities not present in EpcEntities are excluded from joined results (inner-join semantics).
/// </summary>
public class FundingCommitmentAnalyticsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly FundingCommitmentRepository _repo;

    public FundingCommitmentAnalyticsTests()
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
        string frn, int lineItem,
        int year = 2024,
        decimal amount = 1_000m,
        string? entityNumber = null,
        string? category = "Category 1") =>
    new()
    {
        FundingRequestNumber = frn,
        FrnLineItemNumber = lineItem,
        RawSourceKey = $"{frn}-{lineItem}",
        FundingYear = year,
        ApplicantEntityNumber = entityNumber,
        CommittedAmount = amount,
        CategoryOfService = category,
        ImportedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };

    private static EpcEntity Entity(
        string entityNumber,
        string state = "TX",
        decimal? cat1Rate = 80m) =>
    new()
    {
        EntityNumber = entityNumber,
        EntityName = $"Entity {entityNumber}",
        EntityType = EpcEntityType.School,
        PhysicalState = state,
        CategoryOneDiscountRate = cat1Rate,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    // --- GetFundingByYearAsync ---

    [Fact]
    public async Task GetFundingByYear_GroupsAndSumsCorrectly()
    {
        await _repo.UpsertBatchAsync([
            Commitment("FRN001", 1, year: 2023, amount: 10_000m),
            Commitment("FRN002", 1, year: 2023, amount: 5_000m),
            Commitment("FRN003", 1, year: 2024, amount: 20_000m),
        ]);

        var results = await _repo.GetFundingByYearAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(2024, results[0].FundingYear); // newest first
        Assert.Equal(1, results[0].CommitmentCount);
        Assert.Equal(20_000m, results[0].TotalCommitted);

        Assert.Equal(2023, results[1].FundingYear);
        Assert.Equal(2, results[1].CommitmentCount);
        Assert.Equal(15_000m, results[1].TotalCommitted);
    }

    // --- GetFundingByCategoryAsync ---

    [Fact]
    public async Task GetFundingByCategory_SplitsCategoryOneAndTwo()
    {
        await _repo.UpsertBatchAsync([
            Commitment("FRN001", 1, category: "Category 1", amount: 30_000m),
            Commitment("FRN002", 1, category: "Category 1", amount: 20_000m),
            Commitment("FRN003", 1, category: "Category 2", amount: 5_000m),
        ]);

        var results = await _repo.GetFundingByCategoryAsync();

        Assert.Equal(2, results.Count);
        var cat1 = results.Single(r => r.Category == "Category 1");
        Assert.Equal(2, cat1.CommitmentCount);
        Assert.Equal(50_000m, cat1.TotalCommitted);

        var cat2 = results.Single(r => r.Category == "Category 2");
        Assert.Equal(5_000m, cat2.TotalCommitted);
    }

    [Fact]
    public async Task GetFundingByCategory_NullCategoryRows_AreExcluded()
    {
        await _repo.UpsertBatchAsync([
            Commitment("FRN001", 1, category: null, amount: 99_999m),
            Commitment("FRN002", 1, category: "Category 1", amount: 1_000m),
        ]);

        var results = await _repo.GetFundingByCategoryAsync();

        Assert.Single(results);
        Assert.Equal("Category 1", results[0].Category);
    }

    // --- GetFundingByStateAsync ---

    [Fact]
    public async Task GetFundingByState_JoinsToEpcEntitiesForState()
    {
        _db.EpcEntities.AddRange(
            Entity("BEN001", state: "TX"),
            Entity("BEN002", state: "CA"));
        await _db.SaveChangesAsync();

        await _repo.UpsertBatchAsync([
            Commitment("FRN001", 1, entityNumber: "BEN001", amount: 10_000m),
            Commitment("FRN002", 1, entityNumber: "BEN001", amount: 5_000m),
            Commitment("FRN003", 1, entityNumber: "BEN002", amount: 8_000m),
        ]);

        var results = await _repo.GetFundingByStateAsync();

        Assert.Equal(2, results.Count);
        var tx = results.Single(r => r.State == "TX");
        Assert.Equal(2, tx.CommitmentCount);
        Assert.Equal(15_000m, tx.TotalCommitted);

        var ca = results.Single(r => r.State == "CA");
        Assert.Equal(8_000m, ca.TotalCommitted);
    }

    [Fact]
    public async Task GetFundingByState_CommitmentsWithNoMatchingEntity_AreExcluded()
    {
        // BEN001 exists in EpcEntities; BEN999 does not
        _db.EpcEntities.Add(Entity("BEN001", state: "TX"));
        await _db.SaveChangesAsync();

        await _repo.UpsertBatchAsync([
            Commitment("FRN001", 1, entityNumber: "BEN001", amount: 5_000m),
            Commitment("FRN002", 1, entityNumber: "BEN999", amount: 99_000m), // excluded
        ]);

        var results = await _repo.GetFundingByStateAsync();

        Assert.Single(results);
        Assert.Equal("TX", results[0].State);
        Assert.Equal(5_000m, results[0].TotalCommitted);
    }

    // --- GetTopFundedEntitiesAsync ---

    [Fact]
    public async Task GetTopFundedEntities_OrdersByTotalDescending()
    {
        _db.EpcEntities.AddRange(
            Entity("BEN001", state: "TX"),
            Entity("BEN002", state: "CA"),
            Entity("BEN003", state: "NY"));
        await _db.SaveChangesAsync();

        await _repo.UpsertBatchAsync([
            Commitment("FRN001", 1, entityNumber: "BEN001", amount: 10_000m),
            Commitment("FRN002", 1, entityNumber: "BEN002", amount: 50_000m),
            Commitment("FRN003", 1, entityNumber: "BEN003", amount: 25_000m),
        ]);

        var results = await _repo.GetTopFundedEntitiesAsync(topN: 10);

        Assert.Equal(3, results.Count);
        Assert.Equal("BEN002", results[0].EntityNumber);
        Assert.Equal(50_000m, results[0].TotalCommitted);
        Assert.Equal("CA", results[0].State);
    }

    [Fact]
    public async Task GetTopFundedEntities_RespectsTopNLimit()
    {
        _db.EpcEntities.AddRange(
            Entity("BEN001"), Entity("BEN002"), Entity("BEN003"));
        await _db.SaveChangesAsync();

        await _repo.UpsertBatchAsync([
            Commitment("FRN001", 1, entityNumber: "BEN001", amount: 3_000m),
            Commitment("FRN002", 1, entityNumber: "BEN002", amount: 2_000m),
            Commitment("FRN003", 1, entityNumber: "BEN003", amount: 1_000m),
        ]);

        var results = await _repo.GetTopFundedEntitiesAsync(topN: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal("BEN001", results[0].EntityNumber);
        Assert.Equal("BEN002", results[1].EntityNumber);
    }

    // --- GetHighDiscountLowUtilizationAsync ---

    [Fact]
    public async Task GetHighDiscountLowUtilization_ReturnsEntitiesBelowStateAverage()
    {
        // Two TX entities with high discount; BEN001 gets lots of funding, BEN002 gets little.
        // State average = (20_000 + 1_000) / 2 = 10_500 → BEN002 qualifies.
        _db.EpcEntities.AddRange(
            Entity("BEN001", state: "TX", cat1Rate: 80m),
            Entity("BEN002", state: "TX", cat1Rate: 85m));
        await _db.SaveChangesAsync();

        await _repo.UpsertBatchAsync([
            Commitment("FRN001", 1, entityNumber: "BEN001", amount: 20_000m),
            Commitment("FRN002", 1, entityNumber: "BEN002", amount: 1_000m),
        ]);

        var results = await _repo.GetHighDiscountLowUtilizationAsync(minDiscountRate: 70m);

        Assert.Single(results);
        Assert.Equal("BEN002", results[0].EntityNumber);
        Assert.Equal(1_000m, results[0].TotalCommitted);
        Assert.Equal(10_500m, results[0].StateAvgCommitted);
    }

    [Fact]
    public async Task GetHighDiscountLowUtilization_ExcludesEntitiesBelowDiscountThreshold()
    {
        // BEN001 has 60% discount (below threshold of 70) — should be excluded
        // BEN002 has 75% discount and low funding — should be included
        _db.EpcEntities.AddRange(
            Entity("BEN001", state: "TX", cat1Rate: 60m),
            Entity("BEN002", state: "TX", cat1Rate: 75m));
        await _db.SaveChangesAsync();

        await _repo.UpsertBatchAsync([
            Commitment("FRN001", 1, entityNumber: "BEN001", amount: 500m),
            Commitment("FRN002", 1, entityNumber: "BEN002", amount: 1_000m),
        ]);

        // Only BEN002 passes the discount threshold (70)
        // State avg among qualifying entities = 1_000 (only BEN002 in the candidate pool)
        // BEN002 is not below its own average, so 0 results
        var results = await _repo.GetHighDiscountLowUtilizationAsync(minDiscountRate: 70m);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetHighDiscountLowUtilization_EntitiesWithNoCommitments_IncludedWithZero()
    {
        // BEN001 has funding; BEN002 does not — BEN002 should appear with 0 committed
        _db.EpcEntities.AddRange(
            Entity("BEN001", state: "TX", cat1Rate: 80m),
            Entity("BEN002", state: "TX", cat1Rate: 90m));
        await _db.SaveChangesAsync();

        await _repo.UpsertBatchAsync([
            Commitment("FRN001", 1, entityNumber: "BEN001", amount: 10_000m),
        ]);

        var results = await _repo.GetHighDiscountLowUtilizationAsync(minDiscountRate: 70m);

        Assert.Single(results);
        Assert.Equal("BEN002", results[0].EntityNumber);
        Assert.Equal(0m, results[0].TotalCommitted);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
