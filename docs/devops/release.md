# Release Process

## Overview

Releases are produced manually via the **Release** GitHub Actions workflow (`release.yml`). CI validates every push and PR — it does not create releases. The operator decides when a commit is release-ready and triggers the release workflow explicitly.

**What a release produces:**
- A GitHub Release tagged `v{version}` with auto-generated notes from merged PRs
- A single downloadable asset: `erate-workbench-api-v{version}-linux-x64.tar.gz`
- A self-contained binary with the version stamped into the assembly (`{version}+{git-sha}`)

---

## How to create a release

### Prerequisites

- The target commit is on `main` and CI is green (all jobs passing)
- You have write access to the repository

### Steps

1. **Confirm CI is green** on the commit you intend to release. Do not trigger a release from a failing or untested commit.

2. **Go to the GitHub Actions UI:**
   `Actions → Release → Run workflow`

3. **Fill in the inputs:**

   | Input | Value | Example |
   |---|---|---|
   | **Version** | Semantic version, no `v` prefix | `0.2.0` |
   | **Pre-release** | Check if this is a preview/RC | unchecked for stable |

4. **Run the workflow.** It will:
   - Build and publish the API as a self-contained linux-x64 binary
   - Package it as `erate-workbench-api-v{version}-linux-x64.tar.gz`
   - Create a GitHub Release tagged `v{version}` with auto-generated notes
   - Attach the archive as a release asset

5. **Verify the release** at `Releases` on the repository home page. Confirm the archive is attached and the release notes look correct.

---

## Versioning model

Versions follow `MAJOR.MINOR.PATCH`:

| Segment | When to increment |
|---|---|
| `PATCH` | Bug fixes, dependency updates, minor tweaks |
| `MINOR` | New features, significant UI or data changes |
| `MAJOR` | Breaking changes to the data model, API contract, or deployment model |

For a POC, version increments are judgment calls — there is no automated semantic release tooling.

**Version in the binary:**

The released binary has `{version}+{git-sha}` stamped as its `InformationalVersion`. The app footer and the `/about` page display this version, making it easy to confirm which build is running.

```
ERATE Workbench POC · Version 0.2.0+a1b2c3d · © 2026
```

---

## Release asset

The release asset is a `.tar.gz` archive of the `dotnet publish` output directory:

```
erate-workbench-api-v{version}-linux-x64.tar.gz
├── ErateWorkbench.Api          ← executable entry point
├── ErateWorkbench.Api.dll
├── ErateWorkbench.Api.deps.json
├── ErateWorkbench.Api.runtimeconfig.json
├── *.dll                       ← bundled dependencies
└── wwwroot/                    ← static web assets
```

**No .NET runtime required on the target host** — the runtime is bundled. To run:

```bash
tar -xzf erate-workbench-api-v0.2.0-linux-x64.tar.gz -C /opt/erate-workbench
chmod +x /opt/erate-workbench/ErateWorkbench.Api
ASPNETCORE_URLS="http://0.0.0.0:5000" /opt/erate-workbench/ErateWorkbench.Api
```

---

## Release notes

The workflow uses `gh release create --generate-notes`, which automatically generates release notes from merged PR titles and authors since the previous release tag. No manual changelog is required.

For the first release (no prior tag), GitHub generates notes from all merged PRs in the repository history.

To add a custom introduction or highlight specific changes, edit the release description on GitHub after the workflow completes: `Releases → {version} → Edit`.

---

## Relationship to CI

```
CI (ci.yml)                     Release (release.yml)
─────────────────────────────   ──────────────────────────────
Runs on every push and PR       Runs on explicit manual trigger
Validates: build, tests,        Builds: self-contained binary
  smoke, security, secrets      Packages: tar.gz archive
Produces: 14-day artifact       Produces: GitHub Release + asset
Not intended for release        Only triggered after CI passes
```

The CI artifact (`erate-workbench-api` in Actions) is ephemeral and rebuild on every push. The release asset is permanent and versioned. The two are produced by independent builds — the release workflow does not download the CI artifact.

---

## Future path: deploy

The release asset is the natural input to a future `deploy` job or workflow:

```
release asset (tar.gz)
  → download to target host
  → unpack
  → configure (connection string, port)
  → start as service
```

Potential deploy targets:
- Linux VM / VPS: unpack + systemd unit
- Docker: `COPY` publish output into an `ubuntu` base image
- Azure App Service / AWS App Runner: container or zip deploy

No deploy automation is implemented yet. The release workflow provides the artifact; deploy is the next logical stage when infrastructure is defined.
