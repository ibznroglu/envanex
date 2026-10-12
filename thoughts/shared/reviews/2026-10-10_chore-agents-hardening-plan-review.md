Verdict: NEEDS_REVISION
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md phase 1-5
Range: 561624241a31754d3c028e32b4c32d1a5ab83331..17a22f0a264e3b0cef49580f38b3d0b4ccf43dee
Head: 17a22f0a264e3b0cef49580f38b3d0b4ccf43dee

## Findings

### Scope and abbreviations

This review covers stages (phases) 1-5 of the correction proposal, including D1-D5 and the edit blocks E1-E7 that those stages apply. It is not a fresh audit of the Envanex code. I ran nothing, so every runtime claim below is marked UNVERIFIED, with the command that would settle it.

- P = `thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md`
- plan = `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` (unchanged since 5064146)
- ev = `TestResults/chore-agents-hardening/evidence/phase-2.md`

### Verified as the proposal states (no change needed)

- **Helpers:** `write-scope.test.js:32-35` and `:45-49`, `coder-bash-guard.test.js:44-48`, `bash-allowlist.test.js:46-50`.
- **Log isolation:**
  - `run-hook.js:10-15`;
  - the temporary-project tests (`guard-common.test.js:355-404`, `path-guard.test.js:240-256`);
  - `guard-common.test.js:112`.
- **Static branch readings:** they match the source for 2a-2e, all eight write-scope targets, 5a and 5f.
- **Test count:** the files hold 207 `test(` calls today (50 + 35 + 53 + 56 + 13). E6 brings the total to 211. That the run reports 211 is UNVERIFIED (`node --test .claude/hooks/tests/*.test.js`).
- **The four E6 tests:**
  - they reach `write-scope.js:23-24` with exactly the hook and reason text they assert;
  - no redaction rule changes those messages;
  - the numstat zero-deletions check keeps the M2.3/M2.4 test lines intact.
- **E1-E7 anchors:** each occurs exactly once in plan, at lines 350, 430, 444, 3170, 3292, 3298 and 3347.
- **Correlation records:** the R8 researcher and R8 planner records exist (ev:14568-14596, ev:14652-14775).

### Findings

1. **P:339 (E1, "Restriction and usage description") — High.**
   - **Issue.** E1 turns D2's reading into a plan-wide settled rule: any agent `description`, any CLAUDE.md pipeline position and any plan "not required" "does not forbid a human-authorized task". Read literally, that also releases these conditions in every phase:
     - coder.md:3 ("Use only after plan-reviewer outputs APPROVED");
     - tester.md:3;
     - explainer.md:3;
     - CLAUDE.md:90-92 and :104.
   - **Why it matters.**
     - It is an authority change by interpretation. It contradicts P:40-44 and plan:430 ("a limited scheduling accommodation …, not permission to relax").
     - D2 itself calls the reading uncertain and leaves it to the human (P:153).
     - Only two accommodations are needed, both inside Phase 2: R9.8 and R9.10.

2. **P:237, P:290, P:320, P:372-375 — High.**
   - **Issue.**
     - R9.4 (apply) and R9.5 (coder) require only that R9.3 happened, not that the plan review returned APPROVED.
     - NEEDS_REVISION sends the work to the planner as a new file (P:374). No re-review of that file is defined, because `-rN` is reserved for "a re-review of this proposal" (P:290, P:373).
     - P:320 lets blocks be "adjusted to the decisions" but has no rule for edits that go beyond choosing an alternative.
   - **Why it matters.**
     - coder.md:3 and CLAUDE.md:90 put the coder after an APPROVED plan review. Together with finding 1, the order as written lets R9.5 implement text that no one reviewed.
     - A review of a revised proposal made on the same day would also take this review file's name.

