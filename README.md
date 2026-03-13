# E-Rate Workbench

A proof-of-concept analytics workbench for USAC E-Rate open data. E-Rate is the FCC program that provides subsidized broadband and telecommunications to eligible schools and libraries across the United States. USAC publishes the underlying datasets publicly; this tool ingests them, normalizes the data into a local database, and surfaces search and analytics through a browser UI and REST API.

## What it does

- **Ingests four USAC datasets** via one-click import endpoints (entity directory, funding commitments, service providers, Form 471 applications)
- **Idempotent ETL** — re-running any import updates changed records without creating duplicates
- **Entity search** — full-text + filter search across 250k+ eligible schools, libraries, and districts
- **Analytics** — committed funding by year, Category 1 vs Category 2 breakdown, top-funded entities, discount rate analysis, and cross-dataset metrics that join entity eligibility data with actual funding commitments
- **Swagger UI** — every endpoint is documented and testable at `/swagger`

## Running locally

```bash
dotnet run --project src/ErateWorkbench.Api
```

The database (SQLite) is created automatically on first run. Navigate to `http://localhost:5075`.

## Importing data

Trigger each import via `POST` to the corresponding endpoint (visible in Swagger UI):

| Dataset | Endpoint |
|---|---|
| USAC Entity Directory (EPC) | `POST /import/usac` |
| Funding Request Commitments | `POST /import/funding-commitments` |
| Service Providers (SPIN) | `POST /import/service-providers` |
| Form 471 Applications | `POST /import/form471` |

Each import downloads the full dataset from USAC's public data hub, processes it in 500-record batches, and returns a summary of records inserted, updated, and failed. Import history is tracked in the database.

## Running tests

```bash
dotnet test
```

61 tests covering CSV parsing, idempotent upsert, and analytics queries. All tests use an in-memory SQLite database — no external dependencies required.

## Tech stack

- ASP.NET Core 8 — Minimal API + MVC controllers + Razor Pages
- Entity Framework Core 8 with SQLite
- CsvHelper for USAC CSV ingestion
- Chart.js + Bootstrap 5 for the browser UI
- xUnit for tests
