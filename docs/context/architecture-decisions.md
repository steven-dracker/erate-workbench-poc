# Architecture Decisions — ERATE Workbench POC

_Last updated: 2026-03-18_

---

## ADR-001 — SQLite as the primary database

**Decision:** Use SQLite (via Entity Framework Core) as the sole data store.

**Why chosen:**
- POC context; no need for a server process, connection pool management, or infrastructure
- Single-file DB is portable and trivially backed up
- EF Core migrations work identically against SQLite and Postgres, so upgrade path is clear
- WAL mode enabled at startup (migration `20260315000001`) to allow concurrent reads during writes

**Alternatives rejected:**
- Postgres: real production choice but adds deployment complexity for a POC
- SQL Server / Azure SQL: same concern; overkill for internal use

**Assumptions:**
- Dataset size stays manageable in SQLite (current: ~2M+ FundingCommitments rows — still fast)
- Concurrent write contention is rare (import jobs are the only heavy writer; two simultaneous instances caused 30-second lock contention in testing — documented)
- Migration to Postgres would require only connection string + minor EF config changes

---

## ADR-002 — .NET 8 / ASP.NET Core Razor Pages + Minimal API

**Decision:** Hybrid architecture: Razor Pages for UI, Minimal API (MapGet/MapPost) for all data endpoints.

**Why chosen:**
- Razor Pages provides strong typing and layout inheritance with minimal ceremony
- Minimal API is concise for a data-heavy POC with many GET/POST endpoints
- No client-side framework needed; Bootstrap 5 + vanilla JS is sufficient for the analytics/dashboard UI
- All page routing is convention-based (Pages directory → URL)

**Alternatives rejected:**
- Full MVC Controllers: more boilerplate for no benefit at this scale
- Blazor: heavier runtime, harder to embed standalone custom HTML pages
- React/Vue frontend: adds a build pipeline and significant complexity for a POC

**Assumptions:**
- Swagger UI (enabled via `app.MapControllers()` + Swashbuckle) is sufficient for API exploration
- Pages that are purely informational (Ecosystem, History) can be Razor Pages with inline CSS/content

---

## ADR-003 — Socrata Open Data API as the authoritative external source

**Decision:** Pull all USAC E-Rate data from the Socrata Open Data API (data.usac.org) using SoQL queries and CSV bulk downloads.

**Why chosen:**
- Only publicly available structured source for USAC program data
- Supports both individual record queries (SoQL) and bulk CSV export
- FundingCommitments (`avi8-svp9`) and Disbursements (`jpiu-tj8h`) are the two imported datasets
- GROUP BY SoQL queries are used for reconciliation without pulling all rows

**Known limitations (documented):**
- Socrata returns rows in internal record order, not by FundingYear — FY2021 rows were at offset ~8–12M of 19.7M total rows, causing incomplete imports when jobs were killed early (CC-ERATE-000010 root cause)
- HttpClient default timeout of 100 seconds will terminate any individual page fetch exceeding that limit — caused job 46 to record `status=Failed` despite completing all FY2021 data

**Alternatives rejected:**
- USAC FTP/bulk file downloads: not publicly available in machine-readable bulk form
- Scraping EPC portal: fragile, rate-limited, legally questionable

---

## ADR-004 — Idempotent upsert by RawSourceKey for all imports

**Decision:** All import services upsert (INSERT OR REPLACE) by a `RawSourceKey` unique constraint rather than truncate-and-reload.

**Why chosen:**
- Safe to re-run at any time without data loss
- Allows partial recovery from killed/failed imports — rows written before failure are preserved
- Allows incremental updates as Socrata data changes without full reload

**Key consequence:** Imports can only ADD or UPDATE rows, never DELETE. If a Socrata row disappears, the local copy persists. No mechanism currently exists for detecting deletions.

**Alternatives rejected:**
- Truncate-and-reload: simple but destroys partial progress; unsafe for multi-hour imports
- Change-detection by hash: more complex, provides deletion detection, but overkill for POC

---

## ADR-005 — Three-layer data model: Raw → Summary → Risk

