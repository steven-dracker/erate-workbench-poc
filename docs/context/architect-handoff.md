ERATE Workbench POC — Architect Handoff

Last updated: 2026-03-19

1. PROJECT OVERVIEW
What we are building and why

ERATE Workbench POC is a .NET 8 / ASP.NET Core / SQLite analytics and workflow platform built around USAC E-Rate open data.

It started as a domain-rich interview/demo artifact and has evolved into a credible miniature platform demonstrating:

real-source data ingestion

analytics and advisory-oriented product thinking

backend architecture

test discipline

validation/reconciliation discipline

DevOps / DevSecOps maturity

The product thesis is:

Most E-Rate dashboards stop at requested or committed dollars.
ERATE Workbench aims to show where execution breaks down, where advisors should focus, and how to reason about the E-Rate lifecycle operationally.

The application currently includes:

Dashboard

Search

Analytics

Risk Insights

Program Workflow

Ecosystem

History

It also now includes an increasingly mature engineering backbone:

local developer workflow scripts

GitHub Actions CI

Playwright UI smoke tests

dependency vulnerability scanning

secrets scanning

build artifact publishing

operational documentation

basic local observability/logging

Core technical stack and why it was chosen
Language / runtime

C# / .NET 8

Why:

strong typing

mature ecosystem

good architectural credibility

good testability and runtime performance

appropriate for senior/architect-level discussion

Web framework

ASP.NET Core with Razor Pages + Minimal API

Why:

fast full-stack iteration

clean server-rendered UI for a POC

simple routing and layout inheritance

avoids unnecessary frontend framework complexity

supports both pages and data endpoints cleanly

Database

SQLite

Why:

zero infrastructure burden

portable, local-first demo setup

fast iteration

good fit for a POC with summary-layer architecture

easy migration path later to Postgres if needed

ORM / data access

Entity Framework Core

Why:

schema evolution with migrations

typed queries

maintainable code-first/data-access story

Source ingestion

Socrata API + CSV parsing

Why:

real public USAC E-Rate data

supports both ingestion and reconciliation

makes the app analytically credible

Testing