3. **P:270-277, P:392, P:396, P:398 (the preliminary tester) — High.**
   - **What the prompt leaves out.** The R9.9 prompt excludes only As built, the final review and the human-only commands. It does not tell the tester that these are also later steps, by design:
     - the explainer's connection;
     - the ledger;
     - the P2.x/C2.x assessments;
     - Revision 8 Closure (plan:346: missing connections "remain blocking");
     - Validation "Order" (plan:3347, as E7 amends it).
   - **Why that matters.** tester.md:32 and :36 make the tester read the plan's validation and run whatever it specifies. A tester that reports any of these as missing returns NEEDS_FIXES. The explainer then cannot start, and the cycle that P:205-212 cuts forms again through the tester's judgment.
   - **The in-scope set is not enumerated.** The tester can run numbered commands 2-4, 8-10 and 12-17. Commands 1, 5, 6, 7 and 11 are human-only.
   - **The R9.10 trigger is too loose.**
     - It accepts "an R9.9 run" (P:396), not the latest run.
     - It checks neither the verdict file's `Head:` nor its `Branch:`.
     - So a READY_TO_PUSH written for a superseded HEAD satisfies it.
   - **The R9.10 prompt overstates.** It calls the state "validated" (P:398) without saying that Phase 2's final review and the human's Validation are still pending. The journal is a tracked doc, so it could overstate acceptance.

4. **P:134 and P:136, against P:333 and P:447 (5b and 5d) — Medium.**
   - **Issue.** D1 calls the 5b difference "the length-shortfall branch versus a third-token mismatch". E1 and E5 column 3 require "the same decision branch".
   - **What the source shows.**
     - Both differences are sub-conditions of `startsWithTokens` (bash-allowlist.js:97-99) against `dotnet format --verify-no-changes`. Both end at `matched === undefined` (bash-allowlist.js:190-192).
     - 5d has the same difference, and D1 does not state it. The test input `dotnet build` (two tokens) fails the length condition against `dotnet list package` (bash-allowlist.js:56). The three-token target fails the second-token comparison instead.
   - **Why it matters.** "Branch" is used at two granularities.
     - Under the finer one, 5b and 5d are not covered by E1's own definition.
     - Under the coarser one, D1 mislabels 5b and leaves 5d's difference unstated.

5. **P:168, P:172-174, P:256, P:341 and P:399, against P:614 (the journal number) — Medium.**
   - **Issue.**
     - D3-A and Disposition 2 allow a later PR-end entry to reuse the number with "a different slug".
     - E2 checks only the exact path (P:399).
     - Phase 3 Validation forbids any entry that starts with `<NNNN>-` (P:614).
     - The exact-path check cannot run before the explainer does, because the human supplies only the number (P:397) and the explainer picks the slug (explainer.md:36).
   - **Repository practice (verified).**
     - 0001-0008 are unique and follow the merged roadmap rows in order: rows 1, 2, 3, 4, 5a, 5b, 6a and 6b, merged as GitHub #1, #2, #3, #4, #6, #8, #11 and #12 (docs/roadmap.md:45-57). Each heading reads `# PR 000N`.
     - Chore rows "take no number of their own" (docs/roadmap.md:62-64). Neither chore row has a journal entry.
     - The one non-PR entry is `note-2026-09-18-…`.
   - **Why it matters.** Two rules contradict each other, and one of them cannot be executed. P:168 also does not tell the human that 0009 would be the first numbered entry for a chore, and that it would shift PR 7's number.

6. **P:394, P:395, P:601, P:607-611, P:615 and P:663 (retention and retry) — Medium.**
   - **(a) The move can overwrite.** Nothing checks the destination before `mv`. Plain `mv` replaces an existing file (UNVERIFIED for this Git Bash; a `test ! -e <destination>` check before the move settles it either way). `<HEAD7>` and `<n>` are undefined, so a repeat run at the same HEAD can overwrite a retained report.
   - **(b) The hook-log checks break on a repeat run.** The "exactly one line" checks on `grep -F LIVEPROBE-5c` and `grep -F LIVEPROBE-5e` run over the whole append-only log, so they fail by construction on any repeat that P:395 allows. The R9.10 check also has no agent_id, session_id or time-window filter, so on a repeat it can pass on a line an earlier run wrote.
   - **(c) Tool records are missing.** Neither step extracts the tool_use input and the tool_result of the two `git log --grep` operations, which plan:3297 requires.
   - **(d) The final tester has no retained name.** If R9.12's tester must run again, the check at P:663 fails, because the first final verdict file still occupies the fixed path.

