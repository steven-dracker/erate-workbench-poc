# Test Lifecycle

Rules for how quality checks are created, maintained, and retired.
The goal is a test inventory that stays accurate and trustworthy over time —
one where every entry reflects a real check, and nothing disappears silently.

---

## Status definitions

| Status | Meaning |
|---|---|
| `candidate` | Identified as worthwhile but not yet written or formalized |
| `active` | Written, passing, and currently providing value |
| `superseded` | Replaced by a stronger check; the old check is no longer run |
| `deprecated` | No longer relevant (feature removed, assumption invalid); retired |
| `removed` | Deleted from the codebase; inventory entry kept for historical record |

Every check in `test-inventory.md` must have exactly one of these statuses at all times.

---

## Candidate

A check moves to `candidate` when someone identifies it as worth writing but
has not yet done the work. Candidates should be specific enough that another
person could implement them without asking follow-up questions.

**What makes a good candidate entry:**
- Concise description of what it checks
- Why it matters (which bug, rule, or risk it protects)
- Enough context to implement it without asking follow-up questions

**What a candidate is not:**
- "We should test the analytics layer" — too vague; split into specific behaviors
- A wish-list item with no concrete scope

Candidates with no owner and no movement for 3+ months should be promoted,
demoted to `deprecated`, or split into smaller pieces at the next quarterly review.

---

## Active

A check becomes `active` when it is:
- Written and working (for automated tests: passing on `main`)
- Documented in `test-inventory.md` with enough description to understand scope
- Runnable by someone other than the author (for manual checks: the runbook step
  is clear without additional explanation)

**Maintenance obligation while active:**
- Automated tests must pass on every commit to `main`. A persistent failing
  test is either a genuine regression (investigate and fix the code) or an
  invalid test (demote to `deprecated` with a note).
- Manual runbook checks should be reviewed after each full data reload cycle.
  If the expected outcome has changed due to legitimate source data changes,
  update the expected values and log the reason in `evidence/yearly-quality-log.md`.
- If a check starts consistently failing due to an upstream Socrata schema change,
  treat it as a bug — fix the code and the check together; do not silently
  ignore a failing check.

---

## Superseded

A check moves to `superseded` when a better check replaces it.
"Better" means: broader coverage, tighter assertions, or automation of a
previously manual step.

**Rules:**
1. The superseding check must be `active` before the original is marked `superseded`.
   Do not prematurely retire the old check if the replacement is still a `candidate`.
2. The `superseded by` field in the inventory must reference the replacement by name.
3. For automated tests: leave the test method in the codebase for one release cycle,
   decorated with `[Trait("Status", "Superseded")]` and a comment
   `// Superseded by: <new test name>`. Delete it in the next cleanup pass
   and move the inventory entry to `removed`.
4. For manual runbook checks: strike through the step in the runbook and add a note
   pointing to the automated test that now covers it.

**Example — manual check superseded by automation:**
The smoke-test step "reconciliation endpoint returns report" covers the full
Socrata → DB → markdown path. If an automated integration test is written that
mocks the HTTP layer and asserts the same output, that test supersedes the
reconciliation-specific assertion in the smoke check — but the smoke check step
covering HTTP wiring remains, because automation cannot replace it.

---

## Deprecated

A check moves to `deprecated` when it is no longer valid or relevant:

| Reason | Example |
|---|---|
| The feature under test was removed | A test for an endpoint that was deleted |
| The assumption was wrong from the start | A test asserting the wrong column name |
| The check became permanently misleading | A row-count check for a dataset whose Socrata granularity changed |

**Rules:**
1. Do not silently delete — mark `deprecated` in the inventory with a `reason` note.
2. For automated tests: remove the test code (a deprecated test that still passes
   provides false confidence). The inventory entry stays.
3. For manual checks: remove or explicitly strike through the step in the runbook.

---

## Removed

A check moves to `removed` when its test code has been deleted from the codebase
(after passing through `superseded` or `deprecated`). The inventory entry is
retained so the history of coverage is legible.

A `removed` entry must include:
- The date it was removed (or the commit SHA)
- The reason (superseded by what, or deprecated for what reason)

---

## Lifecycle flow

```
                    ┌───────────────────────────────────────────────┐
                    │                                               │
 (identified) → candidate → active ──── superseded ──► removed     │
                                  └──── deprecated ──► removed      │
                                  └──── (stays active while valid)  │
                                                                    │
```

---

## How a manual check becomes automated (promotion path)

1. Manual check is `active` in the inventory.
2. An automated test is written that covers the same assertion (or more).
3. The automated test passes; it is added to the inventory as `active`.
4. The manual check's status changes to `superseded`, referencing the new test.
5. The manual runbook step is annotated: "covered by `<TestClass.Method>` — retained
   for environment wiring only" or struck through if fully redundant.
6. At the next quarterly review, the `superseded` manual entry moves to `removed`.

This path should be the primary direction of travel: manual → automated.
The reverse (automation removed, manual reinstated) is a regression in quality
discipline and must be explicitly noted as temporary in the inventory.

---

## Review cadence

| Trigger | Action |
|---|---|
| After any full data reload | Run data validation runbook; update evidence log; review relevant `active` checks for stale expected values |
| After any significant refactor | Re-run full test suite; review smoke runbook; check that `active` checks still test the right code paths |
| After any bug fix | Add a regression test (see `regression-strategy.md`); update inventory |
| Quarterly (or at major release) | Walk `test-inventory.md` for `candidate` entries with no movement; review `superseded` entries ready to become `removed` |

---

## Ownership

No formal ownership is assigned at this stage. As the team grows, add an `owner`
column to `test-inventory.md`. Ownership means: responsible for keeping the check
`active` and valid, and for updating the inventory if the check's status changes.
