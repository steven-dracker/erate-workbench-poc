using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests use an in-memory SQLite database (a real SQLite engine, not the EF in-memory provider),
/// which ensures queries translate correctly and indexes behave as in production.
/// </summary>
public class EpcEntityRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly EpcEntityRepository _repo;

    public EpcEntityRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _repo = new EpcEntityRepository(_db);

        SeedData();
    }

    private void SeedData()
    {
        _db.EpcEntities.AddRange(
            Entity("100001", "Springfield Elementary School", EpcEntityType.School, state: "IL", status: "Active"),
            Entity("100002", "Springfield Public Library", EpcEntityType.Library, state: "IL", status: "Active"),
            Entity("100003", "Shelbyville High School", EpcEntityType.School, state: "IL", status: "Active"),
            Entity("100004", "Capital City School District", EpcEntityType.SchoolDistrict, state: "IL", status: "Active"),
            Entity("100005", "Portland Elementary", EpcEntityType.School, state: "OR", status: "Active"),
            Entity("100006", "Portland Public Library", EpcEntityType.Library, state: "OR", status: "Closed"),
            Entity("100007", "Seattle Community Consortium", EpcEntityType.Consortium, state: "WA", status: "Active")
        );
        _db.SaveChanges();
    }

    private static EpcEntity Entity(
        string number, string name, EpcEntityType type,
        string state = "TX", string status = "Active") => new()
    {
        EntityNumber = number,
        EntityName = name,
        EntityType = type,
        PhysicalState = state,
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    // --- SearchAsync: state filter ---

    [Fact]
    public async Task SearchAsync_WithStateFilter_ReturnsOnlyEntitiesInThatState()
    {
        var (items, total) = await _repo.SearchAsync(
            search: null, state: "OR", entityType: null, status: null,
            skip: 0, take: 25);

        Assert.Equal(2, total);
        Assert.All(items, e => Assert.Equal("OR", e.PhysicalState));
    }

    [Fact]
    public async Task SearchAsync_StateFilter_IsCaseInsensitive()
    {
        var (items, total) = await _repo.SearchAsync(
            search: null, state: "or", entityType: null, status: null,
            skip: 0, take: 25);

        Assert.Equal(2, total);
    }

    // --- SearchAsync: entityType filter ---

    [Fact]
    public async Task SearchAsync_WithEntityTypeFilter_ReturnsOnlyMatchingType()
    {
        var (items, total) = await _repo.SearchAsync(
            search: null, state: null, entityType: EpcEntityType.Library, status: null,
            skip: 0, take: 25);

        Assert.Equal(2, total);
        Assert.All(items, e => Assert.Equal(EpcEntityType.Library, e.EntityType));
    }

    // --- SearchAsync: name search ---

    [Fact]
    public async Task SearchAsync_WithSearchTerm_ReturnsMatchingNames()
    {
        var (items, total) = await _repo.SearchAsync(
            search: "Springfield", state: null, entityType: null, status: null,
            skip: 0, take: 25);

        Assert.Equal(2, total);
        Assert.All(items, e => Assert.Contains("Springfield", e.EntityName));
    }

    [Fact]
    public async Task SearchAsync_WithSearchTerm_NoMatch_ReturnsEmpty()
    {
        var (items, total) = await _repo.SearchAsync(
            search: "Nonexistent School XYZ", state: null, entityType: null, status: null,
            skip: 0, take: 25);

        Assert.Equal(0, total);
        Assert.Empty(items);
    }

    // --- SearchAsync: combined filters ---

    [Fact]
    public async Task SearchAsync_WithStateAndEntityType_NarrowsResults()
    {
        var (items, total) = await _repo.SearchAsync(
            search: null, state: "IL", entityType: EpcEntityType.School, status: null,
            skip: 0, take: 25);

        Assert.Equal(2, total);
        Assert.All(items, e =>
        {
            Assert.Equal("IL", e.PhysicalState);
            Assert.Equal(EpcEntityType.School, e.EntityType);
        });
    }

    // --- SearchAsync: pagination ---

    [Fact]
    public async Task SearchAsync_Pagination_TotalCountReflectsAllMatches()
    {
        var (_, total) = await _repo.SearchAsync(
            search: null, state: null, entityType: null, status: null,
            skip: 0, take: 2);

        Assert.Equal(7, total);
    }

    [Fact]
    public async Task SearchAsync_Pagination_SecondPageReturnsCorrectItems()
    {
        var (page1, _) = await _repo.SearchAsync(
            search: null, state: null, entityType: null, status: null,
            skip: 0, take: 3);

        var (page2, _) = await _repo.SearchAsync(
            search: null, state: null, entityType: null, status: null,
            skip: 3, take: 3);

        var allNumbers = page1.Concat(page2).Select(e => e.EntityNumber).ToList();
        Assert.Equal(6, allNumbers.Distinct().Count()); // no overlap
    }

    // --- FindByEntityNumberAsync ---

    [Fact]
    public async Task FindByEntityNumberAsync_WhenExists_ReturnsEntity()
    {
        var entity = await _repo.FindByEntityNumberAsync("100002");

        Assert.NotNull(entity);
        Assert.Equal("100002", entity.EntityNumber);
        Assert.Equal("Springfield Public Library", entity.EntityName);
    }

    [Fact]
    public async Task FindByEntityNumberAsync_WhenNotFound_ReturnsNull()
    {
        var entity = await _repo.FindByEntityNumberAsync("999999");

        Assert.Null(entity);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
