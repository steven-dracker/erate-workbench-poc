using System.Net;
using System.Text;
using System.Text.Json;
using ErateWorkbench.Infrastructure.Reconciliation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ErateWorkbench.Infrastructure;

namespace ErateWorkbench.Tests;

// ── Helpers ──────────────────────────────────────────────────────────────────

/// <summary>Stub HTTP handler that returns a fixed JSON body for every request.</summary>
file sealed class JsonStubHandler : HttpMessageHandler
{
    private readonly Queue<string> responses;

    /// <param name="responses">Responses returned in order (last response repeated if queue exhausted).</param>
    public JsonStubHandler(params string[] responses)
    {
        this.responses = new Queue<string>(responses);
    }

    public List<string> RequestedUrls { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        RequestedUrls.Add(request.RequestUri!.ToString());
        var body = responses.Count > 1 ? responses.Dequeue() : responses.Peek();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }
}

// ── Manifest / URL-building tests ─────────────────────────────────────────────

public class ReconciliationManifestTests
{
    private static SocrataReconciliationService Service(string? json = null)
    {
        var handler = new JsonStubHandler(json ?? "[]");
        return new SocrataReconciliationService(new HttpClient(handler));
    }

    [Fact]
    public void BuildTotalCountUrl_ContainsDatasetId()
    {
        var svc = Service();
        var url = svc.BuildTotalCountUrl(DatasetManifests.FundingCommitments);
        Assert.Contains("avi8-svp9", url);
        Assert.Contains("count", url);
    }

    [Fact]
    public void BuildTotalCountUrl_Disbursements_ContainsDatasetId()
    {
        var svc = Service();
        var url = svc.BuildTotalCountUrl(DatasetManifests.Disbursements);
        Assert.Contains("jpiu-tj8h", url);
    }

    [Fact]
    public void BuildByYearUrl_FundingCommitments_ContainsFundingYearGroup()
    {
        var svc = Service();
        var url = svc.BuildByYearUrl(DatasetManifests.FundingCommitments);
        Assert.Contains("funding_year", url);
        Assert.Contains("count", url);
        Assert.Contains("total_eligible_amount", url);
        Assert.Contains("committed_amount", url);
        Assert.Contains("applicant_entity_number", url);
    }

    [Fact]
    public void BuildByYearUrl_Disbursements_ContainsBenAndApprovedAmt()
    {
        var svc = Service();
        var url = svc.BuildByYearUrl(DatasetManifests.Disbursements);
        Assert.Contains("jpiu-tj8h", url);
        Assert.Contains("approved_inv_line_amt", url);
        Assert.Contains("ben", url);
    }

    [Fact]
    public void BuildByYearUrl_ContainsLimitAndGroupAndOrder()
    {
        var svc = Service();
        var url = svc.BuildByYearUrl(DatasetManifests.FundingCommitments);
        Assert.Contains("$group=", url);
        Assert.Contains("$order=", url);
        Assert.Contains("$limit=", url);
    }

    [Fact]
    public void FundingCommitments_Manifest_HasTwoAmountMetrics()
    {
        Assert.Equal(2, DatasetManifests.FundingCommitments.AmountMetrics.Count);
    }

    [Fact]
    public void Disbursements_Manifest_HasTwoAmountMetrics()
    {
        Assert.Equal(2, DatasetManifests.Disbursements.AmountMetrics.Count);
    }
}

// ── JSON parsing tests ────────────────────────────────────────────────────────

