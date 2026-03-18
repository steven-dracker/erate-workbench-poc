# Test Inventory

Catalogue of all quality checks for E-Rate Workbench — automated and manual.
Update this file whenever a check is added, changed, or retired.
Status definitions and lifecycle rules: see `strategy/test-lifecycle.md`.

## Column guide

| Column | Meaning |
|---|---|
| **Name** | Short unique identifier for the check |
| **Type** | From `strategy/test-taxonomy.md` |
| **Mode** | `automated` or `manual` |
| **Scope** | What it exercises |
| **Location** | File, class, or runbook section |
| **Status** | `candidate` / `active` / `superseded` / `deprecated` / `removed` |
| **Supersedes / Superseded by** | Cross-reference when status is `superseded` |
| **Notes** | Caveats, known limitations, open defects |

---

## A. Automated tests — unit and integration

Runner: `dotnet test`
Suite location: `tests/ErateWorkbench.Tests/`
Count: 345 (as of 2026-03-18; CC-ERATE-000007 added sections A8–A10; CC-ERATE-000008 corrected smoke-test location references)

### A1 — CSV parsing

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| FC-CSV-001 | unit | automated | `FundingCommitmentCsvParser` header mapping to `FundingCommitmentCsvRow` | `FundingCommitmentCsvParserTests` | active | — | Covers `billed_entity_number` → `ApplicantEntityNumber`, `ros_entity_name` fallback |
| FC-CSV-002 | unit | automated | `RawSourceKey` construction: `{FRN}-{form_471_line_item_number}` | `FundingCommitmentCsvParserTests` | active | — | Key format is critical for idempotent upsert; regression risk if column renamed |
| FC-CSV-003 | unit | automated | Blank/null FRN rows are skipped | `FundingCommitmentCsvParserTests` | active | — | |
| D-CSV-001 | unit | automated | `DisbursementCsvRow` header mapping | `DisbursementCsvParserTests` | active | — | Covers `billed_entity_number`, `funding_year`, `requested_inv_line_amt`, `approved_inv_line_amt` |
| D-CSV-002 | unit | automated | Empty `FundingYear` guard (blank year row skipped) | `DisbursementCsvParserTests` | active | — | Prior bug: empty year caused int parse failure and crashed the import page |

### A2 — Upsert and deduplication

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| FC-UPSERT-001 | integration | automated | Insert a FundingCommitment row; re-insert same `RawSourceKey`; assert count = 1 (not 2) | `FundingCommitmentRepositoryTests` | active | — | Unique constraint on `RawSourceKey` |
| FC-UPSERT-002 | integration | automated | Re-insert with changed fields; assert updated values are stored | `FundingCommitmentRepositoryTests` | active | — | |
| D-UPSERT-001 | integration | automated | Same pattern for Disbursements | `DisbursementRepositoryTests` | active | — | |

### A3 — Summary builders

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| DISB-SUM-001 | integration | automated | `ApplicantYearDisbursementSummaryBuilder.RebuildAsync` groups by (FundingYear, ApplicantEntityNumber) | `DisbursementSummaryBuilderTests` | active | — | |
| DISB-SUM-002 | integration | automated | **Regression:** Only rows with `ApprovedAmount > 0` appear in summary | `DisbursementSummaryBuilderTests` | active | — | Core inclusion rule — must not be relaxed without explicit decision |
| DISB-SUM-003 | integration | automated | Zero-approved rows are excluded from `TotalApprovedAmount` total | `DisbursementSummaryBuilderTests` | active | — | |
| DISB-SUM-004 | integration | automated | Year-scoped rebuild does not affect other years | `DisbursementSummaryBuilderTests` | active | — | Rebuild year N; assert year N±1 rows unchanged |
| COMM-SUM-001 | integration | automated | `ApplicantYearCommitmentSummaryBuilder.RebuildAsync` groups correctly | `CommitmentSummaryBuilderTests` | active | — | |
| COMM-SUM-002 | integration | automated | Year-scoped rebuild does not affect other years | `CommitmentSummaryBuilderTests` | active | — | |

