# Test Taxonomy

Defines every category of quality check used in E-Rate Workbench, when each is
appropriate, and how each one maps to this project's architecture and maturity level.
This taxonomy is the authoritative source for the `type` column in `test-inventory.md`.

---

## 1. Unit Tests

**What:** Automated tests of a single class or method in complete isolation.
All external dependencies (database, HTTP, file system) are replaced by
in-memory fakes or hand-crafted stubs passed as constructor arguments.

**When appropriate:**
- Pure logic with no I/O — parsers, calculators, URL builders, formatters
- Logic that is hard to exercise through higher-level tests (edge cases,
  null inputs, boundary values)
- Any logic where you want instant feedback on a laptop with no network

**Project examples:**
- `FundingCommitmentCsvParser` — header mapping, null FRN skipping,
  `RawSourceKey` construction from `{FRN}-{form_471_line_item_number}`
- `RiskCalculator.ComputeRiskScore` and `ClassifyRisk` — score formula
  `0.5 × ReductionPct + 0.5 × (1 − DisbursementPct)`, clamped to [0, 1];
  thresholds High > 0.6, Moderate 0.3–0.6, Low < 0.3
- `SocrataReconciliationService.BuildTotalCountUrl` / `BuildByYearUrl` —
  Socrata simple-filter format (`&column=value`, never `$where`)
- `ReconciliationReportWriter.BuildMarkdown` — three-layer vs two-layer
  table detection; year scope line in header

**Runner:** `dotnet test`
**Location:** `tests/ErateWorkbench.Tests/`

---

## 2. Integration Tests

**What:** Automated tests that exercise two or more layers together against
a real (in-memory) database. This project's xUnit suite sits mostly in this
category: most tests instantiate `AppDbContext` with an in-memory SQLite
provider and call actual EF Core queries.

**When appropriate:**
- Upsert and deduplication behavior (requires real DB constraint enforcement)
- Summary builder output — the builders load from DB, group in memory, and
  write back; testing them end-to-end catches grouping and aggregation bugs
- Reconciliation model wiring — provider → service → result shape
- EF Core query translation — queries that look correct in code can fail
  at the EF→SQLite translation layer

**Project examples:**
- `FundingCommitmentRepositoryTests` — insert + re-insert by `RawSourceKey`;
  confirms unique constraint prevents duplicates and counts insert vs update
- `DisbursementSummaryBuilderTests` — seeds raw Disbursements rows, calls
  `RebuildAsync`, asserts aggregated amounts and `ApprovedAmount > 0` exclusion
- `RiskSummaryBuilderTests` — seeds both commitment and disbursement summaries,
  calls `RebuildAsync`, asserts full-outer-join merge: matched rows,
  commitment-only rows (DisbursementPct=0), disbursement-only rows (Score=0.5)
- `RiskInsightsRepositoryTests` — queries advisory signals against seeded data

**Runner:** `dotnet test`
**Location:** `tests/ErateWorkbench.Tests/`

**Current gap:** No end-to-end HTTP integration tests (no `WebApplicationFactory`
usage). The API layer is tested manually via Swagger and smoke checks.

---

## 3. Smoke Tests

**What:** A short, manual checklist that confirms the application is alive and
its most visible paths work. Completable in under five minutes.

**When appropriate:**
- After any app restart or deploy
- Before sharing a demo or beginning a data validation cycle
- After upgrading a NuGet dependency or changing `Program.cs` wiring

**Scope:** Does the app start? Do pages load? Do the most-used endpoints
return 200? No JS errors on the analytics pages?

**Procedure:** `runbooks/smoke-test-runbook.md`

---

## 4. Regression Tests

**What:** Automated checks that a known-good output has not changed
unexpectedly. The baseline is a committed artifact; a regression test
fails when current output diverges beyond a defined tolerance.

**When appropriate:**
- After confirming a behavior is correct, to protect it from future changes
- When a bug is fixed — the fix should become a regression test so the bug
  cannot silently return
- For business rules whose violation would not be obvious from unit tests
  alone (e.g., `ApprovedAmount > 0` inclusion, full-outer-join merge semantics)

**Status:** Formal regression baselines are not yet implemented.
Tests that protect specific bug fixes already exist as unit/integration tests
and should be tagged as regression tests in the inventory.

See `regression-strategy.md` for the full policy.

---

## 5. Data Validation Tests

**What:** Checks that compare the local SQLite database against the
authoritative Socrata source. These are manual-or-scripted checks that
produce evidence recorded in `evidence/yearly-quality-log.md`.

**When appropriate:**
- After any full or year-scoped data reload
- Before presenting analytics to external stakeholders
- When investigating anomalies flagged by Risk Insights

