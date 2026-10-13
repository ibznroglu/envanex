Verdict: NEEDS_REVISION
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md phase 3-4
Range: 2e8497a9220ae3ec8789fbc551270dd5538c609c..6aa0305158aa14977e5d22bd3fd8b29352ba3979
Head: 6aa0305158aa14977e5d22bd3fd8b29352ba3979

## Findings

### Method

- I read three things, and ran nothing:
  - the whole plan
  - every repository file it changes or cites
  - the inherited sections of the source plan
- `plan:N` is line N of the reviewed plan. `src:N` is line N of `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`. That file is unchanged since 2e8497a.
- Runtime behaviour I could not settle by reading is marked UNVERIFIED, with the command that settles it.

### What holds

- **Bootstrap, restarts and commit order (plan:118-147).**
  - No stage relies on a tool before the restart that installs it.
  - P3.5 is the first commit after R3.
  - P3.6 runs the tester as loaded after R3.
  - The new `/pr` is first used at P4.4.
  - The restart rule (plan:197) covers both code commits.
  - The S0 order matches decision 6. This review is S0.3, on P0 = 6aa0305.
  - Late records reach As built through the next copy and the final evidence commit (plan:194-196).
  - Nothing can be committed after the PR-end tester, so `pr-end.md` stays local by necessity.
- **Inherited requirements.**
  - Phases 3-4 of the source plan (src:22820-23186) are carried in full.
  - Each deviation is declared (DV1-DV15).
  - The re-evaluation that src:417 requires is done.
  - The "Touches schema? No" and "ADR? No" answers hold.
- **Anchors match the repository.** I checked these against the current files:
  - **settings.json.** `.claude/settings.json:48`. It holds 48 allow and 94 deny entries, so X3 is `48a49,51` and X4 is 51/94.
  - **Agent files.** `tester.md:18-63`, `coder.md:35-36`, and `explainer.md:22`, `:36-37` and `:42`. D10's lines 36, 37 and 44 follow.
  - **Skill files.** The `description:` lines of `/commit` and `/pr` are unchanged.
  - **Hooks.**
    - `path-guard.test.js:44` and `:59` (DV6)
    - `guard-common.js:45` (DV7)
    - `bash-allowlist.js:36`
  - **docs/roadmap.md.** Lines `:58`, `:59`, `:197-198`, `:200`, `:204`, `:207-209`, `:220` and `:236-237`.
  - **Other files.**
    - `CLAUDE.md:160`
    - `db-reviewer.md:45`
    - both design-time factories at `:12`
    - `.config/dotnet-tools.json`, which `dotnet tool restore` needs
- **ENVANEX_CONNECTION_STRING.** Only the two design-time factories read it, so the new `export` line cannot reach a test host.
- **Appendix A (plan:1566-1768).**
  - It has 199 lines (plan:1568-1766), and D2's awk and sed extract exactly those.
  - D4 matches only line 8, and no `/verify` remains.
  - It keeps F20's four Windows notes (research:315-320) and the seven Verification rules.
  - It keeps the outbox same-transaction invariant (decision 10).
  - It agrees with the exact skill texts. The security-review header lives in `pr/SKILL.md`, the exemption is the same, and the tester takes its file list from `gates.txt`.
- **Mutation cleanup (DV3, plan:834-847).**
  - `git diff --exit-code HEAD --` plus `git diff --cached --exit-code --` compares both the working tree and the index with HEAD.
  - P3.8 cases h-l discriminate, and none depends on chmod. Case i is the one that plain `git diff --exit-code` misses.
- **P3.4 safety reading (plan:1216).** I agree with it, as a static reading that stays UNVERIFIED.
  - `CLAUDE_PROJECT_DIR` is `C:/projects/envanex` (src:1385), so the mutated `toSlashForm` still yields an absolute project dir.
  - A backslashed target is joined as a relative path that has no protected segment.
  - So path-guard should allow the inverse Edit.