public class ReconciliationJsonParsingTests
{
    [Fact]
    public void ParseLong_NumberKind_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("""[{"row_count": 42}]""");
        var result = SocrataReconciliationService.ParseLong(doc.RootElement[0], "row_count");
        Assert.Equal(42L, result);
    }

    [Fact]
    public void ParseLong_StringKind_ParsesCorrectly()
    {
        using var doc = JsonDocument.Parse("""[{"row_count": "3239707"}]""");
        var result = SocrataReconciliationService.ParseLong(doc.RootElement[0], "row_count");
        Assert.Equal(3_239_707L, result);
    }

    [Fact]
    public void ParseLong_MissingKey_ReturnsZero()
    {
        using var doc = JsonDocument.Parse("""[{}]""");
        Assert.Equal(0L, SocrataReconciliationService.ParseLong(doc.RootElement[0], "row_count"));
    }

    [Fact]
    public void ParseLong_NullValue_ReturnsZero()
    {
        using var doc = JsonDocument.Parse("""[{"row_count": null}]""");
        Assert.Equal(0L, SocrataReconciliationService.ParseLong(doc.RootElement[0], "row_count"));
    }

    [Fact]
    public void ParseDecimal_StringWithDecimals_ParsesCorrectly()
    {
        using var doc = JsonDocument.Parse("""[{"total_eligible_amount": "1234567890.12"}]""");
        var result = SocrataReconciliationService.ParseDecimal(doc.RootElement[0], "total_eligible_amount");
        Assert.Equal(1_234_567_890.12m, result);
    }

    [Fact]
    public void ParseDecimal_BlankString_ReturnsZero()
    {
        using var doc = JsonDocument.Parse("""[{"total_eligible_amount": ""}]""");
        Assert.Equal(0m, SocrataReconciliationService.ParseDecimal(doc.RootElement[0], "total_eligible_amount"));
    }

    [Fact]
    public void ParseDecimal_NullValue_ReturnsZero()
    {
        using var doc = JsonDocument.Parse("""[{"total_eligible_amount": null}]""");
        Assert.Equal(0m, SocrataReconciliationService.ParseDecimal(doc.RootElement[0], "total_eligible_amount"));
    }

    [Fact]
    public void ParseDecimal_MissingKey_ReturnsZero()
    {
        using var doc = JsonDocument.Parse("""[{}]""");
        Assert.Equal(0m, SocrataReconciliationService.ParseDecimal(doc.RootElement[0], "amount"));
    }
}

// ── End-to-end reconcile via stub HTTP ───────────────────────────────────────

public class SocrataReconciliationServiceTests
{
    private const string TotalJson = """[{"row_count": "100"}]""";

    private const string ByYearJson = """
        [
          {"funding_year": "2023", "row_count": "60", "distinct_applicants": "10",
           "total_eligible_amount": "5000000.00", "committed_amount": "4500000.00"},
          {"funding_year": "2024", "row_count": "40", "distinct_applicants": "8",
           "total_eligible_amount": "3000000.00", "committed_amount": "2800000.00"}
        ]
        """;

    private static SocrataReconciliationService Service(string totalJson, string byYearJson)
    {
        var handler = new JsonStubHandler(totalJson, byYearJson);
        return new SocrataReconciliationService(new HttpClient(handler));
    }

