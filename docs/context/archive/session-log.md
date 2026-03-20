# Session Log — ERATE Workbench POC

_Most recent entry first._

---

## 2026-03-19 — Multi-prompt session: CI pipeline, DevOps hardening, analytics performance, logging

### What was accomplished

#### CC-ERATE-000018 — Local validation script and initial CI foundation
- Created `scripts/dev-run.sh` with `--validate` mode (restore → build → test, no launch)
- Created `.github/workflows/ci.yml` with `build` and `test` jobs on every push/PR
- Targets `ErateWorkbench.Tests` only (not `UITests`) — critical decision to avoid running browser tests without a running app

#### CC-ERATE-000019 — Hardened dev script and deterministic startup
- Added `--start-for-tests` and default foreground run modes to `dev-run.sh`
- Added graceful stop: SIGTERM → 5-second wait → SIGKILL
- Added health poll loop (30-second timeout) in background start mode
- Added `GET /health → {"status":"ok"}` endpoint to `Program.cs` as readiness gate

#### CC-ERATE-000020 — Playwright UI smoke tests
- Created `tests/ErateWorkbench.UITests/` project with `SmokeTests.cs` (5 tests)
- Tests cover: `/health` 200, dashboard title/nav, 8 nav links, Ecosystem h1, History h1
- Created `scripts/ui-test.sh` with full (start+test+stop) and `--app-running` modes
- Extended CI with `ui-smoke` job: installs `libnss3/libnspr4/libasound2t64`, Playwright Chromium, starts app, polls `/health`, runs UITests, uploads failure artifacts

#### CC-ERATE-000021 — Security CI stage
- Fixed xunit 2.4.2 → 2.9.0 in `ErateWorkbench.Tests` to eliminate transitive CVEs (System.Net.Http 4.3.0, System.Text.RegularExpressions 4.3.0)
- Fixed `Assert.Equal([0,1,2,3,4,5,6,7], numbers)` ambiguity under xunit 2.9 → `Assert.Equal(new[] {...}, numbers)`
- Added `security` CI job: two-tier NuGet vuln scan (fail on direct, warn on transitive)
- Added `.github/dependabot.yml`: weekly NuGet + GitHub Actions, 5-PR limit per ecosystem

#### CC-ERATE-000022 — Fix History smoke test
- History page title is `"E-Rate Central — Historical Timeline — ERATE Workbench"` — does not contain bare `"History"`
- Fixed: `Assert.Contains("ERATE Workbench", title)` + heading locator `GetByRole(Heading, "E-Rate Central")`

#### CC-ERATE-000023 — Secrets scanning CI stage
- Added `secrets-scan` CI job using gitleaks CLI v8.30.0 (pinned), not the GitHub Action (requires license for private repos)
- `fetch-depth: 0` — scans full git history against 170+ default secret patterns
- Created `.gitleaks.toml` with two path exclusions: SQLite binary (`erate-workbench.db`) and legacy working-directory snapshot (`erate-workbench/`)
- Validated locally: 50+ commits, 0 leaks detected

#### CC-ERATE-000024 — DevOps documentation
- Rewrote `README.md`: prerequisites, dev-run.sh modes, ui-test.sh, Playwright WSL setup, import table, CI pipeline diagram, DevSecOps controls, Dependabot strategy, tech stack
- Created `docs/devops/pipeline.md`: per-job breakdown, failure interpretation table, extension path
- Created `docs/devops/local-workflow.md`: script reference, one-time Playwright setup, developer loop, manual split-terminal flow

#### CC-ERATE-000025 — Publish/artifact CI stage
- Added `publish` CI job: `dotnet publish --self-contained --runtime linux-x64 --configuration Release`
- Uploads artifact `erate-workbench-api` with 14-day retention, `if-no-files-found: error`
- `needs: [ui-smoke, security, secrets-scan]` — only runs after all validation gates pass
- **Key fix:** `--no-restore` intentionally omitted — RID-aware restore needed so `linux-x64` native assets resolve correctly (NETSDK1047 error if omitted restore with RID)
- Updated `docs/devops/pipeline.md` with publish job documentation

