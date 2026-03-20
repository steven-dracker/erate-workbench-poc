You are resuming work on ERATE Workbench. Read CLAUDE.md now if you have not already — it is your source of truth.

Confirm your understanding by outputting EXACTLY this status check with no preamble:

---

## SESSION RESUME — [today's date]

### I UNDERSTAND THE FOLLOWING
- Current boot block ID: [from CLAUDE.md]
- Last completed task: [from CLAUDE.md]
- Active task / next prompt: [from CLAUDE.md]
- Branch status: [from CLAUDE.md — is there an open branch that needs a PR?]

### PENDING ACTIONS BEFORE NEW WORK
- [ ] [List any open branches, unpushed commits, or open PRs mentioned in CLAUDE.md — these must be resolved first]
- [ ] [Write NONE if CLAUDE.md shows a clean state]

### READY TO START
- Confirmed: I will not commit directly to main
- Confirmed: I will use the next CC-ERATE ID for this session's work
- Confirmed: WSL is the canonical environment
- Confirmed: Feature branch → push → PR → CI → merge → delete → sync main

What would you like me to work on? (Or say "proceed with active task" to start the recommended next task from CLAUDE.md.)

---
