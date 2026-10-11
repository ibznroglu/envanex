Verdict: NEEDS_REVISION
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-10-11_phase2-r8-correction-proposal-r3.md phase 1-5
Range: fc318f1611e8b980b0acc930084645547474c0a1..657b9beca80b4cca01b3aed4cc344a095d2eff58
Head: 657b9beca80b4cca01b3aed4cc344a095d2eff58

## Findings

### Scope and abbreviations

This is the first re-review in the `chore-agents-hardening` plan-review family. It covers phases 1-5 of proposal r3, including D1-D5 and E1-E7. The first review covered the original proposal. r2 was never reviewed.

I ran nothing. Every runtime claim below is UNVERIFIED and names the command that would settle it.

- P = `thoughts/shared/plans/2026-10-11_phase2-r8-correction-proposal-r3.md`
- plan = `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` (Revision 8)
- ev = `TestResults/chore-agents-hardening/evidence/phase-2.md`
- r2 = `thoughts/shared/plans/2026-10-10_phase2-r8-correction-proposal-r2.md`
- R1 = `thoughts/shared/reviews/2026-10-10_chore-agents-hardening-plan-review.md`

### Verified as the proposal states (no change needed)

- **Review inputs.**
  - ev:15205-15261 records:
    - r3's sha256 `02197721…`, before and after the commit;
    - commit `657b9be` with parent `fc318f1`;
    - a diff that is r3 only;
    - an empty status.
  - This matches the supplied Range and Head.
  - The hardening plan-review family holds only `2026-10-10_chore-agents-hardening-plan-review.md`, so `-r2` is free.
- **D2 and D3.**
  - The record exists at ev:15191-15203. D1, D4 and D5 stay undecided (ev:15203).
  - E1's accommodation text (P:371-372) is identical, as read, to r2:359-360, the text the decisions were taken against.
  - E1 stays within the recorded scope:
    - db-review after an APPROVED code review and before the preliminary tester;
    - journal entry after a READY_TO_PUSH trigger, with a free prefix, a separate authorization and the pending-work disclosure.
  - r2 already carried the db re-review loop, so "the proposed review" covers its `-rN` re-reviews.
- **Anchors.**
  - Each E1-E7 anchor occurs exactly once: plan:350, :430 (before :432), :444, :3170, :3292, :3298 (before :3299) and :3347.
  - plan:281 reads "…Phase 2's commits, on a clean tree" and does not match E7's colon anchor.
  - Inside E1-E7, `<<` occurs only in placeholders (P:366, P:416).
- **Source facts 1-13 match the sources:**
  - write-scope.test.js:32-35, :45-49, :95-119, :121-129;
  - bash-allowlist.js:46, :56, :91-92, :97-99, :141, :171-174, :191-192, :194-196;
  - bash-allowlist.test.js:133, :149-151, :248-250, :304-306;
  - settings.json:151-157;
  - tester.md:3, :32, :36, :41-49;
  - db-reviewer.md:3, :14, :48-51, :54-67, :78;
  - code-reviewer.md:46; plan-reviewer.md:45;
  - CLAUDE.md:90-92, :96-97, :100-101, :104-105, :111-112;
  - docs/roadmap.md:45-57, :62-64;
  - docs/journal: 0001-0008 plus one `note-` file, and no `0009-` entry.
- **The four E6 tests.**
  - Each reaches write-scope.js:23-24. The reason (:24) and the hook name (:28) both come from the raw `scopeArg`, so the asserted texts are exact.
  - guard-common.js:255 formats `Blocked by <hook>: <reason> -> <target>`. No SENSITIVE keyword (guard-common.js:125-126) occurs in the four messages.
  - `runWrite` and `assertBlocked` match P:653-654.
  - The names are unique and are prefixed code spans (plan:3089).
  - Counts are 56+53+50+35+13 = 207, so 211 after E6; write-scope.test.js goes from 13 to 17.
  - run-hook.js:13-15 isolates the log, and the block assertions read only status and stderr. No test can pass on state another path wrote.
