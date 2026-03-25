## BOOT BLOCK
— # Last updated: 2026-03-25 | Boot Block: CC-ERATE-000055


### PROJECT IDENTITY
- App: **ERATE Workbench** — E-Rate lifecycle analytics POC showing where execution breaks down, where advisors should focus, and how to reason about the E-Rate program operationally
- Repo: `steven-dracker/erate-workbench-poc`
- Stack: C# / .NET 8, ASP.NET Core Razor Pages + Minimal API, SQLite, Entity Framework Core, xUnit, Playwright (C#), GitHub Actions
- Dev env: **WSL-first** (canonical). Windows local is not the source of truth.
- Solution: `ErateWorkbench.sln`
- Projects: `ErateWorkbench.Api` | `ErateWorkbench.Domain` | `ErateWorkbench.Infrastructure` | `ErateWorkbench.Tests` | `ErateWorkbench.UITests`

## KEY ENGINEERING PRINCIPLES
- Never fabricate data  
- Prefer truthful UI over complete UI  
- Maintain aggregation-first design (no fan-out joins)  
- Keep fixes minimal and scoped  
- Prioritize demo clarity and correctness  

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

### CURRENT STATE (as of CC-ERATE-000055)

- **Last completed:** CC-ERATE-000054B — Safe targeted data refresh (operational, no code changes)  
- **Branch:** main  
- **Active task / next prompt:** CC-ERATE-000055 — Entity type badge color enhancement (School Search)  

- **Project State:**  
  The ERATE Workbench is **feature-complete and demo-ready**.  
  All major QA issues have been resolved.  
  Data has been refreshed to the extent safely supported by upstream sources.  

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

 ## CURRENT TASK — CC-ERATE-000055

Enhance School & Library Search UI with color-coded entity type badges.

### Requirements

Map entity types to distinct colors:

- School → medium green  
- Library → medium blue  
- SchoolDistrict → darker green  
- LibrarySystem → dark blue  
- Consortium → dark orange  
- NonInstructionalFacility → medium orange  
- Unknown → unchanged  

### Constraints

- Modify existing badge rendering only  
- No backend or data changes  
- Use Bootstrap classes or minimal CSS  
- Maintain readability and layout stability     

### Data Caveats (Quality, Coverage, and Scale)

#### Time-Based Characteristics
- FY2020 includes a COVID-era filing window extension spike (explicitly annotated)  
- Late-certification outliers are excluded from timing charts to preserve interpretability  
- FY2026 data is partial; advisory messaging is shown in the UI  

---

#### Data Completeness & Schema
- `ServiceType` is null for portions of the Form 471 dataset  
- External datasets (USAC) may change schema over time (e.g., Form 471 CSV header drift)  

---

#### Identity & Modeling
- `ConsultantName` is display-only; EPC ID is the canonical identity  
- E-Rate Central confirmed: EPC ID `16060891`  

---

#### Data Integrity & Processing
- Socrata duplication handled via idempotent upsert logic  
- Aggregation-first model prevents duplication from FRN fan-out 
- Some datasets unavailable or unstable upstream (USAC)
- Form 471 CSV schema drift (snake_case → Title Case) breaks parser
- Very large imports may hit SQLite limits near completion (minor tail loss possible)
- Data is **accurate where present**, not artificially completed 

---

#### Scale Characteristics
- Large datasets (e.g., consultant FRN dataset `mihb-jfex`) operate at:
  - ~556K records  
  - ~231 pages  
  - ~40+ minute full import locally  
- Dataset growth is ongoing and impacts ingestion strategy   

---

#### Data Completeness & Schema
- `ServiceType` is null for portions of the Form 471 dataset  
- External datasets (USAC) may change schema over time (e.g., Form 471 CSV header drift)  

---

#### Identity & Modeling
- `ConsultantName` is display-only; EPC ID is the canonical identity  
- E-Rate Central confirmed: EPC ID `16060891`  

---

#### Data Integrity & Processing
- Socrata duplication handled via idempotent upsert logic  
- Aggregation-first model prevents duplication from FRN fan-out  
- Some datasets unavailable or unstable upstream (USAC)
- Form 471 CSV schema drift (snake_case → Title Case) breaks parser
- Very large imports may hit SQLite limits near completion (minor tail loss possible)
- Data is **accurate where present**, not artificially completed

---

#### Scale Characteristics
- Large datasets (e.g., consultant FRN dataset `mihb-jfex`) operate at:
  - ~556K records  
  - ~231 pages  
  - ~40+ minute full import locally  
- Dataset growth is ongoing and impacts ingestion strategy
  

### Consultant Identity Model (Reference)

- **Canonical Key**
  - `ConsultantEpcOrganizationId` is the sole identity key used for grouping and aggregation  

- **Display Field**
  - `ConsultantName` is display-only and not used for grouping  

- **Raw Source Keys**
  - Applications: `{ApplicationNumber}-{ConsultantEpcOrganizationId}`  
  - FRNs: `{ApplicationNumber}-{FRN}`  

- **Aggregation Model**
  - Aggregation-first approach is enforced before combining datasets  
  - Distinct counts used for:
    - applications  
    - FRNs  

- **Fan-Out Characteristics**
  - FRN data introduces natural fan-out (~2 FRNs per application on average)  
  - Managed through aggregation rules to prevent duplication errors  

- **Join Safety**
  - Raw joins are intentionally avoided  
  - All cross-dataset logic is applied post-aggregation to ensure correctness 

### ACTIVE TASK
- Next prompt: CC-ERATE-000055 
- Status: Final feature — Entity type badge color enhancement (School Search)

### CURRENT MODE
- Release QA complete → Demo-ready state  
- Limited-scope enhancements only (UI polish, clarity, minor improvements)  
- Focus on stability, correctness, and demo presentation  
- No major feature expansion or architectural changes  

### KNOWN DEBT (summary — see docs/context/technical-debt.md)
- TD-001: HttpClient timeout handling (long-running imports may stall without explicit timeout control)  
- TD-002: Import observability limited (no real-time progress visibility beyond polling)  
- TD-003: Inconsistent incremental import support (some datasets require full reloads)  
- TD-004: Manual rebuild ordering required for derived tables  
- TD-006: SQLite limitations under large-scale aggregation/import (connection errors on long-running jobs)  
- TD-007: Analytics executed on raw tables (no materialized views)  
- TD-008: No deletion detection from upstream data sources  
- TD-011: Cache invalidation not tied to data refresh operations  
- TD-012: Residual diagnostic logging  
- TD-013: xUnit analyzer warning (test quality improvement opportunity)  
- TD-014: Playwright dependency setup required in WSL  
- TD-015: Dependabot PR management requires manual oversight  
- TD-016: Minor UI polish and visual consistency gaps  
- TD-017: Large-scale dataset ingestion performance (multi-million row imports stress current architecture)  
- TD-018: Consultant name normalization inconsistencies  
- TD-019: EPC identification gaps for certain firms  
- TD-020: External dataset growth (USAC datasets expanding significantly, impacting ingestion strategy)  
- TD-021: GitHub Actions test runner hang (CI-only, non-blocking)  
- TD-022: Form 471 schema drift (USAC CSV header change breaks parser)

### WHAT TO IGNORE
- `erate-workbench/` subdirectory — legacy
- `reports/` output files
- UI test build artifacts
- Any Windows-local assumptions