# Technical Debt — ERATE Workbench POC

_Last updated: 2026-03-18_

---

## TD-001 — HttpClient default timeout kills long imports

**What:** `UsacCsvClient` and `SocrataReconciliationService` are registered with `builder.Services.AddHttpClient<T>()` using the default `HttpClient.Timeout` of 100 seconds. A full FundingCommitments import takes ~3.5–4 hours; any single Socrata page fetch exceeding 100 seconds will abort the job.

**Why accepted:** Default registration was sufficient for testing; the timeout issue was only discovered when a 3.7-hour repair import failed on the final page fetch (CC-ERATE-000010).

**Risk:** Medium. Any import that runs long enough to encounter a slow Socrata response will silently fail on the last page. The job record shows `status=Failed` but all prior pages are committed — data is intact but the job appears failed with `RecordsProcessed=0`.

**Recommended resolution:**
```csharp
builder.Services.AddHttpClient<UsacCsvClient>(c =>
    c.Timeout = TimeSpan.FromMinutes(5));
```
One-line fix in `Program.cs`. The per-page retry logic (4 attempts, delays 3s/10s/30s) already handles transient errors; the timeout just needs to be long enough to not interrupt a slow-but-valid response.

---

## TD-002 — Import `RecordsProcessed` only written on job success

**What:** `FundingCommitmentImportService` and `DisbursementImportService` write `RecordsProcessed` to the `ImportJob` record only when the full import completes successfully. A killed or timed-out import leaves the job with `status=Running` (or eventually `Failed`) and `RecordsProcessed=0`, with no indication of how many rows were actually written.

**Why accepted:** Simple implementation; progress tracking adds complexity for a POC.

**Risk:** Medium. Makes import health monitoring misleading — a "failed" import with `processed=0` may have successfully written millions of rows. Confused two debugging sessions (CC-ERATE-000010D).

**Recommended resolution:** Write a per-batch progress counter to the job record, or at minimum write final row count even on failure in a `finally` block.

---

## TD-003 — No year-scoped import capability

**What:** Import services always page the entire Socrata dataset (~19.7M rows for FundingCommitments). The `?year=YYYY` parameter on import endpoints is silently ignored. There is no way to re-import a single funding year without running the full 3.5-hour import.

**Why accepted:** Socrata's CSV endpoint with `$filter=funding_year=X` is slow and unreliable for bulk exports. Full-dataset paging is the only reliable approach discovered.

**Risk:** Low for POC. Operational burden for targeted repairs — a one-year fix costs 3.5 hours.

**Recommended resolution:** Implement a parallel year-scoped import path using Socrata's `$filter` + `$order` parameters with appropriate timeout handling and result validation. Or implement a direct SQLite delete-by-year + year-filtered re-import via SoQL.

---

## TD-004 — Summary rebuild order is manual discipline

**What:** The Risk summary builder (`ApplicantYearRiskSummaryBuilder`) reads from `ApplicantYearCommitmentSummary` and `ApplicantYearDisbursementSummary`. These must be rebuilt before the Risk summary is rebuilt. There is no enforcement — calling `POST /dev/summary/risk` before rebuilding commitments produces silently stale results.

**Why accepted:** Documented in the watchlist and runbook; acceptable for POC with a single operator.

**Risk:** Low-Medium. Incorrect rebuild order produces misleading Raw→Summary variance without any error.

**Recommended resolution:** Add a `/dev/rebuild-all?year=YYYY` endpoint that executes all three builders in the correct order atomically.

---

## TD-005 — `[DIAG]` logging still active in FundingCommitmentCsvParser

**What:** `FundingCommitmentCsvParser` emits `LogWarning("[DIAG]...")` for CSV headers and the first 5 rows of every parsed file. Added for debugging during import development.

**Why accepted:** Useful during active debugging; removal was deferred.

**Risk:** Low. Noisy logs during imports (~5 extra warning lines per page = ~2,000 lines for a full import). Could mask real warnings.

**Recommended resolution:** Remove the `[DIAG]` log lines entirely, or change to `LogDebug` so they are suppressed in normal operation.

---

## TD-006 — Full outer join implemented in-memory for Risk summary

**What:** `ApplicantYearRiskSummaryBuilder` implements a full outer join of commitment and disbursement summaries in-memory (dictionary lookup) because SQLite does not natively support `FULL OUTER JOIN`.

**Why accepted:** Correct implementation; SQLite limitation is real and well-documented.

**Risk:** Low for current data sizes. At ~20K rows per year per summary, the in-memory join is fast. With multi-year full loads, the total rows in memory could reach ~120K — still manageable. The `GetTopCommitmentDisbursementGapsAsync` method in `RiskInsightsRepository` also loads all risk rows to memory before sorting by gap (documented in watchlist as CAND-PERF-001).

**Recommended resolution:** If migrating to Postgres, replace with a proper SQL `FULL OUTER JOIN`. For SQLite, consider a `UNION`-based approach to push more work to the DB engine.

---

## TD-007 — Analytics queries on raw tables (not summaries)

**What:** `AnalyticsRepository` queries the raw `FundingCommitments` and `Disbursements` tables directly for all analytics endpoints (top applicants, top providers, rural/urban breakdown, etc.). It does not use the pre-aggregated summary tables.

**Why accepted:** Analytics queries use different grouping dimensions (by provider, by rural/urban, by category) that don't map directly to the applicant-year summary tables. Raw queries are correct and covered by indexes.

**Risk:** Low-Medium. With ~2M+ raw FC rows and growing, complex analytics queries may become slow. The `cast(... as double)` pattern used for `SUM()` (SQLite TEXT affinity workaround) adds CPU overhead.

**Recommended resolution:** Pre-aggregate analytics-specific summary tables for the most common query patterns, or migrate to Postgres where decimal aggregation is native.

---

## TD-008 — No deletion detection against Socrata source

**What:** The idempotent upsert import pattern (ADR-004) can only add or update rows. If a Socrata row is deleted (corrections, retractions), the local copy persists indefinitely.

**Why accepted:** For a POC, USAC data corrections are rare and small in magnitude.

**Risk:** Low for POC. Could cause minor reconciliation variance over time as Socrata data drifts.

**Recommended resolution:** For production, implement a change-detection pass: fetch a complete list of source keys for a given year from Socrata and delete any local rows whose `RawSourceKey` is no longer present.

---

## TD-009 — No partial-year disclaimer in UI

**What:** The Risk Insights page displays advisory signals for FY2026 (a partial year) with the same presentation as complete years. Advisory signals for a partial year may overstate anomalies (e.g., "No Disbursement" is normal early in the year).

**Why accepted:** Known gap documented in watchlist. Low-effort UI label not yet implemented.

**Risk:** Low-Medium. Could mislead stakeholders at a demo if FY2026 is selected.

**Recommended resolution:** Add a visible "Partial year — signals preliminary" banner when the selected year is the most recent/partial year.

---

## TD-010 — No test for `.Reader?.HeaderRecord` null path

**What:** The CC-ERATE-000015 fix (`FundingCommitmentCsvParser.cs` line 25) added a null-conditional for `csv.Context.Reader?.HeaderRecord`. The null path (where `Reader` is null after `ReadHeader()`) is not exercised by any test.

**Why accepted:** The null path is unreachable in normal CsvHelper usage — `Reader` is only null before CSV reading begins, not after `ReadHeader()`. The fix is defensive only.

**Risk:** Negligible.

**Recommended resolution:** No action needed. The fix is purely defensive and the null path is not reachable in practice.

---
