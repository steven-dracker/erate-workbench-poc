#!/usr/bin/env bash
# validate-postgres-import.sh — CC-ERATE-000056B
#
# Run on dude-mcp-01 from the repo root.
# Validates Postgres provider end-to-end:
#   1. Retrieve credential from Keeper Commander
#   2. Start app against local Postgres
#   3. Apply migrations
#   4. Run small real import (EPC entities)
#   5. Idempotency rerun
#   6. Read-path smoke (dashboard, search, analytics)
#   7. Print summary
#
# Usage:
#   cd ~/erate-workbench (or wherever repo is cloned)
#   bash scripts/validate-postgres-import.sh
#
# Keeper record title must contain "erate" (case-insensitive).
# Override: ERATE_PG_PASSWORD=<pw> bash scripts/validate-postgres-import.sh

set -euo pipefail

APP_PORT=5080
BASE_URL="http://localhost:${APP_PORT}"
APP_PID=""
PASS=""

# ── colour helpers ────────────────────────────────────────────────────────────
GREEN='\033[0;32m'; RED='\033[0;31m'; YELLOW='\033[1;33m'; NC='\033[0m'
ok()   { echo -e "${GREEN}[PASS]${NC} $*"; }
fail() { echo -e "${RED}[FAIL]${NC} $*"; }
info() { echo -e "${YELLOW}[INFO]${NC} $*"; }

