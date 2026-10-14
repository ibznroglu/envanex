Verdict: NEEDS_REVISION
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md phase 3-4
Range: 2e8497a9220ae3ec8789fbc551270dd5538c609c..54cf1e751cf6f10a6b8c9d5b527212b280835365
Head: 54cf1e751cf6f10a6b8c9d5b527212b280835365

## Findings

### Method

- **Subject.** This re-review covers Revision 2 (54cf1e7). The first review is `…-plan-review.md`, committed as b06e223 against Head 6aa0305.
- **What I read.**
  - The main session's plan diff for 6aa0305..54cf1e7: 915 lines, sha256 bb35707f…, as relayed by the caller.
  - Every changed section of the revised plan, with its surroundings.
- **What I ran.** Nothing.
- **Notation.** `plan:N` is line N at 54cf1e7. `src:N` is line N of `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`.
- **Not re-investigated.** I did not repeat the investigation of sections Revision 2 leaves alone.
  - Appendix A is unchanged: plan:1838-2036, 199 lines.
  - D2's markers are still unique, at plan:1836 and plan:2038.
- **UNVERIFIED claims.** Any runtime claim below is marked UNVERIFIED, with the line or command that settles it.

### Closure of the first review's findings

- **B1 — closed, apart from regression R1 below.**
  - **Deterministic failed-open cases.** Each script now has one: a directory placed at the record or log path.
    - d-open: plan:1278-1280, expected at :1378
    - e9-open: plan:1305-1307, expected at :1389
    - f7-open: plan:1342-1344, expected at :1402
    - Each expects `rc=3`, one exact reason line and an empty directory.
    - That such a directory makes the open fail is UNVERIFIED. These three lines settle it.
  - **The chmod cases are supplementary.** They are at plan:1348-1362 and are read at :1406-1412.
  - **Outcome rules (plan:1414-1427).**
    - INCONCLUSIVE never passes.
    - Only the guarded cases can be inconclusive.
    - The follow-up decision is fixed, and so is the residual text.
  - **Not-a-work-tree cases.** They run behind `notree-guard` (plan:1262-1269, :1301-1304, :1338-1341). `GIT_CEILING_DIRECTORIES=S` is passed to the script under test, so no script can reach a parent repository.
    - Whether Git for Windows honours a `C:/` ceiling is UNVERIFIED. `notree-guard` settles it.
    - If the ceiling is not honoured, the cases skip and are INCONCLUSIVE. That failure mode is safe.
  - **Evidence sufficiency.** The exit-3 failed-open path is now executed for all three scripts without chmod. An inconclusive chmod case removes no required evidence.
  - **Docs claim.** The old claim is gone. plan:1593 and plan:1597 match what P3.7/P3.8 execute, and ai-workflow.md item (10) at plan:1650 states it too.
- **B2 — closed.**
  - **mutate-run.sh** (plan:1284-1307, table :1384-1389). Cases e1-e9 reach:
    - usage: no arguments, label, mode, and both count errors
    - untracked target, branch, mkdir and not-a-work-tree
    - the failed open
  - **mutate-clean.sh** (plan:1311-1346, table :1395-1403). Cases f0-f2, f4-f7 and q reach the same failures. q also reaches a git exit other than 0 or 1.
  - **Unreached branches** are named in residual (10) (plan:1593) and in Coverage (plan:1436-1441):
    - the `cd` failure of all three scripts
    - base-check
    - I/O failures after a successful open
- **B3 — closed.**
  - **Item 8 (plan:249-260)** sets three rules:
    - the live record is removed before each used run, recorded as `absent=0`
    - a record counts only for the run whose complete stdout printed its hash
    - success is shown without inferring an exit code
  - **P4.4 step 5 (plan:1744-1756)** ties the `pr-end-gate` copy to its own `/gate` run:
    - the tool rows and the hash
    - the last line `gate-exit=0 failed=`
    - the error flag
    - G0-G11
    - a header time later than the `pr-end-tester` copy's

    A failed check re-runs `/gate` and never falls back to an older copy.
  - **The inference holds by the stdout spec (plan:717-735).** A last stdout line `gate-exit=0 failed=` implies a verified record and a complete summary.
- **B4 — closed (plan:1451-1458, :1464-1469).**
  - The precondition is `fresh=0`.
  - Every attempt and infrastructure rerun takes a new label.
  - sha256 and `wc -l` are recorded after step 4.
  - The pass rule is now "exactly, in order".
- **B5 — closed.**
  - D1 is `wc -l CLAUDE.md` (plan:406), with a whitespace-tolerant expected line (plan:420).
  - The tester-runnable note now excludes redirection (plan:432).
- **L1 — closed.**
  - tester.md steps 3-5 and 11 (plan:762-804) cover a run with incomplete stdout or a `head=` mismatch:
    - the tester returns NEEDS_FIXES with `Gates-sha256: none`
    - `none` never pairs with READY_TO_PUSH
    - `gates.txt` is not read for an incomplete run
  - P3.6 requires a 64-hex hash for READY_TO_PUSH (plan:1499-1501).
- **L2 — closed.**
  - All three scripts now have Diagnostics (plan:664-673, :897-908, :928-932). Each gives exact reasons and discards the start-up commands' own output.
  - Every P3.7/P3.8 "exactly" line matches one of those reasons.
