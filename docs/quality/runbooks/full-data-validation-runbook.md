# Full Data Validation Runbook

The authoritative procedure for validating data integrity after a full or partial
data reload. Covers FY2020 through the current partial year.

This runbook defines the **exact operational order** for a safe, complete validation
cycle. Steps must be followed in sequence — running reconciliation before rebuilding
summaries will produce stale or misleading results.

Record all results in `evidence/yearly-quality-log.md`.

---

## Run record (fill in before starting)

| Field | Value |
|---|---|
| **Date** | |
| **Year scope** | (e.g., "FY2020–FY2025 full, FY2026 partial") |
| **App commit / version** | (run `git rev-parse --short HEAD`) |
| **Validator** | |
| **Trigger** | (e.g., "post full-load", "post FY2022 re-import", "routine monthly") |

---

## ⚠ Partial-year caution

**Read before running any validation on the current or most recent funding year.**

The most recent funding year is always partially loaded. USAC publishes data on a
rolling basis throughout the program year and for months after it closes. This means:

- Row counts for the current year will be meaningfully lower than mature years.
  This is not a data error.
- Dollar amounts will be lower in proportion to the incompleteness.
- Advisory signals for the current year may flag applicants whose invoices simply
  have not been processed yet. These are **not** confirmed anomalies.
- A reconciliation "pass" for the current year means Source = Raw for what has been
  published so far — not that the year's data is complete.

**Never apply the same completeness expectations to the current year as to a
mature year (FY2020–FY2022).**

As of the most recent full load (2026-03-18):
- FY2026 (FundingCommitments): ~66,585 rows — clearly partial (~25% of a mature year)
- FY2025 (Disbursements): ~142,440 rows — partially loaded (~52% of FY2022)

When documenting results, always note "partial year — expected incompleteness"
for the highest-numbered year in each dataset.

---

## Prerequisites

Before starting the validation cycle:

- [ ] App is running: `dotnet run --project src/ErateWorkbench.Api`
- [ ] Base URL: `http://localhost:5075`
- [ ] SQLite CLI available: `sqlite3 src/ErateWorkbench.Api/erate-workbench.db`
- [ ] Outbound internet access to `datahub.usac.org`
- [ ] No other import or rebuild operations are currently running (check
      `/imports` endpoint or ImportJobs table for status=1 rows)

---

## Operational order overview

```
  1. Import raw data          POST /import/funding-commitments   (all years — no year scope)
                              POST /import/disbursements          (all years — no year scope)

  2. Rebuild summaries        POST /dev/summary/funding-commitments?year=YYYY
                              POST /dev/summary/disbursements?year=YYYY
                              POST /dev/summary/risk?year=YYYY

  3. Reconcile                POST /dev/reconcile/disbursements
                              POST /dev/reconcile/funding-commitments

  4. Sanity-check summaries   SQL queries (see §4)

  5. Cross-year checks        Review after all years complete (§5)

  6. Smoke test               Run smoke-test-runbook.md §1–4

  7. Log evidence             Append to evidence/yearly-quality-log.md
```

**Import endpoints are not year-scoped.** Both import services always fetch the
complete USAC dataset regardless of any URL parameter. Year-scoped processing
begins at Step 2 (summary rebuild), which accepts `?year=YYYY`. Running imports
one at a time is still advisable to avoid SQLite write conflicts, but there is
no meaningful "per-year import" from a Socrata perspective — each call fetches
all years and upserts idempotently by `RawSourceKey`.

**Do not skip the rebuild steps (Step 2) between import and reconciliation.**
Reconciliation reads from both the raw tables and the summary tables.
Stale summaries will produce misleading Raw→Summary variance.

**Reconciliation endpoints are also not year-scoped.** They fetch all years from
Socrata in a single GROUP BY query and return per-year breakdowns. Do not pass
`?year=YYYY` to reconciliation endpoints — the parameter would be silently ignored.

---

## Step 1 — Confirm raw data is present

Run these SQL queries before importing to establish a baseline:

```sql
-- Funding Commitments by year
SELECT 'FC', FundingYear, COUNT(*) AS rows
FROM FundingCommitments
GROUP BY FundingYear
ORDER BY FundingYear;

-- Disbursements by year
SELECT 'D', FundingYear, COUNT(*) AS rows
FROM Disbursements
GROUP BY FundingYear
ORDER BY FundingYear;
```