- **Version history.**
  - ev:14089, ev:14110, ev:14181 and ev:14840 carry the stated versions.
  - The "not recorded" rows extract no version field. The 2.1.292 strings at ev:14583 and ev:14769 lie inside quoted model-report text.
  - The planner transcript hash at P:11 equals ev:14657.
- **R9.1 baseline (ev:15034-15089).**
  - 18 hashes (ev:15063-15080).
  - settings.local.json equals ev:181; code-reviewer.md equals ev:14263.
  - No `claude --version` and no ignored-path listing.
  - Only `.claude/settings.local.json` is ignored under `.claude` (.gitignore:44-45).
- **C2.2 pairs.** plan:464 names ten profile-hook pairs, and five remain (P:39). ev:14586 and ev:14772 are the research and plans `write-scope` allow lines.
- **P2.7 and P2.8.** Their records meet the pass conditions (ev:14393, ev:14399-14404), so plan:3332-3340 does not block R9.7.
- **Allowlists.**
  - The tester allowlist (bash-allowlist.js:35-50) admits every in-scope command, including `git rev-parse` for `Head:`.
  - The explainer's (:51-58) admits P2.5's `git log`.

### Disposition of R1 findings 1-14

All 14 are resolved. None is reopened.

- **1:** E1 "Phase 2 accommodations", with an explicit list of the conditions that keep their force (P:370-374).
- **2:** content-bound approval and the `-rN` chain (P:430; P:604-619).
- **3:**
  - the scope is enumerated, with a later-by-design list (P:436-440);
  - the trigger is bound to the latest run (P:442);
  - the pending work is disclosed (P:425).
  - P is right to correct R1's `Branch:` wording: tester.md:48 writes the branch name.
- **4:** one definition, at the final decision site, with the 5b and 5d predicate-path differences stated (P:378; P:187-192).
- **5:** the exact-path rule plus the prefix rule. The same-number option is gone, and practice and the PR 7 consequence are stated. The human decided it as D3.
- **6:**
  - destination check, HEAD7, `n` and final names (P:446);
  - windowed matching (P:455);
  - tool input and result (P:786, :811).
  - plan:3297 already matches by context, so no whole-log count remains.
- **7:**
  - the baseline hashes CLAUDE.md and settings.local.json;
  - current and historical state are kept apart;
  - the planner record and the actual order are recorded;
  - D5 (PROPOSED) answers the update and restart question.
- **8:** three categories (1/6/10). P is right to correct R1's "a `grep -F` absence proves no call" (plan:3290).
- **9:**
  - DB_APPROVED gate;
  - db loop with `[-rN]`;
  - full-range re-reviews;
  - D4's base;
  - the precedent is withdrawn and the plan:430 reading is stated.
- **10:** per-run parameters. P's separate runtime-tools group (ev:14247; plan:3237) and `requestShape` as context (plan:3248) are sound.
- **11-14:** resolved as P's table states.

### Objective checks

- **Tester prerequisites.**
  - P:424, :427 and :434 separate the gates.
  - Preliminary: only review paths change after each review's Head.
  - Final:
    - after the final code review's Head, only review paths change;
    - after the db review's Head, review paths, the 0009 entry and the plan may change.
  - This removes r2's conflict between r2:415 and r2:422. Phase 5's own checklist is incomplete (finding 5).
- **Journal retention.**
  - P:446 gives the journal its own steps: bytes, sha256 and originating run; `n`; `test ! -e`; the move; the hash check.
  - It exempts the journal from the `Verdict:`/`Head:`/`Branch:` and HEAD7 checks, so no tester-specific header is required.
- **Version evidence.**
  - P keeps "not extracted" apart from "not in the transcript":
    - the cited planner record (ev:14653-14775) extracts no version (P:107);
    - the raw transcript's 2.1.292 rests on a Codex inspection (P:11) and the assessment line ev:15126.
  - The human-run extraction is pending (P:592-602). I treat the value as unverified. On labelling, see O4.