7. **P:103-105, P:192-201, P:235, P:366, P:369, P:415, P:500 and P:502 (identity and reuse) — High.**
   - **(a) CLAUDE.md is not hashed.** CLAUDE.md is in the reuse condition (P:369) but not in the 15-line hash list (P:366, P:502). The restart trigger, "a file that R9.1 lists" (P:415), is therefore ambiguous for it.
   - **(b) `.claude/settings.local.json` is not covered.**
     - It exists and is gitignored (.gitignore:45), so neither `git diff` nor an empty status can show it unchanged.
     - It can disable every hook (plan:3816).
     - This evidence file already recorded its sha256 at H2.1 (ev:181).
     - R9.1 and the reuse condition both omit it.
   - **(c) Current hashes are not historical provenance.**
     - R9.1's hashes are current values. They cannot show the state at a68dd16, 5064146 or 5616242. Yet P:105 says R9.1 "supplies" the missing hashes, and P:369 calls it "the 'unchanged' record".
     - For tracked files, the historical basis is the commit chain already recorded (ev:14552, ev:14560, ev:14640, ev:14823), plus each record's status check.
     - The one historical agent-file hash, for code-reviewer.md at a68dd16 (ev:14261-14263), is never compared.
   - **(d) D5 contradicts itself, and the version has since changed.**
     - D5 calls the version "context, not a condition" (P:194), yet says reuse "rests on … the recorded version 2.1.292" (P:201).
     - The caller reports Claude Code 2.1.293 while records continue in session 340a3876 (ev:14652-14656 and this run). Session plus version therefore no longer pins one version.
     - ev:14070-14090 is a preflight made before the restart. It does not show when the session started.
     - The proposal does not say whether an update counts as an executable change under plan:348 and plan:448.
     - Which version ran R9.2 and R9.3 is UNVERIFIED. To settle it, run `grep -o '"version":"[^"]*"' <transcript> | sort | uniq -c` on agent-ae5c9090f4055b6e2.jsonl and on this run's transcript. Whether that field exists at all is also UNVERIFIED.
   - **(e) One earlier record is missing.** P:369 names three earlier records. The R8 planner record (ev:14653) is a fourth.
   - **(f) The order has already changed.** R9.2 was recorded and committed with no R9.1 before it (ev:14652-14833). So the dependency "R9.2 | R9.1" (P:235) and the status expectation at P:500 no longer describe the order that ran.

8. **P:46, P:332 and P:450 (refused versus never requested) — Medium.**
   - **Issue.** E5 column 6 offers only "exact live block" and "not called". E1 and the Non-goals call all 16 targets "inconclusive".
   - **What the plan's terms say.** "Inconclusive" means an attempt in which the agent refused without a tool call (plan:446, :3272, :3280).
     - Requested and refused: 6 targets. These are 2a-2d (both P2.2 attempts) and the two code-reviewer targets (ev:14253-14255).
     - Never requested: the other 10, namely six `src/LIVEPROBE-4-<agent>.txt` targets and 5a, 5b, 5f and 5d (plan:3280, :3294). For these there is no tool-call list to cite, and a `grep -F` absence proves no call (plan:3290).
   - **Why it matters.** The ledger would misstate the record type for 10 rows. Neither class is a live hook block, and both remain UNVERIFIED end to end.

9. **P:182-185, P:242, P:385, P:388-389, P:410 and P:580 (approvals and the review chain) — Medium.**
   - **(a) The tester does not wait for DB_APPROVED.** R9.9 depends on "R9.8" (P:242), not on DB_APPROVED, although tester.md:3 requires APPROVED reviews first.
   - **(b) DB_NEEDS_REVISION has no path back.** It goes to the coder (P:389), but there is no R9.7 re-review, no db re-review, and no `[-rN]` in the db-review file name (P:388, P:580). db-reviewer.md:54 has one.
   - **(c) The R9.7 re-review range is unstated** (P:385).
   - **(d) D4-A starts at the wrong review.** Its base is "the last R9.7 review", not the last APPROVED one. Coverage from 84ebd90 holds only if that link is APPROVED.
   - **(e) The precedent does not match.** The first Phase 1 review covered `f6df221..de44869`, but the re-review started at `3d26eed` (plan:3) (P:185). That only doc commits lie between them is UNVERIFIED (`git diff --name-only de44869 3d26eed`).
   - **(f) D4-A conflicts with plan:430.** Under A, the preliminary R9.7 review becomes a link in the final review's coverage. plan:430 says preliminary reviews do not "replace the review … of the final evidence-bearing state". E2 hardcodes A (P:410) without stating that reading.

