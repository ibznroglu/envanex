---
name: commit
description: Stage and commit changes on the current feature branch, following the project's commit conventions. Invoked manually via /commit — never runs automatically. Does NOT push and does NOT touch main.
disable-model-invocation: true
effort: low
allowed-tools: Read, Write, Bash(git status *), Bash(git diff *), Bash(git add *), Bash(git commit -F TestResults/COMMIT_MSG.txt), Bash(git log *), Bash(git branch --show-current), Bash(git switch -c *), Bash(git reset --soft HEAD~1)
---

# Commit (feature branch only)

## Procedure

1. Run `git branch --show-current`. **If the current branch is `main`, STOP.** Tell the user which
   branch name you would create (`feat/<slug>`, `fix/<slug>` or `chore/<slug>`, derived from the
   active plan file name) and ask them to confirm before running `git switch -c`.
2. Run `git status --short` and `git diff --stat`. Show the user the list of changed files.
3. Stage only the intended files with `git add <path>...`. Never blindly `git add -A` — if an
   unexpected file appears, ask the user before staging it. Unstaging is the user's job, because
   `git restore` is denied.
4. Write the message to `TestResults/COMMIT_MSG.txt` with the Write tool: a Conventional Commit
   `type(scope): summary`, plus a bullet body for non-trivial changes. English only. If the file
   already exists, Read it first; the Write tool refuses to overwrite a file it has not read.
5. Commit with `git commit -F TestResults/COMMIT_MSG.txt` through the **Bash** tool — never
   PowerShell, never `-m`. Do NOT push.
6. Print `git log -1 --format=%B` raw, then report the hash and the branch name.

## Conventions

- Commits inside a feature branch should be **atomic**: one logical change each. They exist to
  make the PR readable. Because the PR is squash-merged, they will collapse — so favor clarity
  over ceremony, and do not agonize over splitting.
- Do keep code and docs/plans in separate commits when both changed. It makes the diff readable.
- Types: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`.
- Scopes: `domain`, `app`, `infra`, `api`, `soap`, `worker`, `web`, `db`, `ci`, `agents`, `auth`,
  `solution`, `roadmap`, `adr`, `journal`.

## Rules

- NEVER commit automatically or mid-phase — only when the user explicitly runs `/commit`.
- NEVER commit directly on `main`.
- NEVER push. Pushing and merging is `/pr`.
- If unsure which files belong in the commit, ask before staging.
- `git reset --soft HEAD~1` is allowed only on a commit this invocation made, and only while
  `git status -sb` shows it unpushed (no upstream, or `ahead`).