- **Order, ranges and retries.**
  - No dependency cycle in P:331-346.
  - Approval binds to the file, the commit, the hash and the latest review naming the file (P:430, P:604-613).
  - Every retry takes a new name or stops on a conflict, except the D4-B path (finding 2).

### Findings that require a change

1. **P:409 vs P:420, P:643-645 and P:670-674 — Medium. R9.5 does not fit E2's per-agent-task rule.**
   - **What P:409 requires of "each agent task":**
     - the delegation wording ("For the task, call only ...");
     - `test ! -e <target path>` before the run;
     - "the connection correlation check" after it.
   - **Why the check does not fit the coder.**
     - Its expected calls and hooks cover only a report Write (path-guard plus `write-scope:<dir>`) and a Bash call (`bash-allowlist:<profile>`) (P:454-455).
     - Its status rule expects `?? <report path>` (P:457).
   - **What the coder does instead at R9.5:**
     - it modifies an existing tracked file, so `test ! -e` exits 1;
     - it has no write-scope hook: Edit and Write meet only path-guard (settings.json:151-157), and Bash meets `coder-bash-guard` (coder.md:7-12);
     - it ends with ` M .claude/hooks/tests/write-scope.test.js` (P:674).
   - **What is missing.**
     - Neither P:420 nor P:645 gives R9.5's values for these records.
     - Phase 2 expects the coder to run and report four commands (P:670-674), but the prompt names only the plan, the phase and the limitation (P:645). coder.md:26 guarantees only the build.
   - **Why it matters.** R9.5 is the first agent step after R9.4, and P:353-356 forbids adjusting E2 at application.

2. **P:293 and P:396-399 vs P:427 and P:872-903 — Medium. D4-B is not apply-ready.**
   - B replaces the E1 bullet "Final review links" with "Final review range" (P:399). E2's R9.12 still cites `(Revision 9, "Final review links")` (P:427).
   - For B, Phase 5 gives only the Range (P:885). These steps are D4-A only and have no B form:
     - the pre-run `git diff --name-only <H_A> <As built commit>` (P:875);
     - the caller values "link 1's path …" and "whether the two links cover …" (P:880, :883);
     - the READY_TO_PUSH record's "link 1 and link 2" (P:903).
   - D4 is still undecided (ev:15203). Choosing B would leave inconsistent plan text and an underspecified final review, and P:353-356 sends any fix back to R9.3.

3. **P:601 — Low. The example comparison value for the code-reviewer transcript is superseded.**
   - ev:14262 (`224fc980…`) predates the P2.6 LSP calls.
   - The latest recorded hash of `agent-a49c1b7cd999418ca.jsonl` is ev:14324 (`4a02cacb…`).
   - The two researcher transcripts have recorded hashes at ev:14359 and ev:14577.
   - Following the example records an expected "difference".

4. **P:762-763 vs plan:3362 and P:437 — Low. Command 2's prompt text is not verbatim.**
   - E2 requires each command "quoted verbatim with its expected exit code and output condition" (P:437), and P:762 says "verbatim".
   - P:763 drops "one run of the five test files." and appends "After R9.5, `tests` is 211."
   - The fixed 211 (P:672, :695, :763, :900) goes stale if an approved correction adds a test. plan:3362's "85 + the number of new names" is the binding condition.

5. **P:889-892 vs P:434 and P:444 — Low. The final-tester checklist omits E2 prerequisites.**
   - "Before the final tester" lists only the db-review diff.
   - It omits three checks:
     - the final code review reads `Verdict: APPROVED`;
     - `git diff --name-only <its Head> HEAD` prints only review paths;
     - the applicable db review reads `Verdict: DB_APPROVED`.
   - The tester's read check (P:444) is a model report, not the human's gate (plan:418).

### Optional improvements (not blocking)

- **O1, P:374 (E1).** "CLAUDE.md:90-92 and :101 outside D2" implies D2 is an exception to :101. D2 leaves :100-101 unchanged (P:224).
- **O2, P:229.** The D3 quote "as recorded by the caller" differs from the authoritative record (ev:15200).
  - The record adds the separate authorization and "the hardening PR". P:246 and P:261 cover both, so nothing conflicts.
  - Cite the record. Consider naming its evidence heading in E1 (P:366) or E2 (P:415).
