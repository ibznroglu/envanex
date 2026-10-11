# Plan: Phase 2 — bash-allowlist pre-check block tests (F1 correction; proposed Revision 10)

Status: PROPOSED amendment to `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`. Not applied. Written against branch `chore/agents-hardening`, HEAD `7b9f113a58e1709301fdebb88e06b93ce7ebc7ee`, clean status (main-session preflight 2026-10-11T12:47:24Z).

References: "plan:N" is a line of the active plan at that HEAD; "review:N" is a line of `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-code-review.md`; "ev:N" is a line of `TestResults/chore-agents-hardening/evidence/phase-2.md`. Hook and test paths are under `.claude/hooks/`.

## Goal

Close F1 of the first R9.7 preliminary code review (review:62-90; Required change 1, review:147-162). Add five tester-profile block tests to `.claude/hooks/tests/bash-allowlist.test.js`, one for each pre-check branch that has no test. Run them through Revision 9's existing correction loop (plan:487-489). The plan gets only the cross-references that loop needs:
- a `## Revision 10` record
- the five names under Tests to add
- Validation "Order"
- the compliant file name for the full-range R9.7 re-review

## Non-goals

- No change to `bash-allowlist.js` or to any other hook, helper, permission, agent file, `.mcp.json` or `CLAUDE.md`.
- No work on F2-F6:
  - F2: no quote handling for `--output`
  - F3: no quote handling for the `git branch` cut
  - F4: no scope-`..` test
  - F5: no edit to plan:3019's wording
  - F6: no tests for the fail-closed branches
- No mutation proof, no live probe, no `LIVEPROBE` string, and no tests beyond the five.
- The first R9.7 report is not renamed, overwritten or deleted. No evidence record or As built section is rewritten.
- No change to Revision 9's decisions, steps, conditions, order or settled readings. No fixed count goes into any Validation condition.

## Touches schema? (yes/no — db-reviewer required if yes)

No. Only a Node test file and plan text change. R9.8's single Phase 2 db-review (decision D2) still runs when its Revision 9 conditions hold. This proposal neither adds nor removes it.

## ADR needed? (title, or "no")

No.

## Source facts (read at 7b9f113)

- **Pre-check order in `bash-allowlist.js`:**
  - newline :63-65
  - `;` :66-68
  - `&&` :69-71
  - `||` :72-74
  - backtick :75-77
  - `$(` :78-80
  - `<(` / `>(` :81-83
  - the `2>&1` filter :84-87
  - bare `&` :88-90
  - `>` :91-93
  - Related lines: the empty pipe segment check is at :179-181, `prefix('git status')` at :37, and `cat` comes from `READ_TOOLS` (:12, :49).
- **How a block reaches stderr:**
  - `getCommand` returns `tool_input.command` unchanged (`lib/guard-common.js:112-122`).
  - `block` writes `Blocked by <hook>: <reason> -> <target>` (`lib/guard-common.js:255`).
  - Redaction's only line rule joins a `\` or backtick that comes before an LF (`lib/guard-common.js:134`). It alters none of the five reasons.
- **No shell runs a test input.** `tests/run-hook.js:23-27` passes the JSON to the hook through `spawnSync` `input`. Each run gets a temporary `ENVANEX_HOOK_LOG_DIR` (`tests/run-hook.js:13-15`).
- **`tests/bash-allowlist.test.js`:**
  - It has 56 flat `test()` calls.
  - Helpers: `runBash` :34-36 (the Bash shape through `runHook`), `assertBlocked` :46-50 (status 2, `Blocked by <hook>:`, the reason) and `assertTesterBlocks` :64-66.
  - The `$(` test is at :193-195.
  - No existing test name under `.claude/hooks/tests/` equals any of the five names below.
  - No input in that file contains a newline, a bare `&`, `||`, `<(` or `>(` (review:72; confirmed by search).
- **Permission.** `.claude/settings.json:46` allows `Bash(node --test .claude/hooks/tests/bash-allowlist.test.js)`.
- **The first review:**
  - Header at review:1-5: NEEDS_REVISION, Range `84ebd90..4994826`.
  - Correlated and preserved under ev:16370, "R9.7 preliminary review correlation and preservation".
  - sha256 `8b08a823693a85772220c0a9345e895890d97774cf0f3f3416b9180645d898ae` (ev:16418).
  - Committed as `7b9f113` with parent `4994826`; only the review path changed (ev:16446-16461).
