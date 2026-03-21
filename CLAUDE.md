# ERATE WORKBENCH — Claude Code Project Brain
# Auto-loaded by Claude Code at session start. Do not delete.
# Last updated: 2026-03-21 | Boot Block: CC-ERATE-000037

---

## BOOT BLOCK
— # Last updated: 2026-03-21 | Boot Block: CC-ERATE-000037

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

### CURRENT STATE (as of CC-ERATE-000037)
- **Last completed:** CC-ERATE-000037 — Desktop navigation UX refresh (grouped dropdowns)
- **Branch:** feature/desktop-nav-refresh — completed locally, PR not yet opened
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
  - Filing Window Analytics dashboard at /FilingWindow — Chart.js visualizations for
    cumulative certification timing, requested vs committed by year, commitment rate,
    and application status breakdown; FY2026 progress cards; FY2020 COVID annotation
  - Desktop navigation reorganized into grouped dropdowns (Dashboard, Explore, Insights,
    Reference, Help); Swagger UI moved under Help; mobile hamburger behavior unchanged
- **Caveats (data quality):**
  - FY2020 contains a COVID window extension spike (Sept–Oct 2020, ~2,317 records) — annotated in dashboard
  - Late-certification outliers (CertificationDate > FY+1 Jul 1) excluded from timing charts
  - ServiceType is null for all Form 471 records — not available in dataset 9s6i-myen
  - FY2026 is in-progress / partial — dashboard shows advisory banner
- **Merge dependency:**
  - feature/filing-window-analytics and feature/desktop-nav-refresh must merge together —
    the nav Explore dropdown links to /FilingWindow which does not exist on main until both land
- **Nav groups (reference):**
  - Explore → School & Library Search, Analytics, Filing Window
  - Insights → Risk Insights, Advisor Playbook
  - Reference → Program Workflow, Ecosystem, History
  - Help → About, Release Notes, Swagger UI

### ACTIVE TASK
- Next prompt: CC-ERATE-000038
- Status: Pending — architect session required to define next task

### KNOWN DEBT (summary — see docs/context/technical-debt.md for full detail)
- TD-001: HttpClient default timeout on long imports (Medium)
- TD-002: Import observability/progress reporting weak (Medium)
- TD-003: No true year-scoped import (Low-Medium) — resolved for Form 471 in CC-ERATE-000036; other datasets unchanged
- TD-004: Summary rebuild order is manual (Low-Medium)
- TD-006: Full outer join in-memory for Risk summary (Low, SQLite limitation)
- TD-007: Analytics queries on raw tables, not summaries (Low-Medium)
- TD-008: No deletion detection against Socrata source (Low)
- TD-011: Analytics cache has no invalidation on import (Low)
- TD-012: [DIAG] log lines still active in FundingCommitmentCsvParser (Low)
- TD-013: xUnit2013 analyzer warning in ReconciliationTests.cs (Negligible)
- TD-014: Playwright local WSL browser deps (Low)
- TD-015: Dependabot PR queue management (Low)
- TD-016: UI/theme polish behind engineering maturity (Low) — nav IA addressed in CC-ERATE-000037; visual polish remains

### WHAT TO IGNORE
- `erate-workbench/` subdirectory — this is a legacy/duplicate artifact, not the canonical source
- `src/ErateWorkbench.Api/reports/` — reconciliation output files, not source code
- `tests/ErateWorkbench.UITests/bin/` — build artifacts, never edit
- Any Windows-local path assumptions

---

## COMMON COMMANDS

```bash
# Build
dotnet build ErateWorkbench.sln

# Run tests
dotnet test ErateWorkbench.sln

# Run app
cd src/ErateWorkbench.Api && dotnet run

# Run with log capture
./scripts/run-with-logs.sh   # if exists, otherwise: dotnet run | tee app.log

# UI smoke tests
cd tests/ErateWorkbench.UITests && dotnet test

# Vulnerability scan
dotnet list package --vulnerable

# Secrets scan
gitleaks git --log-level warn

# New feature branch
git checkout main && git pull && git checkout -b feature/[task-name]

# PR process after work
git push -u origin feature/[branch-name]
# Then open PR on GitHub, wait for CI green, merge, delete branch, git checkout main && git pull
```

## KEY FILE PATHS
- Solution root: `~/projects/erate-workbench/`
- API project: `src/ErateWorkbench.Api/`
- Domain: `src/ErateWorkbench.Domain/`
- Infrastructure: `src/ErateWorkbench.Infrastructure/`
- Unit tests: `tests/ErateWorkbench.Tests/`
- UI tests: `tests/ErateWorkbench.UITests/`
- CI pipeline: `.github/workflows/`
- Scripts: `scripts/`
- Architecture decisions: `docs/context/architecture-decisions.md`
- Technical debt: `docs/context/technical-debt.md`
- Quality docs: `docs/quality/`
- DevOps docs: `docs/devops/`

## CODE STYLE
- C# / .NET 8 idioms — no legacy patterns
- Dependency injection everywhere — no service locator
- Typed queries via EF Core — no raw SQL unless performance-justified and documented
- Minimal API for data endpoints, Razor Pages for UI
- ILogger<T> for all logging — no Console.WriteLine in production paths
- xUnit for all tests — no MSTest or NUnit

## DEPENDABOT GOVERNANCE
- GitHub Actions updates: safe to merge after green CI
- Test/tooling updates: one at a time after green CI
- Major runtime/data-layer upgrades: deliberate engineering work, not routine merges