### A4 — Risk summary builder

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| RISK-BUILD-001 | integration | automated | Matched row (commitment + disbursement) produces correct amounts and flags | `RiskSummaryBuilderTests` | active | — | |
| RISK-BUILD-002 | integration | automated | **Regression:** Commitment-only row has `HasDisbursementData=false`, `DisbursementPct=0`, `RiskScore≥0.5` | `RiskSummaryBuilderTests` | active | — | Entities with unfulfilled commitments must appear in output |
| RISK-BUILD-003 | integration | automated | **Regression:** Disbursement-only row has `HasCommitmentData=false`, `RiskScore=0.5`, `RiskLevel="Moderate"` | `RiskSummaryBuilderTests` | active | — | Score=0.5 when both inputs are 0 (insufficient baseline, not low risk) |
| RISK-BUILD-004 | integration | automated | All three join outcomes (matched, commit-only, disb-only) are present in output for seeded data | `RiskSummaryBuilderTests` | active | — | Full outer join completeness |
| RISK-BUILD-005 | integration | automated | Year-scoped rebuild does not affect other years | `RiskSummaryBuilderTests` | active | — | |
| RISK-BUILD-006 | integration | automated | Name selection: commitment name used when both present | `RiskSummaryBuilderTests` | active | — | Falls back to disbursement name; MIN used for determinism when both non-null |

### A5 — Risk scoring

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| RISK-SCORE-001 | unit | automated | `RiskCalculator.ComputeRiskScore(0, 0) = 0.5` | `RiskInsightsRepositoryTests` or inline | active | — | Both-zero case → Moderate, not Low |
| RISK-SCORE-002 | unit | automated | `RiskCalculator.ComputeRiskScore(1, 0) = 1.0` (clamped) | — | active | — | |
| RISK-SCORE-003 | unit | automated | `ClassifyRisk` thresholds: >0.6=High, 0.3–0.6=Moderate, <0.3=Low | — | active | — | |
| RISK-SCORE-004 | unit | automated | Score formula: `0.5 × redPct + 0.5 × (1 − disbPct)` | — | active | — | Regression if weighting ever changes |

### A6 — Advisory signals

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| ADV-001 | integration | automated | "No Commitment" signal emitted for disbursement-only row | `AdvisorySignalTests` or `RiskInsightsRepositoryTests` | active | — | |
| ADV-002 | integration | automated | "No Disbursement" signal emitted for commitment-only row | — | active | — | |
| ADV-003 | integration | automated | "High Reduction" signal emitted when `ReductionPct > 0.5` | — | active | — | Threshold is >50% reduction from eligible to committed |
| ADV-004 | integration | automated | "Low Utilization" signal emitted when `DisbursementPct < 0.5` and `HasCommitmentData=true` | — | active | — | |
| ADV-005 | integration | automated | `topN` cap is respected (default 25) | — | active | — | |

### A7 — Reconciliation

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| RECON-URL-001 | unit | automated | `BuildTotalCountUrl` for FundingCommitments produces correct Socrata simple-filter URL | `ReconciliationUrlTests` | active | — | Must use `&funding_year=YYYY` not `$where` |
| RECON-URL-002 | unit | automated | `BuildByYearUrl` for Disbursements includes `billed_entity_number` in `COUNT(DISTINCT ...)` and does not include `"ben"` | `ReconciliationManifestTests` | active | — | **Fixed 2026-03-18.** `SourceDatasetManifest` was using `"ben"` (wrong); corrected to `"billed_entity_number"`. Test name updated to `BuildByYearUrl_Disbursements_ContainsBilledEntityNumberAndApprovedAmt`. |
| RECON-URL-003 | unit | automated | Year-scoped URL includes `&funding_year=YYYY` suffix | `ReconciliationYearScopedUrlTests` | active | — | |
| RECON-URL-004 | unit | automated | No-year URL omits funding_year filter | `ReconciliationYearScopedUrlTests` | active | — | |
| RECON-MODEL-001 | unit | automated | `YearReconciliationRow.RowCountVariance` = Raw − Source | `ReconciliationModelTests` | active | — | |
| RECON-MODEL-002 | unit | automated | `HasAnyVariance` is false when all row counts and amounts match | `ReconciliationModelTests` | active | — | |
| RECON-RPT-001 | unit | automated | Three-layer header present when summary data exists | `ReconciliationReportWriterTests` | active | — | |
| RECON-RPT-002 | unit | automated | Two-layer header present when no summary data | `ReconciliationReportWriterTests` | active | — | |
| RECON-RPT-003 | unit | automated | Year scope line appears in markdown when `FundingYearScope` is set | `ReconciliationReportWriterTests` | active | — | |