    [Fact]
    public async Task ReconcileAsync_MatchingLocalData_NoVariance()
    {
        var svc = Service(TotalJson, ByYearJson);
        var localProvider = new StubLocalProvider("FundingCommitments",
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 60, DistinctApplicants = 10,
                Amounts = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m, ["CommittedAmount"] = 4_500_000m } },
            new LocalYearTotals { FundingYear = 2024, RowCount = 40, DistinctApplicants = 8,
                Amounts = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 3_000_000m, ["CommittedAmount"] = 2_800_000m } },
        ]);

        var result = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider);

        Assert.Equal(100L, result.SourceTotalRowCount);
        Assert.Equal(100L, result.LocalRawTotalRowCount);
        Assert.False(result.HasAnyVariance);
        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public async Task ReconcileAsync_LocalMissingRows_NegativeVariance()
    {
        var svc = Service(TotalJson, ByYearJson);
        var localProvider = new StubLocalProvider("FundingCommitments",
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 55, DistinctApplicants = 10,
                Amounts = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m, ["CommittedAmount"] = 4_500_000m } },
        ]);

        var result = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider);

        Assert.True(result.HasAnyVariance);
        var row2023 = result.Rows.Single(r => r.FundingYear == 2023);
        Assert.Equal(-5L, row2023.RowCountVariance); // 55 - 60 = -5
    }

    [Fact]
    public async Task ReconcileAsync_SourceHasYearNotInLocal_YearAppearsWithZeroLocalCount()
    {
        var svc = Service(TotalJson, ByYearJson);
        var localProvider = new StubLocalProvider("FundingCommitments",
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 60 },
            // 2024 missing from local
        ]);

        var result = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider);

        var row2024 = result.Rows.Single(r => r.FundingYear == 2024);
        Assert.Equal(40L, row2024.SourceRowCount);
        Assert.Equal(0L,  row2024.LocalRawRowCount);
        Assert.Equal(-40L, row2024.RowCountVariance);
    }

    [Fact]
    public async Task ReconcileAsync_LocalHasYearNotInSource_YearAppearsWithZeroSourceCount()
    {
        var svc = Service(TotalJson, ByYearJson);
        var localProvider = new StubLocalProvider("FundingCommitments",
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 60 },
            new LocalYearTotals { FundingYear = 2024, RowCount = 40 },
            new LocalYearTotals { FundingYear = 2022, RowCount = 5 }, // extra year not in source
        ]);

        var result = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider);

        var row2022 = result.Rows.Single(r => r.FundingYear == 2022);
        Assert.Equal(0L, row2022.SourceRowCount);
        Assert.Equal(5L, row2022.LocalRawRowCount);
        Assert.Equal(5L, row2022.RowCountVariance);
    }

    [Fact]
    public async Task ReconcileAsync_AmountVariance_ComputedCorrectly()
    {
        var svc = Service(TotalJson, ByYearJson);
        var localProvider = new StubLocalProvider("FundingCommitments",
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 60,
                Amounts = new Dictionary<string, decimal>
                {
                    ["TotalEligibleAmount"] = 4_900_000m,  // 100k less than source
                    ["CommittedAmount"]     = 4_500_000m,
                }},
            new LocalYearTotals { FundingYear = 2024, RowCount = 40,
                Amounts = new Dictionary<string, decimal>
                {
                    ["TotalEligibleAmount"] = 3_000_000m,
                    ["CommittedAmount"]     = 2_800_000m,
                }},
        ]);

        var result  = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider);
        var row2023 = result.Rows.Single(r => r.FundingYear == 2023);

        Assert.True(row2023.HasVariance);
        Assert.Equal(-100_000m, row2023.AmountVariances["TotalEligibleAmount"]);
        Assert.Equal(0m,        row2023.AmountVariances["CommittedAmount"]);
    }

    [Fact]
    public async Task ReconcileAsync_EmptySourceResponse_ReturnsOnlyLocalYears()
    {
        var svc = Service("""[{"row_count":"0"}]""", "[]");
        var localProvider = new StubLocalProvider("FundingCommitments",
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 10 },
        ]);

        var result = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider);

        Assert.Equal(1, result.Rows.Count);
        Assert.Equal(0L,  result.SourceTotalRowCount);
        Assert.Equal(10L, result.LocalRawTotalRowCount);
    }

    [Fact]
    public async Task ReconcileAsync_RowsOrderedByFundingYearAscending()
    {
        var svc = Service(TotalJson, ByYearJson);
        var localProvider = new StubLocalProvider("FundingCommitments", []);

        var result = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider);

        var years = result.Rows.Select(r => r.FundingYear).ToList();
        Assert.Equal(years.OrderBy(y => y).ToList(), years);
    }

    [Fact]
    public async Task ReconcileAsync_ResultContainsDisplayNames()
    {
        var svc = Service(TotalJson, ByYearJson);
        var localProvider = new StubLocalProvider("FundingCommitments", []);

        var result = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider);

        Assert.True(result.AmountDisplayNames.ContainsKey("TotalEligibleAmount"));
        Assert.True(result.AmountDisplayNames.ContainsKey("CommittedAmount"));
    }
}

// ── Variance calculation unit tests ──────────────────────────────────────────

public class YearReconciliationRowTests
{
    [Fact]
    public void RowCountVariance_LocalMinusSource()
    {
        var row = new YearReconciliationRow
        {
            FundingYear      = 2024,
            SourceRowCount   = 100,
            LocalRawRowCount = 95,
        };
        Assert.Equal(-5L, row.RowCountVariance);
    }