- **ev:16427-16434, "Append-only clarification of earlier evidence".** Its record type is "model-authored clarification, recorded by the human". It:
  - labels the R9.5 assessment as a Codex model report
  - names the recorders of R9.6 and of the R9.6 name assessment (F8)
  - records the missing `-phase2` in the first report's name as a naming deviation
  - notes that the changed-path list was collected after the review
- **Naming:**
  - R9.7's pattern is `<date>_chore-agents-hardening-code-review-phase2[-rN].md` (plan:476). See also `code-reviewer.md:46-47`.
  - For plan reviews, plan:486 says "-r2 first".
  - Phase 1's code-review series went from `2026-09-26_chore-agents-code-review-phase-1.md` to `2026-09-29_chore-agents-code-review-phase-1-r2.md`.
- **Anchors.** Each is unique in the plan:
  - plan:381, the line beginning `Unchanged: Revision 8's accepted limit`
  - plan:3221
  - plan:3453, which contains E3's OLD text exactly once
  - The plan currently contains no `<<…>>` placeholder.

## Proposed edit set E1-E3

Apply the blocks unchanged. Only the double-angle-bracket placeholders are filled in (plan:486). A block is all text between its `~~~` fence lines, not counting the fence lines.

### E1 — insert `## Revision 10`

Anchor: after plan:382, the empty line that follows the line beginning `Unchanged: Revision 8's accepted limit, closure conditions and Unchanged list;`. Insert the 15 lines below and then one empty line. The `---` line before `## Goal` then still has an empty line before it.

~~~
## Revision 10

Human decision: <<decision date>>, accept proposal thoughts/shared/plans/2026-10-11_phase2-precheck-test-correction.md (sha256 <<proposal sha256>>, preserved at <<proposal commit>>). Plan review: <<plan review path>> (APPROVED, Head <<plan review Head>>). Source: finding F1 of the first R9.7 preliminary code review, thoughts/shared/reviews/2026-10-11_chore-agents-hardening-code-review.md (NEEDS_REVISION; Range 84ebd9072f3a06783d71f4af4a7d95f4644c9361..4994826ca784c2cfc66268aefbde16ef77591748; sha256 8b08a823693a85772220c0a9345e895890d97774cf0f3f3416b9180645d898ae; preserved alone at 7b9f113a58e1709301fdebb88e06b93ce7ebc7ee). The human's dated evaluation of its findings F1-F8 (Probe protocol step 3, "Approval and review chain"): "<<evaluation record heading>>" in the evidence file.

This revision closes the test gap that F1 names. Tests to add, bash-allowlist.test.js, gains five tester-profile block tests, one for each pre-check branch that had none: a newline, a bare `&`, `<(`, `||` and `>(`. It changes no hook code, test helper, permission, tool list, write scope, agent definition, model or effort value, CLAUDE.md, Behaviour mapping row, mutation proof, evidence record or As built section, and it closes no probe.

- Inputs. Each input is a string that `run-hook.js` passes to the hook as JSON on stdin; no shell runs it (Phase 2, Mutation proofs, step 1). No input contains `LIVEPROBE` (C1.7).
- Behaviour evidence. The five items are "review only" under the Behaviour mapping's last row; no mutation proof is added. That each test fails when its branch is removed is a static reading, UNVERIFIED by execution and not a condition: without its branch, the newline, `&` and `<(` inputs would match a prefix entry and be allowed (the status assertion fails), and the `||` and `>(` inputs would be blocked by a later check with another reason (the reason assertion fails).
- Counts. From the current 56 bash-allowlist tests and R9.6's 211: 61 and 216, that is 85 baseline names and 131 new names. These are cross-checks; Validation command 2's condition stays as written.
- Correction steps. The correction loop in Probe protocol step 3, "Approval and review chain", applies unchanged. The coder turn is R9.5 with `.claude/hooks/tests/bash-allowlist.test.js` in place of `.claude/hooks/tests/write-scope.test.js` in every command and status condition, its input limited to the five Revision 10 items (Tests to add, bash-allowlist.test.js, tester profile blocks), and "the five additions" in place of "the four additions". Then come the test commit alone, an identity comparison and R9.6 again, because a file under .claude/hooks changed; no restart follows, because files under .claude/hooks/tests/ are excluded from the restart rule. The Coverage ledger's unmutated results come from the R9.6 record at the Revision 10 test commit; the earlier R9.6 record stays as recorded.
- Re-review name (F7). The first R9.7 report keeps its recorded name and is not renamed, overwritten or deleted; its naming deviation stays recorded under "R9.7 preliminary review correlation and preservation" in the evidence file. The R9.7 re-review over the full Range writes thoughts/shared/reviews/<date>_chore-agents-hardening-code-review-phase2-r2.md, and a later re-review takes -r3, and so on (-r2 first, as for plan reviews). Its caller values name the first report, with its path and sha256, as the earlier review file, and give the changed-path list at the re-review's own HEAD.
- Other findings. F2 and F3 stay disclosed, non-blocking residual findings in the class of Phase 4's "the Bash guards are string heuristics"; their runtime claims stay UNVERIFIED. F4-F6 are not implementation work in this revision. F8 is answered by "Append-only clarification of earlier evidence" in the evidence file, a model-authored clarification recorded by the human; no earlier record is rewritten.
- Tester scope. The preliminary tester's existing check that every test Phase 2 requires exists and asserts something covers the five tests; R9.9's scope text is unchanged.

