using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Tests;

// ===========================================================================
// Unit tests — RiskCalculator static methods
// ===========================================================================

public class RiskCalculatorTests
{
    // -----------------------------------------------------------------------
    // ReductionPct
    // -----------------------------------------------------------------------

    [Fact]
    public void ReductionPct_ZeroRequested_ReturnsZero()
        => Assert.Equal(0.0, RiskCalculator.ReductionPct(0, 1000));

    [Fact]
    public void ReductionPct_FullyApproved_ReturnsZero()
        => Assert.Equal(0.0, RiskCalculator.ReductionPct(1000, 1000), 6);

    [Fact]
    public void ReductionPct_FullyDenied_ReturnsOne()
        => Assert.Equal(1.0, RiskCalculator.ReductionPct(1000, 0), 6);

    [Fact]
    public void ReductionPct_HalfApproved_ReturnsPointFive()
        => Assert.Equal(0.5, RiskCalculator.ReductionPct(1000, 500), 6);

    [Fact]
    public void ReductionPct_CommittedExceedsRequested_ClampsToZero()
        // Edge case: committed > requested should not produce negative reduction
        => Assert.Equal(0.0, RiskCalculator.ReductionPct(500, 1000));

    [Fact]
    public void ReductionPct_NegativeRequested_ReturnsZero()
        => Assert.Equal(0.0, RiskCalculator.ReductionPct(-100, 0));

    // -----------------------------------------------------------------------
    // DisbursementPct
    // -----------------------------------------------------------------------

    [Fact]
    public void DisbursementPct_ZeroCommitted_ReturnsZero()
        => Assert.Equal(0.0, RiskCalculator.DisbursementPct(0, 500));

    [Fact]
    public void DisbursementPct_FullyDisbursed_ReturnsOne()
        => Assert.Equal(1.0, RiskCalculator.DisbursementPct(1000, 1000), 6);

    [Fact]
    public void DisbursementPct_NothingDisbursed_ReturnsZero()
        => Assert.Equal(0.0, RiskCalculator.DisbursementPct(1000, 0), 6);

    [Fact]
    public void DisbursementPct_HalfDisbursed_ReturnsPointFive()
        => Assert.Equal(0.5, RiskCalculator.DisbursementPct(1000, 500), 6);

    [Fact]
    public void DisbursementPct_DisbursedExceedsCommitted_ClampsToOne()
        // Over-disbursement (e.g., data join artifact) should clamp at 100%
        => Assert.Equal(1.0, RiskCalculator.DisbursementPct(500, 1000));

    // -----------------------------------------------------------------------
    // ComputeRiskScore
    // -----------------------------------------------------------------------

    [Fact]
    public void ComputeRiskScore_PerfectExecution_ReturnsZero()
    {
        // reduction = 0, disbursement = 1 → score = 0.5×0 + 0.5×(1-1) = 0
        Assert.Equal(0.0, RiskCalculator.ComputeRiskScore(0.0, 1.0), 6);
    }

    [Fact]
    public void ComputeRiskScore_TotalFailure_ReturnsOne()
    {
        // reduction = 1, disbursement = 0 → score = 0.5×1 + 0.5×1 = 1
        Assert.Equal(1.0, RiskCalculator.ComputeRiskScore(1.0, 0.0), 6);
    }

    [Fact]
    public void ComputeRiskScore_HalfReductionFullDisbursement_ReturnsPointTwentyFive()
    {
        // reduction = 0.5, disbursement = 1.0 → 0.5×0.5 + 0.5×0 = 0.25
        Assert.Equal(0.25, RiskCalculator.ComputeRiskScore(0.5, 1.0), 6);
    }

    [Fact]
    public void ComputeRiskScore_NoReductionNoDisbursement_ReturnsPointFive()
    {
        // reduction = 0, disbursement = 0 → 0.5×0 + 0.5×1 = 0.5
        Assert.Equal(0.5, RiskCalculator.ComputeRiskScore(0.0, 0.0), 6);
    }