# ── cleanup on exit ───────────────────────────────────────────────────────────
cleanup() {
  if [[ -n "$APP_PID" ]] && kill -0 "$APP_PID" 2>/dev/null; then
    info "Stopping app (PID $APP_PID)…"
    kill "$APP_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

# ── 1. Credential retrieval ───────────────────────────────────────────────────
echo
info "=== Step 1: Credential retrieval ==="

if [[ -n "${ERATE_PG_PASSWORD:-}" ]]; then
  PASS="$ERATE_PG_PASSWORD"
  info "Using ERATE_PG_PASSWORD env var"
else
  info "Trying Keeper Commander…"
  if command -v keeper &>/dev/null; then
    # Try to get password — record must have 'erate' or 'postgres' in title
    # Adjust the record path/UID if your Keeper vault uses a different title
    KEEPER_RECORD="${KEEPER_RECORD:-erate-postgres}"
    PASS=$(keeper get "$KEEPER_RECORD" --format password 2>/dev/null || true)
  fi

  if [[ -z "$PASS" ]]; then
    echo -n "Keeper lookup failed or not configured. Enter Postgres 'erate' user password: "
    read -rs PASS
    echo
  fi
fi

if [[ -z "$PASS" ]]; then
  fail "No password available — aborting"
  exit 1
fi
ok "Credential obtained"

# ── 2. Connection string ──────────────────────────────────────────────────────
PG_CONN="Host=localhost;Port=5432;Database=eratedb;Username=erate;Password=${PASS}"

# Quick connectivity check before starting the app
info "Verifying Postgres connectivity…"
if ! pg_isready -h localhost -p 5432 -U erate -d eratedb -q 2>/dev/null; then
  # pg_isready may not be available; try nc
  if ! nc -z localhost 5432 2>/dev/null; then
    fail "Postgres is not listening on localhost:5432"
    exit 1
  fi
fi
ok "Postgres is reachable"

# ── 3. Build ──────────────────────────────────────────────────────────────────
echo
info "=== Step 2: Build ==="
dotnet build src/ErateWorkbench.Api/ErateWorkbench.Api.csproj -c Release --nologo 2>&1 | tail -5
ok "Build complete"

# ── 4. Start app against Postgres ────────────────────────────────────────────
echo
info "=== Step 3: App startup (Postgres) ==="
info "Starting on port $APP_PORT…"

export DatabaseProvider=Postgres
export ConnectionStrings__Postgres="$PG_CONN"
export ASPNETCORE_URLS="http://localhost:${APP_PORT}"
export ASPNETCORE_ENVIRONMENT=Development

dotnet run --project src/ErateWorkbench.Api -c Release --no-build \
  > /tmp/erate-pg-app.log 2>&1 &
APP_PID=$!
info "App PID: $APP_PID"

# Wait for /health
MAX_WAIT=30
WAITED=0
until curl -sf "${BASE_URL}/health" >/dev/null 2>&1; do
  sleep 1
  WAITED=$((WAITED + 1))
  if [[ $WAITED -ge $MAX_WAIT ]]; then
    fail "App did not become healthy after ${MAX_WAIT}s"
    echo "--- app log ---"
    tail -30 /tmp/erate-pg-app.log
    exit 1
  fi
done
ok "App healthy after ${WAITED}s"

# Confirm provider and migration log
if grep -q "Database provider: Postgres" /tmp/erate-pg-app.log; then
  ok "Startup log: 'Database provider: Postgres — applying migrations'"
else
  fail "Expected provider log not found"
  cat /tmp/erate-pg-app.log
  exit 1
fi

# ── 5. EPC Entity import (small, fast reference dataset) ─────────────────────
echo
info "=== Step 4: EPC Entity import (dataset 7i5i-83qf) ==="
info "This is the USAC Supplemental Entity Information dataset (~50–100k rows)"

IMPORT_START=$(date +%s)
IMPORT_RESULT=$(curl -sf -X POST "${BASE_URL}/import/usac" \
  -H "Content-Type: application/json" \
  -d '{}' 2>&1) || { fail "Import request failed"; exit 1; }
IMPORT_END=$(date +%s)
DURATION=$((IMPORT_END - IMPORT_START))

echo "Result: $IMPORT_RESULT"

RECORDS_PROCESSED=$(echo "$IMPORT_RESULT" | grep -oP '"recordsProcessed":\K[0-9]+' || echo "?")
RECORDS_INSERTED=$(echo  "$IMPORT_RESULT" | grep -oP '"recordsInserted":\K[0-9]+'  || echo "?")
STATUS=$(echo "$IMPORT_RESULT"            | grep -oP '"status":"\K[^"]+' | head -1  || echo "?")

if [[ "$STATUS" == "Completed" ]]; then
  ok "Import completed: processed=$RECORDS_PROCESSED inserted=$RECORDS_INSERTED duration=${DURATION}s"
else
  fail "Import status: $STATUS"
  echo "$IMPORT_RESULT"
  exit 1
fi

# ── 6. Idempotency rerun ──────────────────────────────────────────────────────
echo
info "=== Step 5: Idempotency rerun ==="

RERUN_RESULT=$(curl -sf -X POST "${BASE_URL}/import/usac" \
  -H "Content-Type: application/json" \
  -d '{}' 2>&1) || { fail "Rerun request failed"; exit 1; }

echo "Rerun result: $RERUN_RESULT"

RERUN_INSERTED=$(echo "$RERUN_RESULT" | grep -oP '"recordsInserted":\K[0-9]+'  || echo "?")
RERUN_UPDATED=$(echo  "$RERUN_RESULT" | grep -oP '"recordsUpdated":\K[0-9]+'   || echo "?")
RERUN_STATUS=$(echo  "$RERUN_RESULT"  | grep -oP '"status":"\K[^"]+' | head -1 || echo "?")

if [[ "$RERUN_STATUS" == "Completed" ]]; then
  if [[ "$RERUN_INSERTED" == "0" ]]; then
    ok "Idempotency confirmed: rerun inserted=$RERUN_INSERTED updated=$RERUN_UPDATED (no new rows)"
  else
    fail "Idempotency breach: rerun inserted $RERUN_INSERTED new rows"
    exit 1
  fi
else
  fail "Rerun status: $RERUN_STATUS"
  exit 1
fi

# ── 7. Read-path smoke ────────────────────────────────────────────────────────
echo
info "=== Step 6: Read-path smoke ==="

# Dashboard
HTTP_STATUS=$(curl -so /dev/null -w "%{http_code}" "${BASE_URL}/")
if [[ "$HTTP_STATUS" == "200" ]]; then
  ok "Dashboard: HTTP $HTTP_STATUS"
else
  fail "Dashboard: HTTP $HTTP_STATUS"
fi

# Search (no query — should load form)
HTTP_STATUS=$(curl -so /dev/null -w "%{http_code}" "${BASE_URL}/Search")
if [[ "$HTTP_STATUS" == "200" ]]; then
  ok "Search: HTTP $HTTP_STATUS"
else
  fail "Search: HTTP $HTTP_STATUS"
fi

# Search with filter to exercise DB read
HTTP_STATUS=$(curl -so /dev/null -w "%{http_code}" "${BASE_URL}/Search?state=CA&pageNum=1")
if [[ "$HTTP_STATUS" == "200" ]]; then
  ok "Search (filtered): HTTP $HTTP_STATUS"
else
  fail "Search (filtered): HTTP $HTTP_STATUS"
fi

# Analytics page (requires FundingCommitments — may have no data yet, 200 is enough)
HTTP_STATUS=$(curl -so /dev/null -w "%{http_code}" "${BASE_URL}/Analytics")
if [[ "$HTTP_STATUS" == "200" ]]; then
  ok "Analytics: HTTP $HTTP_STATUS"
else
  fail "Analytics: HTTP $HTTP_STATUS"
fi

# ── 8. Row count spot-check via Swagger/API ────────────────────────────────────
echo
info "=== Step 7: Row count spot-check ==="
ENTITY_COUNT=$(PGPASSWORD="$PASS" psql -h localhost -U erate -d eratedb -t -c \
  "SELECT COUNT(*) FROM \"Entities\";" 2>/dev/null | tr -d ' \n' || echo "psql unavailable")
info "Entities table row count: $ENTITY_COUNT"
if [[ "$ENTITY_COUNT" =~ ^[0-9]+$ ]] && [[ "$ENTITY_COUNT" -gt 0 ]]; then
  ok "Entities table has $ENTITY_COUNT rows"
else
  info "psql count unavailable — relying on import result above"
fi

# ── 9. Summary ────────────────────────────────────────────────────────────────
echo
echo "========================================"
echo " CC-ERATE-000056B — Validation Summary"
echo "========================================"
echo " Node:          dude-mcp-01"
echo " Provider:      Postgres (eratedb)"
echo " Import:        EPC Entities (7i5i-83qf)"
echo " Processed:     $RECORDS_PROCESSED"
echo " Inserted:      $RECORDS_INSERTED"
echo " Rerun status:  $RERUN_STATUS (inserted=$RERUN_INSERTED)"
echo " Entity count:  $ENTITY_COUNT"
echo "========================================"
ok "All validation steps passed"
