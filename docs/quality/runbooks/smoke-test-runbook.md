# Smoke Test Runbook

A durable manual validation checklist covering every active app surface.
Run this after any restart, deploy, dependency upgrade, or before a demo or formal
data validation cycle.

**Target completion time:** 20–30 minutes for a full run; 5 minutes for a quick
liveness-only pass (Sections 1–2 only).

Record outcomes in `evidence/yearly-quality-log.md` when running as part of a
formal quality cycle. For routine restarts, informal confirmation is sufficient.

---

## Run record (fill in before starting)

| Field | Value |
|---|---|
| **Date** | |
| **Validated data scope** | (e.g., "FY2020–FY2025 full load + FY2026 partial") |
| **App commit / version** | (run `git rev-parse --short HEAD`) |
| **Validator** | |
| **Run type** | `quick` (§1–2 only) / `standard` (§1–5) / `full` (all sections) |
| **Overall result** | pass / pass with caveats / fail |

---

## Prerequisites

- App running: `dotnet run --project src/ErateWorkbench.Api`
- Base URL: `http://localhost:5075`
- Database seeded with at least FY2020–FY2022 data
- Browser DevTools open (check for JS console errors on each page load)
- Representative years for year-filter tests:
  - **Early (mature):** 2020
  - **Middle (mature):** 2022
  - **Latest/current (partial):** highest year present in Risk Insights year dropdown

---

## Section 1 — Liveness and infrastructure

*Purpose: confirm the app started and core infrastructure is wired correctly.
These checks should always pass; failure here blocks everything else.*

### 1.1 — Home page

```
GET http://localhost:5075/
```

| Check | Expected | Result | Notes |
|---|---|---|---|
| HTTP status | 200 | | |
| Page renders without error banner | Yes | | |
| Navigation links present (Analytics, Risk Insights, etc.) | Yes | | |

---

### 1.2 — Swagger UI

```
GET http://localhost:5075/swagger
```

| Check | Expected | Result | Notes |
|---|---|---|---|
| HTTP status | 200 | | |
| Endpoint list renders | Yes | | |
| Import endpoints visible (`/import/*`) | Yes | | |
| Dev endpoints visible (`/dev/summary/*`, `/dev/reconcile/*`) | Yes | | |

---

## Section 2 — Core analytics pages

*Purpose: confirm data-backed pages render without errors and display content.
These are manual because chart rendering and data binding cannot be confirmed
by an HTTP status code alone.*

### 2.1 — Analytics page

```
GET http://localhost:5075/Analytics
```

| Check | Expected | Result | Notes |
|---|---|---|---|
| HTTP status | 200 | | |
| "Committed Funding by Year" bar chart renders | Yes — bars visible for each loaded year | | |
| Category 1 vs Category 2 chart renders | Yes | | |
| Top funded entities chart renders | Yes — at least 5 entities | | |
| Import summary section shows last import date | Yes | | |
| No JS console errors | Yes | | |

**Why manual:** Chart.js rendering requires visual confirmation. An HTTP 200 with broken JSON data would still return 200 but charts would be empty or misshapen.

---

### 2.2 — Risk Insights — all-years view

```
GET http://localhost:5075/RiskInsights
```
*(No year filter — default "All Years")*

| Check | Expected | Result | Notes |
|---|---|---|---|
| HTTP status | 200 | | |
| Year filter dropdown present with "All Years" default | Yes | | |
| Funding waterfall section renders (Requested → Committed → Approved) | Yes — three bars visible | | |
| Top Risk Applicants table populated | Yes — at least 10 rows | | |
| Advisory Signals table populated | Yes — rows present | | |
| All four signal types represented in data (No Commitment, No Disbursement, High Reduction, Low Utilization) | At least 2 types visible | | |
| No JS console errors | Yes | | |

---

### 2.3 — Risk Insights — early year (2020)

```
GET http://localhost:5075/RiskInsights?year=2020
```

| Check | Expected | Result | Notes |
|---|---|---|---|
| FY 2020 badge visible in filter summary | Yes | | |
| Waterfall bars reflect FY2020 amounts | Yes — values change from all-years view | | |
| Advisory signals show `FY @sig.FundingYear = 2020` | Yes | | |
| Risk score distribution plausible for a mature year | High/Moderate/Low all represented | | |
| Disclaimer about datasets not reconciling 1:1 visible | Yes (below waterfall) | | |

---

### 2.4 — Risk Insights — middle year (2022)

```
GET http://localhost:5075/RiskInsights?year=2022
```

| Check | Expected | Result | Notes |
|---|---|---|---|
| FY 2022 badge visible | Yes | | |
| Waterfall values differ from FY2020 view | Yes | | |
| Advisory signals present | Yes | | |
| No JS console errors | Yes | | |

*FY2022 is the reference year with a validated reconciliation pass (see evidence log).
If advisory signal counts look dramatically different from prior runs, investigate.*

---

### 2.5 — Risk Insights — latest/current year (partial)

