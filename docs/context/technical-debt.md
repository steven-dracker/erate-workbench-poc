# Technical Debt — ERATE Workbench POC

_Last updated: 2026-03-25

---

## Consultant ETL — Implemented (CC-ERATE-000038B)

Raw-layer ingestion for the two USAC consultant datasets is complete.

**Tables created:**
- `ConsultantApplications` — x5px-esft, grain: one row per consultant per Form 471 application
- `ConsultantFrnStatuses` — mihb-jfex, grain: one row per FRN per consultant-assisted application

**Identity model (canonical):**
- `ConsultantEpcOrganizationId` is the grouping key for consultant identity — not `ConsultantName`
- `ConsultantName` is preserved as-is (display only); casing is inconsistent across records
- `ApplicationNumber` is the cross-dataset bridge key between the two tables
- `RawSourceKey` per table: `"{ApplicationNumber}-{ConsultantEpcOrganizationId}"` (ConsultantApplications),
  `"{ApplicationNumber}-{FundingRequestNumber}"` (ConsultantFrnStatuses)

**Datasets remain unjoined at the ETL layer** — tables are loaded independently.
Joining/aggregation is deferred to CC-ERATE-000038C (Competitive Intelligence Dashboard).

**Deferred to CC-ERATE-000038C:**
- Name normalization across consultant name variants — use EPC ID for grouping instead
- Join validation (application_number cardinality, multi-consultant applications)
- E-Rate Central EPC Organization ID lookup and firm identification
- Any analytics, rankings, or dashboard queries

**Import endpoints:**
- `POST /import/consultants/applications` — x5px-esft full ingestion
- `POST /import/consultants/frn-status` — mihb-jfex full ingestion

---

## Import Resilience — Implemented (CC-ERATE-000039)

The following behaviors were hardened in CC-ERATE-000039 and are now active:

**Retry / backoff (`UsacCsvClient.DownloadStreamAsync`)**
- Retries on transient failures: HTTP 5xx, HTTP 429, `SocketException`, `IOException`, network-level `HttpRequestException`
- 3 retries maximum with exponential backoff: 1s → 2s → 4s
- Non-transient HTTP errors (4xx other than 429) propagate immediately without retry
- HTTP 5xx responses are logged as "upstream temporarily unavailable" before retrying

**Pre-flight availability check (`UsacCsvClient.CheckAvailabilityAsync`)**
- All importers perform a lightweight `$limit=1` probe before starting a full import
- Probe retries once (1s delay) on transient failure, then reports unavailable
- If the probe fails, the import job is marked `Failed` immediately with error message:
  `"USAC data source is unavailable. Import aborted."`
- The job record IS written to the DB before the probe, so there is always a trace

**Expected failure mode during upstream outage (e.g. Socrata maintenance)**
- Probe fires → gets 503 → retries once → still 503 → returns `false`
- Import job written to DB as `Failed` within ~1–2 seconds
- Clear error message in job record and in logs
- No pages are fetched; no data is modified

**Remaining gap:** Paged importers also have their own per-page retry (4 attempts: 3s/10s/30s delays).
The pre-flight probe prevents the paging loop from starting, but if the upstream becomes unavailable
MID-import (after the probe passes), the per-page retry still fires before ultimately failing.
This is expected behavior — the probe only guards against outages at import start time.

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

## TD-010 — No test for `.Reader?.HeaderRecord` null path

**What:** The CC-ERATE-000015 fix (`FundingCommitmentCsvParser.cs` line 25) added a null-conditional for `csv.Context.Reader?.HeaderRecord`. The null path (where `Reader` is null after `ReadHeader()`) is not exercised by any test.

**Why accepted:** The null path is unreachable in normal CsvHelper usage — `Reader` is only null before CSV reading begins, not after `ReadHeader()`. The fix is defensive only.

**Risk:** Negligible.

**Recommended resolution:** No action needed. The fix is purely defensive and the null path is not reachable in practice.

---