#### CC-ERATE-000026 — Analytics page performance (caching)
- Baseline measured: cold 17.5s, warm 2.1s (7 sequential full-table scans per request)
- Registered `IMemoryCache` in `Program.cs`
- Wrapped 6 expensive queries in `Analytics.cshtml.cs` with `GetOrCreateAsync`, 24-hour absolute expiry
- Import summary left uncached (live state, fast — small table)
- **Why not parallelized:** All repos share one scoped `AppDbContext`; SQLite EF Core forbids concurrent ops on the same context
- **Result:** warm latency 2.1s → ~10ms (~200× improvement); cold unchanged

#### CC-ERATE-000027 — Structured logging and observability baseline
- `Program.cs`: `ClearProviders()` + `AddSimpleConsole(TimestampFormat = "HH:mm:ss ", SingleLine = true)`
- `appsettings.json`: `ErateWorkbench→Information`, `Microsoft.EntityFrameworkCore→Warning`, `Microsoft.AspNetCore→Warning`, `System→Warning`
- `Analytics.cshtml.cs`: injected `ILogger<AnalyticsModel>` + `Stopwatch` — logs `"Analytics page rendered in {ms}ms (cache hit|miss)"` on every request
- `scripts/dev-run.sh`: foreground mode now `tee`s to `/tmp/erate-workbench-app.log`
- Created `docs/devops/logging.md`: format, levels, adjusting verbosity, grep patterns, file growth
- **Validated live:**
  ```
  21:30:35 info: ErateWorkbench.Api.Pages.AnalyticsModel[0] Analytics page rendered in 2613ms (cache miss — queries executed)
  21:30:35 info: ErateWorkbench.Api.Pages.AnalyticsModel[0] Analytics page rendered in 42ms (cache hit)
  ```

#### Dependabot merges (all CI-green, no manual review required)
- `actions/checkout@v4 → v6`
- `actions/setup-dotnet@v4 → v5`
- `actions/upload-artifact@v4 → v7`
- `Microsoft.Playwright 1.49.0 → 1.58.0`
- `xunit 2.9.0 → 2.9.3`
- `Microsoft.NET.Test.Sdk 17.8.0 → 18.3.0` (both test projects)

---

### Files created this session
- `scripts/dev-run.sh`
- `scripts/ui-test.sh`
- `.github/workflows/ci.yml`
- `.github/dependabot.yml`
- `.gitleaks.toml`
- `tests/ErateWorkbench.UITests/` (full project: `SmokeTests.cs`, `.csproj`, `playwright-artifacts/`)
- `docs/devops/pipeline.md`
- `docs/devops/local-workflow.md`
- `docs/devops/logging.md`

### Files modified this session
- `src/ErateWorkbench.Api/Program.cs` — `/health` endpoint, `AddMemoryCache()`, `ClearProviders()` + `AddSimpleConsole()`
- `src/ErateWorkbench.Api/Pages/Analytics.cshtml.cs` — `IMemoryCache` cache-aside, `ILogger` timing
- `src/ErateWorkbench.Api/appsettings.json` — structured log levels
- `src/ErateWorkbench.Api/appsettings.Development.json` — structured log levels
- `tests/ErateWorkbench.Tests/ErateWorkbench.Tests.csproj` — xunit 2.4.2 → 2.9.0, runner + SDK version bumps
- `tests/ErateWorkbench.Tests/ProgramWorkflowModelTests.cs` — `Assert.Equal([...])` ambiguity fix
- `README.md` — full rewrite