- **O3, P:236.** 0001 documents roadmap row 1, `chore(solution)` (docs/roadmap.md:45). "First numbered entry for a chore" holds only for the unnumbered (`—`) chore rows.
- **O4, P:20, P:151, P:956 and P:971.** The planner's 2.1.292 rests on a Codex inspection (a model report, plan:433, :436) and an assessment record (ev:15126), not on a mechanical result record. Label it so under "Source facts verified".
- **O5, P:733.** One listing covers both the code-review and db-review families. State that the next suffix is computed per family.
- **O6, P:799-818.** Phase 3's R9.10 checklist covers only an accepted entry. Point to P:446 for an unaccepted one.

### Unverified (and the command that settles each)

- **Test counts and outcomes:** `node --test .claude/hooks/tests/write-scope.test.js` (17) and `node --test .claude/hooks/tests/*.test.js` (211), after R9.5.
- **Deletion counts:** zero deletions at R9.5 and two at R9.4, by the numstat checks P already specifies. The R9.5 count assumes write-scope.test.js ends in a newline; `tail -c1 .claude/hooks/tests/write-scope.test.js | od -c` settles that.
- **Planner transcript version set:** the R9.4 extraction, for example `grep -o '"version":"[^"]*"' <agent-ae5c9090f4055b6e2.jsonl> | sort | uniq -c`, plus `sha256sum` compared with ev:14657.
- **That r3 differs from r2 only by the listed corrections.** I compared only P:371-372 with r2:359-360, and r2:415, :422 and :434. `git diff --no-index -- <r2> <r3>` settles it.
- **Runtime agent behaviour:**
  - whether the tester honours the later-by-design list;
  - whether the explainer accepts 0009 despite explainer.md:36-37.

  Only the runs settle these. P already records a declined write as pending.

## Required changes

1. **(Finding 1) State R9.5's values for P:409's records** in E2's R9.5 (P:420), and match them in Phase 2 (P:643-674):
   - **Whether R9.5 is a connection record.** It is not needed for C2.2, because the coder's connection is P2.2's. If it is not, list what is still recorded: identity, version, model/effort, and every Edit/Write/Bash call with its path-guard or coder-bash-guard decision. State that no report Write is expected.
   - **The pre-run check that replaces `test ! -e`.** For example, `git diff --exit-code -- .claude/hooks/tests/write-scope.test.js` and an empty status. State the post-run status: exactly ` M .claude/hooks/tests/write-scope.test.js`.
   - **The tool list of the delegation wording.**
   - **The four commands of P:671-674.** Quote them in the coder prompt, or state that the human runs any the coder did not report.
2. **(Finding 2) Make D4-B apply-ready.**
   - Make P:427's reference work under both alternatives: give B's bullet the same label, or cite the final-review bullet without its label.
   - Give Phase 5 a B form of the pre-run check, the caller values and the READY_TO_PUSH record (P:875-885, :903).
3. **(Finding 3) At P:601, name the latest recorded hash per transcript:**
   - ev:14324 for the code-reviewer, superseding ev:14262;
   - ev:14359 for the P2.6 researcher;
   - ev:14577 for the R8 researcher;
   - ev:14657 for the planner.
4. **(Finding 4) At P:763, quote plan:3362 verbatim.** State 211 separately as a derived expectation, here and at P:672, :695 and :900. It holds unless an approved correction changes the set of tests.
5. **(Finding 5) At P:889-892, add P:434's prerequisites as explicit human checks before the final tester:**
   - the final code review's `Verdict: APPROVED`;
   - its Head..HEAD review-only diff;
   - the applicable db review's `Verdict: DB_APPROVED`.

   Alternatively, write "the R9.12 preconditions of E2 are met", as P:757 does for R9.9.

O1-O6 do not block. E1-E7 cannot change at R9.4 (P:353-356), so O1, an E1 text, is cheapest to fold into the next revision.