Unchanged: Revision 9's decisions, execution order, approval chain, step conditions and settled readings; the Coverage ledger's 17 rows; Validation's numbered commands and conditions; Phases 1, 3 and 4.
~~~

### E2 — Tests to add, `bash-allowlist.test.js`, tester profile blocks (insert)

Anchor: after plan:3221, which is this exact line:

~~~
  - substitution: `` echo `id` ``, `echo $(id)`
~~~

Insert these 6 lines:

~~~
  - pre-check branches (Revision 10), in this order after the `$(` substitution test; each test uses the existing `assertTesterBlocks` helper, so it asserts exit status 2 and stderr containing `Blocked by bash-allowlist:tester:` and the reason:
    - `bash-allowlist: blocks git status and rm x separated by a newline` — input `git status`, one LF, `rm x` (the source writes the LF as the `\n` escape inside a single-quoted string; no CR); reason `a newline is not allowed`
    - `bash-allowlist: blocks git status & rm x` — input `git status & rm x`; reason `a background "&" is not allowed`
    - `bash-allowlist: blocks cat <(rm x)` — input `cat <(rm x)`; reason `process substitution is not allowed`
    - `bash-allowlist: blocks git status || rm x` — input `git status || rm x`; reason `chaining with "||" is not allowed`
    - `bash-allowlist: blocks cat x >(rm x)` — input `cat x >(rm x)`; reason `process substitution is not allowed`
~~~

### E3 — Validation "Order" (replace, plan:3453)

OLD:

~~~
the Revision 9 plan and test commits and any approved correction commit;
~~~

NEW:

~~~
the Revision 9 plan and test commits, the Revision 10 plan and test commits, and any approved correction commit;
~~~

## Phase 1: Plan review and application (plan-only)

### Files

- `thoughts/shared/plans/2026-10-11_phase2-precheck-test-correction.md` — created (this file). The human preserves it alone in its own commit.
- `thoughts/shared/reviews/<date>_chore-agents-hardening-plan-review-r4.md` — created by plan-reviewer.
  - It is the next -rN after -r2 and -r3 (plan:486; `plan-reviewer.md:45-46`).
  - Scope names this proposal. Range is `7b9f113a58e1709301fdebb88e06b93ce7ebc7ee..<proposal commit>`; Head is `<proposal commit>`.
  - It is committed alone, whatever its verdict.
  - On NEEDS_REVISION, a revised proposal goes into a new file. That file is preserved alone and re-reviewed under the next -rN.
- `TestResults/chore-agents-hardening/evidence/phase-2.md` — appended by the human:
  - the dated evaluation of F1-F8 (plan:487)
  - the decision that accepts the approved proposal
  - the application record
- `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` — modified — E1-E3, in one plan-only commit.

### Signatures

None.

### Tests to add

None in this phase.

### Validation

The human records each step with `run` in an external Git Bash.

1. **Plan review.**
   - Before the run, record what plan:463 lists for every agent task: date, branch, full HEAD, status, and `test ! -e <review path>`.
   - After the run, do the connection correlation check (plan:504-514).
2. **Before applying.**
   - `head -1 <plan review path>` prints `Verdict: APPROVED`.
   - The evaluation record exists. Its heading is the value for `<<evaluation record heading>>`.
   - `git --no-optional-locks status --porcelain --untracked-files=all` prints nothing.
   - Record `git rev-parse HEAD` as the pre-application HEAD.