### A8 — Import URL construction (CC-ERATE-000007)

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| IMP-URL-001 | integration (stubbed HTTP) | automated | `FundingCommitmentImportService` page URLs include `$limit=` and `$offset=` | `FundingCommitmentImportServiceTests` | active | — | Verifies Socrata paging parameters are present |
| IMP-URL-002 | integration (stubbed HTTP) | automated | `FundingCommitmentImportService` page URLs do not contain `funding_year=` — imports are always full-dataset | `FundingCommitmentImportServiceTests` | active | — | Documents intentional absence of year filter at import stage |
| IMP-URL-003 | integration (stubbed HTTP) | automated | `DisbursementImportService` page URLs include `$limit=` and `$offset=` | `DisbursementImportServiceTests` | active | — | |
| IMP-URL-004 | integration (stubbed HTTP) | automated | `DisbursementImportService` page URLs do not contain `funding_year=` — imports are always full-dataset | `DisbursementImportServiceTests` | active | — | Documents intentional absence of year filter at import stage |

### A9 — Manifest column regression guards (CC-ERATE-000007)

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| MANIFEST-001 | unit | automated | `DatasetManifests.Disbursements.ApplicantColumn` is `"billed_entity_number"` (not `"ben"`) | `ReconciliationManifestTests` | active | — | **Regression guard** for 2026-03-18 fix. Tests property directly, independent of URL construction. |
| MANIFEST-002 | unit | automated | `DatasetManifests.FundingCommitments.ApplicantColumn` is `"applicant_entity_number"` | `ReconciliationManifestTests` | active | — | Baseline guard for FC manifest column name |
| MANIFEST-003 | unit | automated | `BuildByYearUrl` does not contain `$where=` clause for either dataset | `ReconciliationManifestTests` | active | — | Documents that reconciliation uses simple-filter not `$where` syntax |
| MANIFEST-004 | unit | automated | `BuildTotalCountUrl` does not contain `funding_year=` — reconciliation is not year-scoped | `ReconciliationManifestTests` | active | — | Documents intentional absence of year filter; reconciliation fetches all years at once via GROUP BY |

### A10 — Sparse-data safety (CC-ERATE-000007)

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| SPARSE-001 | integration | automated | Commitment-only row with all-zero amounts (eligible=0, committed=0) produces score=0.5, level=Moderate — no exception | `RiskSummaryBuilderTests` | active | — | Guards zero-denominator path in `ReductionPct` and `DisbursementPct` under partial-year data |
| SPARSE-002 | integration | automated | Disbursement-only row with zero approved amount produces score=0.5, level=Moderate — no exception | `RiskSummaryBuilderTests` | active | — | Guards anomalous data path; in practice excluded by DisbursementSummaryBuilder inclusion rule |
| SPARSE-003 | integration | automated | `GetAdvisorySignalsAsync` with fewer qualifying rows than `topN` returns all qualifying rows without error | `RiskInsightsRepositoryTests` | active | — | Exercises result-set smaller than cap (sparse partial-year data scenario) |

---

## B. Manual smoke tests

Procedure: `runbooks/smoke-test-runbook.md`

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| SMOKE-001 | smoke | manual | Home page loads (HTTP 200, no error banner) | Smoke runbook §1.1 | active | — | |
| SMOKE-002 | smoke | manual | Swagger UI reachable at `/swagger` | Smoke runbook §1.2 | active | — | |
| SMOKE-003 | smoke | manual | Disbursements reconciliation endpoint returns 200 with JSON body | Smoke runbook §3.1 | active | — | Exercises Socrata HTTP + DB + markdown writer end-to-end; URL structure verified by MANIFEST-003/004 |
| SMOKE-004 | smoke | manual | Risk Insights page renders, no JS console errors | Smoke runbook §2.2 | active | — | Semantic honesty requires page to render without error |
| SMOKE-005 | smoke | manual | Program Workflow page renders, year selector present | Smoke runbook §2.6 | active | — | |
| SMOKE-006 | smoke | manual | Idempotent re-import returns 200 with `recordsProcessed > 0` | Smoke runbook §3.3 | active | — | Safe to repeat; idempotent by `RawSourceKey`. **Note:** `?year=YYYY` on import endpoints is silently ignored — imports always fetch the complete dataset. Year-scoped processing begins at the summary rebuild stage. Import URL behavior verified by IMP-URL-001–004. |

---

## C. Data validation checks

