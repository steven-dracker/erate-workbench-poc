
ERATE Workbench POC — Architect Handoff 3-18-2026 9:30 PM ET
1. Project Overview
What we are building and why

ERATE Workbench POC is a C# / ASP.NET Core analytics and workflow platform built around USAC E-Rate open data. It began as interview preparation and evolved into a credible mini-platform demonstrating:

data engineering

analytics engineering

backend architecture

product thinking

quality / validation discipline

The core product thesis is:

Most E-Rate dashboards stop at requested or committed dollars.
ERATE Workbench aims to show where execution breaks down, where advisors should focus, and how to reason about the E-Rate lifecycle operationally.

The app now includes both analytics surfaces and reference/product surfaces:

Dashboard / Search / Analytics

Risk Insights

Program Workflow

Ecosystem

History

It also includes a quality system with reconciliation, runbooks, evidence logs, regression strategy, and test inventory.

Core technical stack and why it was chosen

Language / Runtime

C# / .NET 8
Chosen for strong typing, mature tooling, performance, and relevance to senior/architect-level discussions.

Web framework

ASP.NET Core + Razor Pages
Chosen for fast full-stack iteration, simple routing, and production-style structure without frontend complexity.

Database

SQLite
Chosen for local portability, zero infrastructure burden, easy demo setup, and fast iteration.

ORM / data access

Entity Framework Core
Chosen for maintainable schema evolution, typed querying, and migrations.

Testing

xUnit
Used for extensive automated coverage; current status is 347/347 tests passing.

Source ingestion

Socrata HTTP + CSV parsing via CsvHelper
Supports paginated ingestion, retries, import resilience, and large public datasets.

Environment

Windows 11 + WSL2 Ubuntu 24.04 + VS Code Remote WSL
Chosen for practical .NET development, Linux tooling, and easy local operations.

2. Architectural Decisions

Below are the major decisions in effect as of this handoff.

Decision 1 — Use a layered .NET architecture

What
Projects are split into:

ErateWorkbench.Api

ErateWorkbench.Domain

ErateWorkbench.Infrastructure

ErateWorkbench.Tests

Why

separation of concerns

testability

clearer architectural story

maintainability

Rejected alternatives

single-project MVC app

heavy DDD-style overhead for a POC

Assumptions

The artifact should feel like a real platform, not a throwaway demo.

Decision 2 — Use SQLite as the primary datastore

What
SQLite is the local database used for imports, summaries, analytics, and validation workflows.

Why

zero infrastructure

portable demo setup

fast iteration

adequate for POC with summary-layer architecture

Rejected alternatives

Postgres / SQL Server

file-only analytics without a database

Assumptions

Local/demo-first operation is the primary mode.

Decision 3 — Ingest real USAC open datasets, not mock data

What
The system imports real USAC open data, primarily:

Funding Commitments

Disbursements

entity/supporting datasets

Why

stronger credibility

realistic domain behavior

enables true reconciliation and caveat surfacing

Rejected alternatives

seed/demo data

small static CSV extracts only

Assumptions

Public data inconsistencies are acceptable and analytically valuable.

Decision 4 — Use paged/streaming ingestion with idempotent upserts

What
Import services page source data and upsert into SQLite using a stable raw key.

Why

handles large source volume

avoids full in-memory loads

safe reruns

supports partial failure without full rollback

Rejected alternatives

full download then parse

truncate/reload every time

naive insert-only import

Assumptions

repeated imports during development are normal

source keys are stable enough for idempotent behavior

Decision 5 — Make analytics run off summary tables, not raw tables

What
Core analytics use:

ApplicantYearCommitmentSummary

ApplicantYearDisbursementSummary

ApplicantYearRiskSummary

Why

performance

consistency

easier debugging

easier reconciliation

simpler repository logic

Rejected alternatives

raw-table aggregations everywhere

SQL views only

caching before fixing data model

Assumptions

analytics should be driven by curated aggregates, not transactional/raw tables

Decision 6 — Build Risk Insights as a dedicated product area

What
Risk Insights is a top-level area rather than a subsection of generic analytics.

Why

execution risk is a distinct product capability

aligns to advisory use cases

demonstrates product thinking beyond charts

Rejected alternatives

fold into generic analytics

treat as just another chart section

Assumptions

interviewers and users care about operational insight, not just totals

Decision 7 — Keep semantic honesty explicit in UI

What
UI language explicitly frames cross-dataset comparisons honestly:

“Approved Invoice Amount”

“Invoice / Commitment Ratio”

partial-year caution

advisory signals as prioritization aids, not definitive judgments

Why

source datasets are not strictly like-for-like

misleading “clean” dashboard semantics would be false

Rejected alternatives

hide >100% values

force “completion” framing

suppress caveats

Assumptions

