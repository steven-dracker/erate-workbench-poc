# Boot Block Archive

This directory contains the historical boot block snapshots for every CC-ERATE prompt session.

## Purpose
- Provides a full audit trail of project state at each prompt boundary
- Enables recovery of context if CLAUDE.md gets corrupted or overwritten incorrectly
- Gives ChatGPT a clean starting point when resuming an architecture conversation after a long break

## Naming Convention
```
CC-ERATE-XXXXXX-handoff.md    ← end-of-session handoff (Claude Code → ChatGPT)
CC-ERATE-XXXXXX-boot.md       ← boot block snapshot at session start (optional)
```

## How to Use
- At the end of every Claude Code session, run `/handoff` and save the output here
- When starting a new ChatGPT architecture session after a break, paste the most recent handoff file as your first message
- The ChatGPT prompt: *"Here is the current project state. Resume as architect."* + paste file contents

## Current Latest
CC-ERATE-000027 — Structured logging and observability baseline
Next: CC-ERATE-000028
