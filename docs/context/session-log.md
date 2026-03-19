# Session Log — ERATE Workbench POC

_Most recent entry first._

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
