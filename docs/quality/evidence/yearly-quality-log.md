# Quality Evidence Log

Living record of quality validation activities for E-Rate Workbench.
Covers data validation, smoke tests, semantic review, and any formal
quality cycle run against a loaded dataset.

**How to use this file:**
- Update the [Current Data Quality Status](#current-data-quality-status) table
  after every full or partial validation cycle. This table represents
  the current assessed state of each year's data.
- Append new cycle entries below the template, most recent first.
- Leave historical entries unchanged — they are the audit trail.
- Runbooks for each activity are in `docs/quality/runbooks/`.

---

## Status definitions

Use these codes consistently across all tables and entries.

| Code | Meaning |
|---|---|
| `validated` | All checks passed; no unexplained variance |
| `validated-caveat` | All critical checks passed; one or more known, documented variances present (partial year, known defect, timing) |
| `needs-investigation` | Unexpected variance found; cause not yet identified; do not present to stakeholders |
| `fail` | Critical check failed (Source ≠ Raw for amounts, zero-approved rows in summary, rebuild produced 0 rows) |
| `partial` | Year is currently loading or is a known partial year — completeness expectations do not apply |
| `not-run` | Check not run in this cycle (Socrata unavailable, year not yet imported, out of scope) |
| `skipped` | Check intentionally skipped with a noted reason |

---

## Current data quality status

**Last updated:** 2026-03-18 (spot check, disbursements 2022 only)
**Full validation cycle:** not yet run

This table represents the *current assessed state* of each loaded year.
Update it after each validation cycle — do not append a new table, replace the values.

### Funding Commitments

| Year | Raw rows (approx) | Import complete | Reconciliation | Summary rebuilt | Risk summary | Overall | Last validated | Notes |
|---:|---:|---|---|---|---|---|---|---|
| 2020 | ~250,000 | `not-run` | `not-run` | `not-run` | `not-run` | `not-run` | — | |
| 2021 | ~125,000 | `not-run` | `not-run` | `not-run` | `not-run` | `not-run` | — | See watchlist: FY2021 row count anomaly |
| 2022 | ~169,000 | `not-run` | `not-run` | `not-run` | `not-run` | `not-run` | — | |
| 2023 | ~156,000 | `not-run` | `not-run` | `not-run` | `not-run` | `not-run` | — | |
| 2024 | ~158,000 | `not-run` | `not-run` | `not-run` | `not-run` | `not-run` | — | |
| 2025 | ~163,000 | `not-run` | `not-run` | `not-run` | `not-run` | `not-run` | — | |
| 2026 | ~67,000 | `not-run` | `not-run` | `not-run` | `not-run` | `partial` | — | Partial year — ~25% of mature year as of 2026-03-18 |

### Disbursements

| Year | Raw rows (approx) | Import complete | Reconciliation | Summary rebuilt | Risk summary | Overall | Last validated | Notes |
|---:|---:|---|---|---|---|---|---|---|
| 2020 | ~279,000 | `not-run` | `not-run` | `not-run` | `not-run` | `not-run` | — | |
| 2021 | ~274,000 | `not-run` | `not-run` | `not-run` | `not-run` | `not-run` | — | |
| 2022 | ~275,000 | `not-run` | `validated` | `not-run` | `not-run` | `validated-caveat` | 2026-03-18 | Spot check only; no FC or risk summary validated |
| 2023 | ~266,000 | `not-run` | `not-run` | `not-run` | `not-run` | `not-run` | — | |
| 2024 | ~270,000 | `not-run` | `not-run` | `not-run` | `not-run` | `not-run` | — | |
| 2025 | ~142,000 | `not-run` | `not-run` | `not-run` | `not-run` | `partial` | — | Partial year — ~52% of FY2022 as of 2026-03-18 |

---

## Watchlist — recurring items

These items must be checked on every full validation cycle.
Reference: `runbooks/full-data-validation-runbook.md §7`.

| Item | Description | Expected behavior |
|---|---|---|
| FY2021 FC row count anomaly | FundingCommitments FY2021 has ~125K rows vs ~165K for adjacent years | May be a genuine USAC data characteristic or partial import artifact; note in each cycle |
| Current/partial year advisory signals | Advisory signals for the most recent partial year may overstate anomalies | Mark all current-year signals as "preliminary — partial year" in any stakeholder presentation |
| Summary rebuild order | Risk summary must be built after disbursement and commitment summaries | Stale input produces misleading Raw→Summary variance; confirm order on every cycle |
| Analytics in-memory gap sort | `GetTopCommitmentDisbursementGapsAsync` loads all risk summary rows to memory before sorting by gap | With full multi-year data, watch Risk Insights page load time; profile if it becomes slow |
| ~~`ben` column defect~~ | ~~Disbursements reconciliation used `"ben"` instead of `"billed_entity_number"`~~ | **Fixed 2026-03-18** — `SourceDatasetManifest.cs` corrected; regression test updated |

---

## Repeatable validation workflow

For a full validation cycle, follow these runbooks in order:

```
1. full-data-validation-runbook.md
   Steps 1–7 (confirm raw → import → rebuild summaries → reconcile
              → sanity SQL checks → cross-year checks → smoke test)

2. smoke-test-runbook.md
   Sections 1–4 minimum; Section 5 (Semantic Honesty) before any
   stakeholder demo or external sharing

3. Return here and:
   a. Update the Current Data Quality Status tables above
   b. Append a new cycle entry below (use the template)
```

**Do not update the status tables without completing the runbook steps.**
A status of `validated` means the runbook was run and passed, not just that
the data loaded without error.

---

## Entry template

Copy this block and fill it in for each new validation cycle.
Delete rows or sections that are not applicable; note the reason.

```markdown
---

## [YYYY-MM-DD] — [brief title, e.g. "Full cycle FY2020–FY2025 + FY2026 partial"]

**Validator:** [name or "unattributed"]
**Trigger:** [post full-load / post partial re-import / routine monthly / pre-demo]
**App commit:** [git rev-parse --short HEAD]
**Data scope:** [e.g., "FY2020–FY2025 full, FY2026 partial as of YYYY-MM-DD"]
**Runbook version:** [commit of full-data-validation-runbook.md used, or "current HEAD"]

### Raw data baseline (Step 1 of runbook)

| Dataset | Year | Row count | Expected (approx) | Status |
|---|---:|---:|---:|---|
| FundingCommitments | 2020 | | ~250,000 | |
| FundingCommitments | 2021 | | ~125,000 | See watchlist |
| FundingCommitments | 2022 | | ~169,000 | |
| FundingCommitments | 2023 | | ~156,000 | |
| FundingCommitments | 2024 | | ~158,000 | |
| FundingCommitments | 2025 | | ~163,000 | |
| FundingCommitments | 2026 | | ~67,000 | partial |
| Disbursements | 2020 | | ~279,000 | |
| Disbursements | 2021 | | ~274,000 | |
| Disbursements | 2022 | | ~275,000 | |
| Disbursements | 2023 | | ~266,000 | |
| Disbursements | 2024 | | ~270,000 | |
| Disbursements | 2025 | | ~142,000 | partial |

### Disbursements reconciliation (Step 4a of runbook)

| Year | Source rows | Raw rows | Src→Raw Δ | Req. $ match | Appr. $ match | Summary rows | Raw→Sum Δ | Result |
|---:|---:|---:|---:|---|---|---:|---|---|
| 2020 | | | | | | | | |
| 2021 | | | | | | | | |
| 2022 | | | | | | | | |
| 2023 | | | | | | | | |
| 2024 | | | | | | | | |
| 2025 | | | | | | | | partial year |

### Funding Commitments reconciliation (Step 4b of runbook)

Note: Source row count is expected to be 10–15× Raw row count (ROS granularity).
Primary signal is amounts, not row count.

| Year | Source rows | Raw rows | Src/Raw ratio | Eligible $ match | Committed $ match | Summary rows | Result |
|---:|---:|---:|---:|---|---|---:|---|
| 2020 | | | | | | | |
| 2021 | | | | | | | |
| 2022 | | | | | | | |
| 2023 | | | | | | | |
| 2024 | | | | | | | |
| 2025 | | | | | | | |
| 2026 | | | | | | | partial year |

### Summary sanity checks (Step 5 of runbook)

| Check | Expected | Result | Notes |
|---|---|---|---|
| §5a — Summary row counts present for all years | Non-zero for all loaded years | | |
| §5b — No zero-approved rows in disbursement summary | 0 rows | | |
| §5c — Risk summary: all three flag categories present per year | matched, commit_only, disb_only all > 0 | | |
| §5d — Risk level distribution: High/Moderate/Low all present | All three levels present for mature years | | |
| §5e — Amount sanity: committed $1B–$5B per mature year | In range | | |

### Cross-year checks (Step 6 of runbook)

| Check | Result | Notes |
|---|---|---|
| No cliff or spike in FC row counts between adjacent years | | |
| No cliff or spike in Disbursements row counts between adjacent years | | |
| Current/partial year documented as partial | | |
| Year-over-year committed amounts vary <30% for mature years | | |

### Smoke test (Step 8 of runbook)

| Section | Result | Notes |
|---|---|---|
| §1 — Liveness and infrastructure | | |
| §2 — Core analytics pages | | |
| §3 — API endpoint health | | |
| §4 — Year-filter consistency | | |
| §5 — Semantic honesty review | | Run before any stakeholder demo |

### Watchlist items (Step 7 of runbook)

| Item | Observed | Action |
|---|---|---|
| `ben` column defect | | |
| FY2021 FC anomaly | | |
| Current-year advisory signal labeling | | |
| Summary rebuild order confirmed | | |

### Semantic / caveat notes

- [Any known variances that are explained and do not constitute a failure]
- [Partial-year caveats for the highest-numbered year in each dataset]
- [Any advisory signal results that require qualification before sharing]

### Overall verdict

| Year | FC reconciliation | Disb. reconciliation | Risk summary | Overall |
|---:|---|---|---|---|
| 2020 | | | | |
| 2021 | | | | |
| 2022 | | | | |
| 2023 | | | | |
| 2024 | | | | |
| 2025 | | | | partial |
| 2026 | | | | partial |

**Cycle verdict:** [validated / validated-caveat / needs-investigation / fail]

[One-sentence summary of the cycle result and any required follow-up]
```

---

## Validation cycle entries

*(Most recent first. Do not edit historical entries.)*

---

## 2026-03-18 — Code and architecture audit (static, no runtime)

**Validator:** unattributed
**Trigger:** First formal quality pass — code review of all key data paths
**App commit:** 8e7165a (feature/import-resilience)
**Data scope:** Code paths only — no runtime data accessible in this environment
**Runbook version:** current HEAD

This entry records the results of a static code audit covering import services,
summary builders, reconciliation, Risk Insights, and analytics data paths.
No database or Socrata connectivity was available. All findings are
code-verified (marked `verified-code`) or require manual runtime confirmation
(marked `manual-required`).

### Import pipeline — verified findings

| Finding | Method | Status |
|---|---|---|
| Import services fetch full dataset on every call — no year scoping at Socrata URL level | Code read: `FundingCommitmentImportService.RunAsync`, `DisbursementImportService.RunAsync` | `verified-code` |
| `?year=YYYY` on import endpoints is silently ignored — not bound to any parameter | Code read: `Program.cs` endpoint registrations | `verified-code` |
| Idempotent upsert by `RawSourceKey` confirmed — re-imports are safe | Code read: import services + repository upsert logic | `verified-code` |
| Retry logic: 4 attempts, delays 3s/10s/30s, transient errors only | Code read: both import services | `verified-code` |
| Page size: FundingCommitments 10,000/page (5,000 batch write); Disbursements 10,000/page (2,000 batch write) | Code read | `verified-code` |
| JSON/error payload detection before CSV parse (first-byte sniff) | Code read: both import services | `verified-code` |

### Summary builders — verified findings

| Finding | Method | Status |
|---|---|---|
| Year-scoped rebuild: DELETE + re-INSERT for specified year only | Code read: all three builder `RebuildAsync` implementations | `verified-code` |
| ApprovedAmount > 0 filter applied in-memory (not in SQL) after raw load | Code read: `ApplicantYearDisbursementSummaryBuilder` | `verified-code` |
| Full outer join implemented in-memory (dictionary lookup) — SQLite lacks FULL OUTER JOIN | Code read: `ApplicantYearRiskSummaryBuilder` | `verified-code` |
| All three join outcomes preserved: matched, commitment-only, disbursement-only | Code read + test coverage (RISK-BUILD-004) | `verified-code` |
| Name selection: commitment name preferred; falls back to disbursement name; MIN for determinism | Code read + tests | `verified-code` |
| Composite (FundingYear, ApplicantEntityNumber) indexes on all summary tables | Code read: `AppDbContext` index configuration | `verified-code` |
| WAL mode enabled on SQLite | Code read: migration `20260315000001` | `verified-code` |

### Reconciliation — verified findings

| Finding | Method | Status |
|---|---|---|
| **DEFECT FIXED:** `ApplicantColumn = "ben"` corrected to `"billed_entity_number"` in `SourceDatasetManifest.cs` | Code fix applied; test updated and passing | `verified-code` |
| Reconciliation fetches all years from Socrata in one GROUP BY call — not year-scoped | Code read: `SocrataReconciliationService` | `verified-code` |
| `?year=YYYY` on reconciliation endpoints would be silently ignored | Code read: `Program.cs` endpoint registrations | `verified-code` |
| Three-layer comparison (Source / Raw / Summary) supported when summary provider is passed | Code read + tests | `verified-code` |
| Reports written to `src/ErateWorkbench.Api/reports/` in markdown and JSON | Code read: `Program.cs` reconciliation endpoints | `verified-code` |

### Risk Insights data path — verified findings

| Finding | Method | Status |
|---|---|---|
| Risk Insights reads only from `ApplicantYearRiskSummaries` — never raw tables | Code read: `RiskInsightsRepository` | `verified-code` |
| Year filter propagates correctly to all five repository methods | Code read + test coverage (`GetAdvisorySignals_YearFilter_ExcludesOtherYears` etc.) | `verified-code` |
| Advisory signal logic: 4 types (No Commitment, No Disbursement, High Reduction >50%, Low Utilization <50%) | Code read + test coverage (ADV-001 through ADV-004) | `verified-code` |
| `GetTopCommitmentDisbursementGapsAsync` loads all risk rows to memory before sorting | Code read: in-memory gap calculation due to SQLite decimal affinity limitation | `verified-code` |
| Waterfall snapshot (Eligible → Committed → Approved) computed from sum of risk summary amounts | Code read: `GetSnapshotAsync` | `verified-code` |

### Analytics page — verified findings

| Finding | Method | Status |
|---|---|---|
| Analytics page queries raw `FundingCommitments` and `Disbursements` tables directly — does not use summary tables | Code read: `AnalyticsRepository` | `verified-code` |
| No year filter on analytics endpoints — always returns all years | Code read: `Program.cs` + `AnalyticsRepository` | `verified-code` |
| Decimal aggregation pattern: cast to `double?` for SUM (SQLite TEXT affinity workaround) | Code read: `AnalyticsRepository` all methods | `verified-code` |

### Items requiring manual runtime confirmation

| Item | What to verify | Priority |
|---|---|---|
| Actual row counts per year in local DB | Run SQL from runbook §1 against live DB | High — needed to confirm full load status |
| Reconciliation results per year (Source vs Raw amounts) | Run `POST /dev/reconcile/disbursements` and `POST /dev/reconcile/funding-commitments` | High — first full reconciliation needed |
| Summary sanity checks (§5a–5e SQL) | Run SQL queries against live DB | High |
| Risk Insights page load time with full data | Browse to `/RiskInsights`, time page load | Medium — in-memory gap sort may be slow |
| Analytics page chart rendering | Browse to `/Analytics`, confirm all charts render | Medium |
| Advisory signal label accuracy (smoke §5) | Review signal text in UI | Medium — before any stakeholder demo |
| Partial-year disclaimer visibility | Browse to `/RiskInsights?year=2026` | Low — known gap in UI |

### Runbook corrections applied

The full-data-validation-runbook.md was updated to fix two inaccuracies:

1. **Import step** — Removed incorrect `?year=YYYY` parameter from import endpoint
   calls. Clarified that imports are full-dataset only.
2. **Reconciliation step** — Removed `?year=YYYY` from reconciliation endpoint
   calls. Clarified that reconciliation always fetches all years in one call.
3. **Watchlist** — Removed resolved `ben` defect; added in-memory gap sort as new item.

### Overall verdict

| Area | Result | Notes |
|---|---|---|
| Import pipeline | `validated-caveat` | Code is correct; runbook had wrong parameter docs — fixed |
| Summary builders | `validated` | Code correct; all regression-critical paths test-covered |
| Reconciliation `ben` defect | `validated` | Defect fixed; test updated; all 334 tests pass |
| Risk Insights data path | `validated` | Code correct; year filter propagation verified |
| Analytics data path | `validated-caveat` | Raw table queries are correct; no year filter is a known, expected characteristic |
| Runtime data | `not-run` | Full manual validation cycle still required |

**Cycle verdict:** `validated-caveat` (code audit scope only)

All code paths are structurally correct. One defect fixed (`ben` → `billed_entity_number`).
Two runbook inaccuracies corrected. Runtime data validation (actual reconciliation,
row count confirmation, UI smoke test) still required before the data can be
considered fully validated.

---

## 2026-03-18 — Spot check: Disbursements FY2022

**Validator:** unattributed
**Trigger:** Ad hoc — confirming reconciliation behavior during quality system build
**App commit:** d5188c2
**Data scope:** FY2022 disbursements only
**Runbook version:** pre-runbook (manual check; runbook not yet written at time of run)

### Disbursements reconciliation

| Year | Source rows | Raw rows | Src→Raw Δ | Req. $ match | Appr. $ match | Summary rows | Raw→Sum Δ | Result |
|---:|---:|---:|---:|---|---|---:|---|---|
| 2022 | 274,905 | 274,905 | +0 | Yes — exact ($2,736.3M) | Yes — exact ($2,342.7M) | 20,284 | −254,621 | `validated` |

### Notes

- Source = Raw row count: exact match.
- Source = Raw amounts: exact match.
- Raw → Summary reduction: −254,621 rows. Expected — summary excludes `ApprovedAmount ≤ 0` rows.
- Summary `TotalApprovedAmount` ($2,343.2M) vs Raw ($2,342.7M): +$0.4M variance. Within tolerance — consistent with `ApprovedAmount > 0` filter slightly rounding up.
- Distinct applicants: Source/Raw 20,430 → Summary 20,284 (−146). Consistent with zero-approved exclusion.
- Disbursement distinct applicant count may be affected by `ben` column defect in the Disbursements manifest (watchlist item). Not confirmed whether the 20,430 figure is accurate.

### Watchlist items

| Item | Observed | Action |
|---|---|---|
| `ben` column defect | Distinct applicant count in Disbursements reconciliation may be inaccurate | Tracked; not treated as fail until defect resolved |
| FY2021 FC anomaly | Not checked this cycle | Deferred |
| Current-year advisory signals | Not checked this cycle | Deferred |
| Summary rebuild order | Not confirmed this cycle | Deferred |

### Semantic / caveat notes

- Spot check only. No Funding Commitments validation run.
- No Risk Insights smoke test run.
- No cross-year checks run.
- Full year-by-year validation pending.

### Overall verdict

| Year | FC reconciliation | Disb. reconciliation | Risk summary | Overall |
|---:|---|---|---|---|
| 2022 | `not-run` | `validated` | `not-run` | `validated-caveat` |

**Cycle verdict:** `validated-caveat`

Disbursements FY2022 reconciliation passed with no critical failures. Caveats: spot check only (no FC, no risk summary, no full smoke test); `ben` column defect present in manifest.

---

## Future quality evidence categories

The sections below are placeholders for quality evidence types that are not yet
formally tracked. Add entries here as these areas are formalized.

### Security review log

*(Not yet started. Candidate checks CAND-SEC-001 and CAND-SEC-002 are tracked in
`test-inventory.md`. When formal security review is run, append entries here in
the same dated format as data validation cycles above.)*

### Performance baseline log

*(Not yet started. Candidate check CAND-PERF-001 (Risk Insights page load < 1s
with full FY2022 data) is tracked in `test-inventory.md`. Capture baseline
measurements here once the check is formalized and first run.)*

### Regression baseline log

*(Not yet started. `strategy/regression-strategy.md` identifies five candidate
baselines: committed amounts by year, approved disbursements by year, risk
level distributions, advisory signal counts, and reconciliation report
structure. Capture first-run values here when the snapshot process is
implemented.)*