## TD-011 — Analytics cache has no invalidation on import

**What:** `AnalyticsModel` caches all 6 query results in `IMemoryCache` with a 24-hour absolute expiry (CC-ERATE-000026). If an import runs and loads new data, the Analytics page continues serving stale cached data until the cache expires or the app restarts.

**Why accepted:** For a demo tool with infrequent imports, 24-hour staleness is acceptable. Cache invalidation adds coupling between import services and the page model.

**Risk:** Low for POC. If a stakeholder runs an import immediately before a demo, the Analytics charts will show pre-import data until restart.

**Recommended resolution:** Add a `POST /admin/cache/clear` endpoint that evicts all analytics cache keys, or publish a domain event from import services that triggers cache eviction.

---

## TD-012 — `[DIAG]` log lines still active in FundingCommitmentCsvParser

**What:** `FundingCommitmentCsvParser` emits `LogWarning("[DIAG]...")` for CSV headers and the first 5 rows of every parsed page. Added for debugging; now more visible because CC-ERATE-000027 improved the logging baseline.

**Why accepted:** Useful during active debugging; removal was deferred.

**Risk:** Low. Noisy logs during imports (~5 extra warning lines per page, ~2,000 lines for a full import). Could mask real warnings in filtered log output.

**Recommended resolution:** Remove the `[DIAG]` lines or demote to `LogDebug`. One is suppressed by default with `ErateWorkbench: Information` config.

---

## TD-013 — xUnit2013 analyzer warning in ReconciliationTests.cs

**What:** `ReconciliationTests.cs` line 394 uses `Assert.Equal(expected, collection.Count)` to check collection size. xUnit 2.9 recommends `Assert.Single()` or `Assert.Empty()` for single-element assertions. The warning (`xUnit2013`) is suppressed via `NoWarn` but surfaces in build output.

**Why accepted:** Not a functional issue; test is correct. Fix is cosmetic.

**Risk:** Negligible.

**Recommended resolution:** Change to `Assert.Single(collection)` to eliminate the warning. One-line change.

---

## TD-014 — Playwright browser not installed in local WSL environment

**What:** The Playwright Chromium browser is not installed in the WSL dev environment. Running `dotnet test ErateWorkbench.sln` always fails the 5 UI smoke tests locally with "Executable doesn't exist at …/chrome-headless-shell." The `pwsh playwright.ps1 install` step has not been run.

**Why accepted:** UI smoke tests run successfully in CI (GitHub Actions) where browsers are installed automatically. Local dev relies on CI for UI test validation.

**Risk:** Low. UI smoke tests always fail locally — developers must either run `playwright.ps1 install` or exclude `ErateWorkbench.UITests` from local test runs. Could mask genuine UI regressions if CI is not consulted before merge.

**Recommended resolution:** Document the one-time `pwsh playwright.ps1 install` setup step in dev onboarding docs. Alternatively, add a `scripts/install-playwright.sh` wrapper for WSL.

---

## TD-015 — Dependabot PR queue management

**What:** Dependabot generates automated dependency update PRs on a regular cadence. Without a management process these accumulate, become stale, conflict with each other, or get merged without adequate review of downstream impact.

**Why accepted:** Low PR volume for a POC. Merge policy is documented in the DEPENDABOT GOVERNANCE section of CLAUDE.md.

**Risk:** Low. Stale PRs cause merge conflicts; unreviewed major upgrades could introduce breaking changes.

**Recommended resolution:** Review and process Dependabot PRs on a regular cadence (weekly or per-session). Follow DEPENDABOT GOVERNANCE rules: GitHub Actions updates are safe to auto-merge after green CI; major runtime/data-layer upgrades require deliberate engineering review.

---

## TD-016 — UI/theme polish behind engineering maturity

**What:** The application's visual design (typography, spacing, color consistency, mobile responsiveness) lags behind the engineering quality. Pages use a mix of inline styles and Bootstrap utilities applied inconsistently across views.