### Important outputs and test results
- **Tests:** 347/347 passing throughout entire session (no regressions introduced)
- **Build:** 0 errors, 1 pre-existing xUnit2013 warning (`ReconciliationTests.cs:394`)
- **gitleaks:** 0 leaks across 50+ commits
- **Analytics cache validation:** cold 17.5s (unchanged), warm 10ms (from 2.1s)
- **Log format validated live:** timestamp + level + category confirmed on every line
- **Dependabot:** All 6 dependency update PRs merged without incident

### Decisions escalated to architect
- None. All decisions within the DevOps, observability, and performance improvement scope of the session.

---

## 2026-03-18 — Multi-prompt session: data repair, page integration, code quality

### What was accomplished

#### CC-ERATE-000009 — FundingCommitments SoQL column name fix
- **Root cause:** `SourceDatasetManifest.FundingCommitments` referenced three non-existent SoQL columns: `applicant_entity_number`, `total_eligible_amount`, `committed_amount`. All caused HTTP 400 from Socrata, returning HTTP 500 on every FC reconciliation call.
- **Fix:** Corrected to `billed_entity_number`, `pre_discount_extended_eligible_line_item_costs`, `post_discount_extended_eligible_line_item_costs`
- **Tests fixed:** 3 stub-JSON tests in `ReconciliationTests.cs` updated to use correct column name keys
- **Regression guards added:** 3 new tests in `ReconciliationManifestTests` asserting correct column names
- **Test count:** 345 → 347, all passing
- **Runtime confirmed:** `POST /dev/reconcile/funding-commitments` returns HTTP 200 in ~46s

#### CC-ERATE-000010 — FY2021 FundingCommitments repair import
- **Before:** 125,296 raw rows, 16.9× source ratio, −9.6% Raw→Summary variance
- **Action:** Full idempotent re-import (job 46, 19:10–22:52 UTC, 3h41m)
- **Outcome:** Job record shows `status=Failed` due to `HttpClient.Timeout` on final page; all data written successfully (idempotent upsert commits per-batch)
- **After:** 171,977 raw rows, 12.3× ratio, ~$0 Raw→Summary variance

#### CC-ERATE-000010D — Root-cause investigation
- **Finding:** Socrata `avi8-svp9.csv` returns rows in internal record order, not by FundingYear. FY2021 rows concentrated at offset ~8–12M of 19.7M total. All prior imports were killed before reaching that range.
- **Finding:** `RecordsProcessed` is only written on job success — killed imports show `status=Running, processed=0` even while actively writing rows. This caused repair to appear as a no-op when it was not.
- **Finding:** Import service has no year parameter — only repair path is full re-import.
- **Documented:** `docs/quality/evidence/test-suite-audit.md` (gap G7: no import-completion detectability)

#### CC-ERATE-000011 — Quality log closure for FY2021
- Updated dashboard row from `~171,000 (est.)` to confirmed `171,977`
- Raw→Summary variance updated from `<2% (est.)` to `~$0 (<0.001%)`
- Watchlist item resolved/struck through
- CC-ERATE-000010 historical entry closed; CC-ERATE-000011 entry rewritten with confirmed post-repair numbers

#### CC-ERATE-000012 — Ecosystem page integration
- `wwwroot/erate_ecosystem.html` → served at `/ecosystem` via `MapGet` + `Results.File`
- Nav link added to `_Layout.cshtml`

#### CC-ERATE-000013 — History timeline page integration
- `wwwroot/erate_timeline.html` → served at `/history` via `MapGet` + `Results.File`
- Nav link added to `_Layout.cshtml`

