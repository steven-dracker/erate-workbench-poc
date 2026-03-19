# ERATE Workbench

A proof-of-concept analytics and workflow platform built on USAC E-Rate open data. E-Rate is the FCC program that funds broadband and telecommunications for eligible schools and libraries. USAC publishes the underlying datasets publicly; this tool ingests them, normalizes the data into a local SQLite database, and surfaces search, analytics, risk insights, and reference content through a Razor Pages UI and REST API.

## What it does

- **Ingests four USAC datasets** via import endpoints — entity directory, funding commitments, service providers, Form 471 applications
- **Idempotent ETL** — re-running any import updates changed records without creating duplicates
- **Entity search** — search across 250k+ eligible schools, libraries, and districts
- **Analytics** — committed funding by year, top-funded entities, discount rate analysis, cross-dataset commitment vs. disbursement metrics
- **Risk Insights** — execution risk indicators for advisor use
- **Reference pages** — Program Workflow, Ecosystem map, E-Rate Central Historical Timeline
- **Swagger UI** — every API endpoint is testable at `/swagger`

## Local development

### Prerequisites

- .NET 8 SDK
- WSL2 (Ubuntu) recommended on Windows
- `lsof` and `curl` — standard on Ubuntu, used by dev scripts

### Validate (restore → build → test, no app launch)

```bash
./scripts/dev-run.sh --validate
```

### Run the app (full pipeline + foreground launch)

```bash
./scripts/dev-run.sh
```

App binds to `http://localhost:5000`. The SQLite database is created automatically on first run via EF Core migrations. Press `Ctrl+C` to stop.

> `dev-run.sh` stops any process already bound to port 5000 before starting.

### Run UI smoke tests

`ui-test.sh` starts the app, waits for `/health` readiness, runs Playwright tests, and stops the app:

```bash
./scripts/ui-test.sh
```

If the app is already running separately:

```bash
./scripts/ui-test.sh --app-running
```

**One-time Playwright system setup (WSL / bare Ubuntu):**

```bash
sudo apt-get install -y libnss3 libnspr4 libasound2t64
~/.dotnet/tools/playwright install chromium
```

This is handled automatically in GitHub Actions CI.

See [`docs/devops/local-workflow.md`](docs/devops/local-workflow.md) for full script reference.

### Importing data

Trigger each import via `POST` in Swagger UI at `/swagger`:

| Dataset | Endpoint |
|---|---|
| USAC Entity Directory (EPC) | `POST /import/usac` |
| Funding Commitments | `POST /import/funding-commitments` |
| Service Providers (SPIN) | `POST /import/service-providers` |
| Form 471 Applications | `POST /import/form471` |

Imports are idempotent — safe to re-run. Each pages through the full USAC dataset and may take several minutes.

## CI pipeline

```
build → test → ui-smoke      (Playwright, headless Chromium)
             → security      (NuGet vulnerability scan)
             → secrets-scan  (gitleaks git history scan)
                    └─────────────────── publish  (self-contained linux-x64 artifact)
```

Runs on every push and pull request. See [`docs/devops/pipeline.md`](docs/devops/pipeline.md) for details.

## DevSecOps controls

| Control | Tool | Behavior |
|---|---|---|
| Dependency vulnerability scan | `dotnet list package --vulnerable` | Fails on vulnerable direct packages; warns on transitive |
| Secrets scanning | gitleaks v8.30.0 | Fails on any detected secret in git history |
| Automated dependency updates | Dependabot | Weekly PRs for NuGet and GitHub Actions |

**Dependabot PR strategy:**

- Patch/minor — merge after CI passes
- Test/tooling packages — review CI output, merge if green
- Major runtime upgrades (EF Core, ASP.NET Core) — treat as deliberate work, test carefully
- Keep the open PR queue small (5-PR limit per ecosystem)

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8, ASP.NET Core, Razor Pages |
| Database | SQLite via Entity Framework Core 8 |
| CSV ingestion | CsvHelper + Socrata HTTP API |
| UI | Bootstrap 5, Chart.js |
| Unit tests | xUnit (347 tests) |
| UI automation | Playwright for .NET (headless Chromium) |
| CI | GitHub Actions |
| Security | gitleaks, NuGet advisory DB, Dependabot |
