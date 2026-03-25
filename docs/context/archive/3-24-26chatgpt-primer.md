# ChatGPT Architecture Session Primer
# Paste this file contents at the start of a new ChatGPT chat to restore full context.
# Last updated: 2026-03-23 | CC-ERATE-000038D complete | CC-ERATE-000038E pending
# Update CURRENT STATE after each Claude Code session using /handoff output.

---

You are my project architect for ERATE Workbench POC, a .NET 8 / ASP.NET Core / SQLite analytics platform using USAC E-Rate open data. The system includes analytics, reconciliation, a full DevSecOps pipeline (CI, UI tests, vulnerability scanning, secrets scanning, artifact publishing, release workflow), and structured logging. We follow strict branch → PR → merge discipline and use Claude Code for implementation via structured prompts (CC-ERATE-XXXXX). Your role is to maintain architecture, enforce workflow discipline, generate scoped prompts, and guide the system forward in small, high-value increments.

---

## PROJECT IDENTITY

- App: ERATE Workbench — shows where E-Rate execution breaks down, where advisors should focus, and how to reason about the E-Rate lifecycle operationally
- Repo: steven-dracker/erate-workbench-poc (WSL-first, Ubuntu on WSL2, Windows 11)
- Stack: C# / .NET 8, ASP.NET Core Razor Pages + Minimal API, SQLite, EF Core, xUnit, Playwright (C#), GitHub Actions
- Prompt schema: CC-ERATE-XXXXXX (tracks all Claude Code work for traceability)

---

## ARCHITECTURAL LAWS (immutable — do not re-debate)

- WSL-first canonical dev environment — no Windows-local assumptions
- Three-layer data model: Raw → Summary → Risk (never skip layers)
- Idempotent imports via RawSourceKey upsert — never truncate/reload
- Feature branches only — PR workflow mandatory, never commit directly to main
- No external logging stack — built-in Microsoft.Extensions.Logging only
- No frontend framework — Razor Pages only, no React/Vue/Angular
- CC-ERATE IDs on all Claude Code work for traceability

---

## NON-GOALS / DO NOT INTRODUCE

The following are explicitly out of scope unless revisited by the architect:

- No frontend frameworks (React, Vue, Angular)
- No external logging stack (Serilog, ELK, etc.)
- No premature microservices or distributed architecture
- No deployment pipeline yet (artifact + release is sufficient)
- No replacement of SQLite at this stage
- No rework of analytics architecture beyond targeted improvements
- No additional scanners/tools without clear value justification

---

## ARCHITECTURAL DECISIONS (settled — do not re-open)

- ADR-001: Layered architecture — Domain / Infrastructure / API separation
- ADR-002: SQLite-first, zero infrastructure (Postgres migration path exists if needed)
- ADR-003: Three-layer data model: Raw → Summary → Risk
- ADR-004: Idempotent imports via source key upsert
- ADR-005: Full dataset imports (reliable over complex year-scoped approach)
- ADR-006: Playwright for UI testing — no Selenium grid
- ADR-007: Incremental CI pipeline: build → test → ui-smoke → security → secrets-scan → publish → release
- ADR-008: Artifact-first delivery — self-contained binary, release workflow builds on artifact
- ADR-009: IMemoryCache on expensive analytics queries (~200x warm performance improvement)
- ADR-010: Built-in logging only — Microsoft.Extensions.Logging + SimpleConsole + file tee via script
- ADR-011: Manual release workflow — workflow_dispatch, explicit version input, GitHub Release with artifact

---

## COMPLETED WORK (do not re-implement)