```
GET http://localhost:5075/RiskInsights?year=<CURRENT_YEAR>
```
*(Use the highest year present in the year dropdown)*

| Check | Expected | Result | Notes |
|---|---|---|---|
| Page renders without error | Yes | | |
| Waterfall amounts are lower than mature years | Expected — year is partially loaded | | |
| Advisory signals present but count may be lower | Expected | | |
| **Semantic check:** Signals shown without "partial year" label | Caution — results may overstate anomalies for partial year | | |
| No JS console errors | Yes | | |

**⚠ Partial-year caution:** The current/in-progress funding year will always have
fewer rows than mature years. Advisory signals for this year may flag entities that
simply have not had their invoices processed yet. Do not present current-year risk
signals to stakeholders without explicit caveats. See Section 5 for the semantic
review checklist.

---

### 2.6 — Program Workflow page

```
GET http://localhost:5075/ProgramWorkflow
```

| Check | Expected | Result | Notes |
|---|---|---|---|
| HTTP status | 200 | | |
| All workflow phases render | Yes — numbered phase cards visible | | |
| Phase steps visible within each card | Yes | | |
| "Audit Risk" phase card has danger styling | Yes — red accent | | |
| Notes column visible alongside phases | Yes | | |
| No JS console errors | Yes | | |

*Program Workflow is static content — it does not depend on imported data. A failure here
indicates a server-side rendering error or template problem, not a data issue.*

---

### 2.7 — Advisor Playbook page

```
GET http://localhost:5075/AdvisorPlaybook
```

| Check | Expected | Result | Notes |
|---|---|---|---|
| HTTP status | 200 | | |
| "Program Year" summary section renders | Yes | | |
| Risk section renders | Yes | | |
| Notes section renders | Yes | | |
| No JS console errors | Yes | | |

---

### 2.8 — Search page

```
GET http://localhost:5075/Search
```

| Check | Expected | Result | Notes |
|---|---|---|---|
| HTTP status | 200 | | |
| Search input present | Yes | | |
| Submit with empty query returns page without error | Yes | | |

---

## Section 3 — API endpoint health

*Purpose: confirm the operational endpoints respond correctly. These checks exercise the
import and summary pipeline without triggering a full re-import.*

### 3.1 — Disbursements reconciliation (reference year)

```
POST http://localhost:5075/dev/reconcile/disbursements?year=2022
```
*(Run via Swagger UI or curl)*

| Check | Expected | Result | Notes |
|---|---|---|---|
| HTTP status | 200 | | |
| Response body has `datasetName = "Disbursements"` | Yes | | |
| `fundingYearScope = 2022` in response | Yes | | |
| `sourceTotalRowCount` matches known Socrata count (~274,905 for 2022) | Within 1% | | |
| `localRawTotalRowCount` matches local DB count | Exact match | | |
| Report file written to `reports/` | Yes | | |

**Allow up to 2 minutes** — this makes outbound HTTP calls to Socrata.

> **Automated coverage (URL structure):** MANIFEST-003 and MANIFEST-004 in
> `ReconciliationManifestTests` verify that reconciliation URLs use simple-filter
> syntax (no `$where=`) and do not apply a `funding_year=` filter — reconciliation
> always fetches all years from Socrata at once. This check remains manual because
> it exercises the live Socrata HTTP call and the full pipeline.

---

### 3.2 — Summary rebuild (safe idempotent check)

```
POST http://localhost:5075/dev/summary/risk?year=2022
```

| Check | Expected | Result | Notes |
|---|---|---|---|
| HTTP status | 200 | | |
| Response has `riskSummaryRowsWritten > 0` | Yes — should be ~20,000 | | |
| `matchedRows + commitmentOnlyRows + disbursementOnlyRows = riskSummaryRowsWritten` | Yes | | |

---

### 3.3 — Idempotent re-import

```
POST http://localhost:5075/import/disbursements
```

| Check | Expected | Result | Notes |
|---|---|---|---|
| HTTP status | 200 | | |
| `recordsProcessed > 0` | Yes | | |
| `status = "Succeeded"` | Yes | | |

**Note:** This re-imports existing data and is safe to run repeatedly. It does not
change the data if Socrata has not changed. Allow up to 10 minutes.

**⚠ Import endpoints are not year-scoped.** The `?year=YYYY` parameter is silently
ignored by the import service — any value passed is discarded. Imports always fetch
the complete USAC dataset via `$limit/$offset` paging and upsert all rows idempotently
by `RawSourceKey`. Year-scoped processing begins at Step 2 (summary rebuild, `?year=YYYY`).

> **Automated coverage (URL construction):** IMP-URL-001 through IMP-URL-004 in
> `FundingCommitmentImportServiceTests` and `DisbursementImportServiceTests` verify that
> import page URLs contain `$limit=` and `$offset=` paging parameters and do not contain
> `funding_year=`. This check remains manual because it exercises the live HTTP endpoint
> and end-to-end record processing.

---

## Section 4 — Year-filter consistency checks

