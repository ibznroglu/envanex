Verdict: NEEDS_REVISION
Reviewer: code-reviewer
Scope: Phase 1 of thoughts/shared/plans/2026-09-25_chore-agents-hardening.md
Range: f6df221d70faab6dc629b761b30b7479489d5b3c..de44869ad1b9fcb8f5cd092cb0c2b9c638ec210d

## Verdict
NEEDS_REVISION

I checked the implementation against the plan's Phase 1 spec (plan lines 264-540). The files match it: every signature, the exact allow list and the 40 deny entries, the hook command, the matcher and all 57 listed tests. Fail-closed behaviour holds on every error path I traced:
- empty or malformed stdin, and `null` or `[]` input
- a missing or relative project dir
- a missing, empty or non-string target
- a throw inside `decide`
- a missing node binary (exit 127, turned into 2 by `|| exit 2`)
- a missing script (P1.10)
- a crash inside the catch handler (exit 1, turned into 2)

Redaction runs before the only log write (`logDecision` redacts `target` and `reason`; the other fields are metadata) and before the only stderr write (`block` redacts the whole message).

There are two reasons for the verdict:
- One implementation-level test defect, which is the M1.1 lesson again.
- Several plan-level gaps where an allow entry reopens what a deny entry or the path guard closes, or where a deny pattern misses forms people commonly type. Those need the planner.

## Findings

**Implementation (coder-level)**

