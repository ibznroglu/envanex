---
name: commit
description: Stage and commit changes on the current feature branch, following the project's commit conventions. Invoked manually via /commit — never runs automatically. Does NOT push and does NOT touch main.
disable-model-invocation: true
allowed-tools: Bash(git status:*), Bash(git diff:*), Bash(git add:*), Bash(git commit:*), Bash(git log:*), Bash(git branch:*), Bash(git switch:*)
---

# Commit (feature branch only)

## Procedure

1. Run `git branch --show-current`. **If the current branch is `main`, STOP.** Tell the user which
   branch name you would create (`feat/<slug>`, `fix/<slug>` or `chore/<slug>`, derived from the
   active plan file name) and ask them to confirm before running `git switch -c`.
2. Run `git status --short` and `git diff --stat`. Show the user the list of changed files.
3. Stage only the intended files. Never blindly `git add -A` — if an unexpected file appears,
   ask the user before staging it.
4. Write a Conventional Commit message: `type(scope): summary`, plus a bullet body for
   non-trivial changes. English only.
5. Commit. Do NOT push.
6. Report the hash and the branch name.

## Conventions

- Commits inside a feature branch should be **atomic**: one logical change each. They exist to
  make the PR readable. Because the PR is squash-merged, they will collapse — so favor clarity
  over ceremony, and do not agonize over splitting.
- Do keep code and docs/plans in separate commits when both changed. It makes the diff readable.
- Types: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`.
- Scopes: `domain`, `app`, `infra`, `api`, `soap`, `worker`, `web`, `db`, `ci`, `agents`.

## Rules

- NEVER commit automatically or mid-phase — only when the user explicitly runs `/commit`.
- NEVER commit directly on `main`.
- NEVER push. Pushing and merging is `/pr`.
- If unsure which files belong in the commit, ask before staging.