*Purpose: confirm the year filter propagates correctly across pages. Manual because
it requires cross-referencing numbers between the year-filtered page and known data counts.*

### 4.1 — Risk Insights year filter changes data

Select three different years in sequence on the Risk Insights page and confirm
the waterfall amounts change each time.

| Year | Requested total changes from default | Advisory signal count changes | Result |
|---|---|---|---|
| All Years | (baseline) | (baseline) | |
| 2020 | Yes | Yes | |
| 2022 | Yes | Yes | |
| Current year | Yes — lower than 2022 | Yes — may be lower | |

---

### 4.2 — Analytics page reflects loaded years

On the Analytics page, confirm the "Committed Funding by Year" bar chart includes
bars for every year in the target data scope.

| Check | Expected | Result | Notes |
|---|---|---|---|
| 2020 bar present | Yes | | |
| 2022 bar present | Yes | | |
| Current year bar present (shorter than mature years) | Yes — partial year expected | | |
| No unexpected gaps (missing year bars) | Yes | | |

---

## Section 5 — Semantic honesty review

*Purpose: confirm the application presents data honestly and does not overstate
conclusions. This section is entirely manual because it requires human judgment
about meaning, framing, and the risk of misinterpretation.*

**Run this section before any stakeholder demo or external sharing.**

### 5.1 — Waterfall disclaimer

On Risk Insights (any year view):

| Check | Expected | Result | Notes |
|---|---|---|---|
| Disclaimer text visible below waterfall | Yes — text explaining datasets do not reconcile 1:1 | | |
| Disclaimer mentions "invoicing timing" and "multi-year payment cycles" | Yes | | |
| Disclaimer visible without scrolling on a standard display | Preferred | | |

---

### 5.2 — Advisory signal labels

Review the Advisory Signals table on Risk Insights for a mature year (e.g., 2022):

| Signal type | Label meaning | Check: label accurately describes condition | Result | Notes |
|---|---|---|---|---|
| No Commitment | Disbursements exist but no matching commitment record | Does not imply fraud — possible timing, dataset lag, or data gap | | |
| No Disbursement | Commitment exists but no matching disbursement record | Does not imply non-delivery — may be within payment window | | |
| High Reduction | >50% of eligible amount was not committed after PIA review | Reduction is expected; >50% is a flag, not a finding | | |
| Low Utilization | <50% of committed amount invoiced/approved | May reflect multi-year disbursement cycles | | |

**Fail condition:** If any label could be reasonably misread as an accusation of
misuse or fraud by a reader unfamiliar with E-Rate program dynamics, that label
needs a tooltip or inline caveat added before external sharing.

---

### 5.3 — Partial/current year presentation

View Risk Insights for the current/latest year:

| Check | Expected | Result | Notes |
|---|---|---|---|
| Page renders and shows data | Yes | | |
| No explicit "partial year" warning visible to user | Current state — this is a known gap | | |
| Advisory signals for current year could mislead a non-expert | **Caution** — document before sharing | | |
| Counts are visibly lower than mature years for same metrics | Yes | | |

**Known gap:** There is no automatic partial-year disclaimer on the Risk Insights page
for the current year. Before presenting current-year risk signals to stakeholders,
verbally caveat that the year is partially loaded and signals should be treated as
preliminary indicators only.

---

### 5.4 — Program Workflow — content accuracy

Review the Program Workflow phases:

| Check | Expected | Result | Notes |
|---|---|---|---|
| Phase timing labels reflect the current program year calendar | Yes | | |
| No stale dates or program-year-specific references that have expired | Yes | | |
| Audit Risk phase is clearly differentiated (red accent) | Yes | | |

---

## Section 6 — Evidence record

After completing a full or standard run, append to `evidence/yearly-quality-log.md`:

```
## Smoke Test — YYYY-MM-DD

**Validator:** [name]
**Run type:** full / standard / quick
**Data scope:** [e.g., FY2020–FY2025 full + FY2026 partial]
**Commit:** [git SHA]

| Section | Result | Notes |
|---|---|---|
| §1 Liveness | pass / fail | |
| §2 Analytics pages | pass / caveat / fail | |
| §3 API endpoint health | pass / caveat / fail | |
| §4 Year-filter consistency | pass / caveat / fail | |
| §5 Semantic honesty | pass / caveat / fail | |

Overall: pass / pass with caveats / fail
```

---

## Known caveats and timing expectations

| Check | Timing expectation | Notes |
|---|---|---|
| Reconciliation endpoint (§3.1) | Up to 2 minutes | Socrata API call; failure after 5 min = investigate |
| Summary rebuild (§3.2) | 30–90 seconds | In-memory group; slower with large years |
| Idempotent re-import (§3.3) | Up to 10 minutes | Idempotent — safe to cancel if needed; fetches full dataset regardless of any `?year=` parameter |
| Socrata unavailability | N/A | If Socrata is down, §3.1 will timeout; mark as "skipped (Socrata unavailable)" not "fail" |
