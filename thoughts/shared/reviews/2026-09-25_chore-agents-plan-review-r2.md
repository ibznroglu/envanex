Verdict: NEEDS_REVISION
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-09-25_chore-agents-hardening.md (Revision 1)
Range: f6df221d70faab6dc629b761b30b7479489d5b3c..30b7972023aff493425312223aa8418f8d5b2298

## Verdict
NEEDS_REVISION

Plan: C:\projects\envanex\thoughts\shared\plans\2026-09-25_chore-agents-hardening.md (Revision 1)
Previous review: C:\projects\envanex\thoughts\shared\reviews\2026-09-25_chore-agents-plan-review.md

Revision 1 does what the first review asked. All 14 required changes and all five rulings are in the sections its table names. But three of the new rules conflict with steps the plan already has. The new redaction and allowlist rules also leave gaps that are specific to this repo. The fixes are all small.

## Rulings on the six deviations

- **A. Reviewer Head (Plan:64, rule R at Plan:853): accepted.** A strict "equals HEAD" check can never pass once the review file is committed, so this is the right fix. One gap: nothing says what happens when `git diff --name-only <Head> HEAD` errors, for example on an unknown or mistyped SHA. That must count as a failure (required change 8).
- **B. db-review Head as an ancestor (Plan:65, 873): accepted, with one condition.** The ancestor check alone lets a later schema phase with no db-review pass. Precondition 7 only needs "at least one" file, and one early `DB_APPROVED` satisfies it. CLAUDE.md calls a skipped db-review on a schema-touching phase a defect. Required change 5 adds the condition.
- **C. Gates-sha256 recorded but not compared (Plan:66, 857): accepted.** It is logical, because `/gate` re-runs after the tester. But it drops the tester's dirty-tree check from the final gate run. Precondition 3 only checks `head=` and `gate-exit`. It must also check the `status` section (required change 6).
- **D. The tester verdict file is required (Plan:67, 858): accepted.** pr/SKILL.md:10 still says "verify all four". It has to become seven (required change 8).
- **E. The human sets the security-review `Verdict:` (Plan:68, 872, 963): accepted, with one clarification.** The plan never says what the main session writes on that line when it saves the file. If it's left to guess, the main session can write `SECURITY_CLEAR` itself (required change 7).
- **F. Additions: accepted.**
  - `LIVEPROBE` tags and the case-insensitive git match are both sound.
  - I checked the expected red for each new mutation, and each one works:
    - M1.5: the test calls `logDecision` directly.
    - M1.6: the test checks stderr.
    - M1.7: the project dir is temp dir A, so the file lands under A.
    - M2.6: string-prefix matching of `node --test` lets the traversal through.
    - M2.7: string-prefix matching lets `difftool` through.

## Findings

1. **Plan:129 with Plan:560 and Plan:883: the per-phase order can never pass.** This comes from putting required change 12 together with the new rule that reviewers write files.
   - Step 1.9 runs code-reviewer and then the tester "on that commit". Nothing commits the review file in between.
   - The review file is untracked and not gitignored, so the `status` section of `gates.txt` shows `?? thoughts/shared/reviews/…`.
   - The tester then returns NEEDS_FIXES by rule (Plan:883).
   - The next phase's clean-tree precondition (Plan:132) fails too.
   - The PR-end sequence has this commit (Plan:964, 1030). The per-phase sequence doesn't.

2. **Plan:433 with Plan:484 and Plan:742/930/1039: the coder can't run its own validation from Phase 2 on.**
   - The Phase 1 block contains `$(git merge-base main HEAD)`.
   - coder-bash-guard scans the whole string, finds `git`, and takes `merge-base` as the next token. `merge-base` is not in the read allowlist, so the command is blocked.
   - Phases 2-4 say "The Phase 1 block, plus", so every later coder turn hits this.

