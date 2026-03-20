# Active Work — ERATE Workbench POC

_Last updated: 2026-03-19_

---

## Current state summary

All CC-ERATE-000018 through CC-ERATE-000027 work items are **complete and merged to `main`**. No work items are currently in flight. The repo is in a clean, stable state with a full CI pipeline, observability baseline, analytics caching, and complete DevOps documentation.

**Branch:** `main`
**Tests:** 347/347 passing
**Build:** Clean (0 errors, 1 pre-existing xUnit2013 analyzer warning in `ReconciliationTests.cs`)

---

## Completed since last context snapshot (2026-03-18)

| Prompt ID | Description | Commit | Branch | Status |
|---|---|---|---|---|
| CC-ERATE-000018 | Local validation script + initial GitHub Actions CI (build + test jobs) | `015e2e0` | — | ✅ Merged |
| CC-ERATE-000019 | Harden dev script: 3 modes, graceful stop, health poll, `/health` endpoint | `7d75afc` | — | ✅ Merged |
| CC-ERATE-000020 | Playwright UI smoke tests + `ui-smoke` CI job + `scripts/ui-test.sh` | `39bfed6` | — | ✅ Merged |
| CC-ERATE-000021 | Security CI job: NuGet vuln scan (2-tier) + Dependabot config | `24a1f22` | `feature/security-baseline` | ✅ Merged PR #1 |
| CC-ERATE-000022 | Fix History smoke test: title assertion used wrong keyword | `0441228` | `feature/ui-smoke-hardening` | ✅ Merged PR #10 |
| CC-ERATE-000023 | Secrets scanning CI job: gitleaks v8.30.0 CLI + `.gitleaks.toml` | `7b782f0` | `feature/secrets-scanning` | ✅ Merged PR #15 |
| CC-ERATE-000024 | DevOps documentation: README rewrite, `pipeline.md`, `local-workflow.md` | `3210a39` | `feature/pipeline-documentation` | ✅ Merged PR #16 |
| CC-ERATE-000025 | Publish CI job: self-contained linux-x64 artifact, 14-day retention | `f1f6e89` | `feature/build-artifacts` | ✅ Merged PR #17 |
| CC-ERATE-000026 | Analytics page caching: `IMemoryCache`, 24-hour expiry, ~200× warm improvement | `c404a20` | `feature/analytics-performance` | ✅ Merged PR #18 |
| CC-ERATE-000027 | Structured logging baseline: SimpleConsole timestamps, Analytics timing | `9beae64` | `feature/logging-observability` | ✅ Merged PR #19 |

**Dependabot merges (all CI-green):**
- `actions/checkout@v4 → v6`, `actions/setup-dotnet@v4 → v5`, `actions/upload-artifact@v4 → v7`
- `Microsoft.Playwright 1.49.0 → 1.58.0`
- `xunit 2.9.0 → 2.9.3`, `Microsoft.NET.Test.Sdk 17.8.0 → 18.3.0` (both test projects)

---

## Open items (not blocking)

### 1. HttpClient default timeout kills long imports (TD-001)
**Status:** Known. Not fixed.
**What:** `UsacCsvClient` uses the default 100-second HttpClient timeout. A full FundingCommitments import takes ~3.5–4 hours; a slow Socrata response on any page will abort the job.
**Fix:** One line in `Program.cs`:
```csharp
builder.Services.AddHttpClient<UsacCsvClient>(c => c.Timeout = TimeSpan.FromMinutes(5));
```

### 2. `[DIAG]` logging in FundingCommitmentCsvParser (TD-005)
**Status:** Known. Not fixed.
**What:** `LogWarning("[DIAG]...")` lines still emit for CSV headers and first 5 rows on every parsed page during imports. Now more visible since logging is improved.
**Fix:** Remove or demote to `LogDebug`.

### 3. No `POST /dev/rebuild-all` ordered endpoint (TD-004)
**Status:** Known. Not blocking for POC.
**What:** Risk summary must be rebuilt after commitment + disbursement summaries. No enforcement.

### 4. Analytics cache has no invalidation on import (ADR-011)
**Status:** Accepted for POC. Documented.
**What:** After running an import, Analytics page continues serving stale cached data for up to 24 hours.
**Workaround:** Restart the app. A `POST /admin/cache/clear` endpoint or `IHostedService` cache-busting hook could be added later.

### 5. Full validation cycle not yet run on current data state
**Status:** Deferred.
**What:** The `full-data-validation-runbook.md` has not been run to completion on the current database state. FY2022 remains the reference validated year; all others are `validated-caveat`.

---

## Current navigation structure

| Page | Route | Type |
|---|---|---|
| Dashboard | `/` | Razor Page |
| School & Library Search | `/Search` | Razor Page |
| Analytics | `/Analytics` | Razor Page (IMemoryCache, 24h expiry) |
| Program Workflow | `/ProgramWorkflow` | Razor Page |
| Advisor Playbook | `/AdvisorPlaybook` | Razor Page |
| Risk Insights | `/RiskInsights` | Razor Page |
| Ecosystem | `/Ecosystem` | Razor Page |
| History | `/History` | Razor Page |

---

## CI pipeline state

```
build → test → ui-smoke      (Playwright, headless Chromium, 5 tests)
             → security      (NuGet vuln scan, 2-tier)
             → secrets-scan  (gitleaks v8.30.0, full history)
                    └──────── publish  (self-contained linux-x64 artifact, 14-day retention)
```

All jobs passing on `main`. Dependabot active (weekly NuGet + GitHub Actions, 5-PR limit).

---

## Data state (as of 2026-03-18 — unchanged since last snapshot)

### FundingCommitments (avi8-svp9)

| Year | Raw rows | Source rows | Ratio | Status |
|---:|---:|---:|---:|---|
| 2016 | 264,553 | 2,084,840 | 7.9× | `validated-caveat` |
| 2017 | 196,851 | 1,694,752 | 8.6× | `validated-caveat` |
| 2018 | 169,179 | 1,639,720 | 9.7× | `validated-caveat` |
| 2019 | 183,345 | 1,439,485 | 7.8× | `validated-caveat` |
| 2020 | 250,037 | 1,702,938 | 6.8× | `validated-caveat` |
| 2021 | 171,977 | 2,116,248 | 12.3× | `pass` (repaired 2026-03-18) |
| 2022 | 169,458 | 2,185,316 | 12.9× | `validated` (reference year) |
| 2023 | 155,537 | 2,369,338 | 15.2× | `validated-caveat` |
| 2024 | 157,964 | 2,004,155 | 12.7× | `validated-caveat` |
| 2025 | 163,057 | 1,977,465 | 12.1× | `validated-caveat` |
| 2026 | ~67,000 | 557,976 | 8.4× | `partial` |

### Disbursements (jpiu-tj8h)

| Year | Raw rows | Status |
|---:|---:|---|
| 2020 | ~279,000 | `validated-caveat` |
| 2021 | ~274,000 | `validated-caveat` |
| 2022 | 274,905 | `validated` (reference year) |
| 2023 | ~266,000 | `validated-caveat` |
| 2024 | ~270,000 | `validated-caveat` |
| 2025 | ~142,000 | `partial` |

---
