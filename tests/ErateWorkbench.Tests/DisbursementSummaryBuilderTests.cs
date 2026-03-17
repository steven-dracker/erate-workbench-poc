using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using ErateWorkbench.Infrastructure.Reconciliation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Tests;

public class DisbursementSummaryBuilderTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;

    public DisbursementSummaryBuilderTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
        _db = new AppDbContext(opts);
        _db.Database.EnsureCreated();
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Disbursement D(
        string key, int year, string? ben,
        decimal? approved, decimal? requested = null,
        string? frn = null, string? invoiceId = null,
        string? name = null) => new()
    {
        RawSourceKey           = key,
        FundingYear            = year,
        ApplicantEntityNumber  = ben,
        ApplicantEntityName    = name,
        FundingRequestNumber   = frn ?? key,
        InvoiceId              = invoiceId,
        ApprovedAmount         = approved,
        RequestedAmount        = requested ?? approved,
        ImportedAtUtc          = DateTime.UtcNow,
        UpdatedAtUtc           = DateTime.UtcNow,
    };

    private ApplicantYearDisbursementSummaryBuilder Builder() => new(_db);

    // ── Inclusion rule: ApprovedAmount > 0 ───────────────────────────────────

    [Fact]
    public async Task RebuildAsync_InclusionRule_ExcludesZeroApprovedAmount()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 100m),
            D("A2", 2023, "BEN-1", approved: 0m),     // excluded
            D("A3", 2023, "BEN-1", approved: null)     // excluded (null → 0 in builder)
        );
        await _db.SaveChangesAsync();

        var result = await Builder().RebuildAsync(fundingYear: 2023);

        Assert.Equal(3, result.RawRowsScanned);
        Assert.Equal(1, result.IncludedRows);
        Assert.Equal(1, result.SummaryRowsWritten);

        var row = await _db.ApplicantYearDisbursementSummaries.SingleAsync();
        Assert.Equal(100m, row.TotalApprovedAmount);
        Assert.Equal(1, row.DisbursementRowCount);
    }

    [Fact]
    public async Task RebuildAsync_InclusionRule_IncludesPositiveApprovedAmount()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 50m),
            D("A2", 2023, "BEN-1", approved: 75m)
        );
        await _db.SaveChangesAsync();

        var result = await Builder().RebuildAsync(fundingYear: 2023);

        Assert.Equal(2, result.IncludedRows);
        var row = await _db.ApplicantYearDisbursementSummaries.SingleAsync();
        Assert.Equal(125m, row.TotalApprovedAmount);
    }

    [Fact]
    public async Task RebuildAsync_InclusionRule_AllZero_ProducesNoSummaryRows()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 0m),
            D("A2", 2023, "BEN-2", approved: null)
        );
        await _db.SaveChangesAsync();

        var result = await Builder().RebuildAsync(fundingYear: 2023);

        Assert.Equal(2, result.RawRowsScanned);
        Assert.Equal(0, result.IncludedRows);
        Assert.Equal(0, result.SummaryRowsWritten);
        Assert.Empty(await _db.ApplicantYearDisbursementSummaries.ToListAsync());
    }

    // ── Grouping ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_GroupsByYearAndBen()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 100m),
            D("A2", 2023, "BEN-1", approved: 200m), // same BEN → aggregated
            D("B1", 2023, "BEN-2", approved: 300m),
            D("C1", 2024, "BEN-1", approved: 400m)  // different year
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var all = await _db.ApplicantYearDisbursementSummaries.ToListAsync();
        Assert.Equal(3, all.Count); // (2023,BEN-1), (2023,BEN-2), (2024,BEN-1)
    }

    [Fact]
    public async Task RebuildAsync_AggregatesAmountsCorrectly()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 100m, requested: 120m),
            D("A2", 2023, "BEN-1", approved: 200m, requested: 240m)
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var row = await _db.ApplicantYearDisbursementSummaries.SingleAsync();
        Assert.Equal(300m, row.TotalApprovedAmount);
        Assert.Equal(360m, row.TotalRequestedAmount);
    }

    [Fact]
    public async Task RebuildAsync_DisbursementRowCount_CorrectPerGroup()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 10m),
            D("A2", 2023, "BEN-1", approved: 20m),
            D("A3", 2023, "BEN-1", approved: 30m),
            D("B1", 2023, "BEN-2", approved: 40m)
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var ben1 = await _db.ApplicantYearDisbursementSummaries.SingleAsync(s => s.ApplicantEntityNumber == "BEN-1");
        var ben2 = await _db.ApplicantYearDisbursementSummaries.SingleAsync(s => s.ApplicantEntityNumber == "BEN-2");
        Assert.Equal(3, ben1.DisbursementRowCount);
        Assert.Equal(1, ben2.DisbursementRowCount);
    }

    // ── Distinct FRN + invoice counting ──────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_DistinctFrnCount_CountsUniqueRequestNumbers()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 10m, frn: "FRN-1"),
            D("A2", 2023, "BEN-1", approved: 20m, frn: "FRN-1"), // same FRN
            D("A3", 2023, "BEN-1", approved: 30m, frn: "FRN-2")  // different FRN
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var row = await _db.ApplicantYearDisbursementSummaries.SingleAsync();
        Assert.Equal(2, row.DistinctFrnCount);
    }

    [Fact]
    public async Task RebuildAsync_DistinctInvoiceCount_CountsNonNullUniqueInvoices()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 10m, invoiceId: "INV-1"),
            D("A2", 2023, "BEN-1", approved: 20m, invoiceId: "INV-1"), // duplicate
            D("A3", 2023, "BEN-1", approved: 30m, invoiceId: "INV-2"),
            D("A4", 2023, "BEN-1", approved: 40m, invoiceId: null)     // null excluded
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var row = await _db.ApplicantYearDisbursementSummaries.SingleAsync();
        Assert.Equal(2, row.DistinctInvoiceCount);
    }

    [Fact]
    public async Task RebuildAsync_AllNullInvoiceIds_DistinctInvoiceCountIsZero()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 10m, invoiceId: null),
            D("A2", 2023, "BEN-1", approved: 20m, invoiceId: null)
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var row = await _db.ApplicantYearDisbursementSummaries.SingleAsync();
        Assert.Equal(0, row.DistinctInvoiceCount);
    }

    // ── Year-scoped rebuild ───────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_YearScoped_OnlyAffectsSpecifiedYear()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 100m),
            D("B1", 2024, "BEN-1", approved: 200m)
        );
        await _db.SaveChangesAsync();
        await Builder().RebuildAsync();

        // Modify 2024 raw row and rebuild only 2024
        var raw2024 = _db.Disbursements.Single(d => d.FundingYear == 2024);
        raw2024.ApprovedAmount = 999m;
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2024);

        var s2023 = await _db.ApplicantYearDisbursementSummaries.SingleAsync(s => s.FundingYear == 2023);
        Assert.Equal(100m, s2023.TotalApprovedAmount); // unchanged

        var s2024 = await _db.ApplicantYearDisbursementSummaries.SingleAsync(s => s.FundingYear == 2024);
        Assert.Equal(999m, s2024.TotalApprovedAmount); // updated
    }

    [Fact]
    public async Task RebuildAsync_DeletesExistingRowsForYear_BeforeReinserting()
    {
        _db.Disbursements.Add(D("A1", 2023, "BEN-1", approved: 100m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);
        await Builder().RebuildAsync(fundingYear: 2023); // second run

        // Must not double-insert
        Assert.Equal(1, await _db.ApplicantYearDisbursementSummaries.CountAsync(s => s.FundingYear == 2023));
    }

    // ── ApplicantEntityName ───────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_ApplicantEntityName_UsesMinName()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 10m, name: "Zebra District"),
            D("A2", 2023, "BEN-1", approved: 20m, name: "Apple District")
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var row = await _db.ApplicantYearDisbursementSummaries.SingleAsync();
        Assert.Equal("Apple District", row.ApplicantEntityName);
    }

    // ── Result model ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_ResultModel_HasCorrectCounts()
    {
        _db.Disbursements.AddRange(
            D("A1", 2023, "BEN-1", approved: 100m),
            D("A2", 2023, "BEN-1", approved: 200m),
            D("B1", 2023, "BEN-2", approved: 300m),
            D("C1", 2023, "BEN-3", approved: 0m)   // excluded
        );
        await _db.SaveChangesAsync();

        var result = await Builder().RebuildAsync(fundingYear: 2023);

        Assert.Equal(4, result.RawRowsScanned);
        Assert.Equal(3, result.IncludedRows);
        Assert.Equal(2, result.SummaryRowsWritten); // BEN-1 and BEN-2
        Assert.Equal(600m, result.TotalApprovedAmount);
        Assert.Equal(2023, result.FundingYearScope);
    }

    [Fact]
    public async Task RebuildAsync_AllYears_FundingYearScopeIsNull()
    {
        _db.Disbursements.Add(D("A1", 2023, "BEN-1", approved: 100m));
        await _db.SaveChangesAsync();

        var result = await Builder().RebuildAsync();

        Assert.Null(result.FundingYearScope);
    }

    [Fact]
    public async Task RebuildAsync_EmptyTable_ReturnsZeroAndDoesNotThrow()
    {
        var result = await Builder().RebuildAsync(fundingYear: 2023);
        Assert.Equal(0, result.RawRowsScanned);
        Assert.Equal(0, result.IncludedRows);
        Assert.Equal(0, result.SummaryRowsWritten);
    }
}

