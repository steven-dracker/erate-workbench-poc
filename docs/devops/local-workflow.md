# Local Development Workflow

## Scripts

### `scripts/dev-run.sh`

The primary local entry point. Runs stop → restore → build → unit tests → (optional) app launch.

| Mode | Command | What it does |
|---|---|---|
| Validate only | `./scripts/dev-run.sh --validate` | Restore, build, run unit tests. No app launch. |
| Run (foreground) | `./scripts/dev-run.sh` | Full pipeline + launch app in foreground. Press `Ctrl+C` to stop. |
| Start for tests | `./scripts/dev-run.sh --start-for-tests` | Full pipeline + start app in background, wait for `/health`, exit. App stays running. |

**Port:** defaults to 5000. Override with `APP_PORT=8080 ./scripts/dev-run.sh`.

**Stop behavior:** On each run, `dev-run.sh` first stops any process already bound to the target port using `SIGTERM` (graceful), falling back to `SIGKILL` after 5 seconds if needed.

---

### `scripts/ui-test.sh`

Runs the Playwright UI smoke suite. Manages app lifecycle unless told the app is already up.

| Mode | Command | What it does |
|---|---|---|
| Full (start + test + stop) | `./scripts/ui-test.sh` | Starts app in background, waits for health, runs UI tests, stops app. |
| App already running | `./scripts/ui-test.sh --app-running` | Skips startup. Runs tests only. Leaves app running. |

**Port:** defaults to 5000. Override with `APP_PORT=8080 ./scripts/ui-test.sh`.

App log is written to `/tmp/erate-workbench-app.log` when started by this script.

---

## One-time setup

### Playwright browser dependencies (WSL / bare Ubuntu)

Playwright requires system libraries not installed by default in WSL:

```bash
sudo apt-get install -y libnss3 libnspr4 libasound2t64
```

Install the Playwright CLI and Chromium browser:

```bash
dotnet tool install --global Microsoft.Playwright.CLI
~/.dotnet/tools/playwright install chromium
```

This is only needed once per machine. GitHub Actions CI handles it automatically.

---

## Health endpoint

The app exposes `GET /health` which returns:

```json
{"status": "ok"}
```

This endpoint is used by both `dev-run.sh` (`--start-for-tests` mode) and `ui-test.sh` to confirm the app is ready before running tests. It is also the readiness gate in the CI pipeline.

---

## Typical developer loop

```
edit code
  → ./scripts/dev-run.sh --validate   # fast check: restore/build/unit tests
  → ./scripts/dev-run.sh              # full: run the app and manually verify
  → ./scripts/ui-test.sh              # browser smoke: Playwright happy-path tests
  → git commit / push                 # CI runs the full pipeline automatically
```

For rapid iteration during development, run `--validate` frequently and launch the app only when you need to inspect UI behavior.

---

## Running only unit tests

If you want to skip the stop/restore/build steps and run unit tests directly:

```bash
dotnet test tests/ErateWorkbench.Tests/ErateWorkbench.Tests.csproj
```

---

## Running only UI tests (manual control)

Start the app in one terminal:

```bash
./scripts/dev-run.sh --start-for-tests
```

Run UI tests in another:

```bash
./scripts/ui-test.sh --app-running
```

Stop the app when done:

```bash
kill <PID printed by dev-run.sh>
```