    [Fact]
    public void ComputeRiskScore_IsAlwaysBetweenZeroAndOne()
    {
        var cases = new[] { (0.0, 0.0), (1.0, 1.0), (0.5, 0.5), (-1.0, 2.0) };
        foreach (var (r, d) in cases)
        {
            var score = RiskCalculator.ComputeRiskScore(r, d);
            Assert.InRange(score, 0.0, 1.0);
        }
    }

    // -----------------------------------------------------------------------
    // ClassifyRisk
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0.0,  "Low")]
    [InlineData(0.29, "Low")]
    [InlineData(0.3,  "Low")]       // boundary: > 0.3 is moderate, so 0.3 exactly is Low
    [InlineData(0.31, "Moderate")]
    [InlineData(0.6,  "Moderate")]  // boundary: > 0.6 is High, so 0.6 exactly is Moderate
    [InlineData(0.61, "High")]
    [InlineData(1.0,  "High")]
    public void ClassifyRisk_ReturnsCorrectBand(double score, string expected)
        => Assert.Equal(expected, RiskCalculator.ClassifyRisk(score));
}

// ===========================================================================
// Unit tests — RiskSnapshot derived properties
// ===========================================================================

public class RiskSnapshotDerivedTests
{
    private static RiskSnapshot Make(decimal req, decimal com, decimal disb)
        => new(req, com, disb, 0, 0);

    // -----------------------------------------------------------------------
    // ReductionAmount
    // -----------------------------------------------------------------------

    [Fact]
    public void ReductionAmount_IsRequestedMinusCommitted()
    {
        var snap = Make(15000m, 12000m, 8000m);
        Assert.Equal(3000m, snap.ReductionAmount);
    }

    [Fact]
    public void ReductionAmount_WhenFullyCommitted_IsZero()
    {
        var snap = Make(10000m, 10000m, 5000m);
        Assert.Equal(0m, snap.ReductionAmount);
    }

    [Fact]
    public void ReductionAmount_ClampsToZeroWhenCommittedExceedsRequested()
    {
        // Data artifact: should never be negative
        var snap = Make(5000m, 6000m, 4000m);
        Assert.Equal(0m, snap.ReductionAmount);
    }

    [Fact]
    public void ReductionAmount_AllZero_IsZero()
    {
        var snap = Make(0m, 0m, 0m);
        Assert.Equal(0m, snap.ReductionAmount);
    }

    // -----------------------------------------------------------------------
    // UndisbursedAmount
    // -----------------------------------------------------------------------

    [Fact]
    public void UndisbursedAmount_IsCommittedMinusDisbursed()
    {
        var snap = Make(15000m, 12000m, 8000m);
        Assert.Equal(4000m, snap.UndisbursedAmount);
    }

    [Fact]
    public void UndisbursedAmount_WhenFullyDisbursed_IsZero()
    {
        var snap = Make(10000m, 8000m, 8000m);
        Assert.Equal(0m, snap.UndisbursedAmount);
    }

    [Fact]
    public void UndisbursedAmount_ClampsToZeroWhenDisbursedExceedsCommitted()
    {
        // Data artifact: disbursed > committed should not produce negative undisbursed
        var snap = Make(10000m, 5000m, 6000m);
        Assert.Equal(0m, snap.UndisbursedAmount);
    }

    // -----------------------------------------------------------------------
    // Both together (flow adds up)
    // -----------------------------------------------------------------------

    [Fact]
    public void ReductionPlusCommitted_EqualsTotalRequested_WhenNoClamp()
    {
        var snap = Make(10000m, 7000m, 5000m);
        Assert.Equal(snap.TotalRequested, snap.ReductionAmount + snap.TotalCommitted);
    }

    [Fact]
    public void UndisbursedPlusDisbursed_EqualsCommitted_WhenNoClamp()
    {
        var snap = Make(10000m, 7000m, 5000m);
        Assert.Equal(snap.TotalCommitted, snap.UndisbursedAmount + snap.TotalDisbursed);
    }
}

// ===========================================================================
// Integration tests — RiskInsightsRepository query methods
// ===========================================================================

