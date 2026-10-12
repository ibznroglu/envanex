Verdict: NEEDS_REVISION
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-10-11_phase2-precheck-test-correction.md phase 1-3
Range: 7b9f113a58e1709301fdebb88e06b93ce7ebc7ee..1ea2816b8898e89617703fccc954f771d68b11bc
Head: 1ea2816b8898e89617703fccc954f771d68b11bc

## Findings

### Scope and abbreviations

This review covers phases 1-3 of the F1 correction proposal and its edit set E1-E3. It checks:
- the five test specifications
- the anchors and the line arithmetic
- the placeholder fills
- the cross-references that fit the proposal into Revision 9's correction loop

It does not repeat the Phase 2 code review or re-open the R9 pipeline. I ran nothing. Every runtime claim below is UNVERIFIED, and each one names the command that would settle it.

- P = `thoughts/shared/plans/2026-10-11_phase2-precheck-test-correction.md`
- plan = `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` (Revision 9)
- review = `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-code-review.md`
- ev = `TestResults/chore-agents-hardening/evidence/phase-2.md`
- Hook paths are under `.claude/hooks/`.

Severity: one Medium finding (R1), which is required. The eight notes N1-N8 are optional.

### Review inputs (verified)

- **Proposal preservation (ev:16486-16539).**
  - The sha256 `08a8d80c…` before staging, when staged and when committed (ev:16487, :16514, :16535) equals the supplied hash.
  - The commit is `1ea2816` with parent `7b9f113` (ev:16526-16527).
  - The diff contains only P (ev:16530-16531), and the status is empty (ev:16537-16539).
  - This matches the supplied Range and Head.
- **Line numbers.** 7b9f113..1ea2816 changes only P. The plan line numbers that P cites "at 7b9f113" (P:5) therefore still hold at Head.
- **Review file name.** The plan-review family holds the 2026-10-10 base file, `-r2` and `-r3`. `-r4` did not exist when this review started. The name follows the choice recorded at ev:16502.

### Test specifications against F1 (conform)

- **Coverage.** E2 (P:119-124) and Phase 2's Tests to add (P:205-219) carry the review's Required change 1 (review:147-157) exactly:
  - the three required inputs: newline, bare `&` and `<(`
  - the two recommended inputs: `||` and `>(`
  - the tester profile, names with the hook prefix, no `LIVEPROBE`, and "review only"
- **Reasons.** Each reason equals the hook's string byte for byte:
  - `a newline is not allowed` (bash-allowlist.js:64)
  - `chaining with "||" is not allowed` (:73)
  - `process substitution is not allowed` (:82)
  - `a background "&" is not allowed` (:89)
- **Helpers.** The chain in bash-allowlist.test.js matches P:119 and P:195-197:
  - `assertTesterBlocks` (:64-66) calls `runBash(['tester'], …)` (:34-36), which sends the Bash shape built by `bashStdin` (:30-32).
  - It then calls `assertBlocked` (:46-50), which asserts status 2, `Blocked by bash-allowlist:tester:` and the reason.
  - `REDIRECTION` and `SEMICOLON` are at :74-75, as P cites.
- **Branch reached (static trace of `precheck`, :62-95).** Each input reaches its own branch first:
  - newline at :63
  - `||` at :72; the input has no `;` and no `&&`
  - `<(` and `>(` at :81; neither input has `$(`
  - bare `&` at :88; the `2>&1` filter leaves the `&`
  - No other site emits these four reasons, so a matching reason pins the branch.
- **"Without it" readings (P:98, P:207-219).** My reading agrees with each one:
  - newline: the split gives the tokens `git status rm x` (:177), and the prefix entry `git status` (:37) allows.
  - `&`: there is no `>`, so the prefix entry allows.
  - `<(`: there is no `&` and no `>`, so the prefix entry `cat` (:12, :49) allows.
  - `||`: the empty-segment check (:179-181) blocks with another reason.
  - `>(`: the `>` check (:91-93) blocks with another reason.
  - These readings are UNVERIFIED by execution, as P itself states. Revision 7's copy procedure would settle them.
- **Isolation.**
  - Each test asserts only on its own child process's status and stderr (run-hook.js:23-36). Each run gets a fresh temporary log dir (:13-15), and block tests read no log. No test can pass on output from another code path.
  - The hook reads only two environment variables: `CLAUDE_PROJECT_DIR`, which `runBash` sets, and `ENVANEX_HOOK_LOG_DIR`, which run-hook.js always replaces. Developer-local environment settings therefore cannot change these decisions.