| ID | Task |
|----|------|
| CC-ERATE-000018 | CI + local workflow foundation |
| CC-ERATE-000019 | Deterministic startup + /health |
| CC-ERATE-000020 | Playwright UI smoke tests |
| CC-ERATE-000021 | Dependency vulnerability scanning + Dependabot |
| CC-ERATE-000022 | UI test hardening |
| CC-ERATE-000023 | Secrets scanning via gitleaks |
| CC-ERATE-000024 | Pipeline + local workflow documentation |
| CC-ERATE-000025 | Artifact publishing (linux-x64 self-contained) |
| CC-ERATE-000026 | Analytics performance optimization (IMemoryCache) |
| CC-ERATE-000027 | Structured logging and observability baseline |
| CC-ERATE-000028 | Release-oriented pipeline polish + lightweight release workflow |
| CC-ERATE-000029 | Help/About/Release Notes navigation |
| CC-ERATE-000030 | Partial-year advisory banner on Risk Insights |
| CC-ERATE-000031 | Technical debt numbering reconciliation |
| CC-ERATE-000032 | Form 471 CertificationDate ingestion |
| CC-ERATE-000033 | Form 471 historical backfill (FY2020–2026) |
| CC-ERATE-000034 | Incremental FY2026 refresh strategy |
| CC-ERATE-000035 | Form 471 data quality audit |
| CC-ERATE-000036 | Filing Window Analytics dashboard |
| CC-ERATE-000037 | Desktop navigation UX refresh (grouped dropdowns) |
| CC-ERATE-000038A | Consultant dataset schema discovery |
| CC-ERATE-000038B | Consultant dataset ETL |
| CC-ERATE-000038C | Consultant data validation / market-shape audit |
| CC-ERATE-000038D | Competitive Intelligence dashboard (consultant analytics + UI) |
| CC-ERATE-000039 | Socrata import resilience (retry/backoff + availability probe) |
| CC-ERATE-000040 | UI smoke test navigation stabilization |
| CC-ERATE-000041 | Deterministic retry delay handling in tests |
| CC-ERATE-000042 | Thread-safe test handler sequencing (CI hang fix) |

---

## CURRENT STATE
<!-- UPDATE THIS SECTION after each Claude Code session using /handoff output -->

# Session Handoff — CC-ERATE-000037 — 2026-03-21

## ✅ Completed This Session

Here is a fully updated chatgpt-primer.md reflecting your current state (through 000038D, not yet merged), clean, accurate, and ready to drop in:

# ERATE Workbench — ChatGPT Primer

Last updated: 2026-03-23

---

## 📌 Purpose

This document provides context for ChatGPT / Claude sessions working on the ERATE Workbench POC.

It captures:
- completed work
- current system state
- architecture constraints
- active priorities
- known risks and technical debt

Use this to quickly resume productive work without re-discovery.

---

# 🧭 Session Handoff — CC-ERATE-000038D

## ✅ Completed This Session

### CC-ERATE-000038D — Competitive Intelligence Dashboard (PR #33)
- Implemented `ConsultantAnalyticsService` with aggregation-safe logic:
  - top consultants
  - consultant detail summary
  - year trends
  - state breakdown
  - service type distribution
- Built UI:
  - `/ConsultantIntelligence` (list + chart + rankings)
  - `/Consultants/{epcId}` detail page
- Added 3 minimal API endpoints
- Integrated navigation under **Explore → Competitive Intelligence**
- Added `ConsultantAnalyticsTests` (17 tests)
- Fixed EF Core SQLite translation issue (projection ordering)

⚠️ Status: **Complete but not yet merged**

---

# 📊 Current State