3. **After applying, before the commit.**
   - `git diff --numstat` prints exactly one line: `23	1	thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` (E1 16 + E2 6 + E3 1 added, E3 1 deleted).
   - `grep -cE '<<[A-Za-z][A-Za-z0-9 .-]*>>' thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` prints `0` and exits 1.
   - `grep -c $'\r' thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` prints `0` and exits 1.
4. **After the commit.**
   - `git diff --name-only <pre-application HEAD> HEAD` prints exactly `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`.
   - The status is empty.
   - The plan is not a file Claude Code loads, so no restart follows.

## Phase 2: Five pre-check block tests, test commit, R9.6 again

### Files

- `.claude/hooks/tests/bash-allowlist.test.js` — modified. Five flat `test()` calls go after the `$(` test (:193-195), in E2's order. No existing line changes.

### Signatures

None added.
- Each test calls the existing `assertTesterBlocks(command, reason)` (:64-66). That helper runs `runBash(['tester'], command)` (:34-36) and then `assertBlocked(result, 'bash-allowlist:tester', reason)` (:46-50).
- A reason used twice may be held in a module-level constant, as `REDIRECTION` and `SEMICOLON` are (:74-75).
- No helper is added or changed.

### Tests to add

The names, inputs and reasons are E2's. Each input is a string sent to the hook, never a command a shell runs.

For each test, the line given is the branch that blocks it. "Without it" is a static reading of what happens if that branch is removed. These readings are UNVERIFIED by execution, and no mutation proof is added: the Behaviour mapping classes all five as review only.

- `bash-allowlist: blocks git status and rm x separated by a newline`
  - Input: `git status` + LF + `rm x`. Reason: `a newline is not allowed`. Branch: :63-65.
  - Without it: no later pre-check fires, and the whitespace split gives the tokens `git status rm x`. `prefix('git status')` matches, so the hook allows.
- `bash-allowlist: blocks git status & rm x`
  - Reason: `a background "&" is not allowed`. Branch: :88-90.
  - Without it: the `>` check passes, and `prefix('git status')` allows.
- `bash-allowlist: blocks cat <(rm x)`
  - Reason: `process substitution is not allowed`. Branch: :81-83.
  - Without it: there is no `&` and no `>`, so `prefix('cat')` allows.
- `bash-allowlist: blocks git status || rm x`
  - Reason: `chaining with "||" is not allowed`. Branch: :72-74.
  - Without it: the `|` split leaves an empty segment, and :179-181 blocks with `an empty pipe segment is not allowed`. The test's reason assertion fails.
- `bash-allowlist: blocks cat x >(rm x)`
  - Reason: `process substitution is not allowed`. Branch: :81-83.
  - Without the `>(` test: the `>` check (:91-93) blocks with the redirection reason. The test's reason assertion fails.

### Validation

1. **Before the coder turn** (human, with `run`):
   - `date -Iseconds`
   - `git branch --show-current` prints `chore/agents-hardening`.
   - `git rev-parse HEAD` prints the Revision 10 plan commit.
   - `git --no-optional-locks status --porcelain --untracked-files=all` prints nothing.
   - `test -f .claude/hooks/tests/bash-allowlist.test.js` exits 0.
   - `git diff --exit-code HEAD -- .claude/hooks/tests/bash-allowlist.test.js` exits 0.
2. **Coder turn** (R9.5 as E1 adapts it). The prompt:
   - names this plan's path and phase 2, limited to the five Revision 10 items
   - says "For the task, call only Read, Glob, Grep, Edit, Write and Bash; SubagentHandback, if available, is for report delivery only"
   - quotes these four commands and their conditions:
     1. `node --test .claude/hooks/tests/bash-allowlist.test.js` — exit 0; all names pass, including the five additions. Cross-check: tests 61, pass 61.
     2. `node --test .claude/hooks/tests/*.test.js` — exit 0 and Validation command 2's complete condition. Cross-check: tests 216.
     3. `dotnet build -warnaserror` — exit 0, with `0 Warning(s)` and `0 Error(s)`.
     4. `git status --short` — exactly ` M .claude/hooks/tests/bash-allowlist.test.js`.
   - ends with "do NOT run any other agent, do NOT commit, wait for me".

   The human records the turn with R9.5's record set:
   - identity, transcript and meta hashes, version, and model and effort
   - the tool-call list, and every Edit, Write and Bash call with its result
   - each Edit/Write matched to path-guard, and each Bash call matched to coder-bash-guard