xUnit for unit/integration-style tests
Playwright (C#) for UI smoke tests

Why:

consistent .NET-centered test stack

Playwright provides modern browser automation without Selenium/Grid complexity

simple CI integration

DevOps / CI

GitHub Actions

Why:

native to the current repo hosting platform

sufficient for build/test/security/artifact needs

easy incremental growth into a DevSecOps pipeline

Security / dependency hygiene

dotnet list package --vulnerable

gitleaks

Dependabot

Why:

lightweight but credible early-stage DevSecOps controls

no paid tooling or heavy infrastructure required

2. ARCHITECTURAL DECISIONS

Below are the major decisions in effect as of this handoff.

ADR-001 — Use a layered .NET solution structure

What
The codebase is organized into:

ErateWorkbench.Api

ErateWorkbench.Domain

ErateWorkbench.Infrastructure

ErateWorkbench.Tests

ErateWorkbench.UITests

Why

separation of concerns

clearer architectural story

testability

maintainability

Rejected alternatives

single-project MVC-style app

heavier DDD ceremony for a POC

Assumptions

the project should feel like a real platform, not a throwaway demo

ADR-002 — SQLite as primary datastore

What
SQLite is the local database for imports, summaries, analytics, and validation workflows.

Why

zero infra

portable demo setup

fast local iteration

sufficient for current POC scale

Rejected alternatives

Postgres / SQL Server as initial datastore

file-only analytics without a DB

Assumptions

local/demo-first operation is primary

current data size remains practical in SQLite

ADR-003 — Real USAC open data, not mock/demo data

What
The system imports real USAC open datasets, especially:

Funding Commitments

Disbursements

Why

domain credibility

realistic data behavior

true reconciliation/validation possible

stronger product story

Rejected alternatives

seed/demo data only

static mini CSV extracts only

Assumptions

public data inconsistency is acceptable and analytically valuable

ADR-004 — Idempotent imports via stable raw keys

What
Import services upsert by RawSourceKey instead of truncate/reload.

Why

safe re-runs

resilient to killed/failed imports

preserves partial progress

simpler recovery

Rejected alternatives

truncate-and-reload

complex hash-based change tracking

Assumptions

source keys are sufficiently stable

delete detection can be deferred

ADR-005 — Three-layer data model: Raw → Summary → Risk

What

Raw tables for source-level data

Summary tables for applicant/year aggregation

Risk tables for advisory/risk logic

Why

reconciliation against source remains possible

analytics queries can be fast

advisory logic has a curated derived layer

Rejected alternatives

raw-only analytics

SQLite materialized views (not native)

pure query-on-read model

Assumptions

summary rebuilds are acceptable

year-scoped processing is a useful unit of work

ADR-006 — Full-dataset imports instead of source-side year-scoped imports

What
Imports page the entire Socrata dataset; ?year=YYYY on import endpoints is not truly year-scoped.

Why

Socrata year-filtered bulk behavior proved slow/unreliable

full-dataset paging was the simplest reliable implementation

Rejected alternatives

optimistic year-filtered import path

more complex restartable background import system

Assumptions

full imports are acceptable in a POC

targeted repairs can tolerate longer runtimes

ADR-007 — Reconciliation via SoQL grouped source queries

What
Reconciliation compares local state against grouped source aggregates from Socrata.

Why

efficient validation

enough trust signal without full source replay

supports auditability

Rejected alternatives

trusting imports blindly

manual SQL spot checks only

Assumptions

grouped source reconciliation is sufficient for demo confidence

ADR-008 — Reference content integrated into the app

What
Program Workflow, Ecosystem, and History exist as integrated app pages rather than disconnected artifacts.

Why

improves product narrative

helps technical and non-technical demo audiences

keeps reference content “in product”

Rejected alternatives

keep those as external docs only

keep standalone static HTML forever

Assumptions

reference content is part of product understanding, not just repo docs

ADR-009 — Shared-layout Razor Pages for integrated reference pages

What
Ecosystem and History were converted from standalone/static serving into shared-layout Razor Pages.

Why

consistent navigation

avoids duplicate nav shells

better long-term maintainability

Rejected alternatives

permanent static-file routes with custom nav

Assumptions

shared layout consistency is more valuable than preserving the original standalone HTML structure

ADR-010 — Formal quality system, not just tests

What
docs/quality/ exists to separate:

tests

runbooks

evidence logs

audit findings

validation lifecycle

Why

data-heavy app needs runtime/data validation beyond code tests

supports credible demo readiness

Rejected alternatives

ad hoc QA notes

tests only, no evidence trail

Assumptions

demonstrating engineering discipline is part of the product value

ADR-011 — Prompt/task traceability with CC-ERATE IDs

What
Claude work is tracked with IDs like CC-ERATE-0000XX.

Why

traceability

better mapping between ask → implementation → validation

easier multi-AI workflow management

Rejected alternatives

informal prompting only

Assumptions

lightweight process overhead is worth the clarity

ADR-012 — WSL-first development environment is canonical

What
The repo was reset so the WSL working copy became the canonical source of truth. A new GitHub repo was created:

steven-dracker/erate-workbench-poc

Why

Windows pathing/data issues caused confusion

the populated SQLite/data setup and dev behavior were more stable in WSL

clean reset removed ambiguity from earlier Windows-local changes

Rejected alternatives

continue using Windows-local repo as primary

keep mixed WSL/Windows workflow

Assumptions

WSL remains the main local development environment going forward

ADR-013 — Short-lived feature branches + PR workflow

What
Going forward, new work should happen on feature branches, not directly on main.

Why

cleaner PR review units

easier rollback/isolation

better GitHub workflow discipline

avoids branch/PR confusion

Rejected alternatives

continuing direct-to-main workflow

Assumptions

every material change can be represented as a coherent task branch

Note: some work during transition was still committed directly to main; this should not continue.

ADR-014 — Lightweight CI pipeline first, then DevSecOps layers

What
The GitHub Actions pipeline was built incrementally:

build

test

ui-smoke

security

secrets-scan

publish

Why

incremental maturity

easier debugging and adoption

clearer architecture story

Rejected alternatives

giant monolithic pipeline from day one

immediate deploy/release complexity

Assumptions

foundational validation/security/artifact generation are more valuable than early deployment automation

ADR-015 — Playwright over Selenium/Grid for UI smoke automation

What
UI smoke tests use Playwright in C#.

Why

modern runner + browser engine in one tool

strong local and GitHub Actions support

avoids Selenium server/grid complexity

fits .NET repo well

Rejected alternatives

Cypress as first implementation

Selenium/Grid/BrowserStack integration up front

Assumptions

for this POC, small happy-path smoke tests are enough

cloud-browser execution can be a future extension if desired

ADR-016 — Dependency vulnerability scanning as first security control

What
CI includes dotnet list package --vulnerable.

Why

zero credentials

authoritative NuGet advisory source

practical for private repo

quick and actionable

Rejected alternatives

larger security suite immediately

paid/private-repo advanced tooling as first step

Assumptions

dependency risk is the highest-value first security signal

ADR-017 — Secrets scanning with gitleaks CLI

What
CI includes gitleaks run directly via CLI, not the gitleaks GitHub Action.

Why

avoids license requirement on private repos

simple, effective, GitHub-friendly

scans history when used with full checkout depth

Rejected alternatives

gitleaks GitHub Action requiring license

TruffleHog as initial implementation

Assumptions

repository-content/history scanning is sufficient as an early secrets control

ADR-018 — Produce publishable CI artifacts

What
CI now includes a publish stage that produces a self-contained linux-x64 publish artifact for the API project.

Why

moves pipeline beyond validation into deliverable output

clean handoff point for future release/deploy

appropriate next maturity step

Rejected alternatives

no artifact stage

immediate containerization/deployment

Assumptions

artifact-first is the right next evolution before deploy automation

ADR-019 — In-memory caching for Analytics page demo performance

What
The expensive Analytics queries are cached via IMemoryCache with 24-hour absolute expiration.

Why

Analytics page was too slow for demo use

underlying data is mostly static

built-in cache yields huge warm-path improvement with very low complexity

Rejected alternatives

immediate broad analytics architecture rewrite

naive parallel query fan-out on same EF context

Assumptions

slow cold request is acceptable

fast warm-path is more important for demo quality

data freshness is not critical on every request

ADR-020 — Built-in logging / observability baseline, no external logging stack

What
Logging was improved using built-in Microsoft.Extensions.Logging + SimpleConsole, with documentation and local log capture via script teeing.

Why

enough capability for local diagnostics

zero new packages

works with existing ILogger<T> usage

low operational complexity

Rejected alternatives

immediate Serilog or external observability stack

leaving logging in its weak prior state

Assumptions

local developer observability is the current need, not centralized production telemetry

3. ACTIVE WORK & OPEN ITEMS
What is currently in progress

As of this handoff, the major platform foundation work completed recently includes:

DevOps baseline

UI smoke automation

dependency vulnerability scanning

secrets scanning

pipeline documentation

artifact publishing

analytics performance optimization

logging / observability baseline

Most recent active implementation completed:

CC-ERATE-000027 — Structured logging and observability baseline

Status of that work:

implemented on feature/logging-observability

delivered summary includes commit:

9beae64

next step was to push/open PR/merge using normal process loop

What has been handed to Claude Code most recently

Recent Claude tasks in order:

CC-ERATE-000018 — local validation script + initial CI foundation

CC-ERATE-000019 — hardened startup behavior and /health

CC-ERATE-000020 — Playwright UI smoke automation

CC-ERATE-000021 — dependency vulnerability scanning + Dependabot

CC-ERATE-000022 — smoke test hardening for brittle History page assertion

CC-ERATE-000023 — secrets scanning via gitleaks

CC-ERATE-000024 — pipeline/local workflow documentation

CC-ERATE-000025 — publish/artifact stage

CC-ERATE-000026 — Analytics page performance optimization

CC-ERATE-000027 — structured logging / observability baseline

Current open questions / pending decisions
1. Merge / PR process discipline

The team/user should continue enforcing:

feature branch

push

PR

CI

merge

delete branch

sync main

This process still occasionally needs active reminder.

2. Next product/platform priority

The likely next candidate areas discussed were:

Help/About/Release Notes navigation

UI/theme/rebranding polish

further operational/logging improvements

release/versioning/publish polish

The architect recommendation after logging was:

Help/About/Release Notes next
because it is low-risk and improves the demo/product story

3. Logging usage validation

## 3A. COMPLETED PROMPT HISTORY

Below is the recent Claude Code / architect-guided implementation history that should be treated as part of current project context.

### CC-ERATE-000018 — Establish local validation script and initial GitHub Actions CI foundation
**Outcome**
- Added local bash-driven validation/run workflow
- Added initial GitHub Actions CI pipeline
- Established repo CI baseline

**Key results**
- Local script for restore/build/test/run
- Initial CI workflow for push + pull_request
- Foundation designed to grow into a DevSecOps pipeline

---

### CC-ERATE-000019 — Harden local dev script and make app startup deterministic
**Outcome**
- Reworked local script behavior for explicit modes
- Added deterministic app startup and health checking

**Key results**
- `dev-run.sh` supports:
  - default run mode
  - `--validate`
  - `--start-for-tests`
- App binds to a known port
- Added `GET /health`
- Safer stop behavior (SIGTERM → SIGKILL fallback)

---

### CC-ERATE-000020 — Add minimal Playwright UI smoke automation
**Outcome**
- Added Playwright-based UI smoke tests in C#
- Added CI `ui-smoke` stage

**Key results**
- New `ErateWorkbench.UITests` project
- Small happy-path browser suite
- Local UI test runner script
- CI browser automation stage added after core validation

---

### CC-ERATE-000021 — Add initial security stage to CI
**Outcome**
- Added dependency vulnerability scanning
- Added Dependabot hygiene baseline

**Key results**
- CI `security` job using `dotnet list package --vulnerable`
- Direct vulnerable deps fail; transitive surfaced with less noise
- Added `dependabot.yml`
- Updated xUnit-related dependencies to eliminate discovered CVEs

---

### CC-ERATE-000022 — Make UI smoke tests resilient to actual rendered page content
**Outcome**
- Hardened brittle History page smoke test

**Key results**
- Replaced incorrect title assertion
- Used more stable semantic heading-based assertion
- Improved UI smoke reliability without changing application behavior

---

### CC-ERATE-000023 — Add secrets scanning stage to CI
**Outcome**
- Added secrets scanning to pipeline

**Key results**
- Added `gitleaks`-based `secrets-scan` job
- Added minimal `.gitleaks.toml`
- Full history scanning enabled in CI
- Established second meaningful security control after dependency scanning

---

### CC-ERATE-000024 — Document local workflow, CI pipeline, and DevSecOps operating model
**Outcome**
- Added clear operating documentation for the repo

**Key results**
- Reworked `README.md`
- Added:
  - `docs/devops/pipeline.md`
  - `docs/devops/local-workflow.md`
- Documented local commands, CI stages, and Dependabot handling model

---

### CC-ERATE-000025 — Add publish/artifact stage to CI
**Outcome**
- Added build artifact publishing to pipeline

**Key results**
- New `publish` job in GitHub Actions
- Published self-contained `linux-x64` API artifact
- Artifact uploaded as GitHub workflow output
- Documentation updated to reflect artifact stage

---

### CC-ERATE-000026 — Investigate and improve Analytics page performance
**Outcome**
- Added in-memory caching to expensive Analytics page queries

**Key results**
- Registered `AddMemoryCache()`
- Cached 6 expensive analytics queries with 24-hour absolute expiration
- Left import summary uncached because it is already fast and more live-state oriented
- Warm-path performance improved from ~2.1s to ~10ms
- Cold-path remained ~17.5s, which was accepted for this demo/static-data scenario

**Architectural note**
- `Task.WhenAll()` was intentionally not used because the queries share the same scoped EF Core `AppDbContext`, and concurrent async operations on the same context would throw `InvalidOperationException`

---

### CC-ERATE-000027 — Add structured logging and observability baseline
**Outcome**
- Added practical local logging/observability improvements

**Key results**
- Built-in `Microsoft.Extensions.Logging` + `SimpleConsole`
- Timestamps, levels, categories, single-line formatting
- Log-level defaults tuned:
  - app logs visible
  - framework/EF noise reduced by default
- Added Analytics timing/cache hit-miss logs
- `dev-run.sh` now tees logs to `/tmp/erate-workbench-app.log`
- Added `docs/devops/logging.md`

---

## 3B. CURRENT RECOMMENDED NEXT WORK

The following items were explicitly discussed as likely next priorities after the logging work.

### Priority order recommended by architect

#### 1. Help / About / Release Notes navigation
**Why this is next**
- Low-risk feature
- Strong product/demo storytelling value
- Connects the app to GitHub wiki and release history
- Good polish without destabilizing core platform work

**Desired scope**
- Add Help icon or Help entry in navigation
- Add About page that links to the GitHub wiki
- Add Release Notes page that links to GitHub release notes
- Keep implementation simple and integrated with current site layout

---

#### 2. UI theme / rebranding polish
**Why**
- The platform is now operationally much stronger
- Visual refinement will now land better because the product performs well and has stronger engineering credibility

**Ideas already discussed**
- Better footer presentation
- Improved palette/color combinations
- Sharper overall UI theme
- Keep it tasteful and avoid a large design rabbit hole

---

#### 3. Further logging / observability refinement
**Why**
- Useful after the new baseline is in place
- Could improve local diagnostics and performance analysis further

**Possible follow-up directions**
- more targeted timing logs
- request/endpoint timing refinement
- optional richer local file logging behavior
- better documented local log workflow

---

#### 4. Release / artifact / packaging polish
**Why**
- The publish stage now exists
- A future step could formalize the release story further

**Possible follow-up directions**
- release notes integration
- versioning polish around published artifact
- clearer artifact consumption path
- future deployment design, if desired

---

## 3C. ITEMS THE USER EXPLICITLY IDENTIFIED AS IMPORTANT

The user raised the following as active areas of interest, in this order of discussion:

1. **Logging quality and local log usability**
   - timestamps
   - log levels
   - filtering
   - manageable size
   - good local viewing experience

2. **Analytics page performance**
   - this was implemented in CC-ERATE-000026 and is considered fixed for current demo use

3. **Rebranding and style changes**
   - sharper UI theme
   - improved footer
   - more intentional palette choices

4. **Help icon + About page + Release Notes page**
   - About should link to GitHub wiki
   - Release Notes should map back to GitHub release notes

At this point, the best architect recommendation was:
- **do Help/About/Release Notes next**
- then consider UI/theme polish

---

## 3D. WORKFLOW / PROCESS DISCIPLINE TO PRESERVE

A major part of recent progress has been enforcing clean Git/GitHub workflow discipline. This should be treated as part of architectural context, not just incidental process.

### Standard workflow loop
Every new task should follow this pattern:

```text
main synced → create feature branch → implement → commit → push → open PR → wait for CI → merge → delete branch → sync main

4. Dependabot governance

The repo now has a working maintenance pattern, but there are ongoing decisions about:

which Dependabot PRs are safe to merge routinely

which should be deferred as major upgrade work

Current policy established:

GitHub Actions updates: usually safe after green CI

test/tooling updates: one at a time after green CI

major runtime/data-layer upgrades: deliberate engineering work, not routine merges

5. Release/deploy direction

The pipeline now publishes artifacts, but no deployment stage exists yet. A future decision will be needed on:

whether to add release workflow only

whether to add actual deployment automation

whether to keep it artifact-only for the POC

4. KNOWN TECHNICAL DEBT

Below is the current debt picture combining original app debt plus new platform/developer-workflow debt.

TD-001 — HttpClient default timeout on long imports

What was deferred
Long-running import operations can still hit default client timeout behavior.

Why deferred
Data correctness and repair work were higher priority.

Risk
Medium

Recommended path
Increase/import-client timeout explicitly in DI and validate long-running import behavior.

TD-002 — Import observability and progress reporting remain weak

What was deferred

reliable import progress updates

RecordsProcessed fidelity during failures

better job lifecycle reporting

Why deferred
Validation and data correctness mattered more than import UX/observability.

Risk
Medium

Recommended path
Add batch-level progress persistence, better terminal state handling, and clearer /imports health semantics.

TD-003 — No true year-scoped import behavior

What was deferred
Import endpoints still do not support true year-scoped re-import.

Why deferred
Full imports are acceptable for current POC use, and data confidence is already established.

Risk
Low to Medium

Recommended path
If targeted repair becomes important again, design a real year-scoped import path.

TD-004 — Summary rebuild order is manual discipline

What was deferred
Risk summary still depends on commitment/disbursement summary rebuild order, with no enforced orchestration.

Why deferred
Acceptable for a single-operator POC.

Risk
Low to Medium

Recommended path
Add an ordered rebuild endpoint or equivalent orchestration.

TD-005 — Some analytics still conceptually rely on expensive query patterns

What was deferred
The Analytics page is now warm-path fast due to caching, but cold-path performance remains slow and the underlying expensive query shape still exists.

Why deferred
Caching provided the highest-value fix with minimal complexity.

Risk
Low for demo use, Medium if data freshness or frequent restarts become important

Recommended path
If needed later, move the heaviest analytics slices to precomputed/materialized summary paths rather than raw aggregations.

TD-006 — Logging is practical but still local/dev oriented

What was deferred

centralized log aggregation

richer structured fields/enrichment

rolling app-managed file sink

production-style observability

Why deferred
Local developer diagnostics were the immediate need.

Risk
Low for POC

Recommended path
Only add more if a stronger operational story is needed; otherwise keep it light.

TD-007 — Playwright local setup requires WSL browser dependencies

What was deferred
Local Playwright use in WSL depends on system packages being installed.

Why deferred
CI path works; local fix is straightforward and documented.

Risk
Low

Recommended path
Potential future bootstrap/setup script for local environment prerequisites.

TD-008 — Dependabot PR queue management can be noisy/confusing

What was deferred
No special automation exists to manage or group PRs beyond current Dependabot config and human triage.

Why deferred
Current manual governance is acceptable.

Risk
Low

Recommended path
Keep queue small, close deliberate-deferral PRs, and avoid over-automating unless it becomes a recurring pain point.

TD-009 — Artifact stage exists, but no release/deploy consumption yet

What was deferred
Artifact publishing is in place, but there is no downstream release/deploy automation.

Why deferred
Artifact generation was the correct next maturity step before release/deploy complexity.

Risk
Low

Recommended path
Add release workflow or deployment path only when there is a clear target environment/story.

TD-010 — UI/theme polish remains behind engineering maturity

What was deferred
The app is now much stronger operationally than visually in some places.

Why deferred
Performance, pipeline, and operational maturity had higher value.

Risk
Low

Recommended path
Do theme/rebranding/footer polish after the next low-risk product/storytelling work, not before.

TD-011 — Help/About/Release Notes navigation not yet implemented

What was deferred
The user wanted:

Help icon in navigation

About page linking to GitHub wiki

Release Notes page linking to GitHub release notes

Why deferred
More foundational engineering/platform work took precedence.

Risk
Low

Recommended path
Likely the best next feature/storytelling task after logging is merged.