- **Validation commands.**
  - Every "both" command passes bash-allowlist's precheck (`bash-allowlist.js:62-95`), and none has a `|` inside a pattern.
  - The counts are right:
    - X7 = 10 and X8 = 16
    - D6 = 16 lines, which match the current frontmatter
    - D9 = 9 numbered entries plus the note
    - G7 = 7 lines, in the format of the R9.6 record (`ℹ` under redirection, src:20836-20842)
    - G10 as at src:2932-2934
  - X9 and X10 equal the exact frontmatter texts.
- **UNVERIFIED claims that the plan's own commands settle.**
  - `git commit -F` without `-e` cleans up in `whitespace` mode unless `commit.cleanup` is set, and that should equal `git stripspace`. P3.5's `msg-diff` settles it.
  - The .NET 10 output formats that G9 and G10 assume. The first P3.1 run settles them.
  - The `gh pr merge --match-head-commit` flag. P4.4 step 7 settles it.

### Defects

**B1 — Medium — P3.7/P3.8 have no pass rule, and their unwritable-record and unwritable-log evidence rests on chmod.**
Where: plan:1124-1205, plan:1127, plan:1350, and residual item (10) at plan:1348 and plan:1401.
- **No pass rule.**
  - P3.1-P3.6 each have a "passes when" list. P3.7 and P3.8 have only an expected-line table.
  - The table calls cases a, d and m "inconclusive" when `parent-is-worktree`, `d-readonly` or `m-readonly` is off (plan:1187, :1191, :1202).
  - It never says whether such a case fails, leaves the probe open, or what follows.
- **What depends on chmod.** Cases d and m are the only executions of two exit-3 paths:
  - the open failure of gate.sh's record (decision 8)
  - the open failure of the mutation log
- **If d or m is inconclusive, the remaining evidence is not enough.**
  - Nothing else executes those paths. What remains is static review and X12's `bash -n`, which checks syntax only.
  - plan:1350 would then be false. It says "P3.7 and P3.8 now execute ... the unwritable-record and unwritable-log cases".
  - The fixed residual text, item (10), would also leave the path out.
- **What does not depend on chmod.**
  - The start-up and argument cases b, c, e, f, g, n and o are conclusive without it, and so is the staged/unstaged comparison h-l.
  - They suffice only for their own branches.
- **`d-readonly=0` with a `d-record rc` other than 3 has no reading.**
  - It is a gate.sh defect when `cat` still prints `old`: the open failed and gate.sh handled it wrongly.
  - It is a setup failure when the record was written: the filesystem allowed the open.
  - Case m has the same split.
- **An inconclusive d runs a full gate in the scratch directory.** The script goes on, and gate.sh runs `docker compose ps`, `node --test` and the three dotnet steps there. This is harmless, but unplanned.
- **Case a runs whatever `parent-is-worktree` prints (plan:1137-1138).**
  - If the parent is a work tree, gate.sh runs the full gate in that unrelated repository and writes `TestResults/<branch>/gates.txt` there.
  - That breaks "It touches only a scratch repository" (plan:1126).
  - `p-notree` (plan:1179, :1204) has the same dependency, with no inconclusive note.

**B2 — Medium — Specified exit branches that no named check reaches.**
Where: plan:810-831 and plan:834-846, against the scratch script at plan:1151-1179.
- **mutate-run.sh.** Only two calls reach it: the zero-argument usage call (plan:1152, :1275) and P3.4's success path.
- **Never reached in mutate-run.sh:**
  - the label and mode checks, and the argument count per mode (plan:811-812)
  - the untracked-target, branch, `mkdir` and not-a-work-tree start-up failures (plan:813-817)
  - the log I/O exit 3 (plan:818, :823)
