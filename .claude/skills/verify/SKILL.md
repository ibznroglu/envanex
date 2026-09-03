---
name: verify
description: Run the project's verification gate and print every command's raw output verbatim. Invoked manually via /verify — never runs automatically.
disable-model-invocation: true
allowed-tools: Bash(git status:*), Bash(git log:*), Bash(git branch:*), Bash(git diff:*), Bash(dotnet format:*), Bash(dotnet build:*), Bash(dotnet test:*), Bash(docker compose:*), Bash(grep:*), Bash(ls:*), Bash(cat:*)
---

# Verify

Run the checks below and paste their **raw output**. This skill exists because summarised
verification is worthless — the human needs the evidence, not your reading of it.

## The one rule

**Never summarise, interpret, or judge.** Do not write "all good", "build passes", "tests green",
"looks correct", or any equivalent. Do not add a verdict line. Do not explain what the output
means. Print the command, print its output, move to the next one. The human draws the conclusion.

If a command fails, print its failure exactly as it came back. A failure is a successful run of
this skill.

## Commands

Run these in order, from the repository root. Print each command before its output.

```bash
git branch --show-current
git status --short
git log --oneline -5
docker compose ps
dotnet format --verify-no-changes
dotnet build -warnaserror 2>&1 | tail -5
dotnet test 2>&1 | grep -E "Passed!|Failed!|error|Error"
```

## Arguments

`$ARGUMENTS` may contain extra checks the human wants in this run — usually a `grep` over a file,
or a path to inspect. Run them after the standard set, under a heading, with the same rule: raw
output only.

## Output format

    $ <command>
    <raw output>

    $ <command>
    <raw output>

Nothing before the first command. Nothing after the last one.

## Gotchas

- `dotnet test` needs Docker running for the integration tests. If `docker compose ps` shows no
  container, print that and keep going — do not start it yourself and do not warn about it.
- `grep` exits 1 when it finds nothing. That is a result, not an error. Print it as-is.
- Never run `git clean`, `git reset`, `git checkout --`, or anything else that discards work.
- Never edit a file. This skill only reads and reports.
