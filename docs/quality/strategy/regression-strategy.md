# Regression Strategy

Policy for deciding when something warrants a regression test, what baselines
to capture, and how the relationship between manual checks and automation evolves.

---

## When a bug fix should become a regression test

The default answer is: **always**, with a narrow exception.

A bug fix must become an automated regression test when:

1. **The bug was not caught by existing tests.** If a test should have caught it
   and didn't, that test is insufficient — add the case or fix the test.
2. **The fix involves a business rule.** Business rules (inclusion criteria,
   scoring formulas, column name mappings) encode intentional behavior that is
   easy to silently revert during refactoring.
3. **The fix involves a fragile interface.** Any place where the code depends on
   an exact string (a Socrata column name, a CSV header, a SoQL keyword) is
   prone to quiet breakage. Tests must anchor it.
4. **The fix involves a conditional path that only fires on real data.** If the
   bug only manifested at a certain data shape (e.g., a row with no commitment
   but a disbursement, or a year with partial data), add that data shape to the
   test suite.

The narrow exception: if the bug was a one-time operational mistake (wrong
endpoint called, wrong year argument) with no code change involved, a regression
test is not required — but the incident should be noted in `evidence/yearly-quality-log.md`.

---

## Business rules that must be regression-protected

These rules encode intentional design decisions that are not self-evident from
the code and would be easy to accidentally change.

### Rule 1 — Disbursement inclusion: `ApprovedAmount > 0`

`ApplicantYearDisbursementSummaryBuilder` aggregates only rows where
`ApprovedAmount > 0`. Rows with null, zero, or negative approved amounts are
excluded from the summary. No `InvoiceLineStatus` filter is applied.

**Why this matters:** The Risk Insights page computes `DisbursementPct` as
`ApprovedAmount / CommittedAmount`. Including zero-approved rows in the summary
would deflate `TotalApprovedAmount` and inflate the apparent execution risk for
applicants who had invoices processed but not paid — misleading the analyst.

**Regression test requirement:** Given seeded disbursement rows mixing
approved > 0 and approved ≤ 0, assert that only the approved > 0 rows appear
in the summary, and that the total approved amount matches exactly.

---

### Rule 2 — Risk summary merge: full outer join in memory

`ApplicantYearRiskSummaryBuilder` performs a full outer join between
`ApplicantYearCommitmentSummary` and `ApplicantYearDisbursementSummary`
using in-memory dictionary lookup (SQLite does not support `FULL OUTER JOIN`).
Three join outcomes must all be preserved:

| Case | `HasCommitmentData` | `HasDisbursementData` | Behavior |
|---|---|---|---|
| Matched | true | true | Both sets of amounts populated |
| Commitment-only | true | false | DisbursementPct=0 → RiskScore≥0.5, always flagged |
| Disbursement-only | false | true | ReductionPct=0, DisbursementPct=0 → Score=0.5 (Moderate) |

**Why this matters:** Silently dropping disbursement-only rows would cause
entities that received disbursements without a matching commitment record to
disappear from Risk Insights entirely. Silently dropping commitment-only rows
would hide applicants with unfulfilled commitments.

**Regression test requirement:** Given one row of each join type per year,
assert that the risk summary contains all three, with the correct flags and scores.

---

### Rule 3 — Risk scoring formula and thresholds

`RiskCalculator.ComputeRiskScore`:
```
score = 0.5 × ReductionPct + 0.5 × (1 − DisbursementPct), clamped to [0, 1]
```
`ClassifyRisk` thresholds: High > 0.6, Moderate 0.3–0.6, Low < 0.3.

**Why this matters:** The formula weights reduction risk and disbursement gap
equally. Changing one coefficient without updating the other would silently skew
every risk score in the system. The thresholds determine which entities are
surfaced by advisory signals.

**Regression test requirement:** Fixed-input tests that assert exact scores and
levels at formula boundary conditions (score = 0.0, 0.3, 0.6, 1.0; both inputs 0).

---

### Rule 4 — Year-scoped rebuild does not corrupt other years

`RebuildAsync(fundingYear: YYYY)` must delete and re-insert rows only for
that funding year. It must not touch rows belonging to other years.

**Why this matters:** Year-by-year data loads are the primary operational pattern.
A bug that causes an annual rebuild to truncate adjacent years would corrupt the
entire dataset silently — the rebuild would "succeed" and the loss would only be
detected during a subsequent reconciliation.

**Regression test requirement:** Seed rows for two years; call `RebuildAsync`
scoped to one year; assert the other year's rows are unchanged.