    [Fact]
    public void RowCountVariancePct_CalculatedCorrectly()
    {
        var row = new YearReconciliationRow
        {
            FundingYear      = 2024,
            SourceRowCount   = 200,
            LocalRawRowCount = 190,
        };
        Assert.Equal(-5.0, row.RowCountVariancePct); // -10 / 200 = -5%
    }

    [Fact]
    public void RowCountVariancePct_ZeroSource_ReturnsZero()
    {
        var row = new YearReconciliationRow
        {
            FundingYear      = 2024,
            SourceRowCount   = 0,
            LocalRawRowCount = 5,
        };
        Assert.Equal(0.0, row.RowCountVariancePct);
    }

    [Fact]
    public void ApplicantCountVariance_BothPresent_ComputedCorrectly()
    {
        var row = new YearReconciliationRow
        {
            FundingYear                = 2024,
            SourceDistinctApplicants   = 100,
            LocalRawDistinctApplicants = 102,
        };
        Assert.Equal(2L, row.ApplicantCountVariance);
    }

    [Fact]
    public void ApplicantCountVariance_SourceNull_ReturnsNull()
    {
        var row = new YearReconciliationRow
        {
            FundingYear                = 2024,
            SourceDistinctApplicants   = null,
            LocalRawDistinctApplicants = 50,
        };
        Assert.Null(row.ApplicantCountVariance);
    }

    [Fact]
    public void HasVariance_NoVariance_ReturnsFalse()
    {
        var row = new YearReconciliationRow
        {
            FundingYear      = 2024,
            SourceRowCount   = 50,
            LocalRawRowCount = 50,
            SourceAmounts    = new Dictionary<string, decimal> { ["Amt"] = 100m },
            LocalRawAmounts  = new Dictionary<string, decimal> { ["Amt"] = 100m },
        };
        Assert.False(row.HasVariance);
    }

    [Fact]
    public void HasVariance_RowCountDiffers_ReturnsTrue()
    {
        var row = new YearReconciliationRow
        {
            FundingYear      = 2024,
            SourceRowCount   = 50,
            LocalRawRowCount = 51,
        };
        Assert.True(row.HasVariance);
    }

    [Fact]
    public void HasVariance_AmountDiffers_ReturnsTrue()
    {
        var row = new YearReconciliationRow
        {
            FundingYear      = 2024,
            SourceRowCount   = 50,
            LocalRawRowCount = 50,
            SourceAmounts    = new Dictionary<string, decimal> { ["Amt"] = 100m },
            LocalRawAmounts  = new Dictionary<string, decimal> { ["Amt"] = 99m },
        };
        Assert.True(row.HasVariance);
    }

    [Fact]
    public void AmountVariances_LocalMinusSource()
    {
        var row = new YearReconciliationRow
        {
            FundingYear     = 2024,
            SourceAmounts   = new Dictionary<string, decimal> { ["X"] = 1000m, ["Y"] = 500m },
            LocalRawAmounts = new Dictionary<string, decimal> { ["X"] = 990m,  ["Y"] = 500m },
        };
        Assert.Equal(-10m, row.AmountVariances["X"]);
        Assert.Equal(0m,   row.AmountVariances["Y"]);
    }
}

// ── Report writer tests ───────────────────────────────────────────────────────

