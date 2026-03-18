# Quality Documentation

This directory contains the quality system documentation for E-Rate Workbench.
It is intended to grow alongside the codebase and serve as the authoritative record
of what has been tested, how quality decisions were made, and what evidence exists.

## Directory layout

```
docs/quality/
├── README.md                          ← this file
├── test-inventory.md                  ← catalogue of all checks and tests
│
├── strategy/
│   ├── test-taxonomy.md               ← definitions and categories of test types
│   ├── regression-strategy.md         ← policy for regression coverage decisions
│   └── test-lifecycle.md              ← how tests are added, maintained, superseded
│
├── runbooks/
│   ├── smoke-test-runbook.md          ← step-by-step post-deploy smoke check
│   └── full-data-validation-runbook.md ← year-by-year data validation procedure
│
└── evidence/
    └── yearly-quality-log.md          ← dated log of validation runs and outcomes
```

## Document types

| Type | Purpose | Lives in |
|---|---|---|
| **Strategy / policy** | Why we test, how we categorize tests, lifecycle rules | `strategy/` |
| **Runbooks / checklists** | Step-by-step procedures a person or script can follow | `runbooks/` |
| **Inventory** | What checks exist, their current status, ownership | `test-inventory.md` |
| **Evidence / results** | Dated records of what was run and what was found | `evidence/` |

## Guiding principles

- **Runbooks and evidence are repo artifacts.** Results belong in `evidence/` so they
  can be committed, reviewed in pull requests, and diffed over time.
- **No silent disappearance.** When a check is no longer relevant, it must be explicitly
  marked `superseded` or `deprecated` in `test-inventory.md` with a brief reason, rather
  than deleted. This preserves institutional memory.
- **Do not overclaim.** Documents describe what currently exists. Aspirational sections
  are explicitly labeled "planned" or "not yet implemented."
- **Consistent with `docs/context/`.** The context directory holds architectural and
  session-continuity notes. The quality directory holds testing discipline and evidence.
  The two complement each other; avoid duplicating content between them.

## Current state (as of initial creation)

- Automated test suite: xUnit, ~350 tests, run via `dotnet test`
- Manual smoke testing: ad-hoc; runbook now exists to formalize it
- Data validation: manual reconciliation via `/dev/reconcile/*` endpoints
- CI: not yet configured
- Performance / security testing: not yet started
