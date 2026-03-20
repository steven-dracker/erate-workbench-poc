# Logging and Observability

## Overview

The application uses the built-in ASP.NET Core / Microsoft.Extensions.Logging stack with a `SimpleConsole` formatter. No external logging library is needed. Every log line includes a timestamp, level, and category.

**Sample output:**

```
21:30:30 info: Microsoft.Hosting.Lifetime[14] Now listening on: http://localhost:5000
21:30:35 info: ErateWorkbench.Api.Pages.AnalyticsModel[0] Analytics page rendered in 2613ms (cache miss — queries executed)
21:30:35 info: ErateWorkbench.Api.Pages.AnalyticsModel[0] Analytics page rendered in 42ms (cache hit)
21:30:37 info: Microsoft.Hosting.Lifetime[0] Application is shutting down...
```

Format: `HH:mm:ss LEVEL: Category[EventId] Message`

---

## Log levels by category

Configured in `appsettings.json`:

| Category | Default level | Reason |
|---|---|---|
| `Default` | `Information` | Catch-all for unmatched categories |
| `ErateWorkbench` | `Information` | App namespace — import progress, analytics timing, reconciliation events |
| `Microsoft.AspNetCore` | `Warning` | Suppresses per-request 200 OK noise; errors and redirects still surface |
| `Microsoft.EntityFrameworkCore` | `Warning` | EF Core is very chatty at Information; SQL queries are hidden by default |
| `System` | `Warning` | Runtime-level noise, rarely actionable |

---

## Adjusting log levels

Edit `appsettings.json` (or `appsettings.Development.json` for local-only changes). No code change or restart of the dotnet SDK is needed — just stop and re-run the app.

**See the SQL queries EF Core generates:**

```json
"Microsoft.EntityFrameworkCore.Database.Command": "Information"
```

**Reduce all application logs to warnings only:**

```json
"ErateWorkbench": "Warning"
```

**Turn everything on for deep investigation:**

```json
"Default": "Debug"
```

The log level hierarchy is: `Trace < Debug < Information < Warning < Error < Critical`. Setting a level shows that level and everything above it.

---

## Where logs appear

### Foreground run (`./scripts/dev-run.sh`)

Logs stream to the terminal **and** are written to `/tmp/erate-workbench-app.log` via `tee`. After stopping the app with `Ctrl+C`, review the file:

```bash
cat /tmp/erate-workbench-app.log
```

Or follow it in real time in a second terminal:

```bash
tail -f /tmp/erate-workbench-app.log
```

### Background run (`./scripts/dev-run.sh --start-for-tests` or `./scripts/ui-test.sh`)

Logs are written to `/tmp/erate-workbench-app.log`. The foreground terminal is free for other commands. Check the file after tests:

```bash
cat /tmp/erate-workbench-app.log
```

### CI pipeline

Logs go to GitHub Actions step output and are visible in the workflow run summary. No file artifact is uploaded for normal runs.

---

## Instrumented areas

### Analytics page timing

Every request to `/Analytics` logs:

```
info: ErateWorkbench.Api.Pages.AnalyticsModel[0]
      Analytics page rendered in {ms}ms (cache miss — queries executed)
```

or on subsequent requests:

```
info: ErateWorkbench.Api.Pages.AnalyticsModel[0]
      Analytics page rendered in {ms}ms (cache hit)
```

This makes it easy to confirm the 24-hour in-memory cache is working and to spot any unexpected regressions in query time.

### Import services

All import services (`FundingCommitmentImportService`, `EpcEntityImportService`, etc.) log progress at `Information` and failures at `Warning`/`Error` via `ILogger<T>`.

---

## Filtering logs with `grep`

Since every line is single-line and prefixed with a timestamp, standard shell tools work well:

```bash
# Application logs only (filter out framework noise)
grep "ErateWorkbench" /tmp/erate-workbench-app.log

# Warnings and errors only
grep -E " warn:| fail:" /tmp/erate-workbench-app.log

# Analytics timing lines
grep "Analytics page rendered" /tmp/erate-workbench-app.log

# All errors
grep " fail:" /tmp/erate-workbench-app.log
```

---

## File growth

The log file at `/tmp/erate-workbench-app.log` is overwritten on each `dev-run.sh` invocation (it is not appended). Size is bounded by the session length — there is no log rotation concern for local development.

---

## What is not logged

- Request/response bodies
- Query parameters containing user input
- Database connection strings
- Any credentials or tokens

EF Core SQL query logs (`Database.Command`) are off by default. Enable them only for debugging specific query behavior.