**This project's data validation is structured in three layers:**

```
Source (Socrata JSON API)
  ↓  import via CSV endpoint
Raw (local SQLite — FundingCommitments, Disbursements)
  ↓  summary builder (ApprovedAmount > 0 filter for disbursements)
Summary (ApplicantYearCommitmentSummary / ApplicantYearDisbursementSummary)
  ↓  risk builder (full outer join in memory)
Risk (ApplicantYearRiskSummary)
```

Validation checks span all transitions:
- **Source → Raw:** row counts and amounts should match (exact for Disbursements;
  large row-count variance expected for FundingCommitments due to ROS granularity)
- **Raw → Summary:** row count reduction expected; amount totals should be close
- **Summary → Risk:** coverage flags (`HasCommitmentData`, `HasDisbursementData`)
  should have plausible distributions for the year

**Procedure:** `runbooks/full-data-validation-runbook.md`
**Evidence:** `evidence/yearly-quality-log.md`

---

## 6. Semantic / Manual Review

**What:** Human judgment about whether output is correct, honest, and
interpretable. These checks cannot be automated because they require
reasoning about meaning, not just values.

**When appropriate:**
- Before any significant feature release that changes what users see
- When Risk Insights advisory signal logic changes (thresholds, signal types)
- When the risk scoring formula or level thresholds change
- When narrative text (disclaimers, labels, section headings) is modified

**Project examples:**
- Does the Risk Insights page accurately represent the data it is showing?
  (Does "High Reduction" mean what the label implies?)
- Do advisory signal labels — "No Commitment", "No Disbursement",
  "High Reduction", "Low Utilization" — accurately describe the underlying
  condition without overstating severity?
- Is the current/partial-year disclaimer visible and prominent enough on the
  Risk Insights page?
- Does the waterfall visualization correctly convey the Eligible → Committed
  → Approved progression?

**No automated tooling.** Outcomes are recorded in `evidence/` if material.

---

## 7. Operational Validation Scripts / Runbooks

**What:** Documented, repeatable procedures for operational checks that are
run by a person (or eventually a script) during a specific workflow, such as
a data reload or a production deployment. Distinct from smoke tests (which
confirm liveness) and data validation (which confirms correctness); runbooks
describe multi-step operational sequences.

**When appropriate:**
- Year-batch import sequences (import → rebuild summary → rebuild risk →
  run reconciliation → validate)
- Post-incident data remediation
- Environment setup and database initialization

**Project examples:**
- `runbooks/full-data-validation-runbook.md` — the year-by-year validation
  workflow; includes the year-scoped import/rebuild/reconcile discipline
- `runbooks/smoke-test-runbook.md` — the post-start liveness checklist

---

## 8. Security Tests

**What:** Checks for vulnerabilities: unauthenticated write endpoints,
injection (SQL, CSV, command), sensitive data exposure, missing input
validation at API boundaries.

**When appropriate:**
- Before any public-facing deployment
- After adding new endpoints or changing authentication logic
- Periodically as an operational security audit

**Status:** Not yet started. This is a POC with no public deployment.
When the application approaches production use, this category must be
addressed before external access is granted.

**Known risk areas:**
- `/dev/*` endpoints (import, reconcile, summary builders) are unauthenticated
  and perform destructive operations (delete + rebuild)
- Import endpoints accept arbitrary `year` parameters which flow into Socrata
  URL construction — the simple-filter pattern is safe, but this should be
  validated explicitly

---

## 9. Performance Tests

**What:** Checks that response times and resource consumption remain
acceptable as data volume grows.

**When appropriate:**
- Before scaling to multi-year full loads in a shared or production environment
- After significant changes to analytics queries or summary builder logic
- When adding new index-dependent queries

**Status:** Not yet started.

**Known risk areas:**
- `RiskInsightsRepository` loads summary rows into memory for in-memory grouping
  (avoids EF Core translation limits but does not scale to very large datasets)
- Summary builders load all raw rows for a given year into memory in one call

---

## Mapping summary

| Type | Automated? | Trigger | Evidence recorded? |
|---|---|---|---|
| Unit | Yes | Every commit | No (pass/fail in CI) |
| Integration | Yes | Every commit | No (pass/fail in CI) |
| Smoke | Manual | After start/deploy | Optional |
| Regression | Yes (planned) | Every commit | Baseline files in `evidence/baselines/` |
| Data validation | Manual/scripted | After data reload | Yes — `evidence/yearly-quality-log.md` |
| Semantic/manual review | Manual | Before major release | Yes if material |
| Operational runbook | Manual | During workflow | Yes — evidence log |
| Security | Planned | Before production | Yes |
| Performance | Planned | As needed | Yes |
