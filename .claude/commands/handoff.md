Generate a session handoff document for the architect (ChatGPT). This is used to hand off from Claude Code back to the architecture conversation.

Output EXACTLY this format with no preamble or extra commentary:

---

## SESSION HANDOFF — [current CC-ERATE ID] — [today's date]

### COMPLETED THIS SESSION
- [bullet: what was built, fixed, or changed — one line each, be specific]

### CURRENT STATE
- Works end-to-end:
- Partial / incomplete:
- Tests passing: [yes / no / partial — specify which if partial]
- Branch status: [branch name, whether PR is open, whether merged]

### UNEXPECTED DISCOVERIES
- [anything that changed the picture — schema surprises, API behavior, build issues, etc.]
- [write NONE if nothing unexpected]

### DECISIONS I MADE AUTONOMOUSLY (needs architect review)
- [any architectural or design choices Claude made without explicit instruction]
- [write NONE if all work was explicitly directed]

### WHAT THE NEXT PROMPT SHOULD DO
- Recommended next CC-ERATE ID: [increment by 1]
- Recommended task: [one sentence description]
- Why: [one sentence rationale]

### BOOT BLOCK FIELDS TO UPDATE IN CLAUDE.md
- [ ] Boot Block ID (increment to next CC-ERATE ID)
- [ ] CURRENT STATE — Last completed
- [ ] CURRENT STATE — Branch status
- [ ] CURRENT STATE — Works (if new things verified)
- [ ] ACTIVE TASK — Goal
- [ ] KNOWN DEBT — if any items resolved or added
- [ ] Other: [list any other stale fields]

---

After outputting the handoff, remind the user:
1. Copy this into ChatGPT to resume the architecture conversation
2. Update CLAUDE.md boot block fields listed above before the next Claude Code session
3. Archive this handoff to docs/context/boot-blocks/[CC-ERATE-ID]-handoff.md