3. **Plan:224-227: this repo's own command forms get past the redaction.**
   - **(a) Quoted values.** `(password|pwd)\s*=\s*[^;'"\s]+` can't match a value that starts with a quote. `export MSSQL_SA_PASSWORD='…'` or `SA_PASSWORD="…"` is logged in clear. `.env.example:21` and `docker-compose.yml:8` use exactly this variable.
   - **(b) The sqlcmd `-P` value.** `docker-compose.yml:16` shows `sqlcmd … -P "<pw>"`. A `docker exec … sqlcmd -P …` health check is a likely command, and no pattern covers `-P`.
   - **(c) The user-secrets rule makes the coder guess.** "options of the form `--opt value` or `-p value` are skipped" leaves three cases open:
     - `-v`/`--verbose`, which take no value, so the key gets skipped and the value is missed
     - options placed before `set`
     - the `--opt=value` form

4. **Plan:223 and Plan:239 with Plan:476: the Bash hooks have no target source.**
   - `getTargetPath` reads only `file_path ?? path ?? notebook_path`, and it throws when the result is empty.
   - `runGuard` builds `ctx.target`, but the plan doesn't say where the target comes from for a Bash hook.
   - Taken literally, every Bash hook fails closed on every command. The Phase 2 tests would show this, so it isn't silent, but the coder still has to guess the fix.

5. **Plan:494 and Plan:501: `dotnet build` and `dotnet test` are prefix entries, so the tester's "read-only" profile can still run arbitrary commands and write anywhere.** This is the same class as the `node --test` traversal the first review closed.
   - `dotnet build -p:PreBuildEvent=<cmd>` (or `-p:PostBuildEvent`) runs `<cmd>` from MSBuild's PreBuildEvent target. UNVERIFIED. To settle it, run `dotnet build -p:PreBuildEvent="echo PWNED"` on a scratch project and look for `PWNED`.
   - `dotnet build -o src/x`, `dotnet test --results-directory src`, `-bl` and `dotnet format --verify-no-changes --report <path>` all write files outside `TestResults/`.

6. **Plan:873: precondition 7 needs at least one db-review file, but not one for every schema change** (ruling B).

7. **Plan:857: precondition 3 ignores the `status` section of the final `gates.txt`** (ruling C).

8. **Plan:862-872 and Plan:963: the placeholder the main session writes on the security-review `Verdict:` line is not specified** (ruling E).

9. **Plan:216-222 and Plan:985-993: Windows path aliasing is not listed as a residual risk.** This is documentation only. The pure-string `normalizePath` doesn't handle Win32's stripping of trailing `.` or space (`obj.\x`) or NTFS stream suffixes (`.env::$DATA`). Either one could alias a protected path. UNVERIFIED whether Claude Code's Write tool reaches Win32 path normalization. To settle it, Write `TestResults\x.` and then `ls TestResults`.

10. **Minor points:**
    - **Plan:794:** does `command -v a b c` fail when any one name is missing? UNVERIFIED. Settle it with `command -v git nosuchcmd; echo $?` in Git Bash, or loop over the names so the question doesn't arise.
    - **Plan:789:** `final_status` should match the full `^exit=[0-9]+ step=\S+ kind=required$`, so a step output line that starts with `exit=` isn't counted.
    - **Plan:851:** the `*_<slug>-*.md` glob also matches a longer slug (`<slug>-2-…`). Rule R blocks this in practice. Not blocking.

## What passed

