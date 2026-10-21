Verdict: SECURITY_CLEAR
Reviewer: /security-review
Scope: whole branch
Range: 4f0fa302b409402715fcd21c9cef01c2c2d00a7d..0296c131db2686cc71fea754d26158f67b16d7c0
Head: 0296c131db2686cc71fea754d26158f67b16d7c0

# Security Review: `chore/agents-workflow-skills` (`4f0fa30..0296c13`)

**No vulnerabilities met the reporting bar** (HIGH or MEDIUM, confidence ≥ 8).

Step 1 found no candidate findings, so step 2 had nothing to filter.

## Reviewed and rejected

1. **`.claude/settings.json:49-51`: new allow entries that run the mutate scripts without a permission prompt.** In node mode `mutate-run.sh` runs `node --test "$4"`; in dotnet mode it runs `dotnet test "$5" --filter "$4"`. Whoever writes the command line can therefore run code, but this grants no new privilege:
   - `Bash(dotnet test *)` and `Bash(dotnet run *)` were already allowed without a prompt.
   - The agents that can call these scripts already have Write and Edit.
   - `coder-bash-guard.js` still scans the whole command string.
   - The scripts themselves run only read-only git and bypass no deny rule.
2. **`gate.sh:374`, `eval "$cmd"`:** every call site passes a hard-coded literal (lines 488-497). The script takes no arguments, and the branch name is only printed with `printf '%s'`, so there is no injection path.
3. **Path traversal through a label, slug or file argument:**
   - Labels are restricted to `[A-Za-z0-9][A-Za-z0-9._-]*` (`mutate-clean.sh:621-627`, `mutate-run.sh:754-760`).
   - The slug is the branch name with `/` replaced, and git ref names cannot contain `..`.
   - File arguments follow `--` and must be tracked files (`git ls-files --error-unmatch`).
4. **Secrets in logs:** the gate and mutation logs hold only build, test and `docker compose ps` output. None of the new scripts print the connection string or the environment.
5. **`.claude/skills/pr/SKILL.md` allowed-tools:**
   - `Bash(git push *)` and `Bash(gh pr *)` existed before, as `git push:*` and `gh pr:*`. Nothing was widened; the merge is constrained further with `--match-head-commit`.
   - `disable-model-invocation: true` is kept, and the force-push deny rules still apply.
6. **`.claude/skills/commit/SKILL.md`:**
   - Permissions are narrower (`git commit -F TestResults/COMMIT_MSG.txt`, `git switch -c *`).
   - `git reset --soft HEAD~1` cannot destroy work and is limited to an unpushed commit made by the same invocation.
7. **Deleted `.claude/skills/verify/SKILL.md`:** it only ran read-only checks, and `/gate` replaces it. No guard depended on it.
8. **`.claude/agents/tester.md`, the gate.sh exception:** the tester may run gate.sh only as the exact, argument-free allowlist string (`bash-allowlist.js:36`). The script writes `TestResults/<slug>/gates.txt` and the usual `bin/` and `obj/` build output, and the tester can pass it no input.

Documentation and record files (`CLAUDE.md`, `docs/**`, `thoughts/**`) are excluded as documentation.
