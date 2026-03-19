#!/usr/bin/env bash
# scripts/dev-run.sh
# Local developer workflow: stop → restore → build → test → run
#
# Usage:
#   ./scripts/dev-run.sh            # full validate-and-run
#   ./scripts/dev-run.sh --no-run   # validate only, skip launch

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SLN="$REPO_ROOT/ErateWorkbench.sln"
API_PROJECT="$REPO_ROOT/src/ErateWorkbench.Api/ErateWorkbench.Api.csproj"
LAUNCH_ARGS="--no-launch-profile"   # bypass launchSettings.json; binds on port 5000
NO_RUN=false

# ── argument parsing ─────────────────────────────────────────────────────────
for arg in "$@"; do
  case $arg in
    --no-run) NO_RUN=true ;;
    *) echo "Unknown argument: $arg" && exit 1 ;;
  esac
done

# ── helpers ──────────────────────────────────────────────────────────────────
log()  { echo ""; echo "▶ $*"; }
pass() { echo "  ✓ $*"; }

# ── step 1: stop any running local instance ──────────────────────────────────
log "Checking for running app instance on port 5000..."
# Narrow: only kill processes that have bound port 5000 (dotnet/kestrel).
# Tradeoff: this approach kills any process holding port 5000, not just our app.
# That is acceptable for a local dev workflow where only this app should use 5000.
PIDS=$(lsof -ti :5000 2>/dev/null || true)
if [ -n "$PIDS" ]; then
  echo "  Stopping PIDs: $PIDS"
  echo "$PIDS" | xargs kill -9
  sleep 1
  pass "Stopped."
else
  pass "No running instance found."
fi

# ── step 2: restore ──────────────────────────────────────────────────────────
log "Restoring packages..."
dotnet restore "$SLN" --verbosity quiet
pass "Restore complete."

# ── step 3: build ────────────────────────────────────────────────────────────
log "Building solution..."
dotnet build "$SLN" --no-restore --configuration Debug --verbosity quiet
pass "Build succeeded."

# ── step 4: test ─────────────────────────────────────────────────────────────
log "Running test suite..."
dotnet test "$SLN" --no-build --configuration Debug --verbosity quiet
pass "All tests passed."

# ── step 5: launch ────────────────────────────────────────────────────────────
if [ "$NO_RUN" = true ]; then
  echo ""
  echo "✓ Validation complete. Skipping app launch (--no-run)."
  exit 0
fi

log "Launching API (port 5000)..."
echo "  Press Ctrl+C to stop."
echo ""
dotnet run --project "$API_PROJECT" --no-build $LAUNCH_ARGS
