Update the CLAUDE.md boot block to reflect the current state of the project. 

Ask the user the following questions one at a time, then generate the updated boot block section:

1. What is the new CC-ERATE prompt ID? (increment from current)
2. What was the last completed task? (one line)
3. What is the current branch status? (branch name + PR status)
4. Did anything change in what "works"? (new verified features)
5. Is there anything new that is partial or broken?
6. What is the recommended next task?
7. Were any tech debt items resolved or added?

After collecting answers, output the complete updated BOOT BLOCK section formatted exactly as it appears in CLAUDE.md — ready to paste in as a replacement.

Remind the user to:
1. Replace the BOOT BLOCK section in CLAUDE.md with this output
2. Update the "Last updated" date at the top of CLAUDE.md
3. Archive the previous boot block to docs/context/boot-blocks/
