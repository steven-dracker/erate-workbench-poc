#!/usr/bin/env bash
# scripts/ui-test.sh
# Run Playwright UI smoke tests against a running (or freshly started) app.
#
# Usage:
#   ./scripts/ui-test.sh                # Start app, run UI tests, stop app
#   ./scripts/ui-test.sh --app-running  # App already up; run tests only, leave it running
#
# Port:
#   Default: 5000
#   Override: APP_PORT=8080 ./scripts/ui-test.sh
#
# One-time system setup (WSL / bare Ubuntu):
#   sudo apt-get install -y libnss3 libnspr4 libasound2t64
#   After building the project, browsers are installed via:
#   ~/.dotnet/tools/playwright install chromium

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UI_TEST_PROJECT="$REPO_ROOT/tests/ErateWorkbench.UITests/ErateWorkbench.UITests.csproj"
API_PROJECT="$REPO_ROOT/src/ErateWorkbench.Api/ErateWorkbench.Api.csproj"
APP_PORT="${APP_PORT:-5000}"
APP_URL="http://localhost:$APP_PORT"
APP_RUNNING=false
APP_PID=""

# ── argument parsing ──────────────────────────────────────────────────────────
for arg in "$@"; do
  case $arg in
    --app-running) APP_RUNNING=true ;;
    *) echo "Unknown argument: $arg" && echo "Usage: $0 [--app-running]" && exit 1 ;;
  esac
done

# ── helpers ───────────────────────────────────────────────────────────────────
log()  { echo ""; echo "▶ $*"; }
pass() { echo "  ✓ $*"; }
info() { echo "  → $*"; }

# ── health poll ───────────────────────────────────────────────────────────────
wait_for_health() {
  local url="$APP_URL/health"
  local max_wait=30
  local waited=0
  info "Polling $url (timeout ${max_wait}s)..."
  while [ $waited -lt $max_wait ]; do
    if curl -sf "$url" >/dev/null 2>&1; then
      pass "App is healthy at $APP_URL"
      return 0
    fi
    sleep 1
    waited=$((waited + 1))
  done
  echo "✗ App did not respond at $url within ${max_wait}s."
  exit 1
}

# ── step 1: ensure app is running ────────────────────────────────────────────
if [ "$APP_RUNNING" = true ]; then
  log "Assuming app is already running at $APP_URL..."
  wait_for_health
else
  log "Starting app in background at $APP_URL..."
  ASPNETCORE_URLS="$APP_URL" \
    dotnet run --project "$API_PROJECT" --no-build --no-launch-profile \
    > /tmp/erate-workbench-app.log 2>&1 &
  APP_PID=$!
  info "Started (PID $APP_PID). Log: /tmp/erate-workbench-app.log"
  wait_for_health
fi

# ── step 2: run UI tests ─────────────────────────────────────────────────────
log "Running Playwright UI smoke tests..."
APP_BASE_URL="$APP_URL" \
  dotnet test "$UI_TEST_PROJECT" \
  --no-build --configuration Debug \
  --verbosity normal
pass "UI smoke tests passed."

# ── step 3: stop app if we started it ────────────────────────────────────────
if [ -n "$APP_PID" ]; then
  log "Stopping app (PID $APP_PID)..."
  kill -TERM "$APP_PID" 2>/dev/null || true
  wait "$APP_PID" 2>/dev/null || true
  pass "App stopped."
fi

echo ""
echo "✓ UI smoke complete."