**Why accepted:** POC focus has been on data correctness, pipeline stability, and feature completeness. Visual polish is explicitly deferred.

**Risk:** Low for POC. Could affect stakeholder impression during demos. Not a functional concern.

**Recommended resolution:** After core analytics and risk features stabilize, do a dedicated UI/UX pass to normalize styling, extract shared CSS classes, and improve mobile responsiveness.

---

## Release QA Technical Debt (Intentional Deferrals)

During Release QA Mode, several items were explicitly deferred to maintain delivery velocity and preserve correctness.

---

### CI/CD Pipeline Instability

Issue:
- GitHub Actions workflows (unit tests + UI tests) intermittently hang after successful execution

Current Approach:
- Manual QA used as source of truth
- Workflows manually cancelled when hung

Rationale:
- Avoid blocking feature validation during critical QA phase
- Ensure real functionality is validated over pipeline reliability

Future Resolution:
- Investigate test runner completion conditions
- Add explicit timeouts or job termination guards
- Separate long-running UI tests from blocking pipelines

---

### Incomplete Entity-Level Discount Data

Issue:
- Discount rates available for ~15% of entities only
- Majority of individual schools lack data in USAC datasets

Current Approach:
- Removed discount columns from School Search (CC-ERATE-000053B)

Rationale:
- Avoid misleading users with partial or biased data
- Maintain trust in displayed analytics

Future Resolution:
- Identify dataset with per-entity, per-year discount rates
- Likely requires funding-year keyed enrichment table

---

### Manual Data Refresh Process

Issue:
- Data ingestion not automated
- Requires manual execution (CC-ERATE-000054)

Current Approach:
- Manual refresh prior to demo or analysis

Rationale:
- Keep ETL simple during POC phase
- Avoid premature automation complexity

Future Resolution:
- Add scheduled ingestion (cron / pipeline trigger)
- Track dataset freshness metadata

---

### No Filter Context Propagation to Detail Views

Issue:
- Consultant detail page does not inherit filters from ranking page

Current Approach:
- Explicitly documented as "full-history view"

Rationale:
- Prevent ambiguous partial data views
- Maintain consistent interpretation of detailed analytics

Future Resolution:
- Add optional filter context propagation
- Clearly indicate filtered vs full-history mode

---

### Manual Version Management

Issue:
- Version numbers manually updated in project file

Current Approach:
- Manual bump (0.1.0 → 0.3.0)

Rationale:
- Keep versioning simple during rapid iteration

Future Resolution:
- Automate versioning via CI (commit-based or tag-based)

---

## Post-Demo Expansion Phase Technical Debt (CC-ERATE-000056)

Following completion of the demo-ready POC phase, the system has reached scale and capability limits that require architectural evolution. The following items define the next phase of work.

---

### TD-023 — Test Suite Hygiene and Reliability

**What:**
The test suite (~400+ tests) includes:
- tautological tests (asserting mocked behavior rather than system behavior)
- redundant coverage across similar scenarios
- brittle tests tied to implementation details

A failing idempotency test (`ConsultantFrnStatusImport_IsIdempotent_OnRerun`) indicates potential mismatch between test assumptions and actual ETL behavior.

**Why accepted:**
Rapid AI-assisted development prioritized coverage over test quality.

**Risk:**
Medium.
- False positives reduce trust in test suite
- CI instability worsened by unnecessary test volume
- Slows development iteration

**Recommended resolution:**
- Execute CC-ERATE-TEST-AUDIT-001 cleanup plan:
  - Remove low-value / tautological tests
  - Consolidate overlapping tests
  - Focus on:
    - ETL correctness
    - aggregation integrity
    - API contract validation
- Treat remaining tests as **high-signal validation suite**, not coverage metric

---

### TD-024 — SQLite Scalability Limits Reached

**What:**
Recent imports show:
- Funding commitments: ~1.6M rows
- Disbursements: ~3.1M rows
- Import durations: 45–60+ minutes
- Observed SQLite connection failures during long-running jobs

