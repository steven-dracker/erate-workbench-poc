# CI Pipeline

## Pipeline shape

```
build
  └─ test
       ├─ ui-smoke      (parallel)
       ├─ security      (parallel)
       └─ secrets-scan  (parallel)
```

Every job runs on `ubuntu-latest`. The pipeline triggers on `push` and `pull_request` across all branches.

---

## Jobs

### build

**Purpose:** Restore NuGet packages and compile the full solution.

- `dotnet restore ErateWorkbench.sln`
- `dotnet build --no-restore --configuration Release`

Fails fast if the solution does not compile. All downstream jobs depend on this.

---

### test

**Purpose:** Run the unit and integration test suite.

**Depends on:** `build`

- Targets `tests/ErateWorkbench.Tests` only (not `UITests`, which requires a running app)
- `dotnet test --no-build --configuration Release`
- 347 tests covering CSV parsing, idempotent upsert, reconciliation logic, and analytics queries
- All tests use an in-memory SQLite database — no external dependencies

A failure here indicates a regression in application logic. Fix before merging.

---

### ui-smoke

**Purpose:** Confirm that key application surfaces load and render correctly in a real browser.

**Depends on:** `test`
**Runs in parallel with:** `security`, `secrets-scan`

Steps:
1. Install Playwright browser system dependencies (`libnss3`, `libnspr4`, `libasound2t64`)
2. Install Playwright Chromium via `dotnet tool install --global Microsoft.Playwright.CLI`
3. Start the API in the background (`ASPNETCORE_URLS=http://localhost:5000 dotnet run ...`)
4. Poll `GET /health` until the app is ready (30-second timeout)
5. Run `tests/ErateWorkbench.UITests` with headless Chromium
6. Upload Playwright failure artifacts if tests fail
7. Stop the background API process

**Tests cover:**
- `/health` endpoint returns 200 and `{"status":"ok"}`
- Dashboard loads with correct shared-layout title and navbar
- All 8 navigation links are present
- Ecosystem page renders with `h1` heading
- History page renders with `h1` heading

A failure here indicates a page-level regression or broken layout. Check the uploaded Playwright artifacts (screenshots, traces) for diagnosis.

---

### security

**Purpose:** Scan NuGet packages for known vulnerabilities.

**Depends on:** `test`
**Runs in parallel with:** `ui-smoke`, `secrets-scan`

Two-tier approach:

| Tier | Scope | Behavior |
|---|---|---|
| Direct dependencies | `dotnet list package --vulnerable` | **Fails** — direct vulnerable packages are addressable and production-facing |
| Transitive dependencies | `--include-transitive` | **Warns only** — often test-only or non-exploitable in this context |

A direct-dependency failure requires updating the affected package before merging. Transitive warnings are surfaced for awareness but do not block the pipeline.

**Future extensions in this job:**
- CodeQL SAST analysis (`github/codeql-action`)
- License compliance check
- SARIF upload to GitHub Security tab (requires GitHub Advanced Security or public repo)

---

### secrets-scan

**Purpose:** Detect hardcoded secrets in the git history.

**Depends on:** `test`
**Runs in parallel with:** `ui-smoke`, `security`

- Tool: **gitleaks v8.30.0** (pinned, CLI — no license required)
- Checks out full git history (`fetch-depth: 0`)
- Scans all committed blobs against 170+ default secret patterns
- Config: `.gitleaks.toml` (minimal — two path exclusions for the SQLite binary and a legacy nested working-directory snapshot)

A failure means a likely secret was found in a commit. Investigate the gitleaks output to determine whether it is a true positive or a false positive. If false positive, add a targeted allowlist entry to `.gitleaks.toml`.

**Future extensions:**
- Custom rules in `.gitleaks.toml` for domain-specific patterns
- TruffleHog for verified-secrets second opinion (only reports actively valid credentials)
- SARIF output to GitHub Security tab

---

## Interpreting failures

| Failing job | Likely cause | Action |
|---|---|---|
| `build` | Compile error | Fix the code |
| `test` | Unit/integration regression | Fix the failing test or application logic |
| `ui-smoke` | Page-level regression or broken layout | Check Playwright artifacts; inspect the relevant page |
| `security` | Vulnerable direct NuGet package | Update the package |
| `secrets-scan` | Hardcoded secret or false positive | Investigate gitleaks output; update `.gitleaks.toml` if false positive |

---

## Extending the pipeline

The current shape is intentionally minimal. Future stages slot in cleanly after the existing jobs:

```
build → test → ui-smoke
             → security
             → secrets-scan
             → package     (future: dotnet publish, Docker image)
             → deploy      (future: environment-specific, tag-triggered)
```

Add new jobs to `.github/workflows/ci.yml`. Use `needs:` to express dependencies. Keep each job focused on a single concern.
