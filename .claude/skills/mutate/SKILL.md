---
name: mutate
description: Run one mutation proof against committed code and log it under TestResults. Invoked manually via /mutate — never runs automatically.
disable-model-invocation: true
allowed-tools: Read, Edit, Bash(bash .claude/skills/mutate/scripts/mutate-run.sh *), Bash(bash .claude/skills/mutate/scripts/mutate-clean.sh *)
---

# Mutate

Prove that a test protects a behaviour: break the code on purpose, watch the test go red, restore
the code, and prove the restore. Every step's raw output goes to
`TestResults/<branch-slug>/mutations/<label>.txt`.

`$ARGUMENTS`: a label, the target file, the mode (`dotnet "<filter>" [<project>]` or
`node <test-file>`), and the mutation to apply.

## Procedure

0. `bash .claude/skills/mutate/scripts/mutate-clean.sh <label> <file>` must exit 0. It compares the
   file with HEAD, both staged and unstaged. Mutations run only against committed code; otherwise
   STOP.
1. Apply the mutation with Edit.
2. `bash .claude/skills/mutate/scripts/mutate-run.sh <label> <file> <mode arguments>`, with the Bash
   tool's `timeout` set to 600000.
3. Apply the inverse Edit.
4. `bash .claude/skills/mutate/scripts/mutate-clean.sh <label> <file>` must exit 0. If it does not,
   STOP and tell the human. Never use `git checkout --`.

## Rules

- no constant the compiler folds (CS0162 under warnings-as-errors)
- never `--no-build`
- a red counts only when the test's own assertion fails; infrastructure failures (for example a Docker API 500) are rerun and logged as `<label>-infra-N`
- report the failing assertion for every red
- restore only with the inverse Edit, never `git checkout --` (denied)

## Report

The log path; every failing test and assertion line that `mutate-run.sh` printed; the `exit=`
lines of both clean checks. Raw output only.

## Gotchas

- Mutating a file under `.claude/hooks/` changes a live guard for every tool call until the
  inverse Edit. If a mutation blocks the inverse Edit, stop and tell the human, who restores the
  file outside Claude Code.
- Integration-test filters need Docker running.
- A coder turn never invokes `/mutate`; it calls the two scripts directly (see `coder.md`).