- **Carried out:** required changes 1-14 and point rulings 1-5 are each in the section the Revision 1 table names. I checked every row.
- **normalizePath (Plan:216-222):**
  - It is fully specified and pure string logic.
  - I traced it by hand:
    - `/tmp/x` is not mapped to a drive.
    - `c:/../x` becomes `c:/x`.
    - UNC paths and `\\?\` paths fail closed in write-scope.
    - The three forms of `CLAUDE_PROJECT_DIR` come out the same.
- **Frontmatter YAML (Plan:519-537):** the nesting is correct, and the single-quoted command holding double quotes is valid YAML. Whether `claude-sonnet-5` is a valid model ID is UNVERIFIED; P2.1 already settles it.
- **Base check (Plan:433, 796):** it matches ruling 2. `$?` after `a && b; echo` reports the status of the whole list. A missing local `main` makes the two merge-bases differ, so the check fails.
- **coder-bash-guard:**
  - `\b` handles `.gitignore`, `.github` and `digit` correctly.
  - `"git status"` resolves to `status` once the trailing quote is stripped.
  - `--git-dir=` fails at step 2.
- **Reviewer header (Plan:562-568) and `/pr` allowed-tools (Plan:848):** they cover `merge-base --is-ancestor` and `diff <Head> HEAD`.
- **Nothing reopened:** Revision 1 undoes nothing the first review cleared.

## Required changes

1. **Probe protocol step 1 (Plan:120-131).** Between code-reviewer and tester, add a step: "the human commits the review file". This matches PR-end step 3 (Plan:964). Say that the tester's Head is that commit.
2. **coder-bash-guard read allowlist (Plan:484).** Add `merge-base`, and add the test `allows git merge-base main HEAD` (Plan:616-617). Alternatively, remove the base-check line from the coder's validation and say the coder relies on `gate.sh`. Choose one.
3. **`redactSecrets` (Plan:224-227).**
   - **(a) Quoted values.** Widen the value part of the password pattern to `(?:'[^']*'|"[^"]*"|[^;'"\s]+)`.
   - **(b) sqlcmd `-P`.** Add: in a command containing `sqlcmd`, redact the token after `-P`, quoted or not.
   - **(c) user-secrets.** Replace the option-skipping rule with a simpler one: redact everything after `user-secrets set` up to the next `&&`, `;`, `|`, newline or end of string. Losing the key from the log is harmless.
   - **Tests:**
     - `redacts a quoted MSSQL_SA_PASSWORD assignment`, using `export MSSQL_SA_PASSWORD='S3cretValue4'`
     - `redacts the sqlcmd -P value`
     - `redacts user-secrets set with a --verbose flag before the key`
4. **Targets for Bash hooks (Plan:223, 239).** Specify that the target for Bash-matcher hooks is `tool_input.command`. Either extend `getTargetPath` with `?? tool_input.command`, or give `runGuard` a target extractor. Add a guard-common test for a Bash-shaped input.
5. **Precondition 7 (Plan:873).** Add: no path under `db/`, `src/Envanex.Infrastructure/Migrations/` or `src/Envanex.Infrastructure/Persistence/` may appear in `git diff --name-only <newest db-review Head> HEAD`.
6. **Precondition 3 (Plan:857).** Also require the `status` section of `gates.txt` to be empty.
7. **Security-review file (Plan:862-872, 963).** When it saves the file, the main session writes `Verdict: PENDING`. Only the human changes it to `SECURITY_CLEAR` or `SECURITY_FINDINGS`. `/pr` already rejects anything else. State that.
8. **pr/SKILL.md edits (Plan:849-878).**
   - The precondition heading "verify all four" (`.claude/skills/pr/SKILL.md:10`) becomes seven.
   - Rule R: a `git diff` or `merge-base` error, including an unknown SHA, fails the precondition.
9. **bash-allowlist dotnet entries (Plan:501).**
   - Replace the prefix entries `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` with a fixed list of allowed further tokens: `-warnaserror`, `--no-restore`, `--filter <arg>`, `-v|--verbosity <arg>`, a `src/…` or `tests/…` project path, and `2>&1`. Anything else blocks.
   - Add the tests:
     - `blocks dotnet build -p:PreBuildEvent=x`
     - `blocks dotnet test --results-directory src`
     - `blocks dotnet format --verify-no-changes --report x`
   - Add a mutation proof that allows any extra token.
   - Mark the PreBuildEvent exec UNVERIFIED, with the settling command from finding 5.
10. **Residual risks (Plan:985-993, 1013).** Add the Windows path-aliasing bullet from finding 9, marked UNVERIFIED.
11. **Minor fixes, at the planner's discretion:**
    - For `tools-check`, loop over the names or record the result of the `command -v` check.
    - Anchor the `final_status` regex to the full `exit=` line format.

Verdict: NEEDS_REVISION