// ── DisbursementSummaryLocalProvider tests ────────────────────────────────────

public class DisbursementSummaryLocalProviderTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;

    public DisbursementSummaryLocalProviderTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
        _db = new AppDbContext(opts);
        _db.Database.EnsureCreated();
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    private static ApplicantYearDisbursementSummary Sum(
        int year, string? ben, decimal requested, decimal approved) => new()
    {
        FundingYear           = year,
        ApplicantEntityNumber = ben,
        TotalRequestedAmount  = requested,
        TotalApprovedAmount   = approved,
        DisbursementRowCount  = 1,
        DistinctFrnCount      = 1,
        DistinctInvoiceCount  = 1,
        ImportedAtUtc         = DateTime.UtcNow,
    };

    [Fact]
    public async Task GetLocalSummaryTotals_GroupsByYear()
    {
        _db.ApplicantYearDisbursementSummaries.AddRange(
            Sum(2023, "BEN-1", requested: 120m, approved: 100m),
            Sum(2023, "BEN-2", requested: 240m, approved: 200m),
            Sum(2024, "BEN-1", requested:  60m, approved:  50m)
        );
        await _db.SaveChangesAsync();

        var provider = new DisbursementSummaryLocalProvider(_db);
        var totals   = await provider.GetLocalSummaryTotalsAsync();

        Assert.Equal(2, totals.Count);
        var t2023 = totals.Single(t => t.FundingYear == 2023);
        Assert.Equal(2L,    t2023.RowCount);
        Assert.Equal(360m,  t2023.Amounts["RequestedAmount"]);
        Assert.Equal(300m,  t2023.Amounts["ApprovedAmount"]);
    }

    [Fact]
    public async Task GetLocalSummaryTotals_DistinctApplicants_ExcludesNullBen()
    {
        _db.ApplicantYearDisbursementSummaries.AddRange(
            Sum(2023, "BEN-1", requested: 100m, approved: 90m),
            Sum(2023, null,    requested:  50m, approved: 40m) // null BEN row
        );
        await _db.SaveChangesAsync();

        var provider = new DisbursementSummaryLocalProvider(_db);
        var totals   = await provider.GetLocalSummaryTotalsAsync();

        var t2023 = totals.Single(t => t.FundingYear == 2023);
        Assert.Equal(2L, t2023.RowCount);           // total rows = 2
        Assert.Equal(1L, t2023.DistinctApplicants); // only 1 non-null BEN
    }

    [Fact]
    public async Task GetLocalSummaryTotals_Empty_ReturnsEmpty()
    {
        var provider = new DisbursementSummaryLocalProvider(_db);
        Assert.Empty(await provider.GetLocalSummaryTotalsAsync());
    }

    [Fact]
    public void DatasetName_MatchesDisbursementsManifest()
    {
        var provider = new DisbursementSummaryLocalProvider(_db);
        Assert.Equal(DatasetManifests.Disbursements.Name, provider.DatasetName);
    }
}