- **Why the mutate-clean.sh cases do not cover it.** The two scripts are separate files, and Files lists no shared library (plan:518-520).
- **Neither executed nor listed in residual item (10):**
  - the `cd` failure of all three scripts (plan:599, plan:814, DV3 at plan:461)
  - mutate-clean.sh's exit 2 on "a git call exiting with another code" (plan:845)

**B3 — Low-Medium — The PR-end gate copy can pass on the tester's record (plan:1489).**
- **The check.** Step 5 copies `gates.txt` after `/gate`, then checks only G1 `head=H_R`, G3 and G11.
- **How it passes on the wrong record.**
  - A `/gate` that exits 2 writes nothing (plan:597, :651).
  - The step-4 tester's record at H_R stays in place, and G0-G11 pass on it.
- **What P3.1 does.** It ties its copy to the tool result's hash (plan:1085). Step 5 does not.
- **Why /pr does not catch it.**
  - `/pr` precondition 3 reads only the record (plan:948-952).
  - An exit 3 after a verified record also ends in `gate-exit=0 failed=` (plan:654).
  - So only the human's step-5 check can catch either case.

**B4 — Low — P3.4 can pass on an earlier attempt's records (plan:818, plan:1215, plan:1221-1227).**
- The mutation log is opened in append mode.
- So "M1.1.txt holds, in order ..." can be met by records left by a previous or aborted P3.4 run, or by an infrastructure rerun.
- No precondition requires a new log.

**B5 — Low — D1 conflicts with the tester's own rule (plan:359, :373 and :1526, against the tester.md text at plan:682-683).**
- **What D1 requires.** D1 is marked "both", so the Phase 4 and PR-end testers must run it "exactly as the plan writes it" (plan:706-707).
- **The conflict.** D1 is `wc -l < CLAUDE.md`, an input redirection. The new tester.md forbids any "redirection other than the token `2>&1`".
- **The hook would allow it.** `bash-allowlist.js:81-93` blocks `<(` and `>`, but not `<`.
- **Result.** The tester must either break its rule or change the command. `wc -l CLAUDE.md` prints `199 CLAUDE.md`, which fails D1's expected `199`.

**L1 — Low — `Gates-sha256:` is undefined when gate.sh printed no hash (tester.md text at plan:719-724, with plan:698-701).**
- There is no hash in three cases:
  - Exit 2 prints nothing to stdout.
  - Exit 3 can stop before line 2.
  - `sha256=unavailable` is not a hash.
- Step 4 requires a NEEDS_FIXES file in exactly these cases, and then "Its first lines are exact" cannot be met.

**L2 — Low — The stderr rule of gate.sh's start-up is ambiguous (plan:597-601 against plan:1188).**
- Outside a work tree, `git rev-parse --show-toplevel` prints its own `fatal:` line on stderr. `a-notree` expects "one line".
- "It prints one reason line" does not say whether the git call's stderr is discarded. The coder must guess, and P3.7 can fail on that choice.

**L3 — Low — The residual-risks heading is not pinned (plan:1399 against plan:1348 and plan:377).**
- The exact roadmap row links `ai-workflow.md#residual-risks`.
- Section 9's heading text is not fixed, unlike `## Roles and tiers` (plan:1359), and D5 does not check anchors.

### Non-blocking observations

- **N1 — `/pr` cannot be re-run once `gh pr create` has succeeded (plan:1015-1022, :1552-1554).**
  - After a CI failure, or a stop at step 7, the next `/pr` run fails at step 5 because the PR already exists.
  - This is inherited from src:22975.
  - Consider "if an open PR exists for this branch, reuse it (`gh pr view`)".
- **N2 — A reverted journal entry's number looks free again (plan:1542).** Two rules derive N only from the files present:
  - the explainer's rule (plan:1422-1425)
  - journal step 1 (plan:1462)

  The number would therefore look free again, although plan:1542 says it stays consumed.
