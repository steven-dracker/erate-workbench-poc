Scaffold a new Claude Code task using the CC-ERATE prompt schema.

Ask the user:
1. What is the next CC-ERATE ID? (e.g. CC-ERATE-000028)
2. What is the task name / branch name? (e.g. release-pipeline-polish)
3. What is the objective in one sentence?
4. What is the preferred implementation direction?
5. Are there any explicit things NOT to do?

Then output a complete, ready-to-use Claude Code prompt in this format:

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
[Read CLAUDE.md CURRENT STATE — Works section and list verified stable items]

## Primary Goals
1. [Goal 1]
2. [Goal 2]
3. [Goal 3]

## Requirements
[Numbered requirements — be specific about what to build]

## Design Constraints
[What NOT to do — guard rails]

## Documentation
[What docs to update]

## Validation
[How to verify the work is done correctly]

## Deliverable
Return:
- Summary of changes
- Files changed
- Approach chosen and rationale
- Validation performed
- Commit hash if committed

Use this exact prompt ID in your response: [CC-ERATE-ID]

---

Remind the user: paste this prompt into Claude Code as the first message of a new session, preceded by /resume to confirm state first.
