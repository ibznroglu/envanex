Verdict: APPROVED
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md phase 3-4
Range: 2e8497a9220ae3ec8789fbc551270dd5538c609c..b17ff2e06bd615ac631890d4ab11733706edeb9f
Head: b17ff2e06bd615ac631890d4ab11733706edeb9f

## Findings

### Method

- **Subject.** This re-review covers Revision 3 (b17ff2e). It is checked against the r2 review (7793b05, Head 54cf1e7).
- **What I read.**
  - The main session's plan diff for 54cf1e7..b17ff2e: 241 lines, sha256 396e26e4…, as relayed by the caller.
  - The changed passages of the plan and their callers.
- **What I ran.** Nothing.
- **Notation.** `plan:N` is line N at b17ff2e.
- **Scope.**
  - Revision 3 states that it applies only R1-R3 and decisions 21-23 (plan:26-36, :124-137).
  - The diff confirms this. Appendix A is untouched.

### R1 — closed

- **The new measurement.** m2 now records `m2-before=` and `m2-after=` around the call: each line is the log's sha256 and its line count (plan:1389-1391).
- **The reading (plan:1444).**
  - PASS is `rc=3`, the exact open line, and an unchanged log of one line.
  - DEFECT is an rc other than 3 with the log unchanged.
  - INCONCLUSIVE is an rc other than 3 with lines added.
- **Why this works.** An allowed append open now reads as INCONCLUSIVE, not DEFECT. A failed open that the script mishandles still reads as DEFECT.
- **Unchanged.** d2's first-line reading stays, which is correct because gate.sh truncates its record.
- **Per-probe scope.** d2 bears only on P3.7 and m2 only on P3.8 (plan:1437, :1450, :1454, :1456).
- **Open question.** Whether Windows allows the append open is still UNVERIFIED. `m2-readonly` and the before/after lines settle it.

### R2 — closed

- **The copy commands (plan:279-289).**
  - Each copy is `gates-<label>-<HEAD7>-<n>.txt`, with the smallest free `n`.
  - The path is checked with `test ! -e` before `cp`, and the command prints both hashes and `copied=$? path=…`.
- **The recorded path** is the only one used afterwards: for `GC` (plan:342) and for the hash checks.
- **Callers.** Every caller now uses these commands:
  - P3.1 steps 4-5 (plan:1202-1203)
  - P3.2 step 6
  - P3.6, where the pass also compares the recorded copy's sha256
  - P4.4 steps 4-5. A failed step-5 run keeps its copy unused, and the re-run takes the next `n`.
  - the Phase 3 and Phase 4 human validation
- **No stale names remain.** A grep for the old names (`<HEAD7>.txt`, `gates-p3.1-`, `gates-3-tester-`, `gates-pr-end-…`) finds nothing.

### R3 — closed

- **What counts as a harness failure** (plan:1404, :1435, :1457-1463):
  - a `STOP:` line
  - a missing `== done`
  - a missing `removed=`, or one other than `removed=0`
  - a visible error from a harness command inside a case
- **The consequence.** On such a run neither probe passes. It is not automatically a defect of the scripts under test. The human records it, resolves it and re-runs.
- **The defect rule.** It now applies only on a run without a harness failure (plan:1464).
- **No false PASS.** A real defect cannot pass this way: every passing run needs `setup=0`, `== done`, `removed=0` and all required lines.
- **Rollback.** The scratch-path bullet agrees: harness failure, re-run after cleanup, and cleanup only of the run's own directory.
- **N7.** This rule also covers N7's failure of a harness command inside a case.

### Earlier closures

- **B1-B5 and L1-L3 remain closed.** Revision 3 does not change:
  - the required P3.7/P3.8 cases or their expected lines
  - the provenance rules of item 8: remove before each used run, a record belongs to the run that printed its hash, and how success is established (plan:277-278, :290-297)
  - the tester.md text
  - D1
  - the Diagnostics
  - the residual-risks heading
- **Scratch ownership is unchanged and still satisfied.**

### Non-blocking observations

- **N1-N7.** They stay as recorded in the earlier reviews. N7 is now addressed by R3's harness rule.
- **N8 — The variable `L` and the validation directory.**
  - **The variable.** The copy commands set the shell variable `L` to a label (plan:282). The runner sets `L` to the log path in the same interactive shell (plan:494). In every sequence the plan specifies, the copy comes before the runner whose log is recorded, and the runner prints `recorded: <path>`. So the overlap is harmless. A distinct name such as `LBL` would remove it.
  - **The directory.** `cp` also needs `TestResults/chore-agents-workflow-skills/validation/` to exist. If it does not, the copy prints `copied=1` and can simply be repeated.

## Required changes

None.