10. **P:506 and P:592 (the correlation-check specification) — Medium.**
    - **Issue.** "The assertions of ev:14576-14587, changed to agentType, hook and target" leaves every other fixed value of that template in place:
      - agentId and sessionId;
      - three file hashes;
      - the tool_use id;
      - `requestShape: "background"`;
      - the tool list {Read, Glob, Grep, Write, SubagentHandback}.
    - **Why it matters.**
      - The R9.2 check that actually ran (ev:14652) already changed more than three parameters.
      - P:592 reuses "the R9.2 assertions" for the tester and the explainer, whose tool lists include Bash and whose P2.1 values are sonnet/low and opus/xhigh. It also reuses them for the reviewers, which may use LSP or MCP tools.
      - A faithful copy of the check fails on a correct run, or the human has to guess which assertions to change.

11. **P:365 and P:637 — Low.** Two expectations contradict the fix path that P:415-418 allows: "every hash equals the post-R9.5 record", and the name-only diff being "exactly `.claude/hooks/tests/write-scope.test.js`".

12. **P:117 — Low.**
    - "The other scopes have allow tests only" is wrong for `thoughts/shared/research` and `thoughts/shared/plans`, which have no test at all (write-scope.test.js:121-129).
    - After R9.5 those two scopes have a block test but no allow test. Their allow path rests on the live records at ev:14586 and ev:14772, and the ledger does not say so.

13. **P:300, P:377-380 and P:571-574 — Low.** R9.6 is described as "bound to HEAD", but it records no `git rev-parse HEAD`, which plan:433 lists as record context.

14. **P:336, P:372, P:402 and P:531 — Low.**
    - The R9.3 Range `<git merge-base main HEAD>..<HEAD>` differs from the range supplied for this run.
    - P:336 and P:402 place the ledger inside P2.5, while E5 (P:441-444) makes it a sibling item.
    - "This plan path" at P:531 does not say which document the coder receives.

## Required changes

1. **Confine E1's restriction/usage reading to Phase 2.**
   - Limit it to the Revision 9 execution order and name exactly two accommodations:
     - R9.8: a db-review of non-schema Phase 2, after an APPROVED R9.7 and before the tester;
     - R9.10: the explainer's entry for an unfinished PR.
   - Present both as the human's D2/D3 decisions.
   - State that agent descriptions, CLAUDE.md pipeline conditions and plan requirements otherwise keep their force. Name coder.md:3, tester.md:3, explainer.md:3 and CLAUDE.md:90-92 and :104.
   - Keep plan:430's "limited scheduling accommodation" framing.

2. **Gate R9.4 and R9.5 on an APPROVED plan review.**
   - The review must be the highest `-rN` review of the proposal text being applied.
   - A review of a revised proposal file is a re-review and takes the next `-rN`.
   - Edits beyond choosing among reviewed alternatives go back to review before R9.5.

3. **Make the preliminary tester run self-contained.** In E2 R9.9 and P:270-277:
   - Enumerate the scope:
     - tester.md steps 2-8;
     - the numbered commands 2-4, 8-10 and 12-17, with their output conditions;
     - the two P2.5 operations.
   - State that the following are later steps by design and are not conditions of this run, while any implementation defect stays reportable:
     - the connection records and the ledger;
     - the P2.x/C2.x assessments;
     - Revision 8 Closure;
     - As built and the final review;
     - Validation "Order".
   - State that all of them remain conditions of R9.12 and of the human's READY_TO_PUSH.
   - Bind the R9.10 trigger to the latest R9.9 run. Its file's `Verdict:` line must read READY_TO_PUSH, and its `Head:` and `Branch:` lines must equal the commit it ran on and the current HEAD.
   - Add to the R9.10 prompt that Phase 2's final review and the human's Validation are still pending.

4. **Fix the meaning of "decision branch".**
   - Define it once in E1, at the level of the hook's final `block` call and its reason (bash-allowlist.js:190-192; write-scope.js:23-24).
   - Restate the 5b difference in D1 and in the ledger as an input difference inside `startsWithTokens` (bash-allowlist.js:97-99).
   - Add the matching 5d difference, against `dotnet list package`.
   - If the finer granularity is chosen instead, 5b and 5d become gaps that need a D1-style decision. Either way, use one definition.