**Expected for a full 2020–present load:**

| Year | FC rows (approx) | Disbursement rows (approx) |
|---:|---:|---:|
| 2020 | ~250,000 | ~279,000 |
| 2021 | ~125,000 | ~274,000 |
| 2022 | ~169,000 | ~275,000 |
| 2023 | ~156,000 | ~266,000 |
| 2024 | ~158,000 | ~270,000 |
| 2025 | ~163,000 | ~142,000 (partial) |
| 2026 | ~67,000 (partial) | — (not yet available) |

**Red flags at this step:**
- Any target year with 0 rows → import has not run or failed
- A year with dramatically fewer rows than adjacent years without explanation
  → possible import failure or partial import; do not proceed until explained

---

## Step 2 — Import raw data (if refreshing)

**Skip this step if you are validating an existing load without refreshing data.**

**⚠ Imports are full-dataset, not year-scoped.** Each import fetches the entire
USAC dataset and upserts all rows idempotently. There is no `?year=YYYY` filter
on import endpoints — any such parameter is silently ignored. Year-scoped
processing begins at Step 3 (summary rebuild).

Run both imports. Do not run them concurrently — SQLite does not support
concurrent writers and Socrata rate-limits concurrent callers.

```
POST http://localhost:5075/import/funding-commitments
POST http://localhost:5075/import/disbursements
```

**Timing expectations:**
- Funding Commitments (full dataset): 30–90 minutes for a complete load
- Disbursements (full dataset): 20–60 minutes for a complete load

These are long-running operations. Use Swagger UI or a persistent terminal.
Monitor progress via the `/imports` endpoint or the app logs.

**Verify each import before proceeding:**
- Response `status = "Succeeded"`
- `recordsProcessed > 0`
- No `errorMessage` in response

After imports complete, re-run the row count query from Step 1 to confirm
counts have updated as expected.

---

## Step 3 — Rebuild summary tables

**Must be run after every import, in this exact order:**

For each target year, run all three rebuilds:

```
POST http://localhost:5075/dev/summary/funding-commitments?year=YYYY
POST http://localhost:5075/dev/summary/disbursements?year=YYYY
POST http://localhost:5075/dev/summary/risk?year=YYYY
```

**Why order matters:**
- The disbursement and commitment summaries must be rebuilt before the risk
  summary — the risk builder reads from both.
- Running `dev/summary/risk` before `dev/summary/disbursements` will produce
  a risk summary based on the previous cycle's disbursement summary.

**Per-year expected response values:**

For `dev/summary/disbursements`:
- `rawRowsScanned` ≈ local Disbursements row count for that year
- `includedRows` < `rawRowsScanned` (rows with ApprovedAmount ≤ 0 are excluded)
- `summaryRowsWritten` ≈ distinct (FundingYear, ApplicantEntityNumber) pairs with approved activity

For `dev/summary/risk`:
- `matchedRows + commitmentOnlyRows + disbursementOnlyRows = riskSummaryRowsWritten`
- `riskSummaryRowsWritten` ≈ 15,000–22,000 for a mature year
- `disbursementOnlyRows` > 0 (entities with disbursements but no commitment record)
- `commitmentOnlyRows` > 0 (entities with commitments but no disbursement record)

**Red flags:**
- `riskSummaryRowsWritten = 0` → one of the input summaries is empty; check that
  Steps 2 and 3a/3b completed successfully
- `disbursementOnlyRows = 0` and `commitmentOnlyRows = 0` → full match is unusual;
  suggests the two summaries may be from different data loads

---

## Step 4 — Run reconciliation

Allow up to **5 minutes** for Socrata API calls to complete.
Run in a separate terminal or Swagger UI; do not cancel mid-flight.

**Reconciliation is not year-scoped.** Each reconciliation endpoint queries
Socrata for all years at once via a GROUP BY and returns per-year breakdown
rows. Do not pass `?year=YYYY` — the parameter is silently ignored.

### 4a — Disbursements reconciliation

```
POST http://localhost:5075/dev/reconcile/disbursements
```

