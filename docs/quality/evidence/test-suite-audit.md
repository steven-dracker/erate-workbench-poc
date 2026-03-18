# Test Suite Audit

**Date:** 2026-03-18
**Commit:** 8e7165a (feature/import-resilience)
**Total tests:** 340 (across 26 test classes in 25 files)
**Runner:** `dotnet test tests/ErateWorkbench.Tests`
**All 340 tests pass.**

---

## Purpose

Map the current automated test suite to the quality taxonomy, identify coverage
gaps, and record recommended next targets. This document is a point-in-time
snapshot. It should be updated after significant test additions or architectural
changes.

---

## Test class inventory

| Class | Tests | Primary taxonomy type | Notes |
|---|---:|---|---|
| `RiskInsightsRepositoryTests` | 42 | integration | Full advisory signal, snapshot, topN, year filter coverage |
| `RiskSummaryBuilderTests` | 29 | integration | Full outer join, year isolation, scoring, name selection |
| `RiskCalculatorTests` | 23 | unit | Scoring formula, classification thresholds, boundary conditions |
| `ProgramWorkflowModelTests` | 17 | unit | Phase structure, step names, backward-compat save keys |
| `DisbursementSummaryBuilderTests` | 15 | integration | Inclusion rule, year isolation, aggregation |
| `ReconciliationReportWriterTests` | 9 | unit | Markdown/JSON output structure, variance display |
| `YearReconciliationRowTests` | 9 | unit | Variance computation model (row count, amounts) |
| `RiskSnapshotDerivedTests` | 9 | unit | Derived totals on `RiskSnapshot` model |
| `SocrataReconciliationServiceTests` | 8 | integration (stubbed HTTP) | Merge of source and local data, variance detection |
| `FundingCommitmentImportServiceTests` | 10 | integration | Idempotency, retry, transient-error classification |
| `ReconciliationManifestTests` | 7 | unit | URL construction, manifest structure |
| `ReconciliationJsonParsingTests` | 8 | unit | JSON parsing edge cases (null, blank, missing key) |
| `FundingCommitmentCsvParserTests` | 8 | unit | Header mapping, RawSourceKey construction, skip logic |
| `ServiceProviderCsvParserTests` | 7 | unit | Header mapping, skip logic |
| `Form471CsvParserTests` | 7 | unit | Header mapping, skip logic |
| `EntityCsvParserTests` | 6 | unit | Header mapping, skip logic |
| `CommitmentSummaryBuilderTests` | 13 | integration | Aggregation, year isolation |
| `EpcEntityRepositoryTests` | 10 | integration | Search, pagination, filtering |
| `AnalyticsRepositoryTests` | 12 | integration | Commitment/disbursement analytics, topN, rural/urban |
| `FundingCommitmentAnalyticsTests` | 10 | integration | Commitment analytics queries |
| `Form471RepositoryTests` | 8 | integration | Upsert, category split, service type queries |
| `FundingCommitmentRepositoryTests` | 5 | integration | Upsert deduplication, idempotency |
| `ServiceProviderRepositoryTests` | 7 | integration | Upsert, join queries |
| `DisbursementCsvParserTests` | 10 | unit | Header mapping, skip logic, RawSourceKey fallback |
| `DisbursementImportServiceTests` | 4 | integration | Idempotency, failure marking |
| `SocrataReconciliationServiceSummaryTests` | 4 | integration (stubbed HTTP) | Three-layer reconciliation with summary provider |
| `YearReconciliationRowSummaryTests` | 5 | unit | Raw↔summary variance computation |
| `FundingCommitmentLocalDataProviderTests` | 3 | integration | Local raw totals grouped by year |
| `FundingCommitmentSummaryLocalProviderTests` | 3 | integration | Local summary totals |
| `DisbursementSummaryLocalProviderTests` | 4 | integration | Local summary totals |
| `AdvisorPlaybookModelTests` | 12 | unit | Phase structure, state progression, content completeness |
| `EntityImportServiceTests` | 4 | integration | Idempotency, failure marking |
| `UnitTest1` | 0 | — | Placeholder only — comment says "see EpcEntityRepositoryTests.cs" |

---

## Coverage map by taxonomy category

### Unit tests — strong coverage

- **Risk scoring:** `RiskCalculatorTests` covers all formula boundary conditions,
  both component percentages (`ReductionPct`, `DisbursementPct`), and all
  classification thresholds. Corner cases (zero-zero → 0.5, clamping) are tested.
- **CSV parsers:** Six parser test classes cover header mapping, skip conditions,
  fallback logic, and `RawSourceKey` construction for all five entity types.
  These anchor the fragile source column name mappings.
- **Reconciliation model math:** `YearReconciliationRowTests` and
  `YearReconciliationRowSummaryTests` test all variance properties including
  null-safety for optional summary fields.