5. **Adopt one journal-number rule.**
   - Apply the number-prefix check (no entry starts with `<NNNN>-`) before every explainer run, in D3, Disposition 2, E1 (P:341), E2 (P:399) and P:614.
   - Drop the same-number, different-slug option.
   - Describe repository practice accurately:
     - one number per merged roadmap PR;
     - no entry for chore rows;
     - `note-` for an entry that is not a PR.
   - Tell the human that 0009 would be the first entry for a chore and would shift PR 7's number. The human decides.

6. **Make retention and retries safe.**
   - Add a `test ! -e <destination>` check before `mv`, and stop if the destination exists.
   - Define `<HEAD7>`: take it from the verdict file's `Head:` line, checked against the commit the tester ran on.
   - Define `<n>`: the smallest positive integer whose destination does not exist; record it.
   - Define a retained name for a final-run verdict file before any repeated R9.12 tester.
   - Replace the global "exactly one line" checks with checks filtered by agent_id, session_id, exact target and the call/result window, as P:603 already does.
   - Require the tool_use input and the tool_result for each P2.5 operation (plan:3297).

7. **Complete the identity record and separate current from historical state.**
   - Add CLAUDE.md and `.claude/settings.local.json` to the R9.1 hashes. Either add `settings.local.json` to the reuse condition, or put it in the stated boundary with ev:181 as its comparison value.
   - State that R9.1's hashes are current-state baselines.
   - Base the historical "unchanged" claim on two things:
     - the recorded commit chain (ev:14552, ev:14560, ev:14640, ev:14823) plus each record's status check;
     - a comparison of code-reviewer.md against ev:14263.
   - Resolve D5. Treat the version either as context only (remove it from "reuse rests on") or as a condition (then 2.1.293 needs a human decision on re-collecting the records).
   - Require every new connection record to carry the Claude Code version that produced it.
   - State whether a Claude Code update triggers step 5's restart.
   - Add the R8 planner record to R9.1's list.
   - Update P:235 and P:500 to the order that actually ran.

8. **Split the ledger's live-record column into three values.** Apply the split in E5 column 6, E1 (P:332) and the Non-goals (P:46):
   - **Exact live block:** 2e.
   - **Requested, no tool call (inconclusive):** six rows, each with its attempt's tool-call list.
   - **Never requested:** ten rows. The `grep -F` output is corroboration only (plan:3290).

   Keep all 16 rows UNVERIFIED end to end.

9. **Make the approvals and the review chain explicit.**
   - Require DB_APPROVED (the highest `-rN`), for the same code state as the APPROVED R9.7, before R9.9.
   - On DB_NEEDS_REVISION, define the sequence: a coder fix, then an R9.7 re-review, then an R9.8 re-review with `-rN`. Add `[-rN]` to the db-review name at P:388 and P:580.
   - State the Range of R9.7 re-reviews.
   - Set D4-A's base to the Head of the last APPROVED R9.7 review, with DB_APPROVED at that state.
   - Correct or qualify the plan:3 precedent.
   - If A is chosen, state in Revision 9 how it reads plan:430: the APPROVED preliminary R9.7 review is one link in the final review's coverage.

10. **Specify the correlation check as a per-run parameter list.**
    - The parameters:
      - agent id and session id;
      - transcript, meta and report hashes;
      - the tool_use id;
      - the allowed tools: the profile's `tools:` frontmatter plus SubagentHandback;
      - the P2.1 model and effort;
      - the hooks: path-guard plus `write-scope:<dir>`;
      - HEAD and status.
    - Cite ev:14652 as the template that actually ran.
    - Do not assert `requestShape`.

11. **Compare against the latest record.** At P:365 and P:637, compare against the latest R9.1 record, and against the files changed by R9.5 and by any approved fix.

12. **Correct P:117.** Record in the ledger that the allow path for the research and plans scopes rests on the live records at ev:14586 and ev:14772.

13. **Add `git rev-parse HEAD` to the R9.6 record.**

14. **Make three small corrections.**
    - Align the R9.3 Range with the range actually supplied for this run.
    - Make the ledger's placement consistent across E1, E2 and E5.
    - Name the exact plan path and phase the coder receives (P:531).
