Generate a session handoff document for the architect (ChatGPT). Output EXACTLY this format with no preamble or extra commentary:

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

### BOOT BLOCK FIELDS TO UPDATE IN CLAUDE.md
- [ ] Boot Block ID (increment to next CC-ERATE ID)
- [ ] CURRENT STATE — Last completed
- [ ] CURRENT STATE — Branch status
- [ ] CURRENT STATE — Works (if new things verified)
- [ ] ACTIVE TASK — Goal
- [ ] KNOWN DEBT — if any items resolved or added
- [ ] Other: [list any other stale fields]

---

## NEXT PROMPT DRAFT ([next CC-ERATE ID])

Read the ACTIVE TASK section of CLAUDE.md. Using that task, generate a complete, copy-paste ready Claude Code prompt following the CC-ERATE schema exactly. Do not invent a new task — use only what is already specified in ACTIVE TASK.

The prompt MUST follow this exact structure:

---

Before starting this task:
git checkout main
git pull
git checkout -b feature/[branch-name]

[CC-ERATE-ID] — [Task title]

You are working in the erate-workbench-poc repo on branch feature/[branch-name].

## Objective
[One paragraph — what this task accomplishes and why]

## Context
The repo already includes:
[List verified stable items from CLAUDE.md CURRENT STATE — Works section]

## Primary Goals
1. [Goal 1]
2. [Goal 2]
3. [Goal 3]

## Requirements
[Numbered requirements — specific and implementable]

## Constraints
[What NOT to do — pulled from ARCHITECTURAL LAWS and NON-GOALS in CLAUDE.md]

## Validation
[How to verify the work is complete and correct]

## Deliverable
Return:
- Summary of changes
- Files changed
- Approach chosen and rationale
- Validation performed
- Commit hash if committed

Use this exact prompt ID in your response: [CC-ERATE-ID]

---

After outputting the handoff, remind the user:
1. Review the NEXT PROMPT DRAFT above — verify it matches intent before using
2. Paste handoff to ChatGPT for architect review
3. Update CLAUDE.md boot block fields listed above
4. Archive this handoff to docs/context/boot-blocks/[CC-ERATE-ID]-handoff.md
5. ChatGPT may refine the NEXT PROMPT DRAFT — always use ChatGPT's version as final