- **Static content models:** `ProgramWorkflowModelTests` (17 tests) and
  `AdvisorPlaybookModelTests` (12 tests) protect phase structure and backward-
  compatible save key behavior against accidental content regressions.

### Integration tests — strong coverage

- **Risk summary builder:** `RiskSummaryBuilderTests` (29 tests) covers the
  full outer join merge for all three outcomes (matched, commitment-only,
  disbursement-only), year-scoped rebuild isolation, name selection, and risk
  scoring integration. This is the most regression-sensitive area and has the
  deepest coverage.
- **Disbursement summary inclusion rule:** `DisbursementSummaryBuilderTests`
  explicitly tests that only `ApprovedAmount > 0` rows enter the summary
  (`RebuildAsync_InclusionRule_*` trio), that year isolation holds, and that
  aggregation is correct.
- **Advisory signals:** `RiskInsightsRepositoryTests` tests all four signal
  types (No Commitment, No Disbursement, High Reduction, Low Utilization),
  year filtering, `topN` cap, ordering, and a row that triggers multiple signals.
- **Reconciliation service:** `SocrataReconciliationServiceTests` uses a stub
  HTTP handler to test the source-vs-local merge, including years present in
  one side only, and amount variance computation. Three-layer reconciliation
  (source/raw/summary) has its own `SocrataReconciliationServiceSummaryTests`.
- **Repository upsert semantics:** `FundingCommitmentRepositoryTests`,
  `ServiceProviderRepositoryTests`, and `Form471RepositoryTests` all verify
  the deduplication-by-`RawSourceKey` contract and idempotent update behavior.

---

## Gaps and open defects

### G1 — `ben` column defect — **FIXED 2026-03-18**

**Fixed in:** `SourceDatasetManifest.cs:82`
**Test updated:** `BuildByYearUrl_Disbursements_ContainsBenAndApprovedAmt`
  → renamed to `BuildByYearUrl_Disbursements_ContainsBilledEntityNumberAndApprovedAmt`

The manifest now uses `"billed_entity_number"` (correct Socrata column) and the
test asserts both the correct value and the absence of `"ben"`:

```csharp
Assert.Contains("billed_entity_number", url);
Assert.DoesNotContain("\"ben\"", url); // regression guard
```

All 334 tests pass after the fix.

---

### G2 — Year-scoped import URL not tested

No test verifies that `&funding_year=YYYY` appears in the Socrata page URL
when a year argument is passed to the funding commitment or disbursement import
services.

**Affected classes:** `FundingCommitmentImportServiceTests`,
`DisbursementImportServiceTests`

These tests verify idempotency, error classification, and retry behavior, but
they do not capture the URL that the import service builds. If the year filter
were accidentally dropped from the URL, imports would silently reload all years
instead of just the target year — a high-impact regression.

**Recommendation:** Add URL-capture assertions to the import service tests.
The stub HTTP handler used in `ReconciliationTests.cs` demonstrates the pattern
(`RequestedUrls` list on `JsonStubHandler`). The same approach can be applied
to the import service tests to assert the constructed page URL includes
`funding_year=YYYY`.

---

### G3 — No partial-year / sparse-data safety tests

**Regression-strategy.md Rule 6** requires verifying that:
- The Risk Insights page does not present the current/partial year without a
  disclaimer or caveat
- The year-selector default does not silently pick the most recent year as
  complete

There are no automated tests for this. Advisory signal year filter tests exist
(`GetAdvisorySignals_YearFilter_ExcludesOtherYears`), but nothing tests the
semantic behavior when data is sparse.

**Current mitigation:** Manual check SEM-RISK-002 in the smoke runbook covers
this. But the smoke runbook explicitly notes "no automatic partial-year
disclaimer on the Risk Insights page" as a known gap in the UI itself.

**Recommendation:** Add to candidates — both the UI disclaimer gap and a unit
test asserting that the available-years query returns years sorted descending
(i.e., the current/latest year comes first in the dropdown) so any year-
selector behavior is testable.

---

### G4 — No end-to-end / HTTP integration tests

There are no tests using `WebApplicationFactory<Program>` or any equivalent.
The full HTTP request pipeline (routing, model binding, API controller behavior,
Swagger configuration) is tested only through the smoke runbook.

This is a known deliberate gap — not a regression. The decision not to add
`WebApplicationFactory` tests has not been recorded as such.

**Recommendation:** Note this as a deliberate gap in the test inventory
(candidate CAND-CI-001 touches this indirectly). If the API surface grows,
reconsider.

---

### G5 — No shared DB test helper

Every integration test class independently constructs:
```csharp
var conn = new SqliteConnection("Data Source=:memory:");
conn.Open();
var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
var db = new AppDbContext(opts);
db.Database.EnsureCreated();
```

There is no shared base class or factory method. This is ~20 lines of identical
setup across roughly 15 test classes, with identical `Dispose` patterns.