public class ReconciliationReportWriterTests
{
    private static DatasetReconciliationResult SampleResult(bool withVariance = false) => new()
    {
        DatasetName          = "FundingCommitments",
        RunAtUtc             = new DateTime(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc),
        SourceTotalRowCount  = 100,
        LocalRawTotalRowCount = withVariance ? 95 : 100,
        Notes                = "Test notes.",
        AmountDisplayNames   = new Dictionary<string, string>
        {
            ["TotalEligibleAmount"] = "Total Eligible Amount",
            ["CommittedAmount"]     = "Committed Amount",
        },
        Rows =
        [
            new()
            {
                FundingYear                = 2024,
                SourceRowCount             = 60,
                LocalRawRowCount           = withVariance ? 55 : 60,
                SourceDistinctApplicants   = 10,
                LocalRawDistinctApplicants = 10,
                SourceAmounts              = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m, ["CommittedAmount"] = 4_500_000m },
                LocalRawAmounts            = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m, ["CommittedAmount"] = 4_500_000m },
            },
        ],
    };

    [Fact]
    public void BuildMarkdown_ContainsDatasetName()
    {
        var md = new ReconciliationReportWriter().BuildMarkdown(SampleResult());
        Assert.Contains("FundingCommitments", md);
    }

    [Fact]
    public void BuildMarkdown_NoVariance_ShowsCheckmark()
    {
        var md = new ReconciliationReportWriter().BuildMarkdown(SampleResult(withVariance: false));
        Assert.Contains("✓", md);
        Assert.DoesNotContain("⚠", md);
    }

    [Fact]
    public void BuildMarkdown_HasVariance_ShowsWarning()
    {
        var md = new ReconciliationReportWriter().BuildMarkdown(SampleResult(withVariance: true));
        Assert.Contains("⚠", md);
    }

    [Fact]
    public void BuildMarkdown_ContainsByYearSection()
    {
        var md = new ReconciliationReportWriter().BuildMarkdown(SampleResult());
        Assert.Contains("Funding Year", md);
        Assert.Contains("2024", md);
    }

    [Fact]
    public void BuildMarkdown_ContainsAmountDisplayNames()
    {
        var md = new ReconciliationReportWriter().BuildMarkdown(SampleResult());
        Assert.Contains("Total Eligible Amount", md);
        Assert.Contains("Committed Amount", md);
    }

    [Fact]
    public void BuildMarkdown_ContainsNotes()
    {
        var md = new ReconciliationReportWriter().BuildMarkdown(SampleResult());
        Assert.Contains("Test notes.", md);
    }

    [Fact]
    public void BuildMarkdown_ContainsSummaryTotals()
    {
        var md = new ReconciliationReportWriter().BuildMarkdown(SampleResult());
        Assert.Contains("100", md);    // total rows
        Assert.Contains("Summary", md);
    }

    [Fact]
    public async Task WriteMarkdownAsync_CreatesFile()
    {
        var path   = Path.Combine(Path.GetTempPath(), $"test-reconcile-{Guid.NewGuid()}.md");
        var writer = new ReconciliationReportWriter();
        await writer.WriteMarkdownAsync(SampleResult(), path);
        Assert.True(File.Exists(path));
        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("FundingCommitments", content);
        File.Delete(path);
    }

    [Fact]
    public async Task WriteJsonAsync_CreatesValidJson()
    {
        var path   = Path.Combine(Path.GetTempPath(), $"test-reconcile-{Guid.NewGuid()}.json");
        var writer = new ReconciliationReportWriter();
        await writer.WriteJsonAsync(SampleResult(), path);
        Assert.True(File.Exists(path));
        var json = await File.ReadAllTextAsync(path);
        Assert.StartsWith("{", json.TrimStart());
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("FundingCommitments", doc.RootElement.GetProperty("datasetName").GetString());
        File.Delete(path);
    }
}

// ── Local provider integration tests (in-memory SQLite) ──────────────────────

public class FundingCommitmentLocalDataProviderTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly AppDbContext _db;

    public FundingCommitmentLocalDataProviderTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
        _db = new AppDbContext(opts);
        _db.Database.EnsureCreated();
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    [Fact]
    public async Task GetLocalRawTotals_GroupsByYear()
    {
        _db.FundingCommitments.AddRange(
            FC("A1", 2023, 1000m, 900m),
            FC("A2", 2023, 2000m, 1800m),
            FC("B1", 2024, 500m,  400m));
        await _db.SaveChangesAsync();

        var provider = new FundingCommitmentLocalDataProvider(_db);
        var totals   = await provider.GetLocalRawTotalsAsync();

        Assert.Equal(2, totals.Count);

        var t2023 = totals.Single(t => t.FundingYear == 2023);
        Assert.Equal(2L, t2023.RowCount);
        Assert.Equal(3000m, t2023.Amounts["TotalEligibleAmount"]);
        Assert.Equal(2700m, t2023.Amounts["CommittedAmount"]);

        var t2024 = totals.Single(t => t.FundingYear == 2024);
        Assert.Equal(1L, t2024.RowCount);
    }

    [Fact]
    public async Task GetLocalRawTotals_CountsDistinctApplicants()
    {
        _db.FundingCommitments.AddRange(
            FC("A1", 2023, 100m, 90m, "BEN-1"),
            FC("A2", 2023, 200m, 180m, "BEN-1"),  // same BEN
            FC("A3", 2023, 300m, 270m, "BEN-2"));  // different BEN
        await _db.SaveChangesAsync();

        var provider = new FundingCommitmentLocalDataProvider(_db);
        var totals   = await provider.GetLocalRawTotalsAsync();

        var t2023 = totals.Single(t => t.FundingYear == 2023);
        Assert.Equal(2L, t2023.DistinctApplicants);
    }

    [Fact]
    public async Task GetLocalRawTotals_EmptyTable_ReturnsEmpty()
    {
        var provider = new FundingCommitmentLocalDataProvider(_db);
        var totals   = await provider.GetLocalRawTotalsAsync();
        Assert.Empty(totals);
    }

    private static ErateWorkbench.Domain.FundingCommitment FC(
        string key, int year, decimal eligible, decimal committed, string? ben = null) => new()
    {
        RawSourceKey          = key,
        FundingRequestNumber  = key,
        FundingYear           = year,
        TotalEligibleAmount   = eligible,
        CommittedAmount       = committed,
        ApplicantEntityNumber = ben,
        ImportedAtUtc         = DateTime.UtcNow,
        UpdatedAtUtc          = DateTime.UtcNow,
    };
}

// ── Summary reconciliation tests ─────────────────────────────────────────────

public class SocrataReconciliationServiceSummaryTests
{
    private const string TotalJson  = """[{"row_count": "100"}]""";
    private const string ByYearJson = """
        [
          {"funding_year": "2023", "row_count": "60", "distinct_applicants": "10",
           "total_eligible_amount": "5000000.00", "committed_amount": "4500000.00"}
        ]
        """;

    private static SocrataReconciliationService Service() =>
        new(new HttpClient(new JsonStubHandler(TotalJson, ByYearJson)));

    private static ILocalSummaryProvider SummaryProvider(IReadOnlyList<LocalYearTotals> data) =>
        new StubSummaryProvider("FundingCommitments", data);

    [Fact]
    public async Task ReconcileAsync_WithSummaryProvider_PopulatesSummaryFields()
    {
        var svc           = Service();
        var localProvider = new StubLocalProvider("FundingCommitments",
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 60, DistinctApplicants = 10,
                Amounts = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m, ["CommittedAmount"] = 4_500_000m } },
        ]);
        var summaryProvider = SummaryProvider(
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 10, DistinctApplicants = 10,
                Amounts = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m, ["CommittedAmount"] = 4_500_000m } },
        ]);

        var result  = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider, summaryProvider);
        var row2023 = result.Rows.Single(r => r.FundingYear == 2023);

        Assert.Equal(10L, row2023.LocalSummaryRowCount);
        Assert.Equal(10L, row2023.LocalSummaryDistinctApplicants);
    }

    [Fact]
    public async Task ReconcileAsync_WithSummaryProvider_RawVsSummaryVarianceComputed()
    {
        var svc           = Service();
        var localProvider = new StubLocalProvider("FundingCommitments",
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 60,
                Amounts = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m, ["CommittedAmount"] = 4_500_000m } },
        ]);
        var summaryProvider = SummaryProvider(
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 10,
                Amounts = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m, ["CommittedAmount"] = 4_500_000m } },
        ]);

        var result  = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider, summaryProvider);
        var row2023 = result.Rows.Single(r => r.FundingYear == 2023);

        // Summary rows = 10 (distinct BENs), raw rows = 60 (commitment line items)
        Assert.Equal(-50L, row2023.RawVsSummaryRowCountVariance); // 10 - 60 = -50
    }

    [Fact]
    public async Task ReconcileAsync_WithSummaryProvider_SourceVsSummaryVarianceComputed()
    {
        var svc           = Service();
        var localProvider = new StubLocalProvider("FundingCommitments", []);
        var summaryProvider = SummaryProvider(
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 10,
                Amounts = new Dictionary<string, decimal>
                {
                    ["TotalEligibleAmount"] = 4_800_000m,  // 200k less than source
                    ["CommittedAmount"]     = 4_500_000m,
                } },
        ]);

        var result  = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider, summaryProvider);
        var row2023 = result.Rows.Single(r => r.FundingYear == 2023);

        Assert.NotNull(row2023.SourceVsSummaryAmountVariances);
        Assert.Equal(-200_000m, row2023.SourceVsSummaryAmountVariances["TotalEligibleAmount"]);
    }

    [Fact]
    public async Task ReconcileAsync_WithoutSummaryProvider_SummaryFieldsNull()
    {
        var svc           = Service();
        var localProvider = new StubLocalProvider("FundingCommitments",
        [
            new LocalYearTotals { FundingYear = 2023, RowCount = 60 },
        ]);

        var result  = await svc.ReconcileAsync(DatasetManifests.FundingCommitments, localProvider);
        var row2023 = result.Rows.Single(r => r.FundingYear == 2023);

        Assert.Null(row2023.LocalSummaryRowCount);
        Assert.Null(row2023.LocalSummaryAmounts);
        Assert.Null(row2023.RawVsSummaryRowCountVariance);
    }
}

