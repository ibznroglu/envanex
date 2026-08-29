---
name: pr
description: Push the current feature branch, open a pull request, wait for CI, squash-merge it, and return to an updated main. Invoked manually via /pr — never runs automatically.
disable-model-invocation: true
allowed-tools: Bash(git status:*), Bash(git log:*), Bash(git diff:*), Bash(git push:*), Bash(git switch:*), Bash(git pull:*), Bash(git branch:*), Bash(gh pr:*), Bash(gh run:*)
---

# Open and squash-merge a PR

## Preconditions — verify all four, STOP if any fails

1. Current branch is NOT `main`.
2. Working tree is clean (`git status --short` is empty).
3. The tester agent returned `READY_TO_PUSH` in this session. If it did not, say so and stop.
   "It builds" is not a substitute for the verdict.
4. If the phase touched schema, migrations or queries, db-reviewer returned `DB_APPROVED`.

## Procedure

1. `git push -u origin <branch>`.
2. Compose the PR **title** as a Conventional Commit: `type(scope): summary`. This is the single
   commit that will land on `main` after squash — it matters more than any commit inside the branch.
3. Compose the PR **body** from the active plan file:

   ```
   ## What
   (one paragraph)

   ## Why
   (link the plan: thoughts/shared/plans/YYYY-MM-DD_topic.md)

   ## Phases included
   ## Schema changes
   (migration name, or "none")
   ## Verdicts
   - plan-reviewer: APPROVED
   - code-reviewer: APPROVED
   - db-reviewer: DB_APPROVED | n/a
   - tester: READY_TO_PUSH
   ## Validation
   (commands run + result)
   ## ADR
   (docs/adr/NNNN-title.md — or "none")
   ```

4. `gh pr create --title "<title>" --body "<body>" --base main`.
5. Watch CI: `gh pr checks --watch`. If CI fails, report the failure with file:line and STOP —
   the human routes the fix back to the coder. Do not fix it yourself.
6. On green CI: `gh pr merge --squash --delete-branch`.
7. `git switch main && git pull`.
8. Report the PR number, the squashed commit hash on `main`, and confirm the tree is clean.

## Rules

- NEVER merge with red or pending CI.
- NEVER force-push.
- NEVER merge a PR whose body claims a verdict that did not actually happen in this session.
- If the ADR file listed in the plan does not exist yet, STOP and tell the user to write it.
  The agents do not write ADRs.
