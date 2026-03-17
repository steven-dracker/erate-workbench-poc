using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Tests;

/// <summary>
/// Integration tests for AnalyticsRepository using an in-memory SQLite database.
/// Each test populates only the tables it needs and verifies query output independently.
/// </summary>
public class AnalyticsRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly AnalyticsRepository _analytics;

    public AnalyticsRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _analytics = new AnalyticsRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private FundingCommitment Commitment(string frn, string? ben, string? spin = null,
        decimal? amount = null, int year = 2024, string? name = null, string? providerName = null) =>
        new()
        {
            RawSourceKey = $"{frn}-1",
            FundingRequestNumber = frn,
            ApplicantEntityNumber = ben,
            ApplicantName = name,
            ServiceProviderSpin = spin,
            ServiceProviderName = providerName,
            FundingYear = year,
            CommittedAmount = amount,
            ImportedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

    private Disbursement Disbursement(string frn, string? ben, string? spin = null,
        decimal? approved = null, int year = 2024) =>
        new()
        {
            RawSourceKey = $"{frn}-INV-1",
            FundingRequestNumber = frn,
            ApplicantEntityNumber = ben,
            ServiceProviderSpin = spin,
            FundingYear = year,
            ApprovedAmount = approved,
            ImportedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

    private Entity Entity(string number, string? name = null, string? urbanRural = null,
        int? ftStudents = null, int? ptStudents = null) =>
        new()
        {
            EntityNumber = number,
            EntityName = name,
            UrbanRuralStatus = urbanRural,
            FullTimeStudentCount = ftStudents,
            PartTimeStudentCount = ptStudents,
            ImportedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

    // -------------------------------------------------------------------------
    // 1. CommitmentVsDisbursementByYear
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetCommitmentVsDisbursementByYear_ReturnsCorrectTotalsAndGap()
    {
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", amount: 1000m, year: 2023),
            Commitment("FRN002", "BEN001", amount: 2000m, year: 2023),
            Commitment("FRN003", "BEN002", amount: 5000m, year: 2024));
        _db.Disbursements.AddRange(
            Disbursement("FRN001", "BEN001", approved: 900m, year: 2023),
            Disbursement("FRN003", "BEN002", approved: 4500m, year: 2024));
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetCommitmentVsDisbursementByYearAsync();

        Assert.Equal(2, rows.Count);

        var row2024 = rows.Single(r => r.FundingYear == 2024);
        Assert.Equal(5000m, row2024.TotalCommittedAmount);
        Assert.Equal(4500m, row2024.TotalDisbursedAmount);
        Assert.Equal(500m, row2024.GapAmount);

        var row2023 = rows.Single(r => r.FundingYear == 2023);
        Assert.Equal(3000m, row2023.TotalCommittedAmount);
        Assert.Equal(900m, row2023.TotalDisbursedAmount);
        Assert.Equal(2100m, row2023.GapAmount);
    }

    [Fact]
    public async Task GetCommitmentVsDisbursementByYear_ZeroDisbursements_WhenNoDisbursementsExist()
    {
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", amount: 1000m, year: 2024));
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetCommitmentVsDisbursementByYearAsync();

        Assert.Single(rows);
        Assert.Equal(1000m, rows[0].TotalCommittedAmount);
        Assert.Equal(0m, rows[0].TotalDisbursedAmount);
        Assert.Equal(1000m, rows[0].GapAmount);
    }

    [Fact]
    public async Task GetCommitmentVsDisbursementByYear_OrderedNewestFirst()
    {
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", amount: 100m, year: 2022),
            Commitment("FRN002", "BEN001", amount: 200m, year: 2024),
            Commitment("FRN003", "BEN001", amount: 300m, year: 2023));
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetCommitmentVsDisbursementByYearAsync();

        Assert.Equal([2024, 2023, 2022], rows.Select(r => r.FundingYear));
    }

    // -------------------------------------------------------------------------
    // 2. TopApplicants
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetTopApplicants_ReturnsApplicantsOrderedByCommitment()
    {
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", amount: 500m, name: "School A"),
            Commitment("FRN002", "BEN001", amount: 1500m, name: "School A"),
            Commitment("FRN003", "BEN002", amount: 3000m, name: "School B"));
        _db.Disbursements.AddRange(
            Disbursement("FRN001", "BEN001", approved: 400m),
            Disbursement("FRN003", "BEN002", approved: 2800m));
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetTopApplicantsAsync(topN: 10);

        Assert.Equal(2, rows.Count);
        // BEN002 has highest commitment
        Assert.Equal("BEN002", rows[0].ApplicantEntityNumber);
        Assert.Equal(3000m, rows[0].TotalCommittedAmount);
        Assert.Equal(2800m, rows[0].TotalDisbursedAmount);

        Assert.Equal("BEN001", rows[1].ApplicantEntityNumber);
        Assert.Equal(2000m, rows[1].TotalCommittedAmount);
        Assert.Equal(400m, rows[1].TotalDisbursedAmount);
    }

    [Fact]
    public async Task GetTopApplicants_ZeroDisbursed_WhenNoMatchingDisbursements()
    {
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", amount: 1000m, name: "School A"));
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetTopApplicantsAsync();

        Assert.Single(rows);
        Assert.Equal(0m, rows[0].TotalDisbursedAmount);
    }

    [Fact]
    public async Task GetTopApplicants_TopNLimitsResults()
    {
        _db.FundingCommitments.AddRange(Enumerable.Range(1, 10).Select(i =>
            Commitment($"FRN{i:D3}", $"BEN{i:D3}", amount: i * 100m, name: $"School {i}")));
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetTopApplicantsAsync(topN: 3);

        Assert.Equal(3, rows.Count);
    }

    // -------------------------------------------------------------------------
    // 3. TopServiceProviders
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetTopServiceProviders_ReturnsProvidersOrderedByCommitment()
    {
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", spin: "SPIN001", amount: 1000m, providerName: "Acme"),
            Commitment("FRN002", "BEN002", spin: "SPIN001", amount: 2000m, providerName: "Acme"),
            Commitment("FRN003", "BEN003", spin: "SPIN002", amount: 500m, providerName: "Beta"));
        _db.Disbursements.AddRange(
            Disbursement("FRN001", "BEN001", spin: "SPIN001", approved: 900m),
            Disbursement("FRN003", "BEN003", spin: "SPIN002", approved: 400m));
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetTopServiceProvidersAsync(topN: 10);

        Assert.Equal(2, rows.Count);
        Assert.Equal("SPIN001", rows[0].ServiceProviderSpin);
        Assert.Equal(3000m, rows[0].TotalCommittedAmount);
        Assert.Equal(900m, rows[0].TotalDisbursedAmount);

        Assert.Equal("SPIN002", rows[1].ServiceProviderSpin);
        Assert.Equal(500m, rows[1].TotalCommittedAmount);
        Assert.Equal(400m, rows[1].TotalDisbursedAmount);
    }

    // -------------------------------------------------------------------------
    // 4. RuralUrbanSummary
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetRuralUrbanSummary_GroupsByUrbanRuralStatus()
    {
        _db.Entities.AddRange(
            Entity("BEN001", urbanRural: "Rural"),
            Entity("BEN002", urbanRural: "Urban"),
            Entity("BEN003", urbanRural: "Rural"));
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", amount: 1000m),
            Commitment("FRN002", "BEN002", amount: 2000m),
            Commitment("FRN003", "BEN003", amount: 500m));
        _db.Disbursements.AddRange(
            Disbursement("FRN001", "BEN001", approved: 800m),
            Disbursement("FRN002", "BEN002", approved: 1800m));
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetRuralUrbanSummaryAsync();

        Assert.Equal(2, rows.Count);
        var urban = rows.Single(r => r.UrbanRuralStatus == "Urban");
        Assert.Equal(2000m, urban.TotalCommittedAmount);
        Assert.Equal(1800m, urban.TotalDisbursedAmount);

        var rural = rows.Single(r => r.UrbanRuralStatus == "Rural");
        Assert.Equal(1500m, rural.TotalCommittedAmount);
        Assert.Equal(800m, rural.TotalDisbursedAmount);
    }

    [Fact]
    public async Task GetRuralUrbanSummary_EntityNotInEntitiesTable_BucketedAsUnknown()
    {
        // BEN001 has no row in Entities — falls into "Unknown" bucket
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", amount: 999m));
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetRuralUrbanSummaryAsync();

        Assert.Single(rows);
        Assert.Equal("Unknown", rows[0].UrbanRuralStatus);
        Assert.Equal(999m, rows[0].TotalCommittedAmount);
    }

    // -------------------------------------------------------------------------
    // 5. FundingPerStudent
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetFundingPerStudent_CalculatesFundingPerStudentCorrectly()
    {
        _db.Entities.AddRange(
            Entity("BEN001", ftStudents: 100, ptStudents: 50),   // 150 students
            Entity("BEN002", ftStudents: 500, ptStudents: 0));     // 500 students
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", amount: 15000m, name: "School A"),
            Commitment("FRN002", "BEN002", amount: 50000m, name: "School B"));
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetFundingPerStudentAsync(topN: 10);

        Assert.Equal(2, rows.Count);
        var a = rows.Single(r => r.ApplicantEntityNumber == "BEN001");
        Assert.Equal(150, a.StudentCount);
        Assert.Equal(15000m, a.TotalCommittedAmount);
        Assert.Equal(100m, a.FundingPerStudent);  // 15000 / 150

        var b = rows.Single(r => r.ApplicantEntityNumber == "BEN002");
        Assert.Equal(500, b.StudentCount);
        Assert.Equal(100m, b.FundingPerStudent);  // 50000 / 500
    }

    [Fact]
    public async Task GetFundingPerStudent_ExcludesEntitiesWithZeroStudentCount()
    {
        _db.Entities.Add(Entity("BEN001", ftStudents: 0, ptStudents: 0));
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", amount: 1000m));
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetFundingPerStudentAsync();

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetFundingPerStudent_OrderedByFundingPerStudentDescending()
    {
        _db.Entities.AddRange(
            Entity("BEN001", ftStudents: 1000),   // low per-student
            Entity("BEN002", ftStudents: 10));      // high per-student
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", amount: 10000m),
            Commitment("FRN002", "BEN002", amount: 5000m));  // 500 per student
        await _db.SaveChangesAsync();

        var rows = await _analytics.GetFundingPerStudentAsync();

        // BEN002: 500/student, BEN001: 10/student — BEN002 comes first
        Assert.Equal("BEN002", rows[0].ApplicantEntityNumber);
        Assert.Equal("BEN001", rows[1].ApplicantEntityNumber);
    }
}