**Current state:** Not a correctness issue — the duplication is pure boilerplate
and each test is isolated. The in-memory SQLite per-class pattern is sound.

**Recommendation:** If a future test requires more complex seeding or if the
schema evolves often, consolidate into a `TestDbContext` factory helper.
This would be purely a maintainability improvement and should not be done
unless it's paying its own way.

---

### G6 — No security or performance tests

The `/dev/*` endpoints (summary rebuild, reconciliation) are unauthenticated.
No automated test asserts they are inaccessible in a production configuration.
No performance test exists for page load time under full data load.

These match candidate items CAND-SEC-001, CAND-SEC-002, and CAND-PERF-001 in
the test inventory. Not newly discovered gaps — recorded for completeness.

---

## Taxonomy summary

| Taxonomy type | Test count (approx) | Coverage level |
|---|---:|---|
| unit | ~120 | Strong — all business-rule formulas and parsing logic covered |
| integration (in-memory SQLite) | ~180 | Strong — all builder and repository behaviors covered |
| integration (stubbed HTTP) | ~20 | Good — reconciliation service merge logic covered |
| smoke | 0 (manual only) | Manual only — no `WebApplicationFactory` tests |
| regression | Tagged via naming (`_InclusionRule_`, `_YearScoped_`, `_FullOuterJoin_`) | Embedded in builder tests — not a separate suite |
| data validation | 0 (manual only) | Manual only — see full-data-validation-runbook.md |
| semantic / manual review | 0 (manual only) | Manual only — see smoke-test-runbook.md §5 |
| security | 0 | Not yet started |
| performance | 0 | Not yet started |

---

## Recommended next automated test targets

In priority order:

1. **Fix the `ben` defect test (G1)** — low effort, immediate accuracy gain.
   The test currently documents a bug. Once the manifest is fixed, updating
   the test to assert the correct column name takes minutes and removes a
   source of false confidence.

2. **Add year-scoped URL assertions to import service tests (G2)** — medium
   effort, high regression value. The pattern exists in `ReconciliationTests.cs`
   (`RequestedUrls` on the stub handler). Capturing the URL constructed by
   `FundingCommitmentImportService` and `DisbursementImportService` would anchor
   the year-filter behavior that currently has no automated protection.

3. **`GetAdvisorySignals_TopN_DefaultIsRespected` (missing edge case)** — low
   effort. `GetAdvisorySignals_TopN_LimitsResults` exists, but there is no test
   for the default topN value when no explicit cap is passed. Minor gap.

4. **Year-scoped reconciliation URL with `&funding_year=YYYY` suffix (G2
   variant)** — low effort. `ReconciliationManifestTests` already tests URL
   construction but does not test the year-scoped variant (the `?year=YYYY` API
   parameter that appends `&funding_year=YYYY` to the query). A test for
   `BuildTotalCountUrl_WithYear_IncludesFundingYearFilter` would close the gap
   documented in the full-data-validation-runbook's reconciliation section.

5. **Shared DB helper (G5)** — defer until the boilerplate is causing
   maintenance problems. Not a priority now.

---

## Infrastructure observations

- **No CI configuration exists.** `dotnet test` runs only when invoked manually.
  Candidate CAND-CI-001 tracks this. Until CI is set up, the test suite provides
  no regression protection on commits — only on explicit runs.
- **`UnitTest1.cs`** contains only a comment (`// Placeholder removed — see
  EpcEntityRepositoryTests.cs`). It can be deleted without affecting coverage.
  Retained as a no-op until a cleanup pass is scheduled.
- **Test project uses `GlobalUsings.cs`** for common `xUnit` and namespace
  imports. The pattern is consistent across all test files.

---

## Cleanup performed

**2026-03-18 — `ben` column defect fixed** (G1 above):
- `src/ErateWorkbench.Infrastructure/Reconciliation/SourceDatasetManifest.cs:82`:
  `ApplicantColumn = "ben"` → `ApplicantColumn = "billed_entity_number"`
- `tests/ErateWorkbench.Tests/ReconciliationTests.cs`:
  Renamed `BuildByYearUrl_Disbursements_ContainsBenAndApprovedAmt` to
  `BuildByYearUrl_Disbursements_ContainsBilledEntityNumberAndApprovedAmt`;
  updated assertions to verify correct column name and guard against regression.
- All 334 tests pass.

**2026-03-18 — Runbook corrections:**
- `docs/quality/runbooks/full-data-validation-runbook.md`: Removed incorrect
  `?year=YYYY` from import and reconciliation endpoint examples. Clarified that
  imports are always full-dataset and reconciliation always fetches all years.
- `docs/quality/test-inventory.md`: Updated RECON-URL-002 notes to reflect fix.
- `docs/quality/evidence/yearly-quality-log.md`: Watchlist updated (removed
  resolved `ben` item; added in-memory gap sort as new item).