3. **After PHASE_COMPLETE, before `/commit`** (human, with `run`). These are this phase's own records and add no plan text.
   - `git diff --numstat -- .claude/hooks/tests/bash-allowlist.test.js` prints one line with deletions `0`.
   - `grep -c LIVEPROBE .claude/hooks/tests/bash-allowlist.test.js` prints `0` and exits 1 (C1.7).
   - `grep -c $'\r' .claude/hooks/tests/bash-allowlist.test.js` prints `0` and exits 1 (C1.4).
4. **Commit and R9.6.**
   - Run `/commit` with the test file alone. `git diff --name-only HEAD~1 HEAD` prints exactly `.claude/hooks/tests/bash-allowlist.test.js`.
   - Record the identity comparison (plan:516).
   - Run R9.6 (plan:520) at the test commit, under Validation command 2's condition. Each of the five Revision 10 names appears exactly once. Cross-check: 216.
   - As F8 recommends, the R9.6 record names its recorder.
   - No restart (plan:518).

## Phase 3: R9.7 re-review over the full Range

### Files

- `thoughts/shared/reviews/<date>_chore-agents-hardening-code-review-phase2-r2.md` — created by code-reviewer. It is committed alone, whatever its verdict.

### Signatures

None.

### Tests to add

None.

### Validation

1. **Before the run** (human, with `run`):
   - date, branch, and full HEAD `<H>`
   - `git --no-optional-locks status --porcelain --untracked-files=all` prints nothing.
   - `test ! -e thoughts/shared/reviews/<date>_chore-agents-hardening-code-review-phase2-r2.md` exits 0.
   - Record `git diff --name-only 84ebd9072f3a06783d71f4af4a7d95f4644c9361 <H>`. R9.7 lists this changed-path list as a caller value; the first review did not get it (ev:16433).
2. **The prompt** (plan:463, plan:476) carries the delegation wording of plan:463 and prescribes no verdict. It gives:
   - this plan's path, phase 2, and the branch slug `chore-agents-hardening`
   - Range `84ebd9072f3a06783d71f4af4a7d95f4644c9361..<H>` and Head `<H>`
   - the changed-path list
   - the earlier review file `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-code-review.md`, with sha256 `8b08a823…` and its preservation commit `7b9f113…`
   - the human's evaluation record
   - the exact report path
3. **After the run:**
   - The connection correlation check (plan:504-514) finds exactly one Write to the report path, with path-guard and `write-scope:thoughts/shared/reviews` allow lines.
   - The status is exactly `?? <report path>`.
   - Lines 2-5 of the report read `Reviewer: code-reviewer`, `Scope: thoughts/shared/plans/2026-09-25_chore-agents-hardening.md phase 2`, `Range: 84ebd9072f3a06783d71f4af4a7d95f4644c9361..<H>` and `Head: <H>`.
   - The human commits the file alone.
4. **Next step:**
   - APPROVED: R9.8 (plan:477), as written.
   - NEEDS_REVISION: plan:487 again; the next re-review takes `-r3`.

## Open questions

1. **Suffix of the first re-review.** This proposal uses `-r2`, after plan:486 ("-r2 first") and Phase 1's `…-code-review-phase-1-r2.md`. Read literally, `code-reviewer.md:47` ("`-rN` marks the Nth re-review") gives `-r1`. The human confirms before the plan review. If the answer is `-r1`, E1's "Re-review name" bullet and Phase 3 change.
2. **Name of this proposal's plan review.** It is taken here as `<date>_chore-agents-hardening-plan-review-r4.md`, the next -rN of the existing series (base, -r2, -r3). The human confirms.

## Rollback notes

- **Phase 1.** Nothing changes in the plan before application. If the proposal is rejected or superseded:
  - the proposal file and its review stay as records
  - F1 returns to the planner

  After application, the withdrawal path is a new, recorded plan-only commit (`git revert <Revision 10 plan commit>`). History is not rewritten.
- **Phase 2.**
  - Before `/commit`, the human discards the change with `git restore .claude/hooks/tests/bash-allowlist.test.js` and records an empty status.
  - After the commit, `git revert <test commit>` removes the five tests and reopens F1. That revert is a correction commit under plan:489: an identity comparison and R9.6 again.
  - No restart either way (plan:518). There is no schema, migration or data effect.
- **Phase 3.** No review file is renamed, overwritten or deleted. A failed or declined run is recorded, and the next run takes the next free name.