- **Redaction.** Rule 0 (guard-common.js:134) needs a `\` or a backtick before the LF. No other rule (:151-195) matches the five targets or the reasons, so the stderr line (:255) keeps the reason. This is UNVERIFIED by execution; the new tests settle it.
- **Names and counts.**
  - No file under `.claude/hooks/tests/` already uses any of the five names (search).
  - The counts in E1 (P:99) follow from the files: bash-allowlist.test.js has 56 `test(` calls and the five test files have 211 (also ev:16341). That gives 61 and 216, and 85 + 131 = 216.
- **Validation command 2 (plan:3468).**
  - It holds no fixed count. All five names are hook-prefixed code spans under Tests to add, so command 2 requires each to appear exactly once.
  - E2's other code spans are not hook-prefixed: `$(`, `assertTesterBlocks`, `Blocked by bash-allowlist:tester:`, the inputs and the reasons.
  - Names that end in `)` are not a new parsing case (bash-allowlist.test.js:193, :242).
- **Coverage ledger.** None of the 17 ledger targets contains a newline, `&`, `||`, `<(` or `>(` (plan:3389 lists 5a, 5b, 5f and 5d). The rows and the tests they cite are unchanged, which agrees with E1's Unchanged line (P:105).

### Edit mechanics (conform)

- **Anchors.**
  - plan:381 is the only line that begins `Unchanged: Revision 8's accepted limit`. plan:382 is empty, and plan:383 is `---`.
  - plan:3221 is the only match for E2's anchor line; `wrappers` follows at plan:3222.
  - E3's OLD text occurs once in the plan, on plan:3453.
- **Arithmetic.**
  - E1's block (P:91-105) has 15 lines, plus one empty line: 16.
  - E2's block (P:119-124) has 6 lines.
  - E3 replaces part of one line: 1 added, 1 deleted.
  - That gives 23 added and 1 deleted (P:178). The two insertions add no deletions. UNVERIFIED by execution; P:178's own check settles it.
- **Result shape.** After E1, `## Revision 10` follows Revision 9's last line, with one empty line between them. The trailing empty line that E1 inserts keeps an empty line before `---`.
- **Placeholders.**
  - There are six: decision date, proposal sha256, proposal commit, plan review path, plan review Head and evaluation record heading. All match P:179's pattern.
  - The plan's only `<<` is `<<'EOF'` (plan:3487), which the pattern does not match. A count of 0 after application is therefore meaningful.
  - Available values:
    - proposal sha256: `08a8d80c…` (ev:16487)
    - proposal commit and plan review Head: `1ea2816b…` (ev:16523)
    - plan review path: this file's path
    - evaluation record heading: see R1
  - No candidate value contains `<<`.
  - `<date>` in E1 (P:101) is pattern text in single angle brackets, as in plan:476. It is correctly not a placeholder.

### Fit with the correction loop (conform)

- **Coder turn.** E1's Correction steps (P:100) adapt R9.5 (plan:474) by swapping the file. The swap covers:
  - R9.5's before-turn `test -f` and `git diff --exit-code`
  - its four commands
  - its post-status condition

  P:223-243 restates them. Both `node --test` commands are allowed (settings.json:46, :48).
- **After the test commit.** An identity comparison (plan:516) and R9.6 again (plan:489, :520) follow. No restart follows, because `.claude/hooks/tests/` is excluded from the restart rule (plan:518). P:248-253 matches.
- **Re-review.**
  - Its Range and caller values follow plan:476.
  - The `-r2`/`-r3` naming follows ev:16499.
  - The first report stays unchanged (ev:16432).
  - The header check (P:286) matches the form of review:2-5.
- **Cross-references checked.**
  - The naming deviation (ev:16432) lies in the `###` subsection of "R9.7 preliminary review correlation and preservation" (ev:16370), as P:101 states.
  - The F8 clarification's record type (ev:16428) matches P:102.
  - The Phase 4 residual class "the Bash guards are string heuristics" exists at plan:3825.
  - plan-reviewer.md:45-46 and code-reviewer.md:46-47 say what P:73 and P:147 cite.

### R1 — Medium — P:93: E1 says the F1-F8 evaluation is the human's, but its record says a model wrote it under delegation

- **E1's sentence.** "The human's dated evaluation of its findings F1-F8 (Probe protocol step 3, "Approval and review chain"): "<<evaluation record heading>>" in the evidence file." It goes into the plan unchanged except for the placeholder fill (plan:486; P:84).
- **The record.** The evaluation the caller identified is under "F1 correction proposal preservation and F1-F8 evaluation" (ev:16471). Its subsection "Dated evaluation and naming decisions" gives its own type (ev:16494): "Codex model-authored evaluation and choices under the human's existing delegation …; recorded by the human running this block. Not a fabricated direct quotation."
- **Why it matters.**
  - With that heading filled in, the plan would claim an authorship that the cited record explicitly disclaims.
  - plan:522-526 makes exactly this distinction material. F8 (review:137-143) and its append-only clarification (ev:16427-16434) have just enforced it.
  - E1 is inconsistent with itself: P:102 labels the F8 clarification "a model-authored clarification recorded by the human", but P:93 drops the equivalent label.
  - The plan's precedent for delegated choices states the delegation in the revision record (plan:354: "The human delegated these choices … and then instructed proceeding").
  - plan:486 forbids correcting E1's fixed text at application. Phase 1 step 2 (P:174) takes "the evaluation record" heading as the fill without saying which record type qualifies, so the human would have to guess.
- **Origin.** P was written before the record existed: the planner's Write was at 13:01:05Z (ev:16490), the record at 13:07:03Z (ev:16472). This is a mismatch with later evidence, not a reading error.
- **Not decided here.** Whether a delegated, model-authored evaluation satisfies plan:487 ("the human evaluates each finding in a dated record") is the human's decision. The plan text must say what the record is; it must not decide that question implicitly.

### Non-blocking notes (optional improvements)

- **N1 — P:95, P:105.** The Revision 10 record names E2's change but not E3's. Revision 9's record named its Order change (plan:369: "Validation "Order" names the added commits."). One clause would fix this, for example: "Validation "Order" names the Revision 10 plan and test commits."
- **N2 — P:97.** The reference "(Phase 2, Mutation proofs, step 1)" is ambiguous. That section has two numbered lists, and the first list's step 1 (plan:3296) is unrelated. The cited sentence is step 1 of the M2.3-M2.8 procedure (plan:3307).
- **N3 — P:100.** The ledger's result source is fixed to "the R9.6 record at the Revision 10 test commit". If a later correction changes `.claude/hooks`, plan:489 produces a newer R9.6 record, and this reference then points at superseded results (ledger column 4, plan:3399). "The latest R9.6 record (now the one at the Revision 10 test commit)" avoids that.
- **N4 — P:95.** Two wording points:
  - "one for each pre-check branch that had none": the five tests cover four branches. `<(` and `>(` are two parts of the one condition at :81, with one test each.
  - "This revision closes the test gap" can be read as closing F1. F1 closes only on an APPROVED R9.7 re-review (ev:16434, :16504).
- **N5 — P:172-176.** Two gaps in the application gate:
  - It checks only `head -1`, but plan:486 ties approval to the review's Scope line and to the commit that preserved the proposal. Add `sed -n '2,5p' <plan review path>` with the four expected lines.
  - It does not check that the human's acceptance decision record exists. plan:486 requires that decision, and its date fills `<<decision date>>`; P:152-153 lists it only under Files.
- **N6 — P:178.** `git diff --numstat` compares the working tree with the index, so it prints nothing if the change is already staged. `git diff --numstat HEAD` does not depend on staging. UNVERIFIED that staging comes first in this workflow; running both commands settles it.
- **N7 — P:231, P:277, P:280, P:281.** Prompt details:
  - Inside P, "this plan" can mean P itself (P:145 calls P "this file"), but R9.5's input is the active plan. Name `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` explicitly. For Phase 3, the Scope check at P:286 pins it; for Phase 2, nothing does.
  - P:280 shortens the earlier review's sha256 and commit with "…". R9.7's caller values are exact, so give the full values, which P:93 already contains.
  - P:281 repeats R1's wording "the human's evaluation record". It follows plan:476's name for that caller value; align it with whatever R1's fix says.
- **N8 — P:190, P:259; P:102.** Two informational points:
  - Phase 2's and Phase 3's Files lists omit the evidence file the human appends to (the R9.5 record set, the identity comparison, R9.6 and the correlation check). Phase 1's Files lists it (P:151-154).
  - F2 and F3 stay disclosed only in the Revision 10 record (P:102; ev:16497). Phase 4's residual list (plan:3825) will not name their forms unless Phase 4 is amended later. This does not reopen the evaluation.

## Required changes

1. **(R1) Make E1's reference to the F1-F8 evaluation match the record it cites.** Write the revised proposal as a new file, preserved alone and reviewed under the next -rN (plan:486). Use one of these:
   - **(a) Preferred: one sentence.** Reword P:93's last sentence to give the record's type, as plan:354 does for delegated choices and P:102 does for the F8 clarification. For example: "The dated evaluation of its findings F1-F8 (Probe protocol step 3, "Approval and review chain"), model-authored under the human's delegation and recorded by the human: "<<evaluation record heading>>" in the evidence file." Align P:281 with it.
   - **(b) Keep P:93.** Change Phase 1 step 2 (P:174) so that `<<evaluation record heading>>` must be the heading of a dated human-decision record (plan:522-526, type 3) in which the human evaluates F1-F8 or adopts the delegated evaluation. ev:16471's heading alone is then not a permitted fill.

   Either way, R1 changes no other block, and the placeholders stay the same six. P:88's "15 lines" and P:178's 23-added/1-deleted line stay valid as long as the reworded text stays on the same line. If the revision adds a line anywhere in E1-E3 (for example for N1), update P:88 or P:116 and P:178 together.

Not required (for the planner's and the human's evaluation): N1-N8 above. N1, N2, N5 and N7 are cheap to include in the same revision.