#### CC-ERATE-000014 — Convert Ecosystem + History to Razor Pages
- **Problem:** Static file serving caused double-nav (standalone HTML nav + Bootstrap layout nav)
- **Fix:** Created `Pages/Ecosystem.cshtml` + `Ecosystem.cshtml.cs` and `Pages/History.cshtml` + `History.cshtml.cs`
- **Shell stripped:** `<!DOCTYPE html>`, `<html>`, `<head>`, `<body>`, standalone `<nav>`, `<div class="page-footer">`
- **CSS stripped:** universal `*` reset (conflicted with Bootstrap), `body`, `nav`, `.page`, `.page-footer` rules
- **CSS preserved:** all component rules; `@@media` escaped per Razor syntax
- **Nav updated:** `href="/ecosystem"` → `asp-page="/Ecosystem"` with `ActivePage` highlighting
- **MapGet routes removed** from `Program.cs`
- **Verified:** `/Ecosystem` and `/History` both return HTTP 200 with exactly 1× Bootstrap navbar, 0× `.nav-brand`
- **Original HTML files** retained in `wwwroot/` as archival artifacts

#### CC-ERATE-000015 — Fix nullable warning
- **File:** `FundingCommitmentCsvParser.cs` line 25
- **Warning:** CS8602 — `csv.Context.Reader` is `IReader?` in CsvHelper; accessing `.HeaderRecord` without null guard
- **Fix:** `.Reader.HeaderRecord` → `.Reader?.HeaderRecord` (one character)
- **Result:** 0 warnings, 0 errors, 347/347 tests pass

#### CC-ERATE-000016 — Reconciliation report artifacts
- Added saved reconciliation reports (JSON + Markdown) from the 2026-03-18 validation session to `src/ErateWorkbench.Api/reports/`

### Files created this session
- `src/ErateWorkbench.Api/Pages/Ecosystem.cshtml`
- `src/ErateWorkbench.Api/Pages/Ecosystem.cshtml.cs`
- `src/ErateWorkbench.Api/Pages/History.cshtml`
- `src/ErateWorkbench.Api/Pages/History.cshtml.cs`
- `src/ErateWorkbench.Api/wwwroot/erate_ecosystem.html` (archival)
- `src/ErateWorkbench.Api/wwwroot/erate_timeline.html` (archival)
- Multiple reconciliation report files in `src/ErateWorkbench.Api/reports/`

### Files modified this session
- `src/ErateWorkbench.Infrastructure/Reconciliation/SourceDatasetManifest.cs` — column name fix
- `src/ErateWorkbench.Infrastructure/Csv/FundingCommitmentCsvParser.cs` — nullable fix
- `src/ErateWorkbench.Api/Program.cs` — removed MapGet static routes
- `src/ErateWorkbench.Api/Pages/Shared/_Layout.cshtml` — nav link upgrades
- `tests/ErateWorkbench.Tests/ReconciliationTests.cs` — stub JSON + regression guards
- `docs/quality/evidence/yearly-quality-log.md` — multiple FY2021 updates
- `docs/quality/evidence/test-suite-audit.md` — test count + gap entries
- `docs/quality/test-inventory.md` — count + MANIFEST-005/006

### Important outputs
- **Test results:** 347/347 passing, 0 warnings after CC-ERATE-000015
- **FY2021 confirmed:** 171,977 raw rows, 12.3× source ratio, ~$0 Raw→Summary variance
- **Reconciliation confirmed working:** FC and Disbursements both return HTTP 200 with correct column names
- **Build:** Clean (0 errors, 0 warnings)

### Decisions escalated to architect
- None. All decisions within scope of POC quality and integration work.

---

## 2026-03-13 — Initial data load and analytics layer

### What was accomplished
- Initial FundingCommitments and Disbursements imports completed (partial — FY2021 gap not yet known)
- Analytics refactor: Risk Insights moved from raw-table queries to pre-aggregated summary layer
- Program Risk Insights page added
- Program Workflow page added with autosaving per-phase notes
- Application footer with build version added
- Funding commitment CSV mapping and parser tests added
- Branding integrated (logos, favicons)

### Key commits
- `8e7165a` — Add Program Risk Insights analytics page
- `16781bd` — Add Program Workflow page with autosaving per-phase notes
- `a7eedbf` — Add footer, funding commitment CSV mapping and parser tests
- `d5188c2` — Merge analytics summary refactor (PR #3)

---
