using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Tests;

public class RiskSummaryBuilderTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;

    public RiskSummaryBuilderTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
        _db = new AppDbContext(opts);
        _db.Database.EnsureCreated();
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ApplicantYearCommitmentSummary CS(
        int year, string? ben,
        decimal eligible = 0m, decimal committed = 0m,
        int rowCount = 1, int frnCount = 1,
        string? name = null) => new()
    {
        FundingYear           = year,
        ApplicantEntityNumber = ben,
        ApplicantEntityName   = name,
        TotalEligibleAmount   = eligible,
        TotalCommittedAmount  = committed,
        CommitmentRowCount    = rowCount,
        DistinctFrnCount      = frnCount,
        ImportedAtUtc         = DateTime.UtcNow,
    };

    private static ApplicantYearDisbursementSummary DS(
        int year, string? ben,
        decimal approved = 0m, decimal requested = 0m,
        int rowCount = 1, int frnCount = 1, int invoiceCount = 1,
        string? name = null) => new()
    {
        FundingYear           = year,
        ApplicantEntityNumber = ben,
        ApplicantEntityName   = name,
        TotalApprovedAmount   = approved,
        TotalRequestedAmount  = requested,
        DisbursementRowCount  = rowCount,
        DistinctFrnCount      = frnCount,
        DistinctInvoiceCount  = invoiceCount,
        ImportedAtUtc         = DateTime.UtcNow,
    };

    private ApplicantYearRiskSummaryBuilder Builder() => new(_db);

    // ── Merge: matched row ────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_MatchedRow_CombinesCommitmentAndDisbursementData()
    {
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m, committed: 800m));
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 600m, requested: 700m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(1000m, row.TotalEligibleAmount);
        Assert.Equal(800m,  row.TotalCommittedAmount);
        Assert.Equal(700m,  row.TotalRequestedDisbursementAmount);
        Assert.Equal(600m,  row.TotalApprovedDisbursementAmount);
        Assert.True(row.HasCommitmentData);
        Assert.True(row.HasDisbursementData);
    }

    // ── Merge: commitment-only row ────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_CommitmentOnly_IsIncludedWithZeroDisbursements()
    {
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m, committed: 800m));
        // No disbursement row for BEN-1
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(1000m, row.TotalEligibleAmount);
        Assert.Equal(0m,    row.TotalApprovedDisbursementAmount);
        Assert.True(row.HasCommitmentData);
        Assert.False(row.HasDisbursementData);
    }

    // ── Merge: disbursement-only row ──────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_DisbursementOnly_IsIncludedWithZeroCommitments()
    {
        // No commitment row for BEN-1
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 500m, requested: 600m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0m,   row.TotalEligibleAmount);
        Assert.Equal(500m, row.TotalApprovedDisbursementAmount);
        Assert.False(row.HasCommitmentData);
        Assert.True(row.HasDisbursementData);
    }

    [Fact]
    public async Task RebuildAsync_FullOuterJoin_PreservesAllThreeCombinations()
    {
        _db.ApplicantYearCommitmentSummaries.AddRange(
            CS(2023, "BEN-1", eligible: 1000m, committed: 800m), // will match
            CS(2023, "BEN-2", eligible: 500m,  committed: 400m)  // commitment-only
        );
        _db.ApplicantYearDisbursementSummaries.AddRange(
            DS(2023, "BEN-1", approved: 600m),  // will match
            DS(2023, "BEN-3", approved: 300m)   // disbursement-only
        );
        await _db.SaveChangesAsync();

        var result = await Builder().RebuildAsync(fundingYear: 2023);

        Assert.Equal(3, await _db.ApplicantYearRiskSummaries.CountAsync());
        Assert.Equal(1, result.MatchedRows);
        Assert.Equal(1, result.CommitmentOnlyRows);
        Assert.Equal(1, result.DisbursementOnlyRows);
    }

    // ── ReductionPct ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_ReductionPct_CorrectForMatchedRow()
    {
        // eligible=1000, committed=700 → reduction=(1000-700)/1000=0.3
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m, committed: 700m));
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 500m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0.3, row.ReductionPct, precision: 10);
    }

    [Fact]
    public async Task RebuildAsync_ReductionPct_ZeroWhenEligibleIsZero()
    {
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 0m, committed: 0m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0.0, row.ReductionPct);
    }

    [Fact]
    public async Task RebuildAsync_ReductionPct_ZeroForDisbursementOnlyRow()
    {
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 500m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0.0, row.ReductionPct);
    }

    [Fact]
    public async Task RebuildAsync_ReductionPct_ClampedToOne_WhenCommittedExceedsEligible()
    {
        // Pathological data: committed > eligible should clamp to 0 (not negative)
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 100m, committed: 200m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0.0, row.ReductionPct); // clamped at 0, not negative
    }

    // ── DisbursementPct ───────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_DisbursementPct_CorrectForMatchedRow()
    {
        // committed=800, approved=600 → disbPct=600/800=0.75
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m, committed: 800m));
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 600m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0.75, row.DisbursementPct, precision: 10);
    }

    [Fact]
    public async Task RebuildAsync_DisbursementPct_ZeroForCommitmentOnlyRow()
    {
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m, committed: 800m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0.0, row.DisbursementPct);
    }

    [Fact]
    public async Task RebuildAsync_DisbursementPct_ZeroForDisbursementOnlyRow()
    {
        // No committed amount → DisbursementPct=0
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 500m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0.0, row.DisbursementPct);
    }

    [Fact]
    public async Task RebuildAsync_DisbursementPct_ClampedToOne_WhenApprovedExceedsCommitted()
    {
        // Pathological: approved > committed
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m, committed: 400m));
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 800m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(1.0, row.DisbursementPct); // clamped at 1
    }

    // ── RiskScore ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_RiskScore_ComputedCorrectly()
    {
        // eligible=1000, committed=700 → redPct=0.3
        // committed=700, approved=350 → disbPct=0.5
        // score = 0.5*0.3 + 0.5*(1-0.5) = 0.15 + 0.25 = 0.4
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m, committed: 700m));
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 350m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0.4, row.RiskScore, precision: 10);
    }

    [Fact]
    public async Task RebuildAsync_RiskScore_CommitmentOnly_IsAtLeastHalf()
    {
        // CommitmentOnly: disbPct=0 → score = 0.5*redPct + 0.5*(1-0) = 0.5*redPct + 0.5
        // For any redPct in [0,1], score ∈ [0.5, 1.0]
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m, committed: 800m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.True(row.RiskScore >= 0.5, $"CommitmentOnly score should be ≥ 0.5, was {row.RiskScore}");
    }

    [Fact]
    public async Task RebuildAsync_RiskScore_DisbursementOnly_IsExactlyHalf()
    {
        // DisbursementOnly: redPct=0, disbPct=0 → score = 0.5*0 + 0.5*(1-0) = 0.5
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 500m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0.5, row.RiskScore, precision: 10);
    }

    // ── RiskLevel classification ───────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_RiskLevel_High_WhenScoreAbove0Point6()
    {
        // eligible=1000, committed=200 → redPct=0.8
        // committed=200, approved=0 → disbPct=0
        // score = 0.5*0.8 + 0.5*1 = 0.9 → High
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m, committed: 200m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal("High", row.RiskLevel);
    }

    [Fact]
    public async Task RebuildAsync_RiskLevel_Moderate_WhenScoreBetween0Point3And0Point6()
    {
        // score = 0.4 (verified above) → Moderate
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m, committed: 700m));
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 350m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal("Moderate", row.RiskLevel);
    }

    [Fact]
    public async Task RebuildAsync_RiskLevel_Low_WhenFullyCommittedAndFullyDisbursed()
    {
        // eligible=committed → redPct=0; committed=approved → disbPct=1
        // score = 0.5*0 + 0.5*(1-1) = 0 → Low
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m, committed: 1000m));
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 1000m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal("Low", row.RiskLevel);
        Assert.Equal(0.0, row.RiskScore, precision: 10);
    }

    // ── Year-scoped rebuild ───────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_YearScoped_OnlyAffectsSpecifiedYear()
    {
        _db.ApplicantYearCommitmentSummaries.AddRange(
            CS(2023, "BEN-1", eligible: 1000m, committed: 800m),
            CS(2024, "BEN-1", eligible: 500m,  committed: 400m)
        );
        await _db.SaveChangesAsync();
        await Builder().RebuildAsync(); // all years

        // Modify 2024 commitment and rebuild only 2024
        var c2024 = _db.ApplicantYearCommitmentSummaries.Single(s => s.FundingYear == 2024);
        c2024.TotalEligibleAmount = 999m;
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2024);

        var r2023 = await _db.ApplicantYearRiskSummaries.SingleAsync(r => r.FundingYear == 2023);
        Assert.Equal(1000m, r2023.TotalEligibleAmount); // unchanged

        var r2024 = await _db.ApplicantYearRiskSummaries.SingleAsync(r => r.FundingYear == 2024);
        Assert.Equal(999m, r2024.TotalEligibleAmount); // updated
    }

    [Fact]
    public async Task RebuildAsync_DeletesExistingRowsForYear_BeforeReinserting()
    {
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);
        await Builder().RebuildAsync(fundingYear: 2023); // second run

        Assert.Equal(1, await _db.ApplicantYearRiskSummaries.CountAsync(r => r.FundingYear == 2023));
    }

    // ── Applicant name selection ──────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_Name_PrefersCommitmentName_WhenBothPresent()
    {
        // commitment name alphabetically first → should win (MIN rule)
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", name: "Apple School"));
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", name: "Zebra District"));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal("Apple School", row.ApplicantEntityName);
    }

    [Fact]
    public async Task RebuildAsync_Name_UsesDisbursementName_WhenCommitmentNameIsNull()
    {
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", name: null));
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", name: "Zebra District"));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal("Zebra District", row.ApplicantEntityName);
    }

    [Fact]
    public async Task RebuildAsync_Name_UsesMinName_WhenDisbursementNameAlphabeticallyFirst()
    {
        // Disbursement name is alphabetically earlier → MIN wins
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", name: "Zebra School"));
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", name: "Apple District"));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal("Apple District", row.ApplicantEntityName);
    }

    [Fact]
    public async Task RebuildAsync_Name_DisbursementOnlyRow_UsesDisbursementName()
    {
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", name: "Only Disbursement School"));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal("Only Disbursement School", row.ApplicantEntityName);
    }

    // ── Result model ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_ResultModel_CountsAreCorrect()
    {
        _db.ApplicantYearCommitmentSummaries.AddRange(
            CS(2023, "BEN-1"),  // matched
            CS(2023, "BEN-2")   // commitment-only
        );
        _db.ApplicantYearDisbursementSummaries.AddRange(
            DS(2023, "BEN-1"),  // matched
            DS(2023, "BEN-3")   // disbursement-only
        );
        await _db.SaveChangesAsync();

        var result = await Builder().RebuildAsync(fundingYear: 2023);

        Assert.Equal(2, result.CommitmentSummaryRowsRead);
        Assert.Equal(2, result.DisbursementSummaryRowsRead);
        Assert.Equal(1, result.MatchedRows);
        Assert.Equal(1, result.CommitmentOnlyRows);
        Assert.Equal(1, result.DisbursementOnlyRows);
        Assert.Equal(3, result.RiskSummaryRowsWritten);
        Assert.Equal(2023, result.FundingYearScope);
    }

    [Fact]
    public async Task RebuildAsync_AllYears_FundingYearScopeIsNull()
    {
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1"));
        await _db.SaveChangesAsync();

        var result = await Builder().RebuildAsync();

        Assert.Null(result.FundingYearScope);
    }

    [Fact]
    public async Task RebuildAsync_EmptyTables_ReturnsZeroAndDoesNotThrow()
    {
        var result = await Builder().RebuildAsync(fundingYear: 2023);
        Assert.Equal(0, result.RiskSummaryRowsWritten);
        Assert.Equal(0, result.MatchedRows);
        Assert.Equal(0, result.CommitmentOnlyRows);
        Assert.Equal(0, result.DisbursementOnlyRows);
    }

    // ── Sparse-data safety (CC-ERATE-000007) ─────────────────────────────────

    [Fact]
    public async Task RebuildAsync_ZeroAmountCommitmentOnlyRow_ScoreIsHalfAndLevelIsModerate()
    {
        // Commitment row with all-zero amounts (e.g., partial-year data not yet populated).
        // ReductionPct(0,0)=0, DisbursementPct(0,0)=0 → score=0.5 → Moderate.
        // Guards against divide-by-zero exceptions in the risk calculator.
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 0m, committed: 0m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0.5, row.RiskScore, precision: 10);
        Assert.Equal("Moderate", row.RiskLevel);
        Assert.True(row.HasCommitmentData);
        Assert.False(row.HasDisbursementData);
        Assert.Equal(0.0, row.ReductionPct);
    }

    [Fact]
    public async Task RebuildAsync_ZeroAmountDisbursementOnlyRow_ScoreIsHalfAndLevelIsModerate()
    {
        // Disbursement-only row with zero approved amount (anomalous data or sparse load).
        // Disbursement-only path always uses redPct=0, disbPct=0 → score=0.5 → Moderate.
        // Guards against exceptions when ApprovedAmount=0 bypasses the summary inclusion rule.
        _db.ApplicantYearDisbursementSummaries.Add(DS(2023, "BEN-1", approved: 0m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(0.5, row.RiskScore, precision: 10);
        Assert.Equal("Moderate", row.RiskLevel);
        Assert.False(row.HasCommitmentData);
        Assert.True(row.HasDisbursementData);
    }

    // ── Cross-year isolation ──────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_DifferentYearsSameKey_NotMergedAcrossYears()
    {
        // Same BEN in different years must remain separate rows, not joined
        _db.ApplicantYearCommitmentSummaries.Add(CS(2023, "BEN-1", eligible: 1000m));
        _db.ApplicantYearDisbursementSummaries.Add(DS(2024, "BEN-1", approved: 500m));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync();

        var rows = await _db.ApplicantYearRiskSummaries.OrderBy(r => r.FundingYear).ToListAsync();
        Assert.Equal(2, rows.Count);

        var r2023 = rows[0];
        Assert.Equal(2023, r2023.FundingYear);
        Assert.True(r2023.HasCommitmentData);
        Assert.False(r2023.HasDisbursementData);

        var r2024 = rows[1];
        Assert.Equal(2024, r2024.FundingYear);
        Assert.False(r2024.HasCommitmentData);
        Assert.True(r2024.HasDisbursementData);
    }

    // ── Counter fields ────────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_CounterFields_CopiedFromSourceRows()
    {
        _db.ApplicantYearCommitmentSummaries.Add(
            CS(2023, "BEN-1", rowCount: 7, frnCount: 3));
        _db.ApplicantYearDisbursementSummaries.Add(
            DS(2023, "BEN-1", rowCount: 12, frnCount: 5, invoiceCount: 4));
        await _db.SaveChangesAsync();

        await Builder().RebuildAsync(fundingYear: 2023);

        var row = await _db.ApplicantYearRiskSummaries.SingleAsync();
        Assert.Equal(7,  row.CommitmentRowCount);
        Assert.Equal(3,  row.DistinctCommitmentFrnCount);
        Assert.Equal(12, row.DisbursementRowCount);
        Assert.Equal(5,  row.DistinctDisbursementFrnCount);
        Assert.Equal(4,  row.DistinctInvoiceCount);
    }
}