Procedure: `runbooks/full-data-validation-runbook.md`
Evidence: `evidence/yearly-quality-log.md`

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| DV-DISB-001 | data validation | manual | Disbursements: Source row count = Raw row count per year | Full-data runbook §3 | active | — | Exact match expected |
| DV-DISB-002 | data validation | manual | Disbursements: Source amounts = Raw amounts per year (Requested + Approved) | Full-data runbook §3 | active | — | Exact match expected |
| DV-DISB-003 | data validation | manual | Disbursements: Raw row count > Summary row count (exclusion of zero-approved rows) | Full-data runbook §3 | active | — | Summary is always a strict subset |
| DV-DISB-004 | data validation | manual | Disbursements: Summary approved amount ≈ Raw approved amount | Full-data runbook §3 | active | — | Near match; small variance acceptable due to ApprovedAmount > 0 filter |
| DV-FC-001 | data validation | manual | FundingCommitments: Source row count >> Raw row count (expected; ROS granularity) | Full-data runbook §4 | active | — | Source has ~10–15× more rows (one per ROS per line item); amounts are the primary signal |
| DV-FC-002 | data validation | manual | FundingCommitments: Source TotalEligibleAmount ≈ Raw TotalEligibleAmount per year | Full-data runbook §4 | active | — | Large amount divergence needs investigation |
| DV-FC-003 | data validation | manual | FundingCommitments: Source CommittedAmount ≈ Raw CommittedAmount per year | Full-data runbook §4 | active | — | |
| DV-CROSS-001 | data validation | manual | No year missing entirely from FundingCommitments or Disbursements (2020–present) | Full-data runbook §1 | active | — | |
| DV-CROSS-002 | data validation | manual | No anomalous row-count cliff or spike between adjacent years | Full-data runbook §5 | active | — | A 2× year-over-year change needs explanation |
| DV-CROSS-003 | data validation | manual | Current/partial year has fewer rows than prior complete years — explained, not flagged as error | Full-data runbook §5 | active | — | Most recent year is always incomplete |
| DV-RISK-001 | data validation | manual | Risk summary row count plausible for each year (≈ distinct applicants in disbursement summary) | Full-data runbook — | active | — | |
| DV-RISK-002 | data validation | manual | No year has 100% High risk (would indicate scoring or data bug) | Full-data runbook — | active | — | |

---

## D. Semantic / manual review checks

| Name | Type | Mode | Scope | Location | Status | Supersedes / Superseded by | Notes |
|---|---|---|---|---|---|---|---|
| SEM-RISK-001 | semantic | manual | Risk Insights advisory signal labels are accurate and not overstated | Pre-release review | active | — | "High Reduction" means >50% of eligible amount was not committed — not the same as fraud |
| SEM-RISK-002 | semantic | manual | Current/partial-year disclaimer is visible and prominent on Risk Insights page | Pre-release review | active | — | Partial year data will inflate apparent anomaly rate |
| SEM-RISK-003 | semantic | manual | Disbursement-only applicants surfaced in advisory signals are not misrepresented as fraudulent | Pre-release review | active | — | These may have legitimate reasons for having no commitment record |
| SEM-RISK-004 | semantic | manual | Waterfall chart (Eligible → Committed → Approved) accurately conveys the funding pipeline | Pre-release review | active | — | |

---

## E. Candidate checks (not yet written)

| Name | Type | Mode | Scope | Notes |
|---|---|---|---|---|
| CAND-REG-001 | regression | automated | Baseline snapshot of total committed/approved amounts by year for a stable data load | See `regression-strategy.md` |
| CAND-REG-002 | regression | automated | Advisory signal counts by type for FY2022 (stable year) against committed baseline | See `regression-strategy.md` |
| CAND-SEC-001 | security | manual | All `/dev/*` endpoints are inaccessible without authentication in a production build | Not started |
| CAND-SEC-002 | security | manual | `year` parameter in import/reconcile endpoints is validated (integer, reasonable range) | Not started |
| CAND-PERF-001 | performance | manual | Risk Insights page load time < 1s with full FY2022 data loaded | Not started |
| CAND-CI-001 | automation | automated | `dotnet test` runs on push to `main` via GitHub Actions | CI not yet configured |
| CAND-DV-001 | data validation | manual | Commitment summary amounts match reconciliation source amounts for a stable year | Requires reconciliation source data to be stable |

---

## F. Deprecated / removed checks

*(None at initial creation. Entries will be added here as checks are retired.)*

| Name | Type | Mode | Scope | Status | Reason | Date |
|---|---|---|---|---|---|---|
| — | — | — | — | — | — | — |