// ── Summary report writer tests ───────────────────────────────────────────────

public class ReconciliationReportWriterSummaryTests
{
    private static DatasetReconciliationResult ResultWithSummary() => new()
    {
        DatasetName           = "FundingCommitments",
        RunAtUtc              = new DateTime(2026, 3, 17, 9, 0, 0, DateTimeKind.Utc),
        SourceTotalRowCount   = 100,
        LocalRawTotalRowCount = 100,
        Notes                 = "Test.",
        AmountDisplayNames    = new Dictionary<string, string>
        {
            ["TotalEligibleAmount"] = "Total Eligible Amount",
            ["CommittedAmount"]     = "Committed Amount",
        },
        Rows =
        [
            new()
            {
                FundingYear                    = 2023,
                SourceRowCount                 = 60,
                LocalRawRowCount               = 60,
                SourceDistinctApplicants       = 10,
                LocalRawDistinctApplicants     = 10,
                SourceAmounts                  = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m, ["CommittedAmount"] = 4_500_000m },
                LocalRawAmounts                = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m, ["CommittedAmount"] = 4_500_000m },
                LocalSummaryRowCount           = 10,
                LocalSummaryDistinctApplicants = 10,
                LocalSummaryAmounts            = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m, ["CommittedAmount"] = 4_500_000m },
            },
        ],
    };

    [Fact]
    public void BuildMarkdown_WithSummary_ContainsSummaryLabel()
    {
        var md = new ReconciliationReportWriter().BuildMarkdown(ResultWithSummary());
        Assert.Contains("Summary", md);
        Assert.Contains("Layers", md);
    }

    [Fact]
    public void BuildMarkdown_WithSummary_ContainsThreeLayerVarianceColumns()
    {
        var md = new ReconciliationReportWriter().BuildMarkdown(ResultWithSummary());
        Assert.Contains("Src→Raw", md);
        Assert.Contains("Raw→Sum", md);
        Assert.Contains("Src→Sum", md);
    }

    [Fact]
    public void BuildMarkdown_WithSummary_ContainsAmountSections()
    {
        var md = new ReconciliationReportWriter().BuildMarkdown(ResultWithSummary());
        Assert.Contains("Total Eligible Amount", md);
        Assert.Contains("Committed Amount", md);
    }

    [Fact]
    public void BuildMarkdown_WithSummary_2023RowPresent()
    {
        var md = new ReconciliationReportWriter().BuildMarkdown(ResultWithSummary());
        Assert.Contains("2023", md);
    }

    [Fact]
    public void BuildMarkdown_WithoutSummary_DoesNotContainRawToSumHeader()
    {
        var result = new DatasetReconciliationResult
        {
            DatasetName           = "FundingCommitments",
            RunAtUtc              = DateTime.UtcNow,
            SourceTotalRowCount   = 100,
            LocalRawTotalRowCount = 100,
            AmountDisplayNames    = new Dictionary<string, string> { ["TotalEligibleAmount"] = "Total Eligible Amount" },
            Rows = [new() { FundingYear = 2023, SourceRowCount = 60, LocalRawRowCount = 60,
                SourceAmounts = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m },
                LocalRawAmounts = new Dictionary<string, decimal> { ["TotalEligibleAmount"] = 5_000_000m } }],
        };
        var md = new ReconciliationReportWriter().BuildMarkdown(result);
        Assert.DoesNotContain("Raw→Sum", md);
    }
}