- **N3 — The cited hooks range overshoots (plan:1372).** `:7-16` overshoots, because the hooks block ends at line 12 for six agents (for example `code-reviewer.md:7-12`).
- **N4 — The timeout wording differs (plan:856).** It says "timeout 600000 in `dotnet` mode", while `/mutate` sets the timeout in both modes (plan:775-776). Harmless.

## Required changes

1. **P3.7 and P3.8: outcome rules and deterministic cases (B1).**
   - Add "P3.7 passes when" and "P3.8 passes when" lists: every listed line as the table states.
   - **Inconclusive cases.**
     - A case is INCONCLUSIVE when its precondition line is off: `parent-is-worktree=0`, or `d-readonly` or `m-readonly` not 0.
     - With `readonly=0`, an rc other than 3 is a defect when the record or log is untouched, and INCONCLUSIVE when it was written.
     - An INCONCLUSIVE case is never a pass.
     - State what follows: the deterministic case below, or a recorded human decision with the path added to residual (10).
   - **Deterministic open-failure cases.**
     - Record: put a directory at the record path (`mkdir -p TestResults/scratch/gates.txt`). Expect rc 3, an `I/O error` line, no `gate-exit=` line, and empty `ls -A TestResults/scratch/gates.txt`.
     - Log: put a directory at the log path (`mkdir -p TestResults/scratch/mutations/chk.txt`). Expect rc 3 from both mutation scripts.
     - Keep the chmod cases as supplementary.
     - That a directory makes the open fail is UNVERIFIED. The case's own `rc=` line settles it.
   - **Cases a and p.** Run them only when `parent-is-worktree` is non-zero. Otherwise print `a-skipped` and `p-skipped`, and record both as inconclusive.
   - **Documents.** Align plan:1350 and residual item (10) (plan:1348, :1401) with what actually executes. Give the text to use if any case ends inconclusive.
2. **Reach or list every specified exit branch (B2).**
   - Extend the P3.8 script with mutate-run.sh calls, in node mode, so that no failing call reaches `node --test`. Expect 2 for each, with "nothing written" checks:
     - an invalid label
     - an invalid mode
     - a wrong argument count for a valid mode
     - an untracked target
     - a detached HEAD
     - `TestResults` as a file
     - a call outside a work tree, guarded as in change 1
   - Add the unopenable-log case (as in change 1), expecting 3.
   - Name in residual item (10) every specified branch that stays unreached:
     - the `cd` failures of all three scripts
     - mutate-clean.sh's "git call exiting with another code"
3. **Tie the `pr-end-gate` copy to the `/gate` run (B3).**
   - In P4.4 step 5, record the `/gate` tool_use and tool_result rows, as at plan:1065-1067.
   - Require all of the following:
     - line 1 of the result is the `gates.txt` path
     - the hash on line 2 equals G0 of the `pr-end-gate` copy
     - the last line is `gate-exit=0 failed=`
     - that copy's sha256 differs from the `pr-end-tester` copy's, or its header time is later
4. **P3.4 uses only its own records (B4).**
   - Precondition: `test ! -e TestResults/chore-agents-workflow-skills/mutations/M1.1.txt`, recorded with `run`.
   - A repeat run takes a new label, for example `M1.1-r2`.
   - Record the log's sha256 and line count after step 4.
5. **D1 (B5).** Do one of the following:
   - Use `wc -l CLAUDE.md`, expecting `199 CLAUDE.md` (C4.1's own form, src:599), in commands-4.txt and in D1's row.
   - Or mark D1 human-only and remove it from plan:1526.
6. **`Gates-sha256:` (L1).** Define its value when gate.sh exits 2 or 3, or prints `sha256=unavailable`. For example: `Gates-sha256: none`.
7. **Start-up stderr (L2).** State whether the stderr of gate.sh's start-up `git` and `mkdir` calls is discarded. Align `a-notree`'s expectation with that choice.
8. **The residual-risks heading (L3).** Pin section 9's heading as exactly `## Residual risks`.
