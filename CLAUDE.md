## BOOT BLOCK
— # Last updated: 2026-03-23 | Boot Block: CC-ERATE-000038D

### PROJECT IDENTITY
- App: **ERATE Workbench** — E-Rate lifecycle analytics POC showing where execution breaks down, where advisors should focus, and how to reason about the E-Rate program operationally
- Repo: `steven-dracker/erate-workbench-poc`
- Stack: C# / .NET 8, ASP.NET Core Razor Pages + Minimal API, SQLite, Entity Framework Core, xUnit, Playwright (C#), GitHub Actions
- Dev env: **WSL-first** (canonical). Windows local is not the source of truth.
- Solution: `ErateWorkbench.sln`
- Projects: `ErateWorkbench.Api` | `ErateWorkbench.Domain` | `ErateWorkbench.Infrastructure` | `ErateWorkbench.Tests` | `ErateWorkbench.UITests`

### ARCHITECTURAL LAWS
These are immutable. Never violate without explicit architect approval.
- WSL is the canonical dev environment — no Windows-local assumptions
- Three-layer data model only: Raw → Summary → Risk (never skip layers)
- Idempotent imports via `RawSourceKey` upsert — never truncate/reload
- Feature branches only — never commit directly to `main`
- PR process is mandatory: branch → push → PR → CI green → merge → delete branch → sync main
- Prompt/task IDs (CC-ERATE-XXXXXX) are required on all Claude work for traceability (ADR-011)
- No external logging stack — use built-in `Microsoft.Extensions.Logging` only (ADR-020)
- No frontend framework — Razor Pages only, no React/Vue/Angular (ADR-001)

### CURRENT STATE (as of CC-ERATE-000038D)
- **Last completed:** CC-ERATE-000038D — Competitive Intelligence dashboard with consultant analytics, rankings, and detail views (PR #33 open, locally validated)
- **Branch:** feature/consultant-market-intelligence — PR #33 open (ready for merge)

- **Works (verified stable):**
  - Full CI pipeline: build → test → ui-smoke → security → secrets-scan → publish
  - Playwright UI smoke tests
  - Analytics page with IMemoryCache (24hr expiration)
  - Socrata import + reconciliation
  - All 20 ADRs implemented
  - Dependency vulnerability scanning + Dependabot + gitleaks
  - Artifact publishing (linux-x64 self-contained)
  - Logging baseline (SimpleConsole + ILogger<T>)
  - Partial-year advisory banner on Risk Insights
  - Form 471 ingestion via dataset 9s6i-myen (historical + incremental by funding year)
  - Filing Window Analytics dashboard at /FilingWindow — Chart.js visualizations for cumulative certification timing, requested vs committed by year, commitment rate, and application status breakdown
  - Desktop navigation reorganized into grouped dropdowns (Dashboard, Explore, Insights, Reference, Help)
  - Consultant datasets ETL end-to-end:
    - ConsultantApplications (x5px-esft)
    - ConsultantFrnStatuses (mihb-jfex)
  - Competitive Intelligence dashboard at /ConsultantIntelligence:
    - Consultant rankings (applications + FRNs)
    - Consultant detail pages (/Consultants/{epcId})
    - Year trends, state distribution, service type breakdown
    - Aggregation-safe analytics via ConsultantAnalyticsService
    - Chart.js visualizations

- **Caveats (data quality & scale):**
  - FY2020 contains a COVID window extension spike — annotated
  - Late-certification outliers excluded from timing charts
  - ServiceType null in Form 471 dataset
  - FY2026 partial — advisory banner shown
  - Socrata duplication handled via idempotent upsert
  - ConsultantName is display-only; EPC ID is canonical
  - E-Rate Central confirmed: EPC ID 16060891
  - Tel Logic not found — unresolved
  - mihb-jfex dataset scale confirmed:
    - ~556K records processed
    - 231 pages
    - ~42-minute full import locally

- **Consultant identity model (reference):**
  - Canonical key: `ConsultantEpcOrganizationId`
  - Display only: `ConsultantName`
  - RawSourceKey:
    - Applications: `{AppNumber}-{EpcId}`
    - FRN: `{AppNumber}-{FRN}`
  - Join safety: CONDITIONALLY SAFE
  - Fan-out risk: ~2 FRNs per application

### ACTIVE TASK
- Next prompt: CC-ERATE-000038E
- Status: Final feature — Competitive Intelligence refinements (filters, market share, insights)

### CURRENT MODE
- Demo Stabilization → Release QA (post-000038E)
- No new features beyond CC-ERATE-000038E
- Focus on validation, bug fixes, and demo readiness

### KNOWN DEBT (summary — see docs/context/technical-debt.md)
- TD-001: HttpClient timeout handling
- TD-002: Import observability weak
- TD-003: Partial incremental import support
- TD-004: Manual rebuild ordering
- TD-006: In-memory joins (SQLite limitation)
- TD-007: Analytics on raw tables
- TD-008: No deletion detection
- TD-011: Cache invalidation missing
- TD-012: Diagnostic logging remnants
- TD-013: xUnit analyzer warning
- TD-014: Playwright WSL dependency
- TD-015: Dependabot PR management
- TD-016: UI polish incomplete
- TD-017: Consultant fan-out aggregation risk
- TD-018: Consultant name normalization
- TD-019: EPC identification gaps (Tel Logic unresolved)
- TD-020: mihb-jfex confirmed large-scale dataset (~556K rows, ~42 min import)
- TD-021: GitHub Actions test runner hang (CI-only issue)

### WHAT TO IGNORE
- `erate-workbench/` subdirectory — legacy
- `reports/` output files
- UI test build artifacts
- Any Windows-local assumptions