# SESSION HANDOFF — CC-ERATE-000056B — 2026-03-31

## COMPLETED THIS SESSION

- CC-ERATE-000056 Phase 1: Added Npgsql 8.0.11, dual-provider AddDbContext, DatabaseProvider config key, appsettings.json Postgres connection string (PR #49)
- CC-ERATE-000056A: Guarded PRAGMA WAL with `ActiveProvider.Contains("Sqlite")`, added Npgsql to Infrastructure, unified `Migrate()` for both providers (PR #50)
- CC-ERATE-000056B: Created `scripts/validate-postgres-import.sh` with Keeper credential retrieval, app startup, EPC entity import, idempotency rerun, read-path smoke (PR #51, #52)
- Fixed validation script: `--no-launch-profile`, `127.0.0.1` binding, multi-PID stale process kill (`xargs`), log truncation, curl visibility (separate status/body capture, `--max-time 600`)
- Fixed import response parsing: `ImportJob.Status` is int (2=Succeeded), not string "Completed"; idempotency via `psql COUNT(*)` not `recordsInserted`; table name `EpcEntities` not `Entities`
- Fixed migrations: added `Npgsql:ValueGenerationStrategy` identity annotation to all 13 Id columns across 11 migration files (resolves 23502 null Id constraint error)
- Fixed HttpClient timeout: set 30-minute timeout on `UsacCsvClient` registration, resolving TD-001
- Discovered root blocking issue: all migrations use SQLite type literals (`type: "TEXT"` for DateTime/Decimal) — Postgres creates wrong column types causing `InvalidCastException` on read and duplicate key on upsert fallback

## CURRENT STATE

- **Works end-to-end:** SQLite path fully functional and unchanged; all prior features stable
- **Partial / incomplete:** CC-ERATE-000056B Postgres import validation — blocked on column type mismatch between SQLite migration type literals and Postgres strict typing. `EnsureCreated()` revert was applied to `Program.cs` but **NOT committed** (user cancelled mid-edit). `eratedb` on dude-mcp-01 has a bad schema (TEXT columns for DateTime/Decimal) from the last `Migrate()` run.
- **Tests passing:** Partial — 16 unit tests pass; `ConsultantFrnStatusImport_IsIdempotent_OnRerun` known failing (pre-existing); full suite hangs (TD-021, pre-existing)
- **Branch status:** `feature/fix-postgres-validation-port-binding` — PR #52 open, not merged. Last pushed commit `e55be9c`. `Program.cs` has uncommitted local `EnsureCreated()` revert.

## UNEXPECTED DISCOVERIES

- All EF Core migrations use `type: "TEXT"` for every column (SQLite convention) — Postgres takes this literally, creating `text` columns for DateTime and Decimal fields. Npgsql refuses to deserialize these, throwing `InvalidCastException: Reading as 'System.DateTime'/'System.Decimal' is not supported for fields having DataTypeName 'text'`. This also causes the upsert to fail on `FirstOrDefaultAsync`, fall through to `Add()`, and hit duplicate key on the EntityNumber unique index.
- The Npgsql identity annotation fix (23502) was necessary but insufficient — column type mismatch is a separate, deeper problem.
- `psql -U postgres` requires `sudo -u postgres psql` on Ubuntu (peer auth) — `psql -U postgres` from a non-postgres user fails.
- dude-mcp-01 had multiple dotnet processes holding port 5080 across script runs; `lsof -ti` returns multi-line PIDs; `kill "$VAR"` with a newline-containing variable silently fails.

## DECISIONS I MADE AUTONOMOUSLY (needs architect review)

- **EnsureCreated() vs Migrate() for Postgres:** Chose to revert to `EnsureCreated()` for Postgres as the pragmatic fix for column type mismatch. This bypasses migration SQL and uses Npgsql model-derived types. Trade-off: no `__EFMigrationsHistory` tracking for Postgres, future schema changes require manual handling. Alternative: rewrite all migration column type declarations to be provider-agnostic (larger effort). Edit was applied but cancelled before commit — **architect must decide which path to take.**
- Added Npgsql package to Infrastructure project (design-time migration tooling requirement).
- Used `xargs` for multi-PID process kill in validation script.

## BOOT BLOCK FIELDS TO UPDATE IN CLAUDE.md

- [ ] Boot Block ID (keep CC-ERATE-000056 until phase complete)
- [ ] CURRENT STATE — Branch status (`feature/fix-postgres-validation-port-binding`, PR #52 open)
- [ ] CURRENT STATE — Works (add: HttpClient 30-min timeout, Npgsql identity annotations on all migrations)
- [ ] KNOWN DEBT — TD-001 resolved (HttpClient timeout set); add new item for migration column type provider compatibility
- [ ] Other: Note PR #49 and #50 are superseded by #52

---

## NEXT PROMPT DRAFT (CC-ERATE-000056B-RESUME)

Before starting this task:

```
git checkout feature/fix-postgres-validation-port-binding
git pull
```

**CC-ERATE-000056B-RESUME — Resolve Postgres column type mismatch and complete import validation**

You are working in the erate-workbench-poc repo on branch `feature/fix-postgres-validation-port-binding`.

### Objective

Complete the CC-ERATE-000056B Postgres import validation by resolving the final blocking issue: EF Core migrations use SQLite type literals (`type: "TEXT"`, `type: "INTEGER"`) for all columns, causing Postgres to create `text` columns for DateTime and Decimal fields. This prevents the EPC entity upsert from working. Fix the initialization strategy and complete a successful end-to-end import validation on dude-mcp-01.

### Context

The repo already includes:

- Dual-provider support (DatabaseProvider=Sqlite|Postgres) in Program.cs and appsettings.json
- Npgsql 8.0.11 in both Api and Infrastructure projects
- PRAGMA WAL migration guard (`ActiveProvider.Contains("Sqlite")`)
- Npgsql identity annotations on all 13 migration Id columns
- 30-minute HttpClient timeout on UsacCsvClient
- `scripts/validate-postgres-import.sh` with all script-level fixes applied (port binding, stale kill, curl visibility, response parsing, idempotency via COUNT)
- `Program.cs` has a **LOCAL UNCOMMITTED EDIT** reverting Postgres to `EnsureCreated()` — review and commit or discard per architect direction

### Primary Goals

1. Resolve the DateTime/Decimal column type mismatch so Postgres can read EpcEntity rows correctly
2. Drop and recreate `eratedb` on dude-mcp-01 to clear the bad schema
3. Run `validate-postgres-import.sh` to completion: migrations OK, import succeeds, idempotency confirmed, read-path smoke passes

### Requirements

1. Choose and commit one of these approaches for Postgres schema initialization:
   - **Option A (EnsureCreated):** Keep the uncommitted `Program.cs` edit — Postgres uses `EnsureCreated()` (derives correct Npgsql types from model), SQLite uses `Migrate()`. Drop/recreate eratedb, rerun script.
   - **Option B (Fix migration types):** Remove or make provider-conditional all `type: "TEXT"` and `type: "INTEGER"` literals across all migrations so Postgres creates correct column types. Keep unified `Migrate()` for both providers.
   - Architect chooses; implement as directed.
2. Ensure `eratedb` on dude-mcp-01 is on a clean schema before running:
   ```sql
   sudo -u postgres psql -c "DROP DATABASE eratedb; CREATE DATABASE eratedb OWNER erate;"
   ```
3. Run `validate-postgres-import.sh` — all steps must pass: provider log, HTTP 200 import, `recordsProcessed > 0`, COUNT stable on rerun, Dashboard/Search/Analytics HTTP 200

### Constraints

- No breaking changes to SQLite path
- No secrets committed to repo
- Keep change minimal — do not redesign migration system
- WSL-canonical dev environment

### Validation

1. `dotnet build` — 0 errors
2. On dude-mcp-01: `sudo -u postgres psql -c "DROP DATABASE eratedb; CREATE DATABASE eratedb OWNER erate;"`
3. On dude-mcp-01: `ERATE_PG_PASSWORD=<pw> bash scripts/validate-postgres-import.sh`
4. All steps show `[PASS]` including import succeeded, row count confirmed, idempotency confirmed

### Deliverable

Return:

- Approach chosen (A or B) and rationale
- Files changed
- Validation output from dude-mcp-01 showing all `[PASS]`
- Commit hash

Use this exact prompt ID in your response: `CC-ERATE-000056B-RESUME`

---

## NEXT STEPS FOR ARCHITECT

1. Review the NEXT PROMPT DRAFT above — verify it matches intent before using
2. Paste handoff to ChatGPT for architect review
3. Update CLAUDE.md boot block fields listed above
4. Archive this handoff to `docs/context/boot-blocks/CC-ERATE-000056B-handoff.md`
5. ChatGPT may refine the NEXT PROMPT DRAFT — always use ChatGPT's version as final