**Why accepted:**
SQLite was appropriate for early-stage POC simplicity.

**Risk:**
High (now active constraint).
- Import instability under load
- Limited concurrency
- Inefficient large aggregations
- Blocking further dataset growth

**Recommended resolution:**
- Migrate to PostgreSQL (planned in CC-ERATE-000056)
- Replace:
  - in-memory joins
  - text-based numeric casting
- Enable:
  - proper indexing
  - native numeric aggregation
  - concurrent workloads

---

### TD-025 — Form 471 Schema Drift (Breaking Change)

**What:**
USAC changed CSV headers from `snake_case` → `Title Case`, breaking:
- `Form471CsvRow` mapping
- ingestion pipeline for Form 471

**Why accepted:**
External dependency; schema not controlled locally.

**Risk:**
Medium-High.
- Blocks future Form 471 refreshes
- Breaks incremental ingestion strategy

**Recommended resolution:**
- Implement header normalization layer:
  - case-insensitive mapping
  - alias mapping (snake_case ↔ Title Case)
- Add schema validation logging on import start

---

### TD-026 — CI Pipeline Test Execution Hang

**What:**
GitHub Actions:
- tests execute successfully
- job never completes
- downstream pipeline stages blocked

Local execution:
- clean and fast

**Why accepted:**
Manual QA used during Release QA phase.

**Risk:**
Medium.
- CI cannot be trusted for release gating
- Requires manual intervention

**Recommended resolution:**
- Investigate testhost lifecycle / async completion issues
- Add:
  - explicit test timeouts
  - diagnostic logging at test runner boundary
- Potentially split:
  - unit tests
  - UI tests
  into separate workflows

---

### TD-027 — Data Refresh Process is Manual and Opaque

**What:**
Data refresh (CC-ERATE-000054):
- manually triggered
- monitored via logs / polling
- partial failures require manual interpretation

**Why accepted:**
POC phase prioritized correctness over automation.

**Risk:**
Medium.
- Operational friction
- Hard to validate completeness
- Not repeatable or observable

**Recommended resolution:**
- Introduce:
  - structured job progress tracking
  - per-dataset status reporting
  - optional scheduled refresh
- Add import summary endpoint or dashboard

---

### TD-028 — Lack of Security Scanning (OWASP / SAST / Dependency Analysis)

**What:**
Current pipeline includes:
- dependency scanning (Dependabot)
- secrets scanning (gitleaks)

But lacks:
- OWASP dependency vulnerability scanning (full)
- static application security testing (SAST)

**Why accepted:**
Out of scope for initial POC.

**Risk:**
Medium.
- Unknown vulnerabilities in dependencies or code paths

**Recommended resolution:**
- Add OWASP Dependency Check or equivalent to CI
- Evaluate lightweight SAST options compatible with .NET
- Integrate into non-blocking pipeline stage initially

---

### TD-029 — Platform Portability and Environment Standardization

**What:**
System currently runs:
- locally (WSL)
- partially in CI
- not yet standardized across environments

New MCP infrastructure (dude-mcp-01) introduces:
- dedicated Postgres
- remote execution environment
- multi-node future architecture

**Why accepted:**
Infrastructure evolved after application maturity.

**Risk:**
Medium.
- Environment drift
- inconsistent behavior across nodes

**Recommended resolution:**
- Standardize runtime environment on MCP node
- Define:
  - canonical deployment model
  - environment configuration strategy
- Capture as part of CC-ERATE-000056

---

### TD-030 — Architecture Transition Required (POC → Platform)

**What:**
The system has evolved from:
- single-node SQLite POC  
to:
- multi-dataset analytics platform candidate

**Why accepted:**
Natural evolution of successful POC.

**Risk:**
Strategic.
- current architecture will limit further growth

**Recommended resolution:**
- Transition to:
  - Postgres-backed data layer
  - MCP-based execution model
- Formalize architecture for reuse in future projects

---
