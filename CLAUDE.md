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

- **Next Phase:** CC-ERATE-000056 — MCP Hub integration and platform expansion
- **Infrastructure:** dude-mcp-01 provisioned (Postgres + Claude Code + MCP server)

**Project State:**  
The ERATE Workbench is **feature-complete and demo-ready**.  
Core functionality is stable and validated through manual QA.  
Data has been refreshed to the extent safely supported by upstream sources.  

⚠️ Known follow-up work remains:
- Test suite cleanup (CC-ERATE-TEST-AUDIT-001 — audit complete, cleanup pending)
- One known failing test on main (`ConsultantFrnStatusImport_IsIdempotent_OnRerun`)
- CI reliability issues (GitHub Actions hang behavior)

🚀 The project is now transitioning into a new phase:
- CC-ERATE-000055 — final UI enhancement (next task)
- CC-ERATE-000056 — MCP hub integration and platform expansion

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

### Data Caveats (Quality, Coverage, and Scale)

#### Time-Based Characteristics
- FY2020 includes a COVID-era filing window extension spike (intentionally retained and annotated)
- Late-certification outliers are excluded from timing charts using a fixed threshold
- FY2026 data is partial and still evolving

---

#### Data Completeness & Coverage
- Not all datasets contain complete fields across all entities (e.g., ServiceType nulls in Form 471)
- Entity-level discount rates are only available for a subset of entities (~15% coverage)
  - Primarily district and library-system level
  - Individual schools often lack this data
- UI features must reflect only data that is reliably present (no inferred values)

---

#### Schema Variability (Upstream Risk)
- USAC datasets may change structure without notice
- Example:
  - Form 471 CSV headers changed from `snake_case` → `Title Case`
- Import pipelines must be resilient to schema drift

---

#### Data Integrity & Processing
- Socrata ingestion uses idempotent upsert logic via `RawSourceKey`
- Duplicate records are prevented at ingestion time
- Aggregation-first model prevents duplication at analytics layer

---

#### Scale Characteristics
- Datasets have grown significantly:
  - Funding commitments: ~1.6M+ rows
  - Disbursements: ~3.2M+ rows
- Large imports approach SQLite limits:
  - Long-running jobs (~60+ minutes)
  - Occasional connection failures near completion
- Current architecture is near the upper bound of SQLite capability

---

#### Operational Limitations
- Data refresh is manual and not scheduled
- Partial import failures can result in small data gaps (<0.1%)
- Some datasets may be temporarily unavailable (e.g., USAC 503 responses)

---

#### Strategic Implication
- SQLite is sufficient for POC and demo workloads
- Migration to Postgres (CC-ERATE-000056) is required for:
  - reliability at scale
  - improved query performance
  - long-term system viability
  

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
- CC-ERATE-000055 — Entity type badge color enhancement (next)
- CC-ERATE-000056 — MCP hub integration (follows 000055)

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
- TD-006: SQLite limitations under large-scale aggregation/import (connection errors on long-running jobs; approaching system limits)  
- TD-007: Analytics executed on raw tables (no materialized views; may not scale)  
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
- TD-021: GitHub Actions test runner hang (CI instability; local execution remains authoritative)  
- TD-022: Form 471 schema drift (USAC CSV header change breaks parser)  
- TD-023: Test suite hygiene (tautological/low-value tests identified in CC-ERATE-TEST-AUDIT-001; cleanup pending)  
- TD-024: Platform scaling limitation (SQLite → Postgres migration required for reliability and long-term viability; addressed in CC-ERATE-000056)  

### WHAT TO IGNORE
- `erate-workbench/` subdirectory — legacy
- `reports/` output files
- UI test build artifacts
- Any Windows-local assumptions

## Home Lab Infrastructure

### dude-mcp-01
- **Role:** Primary MCP hub, Postgres server, CI/build node
- **Hardware:** Dell Latitude 7400, Intel i7-9750H (6-core, 4.5GHz boost), 16GB DDR4, 512GB NVMe SSD
- **OS:** Ubuntu 24.04 LTS (kernel 6.8.0-106-generic)
- **IP:** 192.168.1.208 (static, DHCP reserved at router)
- **Tailscale:** enrolled
- **SSH:** ssh drake@192.168.1.208
- **User:** drake
- **Postgres:** 
  - App user: erate
  - App database: eratedb
  - Superuser: postgres
- **Installed:** Node.js v24, Claude Code 2.1.86, Git, curl, wget, htop
- **Ethernet:** TP-Link UE306 USB-C adapter (enx9c69d375f5a0)
- **Provisioned:** 2026-03-28

### Fleet Naming Convention
- dude-mcp-01 — MCP hub (this node)
- dude-mcp-02 — reserved
- dude-db-01 — dedicated database (future)
- dude-ci-01 — dedicated CI (future)
- dude-mon-01 — monitoring (future)

### Network
- Home subnet: 192.168.1.0/24
- Gateway: 192.168.1.1
- Mesh VPN: Tailscale
- Switch: Nighthawk (desk mounted)
- Backhaul: single Cat6 run FiOS → desk switch

### Golden Image - Laptop Specific Settings
- Disable lid sleep: set HandleLidSwitch=ignore in /etc/systemd/logind.conf
- Apply with: sudo systemctl restart systemd-logind