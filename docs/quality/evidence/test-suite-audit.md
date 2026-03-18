# Test Suite Audit

**Date:** 2026-03-18
**Commit:** feature/import-resilience (updated via CC-ERATE-000007)
**Total tests:** 345 (across 26 test classes in 25 files)
**Runner:** `dotnet test tests/ErateWorkbench.Tests`
**All 345 tests pass.**

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

### G2 — Year-scoped import URL not tested — **ADDRESSED 2026-03-18**

**Corrected understanding:** Import services are not year-scoped by design.
`FundingCommitmentImportService.RunAsync` and `DisbursementImportService.RunAsync`
fetch the complete USAC dataset (`$limit/$offset` paging only); no
`funding_year=` filter is ever appended to import URLs.

**New tests added (CC-ERATE-000007):**
- `RunAsync_ConstructedPageUrls_ContainLimitAndOffset` (both services) — verifies paging params present
- `RunAsync_ConstructedPageUrls_DoNotContainFundingYearFilter` (both services) — documents that imports are always full-dataset; absence of year filter is correct behavior

These tests capture the constructed Socrata URLs via a recording closure on the
stub handler and assert the invariants. The absence of `funding_year=` is
explicitly documented as intentional — imports fetch all years; year-scoped
processing begins at the summary rebuild stage. See IMP-URL-001 through
IMP-URL-004 in `test-inventory.md`.

**Tests added to:** `FundingCommitmentImportServiceTests`, `DisbursementImportServiceTests`

---

### G3 — No partial-year / sparse-data safety tests — **PARTIALLY ADDRESSED 2026-03-18**

**New tests added (CC-ERATE-000007):**
- `RebuildAsync_ZeroAmountCommitmentOnlyRow_ScoreIsHalfAndLevelIsModerate` — commitment row
  with `eligible=0, committed=0` produces score=0.5, Moderate, no exception (guards
  zero-denominator in `ReductionPct` / `DisbursementPct` under partial-year data)
- `RebuildAsync_ZeroAmountDisbursementOnlyRow_ScoreIsHalfAndLevelIsModerate` — disbursement-only
  row with `approved=0` produces score=0.5, Moderate, no exception
- `GetAdvisorySignals_WhenFewerRowsQualifyThanTopN_ReturnsAllQualifyingRows` — 2 qualifying
  entities with `topN=25` returns exactly 2 without error (exercises sparse result-set path)

See SPARSE-001 through SPARSE-003 in `test-inventory.md`.

**Remaining gap:** No automated test covers the UI disclaimer behavior — whether
the Risk Insights page shows a caveat when the selected year is partial. This
remains a manual check (SEM-RISK-002). The smoke runbook notes "no automatic
partial-year disclaimer on the Risk Insights page" as a known gap in the UI itself.

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

1. ~~**Fix the `ben` defect test (G1)**~~ — **Done 2026-03-18** (G1 section above).

2. ~~**Add year-scoped URL assertions to import service tests (G2)**~~ — **Done 2026-03-18**
   (CC-ERATE-000007). Tests now document that imports are full-dataset by design and
   capture `$limit`/`$offset` paging parameters. See IMP-URL-001 through IMP-URL-004.

3. ~~**`GetAdvisorySignals_TopN_DefaultIsRespected` (missing edge case)**~~ — **Superseded
   2026-03-18** by `GetAdvisorySignals_WhenFewerRowsQualifyThanTopN_ReturnsAllQualifyingRows`
   (SPARSE-003), which exercises the under-count path more meaningfully.

4. **Year-scoped reconciliation URL with `&funding_year=YYYY` suffix (G2 variant)** —
   low effort. `ReconciliationManifestTests` already tests URL construction but does not
   test the year-scoped variant. A test for `BuildTotalCountUrl_WithYear_IncludesFundingYearFilter`
   would close the gap documented in the full-data-validation-runbook.

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

**2026-03-18 — CC-ERATE-000007 targeted automation phase:**
- `tests/ErateWorkbench.Tests/ReconciliationTests.cs`: Added 4 manifest regression
  guard tests to `ReconciliationManifestTests` class (MANIFEST-001 through MANIFEST-004):
  direct property assertions for `ApplicantColumn` on both manifests; URL structure
  guards confirming no `$where=` or `funding_year=` in reconciliation URLs.
- `tests/ErateWorkbench.Tests/FundingCommitmentImportServiceTests.cs`: Added 2 URL
  construction tests (IMP-URL-001, IMP-URL-002): paging parameters present; no year
  filter in import URLs.
- `tests/ErateWorkbench.Tests/DisbursementImportServiceTests.cs`: Added 2 URL
  construction tests (IMP-URL-003, IMP-URL-004): same pattern.
- `tests/ErateWorkbench.Tests/RiskSummaryBuilderTests.cs`: Added 2 sparse-data safety
  tests (SPARSE-001, SPARSE-002): zero-amount commitment-only and disbursement-only rows
  produce score=0.5, Moderate, no exception.
- `tests/ErateWorkbench.Tests/RiskInsightsRepositoryTests.cs`: Added 1 TopN under-count
  test (SPARSE-003): fewer qualifying rows than topN cap returns all without error.
- `docs/quality/test-inventory.md`: Added sections A8–A10 for new automated checks;
  updated count to 345.
- All 345 tests pass.

**2026-03-18 — CC-ERATE-000008 lifecycle and documentation alignment:**
- `docs/quality/test-inventory.md`: Corrected SMOKE-001 through SMOKE-006 section
  references (all were pointing to wrong runbook sections); corrected SMOKE-006
  description from "Year-scoped import" to "Idempotent re-import" (imports are not
  year-scoped); added automation cross-references to SMOKE-003 and SMOKE-006 notes.
- `docs/quality/runbooks/smoke-test-runbook.md`: Fixed §3.3 heading and removed
  misleading `?year=2022` from the import URL example; added ⚠ note that
  `?year=YYYY` is silently ignored on import endpoints; added automation annotation
  blocks to §3.1 (MANIFEST-003/004) and §3.3 (IMP-URL-001–004).
- `docs/quality/strategy/test-lifecycle.md`: Added "Net-new automated tests" section
  covering the case where a test covers a previously unprotected area with no prior
  manual predecessor (applies to all CC-ERATE-000007 additions).
- Gaps G2 and G3 are now accurately described in this audit as closed/partially closed.