- **L3 — closed (plan:1648).**
- **Scratch ownership (decision 15) — satisfied.**
  - **Hard stop.**
    - The EXIT trap is installed first (plan:1251).
    - An existing `S` stops the run with exit 8 before any change (plan:1254).
    - `mkdir -- "$S"` without `-p` also fails on any existing entry, which closes the check-then-create race (plan:1255).
  - **Setup checked.** Every setup step ends in `STOP: setup failed: …` (plan:1257-1260), and `setup=0` is required (plan:1373).
  - **Cleanup only of its own directory.**
    - `CREATED=1` is set only after this invocation's `mkdir` succeeds (plan:1256).
    - `finish` changes to `/` and removes `S` only in that case. Otherwise it prints `removed=skipped` (plan:1247-1250).
    - The rollback note (plan:1817-1820) agrees.
- **N1-N4.** They stay non-blocking. Revision 2 adds nothing that changes their assessment.

### Regressions and new defects

**R1 — Low-Medium — The m2 reading turns an allowed open into a DEFECT (plan:1412, with :1356-1361 and :1418, :1421, :1423).**
- **Why the first line is the wrong test.**
  - mutate-clean.sh's log is opened in append mode (plan:909, :937). The h-l log at plan:1398 and P3.4's log depend on that.
  - Suppose the filesystem allows the append open despite chmod. The script then appends its records and exits 0, and `m2-first-line` still prints `old`.
- **The result.**
  - "The same reading" as d2 then yields DEFECT: an rc other than 3, with the first line `old`.
  - INCONCLUSIVE ("record rewritten") can never be observed for m2.
  - A supplementary DEFECT "fails the probe" (plan:1423), so the phase is falsely sent back to the coder.
- **d2 is not affected.** gate.sh truncates, so an allowed open rewrites the first line.
- **Open question.** Whether Windows allows the append open is UNVERIFIED. `m2-readonly` and `m2-log` settle it.
- **Related.** Both outcome rules say "no supplementary case is a DEFECT" (plan:1418, :1421). They do not tie d2 to P3.7 and m2 to P3.8.

**R2 — Low — A repeated gate run at the same HEAD has no copy name (plan:252, against plan:1168, :1494, :1747 and :1756).**
- Item 8 now says "A copy is never overwritten or reused for another run".
- Yet P4.4 step 5 re-runs `/gate` at H_R when a check fails (plan:1756). P3.1 or a tester run can also be repeated at the same HEAD, for example after Docker was down.
- The name `gates-<label>-<HEAD7>.txt` is then taken, and the plan's own `cp` commands (plan:1168, :1494) silently overwrite it. The human must either break the rule or invent a name.

**R3 — Low — A cleanup failure is classed as a defect of the scripts under test (plan:1404 and :1424, against plan:1820).**
- `removed=0` is a required line.
- plan:1424 says "A required line that differs from the table is a defect. The phase goes back to the coder".
- A non-zero `removed=` is an environment failure the coder cannot fix, for example a Windows file lock during `rm -rf`.
- The rollback note (plan:1820) says how to clean up, but not what the probe outcome is. Setup failures already have the right reading: "neither probe passes" (plan:1373).

### Non-blocking observations

- **N5 — An f3 that does not exist (plan:18, :1420).** "f0-f7" names an f3, but the cases are f0-f2 and f4-f7.
- **N6 — The stderr rule for exit-3 paths is incomplete (plan:664-667, :897-900).**
  - "Exactly one line to stderr" applies to every exit-3 path. But the discard list names only the failed open's shell message.
  - bash's own message on a failed write, such as `printf: write error`, would add a second line.
  - These paths are review-only (residual (10)), so the code review should check that record writes are silenced as well.
- **N7 — Unchecked harness steps inside the cases.** Examples are plan:1270-1277, :1293-1300 and :1330-1337.
  - The `git checkout`, `printf`, `rm` and `mkdir -p` steps are not checked.
  - q assumes that `git branch --show-current` prints `unborn` on an unborn branch. This is UNVERIFIED, and q's own output settles it.
  - A failure in either would show up as a differing required line. That is unlikely in a fresh scratch repository. R3's fix can cover it if extended to harness errors.
- **N1-N4** carry over unchanged from the first review.

## Required changes

1. **m2 reading (R1).**
   - Replace `m2-first-line` with a measure that an append cannot hide. Examples: `m2-lines=$(wc -l < TestResults/scratch/mutations/ro.txt)`, or the file's sha256 before and after.
   - Read the outcome as follows:
     - **PASS:** `rc=3`, the exact open line, and the log unchanged (one line, `old`)
     - **DEFECT:** an rc other than 3, with the log unchanged
     - **INCONCLUSIVE:** an rc other than 3, with lines added
   - Tie each probe's DEFECT condition to its own supplementary case: d2 for P3.7, m2 for P3.8.
2. **Gate copy names (R2).**
   - Give a repeated run at the same HEAD its own copy name, for example `gates-<label>-<HEAD7>-<n>.txt`. Let `n` be the smallest free integer, and check it with `test ! -e` before `cp`, as the retention rule does.
   - Use that name in P3.1, P3.6, and P4.4 steps 4-5.
3. **Cleanup outcome (R3).** State the following:
   - A `removed=` other than 0 means neither probe passes on that run.
   - The human cleans up as plan:1820 says, and re-runs.
   - This is not a defect of the scripts under test, and not a reason to send the phase back to the coder.
   - Optionally, classify an error from a harness command (N7) the same way.