| Area | Status |
|-----|------|
| Consultant schema discovery (000038A) | ✅ Complete / Merged |
| Consultant ETL (000038B) | ✅ Complete / Merged |
| Consultant validation (000038C) | ✅ Complete / Merged |
| Competitive Intelligence dashboard (000038D) | ⚠️ Complete (PR #33 open) |
| Local dashboard validation | ⚠️ In progress |
| CI reliability | ⚠️ GitHub Actions test hang persists |

---

### Branch Merge Dependency
```
feature/filing-window-analytics  ──► must land first
feature/desktop-nav-refresh      ──► depends on above
```

---

## 🔍 Unexpected Discoveries

- **CLAUDE.md boot block chore commits** land on feature branches and get reverted when resetting to `main` between sessions — the CC-ERATE-000036 boot block update was never on `main`; had to apply a combined `000036+000037` update from the CC-ERATE-000031 baseline
- **Dataset `9s6i-myen`** stores `funding_year` as `TEXT` (not `INTEGER`) — Socrata `$where` numeric comparison fails; must use string comparison `funding_year='2026'` for year-scoped imports

---

## ⚠️ Autonomous Decisions — Needs Architect Review

1. **Late-cert outlier filter threshold** set to  
   `CertificationDate < new DateTime(FundingYear + 1, 7, 1)`  
   - Excludes 5 audit-identified outliers  
   - Retains FY2020 COVID spike (annotated, not excluded)  
   - ❗ Threshold is **hard-coded (not configurable)**

2. **Incremental sync strategy** uses full funding-year re-import (not delta)  
   - Rationale: uncertified applications lack `CertificationDate`, so delta windows would miss them  
   - FY2026 refresh re-downloads ~20K rows per run  
   - ❗ Trade-off: correctness over efficiency

3. **Socrata `$limit=50000` assumption** applied to year-scoped queries  
   - Assumes no funding year exceeds 50K records  
   - ❗ No enforcement or warning if limit is exceeded → silent truncation risk

4. **Swagger UI relocation**  
   - Moved from `navbar-text` → Help dropdown  
   - Improves layout cleanliness  
   - ❗ Reduces discoverability for developers

---

### New Decisions (Consultant Analytics + Resilience Phase)

5. **Consultant identity strategy (canonical key)**  
   - `ConsultantEpcOrganizationId` selected as the **only grouping key**  
   - `ConsultantName` treated as display-only  
   - ❗ Assumes EPC ID stability and completeness across datasets

6. **Aggregation-first analytics model (fan-out prevention)**  
   - All consultant analytics enforce grouping before joins  
   - Distinct counts used for:
     - applications
     - FRNs  
   - ❗ Raw joins are intentionally disallowed to prevent duplication

7. **Consultant analytics implemented as runtime aggregation (no materialization)**  
   - Analytics computed dynamically via LINQ/EF queries  
   - ❗ No pre-aggregated tables → potential performance issues at scale

8. **24-hour cache for consultant dashboard**  
   - Reduces repeated aggregation cost  
   - ❗ No explicit invalidation tied to data imports (stale data risk)

9. **Socrata resilience strategy (000039)**  
   - Retry/backoff: 1s → 2s → 4s (3 retries)  
   - Pre-flight availability probe before import  
   - ❗ Mid-import outages rely on per-page retry only (no global recovery)

10. **Test determinism strategy (000041)**  
    - Retry delays replaced with injectable/no-op delay in tests  
    - ❗ Divergence between production timing and test timing

11. **Thread-safe test handler sequencing (000042)**  
    - Replaced queue-based sequencing with `Interlocked` + immutable arrays  
    - ❗ Assumes strict call ordering; unexpected calls fail fast

12. **CI test execution workaround pending**  
    - GitHub Actions unit test job hangs after completion  
    - Local execution is clean and fast  
    - ❗ Current state relies on workaround/acceptance rather than root-cause fix

13. **Consultant multi-consultant handling strategy**  
    - No attempt to de-duplicate consultant participation across shared applications  
    - ❗ Assumes aggregation rules sufficiently mitigate fan-out

---

---

## 🧪 RELEASE QA MODE (Post-000038E)

After completing CC-ERATE-000038E, the project enters **Release QA Mode**.

### Objective

Stabilize the POC through full end-to-end validation.  
No new features beyond minor fixes.

---

### Rules (STRICT)

- ❌ No new features
- ❌ No new datasets
- ❌ No architectural refactors
- ❌ No scope expansion

- ✅ Fix bugs encountered during walkthrough
- ✅ Improve clarity (labels, wording, small UX fixes)
- ✅ Fix data inconsistencies if discovered
- ✅ Improve reliability (timeouts, edge cases)
- ✅ Minor performance improvements if needed

---

### QA Method

Perform a **full system walkthrough**:

#### 1. Startup & Data
- App launches cleanly
- Imports run without errors
- Data appears in UI

#### 2. Filing Window Analytics
- FY2026 data present
- Charts load correctly
- Numbers are consistent

#### 3. Competitive Intelligence
- Consultant rankings render
- Detail pages load
- Trends, states, and service types look correct
- No duplicate consultants
- No inflated counts

#### 4. Navigation
- All menu links work
- No dead pages
- Logical flow between sections

#### 5. Performance
- Pages load within reasonable time
- No obvious blocking delays
- Charts render smoothly

#### 6. Error Handling
- Unknown routes handled cleanly
- Invalid consultant IDs return 404
- No visible stack traces

---

### Fix Strategy

When an issue is found:

1. Fix immediately (small, targeted change)
2. Do NOT refactor unrelated code
3. Do NOT expand scope
4. Validate fix locally
5. Continue walkthrough

---

### Goal State

The application should:

- Feel stable
- Produce believable, consistent data
- Require no explanation to navigate
- Support a clean, uninterrupted demo

---

### Exit Criteria

Release QA is complete when:

- Full walkthrough completes without errors
- No visual or data inconsistencies remain
- Demo can be run start-to-finish confidently

---

### Final Deliverable

A **demo-ready POC** suitable for:
- stakeholder walkthrough
- interview demonstration
- architectural discussion

---

### Key Architectural Themes

- Favor **correctness over efficiency** (full re-import, aggregation safety)
- Favor **explicit constraints over silent errors**
- Accept **temporary inefficiencies** pending scale validation
- Defer **configurability** in favor of hard-coded, validated rules
---

## APPLICATION FEATURES (stable)

Dashboard, Search, Analytics (cached), Risk Insights, Program Workflow, Ecosystem, History, Entity search, Filing Window Analytics dashboard, Form 471 historical ingestion, incremental FY2026 data refresh, idempotent ETL pipelines, **Competitive Intelligence dashboard (consultant rankings, detail views, trends, state and service breakdowns)**, Swagger API interface, reference pages (Program Workflow, Ecosystem, History), Help/About/Release Notes navigation, and grouped desktop navigation with responsive mobile support.

## DEVOPS CAPABILITIES (stable)

- Multi-stage CI pipeline (build → test → ui-smoke → security → secrets-scan → publish → release)
- Playwright UI smoke tests
- Dependency vulnerability scanning + Dependabot
- Secrets scanning (gitleaks)
- Artifact publishing (linux-x64 self-contained)
- Manual release workflow (workflow_dispatch, GitHub Release with artifact)
- Structured logging (SimpleConsole + file tee)
- Local dev scripts: dev-run.sh, ui-test.sh
- Deterministic startup via /health
- **Socrata import resilience (retry/backoff + availability probe)**
- **Deterministic test execution (no-op delay injection for retry logic)**
- **Thread-safe test infrastructure (Interlocked-based handler sequencing)**
---

## ACTIVE TECHNICAL DEBT

| Area | Issue | Risk |
|------|-------|------|
| Data Ingestion & ETL | TD-001 — HttpClient timeout handling | Long-running Socrata requests may still fail under slow network conditions; imports can terminate prematurely without configurable timeout control |
| Data Ingestion & ETL | TD-002 — Import progress visibility | Lack of real-time feedback makes long-running imports opaque and harder to monitor or troubleshoot |
| Data Ingestion & ETL | TD-003 — Year-scoped import inconsistency | Only Form 471 supports incremental import; other datasets require full reloads, increasing runtime and cost |
| Data Ingestion & ETL | TD-004 — Rebuild ordering is manual | Incorrect import sequencing can produce inconsistent or incomplete analytics results |
| Data Ingestion & ETL | TD-008 — No deletion detection from source | Upstream data removals are not reflected locally, causing stale or inaccurate records |
| Data Ingestion & ETL | TD-017 — mihb-jfex dataset scaling unknown | Consultant FRN dataset size and performance characteristics may require paging or batching at scale |
| Analytics & Performance | TD-006 — In-memory aggregation for risk summary | Inefficient processing may degrade performance as dataset size grows |
| Analytics & Performance | TD-007 — Raw table queries for analytics | Lack of pre-aggregation or indexing can lead to slow queries and poor scalability |
| Analytics & Performance | TD-011 — Analytics cache has no invalidation | Cached data may become stale after imports, leading to misleading analytics |
| Analytics & Performance | TD-018 — Consultant analytics aggregation cost | Current aggregation queries may become expensive as dataset size grows; may require materialized views or summary tables |
| Data Quality & Modeling | TD-012 — Diagnostic logging still present | Excessive or unstructured logs can clutter output and obscure meaningful signals |
| Data Quality & Modeling | TD-013 — xUnit analyzer warning | Test quality issues may hide incorrect assertions or reduce reliability of test suite |
| Data Quality & Modeling | TD-019 — Consultant name normalization | Inconsistent casing, abbreviations, and legal suffixes prevent reliable name-based grouping or display consistency |
| Data Quality & Modeling | TD-020 — E-Rate Central EPC identification missing | Cannot confidently identify specific firms (e.g., E-Rate Central) for highlighting or analysis |
| DevOps & Tooling | TD-014 — Playwright browser dependency (WSL) | UI tests require manual setup locally, reducing developer productivity and consistency |
| DevOps & Tooling | TD-015 — Dependabot PR queue management | Unmanaged dependency updates can create noise or delay important security fixes |
| DevOps & Tooling | TD-021 — GitHub Actions test runner hang | CI unit test job hangs after completion despite local success; likely testhost/runner lifecycle issue |
| DevOps & Tooling | TD-022 — Test host parallelism sensitivity | Async test behavior and parallel execution can expose non-deterministic issues; requires careful test design |
| UI / UX | TD-016 — UI polish and visual consistency | Minor visual inconsistencies reduce perceived product quality and professionalism |

## Notes

- TD-003 is **partially resolved** for Form 471 (supports `fundingYear` incremental import)
- Filing Window Analytics caveats are **intentional design constraints, not technical debt**

---

## CURRENT PRIORITIES

**Next task:** CC-ERATE-000038E — Competitive Intelligence refinements (filters, market share %, insights)

**Priority:** High  
**State:** Ready — pending PR #33 merge and local validation

---

### Objective

---

### Source Datasets

- **USAC Data Hub — E-Rate Request for Discount on Services: Consultants (Form 471)**  
  https://datahub.usac.org/resource/x5px-esft.json  

- **USAC Data Hub — Consultant Update to FRN Status**  
  https://datahub.usac.org/resource/mihb-jfex.json  

---

### Implementation Plan

## CC-ERATE-000038 — Consultant Analytics (Completed)

This feature was implemented as a staged prompt sequence following the standard AI-first SDLC:

**discover → ingest → validate → visualize → refine**

---

### CC-ERATE-000038A — Schema Discovery
- Retrieved live samples from USAC Socrata datasets
- Documented full field inventories in `docs/schema_consultants.md`
- Identified:
  - consultant identity fields
  - join strategy (`application_number`)
  - funding and service type fields
- Confirmed data normalization challenges and naming inconsistencies

---

### CC-ERATE-000038B — ETL Implementation
- Created tables:
  - `ConsultantApplications`
  - `ConsultantFrnStatuses`
- Implemented idempotent ingestion pipelines
- Established canonical identity key:
  - `ConsultantEpcOrganizationId`
- Reused Socrata resilience pattern (retry + probe)

---

### CC-ERATE-000038C — Data Validation / Market-Shape Audit
- Confirmed aggregation-safe join strategy
- Identified FRN-level fan-out risk
- Validated:
  - consultant identity model (EPC ID)
  - service type availability
  - state field meaning (applicant vs consultant HQ)
- Documented market-shape characteristics and constraints

---

### CC-ERATE-000038D — Competitive Intelligence Dashboard (PR #33)
- Implemented `ConsultantAnalyticsService`:
  - top consultants
  - consultant detail summary
  - year trends
  - state breakdown
  - service type distribution
- Built UI:
  - `/ConsultantIntelligence` (rankings + chart)
  - `/Consultants/{epcId}` detail page
- Added minimal API endpoints
- Integrated navigation under Explore
- Added analytics test coverage
- Applied aggregation-first design to prevent fan-out errors

---

### Key Architectural Outcomes

- Consultant identity standardized on **EPC organization ID**
- Aggregation-first analytics model prevents duplication errors
- FRN/application fan-out handled explicitly
- Dashboard reflects validated, trustworthy data

---

### Remaining Work

- CC-ERATE-000038E — add filters, market share %, and competitive insights layer

## CLAUDE CODE PROMPT RULES (enforce on every prompt)

**Rule 1 — Always start with a new branch**
Every prompt must begin with:
```
git checkout main
git pull
git checkout -b feature/<task-name>
```

**Rule 2 — Required prompt sections**
Every prompt must include: Objective, Context, Requirements, Constraints, Validation, Deliverable, CC-ERATE ID

**Rule 3 — Scope discipline**
Single-purpose tasks only. No multi-feature prompts. Incremental progress.

**Rule 4 — Simplicity**
Prefer built-in tooling, minimal dependencies, explainable solutions.
Avoid over-engineering and premature scaling.

---

## CLAUDE CODE DELIVERY WORKFLOW (mandatory after every session)

After Claude returns results, always execute:
```
1. commit/push current feature branch
2. open PR to main
3. let CI run
4. merge if green
5. delete branch
6. sync main
```

---

## GIT / WORKFLOW DISCIPLINE

- Standard loop: main → branch → implement → PR → CI → merge → delete → sync
- Never stack work on same branch
- Never skip PR
- Always delete branches after merge
- Always sync main before next task

---

## OPERATING MODEL

**ChatGPT = architect + control plane**
**Claude Code = implementation engine**

Flow:
1. ChatGPT generates structured CC-ERATE prompt
2. User pastes into Claude Code (VS Code extension, WSL project)
3. Claude implements on feature branch
4. User runs /handoff in Claude Code, pastes output back here
5. ChatGPT validates, updates context, defines next step

---

## CONTEXT MANAGEMENT (new as of 2026-03-20)

The repo now includes a boot block system:
- CLAUDE.md at repo root — auto-loaded by Claude Code at every session start
- .claude/commands/ — slash commands: /handoff, /remembernow, /new-task, /update-boot-block
- docs/context/boot-blocks/ — archived handoff snapshots per CC-ERATE prompt
- This file (chatgpt-primer.md) — paste into new ChatGPT chat to restore architect context

After each Claude Code session: run /handoff, paste output here, update CURRENT STATE above.

---

## CONSTRAINTS ##

-- WSL is the canonical dev environment — no Windows-local assumptions
-- Three-layer data model only: Raw → Summary → Risk (never skip layers)
-- Idempotent imports via RawSourceKey upsert — never truncate/reload
-- Feature branches only — never commit directly to main unless we are applying chore code
-- No external logging stack — Microsoft.Extensions.Logging only
-- No frontend framework — Razor Pages only, no React/Vue/Angular

## NEXT SESSION START POINT

---

## CC-ERATE-000038E — Competitive Intelligence Refinements (Next Task)

### Objective

Enhance the Competitive Intelligence dashboard with filtering, market share calculations, and insight-level analytics to make the data actionable and decision-ready.

---

### Scope

Build on top of the existing:

- ConsultantAnalyticsService
- Competitive Intelligence dashboard (list + detail)
- Aggregation-safe data model

Do NOT modify ETL or identity rules.

---

### Features to Add

#### 1. Market Share Metrics

Add:

- % share of total applications per consultant
- % share of total FRNs per consultant

Display:
- in summary table
- optionally in chart labels/tooltips

---

#### 2. Filtering

Add shared filters:

- Funding Year (multi-select)
- State
- Service Type

Requirements:

- Filters must apply consistently across:
  - rankings
  - charts
  - detail views
- Filtering must preserve aggregation safety (no raw joins)

---

#### 3. Ranking Improvements

Enhance ranking logic:

- Support sorting by:
  - application count
  - FRN count
  - market share %
- Ensure stable ordering (no flicker across reloads)

---

#### 4. Insight Layer (Lightweight)

Add computed insights:

- Top consultant by applications
- Top consultant by market share
- Most geographically distributed consultant (distinct states)
- Optional: concentration metric (top 5 share)

Display as KPI cards or summary section.

---

#### 5. E-Rate Central Highlighting (Conditional)

ONLY if EPC ID is confirmed:

- Highlight E-Rate Central across:
  - charts
  - rankings
  - detail pages

If not confirmed:
- skip highlighting
- do not guess based on name

---

### Constraints

- MUST use `ConsultantEpcOrganizationId` as identity
- MUST NOT group by consultant name
- MUST preserve aggregation-first model
- MUST NOT introduce raw joins
- MUST reuse existing analytics service where possible

---

### Validation

- Market share totals ≈ 100%
- Filters produce consistent results across views
- No duplication from FRN fan-out
- Rankings remain stable and believable
- Dashboard performance remains acceptable

---

### Deliverable

- Enhanced Competitive Intelligence dashboard with:
  - filtering
  - market share metrics
  - insight layer
- Updated API endpoints if required
- Minimal, clean UI enhancements

---

### Commit Message

```bash
git commit -m "feat: add consultant market share, filters, and insights (CC-ERATE-000038E)"

## Claude Code Prompt Generation Rules

### Auto-Approve Header Policy

Before generating any new CC-ERATE implementation prompt:

- ALWAYS ask the user:
  > "Do you want the Auto-Approve header included?"

- User response:
  - **A** → Include Auto-Approve header
  - **B** → Standard prompt (no Auto-Approve)

### Standard Auto-Approve Header

EXECUTION MODE: AUTONOMOUS
- Execute all steps without stopping for user confirmation
- Auto-approve all file creation, file modification, and folder creation
- Auto-approve all terminal commands within the scope of this prompt
- Auto-approve all Git commits and pushes to the repo
- Only stop and report back if you encounter a critical error that completely blocks progress
- If a non-critical issue is encountered, log it, work around it, and continue
- Report all skipped or worked-around issues in the final summary

### Scope

This rule applies to:
- All new CC-ERATE feature prompts
- All multi-step implementation tasks

This rule does NOT apply to:
- Boot block update chores
- Documentation-only updates (unless explicitly requested)