public class RiskInsightsRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly RiskInsightsRepository _repo;

    public RiskInsightsRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new RiskInsightsRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private FundingCommitment Commitment(string frn, string? ben,
        decimal? eligible = null, decimal? committed = null) => new()
    {
        RawSourceKey = $"{frn}-1",
        FundingRequestNumber = frn,
        ApplicantEntityNumber = ben,
        ApplicantName = ben != null ? $"District {ben}" : null,
        TotalEligibleAmount = eligible,
        CommittedAmount = committed,
        FundingYear = 2024,
        ImportedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };

    private Disbursement Disbursement(string frn, string? ben, decimal? approved = null) => new()
    {
        RawSourceKey = $"{frn}-INV-1",
        FundingRequestNumber = frn,
        ApplicantEntityNumber = ben,
        FundingYear = 2024,
        ApprovedAmount = approved,
        ImportedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };

    // -----------------------------------------------------------------------
    // GetSnapshotAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSnapshot_ReturnsCorrectTotals()
    {
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", eligible: 10000m, committed: 8000m),
            Commitment("FRN002", "BEN002", eligible: 5000m,  committed: 5000m));
        _db.Disbursements.Add(Disbursement("FRN001", "BEN001", approved: 7000m));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(15000m, snap.TotalRequested);
        Assert.Equal(13000m, snap.TotalCommitted);
        Assert.Equal(7000m,  snap.TotalDisbursed);
    }

    [Fact]
    public async Task GetSnapshot_CommitmentFulfillmentRate_IsCorrect()
    {
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 10000m, committed: 8000m));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(0.8, snap.CommitmentFulfillmentRate, 6);
    }

    [Fact]
    public async Task GetSnapshot_DisbursementCompletionRate_IsCorrect()
    {
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", committed: 10000m));
        _db.Disbursements.Add(Disbursement("FRN001", "BEN001", approved: 7500m));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(0.75, snap.DisbursementCompletionRate, 6);
    }

    [Fact]
    public async Task GetSnapshot_EmptyDatabase_ReturnsZeros()
    {
        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(0m,  snap.TotalRequested);
        Assert.Equal(0m,  snap.TotalCommitted);
        Assert.Equal(0m,  snap.TotalDisbursed);
        Assert.Equal(0.0, snap.CommitmentFulfillmentRate);
        Assert.Equal(0.0, snap.DisbursementCompletionRate);
    }

    // -----------------------------------------------------------------------
    // GetTopRiskApplicantsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopRiskApplicants_RiskScoreCalculatedCorrectly()
    {
        // BEN001: eligible=10000, committed=0 → reduction=1.0, no disbursements → disbPct=0.0
        //   score = 0.5×1.0 + 0.5×1.0 = 1.0  → High
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 10000m, committed: 0m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(5);

        Assert.Single(rows);
        Assert.Equal("BEN001", rows[0].EntityNumber);
        Assert.Equal(1.0, rows[0].RiskScore, 6);
        Assert.Equal("High", rows[0].RiskLevel);
    }

    [Fact]
    public async Task GetTopRiskApplicants_OrderedByRiskScoreDescending()
    {
        // BEN001: low risk (fully approved + fully disbursed)
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 1000m, committed: 1000m));
        _db.Disbursements.Add(Disbursement("FRN001", "BEN001", approved: 1000m));
        // BEN002: high risk (fully denied, no disbursement)
        _db.FundingCommitments.Add(Commitment("FRN002", "BEN002", eligible: 1000m, committed: 0m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(10);

        Assert.Equal("BEN002", rows[0].EntityNumber);   // highest risk first
        Assert.Equal("BEN001", rows[1].EntityNumber);
    }

    [Fact]
    public async Task GetTopRiskApplicants_TopNLimitsResults()
    {
        _db.FundingCommitments.AddRange(Enumerable.Range(1, 10).Select(i =>
            Commitment($"FRN{i:D3}", $"BEN{i:D3}", eligible: 1000m, committed: 0m)));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(3);

        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task GetTopRiskApplicants_DisbursementPctCalculatedCorrectly()
    {
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 2000m, committed: 2000m));
        _db.Disbursements.Add(Disbursement("FRN001", "BEN001", approved: 1000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(5);

        Assert.Single(rows);
        Assert.Equal(0.0,  rows[0].ReductionPct, 6);        // no reduction
        Assert.Equal(0.5,  rows[0].DisbursementPct, 6);     // 1000/2000 = 0.5
        Assert.Equal(0.25, rows[0].RiskScore, 6);           // 0.5×0 + 0.5×0.5 = 0.25
        Assert.Equal("Low", rows[0].RiskLevel);
    }

    // -----------------------------------------------------------------------
    // GetTopCommitmentDisbursementGapsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopGaps_OrderedByGapDescending()
    {
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", committed: 5000m),
            Commitment("FRN002", "BEN002", committed: 2000m));
        _db.Disbursements.AddRange(
            Disbursement("FRN001", "BEN001", approved: 1000m),   // gap = 4000
            Disbursement("FRN002", "BEN002", approved: 1500m));   // gap = 500
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopCommitmentDisbursementGapsAsync(10);

        Assert.Equal("BEN001", rows[0].EntityNumber);
        Assert.Equal(4000m,    rows[0].Gap);
        Assert.Equal("BEN002", rows[1].EntityNumber);
        Assert.Equal(500m,     rows[1].Gap);
    }

    [Fact]
    public async Task GetTopGaps_ExcludesNonPositiveGaps()
    {
        // BEN001: fully disbursed → no gap
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", committed: 1000m));
        _db.Disbursements.Add(Disbursement("FRN001", "BEN001", approved: 1000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopCommitmentDisbursementGapsAsync(10);

        Assert.Empty(rows);
    }

    // -----------------------------------------------------------------------
    // GetTopReductionRatesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopReductions_OrderedByReductionPctDescending()
    {
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", eligible: 10000m, committed: 2000m),  // 80% reduction
            Commitment("FRN002", "BEN002", eligible: 10000m, committed: 7000m)); // 30% reduction
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopReductionRatesAsync(10);

        Assert.Equal("BEN001", rows[0].EntityNumber);
        Assert.Equal(0.8, rows[0].ReductionPct, 6);
    }

    [Fact]
    public async Task GetTopReductions_ExcludesSmallEligibleAmounts()
    {
        // BEN001: eligible = 50 (below $100 threshold) — should be excluded
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 50m, committed: 0m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopReductionRatesAsync(10);

        Assert.Empty(rows);
    }

    // -----------------------------------------------------------------------
    // Year filter — disbursement year isolation (regression guard)
    // The old BEN-proxy approach fetched all disbursements for a BEN regardless of
    // year, so filtering FY 2024 commitments would pull in 2023 disbursements too.
    // These tests verify that disbursements are filtered by their own FundingYear.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSnapshot_YearFilter_ExcludesDisbursementsFromOtherYears()
    {
        // BEN001 has a commitment in 2024 and disbursements in BOTH 2023 and 2024.
        // When filtering by 2024, only the 2024 disbursement ($3,000) should count.
        _db.FundingCommitments.Add(CommitmentYear("FRN001", "BEN001", 2024, eligible: 5000m, committed: 5000m));
        _db.Disbursements.AddRange(
            new Disbursement { RawSourceKey = "FRN001-INV-2023", FundingRequestNumber = "FRN001",
                ApplicantEntityNumber = "BEN001", FundingYear = 2023,
                ApprovedAmount = 9000m, ImportedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Disbursement { RawSourceKey = "FRN001-INV-2024", FundingRequestNumber = "FRN001",
                ApplicantEntityNumber = "BEN001", FundingYear = 2024,
                ApprovedAmount = 3000m, ImportedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync(year: 2024);

        Assert.Equal(5000m, snap.TotalCommitted);
        Assert.Equal(3000m, snap.TotalDisbursed);   // must NOT include the 2023 $9,000
    }

    [Fact]
    public async Task GetTopRiskApplicants_YearFilter_ExcludesDisbursementsFromOtherYears()
    {
        // BEN001 commitment FY2024 ($2,000 committed). Old 2023 disbursement of $9,000
        // must NOT inflate disbPct beyond 1.0 or appear in the 2024-filtered result.
        _db.FundingCommitments.Add(CommitmentYear("FRN001", "BEN001", 2024, eligible: 2000m, committed: 2000m));
        _db.Disbursements.AddRange(
            new Disbursement { RawSourceKey = "FRN001-INV-2023", FundingRequestNumber = "FRN001",
                ApplicantEntityNumber = "BEN001", FundingYear = 2023,
                ApprovedAmount = 9000m, ImportedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Disbursement { RawSourceKey = "FRN001-INV-2024", FundingRequestNumber = "FRN001",
                ApplicantEntityNumber = "BEN001", FundingYear = 2024,
                ApprovedAmount = 1000m, ImportedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, year: 2024);

        Assert.Single(rows);
        Assert.Equal(1000m, rows[0].Disbursed);     // only FY2024 disbursement
        Assert.Equal(0.5, rows[0].DisbursementPct, 6);  // 1000/2000 = 0.5, not 5.0
    }

    [Fact]
    public async Task GetTopGaps_YearFilter_ExcludesDisbursementsFromOtherYears()
    {
        // BEN001 committed $5,000 in 2024. Has $4,000 disbursement from 2023 (wrong year)
        // and $1,000 from 2024 (correct). Gap should be $4,000, not $1,000.
        _db.FundingCommitments.Add(CommitmentYear("FRN001", "BEN001", 2024, committed: 5000m));
        _db.Disbursements.AddRange(
            new Disbursement { RawSourceKey = "FRN001-INV-2023", FundingRequestNumber = "FRN001",
                ApplicantEntityNumber = "BEN001", FundingYear = 2023,
                ApprovedAmount = 4000m, ImportedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Disbursement { RawSourceKey = "FRN001-INV-2024", FundingRequestNumber = "FRN001",
                ApplicantEntityNumber = "BEN001", FundingYear = 2024,
                ApprovedAmount = 1000m, ImportedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopCommitmentDisbursementGapsAsync(10, year: 2024);

        Assert.Single(rows);
        Assert.Equal(1000m, rows[0].Disbursed);
        Assert.Equal(4000m, rows[0].Gap);           // must NOT be reduced by the 2023 payment
    }

    // -----------------------------------------------------------------------
    // GetAvailableYearsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAvailableYears_ReturnsDistinctYearsDescending()
    {
        _db.FundingCommitments.AddRange(
            CommitmentYear("FRN001", "BEN001", 2023, 1000m, 800m),
            CommitmentYear("FRN002", "BEN002", 2024, 2000m, 2000m),
            CommitmentYear("FRN003", "BEN003", 2024, 500m,  400m),   // duplicate year 2024
            CommitmentYear("FRN004", "BEN004", 2022, 300m,  300m));
        await _db.SaveChangesAsync();

        var years = await _repo.GetAvailableYearsAsync();

        Assert.Equal([2024, 2023, 2022], years);
    }

    [Fact]
    public async Task GetAvailableYears_EmptyDatabase_ReturnsEmptyList()
    {
        var years = await _repo.GetAvailableYearsAsync();
        Assert.Empty(years);
    }

    // -----------------------------------------------------------------------
    // Year filter — GetSnapshotAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSnapshot_YearFilter_NarrowsTotals()
    {
        _db.FundingCommitments.AddRange(
            CommitmentYear("FRN001", "BEN001", 2023, eligible: 10000m, committed: 8000m),
            CommitmentYear("FRN002", "BEN002", 2024, eligible:  5000m, committed: 5000m));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync(year: 2024);

        Assert.Equal(5000m, snap.TotalRequested);
        Assert.Equal(5000m, snap.TotalCommitted);
    }

    [Fact]
    public async Task GetSnapshot_YearFilter_NoMatchingYear_ReturnsZeros()
    {
        _db.FundingCommitments.Add(CommitmentYear("FRN001", "BEN001", 2023, eligible: 10000m, committed: 8000m));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync(year: 2099);

        Assert.Equal(0m, snap.TotalRequested);
        Assert.Equal(0m, snap.TotalCommitted);
    }

    // -----------------------------------------------------------------------
    // Year filter — GetTopRiskApplicantsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopRiskApplicants_YearFilter_ExcludesOtherYears()
    {
        // BEN001 has commitments in 2023 only; BEN002 in 2024 only.
        _db.FundingCommitments.AddRange(
            CommitmentYear("FRN001", "BEN001", 2023, eligible: 1000m, committed: 0m),
            CommitmentYear("FRN002", "BEN002", 2024, eligible: 1000m, committed: 0m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, year: 2024);

        Assert.Single(rows);
        Assert.Equal("BEN002", rows[0].EntityNumber);
    }

    // -----------------------------------------------------------------------
    // Severity filter — GetTopRiskApplicantsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopRiskApplicants_SeverityHigh_ReturnsOnlyHighRisk()
    {
        // BEN001: fully denied → score = 1.0 → High
        // BEN002: fully approved + fully disbursed → score = 0.0 → Low
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", eligible: 1000m, committed: 0m),
            Commitment("FRN002", "BEN002", eligible: 1000m, committed: 1000m));
        _db.Disbursements.Add(Disbursement("FRN002", "BEN002", approved: 1000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, severity: "High");

        Assert.Single(rows);
        Assert.Equal("BEN001", rows[0].EntityNumber);
        Assert.Equal("High", rows[0].RiskLevel);
    }

    [Fact]
    public async Task GetTopRiskApplicants_SeverityLow_ReturnsOnlyLowRisk()
    {
        // BEN001: high risk; BEN002: low risk
        _db.FundingCommitments.AddRange(
            Commitment("FRN001", "BEN001", eligible: 1000m, committed: 0m),
            Commitment("FRN002", "BEN002", eligible: 1000m, committed: 1000m));
        _db.Disbursements.Add(Disbursement("FRN002", "BEN002", approved: 1000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, severity: "Low");

        Assert.Single(rows);
        Assert.Equal("BEN002", rows[0].EntityNumber);
        Assert.Equal("Low", rows[0].RiskLevel);
    }

    [Fact]
    public async Task GetTopRiskApplicants_SeverityFilter_CaseInsensitive()
    {
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 1000m, committed: 0m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, severity: "high");

        Assert.Single(rows);
        Assert.Equal("High", rows[0].RiskLevel);
    }

    [Fact]
    public async Task GetTopRiskApplicants_SeverityFilter_NoMatch_ReturnsEmpty()
    {
        // Only high-risk entities in DB — asking for Moderate should yield nothing.
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 1000m, committed: 0m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, severity: "Moderate");

        Assert.Empty(rows);
    }

    // -----------------------------------------------------------------------
    // Combined year + severity filter
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopRiskApplicants_YearAndSeverity_IntersectsFilters()
    {
        // BEN001: FY2023, high risk
        // BEN002: FY2024, high risk
        // BEN003: FY2024, low risk
        _db.FundingCommitments.AddRange(
            CommitmentYear("FRN001", "BEN001", 2023, eligible: 1000m, committed: 0m),
            CommitmentYear("FRN002", "BEN002", 2024, eligible: 1000m, committed: 0m),
            CommitmentYear("FRN003", "BEN003", 2024, eligible: 1000m, committed: 1000m));
        _db.Disbursements.Add(
            new Disbursement
            {
                RawSourceKey = "FRN003-INV-1",
                FundingRequestNumber = "FRN003",
                ApplicantEntityNumber = "BEN003",
                FundingYear = 2024,
                ApprovedAmount = 1000m,
                ImportedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, year: 2024, severity: "High");

        Assert.Single(rows);
        Assert.Equal("BEN002", rows[0].EntityNumber);
    }

    // -----------------------------------------------------------------------
    // Year filter — GetTopCommitmentDisbursementGapsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopGaps_YearFilter_ExcludesOtherYears()
    {
        _db.FundingCommitments.AddRange(
            CommitmentYear("FRN001", "BEN001", 2023, committed: 5000m),
            CommitmentYear("FRN002", "BEN002", 2024, committed: 3000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopCommitmentDisbursementGapsAsync(10, year: 2024);

        Assert.Single(rows);
        Assert.Equal("BEN002", rows[0].EntityNumber);
    }

    // -----------------------------------------------------------------------
    // Year filter — GetTopReductionRatesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopReductions_YearFilter_ExcludesOtherYears()
    {
        _db.FundingCommitments.AddRange(
            CommitmentYear("FRN001", "BEN001", 2023, eligible: 10000m, committed: 0m),
            CommitmentYear("FRN002", "BEN002", 2024, eligible: 10000m, committed: 0m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopReductionRatesAsync(10, year: 2024);

        Assert.Single(rows);
        Assert.Equal("BEN002", rows[0].EntityNumber);
    }

    // -----------------------------------------------------------------------
    // InvoiceLineStatus filter — root cause of inflated disbursement totals
    //
    // The USAC jpiu-tj8h dataset populates approved_inv_line_amt for ALL line item
    // statuses, including Pending (proposed amount under review) and Not Approved
    // (amount that was denied). Summing without a status filter inflates TotalDisbursed
    // well beyond TotalCommitted. The repository must only include Approved (or null-
    // status) records so that disbursed totals represent money USAC actually paid.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSnapshot_NullStatus_IsIncluded()
    {
        // Records with null InvoiceLineStatus (older USAC exports) must be treated as paid.
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 5000m, committed: 5000m));
        _db.Disbursements.Add(DisbursementWithStatus("FRN001", "BEN001", 4000m, status: null));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(4000m, snap.TotalDisbursed);
    }

    [Fact]
    public async Task GetSnapshot_ApprovedStatus_IsIncluded()
    {
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 5000m, committed: 5000m));
        _db.Disbursements.Add(DisbursementWithStatus("FRN001", "BEN001", 4000m, status: "Approved"));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(4000m, snap.TotalDisbursed);
    }

    [Fact]
    public async Task GetSnapshot_ApprovedStatus_CaseInsensitive_IsIncluded()
    {
        // USAC data has been observed with "APPROVED" and "approved" casing variants.
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 5000m, committed: 5000m));
        _db.Disbursements.AddRange(
            DisbursementWithStatus("FRN001-A", "BEN001", 1000m, status: "APPROVED"),
            DisbursementWithStatus("FRN001-B", "BEN001", 1000m, status: "approved"));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(2000m, snap.TotalDisbursed);
    }

    [Fact]
    public async Task GetSnapshot_PendingStatus_IsExcluded()
    {
        // A Pending line carries a proposed approval amount — money not yet paid.
        // Including it would inflate TotalDisbursed above TotalCommitted.
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 5000m, committed: 5000m));
        _db.Disbursements.AddRange(
            DisbursementWithStatus("FRN001-A", "BEN001", 3000m, status: "Approved"),
            DisbursementWithStatus("FRN001-B", "BEN001", 2000m, status: "Pending"));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        // Only the Approved $3,000 should count — not the Pending $2,000.
        Assert.Equal(3000m, snap.TotalDisbursed);
    }

    [Fact]
    public async Task GetSnapshot_NotApprovedStatus_IsExcluded()
    {
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 5000m, committed: 5000m));
        _db.Disbursements.AddRange(
            DisbursementWithStatus("FRN001-A", "BEN001", 3000m, status: "Approved"),
            DisbursementWithStatus("FRN001-B", "BEN001", 1500m, status: "Not Approved"));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(3000m, snap.TotalDisbursed);
    }

    [Fact]
    public async Task GetSnapshot_CancelledStatus_IsExcluded()
    {
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 5000m, committed: 5000m));
        _db.Disbursements.AddRange(
            DisbursementWithStatus("FRN001-A", "BEN001", 3000m, status: "Approved"),
            DisbursementWithStatus("FRN001-B", "BEN001", 1500m, status: "Cancelled"));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(3000m, snap.TotalDisbursed);
    }

    [Fact]
    public async Task GetSnapshot_DisbursedDoesNotExceedCommitted_WithApprovedStatusFilter()
    {
        // Core invariant: for a single FRN with approved invoices that are a subset
        // of the committed amount, TotalDisbursed must be <= TotalCommitted.
        // Without the status filter, Pending rows would violate this.
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 10000m, committed: 8000m));
        _db.Disbursements.AddRange(
            DisbursementWithStatus("FRN001-A", "BEN001", 5000m, status: "Approved"),
            DisbursementWithStatus("FRN001-B", "BEN001", 3000m, status: "Pending"),   // should be excluded
            DisbursementWithStatus("FRN001-C", "BEN001", 1000m, status: "Not Approved")); // should be excluded
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(8000m, snap.TotalCommitted);
        Assert.Equal(5000m, snap.TotalDisbursed);
        Assert.True(snap.TotalDisbursed <= snap.TotalCommitted,
            $"TotalDisbursed ({snap.TotalDisbursed}) must not exceed TotalCommitted ({snap.TotalCommitted})");
    }

    [Fact]
    public async Task GetTopRiskApplicants_PendingDisbursements_NotInflatingDisbPct()
    {
        // Without the status filter, Pending $3,000 + Approved $2,000 = $5,000 disbursed
        // against $5,000 committed → disbPct = 1.0 → risk score near 0 (looks safe).
        // With the filter, only Approved $2,000 counts → disbPct = 0.4 → higher risk visible.
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", eligible: 5000m, committed: 5000m));
        _db.Disbursements.AddRange(
            DisbursementWithStatus("FRN001-A", "BEN001", 2000m, status: "Approved"),
            DisbursementWithStatus("FRN001-B", "BEN001", 3000m, status: "Pending"));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(10);

        Assert.Single(rows);
        Assert.Equal(2000m, rows[0].Disbursed);
        Assert.Equal(0.4,   rows[0].DisbursementPct, 6);   // 2000/5000
    }

    [Fact]
    public async Task GetTopGaps_PendingDisbursements_NotReducingGap()
    {
        // Without the filter, Pending $4,000 would reduce the gap from $5,000 to $1,000,
        // hiding a large undisbursed amount. With the filter, gap = $5,000 (nothing paid).
        _db.FundingCommitments.Add(Commitment("FRN001", "BEN001", committed: 5000m));
        _db.Disbursements.Add(DisbursementWithStatus("FRN001-A", "BEN001", 4000m, status: "Pending"));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopCommitmentDisbursementGapsAsync(10);

        Assert.Single(rows);
        Assert.Equal(0m,    rows[0].Disbursed);   // Pending row excluded
        Assert.Equal(5000m, rows[0].Gap);
    }

    // -----------------------------------------------------------------------
    // Helper: commitment with explicit year
    // -----------------------------------------------------------------------

    private FundingCommitment CommitmentYear(string frn, string? ben, int year,
        decimal? eligible = null, decimal? committed = null) => new()
    {
        RawSourceKey = $"{frn}-1",
        FundingRequestNumber = frn,
        ApplicantEntityNumber = ben,
        ApplicantName = ben != null ? $"District {ben}" : null,
        TotalEligibleAmount = eligible,
        CommittedAmount = committed,
        FundingYear = year,
        ImportedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };

    /// <summary>Creates a disbursement with an explicit InvoiceLineStatus.</summary>
    private Disbursement DisbursementWithStatus(
        string key, string? ben, decimal? approved, string? status) => new()
    {
        RawSourceKey = key,
        FundingRequestNumber = key.Split('-')[0],
        ApplicantEntityNumber = ben,
        FundingYear = 2024,
        ApprovedAmount = approved,
        InvoiceLineStatus = status,
        ImportedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };
}