trustworthiness matters more than neat visuals

Decision 8 — Use reconciliation as a first-class validation feature

What
Built reconciliation against Socrata source aggregates comparing:

source vs local raw

source vs summary

raw vs summary

Why

summary layers should not be trusted blindly

catches source-mapping and completeness issues

supports confidence before demos

Rejected alternatives

manual SQL spot checks only

trusting imports without source validation

Assumptions

aggregate source validation is enough for directional trust

Decision 9 — Add year-scoped summary rebuilds and validation workflows

What
Summary rebuilds and reconciliation support ?year=YYYY, and runbooks/logs are organized by year.

Why

better debugging

targeted validation

lower blast radius

Rejected alternatives

always full-dataset rebuild/validation with no year slicing

Assumptions

year-level operational checks are a useful unit of work

Important nuance learned later

summary/reconciliation paths became year-oriented, but import behavior was not truly year-scoped in the way originally assumed.

Decision 10 — Keep static explanatory/reference pages inside the app

What
Added app-integrated reference pages:

Program Workflow

Ecosystem

History

Why

strengthens narrative and domain explanation

helps both technical and non-technical demo audiences

turns standalone artifacts into productized content

Rejected alternatives

keep them as external docs only

leave them as disconnected static HTML

Assumptions

reference content belongs in-product if it supports the story and user understanding

Decision 11 — Convert static reference pages to shared-layout Razor Pages

What
Ecosystem and History were initially served as static HTML via MapGet(... Results.File(...)), then converted to Razor Pages using the shared _Layout.cshtml.

Why

consistent shared navigation

no duplicated nav/header maintenance

cleaner long-term integration

Rejected alternatives

keep separate standalone static nav forever

Assumptions

shared layout consistency is worth a small conversion effort

Decision 12 — Build a formal quality system, not just tests

What
Created docs/quality/ with:

taxonomy

regression strategy

lifecycle

test inventory

runbooks

yearly quality evidence log

test suite audit

Why

separate manual smoke checks from regression tests, data validation, semantics, and future security/performance work

make quality durable and extensible

Rejected alternatives

ad hoc QA notes

only automated tests with no manual evidence trail

Assumptions

this POC should demonstrate engineering discipline, not just functionality

Decision 13 — Adopt prompt traceability IDs for Claude Code work

What
Claude prompts use IDs like CC-ERATE-0000XX, and outputs/commits reference them.

Why

traceability

clean mapping from task → implementation → validation

easier multi-AI workflow management

Rejected alternatives

untracked prompting

generic commit messages with no task linkage

Assumptions

lightweight traceability is worth the small process overhead

3. Active Work & Open Items
What is currently in progress

Current state of the project:

2020–present data has been imported and validated sufficiently for demo confidence

Ecosystem page has been integrated

History page has been integrated

Both are now shared-layout Razor Pages

Nullable warning in FundingCommitmentCsvParser was fixed

Build is clean

Tests are green

Current repo status at handoff:

local main is ahead of origin/main by 13 commits

working tree is clean

stale local branches were cleaned up

current local branches:

main

feature/import-resilience

What has been handed to Claude Code most recently

Recent Claude work included:

CC-ERATE-000011

Update yearly quality log with FY2021 full import validation context

Commit: 16ac22a

CC-ERATE-000012

Integrate Ecosystem page initially as static route

Commit: a956cc4

CC-ERATE-000013

Integrate History page initially as static route

Commit: 8939e03

CC-ERATE-000014

Convert Ecosystem and History to shared-layout Razor Pages

Remove MapGet routes

Use _Layout.cshtml shared nav

Commit: d985bfe

CC-ERATE-000015

Fix nullable warning in FundingCommitmentCsvParser

One-character null-conditional fix

0 warnings / 347 tests passing

Commit: 0d73c64

Current confirmed data-validation conclusions

The project owner wanted confidence specifically for 2020–present data.

Final checks showed:

Funding Commitments local validation

2020–2025 row counts are internally consistent

2026 is lower as expected for a partial year

distinct applicants are stable across mature years

committed/eligible amounts are in consistent year-over-year ranges

FY2021 was investigated deeply and is now considered valid for demo use

Important lesson learned:

Source/raw exact parity is not expected because the source model is ROS-expanded and the local model is normalized/deduplicated.

Reconciliation is directional and structural, not row-for-row equality.

Import-job system

Import job status tracking was found to be unreliable

multiple stale jobs were marked status = 1 even though no true work was ongoing

stale jobs were manually cleared in SQLite by setting them to failed/completed state

/imports is now trustworthy again, but this remains technical debt

Open questions / pending decisions

Security pipeline

The next likely major work item

Repo is private

GitHub Pro limitations mean:

Dependabot works

code-scanning/SARIF Security-tab workflows do not behave like public repos

