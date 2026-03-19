#!/usr/bin/env bash
# scripts/dev-run.sh
# Local developer workflow: stop → restore → build → test → (run | start-for-tests | skip)
#
# Modes:
#   (no args)           Full pipeline: restore → build → test → run (foreground)
#   --validate          Restore → build → test only. Do not launch app.
#   --start-for-tests   Restore → build → test → start app in background,
#                       wait for /health to respond, then exit 0.
#                       Leaves app running for external test execution (e.g. Playwright).
#                       Caller is responsible for stopping the app afterward.
#
# Port:
#   Default: 5000
#   Override: APP_PORT=8080 ./scripts/dev-run.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SLN="$REPO_ROOT/ErateWorkbench.sln"
API_PROJECT="$REPO_ROOT/src/ErateWorkbench.Api/ErateWorkbench.Api.csproj"
APP_PORT="${APP_PORT:-5000}"
APP_URL="http://localhost:$APP_PORT"
MODE="run"   # run | validate | start-for-tests

# ── argument parsing ──────────────────────────────────────────────────────────
for arg in "$@"; do
  case $arg in
    --validate)          MODE="validate" ;;
    --start-for-tests)   MODE="start-for-tests" ;;
    *) echo "Unknown argument: $arg" && echo "Usage: $0 [--validate | --start-for-tests]" && exit 1 ;;
  esac
done

# ── helpers ───────────────────────────────────────────────────────────────────
log()  { echo ""; echo "▶ $*"; }
pass() { echo "  ✓ $*"; }
info() { echo "  → $*"; }

# ── stop: graceful SIGTERM, then SIGKILL if still alive ──────────────────────
stop_app() {
  local pids
  pids=$(lsof -ti :"$APP_PORT" 2>/dev/null || true)
  if [ -z "$pids" ]; then
    pass "No running instance on port $APP_PORT."
    return
  fi

  info "Found PIDs on port $APP_PORT: $pids"
  # SIGTERM first — allows Kestrel to drain in-flight requests
  echo "$pids" | xargs kill -TERM 2>/dev/null || true
  local waited=0
  while [ $waited -lt 5 ]; do
    sleep 1
    waited=$((waited + 1))
    if ! lsof -ti :"$APP_PORT" >/dev/null 2>&1; then
      pass "Process exited cleanly."
      return
    fi
  done
  # Still alive after 5s — escalate to SIGKILL
  info "Process did not exit after SIGTERM; sending SIGKILL."
  lsof -ti :"$APP_PORT" 2>/dev/null | xargs kill -KILL 2>/dev/null || true
  sleep 1
  pass "Stopped."
}

# ── health poll: wait until /health returns 200 (or timeout) ─────────────────
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
  echo ""
  echo "✗ App did not respond at $url within ${max_wait}s."
  exit 1
}

# ─────────────────────────────────────────────────────────────────────────────
# Step 1 — Stop any existing instance
# ─────────────────────────────────────────────────────────────────────────────
log "Checking for running instance on port $APP_PORT..."
stop_app

# ─────────────────────────────────────────────────────────────────────────────
# Step 2 — Restore
# ─────────────────────────────────────────────────────────────────────────────
log "Restoring packages..."
dotnet restore "$SLN" --verbosity quiet
pass "Restore complete."

# ─────────────────────────────────────────────────────────────────────────────
# Step 3 — Build
# ─────────────────────────────────────────────────────────────────────────────
log "Building solution..."
dotnet build "$SLN" --no-restore --configuration Debug --verbosity quiet
pass "Build succeeded."

# ─────────────────────────────────────────────────────────────────────────────
# Step 4 — Test
# ─────────────────────────────────────────────────────────────────────────────
log "Running test suite..."
dotnet test "$SLN" --no-build --configuration Debug --verbosity quiet
pass "All tests passed."

# ─────────────────────────────────────────────────────────────────────────────
# Step 5 — Launch (mode-dependent)
# ─────────────────────────────────────────────────────────────────────────────
if [ "$MODE" = "validate" ]; then
  echo ""
  echo "✓ Validation complete. App not launched (--validate)."
  exit 0
fi

log "Launching API at $APP_URL..."

if [ "$MODE" = "start-for-tests" ]; then
  # Background mode: start the app, wait for health, then exit.
  # The caller is responsible for stopping the process when tests are done.
  ASPNETCORE_URLS="$APP_URL" \
    dotnet run --project "$API_PROJECT" --no-build --no-launch-profile \
    > /tmp/erate-workbench-app.log 2>&1 &
  APP_PID=$!
  info "Started in background (PID $APP_PID). Log: /tmp/erate-workbench-app.log"
  wait_for_health
  info "App PID $APP_PID is running. Stop it when done: kill $APP_PID"
  exit 0
fi

# Foreground mode (default): run interactively.
echo "  Press Ctrl+C to stop."
echo ""
ASPNETCORE_URLS="$APP_URL" \
  dotnet run --project "$API_PROJECT" --no-build --no-launch-profile