**Decision:** Three distinct table tiers per domain:
1. **Raw** (`FundingCommitments`, `Disbursements`): one row per Socrata source row
2. **Summary** (`ApplicantYearCommitmentSummary`, `ApplicantYearDisbursementSummary`): one row per (applicant, year), aggregated from raw
3. **Risk** (`ApplicantYearRiskSummary`): one row per (applicant, year), joining commitment and disbursement summaries

**Why chosen:**
- Raw layer enables full reconciliation against Socrata source
- Summary layer enables fast analytics queries without scanning millions of raw rows
- Risk layer enables advisory signal computation without re-joining the full dataset each request
- Each layer can be rebuilt independently (year-scoped rebuilds via `POST /dev/summary/*?year=YYYY`)

**Rebuild order dependency:** Risk summary must be built AFTER commitment and disbursement summaries (recorded in watchlist). This is a manual discipline requirement — no automated enforcement.

**Alternatives rejected:**
- Single-layer views: would require scanning raw tables on every analytics request
- Materialized views: SQLite doesn't support them natively

---

## ADR-006 — Full-dataset imports (no year-scoping at source)

**Decision:** Import services always page through the entire Socrata dataset. The `?year=YYYY` query parameter on import endpoints is silently ignored.

**Why chosen:**
- Socrata CSV endpoint does not efficiently support year-filtered bulk export
- SoQL `WHERE` year filter on the CSV endpoint can cause Socrata to time out on large datasets
- Simplest correct implementation: page everything, rely on idempotent upsert

**Key consequence:** A full FundingCommitments import takes ~3.5–4 hours (19.7M source rows, 10K rows/page). Killing the process mid-import leaves partial data with no progress indicator (`RecordsProcessed` is only written on job completion).

**Alternatives rejected:**
- Year-parameterized imports: would require Socrata pagination by year, which is slow/unreliable
- Background worker with restart capability: warranted for production, deferred for POC

---

## ADR-007 — Reconciliation via Socrata SoQL GROUP BY queries

**Decision:** Reconciliation compares local data against Socrata using a single `GROUP BY funding_year` SoQL API call (not by re-fetching all rows).

**Why chosen:**
- Efficient: one HTTP request returns year-level aggregates for the full dataset
- Enables Source→Raw and Source→Summary comparisons without re-importing
- Three-layer report (Source / Raw / Summary) written as JSON + Markdown to `src/ErateWorkbench.Api/reports/`

**Key discovery (CC-ERATE-000009):** FundingCommitments SoQL column names are full snake_case from the CSV schema (`pre_discount_extended_eligible_line_item_costs`, not `total_eligible_amount`). Wrong columns caused HTTP 500 on all FC reconciliation calls until corrected. Three regression guard tests now protect these column names.

---

## ADR-008 — Standalone reference pages served as Razor Pages (not static files)

**Decision (evolved):** Ecosystem and History pages started as `Results.File()` static serving (CC-ERATE-000012/000013) and were converted to Razor Pages using shared `_Layout.cshtml` (CC-ERATE-000014).

**Why converted:**
- Static file serving produced a double nav (standalone HTML nav + app Bootstrap nav)
- Razor Pages strip the standalone document shell and inherit the shared layout, giving nav consistency
- CSS preserved inline; only universal reset, `body`, `nav`, and `.page`/`.page-footer` rules removed

**Original HTML files** (`wwwroot/erate_ecosystem.html`, `wwwroot/erate_timeline.html`) retained as archival source artifacts — not served by the app.

---

## ADR-009 — Quality evidence and validation lifecycle

**Decision:** Maintain a structured quality evidence system in `docs/quality/` with:
- `test-inventory.md`: canonical list of all tests by ID
- `evidence/yearly-quality-log.md`: per-year data validation state and cycle history
- `evidence/test-suite-audit.md`: test coverage gaps and defect log
- `runbooks/`: step-by-step validation procedures

**Why:**
- POC data loaded from external sources requires explicit validation records separate from test results
- Enables repeatable validation cycles and audit trail for stakeholder demos
- Distinguishes code-verified findings from runtime-required confirmations

---