// ── YearReconciliationRow summary variance unit tests ─────────────────────────

public class YearReconciliationRowSummaryTests
{
    [Fact]
    public void RawVsSummaryRowCountVariance_SummaryMinusRaw()
    {
        var row = new YearReconciliationRow
        {
            FundingYear          = 2023,
            LocalRawRowCount     = 60,
            LocalSummaryRowCount = 10,
        };
        Assert.Equal(-50L, row.RawVsSummaryRowCountVariance); // 10 - 60
    }

    [Fact]
    public void RawVsSummaryRowCountVariance_NullSummary_ReturnsNull()
    {
        var row = new YearReconciliationRow
        {
            FundingYear          = 2023,
            LocalRawRowCount     = 60,
            LocalSummaryRowCount = null,
        };
        Assert.Null(row.RawVsSummaryRowCountVariance);
    }

    [Fact]
    public void SourceVsSummaryRowCountVariance_SummaryMinusSource()
    {
        var row = new YearReconciliationRow
        {
            FundingYear          = 2023,
            SourceRowCount       = 60,
            LocalSummaryRowCount = 10,
        };
        Assert.Equal(-50L, row.SourceVsSummaryRowCountVariance);
    }

    [Fact]
    public void RawVsSummaryAmountVariances_SummaryMinusRaw()
    {
        var row = new YearReconciliationRow
        {
            FundingYear          = 2023,
            LocalRawAmounts      = new Dictionary<string, decimal> { ["A"] = 1000m },
            LocalSummaryAmounts  = new Dictionary<string, decimal> { ["A"] = 950m },
        };
        Assert.NotNull(row.RawVsSummaryAmountVariances);
        Assert.Equal(-50m, row.RawVsSummaryAmountVariances["A"]); // 950 - 1000
    }

    [Fact]
    public void RawVsSummaryAmountVariances_NullSummary_ReturnsNull()
    {
        var row = new YearReconciliationRow
        {
            FundingYear         = 2023,
            LocalRawAmounts     = new Dictionary<string, decimal> { ["A"] = 1000m },
            LocalSummaryAmounts = null,
        };
        Assert.Null(row.RawVsSummaryAmountVariances);
    }
}

// ── Stub local provider for service tests ────────────────────────────────────

file sealed class StubLocalProvider : ILocalDataProvider
{
    private readonly IReadOnlyList<LocalYearTotals> data;

    public StubLocalProvider(string datasetName, IReadOnlyList<LocalYearTotals> data)
    {
        DatasetName  = datasetName;
        this.data    = data;
    }

    public string DatasetName { get; }

    public Task<IReadOnlyList<LocalYearTotals>> GetLocalRawTotalsAsync(CancellationToken ct = default)
        => Task.FromResult(data);
}

file sealed class StubSummaryProvider : ILocalSummaryProvider
{
    private readonly IReadOnlyList<LocalYearTotals> data;

    public StubSummaryProvider(string datasetName, IReadOnlyList<LocalYearTotals> data)
    {
        DatasetName = datasetName;
        this.data   = data;
    }

    public string DatasetName { get; }

    public Task<IReadOnlyList<LocalYearTotals>> GetLocalSummaryTotalsAsync(CancellationToken ct = default)
        => Task.FromResult(data);
}