---

### Rule 5 — Source manifest column name mapping

`DatasetManifests` maps Socrata source column names to local C# properties.
These string literals are the bridge between the Socrata API schema and the
local import. When USAC renames or reorganizes a dataset, a column name
change here can silently zero out an amount field or break reconciliation.

Known fragile mappings:
- `avi8-svp9` (FundingCommitments): `post_discount_extended_eligible_line_item_costs`
  → `CommittedAmount`; `pre_discount_extended_eligible_line_item_costs`
  → `TotalEligibleAmount`. These column names are long and schema-specific.
- `jpiu-tj8h` (Disbursements): `billed_entity_number` → applicant column.
  Note: the static manifest currently uses `"ben"` (incorrect; the real column
  is `"billed_entity_number"`). This is a known open defect.
- Amount metric columns in `AmountMetricDefinition.SourceColumn` must exactly
  match the column names returned by Socrata's SoQL JSON endpoint.

**Regression test requirement:** Tests for `BuildByYearUrl` and `BuildTotalCountUrl`
that assert each known column name appears verbatim in the constructed URL.
When the `ben` defect is fixed, add a test that asserts `"billed_entity_number"`
is used and `"ben"` is not.

---

### Rule 6 — Current/latest-year sparse-data safety

The most recent funding year is always partially loaded — USAC publishes data
on a rolling basis. Analytics pages and advisory signals that don't account for
this will surface incomplete years as anomalies.

**Why this matters:** If the current year has 40% of its expected rows, the
risk scores will be systematically lower than prior years. An analyst who does
not know this may dismiss valid signals or over-interpret them.

**Expected behavior:**
- Risk Insights must not present the current/partial year without a clear
  disclaimer or caveat
- Advisory signals must either exclude the current year or explicitly label it
  as potentially incomplete
- Cross-year trend charts must make the partial nature of the current year visible

**Regression test requirement:** Verify that the disclaimer/caveat is present
in the UI (manual semantic check); verify that the year-selector default behavior
does not silently pick the most recent year as if it were complete.

---

## The relationship between manual smoke checks and automated regression coverage

Manual smoke checks and automated regression tests serve different purposes and
are not interchangeable.

| | Smoke check | Regression test |
|---|---|---|
| When it runs | After a start/deploy, by a person | On every commit, automatically |
| What it catches | Deployment failures, wiring errors | Logic regressions in specific behavior |
| How fast it fails | Minutes (manual) | Seconds (automated) |
| What it covers | Surface-level "is it alive" | Specific behavioral contract |

**The progression:** A manual smoke check that consistently exercises the same
behavior, and where a failure would be non-obvious, is a candidate for automation.
When a corresponding automated test is written and becomes `active`, the smoke
check step may be relaxed (but not necessarily deleted — it still validates
wiring that automated tests cannot).

**Example:** The smoke check "reconciliation endpoint returns report" exercises
the Socrata HTTP call, the local DB query, and the markdown writer together. The
automated `ReconciliationReportWriterTests` cover the writer logic in isolation
but cannot cover the Socrata HTTP call. The smoke check covers the gap that
automation cannot close.

---

## What warrants a baseline snapshot

Not every output needs a committed snapshot. Candidates are outputs that:
- Are expensive to regenerate (require a full data load)
- Should be stable across commits when the data has not changed
- Would degrade silently if a logic change went unnoticed

**Candidates for baseline snapshots (to be implemented):**

| Baseline | Format | Tolerance |
|---|---|---|
| Total committed amount by year (from summary) | JSON | ±0.1% |
| Total approved disbursements by year (from summary) | JSON | ±0.1% |
| Risk level distribution by year (High/Moderate/Low counts) | JSON | Exact |
| Advisory signal counts by type for a stable year (e.g. 2022) | JSON | Exact |
| Reconciliation report for Disbursements 2022 | Markdown diff | Structural only |

Baselines are stored in `evidence/baselines/` (to be created at first capture).
A baseline update requires a commit with an explanation, not just a file overwrite.

## What does NOT warrant a baseline

- Raw import row counts from Socrata (source data changes legitimately)
- Timestamps, run IDs, `RunAtUtc` fields
- UI pixel layout or computed CSS values
- Intermediate builder log messages

---

## Not yet decided

- CI tooling for automated regression baseline comparison
- Whether to use xUnit snapshot assertions or shell-level diff
- Threshold for "stable year" (how long after a year's funding cycle closes
  before treating it as frozen)