**Expected results — mature years (2020–2024):**

| Layer comparison | Expected | Fail condition |
|---|---|---|
| Source row count = Raw row count | Exact match | Any nonzero Src→Raw Δ |
| Source RequestedAmount = Raw RequestedAmount | Exact match | Any nonzero Δ |
| Source ApprovedAmount = Raw ApprovedAmount | Exact match | Any nonzero Δ |
| Raw row count > Summary row count | Yes — ApprovedAmount > 0 filter reduces rows | Summary ≥ Raw is a bug |
| Source distinct applicants ≈ Raw distinct applicants | Exact match | Any difference |
| Summary ApprovedAmount ≈ Raw ApprovedAmount | Near match | >5% variance needs investigation |

**Expected results — current/partial year:**
- Source row count = Raw row count: exact match (for rows published so far)
- All amounts: exact match
- Row counts will be lower than mature years — expected, not a failure

**Interpreting the report:**
Report files are written to `src/ErateWorkbench.Api/reports/`. The markdown report
shows all years in a single table. Review each year's row for variance signals.

### 4b — Funding Commitments reconciliation

```
POST http://localhost:5075/dev/reconcile/funding-commitments
```

**⚠ Known caveat — row count variance is always expected:**

The `avi8-svp9` Socrata dataset contains one row per recipient-of-service (ROS)
per FRN line item. The local import deduplicates by `{FRN}-{form_471_line_item_number}`,
so the source row count will be approximately **10–15× higher** than the local raw count.

Example: Source reports ~2,185,000 rows for FY2022; local has ~169,000. This is correct.

**The primary validation signal for Funding Commitments is amounts, not row counts.**

| Layer comparison | Expected | Fail condition |
|---|---|---|
| Source row count >> Raw row count | Yes — 10–15× variance expected | Source < Raw would be a bug |
| Source TotalEligibleAmount ≈ Raw TotalEligibleAmount | Within ~5% | >10% variance needs investigation |
| Source CommittedAmount ≈ Raw CommittedAmount | Within ~5% | >10% variance needs investigation |
| Raw row count > Summary row count | Yes — summary aggregates by applicant | Summary ≥ Raw is a bug |

**Note on the `ben` column defect:** The Disbursements manifest currently uses
`"ben"` as the Socrata applicant column name; the correct column is
`"billed_entity_number"`. This causes the `COUNT(DISTINCT ...)` query in the
by-year URL to use the wrong column. Distinct applicant counts in Disbursements
reconciliation may be inaccurate until this defect is resolved.

---

## Step 5 — Summary sanity checks

Run these SQL queries after reconciliation to confirm summary table health.

### 5a — Summary row counts by year

```sql
SELECT 'CommitSum', FundingYear, COUNT(*) FROM ApplicantYearCommitmentSummaries
GROUP BY FundingYear ORDER BY FundingYear;

SELECT 'DisbSum',   FundingYear, COUNT(*) FROM ApplicantYearDisbursementSummaries
GROUP BY FundingYear ORDER BY FundingYear;

SELECT 'RiskSum',   FundingYear, COUNT(*) FROM ApplicantYearRiskSummaries
GROUP BY FundingYear ORDER BY FundingYear;
```

Expected: non-zero counts for all loaded years; risk summary count ≈ max of commitment and disbursement summary counts.

### 5b — Disbursement summary ApprovedAmount > 0 check

```sql
-- Should return 0 — no zero-approved rows should be in the summary
SELECT COUNT(*) FROM ApplicantYearDisbursementSummaries WHERE TotalApprovedAmount <= 0;
```

Expected: 0. Any nonzero count means the inclusion rule was violated.

### 5c — Risk summary flag distribution

```sql
SELECT FundingYear,
       SUM(CASE WHEN HasCommitmentData=1 AND HasDisbursementData=1 THEN 1 ELSE 0 END) AS matched,
       SUM(CASE WHEN HasCommitmentData=1 AND HasDisbursementData=0 THEN 1 ELSE 0 END) AS commit_only,
       SUM(CASE WHEN HasCommitmentData=0 AND HasDisbursementData=1 THEN 1 ELSE 0 END) AS disb_only
FROM ApplicantYearRiskSummaries
GROUP BY FundingYear ORDER BY FundingYear;
```