Snyk Free has been installed/evaluated and may be the best fit for private repo scanning

Git/GitHub / GitLab workflow

User indicated desire to commit and merge through remote workflow rather than keep working directly on main

Current recommendation is to move to short-lived feature branches for future Claude tasks

Import observability

Current data is trusted, but the import job UX is weak

no trustworthy progress reporting

no reliable records processed metrics

stale job states required manual cleanup

Performance

Potential watchpoints still exist:

Risk Insights gap sorting previously loaded full result sets into memory

Analytics page historically used raw-table grouping

No major blocking issue was established in this session, but performance hardening remains a valid next step

Security / quality domain expansion

docs/quality/ was designed to support:

smoke tests

regression tests

data validation

semantic review

security

performance

security and performance are still early compared to validation and regression work

4. Known Technical Debt
1. Import job lifecycle / observability is incomplete

What was deferred

reliable progress tracking

heartbeat/last-updated state

cancellation support

auto-cleanup of stale jobs

meaningful recordsProcessed during long imports

Why deferred

data correctness and validation mattered more immediately than import UX

Risk

Medium

Recommended path

add import progress tracking

add explicit job terminal states

add stale-job detection and/or cancellation

surface trustworthy progress in the UI or API

2. Import endpoint year parameter semantics are misleading

What was deferred

true year-scoped import behavior

Why deferred

full 2020–present data is now imported once and validated

the immediate need for targeted repair/import was removed after data confidence was established

Risk

Medium

Recommended path

either implement true year-scoped import

or remove misleading year-scoped import expectations from API semantics and documentation

3. Reconciliation is a dev/admin tool, not productized UI

What was deferred

end-user-facing data-health/admin experience

Why deferred

validation trust was the priority, not surfacing ops tooling

Risk

Low

Recommended path

add lightweight admin/data-health page later

4. Summary rebuilds are rebuild-oriented, not incremental

What was deferred

incremental summary refresh

summary job metadata

resumability/failure metadata

Why deferred

delete/rebuild by year is simpler and more trustworthy for POC validation

Risk

Low to Medium

Recommended path

keep current design unless this becomes a more production-like system

5. Canonical applicant/entity dimension still does not exist

What was deferred

explicit dimension model for canonical applicant/entity identity

Why deferred

current risk-focused path was able to proceed without it

Risk

Medium

Recommended path

introduce later if name drift, join complexity, or multi-year identity modeling starts to hurt

6. Some analytics paths may still deserve further summary-layer hardening

What was deferred

complete audit/refactor of every remaining analytics query path

Why deferred

Risk Insights and the core validation path were higher priority

Risk

Medium

Recommended path

audit remaining raw-table analytics paths and materialize additional summary tables only where clearly justified

7. Security pipeline is not fully established yet

What was deferred

final private-repo-compatible CI scanning baseline

Why deferred

focus was on data trust, validation, and product integration first

Risk

Medium

Recommended path

establish private-repo-safe security baseline with:

Dependabot

Snyk

secrets scanning in CI

branch protection

8. Performance guardrails are not yet formalized

What was deferred

structured performance baselines or automated performance checks

Why deferred

data correctness and product completeness were higher priority

Risk

Medium

Recommended path

add lightweight timing benchmarks for key pages

document acceptable page load ranges

move performance watchpoints into docs/quality/

9. Original source HTML files for Ecosystem and History are now archival artifacts

What was deferred

deciding whether to keep or remove wwwroot/erate_ecosystem.html and wwwroot/erate_timeline.html

Why deferred

they are harmless and preserve source artifacts

current priority was integrating the pages into shared layout

Risk

Low

Recommended path

keep if they are useful as source artifacts; otherwise remove later once comfortable with Razor-page versions

5. Context Restore Instructions
One-paragraph opening prompt for a new ChatGPT session

Paste this into a new session:

You are my project architect for ERATE Workbench POC, a .NET 8 / ASP.NET Core / SQLite analytics and workflow platform built around USAC E-Rate open data. The system now includes real-source ingestion, reconciliation, summary-layer analytics (ApplicantYearCommitmentSummary, ApplicantYearDisbursementSummary, ApplicantYearRiskSummary), Risk Insights, and a formal quality system under docs/quality/ with taxonomy, regression strategy, runbooks, inventory, audit logs, and yearly validation evidence. 2020–present data has been imported and validated to a demo-safe level, FY2021 was investigated and is considered valid, Ecosystem and History pages were integrated as shared-layout Razor Pages, and the build is clean with 347/347 tests passing. Your role is to continue as technical architect: preserve architectural coherence, guide next decisions, generate Claude Code prompts with CC-ERATE-xxxxxx IDs, and help me move next into the highest-value work such as security pipeline, performance hardening, or additional product polish without re-litigating already validated ingestion work.