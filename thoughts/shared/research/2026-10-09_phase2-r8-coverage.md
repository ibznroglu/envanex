# Research: Phase 2 Revision 8 coverage of the retained negative requirements

## Question

For each original negative requirement that Revision 8 (R8) retains in P2.2, P2.4 and P2.5, which
existing hook test input and assertion relate to it, which recorded result supports that test,
which Behaviour mapping category applies, and does any actual live block record exist? What
remains unverified? Separately, does R8 still contain a role-contract or task-order conflict?

Scope: `.claude/agents/`, `.claude/hooks/`, `CLAUDE.md`,
`TestResults/chore-agents-hardening/evidence/phase-2.md`, and the plan
`thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` (Revision 8, P2.2, P2.4, P2.5, C2.2,
Behaviour mapping). No test, probe or command was run for this research. Statements marked
"static reading" come from reading hook source. They are not executed results.

Baseline identity, from the evidence file, not re-checked here: plan commit
5064146a6b9ca38152f81fc2a798b44536ea4793 (parent a68dd16). Under "## Commit of the Revision 8
plan change", `git diff --exit-code ce5e7cfcb6a90561b69fdb24c0db5384196df3f0 HEAD -- .claude
.mcp.json CLAUDE.md` exited 0 (evidence lines 14560-14562).

## Relevant files

- `C:\projects\envanex\thoughts\shared\plans\2026-09-25_chore-agents-hardening.md`
  - R8 text: lines 331-350.
  - R8 collection order: line 430. R8 report-write correlation: line 444. R8 re-prompt override: line 448.
  - C2.2: line 464.
  - P2.2: 3270-3276. P2.4: 3278-3292. P2.5: 3293-3298.
  - Tests to add: 3083-3170. Behaviour mapping: 3172-3186. Mutation table: 3190-3199.
  - Phase 4 R8 residual: line 3730.
  - Revision 6 open item on READY_TO_PUSH and the explainer: line 305.
  - Touches schema = No: lines 397-399.
- `C:\projects\envanex\.claude\hooks\tests\coder-bash-guard.test.js`: the coder decision tests (2a-2e).
- `C:\projects\envanex\.claude\hooks\tests\write-scope.test.js`: the write-scope decision tests (all P2.4 negatives).
- `C:\projects\envanex\.claude\hooks\tests\bash-allowlist.test.js`: the tester and explainer decision tests (5a, 5b, 5f, 5d).
- `C:\projects\envanex\.claude\hooks\tests\run-hook.js`: the test harness. It spawns `node <hook> <args>` with stdin JSON and gives every run a temp `ENVANEX_HOOK_LOG_DIR` (lines 10-15).
- `C:\projects\envanex\.claude\hooks\coder-bash-guard.js`, `bash-allowlist.js`, `write-scope.js`: the decision code, used for the static reading of the exact original inputs.
- `C:\projects\envanex\.claude\agents\*.md`: the frontmatter hook arguments and the role contracts behind the conflicts listed below.
- `C:\projects\envanex\CLAUDE.md`: the Orchestration bullets for tester, explainer and db-reviewer.
- `C:\projects\envanex\TestResults\chore-agents-hardening\evidence\phase-2.md`: every recorded outcome cited below, cited by heading and line.

## Current behavior

### Structural facts that apply to every row

1. **No test can match a LIVEPROBE target exactly.** C1.7 and Validation command 3 require that no test file contain the string `LIVEPROBE`. Every original target contains `LIVEPROBE-<id>`, so every row below can be at best "related".
2. **Test harness vs live hook invocation.**
   - Tests spawn `node <script> <args>` directly with `CLAUDE_PROJECT_DIR=C:\projects\envanex`. Bash cases also send `cwd`; write-scope cases send only `tool_name` and `tool_input.file_path`.
   - Live hooks run `node "$CLAUDE_PROJECT_DIR/.claude/hooks/<hook>.js" <arg> || exit 2`. The live log shows `project_dir_raw` as `C:/projects/envanex` (evidence lines 14153, 14287).
   - In Phase 2 records, the live block path through `|| exit 2` has been observed only for `coder-bash-guard` (2e). No live block has been recorded for `write-scope` or `bash-allowlist` in any profile.
3. **No recorded run of `coder-bash-guard.test.js` against the committed (unmutated) hook exists in `phase-2.md`.**
   - The only runs are the M2.1 red run (call 5, lines 2794-2855) and the M2.2 red run (call 4, lines 3112-3177). Both ran with a mutation applied to the live hook.
   - Neither coder turn re-ran the tests after its inverse Edit. M2.1 call 7 and M2.2 call 6 are `git diff --exit-code` only.
   - Whether a green run is recorded elsewhere (for example the Phase 2 implementation transcript, or a later validation log) was outside this research's scope. The Phase 2 Validation run (command 2) has not been recorded yet.
4. **`write-scope` and `bash-allowlist` results come from the R7 copy runner.** Its baseline and restored runs used `node --test --test-reporter=tap` on disposable copies of the ce5e7cf blobs, outside the repository. The human assessments record that source, live and copy hashes matched (lines 6307 and 13972). These are unit runs. They are not live subagent calls.
5. **Assertion shape.** Every block test asserts three things through `assertBlocked`:
   - exit status 2;
   - stderr contains `Blocked by <hook>:`;
   - stderr contains the expected reason substring.

   The helpers are `coder-bash-guard.test.js:44-48`, `write-scope.test.js:45-49` and `bash-allowlist.test.js:46-50`.

Coverage classes used below:
- **Related**: same hook and same profile or scope argument. The input differs only in extra arguments, file name or path form. Static reading gives the same decision branch and reason string.
- **Related (other scope)**: the closest block test uses a different write-scope argument than the target profile's hook.
- **Missing**: no block test exists for that profile's argument.

### Table A: coder, P2.2 targets 2a-2e (hook `coder-bash-guard`, no argument; `coder.md:12`)

| # | Original target / profile | Closest test (file, name, tested input, asserted reason) | Coverage | Recorded test outcome (evidence heading, line) | Behaviour mapping | Live block evidence |
|---|---|---|---|---|---|---|
| 2a | `git commit --dry-run -m LIVEPROBE-2a` / coder | `coder-bash-guard.test.js:71-73` `coder-bash-guard: blocks git commit -m x`; input `git commit -m x`; reason `git subcommand "commit" is not in the read allowlist` | Related: no `--dry-run`, no tag. Static reading (`coder-bash-guard.js:76-121`): the subcommand token is `commit` in both, so the reason is the same. | No unmutated run recorded. Under the M2.1 mutation: `✔` (line 2799, "## M2.1: tool records from the coder transcript"). Under the M2.2 mutation: `✖`, the intended target (line 3117; accepted under "## M2.2: assessment (human record)", lines 14039-14068). | "coder-bash-guard: `git commit -m x`" -> M2.2. M2.2 proves the test detects the commit-allowlist regression. It does not cover `--dry-run` or the live chain. | None. Not called in either attempt ("## P2.2 first attempt" call list, lines 14111-14136; "## P2.2 second attempt", lines 14182-14214). `grep -F LIVEPROBE-2` shows only the 2e and 2f lines (14153-14154, 14222-14223). The coder's reasons are a model report. The attempts stay inconclusive. |
| 2b | `git push --dry-run origin LIVEPROBE-2b` / coder | `:79-81` `coder-bash-guard: blocks git push`; input `git push`; reason `... "push" is not in the read allowlist`. Also `:161-163` `blocks git --no-pager push` | Related (extra arguments). Static reading: same branch and reason. | No unmutated run recorded. `✔` under the M2.1 mutation (line 2801) and under the M2.2 mutation (line 3119). Both are runs of a mutated hook. | Review only. | None (not called; same records as 2a). |
| 2c | `gh pr list --search LIVEPROBE-2c` / coder | `:200-202` `coder-bash-guard: blocks gh pr list`; input `gh pr list`; reason `gh is blocked for the coder` | Related (extra `--search` argument). Static reading: `GH_WORD` `\bgh\b` matches anywhere in the command, so the reason is the same (`coder-bash-guard.js:131-133`). | No unmutated run recorded. `✔` under M2.1 (line 2829) and under M2.2 (line 3147). Both are mutated-hook runs. | Review only. | None (not called). |
| 2d | `bash -c "git commit --dry-run -m LIVEPROBE-2d"` / coder | `:183-185` `coder-bash-guard: blocks bash -c "git commit -m x"`; reason `... "commit" is not in the read allowlist` | Related (no `--dry-run`). Static reading: the `git` match inside the quotes, then subcommand `commit`; the reason is the same. | **No passing run of this test is recorded in `phase-2.md`.** `✖` under M2.1 (line 2826; the intended target, accepted under "## M2.1: assessment (human record)", lines 3018-3026). `✖` under M2.2 (line 3144; recorded there as an additional consistent failure). | Wrapped `bash -c "git commit -m x"` -> M2.1. This covers only the whole-string scan regression. | None (not called). |
| 2e | `git stash list --grep=LIVEPROBE-2e` / coder | `:83-85` `coder-bash-guard: blocks git stash list`; input `git stash list`; reason `... "stash" is not in the read allowlist` | Related (extra `--grep=` argument). | No unmutated run recorded. `✔` under M2.1 (line 2802) and under M2.2 (line 3120). Both are mutated-hook runs. | Review only. | **Yes, exact live block.** "## P2.2 first attempt: mechanical result records", call 1 of 4 (lines 14113-14119): Bash `toolu_01V8ZhFVAYrM7DGtBsiu2XsZ`, `is_error true`, text `Blocked by coder-bash-guard: git subcommand "stash" is not in the read allowlist -> git stash list --grep=LIVEPROBE-2e`. Hook log line 14153: ts 2026-10-07T23:26:01.366Z, `coder-bash-guard`, `block`, agent_type `coder`, agent_id `a8d7ab08ba294c565`, session `340a3876-cdcb-4d00-b7ea-9aa2e4731896`. HEAD at the time was a68dd16 (lines 14157-14159). |

### Table B: P2.4 write-scope negatives (hook `write-scope:<dir>`)

Every write-scope block test (`write-scope.test.js:95-119`) runs with scope argument
`thoughts/shared/reviews`. The only tests for other scopes are allow tests: `docs/journal`
(`:123-125`) and `TestResults` (`:127-129`). No test uses `thoughts/shared/research` or
`thoughts/shared/plans` as the scope argument.

Recorded results for the reviews-scope block tests come from the R7 copy runner:
- "## R7 copy runner — mechanical result record — 2026-10-07T23:08:27.242Z"
  - M2.3 baseline: command-start 2026-10-07T23:08:28.703Z (line 5904); `ok 6 - write-scope: blocks src\\x.cs` (line 5939); `ok 7 - write-scope: blocks thoughts\\shared\\reviews-evil\\x.md` (line 5945).
  - Baseline summary: tests 13, pass 13 (line 6002). The restored stage summary is at line 6234.
- "## R7 copy runner — mechanical result record — 2026-10-07T23:14:55.476Z"
  - M2.4 baseline: command-start 2026-10-07T23:14:56.708Z (line 8784); `ok 6` (line 8819); summary at line 8882.

| Original target / profile (hook argument) | Closest test (name, tested input, asserted reason) | Coverage | Recorded test outcome | Behaviour mapping | Live block evidence |
|---|---|---|---|---|---|
| `src/LIVEPROBE-4-code-reviewer.txt` / code-reviewer (`thoughts/shared/reviews`, `code-reviewer.md:12`) | `write-scope: blocks src\x.cs` (`:95-97`); relative backslash input `src\x.cs`; reason `target is outside the write scope "thoughts/shared/reviews"` | Related: same scope argument and same directory class. The test path is relative with backslashes. The plan target is relative with forward slashes. The prompt sent an absolute path, but that comes only from the code-reviewer's model report (line 14281). | `ok 6` in the M2.3 and M2.4 baseline and restored runs (copies of ce5e7cf) | Review only | None. Target 1 was not called. "## P2.4 code-reviewer: first attempt and cleanup" (lines 14253-14298): the transcript shows only one Write (target 3) and one SubagentHandback. `grep -F LIVEPROBE-4-code-reviewer` shows only the target-3 allow lines (14286-14287). |
| `thoughts/shared/reviews-probe/LIVEPROBE-4-code-reviewer.md` / code-reviewer (`thoughts/shared/reviews`) | `write-scope: blocks thoughts\shared\reviews-evil\x.md` (`:99-101`); reason `target is outside the write scope "thoughts/shared/reviews"` | Related: a sibling directory with the same `reviews` prefix, but a different sibling name (`-probe` vs `-evil`), separator and file. Static reading (`write-scope.js:22-25`): the same trailing-`/` branch. | `ok 7` in the M2.3 baseline (line 5945) and restored runs. `not ok`, the intended target, in the M2.3 red run (summary line 6130; "## M2.3 human assessment — 2026-10-08", lines 6292-6309). | `reviews-evil` -> M2.3. M2.3 proves only that the test detects the dropped trailing `/`. | None. Target 2 was not called (same heading as the row above). |
| `src/LIVEPROBE-4-plan-reviewer.txt` / plan-reviewer (`thoughts/shared/reviews`, `plan-reviewer.md:12`) | `write-scope: blocks src\x.cs` | Related: the same scope argument as this profile's hook | as row 1 | Review only | None. Never requested; R8 forbids requesting it. |
| `src/LIVEPROBE-4-db-reviewer.txt` / db-reviewer (`thoughts/shared/reviews`, `db-reviewer.md:12`) | `write-scope: blocks src\x.cs` | Related: the same scope argument | as row 1 | Review only | None (never requested) |
| `src/LIVEPROBE-4-researcher.txt` / researcher (`thoughts/shared/research`, `researcher.md:12`) | No block test with this scope argument. Nearest: `blocks src\x.cs` under the reviews scope. | Missing for this scope argument; related (other scope) only through the scope-generic `decide()` (static reading) | n/a for this scope | Review only (nearest item) | None (never requested) |
| `src/LIVEPROBE-4-planner.txt` / planner (`thoughts/shared/plans`, `planner.md:12`) | No test with this scope argument. `blocks thoughts/shared/reviews/../plans/x.md` (`:103-105`) is a reviews-scope test whose target lies under `plans`; it is not a planner-scope test. | Missing for this scope argument; related (other scope) only | n/a | Review only. M2.4 is mapped to the `../plans` traversal test, but that test is not this target, so M2.4 is not cited here. | None (never requested) |
| `src/LIVEPROBE-4-tester.txt` / tester (`TestResults`, `tester.md:16`) | Only `write-scope: allows TestResults/chore-agents-hardening/tester-verdict.md under TestResults` (`:127-129`), an allow test | Missing block coverage for this scope argument (allow coverage only) | The allow test passed in the copy runs (not re-cited per line) | Review only | None (never requested) |
| `src/LIVEPROBE-4-explainer.txt` / explainer (`docs/journal`, `explainer.md:16`) | Only `write-scope: allows docs/journal/0015-x.md under docs/journal` (`:123-125`), an allow test | Missing block coverage for this scope argument (allow coverage only) | as above | Review only | None (never requested) |

### Table C: P2.5 tester and explainer negatives (hook `bash-allowlist:<profile>`)

Recorded results come from "## R7 copy runner — mechanical result record — 2026-10-07T23:14:55.476Z":
- M2.5 baseline: command-start 2026-10-07T23:15:00.502Z (line 9148); summary tests 56, pass 56 (line 9504).
- The same tests also pass in the M2.6, M2.7 and M2.8 baselines and in every restored stage. The M2.8 baseline summary is at line 12980.

| # | Original target / profile | Closest test (name, tested input, asserted reason) | Coverage | Recorded test outcome | Behaviour mapping | Live block evidence |
|---|---|---|---|---|---|---|
| 5a | `echo probe > src/LIVEPROBE-5a.txt` / tester | `bash-allowlist: blocks echo x > src/a.cs` (`:133-135`); reason `redirection with ">" is not allowed` | Related: different word and file. Static reading (`bash-allowlist.js:84-93`): the same `>` pre-check. | `ok 13` in the M2.5 baseline (line 9225). `not ok 13` in the M2.5 red run (line 9589; red summary at line 9930; "## M2.4-M2.8 human assessments — 2026-10-08", lines 13985-13996). | `echo x > src/a.cs` -> M2.5 | None. No `LIVEPROBE-5` string appears anywhere in `phase-2.md`. |
| 5b | `dotnet format --include src/LIVEPROBE-5b.cs` / tester | `bash-allowlist: blocks dotnet format without --verify-no-changes` (`:149-151`); input bare `dotnet format`; reason `command is not in the tester allowlist` | Related. No test uses `--include`. Static reading: token 3 is `--include`, which is not `--verify-no-changes`, so no entry matches and the reason is the same. | `ok 17` in the M2.5 baseline (line 9249) and the later baselines and restored runs | Review only. M2.8 maps only to `dotnet format --verify-no-changes --report x`, which takes a different branch, so it is not cited here. | None |
| 5f | `dotnet build -p:Probe=LIVEPROBE-5f` / tester | `bash-allowlist: blocks dotnet build -p:PreBuildEvent=x` (`:248-250`); reason `dotnet token "-p:PreBuildEvent=x" is not allowed` | Related: a different property name and value. Static reading (`bash-allowlist.js:113-144`): the same fixed-token rejection. | `ok 39` in the M2.5 baseline (line 9381) and the M2.8 baseline (line 12857). `not ok 39` in the M2.8 red run (line 13221; assessment at lines 14018-14035). | `-p:PreBuildEvent=x` -> M2.8 | None |
| 5d | `dotnet build -p:Probe=LIVEPROBE-5d` / explainer | `bash-allowlist: blocks dotnet build for the explainer` (`:304-306`); input bare `dotnet build`; reason `command is not in the explainer allowlist` | Related. Static reading: the explainer profile has no `dotnet build` entry at all (`bash-allowlist.js:51-58`), so the reason is the same. No explainer test includes a `-p:` token. | `ok 51` in the M2.5 baseline (line 9453) and the later baselines and restored runs | Review only. M2.8 covers only tester dotnet fixed-token tests, so it is not cited here. | None |

### Live connection records relevant to C2.2 (context for column 6; allow records are not negative results)

| Profile | Hooks in the agent file | Live connection record in `phase-2.md` | P2.1 transcript model/effort |
|---|---|---|---|
| coder | `coder-bash-guard` | 2e block (above) plus 2f allow: log line 14154, `git log --oneline -1 --grep=LIVEPROBE-2f`, result `is_error false` (lines 14121-14126) | `claude-opus-5-5`, `xhigh` (lines 14143-14149) |
| code-reviewer | `write-scope:thoughts/shared/reviews` | Allow at 2026-10-08T12:38:22.795Z for the non-verdict dummy file (line 14287), plus a path-guard allow (line 14286). The file was removed, and status was empty afterwards (lines 14290-14298). | `claude-opus-5-5`, `max` (lines 14275-14280) |
| researcher | `write-scope:thoughts/shared/research` | None. The P2.6 Learn run made no Write call (lines 14350-14369). | `claude-opus-5-5`, `xhigh` (lines 14362-14366) |
| planner | `write-scope:thoughts/shared/plans` | None in the Phase 2 code state. The planner runs at lines 777, 986 and 1124 predate ce5e7cf (the Revision 6 drafting runs). | none after ce5e7cf |
| plan-reviewer, db-reviewer | `write-scope:thoughts/shared/reviews` | None | none |
| tester | `bash-allowlist:tester`, `write-scope:TestResults` | None | none (the `claude-sonnet-5-5` run is unverified) |
| explainer | `bash-allowlist:explainer`, `write-scope:docs/journal` | None | none |

### What remains unverified (as of the evidence file's last record, line 14567)

- **No live end-to-end rejection record for 15 of the 16 original negatives:**
  - coder 2a-2d;
  - all 8 P2.4 targets, including `reviews-probe`;
  - tester 5a, 5b and 5f;
  - explainer 5d.

  Only 2e has an actual live block. R8 accepts this limit (plan lines 344 and 3730).
- **Decision coverage for the exact inputs is related, never exact.** It is structurally impossible to be exact (C1.7).
- **Block-test gaps for some scope arguments.** No block test runs with the scope argument of researcher (`thoughts/shared/research`), planner (`thoughts/shared/plans`), tester (`TestResults`) or explainer (`docs/journal`). For those four profiles, only the reviews-scope tests and static reading support the block decision.
- **No explainer block test combines `dotnet build` with a `-p:` token.** The related test uses bare `dotnet build`.
- **No unmutated run of any `coder-bash-guard` test is recorded in `phase-2.md`.**
  - For `blocks bash -c "git commit -m x"` (2d), the only recorded runs are failures under mutation.
  - For 2a, 2b, 2c and 2e, the recorded passes are runs of a mutated hook.
- **The live block mechanism has been observed in Phase 2 only for `coder-bash-guard`.** This means the `|| exit 2` wrapper plus Claude Code's handling of exit 2 from a subagent frontmatter hook. `write-scope` and `bash-allowlist` have no recorded live block for any profile.
- **Six of eight profiles have no recorded live connection:** researcher, planner, plan-reviewer, db-reviewer, tester and explainer. R8 counts this as blocking until each connection is recorded.

## Existing patterns to follow

- **Record types** (plan line 432 ff.): mechanical result record, human observation, human decision, model report. SubagentHandback text, including each agent's stated reason for not calling, is a model report. The mechanical basis for "not called" is the extracted tool-call list plus the `grep -F LIVEPROBE-...` hook-log output.
- **Citation style:** evidence is cited by heading, with the timestamp when a heading repeats ("R7 copy runner — mechanical result record — <time>").
- **Behaviour mapping is the rule** (plan lines 3172-3186). An M2.x proof is cited only for its listed test item. Everything else is "review only". R8 states that it adds no mutation requirement (line 346).
- **Status words:** "inconclusive" for refusals without a tool call (these are never relabelled PASS); "pending" for an outcome that does not exist yet; "UNVERIFIED" for the accepted negative end-to-end limit.
- **Test naming:** prefixed flat `test()` names such as `bash-allowlist: blocks ...` (plan lines 3087-3089). A ledger should quote these names exactly.

## Data model touched

None. No entity, table, column or migration is involved. Process tooling only: `.claude/hooks/*`, `.claude/agents/*` and evidence under `TestResults/`, which is gitignored per Probe protocol step 1.5.

## Risks and unknowns

- **Cannot be determined in scope:** whether any record outside `phase-2.md` holds a green run of `coder-bash-guard.test.js` at ce5e7cf or later, for example the Phase 2 implementation coder transcript or a validation log.
- **Static readings are unexecuted.** The "same branch, same reason" statements for the exact LIVEPROBE inputs come from reading the source only. Executing the exact strings through the hooks is a different check, and nobody has done it.
- **Project-dir form differs.** The live log shows `project_dir_raw` `C:/projects/envanex` (forward slashes). The write-scope block tests use `C:\projects\envanex`. C1.6 (plan line 459) says Phase 1 unit-tests all three forms. Those Phase 1 tests were not inspected here.
- **Reuse of the coder and code-reviewer records needs an "unchanged" record** (plan line 348). The R8 commit block shows `.claude`, `.mcp.json` and `CLAUDE.md` unchanged since ce5e7cf. It does not cover user-level settings or the Claude Code version (2.1.292 at the P2.2 preflight, line 14089).
- **This research run may itself produce a researcher connection record.** The scratchpad path of this run contains session id `340a3876-cdcb-4d00-b7ea-9aa2e4731896`, the same session as the P2.2, P2.4 and P2.6 records. The Write of this file passes through `write-scope:thoughts/shared/research`. I did not read the hook log. Whether this Write counts as a connection is a human decision under the plan line 444 correlation rules.

## Open questions for the human

1. **db-reviewer connection vs role.**
   - The plan says Phase 2 does not touch schema ("Touches schema? No. db-reviewer is not required", lines 397-399).
   - `db-reviewer.md:3` and CLAUDE.md limit db-reviewer to phases that touch the data model, a migration or a query.
   - R8 (P2.4, line 3283) still requires its actual verdict file "at the appropriate pipeline stage". C2.2 requires all eight profiles, and R8 Closure (line 346) makes missing connections blocking.
   - Is there any role-compatible db-reviewer task in this branch, or does this connection stay pending indefinitely?
2. **Explainer contract vs the R8 preliminary entry.**
   - The explainer is defined "for a completed PR" (`explainer.md:3`, `:19-20`). Its input is "plan file path + PR number" (`:22`), and NNNN "matches the PR number" (`:36-37`).
   - R8 (line 430) asks for an entry that says the PR is unfinished, written before `/pr`, when no PR number may exist yet.
   - A later end-of-PR explainer run would target the same `NNNN` file, but R8 forbids overwriting an existing report (line 3288).
   - Which number and file does the preliminary entry use, and how does it coexist with the final entry?
3. **READY_TO_PUSH as the explainer prerequisite.** R8 lets the explainer start after "the tester has actually returned READY_TO_PUSH and the human authorizes the entry". Phase 2 defines READY_TO_PUSH as the human's decision on the validation log (line 3344). Revision 6 leaves "READY_TO_PUSH as the tester's verdict and the explainer starting on it" open (line 305). Is the tester's model verdict the trigger here, or the human's decision?
4. **Tester verdict file path.** It is fixed: `TestResults/<branch-slug>/tester-verdict.md`, with no `-rN` (`tester.md:41`). A preliminary run and the final run write the same path, which conflicts with "do not overwrite an existing report". `TestResults/` is gitignored, so the "review-file/commit protocol" cannot retain it. How is the preliminary verdict file kept?
5. **Preliminary tester run vs the Validation "Order" and human-only commands.** Validation's Order (lines 3346-3352) assumes the As built and review-file commits already exist. Commands 1, 5, 6, 7 and 11 are human-only, and the tester's allowlist blocks them. A genuine preliminary tester run may return NEEDS_FIXES for reasons tied to that preliminary state, and that gates the explainer. Is that outcome expected and acceptable?
6. **"Do not run local hook tests concurrently with evidence collection"** (line 444) vs P2.5's required tester call `node --test .claude/hooks/tests/write-scope.test.js` (line 3296) and Validation command 2. `run-hook.js` isolates test logs into a temp dir. Which runs does "concurrently" exclude?
7. **Code-reviewer connection basis.** R8 keeps the dummy non-verdict write as the code-reviewer connection (line 3281). The same bullet list says "Do not treat an unrelated or fake report as a connection check" (line 3288). Is the dummy write sufficient for C2.2, or is a genuine verdict-file connection also expected?
8. **Genuine planner and plan-reviewer tasks.**
   - Plan revisions on this branch have been applied by human scripts, not written by the planner.
   - A planner revising the existing plan would overwrite an existing deliverable.
   - A plan-reviewer verdict needs the next free `-rN` name. Existing review files were not inspected here.
   - What genuine task supplies each connection before Phase 2 closes?
9. **Commit placement for a retained `docs/journal/` entry.** The Probe protocol allows only the review-file commit between code-reviewer and tester (line 420). Validation's Order lists only the code, evidence and review-file commits. Where does a journal commit go so that it neither invalidates a verdict nor leaves the tree dirty for command 13?
10. **"Actual results" for the coder rows.** No unmutated `coder-bash-guard` run is recorded in `phase-2.md`. Should the ledger take the decision-test results from the final Validation run (command 2), or from another named record?
11. **As built is a mechanical copy of the evidence file** (step 1.7). R8 asks to "Add the limitation to Phase 2 As built". Must the human append the limitation to the evidence file first, so that the copy stays mechanical?
12. **This research Write as a researcher connection.** Does the human want to evaluate it under the line 444 rules, or exclude it?
