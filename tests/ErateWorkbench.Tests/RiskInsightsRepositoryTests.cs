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
// Integration tests — RiskInsightsRepository
//
// All tests seed ApplicantYearRiskSummaries directly — the repository no longer
// reads from FundingCommitments or Disbursements. Risk scores, levels, and
// disbursement pcts are pre-computed by ApplicantYearRiskSummaryBuilder and
// stored in the summary table. The repository's job is to aggregate, filter,
// order, and map those stored values.
//
// InvoiceLineStatus filtering and year-consistent disbursement isolation are
// responsibilities of ApplicantYearDisbursementSummaryBuilder (tested in
// DisbursementSummaryBuilderTests) and are not re-tested here.
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
    // Helper — creates a pre-computed ApplicantYearRiskSummary row.
    // All metric fields (ReductionPct, DisbursementPct, RiskScore, RiskLevel)
    // must be provided explicitly because the repository reads them as-is.
    // -----------------------------------------------------------------------

    private static ApplicantYearRiskSummary RS(
        string? ben,
        int year = 2024,
        decimal eligible   = 0m,
        decimal committed  = 0m,
        decimal approved   = 0m,
        double  redPct     = 0.0,
        double  disbPct    = 0.0,
        double  riskScore  = 0.0,
        string  riskLevel  = "Low",
        string? name       = null) => new()
    {
        FundingYear                      = year,
        ApplicantEntityNumber            = ben,
        ApplicantEntityName              = name ?? (ben != null ? $"District {ben}" : null),
        TotalEligibleAmount              = eligible,
        TotalCommittedAmount             = committed,
        TotalApprovedDisbursementAmount  = approved,
        TotalRequestedDisbursementAmount = approved,  // not used by repo
        HasCommitmentData                = eligible > 0 || committed > 0,
        HasDisbursementData              = approved > 0,
        ReductionPct                     = redPct,
        DisbursementPct                  = disbPct,
        RiskScore                        = riskScore,
        RiskLevel                        = riskLevel,
        ImportedAtUtc                    = DateTime.UtcNow,
    };

    // Convenience: compute metrics via RiskCalculator so test data stays self-consistent.
    private static ApplicantYearRiskSummary RSCalc(
        string? ben,
        int year        = 2024,
        decimal eligible  = 0m,
        decimal committed = 0m,
        decimal approved  = 0m,
        string? name      = null)
    {
        var redPct   = RiskCalculator.ReductionPct((double)eligible, (double)committed);
        var disbPct  = RiskCalculator.DisbursementPct((double)committed, (double)approved);
        var score    = RiskCalculator.ComputeRiskScore(redPct, disbPct);
        var level    = RiskCalculator.ClassifyRisk(score);
        return RS(ben, year, eligible, committed, approved, redPct, disbPct, score, level, name);
    }

    // -----------------------------------------------------------------------
    // GetSnapshotAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSnapshot_ReturnsCorrectTotals()
    {
        _db.ApplicantYearRiskSummaries.AddRange(
            RSCalc("BEN001", eligible: 10000m, committed: 8000m, approved: 7000m),
            RSCalc("BEN002", eligible:  5000m, committed: 5000m, approved:    0m));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(15000m, snap.TotalRequested);
        Assert.Equal(13000m, snap.TotalCommitted);
        Assert.Equal( 7000m, snap.TotalDisbursed);
    }

    [Fact]
    public async Task GetSnapshot_CommitmentFulfillmentRate_IsCorrect()
    {
        // eligible=10000, committed=8000 → rate = 8000/10000 = 0.8
        _db.ApplicantYearRiskSummaries.Add(RSCalc("BEN001", eligible: 10000m, committed: 8000m));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync();

        Assert.Equal(0.8, snap.CommitmentFulfillmentRate, 6);
    }

    [Fact]
    public async Task GetSnapshot_DisbursementCompletionRate_IsCorrect()
    {
        // committed=10000, approved=7500 → rate = 7500/10000 = 0.75
        _db.ApplicantYearRiskSummaries.Add(RSCalc("BEN001", committed: 10000m, approved: 7500m));
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
    public async Task GetTopRiskApplicants_ReturnsStoredMetrics()
    {
        // score=1.0, level="High" stored in summary — repo must pass them through
        _db.ApplicantYearRiskSummaries.Add(
            RS("BEN001", eligible: 10000m, committed: 0m, approved: 0m,
               redPct: 1.0, disbPct: 0.0, riskScore: 1.0, riskLevel: "High"));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(5);

        Assert.Single(rows);
        Assert.Equal("BEN001", rows[0].EntityNumber);
        Assert.Equal(1.0,  rows[0].RiskScore, 6);
        Assert.Equal("High", rows[0].RiskLevel);
    }

    [Fact]
    public async Task GetTopRiskApplicants_OrderedByRiskScoreDescending()
    {
        _db.ApplicantYearRiskSummaries.AddRange(
            RS("BEN001", riskScore: 0.0, riskLevel: "Low"),    // low risk
            RS("BEN002", riskScore: 1.0, riskLevel: "High"));  // high risk
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(10);

        Assert.Equal("BEN002", rows[0].EntityNumber);  // highest risk first
        Assert.Equal("BEN001", rows[1].EntityNumber);
    }

    [Fact]
    public async Task GetTopRiskApplicants_TopNLimitsResults()
    {
        _db.ApplicantYearRiskSummaries.AddRange(
            Enumerable.Range(1, 10).Select(i =>
                RS($"BEN{i:D3}", riskScore: 0.5, riskLevel: "Moderate")));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(3);

        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task GetTopRiskApplicants_MapsAmountsAndPercentagesCorrectly()
    {
        // eligible=2000, committed=2000, approved=1000
        // → redPct=0.0, disbPct=0.5, score=0.25, level=Low
        _db.ApplicantYearRiskSummaries.Add(
            RSCalc("BEN001", eligible: 2000m, committed: 2000m, approved: 1000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(5);

        Assert.Single(rows);
        Assert.Equal(2000m, rows[0].Requested);
        Assert.Equal(2000m, rows[0].Committed);
        Assert.Equal(1000m, rows[0].Disbursed);
        Assert.Equal(0.0,  rows[0].ReductionPct,    6);
        Assert.Equal(0.5,  rows[0].DisbursementPct, 6);
        Assert.Equal(0.25, rows[0].RiskScore,        6);
        Assert.Equal("Low", rows[0].RiskLevel);
    }

    // -----------------------------------------------------------------------
    // GetTopCommitmentDisbursementGapsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopGaps_OrderedByGapDescending()
    {
        // BEN001: committed=5000, approved=1000 → gap=4000
        // BEN002: committed=2000, approved=1500 → gap=500
        _db.ApplicantYearRiskSummaries.AddRange(
            RSCalc("BEN001", committed: 5000m, approved: 1000m),
            RSCalc("BEN002", committed: 2000m, approved: 1500m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopCommitmentDisbursementGapsAsync(10);

        Assert.Equal("BEN001", rows[0].EntityNumber);
        Assert.Equal(4000m,    rows[0].Gap);
        Assert.Equal("BEN002", rows[1].EntityNumber);
        Assert.Equal( 500m,    rows[1].Gap);
    }

    [Fact]
    public async Task GetTopGaps_ExcludesNonPositiveGaps()
    {
        // committed=1000, approved=1000 → gap=0 → excluded
        _db.ApplicantYearRiskSummaries.Add(RSCalc("BEN001", committed: 1000m, approved: 1000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopCommitmentDisbursementGapsAsync(10);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetTopGaps_GapFieldsCorrect()
    {
        _db.ApplicantYearRiskSummaries.Add(RSCalc("BEN001", committed: 5000m, approved: 1000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopCommitmentDisbursementGapsAsync(10);

        Assert.Single(rows);
        Assert.Equal(5000m, rows[0].Committed);
        Assert.Equal(1000m, rows[0].Disbursed);
        Assert.Equal(4000m, rows[0].Gap);
    }

    // -----------------------------------------------------------------------
    // GetTopReductionRatesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopReductions_OrderedByReductionPctDescending()
    {
        // BEN001: 80% reduction; BEN002: 30% reduction
        _db.ApplicantYearRiskSummaries.AddRange(
            RSCalc("BEN001", eligible: 10000m, committed: 2000m),
            RSCalc("BEN002", eligible: 10000m, committed: 7000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopReductionRatesAsync(10);

        Assert.Equal("BEN001", rows[0].EntityNumber);
        Assert.Equal(0.8, rows[0].ReductionPct, 6);
    }

    [Fact]
    public async Task GetTopReductions_ExcludesSmallEligibleAmounts()
    {
        // eligible=50 is below the $100 threshold — must be excluded
        _db.ApplicantYearRiskSummaries.Add(RSCalc("BEN001", eligible: 50m, committed: 0m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopReductionRatesAsync(10);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetTopReductions_IncludesEligibleAtThreshold()
    {
        // eligible=100 is exactly at the $100 threshold — must be included
        _db.ApplicantYearRiskSummaries.Add(RSCalc("BEN001", eligible: 100m, committed: 50m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopReductionRatesAsync(10);

        Assert.Single(rows);
        Assert.Equal("BEN001", rows[0].EntityNumber);
    }

    // -----------------------------------------------------------------------
    // GetAvailableYearsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAvailableYears_ReturnsDistinctYearsDescending()
    {
        _db.ApplicantYearRiskSummaries.AddRange(
            RS("BEN001", year: 2023),
            RS("BEN002", year: 2024),
            RS("BEN003", year: 2024),  // duplicate year
            RS("BEN004", year: 2022));
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
        _db.ApplicantYearRiskSummaries.AddRange(
            RSCalc("BEN001", year: 2023, eligible: 10000m, committed: 8000m),
            RSCalc("BEN002", year: 2024, eligible:  5000m, committed: 5000m));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync(year: 2024);

        Assert.Equal(5000m, snap.TotalRequested);
        Assert.Equal(5000m, snap.TotalCommitted);
    }

    [Fact]
    public async Task GetSnapshot_YearFilter_NoMatchingYear_ReturnsZeros()
    {
        _db.ApplicantYearRiskSummaries.Add(
            RSCalc("BEN001", year: 2023, eligible: 10000m, committed: 8000m));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync(year: 2099);

        Assert.Equal(0m, snap.TotalRequested);
        Assert.Equal(0m, snap.TotalCommitted);
    }

    [Fact]
    public async Task GetSnapshot_YearFilter_ExcludesOtherYearsData()
    {
        // BEN001 has rows in two years; filtering by 2024 must return only that year's totals.
        _db.ApplicantYearRiskSummaries.AddRange(
            RSCalc("BEN001", year: 2024, committed: 5000m, approved: 3000m),
            RSCalc("BEN001", year: 2023, committed: 9000m, approved: 9000m));
        await _db.SaveChangesAsync();

        var snap = await _repo.GetSnapshotAsync(year: 2024);

        Assert.Equal(5000m, snap.TotalCommitted);
        Assert.Equal(3000m, snap.TotalDisbursed);  // must NOT include FY2023 amounts
    }

    // -----------------------------------------------------------------------
    // Year filter — GetTopRiskApplicantsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopRiskApplicants_YearFilter_ExcludesOtherYears()
    {
        _db.ApplicantYearRiskSummaries.AddRange(
            RS("BEN001", year: 2023, riskScore: 1.0, riskLevel: "High"),
            RS("BEN002", year: 2024, riskScore: 1.0, riskLevel: "High"));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, year: 2024);

        Assert.Single(rows);
        Assert.Equal("BEN002", rows[0].EntityNumber);
    }

    [Fact]
    public async Task GetTopRiskApplicants_YearFilter_ReturnsCorrectAmounts()
    {
        // BEN001 in 2024: committed=2000, approved=1000 (disbPct=0.5)
        // BEN001 in 2023: committed=2000, approved=9000 (should be excluded)
        _db.ApplicantYearRiskSummaries.AddRange(
            RSCalc("BEN001", year: 2024, eligible: 2000m, committed: 2000m, approved: 1000m),
            RSCalc("BEN001", year: 2023, eligible: 2000m, committed: 2000m, approved: 9000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, year: 2024);

        Assert.Single(rows);
        Assert.Equal(1000m, rows[0].Disbursed);      // only FY2024 amount
        Assert.Equal(0.5, rows[0].DisbursementPct, 6);
    }

    // -----------------------------------------------------------------------
    // Severity filter — GetTopRiskApplicantsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopRiskApplicants_SeverityHigh_ReturnsOnlyHighRisk()
    {
        _db.ApplicantYearRiskSummaries.AddRange(
            RS("BEN001", riskScore: 1.0, riskLevel: "High"),
            RS("BEN002", riskScore: 0.0, riskLevel: "Low"));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, severity: "High");

        Assert.Single(rows);
        Assert.Equal("BEN001", rows[0].EntityNumber);
        Assert.Equal("High",   rows[0].RiskLevel);
    }

    [Fact]
    public async Task GetTopRiskApplicants_SeverityLow_ReturnsOnlyLowRisk()
    {
        _db.ApplicantYearRiskSummaries.AddRange(
            RS("BEN001", riskScore: 1.0, riskLevel: "High"),
            RS("BEN002", riskScore: 0.0, riskLevel: "Low"));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, severity: "Low");

        Assert.Single(rows);
        Assert.Equal("BEN002", rows[0].EntityNumber);
        Assert.Equal("Low",    rows[0].RiskLevel);
    }

    [Fact]
    public async Task GetTopRiskApplicants_SeverityFilter_CaseInsensitive()
    {
        _db.ApplicantYearRiskSummaries.Add(RS("BEN001", riskScore: 1.0, riskLevel: "High"));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopRiskApplicantsAsync(20, severity: "high");

        Assert.Single(rows);
        Assert.Equal("High", rows[0].RiskLevel);
    }

    [Fact]
    public async Task GetTopRiskApplicants_SeverityFilter_NoMatch_ReturnsEmpty()
    {
        _db.ApplicantYearRiskSummaries.Add(RS("BEN001", riskScore: 1.0, riskLevel: "High"));
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
        _db.ApplicantYearRiskSummaries.AddRange(
            RS("BEN001", year: 2023, riskScore: 1.0, riskLevel: "High"),  // wrong year
            RS("BEN002", year: 2024, riskScore: 1.0, riskLevel: "High"),  // match
            RS("BEN003", year: 2024, riskScore: 0.0, riskLevel: "Low"));  // wrong level
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
        _db.ApplicantYearRiskSummaries.AddRange(
            RSCalc("BEN001", year: 2023, committed: 5000m),
            RSCalc("BEN002", year: 2024, committed: 3000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopCommitmentDisbursementGapsAsync(10, year: 2024);

        Assert.Single(rows);
        Assert.Equal("BEN002", rows[0].EntityNumber);
    }

    [Fact]
    public async Task GetTopGaps_YearFilter_UsesCorrectYearAmounts()
    {
        // BEN001 in 2024: committed=5000, approved=1000 → gap=4000
        // BEN001 in 2023: committed=5000, approved=4000 → gap=1000 (should be excluded)
        _db.ApplicantYearRiskSummaries.AddRange(
            RSCalc("BEN001", year: 2024, committed: 5000m, approved: 1000m),
            RSCalc("BEN001", year: 2023, committed: 5000m, approved: 4000m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopCommitmentDisbursementGapsAsync(10, year: 2024);

        Assert.Single(rows);
        Assert.Equal(1000m, rows[0].Disbursed);
        Assert.Equal(4000m, rows[0].Gap);  // must NOT be reduced by the 2023 row
    }

    // -----------------------------------------------------------------------
    // Year filter — GetTopReductionRatesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTopReductions_YearFilter_ExcludesOtherYears()
    {
        _db.ApplicantYearRiskSummaries.AddRange(
            RSCalc("BEN001", year: 2023, eligible: 10000m, committed: 0m),
            RSCalc("BEN002", year: 2024, eligible: 10000m, committed: 0m));
        await _db.SaveChangesAsync();

        var rows = await _repo.GetTopReductionRatesAsync(10, year: 2024);

        Assert.Single(rows);
        Assert.Equal("BEN002", rows[0].EntityNumber);
    }

    // -----------------------------------------------------------------------
    // GetAdvisorySignalsAsync — classification
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAdvisorySignals_NoCommitment_ClassifiedCorrectly()
    {
        // HasCommitmentData=false, HasDisbursementData=true → "No Commitment"
        var row = RSCalc("BEN001", eligible: 0m, committed: 0m, approved: 5000m);
        _db.ApplicantYearRiskSummaries.Add(row);
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync();

        Assert.Single(signals);
        Assert.Equal("No Commitment", signals[0].AnomalyType);
        Assert.Equal("BEN001", signals[0].EntityNumber);
    }

    [Fact]
    public async Task GetAdvisorySignals_NoDisbursement_ClassifiedCorrectly()
    {
        // HasCommitmentData=true, HasDisbursementData=false → "No Disbursement"
        var row = RSCalc("BEN001", eligible: 10000m, committed: 8000m, approved: 0m);
        _db.ApplicantYearRiskSummaries.Add(row);
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync();

        // "No Disbursement" + "Low Utilization" (disbPct=0 < 0.5 AND hasCommitment)
        var noDisbSignal = signals.Single(s => s.AnomalyType == "No Disbursement");
        Assert.Equal("BEN001", noDisbSignal.EntityNumber);
    }

    [Fact]
    public async Task GetAdvisorySignals_HighReduction_ClassifiedCorrectly()
    {
        // ReductionPct = 1 - 3000/10000 = 0.7 > 0.5 → "High Reduction"
        var row = RSCalc("BEN001", eligible: 10000m, committed: 3000m, approved: 2000m);
        _db.ApplicantYearRiskSummaries.Add(row);
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync();

        Assert.Contains(signals, s => s.AnomalyType == "High Reduction" && s.EntityNumber == "BEN001");
    }

    [Fact]
    public async Task GetAdvisorySignals_LowUtilization_ClassifiedCorrectly()
    {
        // DisbursementPct = 2000/10000 = 0.2 < 0.5, HasCommitmentData=true → "Low Utilization"
        var row = RSCalc("BEN001", eligible: 10000m, committed: 10000m, approved: 2000m);
        _db.ApplicantYearRiskSummaries.Add(row);
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync();

        Assert.Contains(signals, s => s.AnomalyType == "Low Utilization" && s.EntityNumber == "BEN001");
    }

    [Fact]
    public async Task GetAdvisorySignals_RowMatchingMultipleConditions_ProducesOneSignalPerType()
    {
        // ReductionPct = 0.7 (> 0.5) AND DisbursementPct = 0.2 (< 0.5) → two signals
        var row = RSCalc("BEN001", eligible: 10000m, committed: 3000m, approved: 600m);
        _db.ApplicantYearRiskSummaries.Add(row);
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync();

        var types = signals.Select(s => s.AnomalyType).ToHashSet();
        Assert.Contains("High Reduction", types);
        Assert.Contains("Low Utilization", types);
        Assert.All(signals, s => Assert.Equal("BEN001", s.EntityNumber));
    }

    [Fact]
    public async Task GetAdvisorySignals_NormalRow_ProducesNoSignals()
    {
        // ReductionPct = 0, DisbursementPct = 1.0, HasCommitmentData=true, HasDisbursementData=true → no signals
        var row = RSCalc("BEN001", eligible: 10000m, committed: 10000m, approved: 10000m);
        _db.ApplicantYearRiskSummaries.Add(row);
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync();

        Assert.Empty(signals);
    }

    [Fact]
    public async Task GetAdvisorySignals_ReductionAtThreshold_NotClassifiedAsHighReduction()
    {
        // ReductionPct = 0.5, which is NOT > 0.5 → should not produce "High Reduction"
        var row = RSCalc("BEN001", eligible: 10000m, committed: 5000m, approved: 3000m);
        _db.ApplicantYearRiskSummaries.Add(row);
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync();

        Assert.DoesNotContain(signals, s => s.AnomalyType == "High Reduction");
    }

    // -----------------------------------------------------------------------
    // GetAdvisorySignalsAsync — ordering
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAdvisorySignals_OrderedByRiskScoreDescending()
    {
        // BEN001: low risk; BEN002: high risk — BEN002 signals should come first
        _db.ApplicantYearRiskSummaries.AddRange(
            RS("BEN001", committed: 5000m, redPct: 0.6, disbPct: 0.3, riskScore: 0.65, riskLevel: "High"),
            RS("BEN002", committed: 5000m, redPct: 0.7, disbPct: 0.2, riskScore: 0.75, riskLevel: "High"));
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync();

        // First signal must belong to the higher risk row
        Assert.Equal("BEN002", signals[0].EntityNumber);
    }

    [Fact]
    public async Task GetAdvisorySignals_TiedRiskScore_OrderedByCommittedAmountDescending()
    {
        // Same risk score; BEN002 has larger committed amount → appears first
        _db.ApplicantYearRiskSummaries.AddRange(
            RS("BEN001", committed:  3000m, redPct: 0.6, disbPct: 0.3, riskScore: 0.65, riskLevel: "High"),
            RS("BEN002", committed: 10000m, redPct: 0.6, disbPct: 0.3, riskScore: 0.65, riskLevel: "High"));
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync();

        Assert.Equal("BEN002", signals[0].EntityNumber);
    }

    // -----------------------------------------------------------------------
    // GetAdvisorySignalsAsync — topN limit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAdvisorySignals_TopN_LimitsResults()
    {
        // 5 rows each with High Reduction → 5 signals; topN=3 must return 3
        _db.ApplicantYearRiskSummaries.AddRange(
            RS("BEN001", redPct: 0.8, disbPct: 0.3, riskScore: 0.75, riskLevel: "High"),
            RS("BEN002", redPct: 0.8, disbPct: 0.3, riskScore: 0.75, riskLevel: "High"),
            RS("BEN003", redPct: 0.8, disbPct: 0.3, riskScore: 0.75, riskLevel: "High"),
            RS("BEN004", redPct: 0.8, disbPct: 0.3, riskScore: 0.75, riskLevel: "High"),
            RS("BEN005", redPct: 0.8, disbPct: 0.3, riskScore: 0.75, riskLevel: "High"));
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync(topN: 3);

        Assert.Equal(3, signals.Count);
    }

    // -----------------------------------------------------------------------
    // GetAdvisorySignalsAsync — year filter
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAdvisorySignals_YearFilter_ExcludesOtherYears()
    {
        _db.ApplicantYearRiskSummaries.AddRange(
            RSCalc("BEN001", year: 2023, eligible: 0m, committed: 0m, approved: 5000m),  // No Commitment
            RSCalc("BEN002", year: 2024, eligible: 0m, committed: 0m, approved: 5000m)); // No Commitment
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync(year: 2024);

        Assert.Single(signals);
        Assert.Equal("BEN002", signals[0].EntityNumber);
        Assert.Equal(2024, signals[0].FundingYear);
    }

    [Fact]
    public async Task GetAdvisorySignals_NoYearFilter_IncludesAllYears()
    {
        _db.ApplicantYearRiskSummaries.AddRange(
            RSCalc("BEN001", year: 2023, eligible: 0m, committed: 0m, approved: 5000m),
            RSCalc("BEN002", year: 2024, eligible: 0m, committed: 0m, approved: 5000m));
        await _db.SaveChangesAsync();

        var signals = await _repo.GetAdvisorySignalsAsync();

        Assert.Equal(2, signals.Count);
        Assert.Contains(signals, s => s.FundingYear == 2023);
        Assert.Contains(signals, s => s.FundingYear == 2024);
    }

    [Fact]
    public async Task GetAdvisorySignals_EmptyDatabase_ReturnsEmpty()
    {
        var signals = await _repo.GetAdvisorySignalsAsync();
        Assert.Empty(signals);
    }
}