Expected: all three categories nonzero for each year. A year with 0 in `commit_only`
or `disb_only` is unusual and may indicate a rebuild order problem.

### 5d — Risk level distribution

```sql
SELECT FundingYear, RiskLevel, COUNT(*) AS cnt
FROM ApplicantYearRiskSummaries
GROUP BY FundingYear, RiskLevel ORDER BY FundingYear, RiskLevel;
```

Expected: all three levels (High, Moderate, Low) present for each mature year.
A year where 100% of rows are High risk is a scoring or data problem.

### 5e — Amount sanity

```sql
SELECT FundingYear,
       ROUND(SUM(TotalCommittedAmount)/1000000,1) AS committed_M,
       ROUND(SUM(TotalApprovedDisbursementAmount)/1000000,1) AS approved_M
FROM ApplicantYearRiskSummaries
GROUP BY FundingYear ORDER BY FundingYear;
```

Expected: amounts are non-negative; committed_M in the range $1,000M–$5,000M for mature years;
approved_M roughly in proportion to committed_M (typically 60–110%). Zero amounts for a mature
year indicate a rebuild failure or bad import.

---

## Step 6 — Cross-year plausibility checks

After all years are complete, review the evidence log for cross-year patterns.

### 6a — Row count trend (FundingCommitments)

Plot or scan the raw row counts by year. Expected pattern: gradual variation year-over-year.

| Flag | Threshold | Likely cause |
|---|---|---|
| One year has <50% of adjacent years | Cliff | Partial import or failed year |
| One year has >200% of adjacent years | Spike | Duplicate import or import bug |
| All years identical | Suspicious | Import deduplication may be too aggressive |

### 6b — Amount trend

Year-over-year committed amounts should vary by <30% in either direction for mature years.
Larger swings require an explanation grounded in E-Rate program history.

### 6c — Partial-year documentation

The highest-numbered year in each dataset must be explicitly noted as partial in the
evidence log. Do not compare it to prior years without a caveat.

---

## Step 7 — Watchlist items

These are known recurring issues to check explicitly on each validation cycle.

| Item | What to check | Expected / Caution |
|---|---|---|
| FY2021 FC anomaly | FundingCommitments FY2021 has ~125K rows vs ~165K for adjacent years | May be a genuine USAC data characteristic or a partial import; note in evidence |
| Advisory signals for current year | Signals present for the current partial year | Mark all current-year signals as "preliminary — partial year" |
| Summary rebuild order | Confirm risk summary was built after disbursement and commitment summaries | Stale summary will cause Raw→Summary variance to look wrong |
| Analytics in-memory gap sort | `GetTopCommitmentDisbursementGapsAsync` loads all risk rows to memory for ordering | With full multi-year data this may be slow; watch Risk Insights page load time |

---

## Step 8 — Smoke test

After completing §4–6, run the smoke test runbook (§1–4 minimum, §5 if presenting
to stakeholders):

```
docs/quality/runbooks/smoke-test-runbook.md
```

This confirms that the rebuilt data surfaces correctly through the UI and that
Risk Insights year filters are working with the newly rebuilt summaries.

---

## Step 9 — Log evidence

Append a new entry to `evidence/yearly-quality-log.md` using the template in that file.

Minimum fields to record:
- Date, validator, commit SHA, data scope
- Disbursements reconciliation result per year (pass / pass with caveat / fail / skipped)
- Funding Commitments reconciliation result per year (amounts — pass / caveat / fail)
- Summary sanity check results (§5a–5e outcomes)
- Any watchlist items triggered (§7)
- Smoke test outcome
- Overall verdict

---

## Result classification guide

| Result | Meaning |
|---|---|
| **pass** | All expected checks met; no unexplained variance |
| **pass with caveat** | All critical checks met; one or more known, documented variances present (e.g., partial year, `ben` defect) |
| **needs investigation** | Unexpected variance found; cause not yet identified; do not present data until resolved |
| **fail** | Critical check failed (Source ≠ Raw for amounts; summary contains zero-approved rows; rebuild produced 0 rows) |
| **skipped** | Check not run this cycle (e.g., Socrata unavailable, year not yet imported) |
