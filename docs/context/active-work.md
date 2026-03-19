# Active Work — ERATE Workbench POC

_Last updated: 2026-03-18_

---

## Current state summary

All CC-ERATE-000009 through CC-ERATE-000016 work items are **complete and committed**. No work items are currently in flight. The branch is `feature/import-resilience`, ahead of `main` by all commits since the analytics refactor merge.

---

## Completed this session (2026-03-18)

| Prompt ID | Description | Commit | Status |
|---|---|---|---|
| CC-ERATE-000009 | Fix FundingCommitments SoQL column names; fix 3 failing tests | `3897db2` | ✅ Complete |
| CC-ERATE-000010 | FY2021 FC repair: full idempotent re-import (3h41m, 19.7M rows) | (background job) | ✅ Complete |
| CC-ERATE-000010D | Root-cause investigation: killed imports, no year-filter, offset ordering | `fb1ec29` | ✅ Complete |
| CC-ERATE-000011 | FY2021 quality log update: row confirmed at 171,977, Raw→Sum ~$0 | `16ac22a` | ✅ Complete |
| CC-ERATE-000012 | Add Ecosystem page at /ecosystem | `a956cc4` | ✅ Complete (superseded by 000014) |
| CC-ERATE-000013 | Add History page at /history | `8939e03` | ✅ Complete (superseded by 000014) |
| CC-ERATE-000014 | Convert Ecosystem + History to shared-layout Razor Pages | `d985bfe` | ✅ Complete |
| CC-ERATE-000015 | Fix CS8602 nullable warning in FundingCommitmentCsvParser | `0d73c64` | ✅ Complete |
| CC-ERATE-000016 | Add reconciliation/validation report artifacts | `9a54fcd` | ✅ Complete |

---

## Post-import validation: FY2021 (completed)

All post-import steps executed and confirmed:

- `POST /dev/summary/funding-commitments?year=2021` → 171,977 raw rows → 19,637 summary rows ✅
- `POST /dev/summary/risk?year=2021` → 21,199 risk rows ✅
- `POST /dev/reconcile/funding-commitments` → Source/Raw ratio 12.3× (within 10–15× expected) ✅
- Raw→Summary amount variance: ~$0 (<0.001%) ✅ (was −9.6% before repair)

---

## Known open items (not blocking)

### 1. HttpClient timeout on long imports
**What:** The default `HttpClient.Timeout` of 100 seconds caused job 46 (the FY2021 repair import) to record `status=Failed` despite successfully writing all data. The timeout hit the final Socrata page fetch after ~3.7 hours.

**Risk:** Medium. Future full re-imports will face the same timeout unless configured.

**Next step:** Set a longer timeout on the `UsacCsvClient` HTTP client in DI registration (e.g., `Timeout = TimeSpan.FromMinutes(5)`). One-line fix in `Program.cs`.

### 2. No year-scoped import capability
**What:** Import services always page the full Socrata dataset. `?year=YYYY` on import endpoints is silently ignored.

**Risk:** Low for POC. A partial-year fix (e.g., re-import only FY2021) requires a full 3.5-hour import.

**Next step:** For production, implement a Socrata `$filter=funding_year=YYYY` page-scan import path.

### 3. Full validation cycle not yet run
**What:** The full `full-data-validation-runbook.md` (all 8 steps including UI smoke test) has not been run to completion.

**Status:** FY2022 is the validated reference year. All other years are `validated-caveat` (row counts and reconciliation pass; no full smoke test completed).

**Next step:** Run the full runbook against a fresh app instance before any stakeholder demo.

### 4. Summary rebuild order is a manual discipline
**What:** Risk summary must be rebuilt AFTER commitment and disbursement summaries. No enforcement exists.

**Risk:** Stale input produces misleading Raw→Summary variance. Documented in watchlist.

**Next step:** Either add an ordered rebuild endpoint (`POST /dev/rebuild-all`) or document it more prominently in the runbook.

### 5. DIAGNOSTIC logging still active in FundingCommitmentCsvParser
**What:** `[DIAG]` log lines (first 5 rows + parse summary) still emit as `LogWarning` in production. Added for debugging during import development.

**Risk:** Low. Noisy logs during imports.

**Next step:** Remove or gate behind a debug flag / `LogDebug` level.

---

## Data state (as of 2026-03-18)

### FundingCommitments (avi8-svp9)

| Year | Raw rows | Source rows | Ratio | Status |
|---:|---:|---:|---:|---|
| 2016 | 264,553 | 2,084,840 | 7.9× | `validated-caveat` |
| 2017 | 196,851 | 1,694,752 | 8.6× | `validated-caveat` |
| 2018 | 169,179 | 1,639,720 | 9.7× | `validated-caveat` |
| 2019 | 183,345 | 1,439,485 | 7.8× | `validated-caveat` |
| 2020 | 250,037 | 1,702,938 | 6.8× | `validated-caveat` |
| 2021 | **171,977** | 2,116,248 | **12.3×** | `pass` (repaired 2026-03-18) |
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

## Current navigation structure

| Page | Route | Type |
|---|---|---|
| Dashboard | `/` | Razor Page |
| School & Library Search | `/Search` | Razor Page |
| Analytics | `/Analytics` | Razor Page |
| Program Workflow | `/ProgramWorkflow` | Razor Page |
| Advisor Playbook | `/AdvisorPlaybook` | Razor Page |
| Risk Insights | `/RiskInsights` | Razor Page |
| Ecosystem | `/Ecosystem` | Razor Page (converted CC-ERATE-000014) |
| History | `/History` | Razor Page (converted CC-ERATE-000014) |

---
