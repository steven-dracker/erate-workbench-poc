using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Tests;

/// <summary>
/// Unit tests for ConsultantAnalyticsService.
///
/// Identity rule (CC-ERATE-000038C): ConsultantEpcOrganizationId is the canonical grouping key.
/// Fan-out guard: application and FRN datasets are queried independently and joined in memory.
/// Financial filter: only FRN rows with FrnStatusName = "Funded" contribute to funding totals.
/// </summary>
public class ConsultantAnalyticsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ConsultantAnalyticsService _svc;

    public ConsultantAnalyticsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _svc = new ConsultantAnalyticsService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private void SeedApplication(string epcId, string appNumber, int year = 2024, string? name = null, string? state = null)
    {
        _db.ConsultantApplications.Add(new ConsultantApplication
        {
            RawSourceKey = $"{appNumber}-{epcId}",
            ApplicationNumber = appNumber,
            ConsultantEpcOrganizationId = epcId,
            ConsultantName = name,
            FundingYear = year,
            ApplicantState = state,
        });
    }

    private void SeedFrn(string epcId, string frn, string appNumber, int year = 2024,
        string status = "Funded", decimal? funding = null, string? serviceType = null)
    {
        _db.ConsultantFrnStatuses.Add(new ConsultantFrnStatus
        {
            RawSourceKey = $"{appNumber}-{frn}",
            FundingRequestNumber = frn,
            ApplicationNumber = appNumber,
            ConsultantEpcOrganizationId = epcId,
            FrnStatusName = status,
            FundingCommitmentRequest = funding,
            ServiceTypeName = serviceType,
            FundingYear = year,
        });
    }

    // ── GetOverviewStatsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetOverviewStats_ReturnsZero_WhenNoData()
    {
        var (consultants, apps) = await _svc.GetOverviewStatsAsync();
        Assert.Equal(0, consultants);
        Assert.Equal(0, apps);
    }

    [Fact]
    public async Task GetOverviewStats_CountsDistinctConsultants()
    {
        SeedApplication("EPC1", "APP1");
        SeedApplication("EPC1", "APP2"); // same consultant, two apps
        SeedApplication("EPC2", "APP3");
        await _db.SaveChangesAsync();

        var (consultants, apps) = await _svc.GetOverviewStatsAsync();
        Assert.Equal(2, consultants); // 2 distinct EPC IDs
        Assert.Equal(3, apps);
    }

    // ── GetTopConsultantsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetTopConsultants_ReturnsEmpty_WhenNoData()
    {
        var result = await _svc.GetTopConsultantsAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTopConsultants_GroupsByEpcId_NotByName()
    {
        // Same EPC ID with two different name casings — must count as one consultant
        SeedApplication("EPC1", "APP1", name: "Erate Exchange LLC");
        SeedApplication("EPC1", "APP2", name: "ERATE EXCHANGE LLC");
        SeedApplication("EPC2", "APP3", name: "Other Consultant");
        await _db.SaveChangesAsync();

        var result = await _svc.GetTopConsultantsAsync();
        Assert.Equal(2, result.Count); // 2 distinct EPC IDs
        Assert.Equal(2, result.Single(r => r.ConsultantEpcOrganizationId == "EPC1").TotalApplications);
    }

    [Fact]
    public async Task GetTopConsultants_OrdersByApplicationCountDescending()
    {
        SeedApplication("EPC_BIG", "APP1");
        SeedApplication("EPC_BIG", "APP2");
        SeedApplication("EPC_BIG", "APP3");
        SeedApplication("EPC_SMALL", "APP4");
        await _db.SaveChangesAsync();

        var result = await _svc.GetTopConsultantsAsync();
        Assert.Equal("EPC_BIG", result[0].ConsultantEpcOrganizationId);
        Assert.Equal("EPC_SMALL", result[1].ConsultantEpcOrganizationId);
    }

    [Fact]
    public async Task GetTopConsultants_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
            SeedApplication($"EPC{i}", $"APP{i}");
        await _db.SaveChangesAsync();

        var result = await _svc.GetTopConsultantsAsync(limit: 3);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetTopConsultants_FrnCountFromFrnDataset_NotApplications()
    {
        // 2 applications, 3 FRNs — counts must come from separate datasets
        SeedApplication("EPC1", "APP1");
        SeedApplication("EPC1", "APP2");
        SeedFrn("EPC1", "FRN1", "APP1");
        SeedFrn("EPC1", "FRN2", "APP1");
        SeedFrn("EPC1", "FRN3", "APP2");
        await _db.SaveChangesAsync();

        var result = await _svc.GetTopConsultantsAsync();
        var epc1 = result.Single();
        Assert.Equal(2, epc1.TotalApplications);
        Assert.Equal(3, epc1.TotalFrns);
    }

    [Fact]
    public async Task GetTopConsultants_FundedTotalFiltersOnFrnStatusName()
    {
        SeedApplication("EPC1", "APP1");
        SeedFrn("EPC1", "FRN1", "APP1", status: "Funded", funding: 10_000m);
        SeedFrn("EPC1", "FRN2", "APP1", status: "Denied", funding: 5_000m);   // must not count
        SeedFrn("EPC1", "FRN3", "APP1", status: "Pending Review", funding: 3_000m); // must not count
        await _db.SaveChangesAsync();

        var result = await _svc.GetTopConsultantsAsync();
        Assert.Equal(10_000m, result.Single().TotalFundingAmount);
    }

    [Fact]
    public async Task GetTopConsultants_FundedTotalIsNull_WhenNoFundedFrns()
    {
        SeedApplication("EPC1", "APP1");
        SeedFrn("EPC1", "FRN1", "APP1", status: "Denied", funding: 5_000m);
        await _db.SaveChangesAsync();

        var result = await _svc.GetTopConsultantsAsync();
        Assert.Null(result.Single().TotalFundingAmount);
    }

    // ── GetConsultantDetailsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetConsultantDetails_ReturnsNull_ForUnknownEpcId()
    {
        var result = await _svc.GetConsultantDetailsAsync("NONEXISTENT");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetConsultantDetails_ReturnsCorrectTotals()
    {
        SeedApplication("EPC1", "APP1", year: 2023);
        SeedApplication("EPC1", "APP2", year: 2024);
        SeedFrn("EPC1", "FRN1", "APP1", year: 2023, status: "Funded", funding: 20_000m);
        SeedFrn("EPC1", "FRN2", "APP2", year: 2024, status: "Funded", funding: 30_000m);
        SeedFrn("EPC1", "FRN3", "APP2", year: 2024, status: "Denied", funding: 10_000m);
        await _db.SaveChangesAsync();

        var detail = await _svc.GetConsultantDetailsAsync("EPC1");
        Assert.NotNull(detail);
        Assert.Equal(2, detail.TotalApplications);
        Assert.Equal(3, detail.TotalFrns);
        Assert.Equal(50_000m, detail.TotalFundingAmount); // only Funded rows
    }

    // ── GetTrendsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTrends_ReturnsSortedYears_UnionOfBothDatasets()
    {
        SeedApplication("EPC1", "APP1", year: 2022);
        SeedApplication("EPC1", "APP2", year: 2024);
        SeedFrn("EPC1", "FRN1", "APP1", year: 2023); // year only in FRN dataset
        await _db.SaveChangesAsync();

        var trends = await _svc.GetTrendsAsync("EPC1");
        Assert.Equal(3, trends.Count);
        Assert.Equal(2022, trends[0].Year);
        Assert.Equal(2023, trends[1].Year);
        Assert.Equal(2024, trends[2].Year);
    }

    [Fact]
    public async Task GetTrends_YearWithNoApps_HasZeroApplicationCount()
    {
        SeedFrn("EPC1", "FRN1", "APP1", year: 2023); // FRN-only year
        await _db.SaveChangesAsync();

        var trends = await _svc.GetTrendsAsync("EPC1");
        var row = Assert.Single(trends);
        Assert.Equal(2023, row.Year);
        Assert.Equal(0, row.ApplicationCount);
        Assert.Equal(1, row.FrnCount);
    }

    // ── GetStateBreakdownAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetStateBreakdown_AggregatesApplicationsByApplicantState()
    {
        SeedApplication("EPC1", "APP1", state: "TX");
        SeedApplication("EPC1", "APP2", state: "TX");
        SeedApplication("EPC1", "APP3", state: "CA");
        await _db.SaveChangesAsync();

        var breakdown = await _svc.GetStateBreakdownAsync("EPC1");
        Assert.Equal(2, breakdown.Count);
        Assert.Equal("TX", breakdown[0].State);
        Assert.Equal(2, breakdown[0].ApplicationCount);
        Assert.Equal("CA", breakdown[1].State);
        Assert.Equal(1, breakdown[1].ApplicationCount);
    }

    [Fact]
    public async Task GetStateBreakdown_ExcludesNullState()
    {
        SeedApplication("EPC1", "APP1", state: "TX");
        SeedApplication("EPC1", "APP2", state: null);
        await _db.SaveChangesAsync();

        var breakdown = await _svc.GetStateBreakdownAsync("EPC1");
        Assert.Single(breakdown);
        Assert.Equal("TX", breakdown[0].State);
    }

    // ── GetServiceTypesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceTypes_AggregatesFrnsByServiceType()
    {
        SeedFrn("EPC1", "FRN1", "APP1", serviceType: "Data Transmission and/or Internet Access");
        SeedFrn("EPC1", "FRN2", "APP1", serviceType: "Data Transmission and/or Internet Access");
        SeedFrn("EPC1", "FRN3", "APP1", serviceType: "Internal Connections");
        await _db.SaveChangesAsync();

        var types = await _svc.GetServiceTypesAsync("EPC1");
        Assert.Equal(2, types.Count);
        Assert.Equal("Data Transmission and/or Internet Access", types[0].ServiceTypeName);
        Assert.Equal(2, types[0].FrnCount);
        Assert.Equal("Internal Connections", types[1].ServiceTypeName);
        Assert.Equal(1, types[1].FrnCount);
    }

    [Fact]
    public async Task GetServiceTypes_ExcludesNullServiceType()
    {
        SeedFrn("EPC1", "FRN1", "APP1", serviceType: "Internal Connections");
        SeedFrn("EPC1", "FRN2", "APP1", serviceType: null);
        await _db.SaveChangesAsync();

        var types = await _svc.GetServiceTypesAsync("EPC1");
        Assert.Single(types);
        Assert.Equal("Internal Connections", types[0].ServiceTypeName);
    }
}