1. `C:\projects\envanex\.claude\hooks\tests\path-guard.test.js:129-136`. `blocks on malformed JSON` and `blocks on an empty file path` assert only the generic prefix `guard error`, so they can pass on the wrong guard error. Concretely, under the first M1.1 mutation (dropping `\` → `/` in `toSlashForm`), `blocks on an empty file path` stays green: it fails closed on "project dir is not absolute", not on the empty path. This contradicts the As-built claim at plan line 990 ("Every block test now asserts its reason too"). **Medium.**

2. `C:\projects\envanex\.claude\hooks\lib\guard-common.js:229-230`. `decide(ctx.input, ctx); allow(ctx);` has no `await`. `decide` is sync today, so nothing is broken yet. But this is the shared entry point that Phase 2's four guards will use. If any of them has an async `decide`, `allow` calls `process.exit(0)` before that promise can settle or reject, so the guard fails open. Writing `await decide(...)` costs nothing. **Medium (latent).**

3. `C:\projects\envanex\.claude\hooks\lib\guard-common.js:105` and `C:\projects\envanex\.claude\settings.json:96`. The `NotebookEdit` matcher and the `notebook_path` extraction are new behaviour with no test and no probe. If the `?? toolInput.notebook_path` fallback were removed, no test would go red; every notebook edit would then block with "target path is missing" (it fails closed, but the defect would go unnoticed). **Low-Medium.**

4. `C:\projects\envanex\.claude\hooks\lib\guard-common.js:165`. Errors raised before `normalizeProjectDir` finishes (malformed or empty stdin) leave `ctx.projectDir` undefined. `resolveLogDir` then throws, and the error is swallowed. So in production, with no `ENVANEX_HOOK_LOG_DIR`, these blocks write no log line. The decision is still correct, but the audit trail has a hole. **Low.**

5. `C:\projects\envanex\.claude\hooks\lib\guard-common.js:75,86`. `normalizeProjectDir` lowercases the project dir, and `logDecision` uses that lowercased value as the base for the fallback log dir. On a case-sensitive filesystem (the ubuntu CI step the Non-goals keep open) the log would go to the wrong directory. The log dir should be built from `projectDirRaw`. **Low.**

**Allow entries that reopen a deny (plan-level)**

6. `C:\projects\envanex\.claude\settings.json:39` (`Bash(git switch *)`) against `:65-68`. The denies `git switch -f*` and `git switch --discard-changes*` are prefix patterns. These forms slip past them:
   - `git switch --force <b>`, where `--force` is git's long form of `-f`
   - `git switch <b> -f` and `git switch <b> --discard-changes`, with the flag after the branch
   - `git switch -C <b> <start>`, which resets an existing branch

   All of them match the allow entry, so in manual mode they run without a prompt and throw away work. UNVERIFIED (rule-matcher behaviour). To settle it, in manual mode on a clean tree run `git switch --force chore/agents-hardening` (a no-op there) and see whether a prompt appears. **Medium-High.**

7. `C:\projects\envanex\.claude\settings.json:24` (`Bash(dotnet ef database update *)`) against `:89-90`. `dotnet ef database update 0` unapplies every migration, which drops every table. `update <OlderMigration>` drops everything after that migration. For data this is equivalent to the denied `database drop`, and it runs without a prompt. **Medium-High.**

8. `C:\projects\envanex\.claude\settings.json:32-35` (`git log *`, `git diff *`, `git show *`) and `:18` (`dotnet new *`). Commands allowed through Bash can write files the path guard protects:
   - `git diff --output=.env` truncates `.env`, and `--output` also works on `git log` and `git show`
   - `dotnet new gitignore --force` overwrites a tracked file
   - `dotnet new … -o obj/x` writes into `obj`

   None of these prompts. UNVERIFIED (git and dotnet option behaviour). To settle `--output`, run `git diff --output=%TEMP%\x.txt` in a scratch repo. **Medium.**

9. `C:\projects\envanex\.claude\settings.json:48` (`Bash(node --test .claude/hooks/tests/*)`). If the `*` matches `/` and `..`, which is UNVERIFIED, then `node --test .claude/hooks/tests/../../scratch/evil.js` runs any JS without a prompt. The same class already exists in `dotnet run *` (.NET 10 file-based `dotnet run x.cs`) and `dotnet test *`: any code the model writes can run without a prompt and call the denied git commands itself. This should be recorded as a residual risk, and the node entry narrowed to the two exact test files plus the literal gate command. To settle it, in manual mode run `node --test .claude/hooks/tests/../tests/path-guard.test.js` and check for a prompt. **Low.**

**Deny patterns that miss common forms (plan-level)**

10. `C:\projects\envanex\.claude\settings.json:51-90`. Every deny entry is anchored on `git <subcommand>` (or the docker/dotnet equivalent). These forms fall outside them:
    - **Global options before the subcommand:** `git -C <dir> stash`, `git -C . reset --hard`, `git -c k=v clean -fdx`, `git --no-pager …`, `docker compose -f docker-compose.yml down -v`, `docker compose -p envanex down -v`.
    - **Another binary name:** `git.exe …`, `docker-compose down -v` (the v1 binary), `dotnet-ef database drop`.
    - **Flag after a positional or after another flag:** `git reset HEAD~1 --hard` (reset has no `git reset * --hard*` counterpart like push has), `docker compose down --remove-orphans -v`.
    - **Bare form missing:** `git rebase` has no `Bash(git rebase)` entry. The plan pairs bare and `*` forms for `git stash` and `git status`, which implies ` *` does not match the bare command.
    - **Other destructive forms with no entry:**
      - `git push origin +main` (a `+refspec` force push)
      - `git checkout <ref> -- <path>`, `git checkout <path>` and `git checkout --force <b>`
      - `git branch -D`
      - `docker system prune --volumes`

    All of this is UNVERIFIED, because the claims are about how the rule matcher behaves. P1.7 shows the `&&` split works but did not test these forms. To settle it, probe each form in auto mode and check that the tool result is the permission-denied text. **Medium.**

**Redaction gaps (plan-level, matter before Phase 2 logs commands)**

11. `C:\projects\envanex\.claude\hooks\lib\guard-common.js:131,141,147`. These forms are not redacted:
    - **JSON form:** `"Password": "x"`. Rule 1 requires `=`.
    - **Space-separated form:** `--password x`.
    - **Key names rule 4 cannot match:** it requires `secret`, `token` or `api_key` immediately before `=`/`:`. So `AWS_SECRET_ACCESS_KEY=x` is missed, and so is `Jwt__SigningKey=x` / `--Jwt:SigningKey=x`, which is this repo's own key.
    - **Headers:** `Authorization: Bearer x`.
    - **URL userinfo:** `https://user:pass@host`.
    - **Line continuation:** `dotnet user-secrets set k \` followed by the value on the next line. Rule 3 stops at `\n`, so the value survives.
    - **Values that rule 1 only partly redacts:** an unterminated quote (`Password='x…` with no closing quote) is left alone, and after a mid-value `'` or an escaped `\"` the rest of the value survives.

    In Phase 1 only file paths are logged, so exposure is small today. **Medium for Phase 2.**

**Path-guard aliasing not in the residual-risk list (plan-level)**

12. `C:\projects\envanex\.claude\hooks\path-guard.js:7-8,20-31`. Checking segments and basenames holds up against UNC paths, `\\?\` and `\\.\` prefixes, `..`, case, and junk prefixes; I traced each. It cannot see filesystem-level aliases. The Non-goals (plan line 184) list only a trailing dot or space and NTFS streams. Not listed:
    - **8.3 short names:** `GIT~1`, `ENV~1.LOC`, `SETTIN~1.JSO`. UNVERIFIED; settle with `fsutil 8dot3name query C:` and `cmd /c dir /x C:\projects\envanex`.
    - **Junctions, symlinks and hard links.**
    - **Drive-relative `C:obj\x`:** it joins to `c:/projects/envanex/c:obj/x`, which passes. Claude Code appears to resolve paths before calling the hook (P1.2's log shows an absolute path), but that is not proven for this form.
    - **Case folding:** `toLowerCase` against NTFS's upcase table. For example `packageſ` or `.gıt` would pass if NTFS folds `ſ`/`ı` to `S`/`I`. UNVERIFIED.

    **Low.**

13. Hooks with no `timeout` (`C:\projects\envanex\.claude\settings.json:98-101`). If Claude Code treats a hook timeout as a non-blocking error, which is UNVERIFIED, a hung guard fails open. Rule 3's lazy `(\s*)` backtracking grows faster than linear on long runs of whitespace, which matters once Phase 2 feeds whole commands into it. **Low.**

## Required changes

**For the coder:**
- `path-guard.test.js:129-136`: assert the specific message. The malformed-JSON test should require `invalid hook input`; the empty-path test should require `target path is missing or empty`.
- `guard-common.js:229`: change to `await decide(ctx.input, ctx);`.
- Add two path-guard tests:
  - `blocks a NotebookEdit notebook_path under obj`, with `{tool_name:"NotebookEdit", tool_input:{notebook_path:"obj/x.ipynb"}}`, asserting `protected segment "obj"`.
  - `allows a NotebookEdit notebook_path`, with `docs/x.ipynb`.
- Add tests that pin the current block behaviour for UNC and device paths: `\\server\share\obj\x`, `\\?\C:\projects\envanex\.env`, `\\.\C:\projects\envanex\obj\x`.
- `guard-common.js:165`: when `ctx.projectDir` is unset, take the log dir from `process.env.CLAUDE_PROJECT_DIR`, so early guard errors are still logged.
- `guard-common.js`: base the fallback log dir on `projectDirRaw` in slash form, not on the lowercased value.

**For the planner (the exact allow and deny lists and the redaction rules are plan decisions):**
- Close the allow reopenings in findings 6-8:
  - narrow `git switch *`, or deny `git switch *--force*`, `git switch * -f*`, `git switch -C*` and `git switch * --discard-changes*`
  - narrow `dotnet ef database update *` to the CLAUDE.md form, or deny a target migration of `0`
  - deny `*--output*` on `git log`/`git diff`/`git show`
  - deny `dotnet new * --force*`
- Extend the deny list for finding 10's forms, or record each one as an accepted residual risk. Pair each new entry with a probe.
- Narrow `node --test .claude/hooks/tests/*` to exact file names, and record arbitrary-code runners (`dotnet run`/`test`/`build`, and `node`) as a residual risk that makes the deny list advisory against code the model writes itself.
- Add redaction rules and tests for finding 11's forms before Phase 2 guards start logging commands.
- Add finding 12's alias classes and finding 13's timeout question to the Phase 4 residual-risk list, each marked UNVERIFIED with the command that would settle it.

Files reviewed:
- C:\projects\envanex\.claude\settings.json
- C:\projects\envanex\.claude\hooks\lib\guard-common.js
- C:\projects\envanex\.claude\hooks\path-guard.js
- C:\projects\envanex\.claude\hooks\tests\run-hook.js
- C:\projects\envanex\.claude\hooks\tests\guard-common.test.js
- C:\projects\envanex\.claude\hooks\tests\path-guard.test.js
- C:\projects\envanex\thoughts\shared\plans\2026-09-25_chore-agents-hardening.md (Phase 1 and "As built — Phase 1")
