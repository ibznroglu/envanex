---
name: gate
description: Run the project's gate script and print its summary verbatim. Invoked manually via /gate — never runs automatically.
disable-model-invocation: true
effort: low
allowed-tools: Bash(bash .claude/skills/gate/scripts/gate.sh), Bash(git status *), Bash(git diff *), Bash(grep *), Bash(cat *), Bash(ls *)
---

# Gate

Run the gate script and paste its **raw output**. The script writes every command's full output
to `TestResults/<branch-slug>/gates.txt` and prints a summary computed from that file, so the
human gets the evidence, not your reading of it.

## The one rule

**Never summarise, interpret, or judge.** Do not write "all good", "build passes", "tests green",
"looks correct", or any equivalent. Do not add a verdict line. Do not explain what the output
means. A failing gate — a `gate-exit=1` line, or an exit status of 2 or 3 — is a successful run of
this skill: print it exactly as it came back.

## Command

Run this once, from the repository root, with the Bash tool's `timeout` set to 600000:

    bash .claude/skills/gate/scripts/gate.sh

Paste its stdout verbatim in a fenced block. For details, read `gates.txt`; never retype them.

## Arguments

`$ARGUMENTS` may contain extra checks the human wants in this run — usually a `grep` over a file,
or a path to inspect. Run them after the script, under the heading `## Extra checks`, with the same
rule: print `$ <command>`, then its raw output.

## Gotchas

- `dotnet test` needs Docker running for the integration tests. If the `docker` step shows no
  container, keep going — do not start it yourself and do not warn about it.
- `grep` exits 1 when it finds nothing. That is a result, not an error. Print it as-is.
- Never run `git clean`, `git reset`, `git checkout --`, or anything else that discards work.
- Never edit a file. The script writes only `TestResults/<branch-slug>/gates.txt`.
