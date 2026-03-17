using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using ErateWorkbench.Infrastructure.Reconciliation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Tests;

public class CommitmentSummaryBuilderTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;

    public CommitmentSummaryBuilderTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
        _db = new AppDbContext(opts);
        _db.Database.EnsureCreated();
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static FundingCommitment FC(
        string key, int year, string? ben, string? frn = null,
        decimal eligible = 0m, decimal committed = 0m, string? name = null) => new()
    {
        RawSourceKey         = key,
        FundingRequestNumber = frn ?? key,
        FundingYear          = year,
        ApplicantEntityNumber = ben,
        ApplicantName        = name,
        TotalEligibleAmount  = eligible,
        CommittedAmount      = committed,
        ImportedAtUtc        = DateTime.UtcNow,
        UpdatedAtUtc         = DateTime.UtcNow,
    };

    private ApplicantYearCommitmentSummaryBuilder Builder() => new(_db);

    // ── Grouping ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_GroupsByYearAndBen()
    {
        _db.FundingCommitments.AddRange(
            FC("A1", 2023, "BEN-1", eligible: 100m, committed: 90m),
            FC("A2", 2023, "BEN-1", eligible: 200m, committed: 180m), // same BEN → aggregated
            FC("B1", 2023, "BEN-2", eligible: 300m, committed: 270m),
            FC("C1", 2024, "BEN-1", eligible: 400m, committed: 360m)  // different year
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var all = await _db.ApplicantYearCommitmentSummaries.ToListAsync();
        Assert.Equal(3, all.Count); // (2023,BEN-1), (2023,BEN-2), (2024,BEN-1)
    }

    [Fact]
    public async Task RebuildAsync_AggregatesAmountsCorrectly()
    {
        _db.FundingCommitments.AddRange(
            FC("A1", 2023, "BEN-1", eligible: 1000m, committed: 900m),
            FC("A2", 2023, "BEN-1", eligible: 2000m, committed: 1800m)
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var row = await _db.ApplicantYearCommitmentSummaries.SingleAsync();
        Assert.Equal(3000m, row.TotalEligibleAmount);
        Assert.Equal(2700m, row.TotalCommittedAmount);
    }

    [Fact]
    public async Task RebuildAsync_CommitmentRowCount_CorrectPerGroup()
    {
        _db.FundingCommitments.AddRange(
            FC("A1", 2023, "BEN-1"),
            FC("A2", 2023, "BEN-1"),
            FC("A3", 2023, "BEN-1"),
            FC("B1", 2023, "BEN-2")
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var ben1Row = await _db.ApplicantYearCommitmentSummaries.SingleAsync(s => s.ApplicantEntityNumber == "BEN-1");
        Assert.Equal(3, ben1Row.CommitmentRowCount);

        var ben2Row = await _db.ApplicantYearCommitmentSummaries.SingleAsync(s => s.ApplicantEntityNumber == "BEN-2");
        Assert.Equal(1, ben2Row.CommitmentRowCount);
    }

    [Fact]
    public async Task RebuildAsync_DistinctFrnCount_CountsUniqueRequestNumbers()
    {
        _db.FundingCommitments.AddRange(
            FC("A1", 2023, "BEN-1", frn: "FRN-100", eligible: 100m),
            FC("A2", 2023, "BEN-1", frn: "FRN-100", eligible: 200m), // same FRN
            FC("A3", 2023, "BEN-1", frn: "FRN-200", eligible: 300m)  // different FRN
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var row = await _db.ApplicantYearCommitmentSummaries.SingleAsync();
        Assert.Equal(2, row.DistinctFrnCount);
    }

    [Fact]
    public async Task RebuildAsync_ApplicantEntityName_UsesMinName()
    {
        _db.FundingCommitments.AddRange(
            FC("A1", 2023, "BEN-1", name: "Zebra School"),
            FC("A2", 2023, "BEN-1", name: "Apple School")  // alphabetically first
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var row = await _db.ApplicantYearCommitmentSummaries.SingleAsync();
        Assert.Equal("Apple School", row.ApplicantEntityName); // MIN
    }

    // ── Year-scoped rebuild ───────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_YearScoped_OnlyAffectsSpecifiedYear()
    {
        _db.FundingCommitments.AddRange(
            FC("A1", 2023, "BEN-1", eligible: 100m, committed: 90m),
            FC("B1", 2024, "BEN-1", eligible: 200m, committed: 180m)
        );
        await _db.SaveChangesAsync();

        // Build all years first
        await Builder().RebuildAsync();
        var beforeCount = await _db.ApplicantYearCommitmentSummaries.CountAsync();
        Assert.Equal(2, beforeCount);

        // Modify a 2024 raw row and rebuild only 2024
        var raw2024 = _db.FundingCommitments.Single(c => c.FundingYear == 2024);
        raw2024.TotalEligibleAmount = 999m;
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2024);

        // 2023 summary should be unchanged
        var s2023 = await _db.ApplicantYearCommitmentSummaries.SingleAsync(s => s.FundingYear == 2023);
        Assert.Equal(100m, s2023.TotalEligibleAmount);

        // 2024 summary should reflect the update
        var s2024 = await _db.ApplicantYearCommitmentSummaries.SingleAsync(s => s.FundingYear == 2024);
        Assert.Equal(999m, s2024.TotalEligibleAmount);
    }

    [Fact]
    public async Task RebuildAsync_YearScoped_DoesNotTouchOtherYears()
    {
        _db.FundingCommitments.AddRange(
            FC("A1", 2021, "BEN-1", eligible: 100m, committed: 90m),
            FC("A2", 2022, "BEN-1", eligible: 200m, committed: 180m),
            FC("A3", 2023, "BEN-1", eligible: 300m, committed: 270m)
        );
        await _db.SaveChangesAsync();
        await Builder().RebuildAsync(); // all years

        await Builder().RebuildAsync(fundingYear: 2022); // only 2022

        // 2021 and 2023 should still exist with original values
        Assert.Equal(3, await _db.ApplicantYearCommitmentSummaries.CountAsync());
        Assert.Equal(100m, (await _db.ApplicantYearCommitmentSummaries.SingleAsync(s => s.FundingYear == 2021)).TotalEligibleAmount);
        Assert.Equal(300m, (await _db.ApplicantYearCommitmentSummaries.SingleAsync(s => s.FundingYear == 2023)).TotalEligibleAmount);
    }

    [Fact]
    public async Task RebuildAsync_DeletesExistingSummaryForYear_BeforeReinserting()
    {
        _db.FundingCommitments.Add(FC("A1", 2023, "BEN-1", eligible: 100m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);
        await Builder().RebuildAsync(fundingYear: 2023); // second run

        // Should not double-insert
        Assert.Equal(1, await _db.ApplicantYearCommitmentSummaries.CountAsync(s => s.FundingYear == 2023));
    }

    // ── Null / blank numeric handling ─────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_NullAmounts_TreatedAsZero()
    {
        _db.FundingCommitments.Add(new FundingCommitment
        {
            RawSourceKey         = "X1",
            FundingRequestNumber = "X1",
            FundingYear          = 2023,
            ApplicantEntityNumber = "BEN-1",
            TotalEligibleAmount  = null,
            CommittedAmount      = null,
            ImportedAtUtc        = DateTime.UtcNow,
            UpdatedAtUtc         = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        // Must not throw
        var result = await Builder().RebuildAsync(fundingYear: 2023);

        Assert.Equal(1, result.SummaryRowsWritten);
        var row = await _db.ApplicantYearCommitmentSummaries.SingleAsync();
        Assert.Equal(0m, row.TotalEligibleAmount);
        Assert.Equal(0m, row.TotalCommittedAmount);
    }

    [Fact]
    public async Task RebuildAsync_MixedNullAndRealAmounts_SumsCorrectly()
    {
        _db.FundingCommitments.AddRange(
            FC("A1", 2023, "BEN-1", eligible: 500m, committed: 450m),
            new FundingCommitment
            {
                RawSourceKey          = "A2",
                FundingRequestNumber  = "A2",
                FundingYear           = 2023,
                ApplicantEntityNumber = "BEN-1",
                TotalEligibleAmount   = null,
                CommittedAmount       = null,
                ImportedAtUtc         = DateTime.UtcNow,
                UpdatedAtUtc          = DateTime.UtcNow,
            }
        );
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearCommitmentSummaries.SingleAsync();
        Assert.Equal(500m, row.TotalEligibleAmount);
        Assert.Equal(450m, row.TotalCommittedAmount);
    }

    // ── Result model ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_ResultModel_HasCorrectCounts()
    {
        _db.FundingCommitments.AddRange(
            FC("A1", 2023, "BEN-1", eligible: 100m),
            FC("A2", 2023, "BEN-1", eligible: 200m),
            FC("B1", 2023, "BEN-2", eligible: 300m)
        );
        await _db.SaveChangesAsync();

        var result = await Builder().RebuildAsync(fundingYear: 2023);

        Assert.Equal(3, result.RawRowsScanned);
        Assert.Equal(2, result.SummaryRowsWritten); // two distinct BENs
        Assert.Equal(600m, result.TotalEligibleAmount);
        Assert.Equal(2023, result.FundingYearScope);
    }

    [Fact]
    public async Task RebuildAsync_AllYears_FundingYearScopeIsNull()
    {
        _db.FundingCommitments.Add(FC("A1", 2023, "BEN-1"));
        await _db.SaveChangesAsync();

        var result = await Builder().RebuildAsync();

        Assert.Null(result.FundingYearScope);
    }

    [Fact]
    public async Task RebuildAsync_EmptyTable_ReturnsZeroAndDoesNotThrow()
    {
        var result = await Builder().RebuildAsync(fundingYear: 2023);
        Assert.Equal(0, result.RawRowsScanned);
        Assert.Equal(0, result.SummaryRowsWritten);
    }
}

// ── FundingCommitmentSummaryLocalProvider tests ───────────────────────────────

public class FundingCommitmentSummaryLocalProviderTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;

    public FundingCommitmentSummaryLocalProviderTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
        _db = new AppDbContext(opts);
        _db.Database.EnsureCreated();
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    private static ApplicantYearCommitmentSummary Sum(
        int year, string? ben, decimal eligible, decimal committed) => new()
    {
        FundingYear           = year,
        ApplicantEntityNumber = ben,
        TotalEligibleAmount   = eligible,
        TotalCommittedAmount  = committed,
        CommitmentRowCount    = 1,
        DistinctFrnCount      = 1,
        ImportedAtUtc         = DateTime.UtcNow,
    };

    [Fact]
    public async Task GetLocalSummaryTotals_GroupsByYear()
    {
        _db.ApplicantYearCommitmentSummaries.AddRange(
            Sum(2023, "BEN-1", 1000m, 900m),
            Sum(2023, "BEN-2", 2000m, 1800m),
            Sum(2024, "BEN-1", 500m,  450m)
        );
        await _db.SaveChangesAsync();

        var provider = new FundingCommitmentSummaryLocalProvider(_db);
        var totals   = await provider.GetLocalSummaryTotalsAsync();

        Assert.Equal(2, totals.Count);
        var t2023 = totals.Single(t => t.FundingYear == 2023);
        Assert.Equal(2L, t2023.RowCount);
        Assert.Equal(3000m, t2023.Amounts["TotalEligibleAmount"]);
        Assert.Equal(2700m, t2023.Amounts["CommittedAmount"]);
    }

    [Fact]
    public async Task GetLocalSummaryTotals_DistinctApplicants_ExcludesNullBen()
    {
        _db.ApplicantYearCommitmentSummaries.AddRange(
            Sum(2023, "BEN-1",  100m, 90m),
            Sum(2023, null,      50m, 45m)  // null BEN catch-all row
        );
        await _db.SaveChangesAsync();

        var provider = new FundingCommitmentSummaryLocalProvider(_db);
        var totals   = await provider.GetLocalSummaryTotalsAsync();

        var t2023 = totals.Single(t => t.FundingYear == 2023);
        Assert.Equal(2L, t2023.RowCount);            // total rows = 2
        Assert.Equal(1L, t2023.DistinctApplicants);  // only 1 has a non-null BEN
    }

    [Fact]
    public async Task GetLocalSummaryTotals_Empty_ReturnsEmpty()
    {
        var provider = new FundingCommitmentSummaryLocalProvider(_db);
        Assert.Empty(await provider.GetLocalSummaryTotalsAsync());
    }
}
