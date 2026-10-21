Verdict: NEEDS_REVISION
Reviewer: code-reviewer
Scope: thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md phase 4
Range: 2e8dc16a115840562c215308a814b7f11f38e0de..a4a1eb0904bb749ede6d41eca6dead20a14f36a0
Head: a4a1eb0904bb749ede6d41eca6dead20a14f36a0

## Findings

### Method

- **Read in full:** the plan's Phase 4 section (plan:1583-1851), Appendix A (plan:1885-2091), Doc checks D1-D10 (plan:442-475), the Probe protocol and Bootstrap order (plan:189-318), and the As built block (plan:2949-4288). Also every changed file at Head: `CLAUDE.md`, `docs/roadmap.md`, `docs/ai-workflow.md`, `.claude/agents/explainer.md`.
- **Source records read for the doc's citations:**
  - the source spec's Phase 4 (`2026-09-25_chore-agents-hardening.md:23040-23203`) and its cited lines
  - research F1-F21
  - the Phase 1, phase2-r2, phase2-r3, db-review and Phase 3 review files
  - the hooks, skills, `settings.json` and all eight agent files
- **Ran:** nothing. I have no shell. Every comparison below is my own reading with Read and Grep, marked **[S]**.
- **Pre-change reference.** For `docs/roadmap.md` and `explainer.md` I compared against `C:\projects\envanex-fix-cryptography`. Its reflog (`.git/worktrees/envanex-fix-cryptography/logs/HEAD:1`) starts at 2e8497a. That its two files are still 2e8497a's is UNVERIFIED. Settle with `git diff 2e8dc16 a4a1eb0 -- docs/roadmap.md .claude/agents/explainer.md`.
- **Record classes, kept apart below:**
  - **[MR] model report:** the S3 coder handback (plan:4049-4134) and the P3.6 tester handbacks (plan:3534-3781)
  - **[H] human observation or decision:** the lines so marked
  - **[M] mechanical record:** `$ command`, its output, `exit=`

### Blocking findings

**R1 — Low — `docs/ai-workflow.md:595`: item (13) calls 5d a tester target. The source records it as the explainer's.**
- **The doc.** "tester allowlist targets 5a, 5b, 5f and 5d".
- **The source.** The hardening plan lists the P2.5 targets as tester `…LIVEPROBE-5a`, `…5b`, `…5f` and explainer `dotnet build -p:Probe=LIVEPROBE-5d` (`2026-09-25_chore-agents-hardening.md:3430`). Line :365 counts them as "4 allowlist (5a, 5b, 5f, 5d)". Line :366 compares 5d's target with `dotnet list package`, which is an explainer-only entry (`bash-allowlist.js:56`). The source spec says "role-conflicting tester/explainer commands" (:23107).
- **Why it matters.** As written, the residual record hides that the explainer profile also has a negative path that is UNVERIFIED end to end.

**R2 — Low — `docs/ai-workflow.md:276-277` states as fact what item (22) records as unproven.**
- **The doc.** "Only the human invokes `/mutate`; a coder turn calls the two scripts directly."
- **The record contradicts it.** "P3.4 attempt M1.1 assessment" (plan:2522-2530) records "a model-made Skill invocation" of `/mutate`, which succeeded with `disable-model-invocation: true` set (`.claude/skills/mutate/SKILL.md:4`).
- **The doc's own item (22)** (`docs/ai-workflow.md:709-734`) treats the flag as an unproven enforcement boundary. Section 4 states the property that boundary would provide, unqualified and without a cross-reference. That presents O1 as closed, which the plan's Revision 4 and the carried-forward record exclude.
- **What is fine.** The second clause matches `coder.md` and `mutate/SKILL.md:48`.

### Checked and conforming

1. **`CLAUDE.md` = Appendix A (DV10) [S].**
   - Lines 1-199 equal plan:1891-2089 line for line.
   - Neither side has trailing whitespace (Grep `[ \t]+$`: no hit in CLAUDE.md, and none within plan:1890-2090). CLAUDE.md has no CR byte.
   - The APPENDIX-A markers stay unique as whole lines (plan:1889, :2091). plan:4092 holds the D2 command only after a `$ ` prefix, so D2's awk is unaffected by the As built block.
   - D1 = `199 CLAUDE.md` [M] (plan:4143-4145). D2 itself exists only as [MR] (plan:4092-4093). The [M] run of D2 is pending in `commands-4.txt`.
2. **`docs/roadmap.md`: only the scoped edits [S].** Against the reference copy, lines 1-57, 60-196, the two kept rows (now :197-198), :202, :219-224 and :230-243 are unchanged. The changes are:
   - :58 `done (#14)`
   - :59 per decision 12
   - old rows 197-198 deleted (no "enforced only by prompts" or "not persisted" left)
   - :199 CI row and :200 residual row, appended after the rate-limit row
   - :204, the diagram :206-212, the paragraph :214-217, the `/gate` sentence :227-229, and :245

   Grep whole-line regex checks match exactly once in both the roadmap and the plan:
   - the full residual row, items (1)-(22) and the `| not scheduled — recorded |` cell, against plan:1640
   - the CI row, against plan:1622
   - row 59, against plan:1616

   No `/verify` is left in the roadmap.
3. **`docs/ai-workflow.md` [S].**
   - **Structure.** Sections 1-10 are in plan order. `## Roles and tiers` (:80) and `## Residual risks` (:473) are exact. The one `/verify` line is :445, which also matches D3 [M] (plan:4147-4149).
   - **Roles table** (:82-91) matches every agent's frontmatter: tools at `:4`, model and effort at `:5-6`, hooks at `:7-12`/`:7-16`. The table also agrees with D6 [M] (plan:4164-4181).
   - **The tools and hooks columns** are the plan's "Each row also gives the tools and hooks" (plan:1667), stated accurately. No agent sets `memory` or `maxTurns`.
   - **Code-backed claims verified against the code:**
     - allowlist mechanics: `bash-allowlist.js:11-203`
     - coder git block: `coder-bash-guard.js:9-28`, :105-111
     - path guard: `path-guard.js:7-18`
     - write scopes: `write-scope.js:15-24`
     - redaction rules 0-9, `SENSITIVE`, `VALUE` and the log fields: `guard-common.js:125-237`
     - gate record format and exits: `gate.sh:11-242`
     - mutation scripts: `mutate-clean.sh:51-59`, :122
     - `/pr` preconditions 3-7, Head rule R, the security-review header and the merge rules: `pr/SKILL.md:22-118`
     - tester steps 3, 4 and 11: `tester.md:39-81`
     - deny/allow counts 47×2 and 51: `settings.json:4-150`
   - **Research and record citations verified:** F3, F4, F5, F6, F10-F13, F15, F20, F21, the light lane, P1.10 (:892), C2.6 (:22780, :22181), plan:3389, H1.1 (:640, :1239, :1670) and Q1 (:453).
   - **Residual items (1)-(22).** They follow the roadmap's order and names, and each ends with Settling and Reopens lines; for (22), the settling check sits in a bullet. The coder-authored "none" and "not needed" entries give reasons and triggers that do not contradict the source. Item-by-item:
     - (3) cites tests that exist (`coder-bash-guard.test.js:216`, :220, :224; the allowed cases at :288-297).
     - (4) names a test that exists (`bash-allowlist.test.js:262`).
     - (10) matches the plan's Coverage list (plan:1476-1485), the human's P3.7/P3.8 assessment (plan:2930-2938: no skips, so "accepted: none") and the Phase 3 review's "Residual cases" (review :132-147).
     - (16)/(17) match `…hardening.md:2766`, :2772-2774.
     - (21) matches F2/F3 (review :92-105), N1-N4 (phase2-r2 :151-184), N3 (:22799) and DBR-1/DBR-2 (db-review :99-109).
     - Additions: (1) cites C2.6 correctly. (14)'s guard and allowlist statement holds for the listed forms (`GIT_WORD` covers `git.exe`, :9). (16)'s `..` rejection is `bash-allowlist.js:106`.
   - **O2 and O3 are absent** (Grep), consistent with Revision 4 (plan:38-42).
   - **(22)** is open throughout. Its observed facts match plan:2517-2532 and review O1 (:171-176). It carries "not established", "potential implication only", "unproven boundary", and a settling check that is UNVERIFIED and not scheduled. Nothing in the file marks a Phase 4 check, the phase, the PR or the live `/pr` path as done (Grep).
4. **`.claude/agents/explainer.md`: exactly three edits [S]:** :22, :36-39 (= plan:1727-1730) and :44 `# NNNN — <title>`. :3 and the rest are unchanged against the reference copy. By my reading, D10's command matches only :36, :37 and :44. The trailing spaces at :58-59 predate this phase (reference :56-57).
5. **As built block (plan:2949-4288).**
   - **Layout** follows Probe protocol 2.7.
   - **The transfer log** `TestResults/chore-agents-workflow-skills/validation/as-built-4-77f07f8-20261019T221903.log` [M] settles the copy and append-only checks:
     - `cmp=0` against `phase-4.md` (:6-8)
     - HEAD's plan equals the first 2947 lines (:10-11)
     - numstat `1341 0` and no removed line (:36-42)
     - no CR and a final LF (:31-34)

     It lies outside the range, and its sha256 is not recorded yet. Under Probe protocol item 3 it belongs in the final evidence commit's late records.
   - **Independently [S]:** `phase-4.md` has 1335 lines, and every heading, Human line, FAIL line and `handback-match`/`exit=1` line maps to plan line = evidence line + 2951.
   - **Content present:**
     - Phase 3's late records: P3.5/C3.2 [M] with [H] at :3128-3133; P3.6 [M]; the tester supplements [MR] printed by a checker [M]; the [H] assessment at :3862-3863
     - H4.0/H0.2 [H]/[M] at :3868-3907
     - R4 and H4.1 [H] at :3909-3913
     - the S3 handback [MR], verbatim inside the r1 checker output [M], with `FAIL …` and `handback-match=1`, `exit=1` kept (:4047, :4136-4137)
     - the dated r2 correction [M], whose structural check gives `handback-match=0` (:4183-4271)
     - C4.1/D1, D3-D5 and D6 [M] at :4139-4181
     - the [H] assessment at :4273-4280 and the cleanup [M] at :4282-4286
   - **The S3 handback's own outputs** are model reports only: build, D1-D4, D10, and the D7, BOM and D5 informal checks. Of these, D1, D3, D4 and D5 have [M] records. Build, D2 and D10 do not. D7-D9 await `commands-4.txt`. My static reading agrees with D2, D7 (no CR in the four files), D9 (10 entries) and D10.

### Non-blocking findings

- **N1 — `docs/ai-workflow.md:616-617`.** "coder-bash-guard blocks the coder regardless" holds for (14)'s forms as written. However, F3's quoted form `git branch -v ";" -D x` passes the guard (`coder-bash-guard.js:31`, :57-62; review :99-105; runtime UNVERIFIED). A pointer to item (21)'s F3 would keep (14) from reading as unconditional.
- **N2 — `docs/ai-workflow.md:260-262`.** "with no arguments, pipe or redirection; it is an exact entry".
  - The exact entry rejects only further tokens, `2>&1` included (`bash-allowlist.js:148-149`).
  - A pipe into grep/head/tail/wc after it passes the hook (:177, :199-203). "No pipe" is tester.md:24's rule, not the hook's.
  - No false pass follows: truncated stdout fails tester.md step 3.
- **N3 — `docs/ai-workflow.md:640-641`.**
  - "A deny entry covers the letter case it is written in" generalizes from one recorded pair. The record says "for `git switch -C*` against `git switch -c`" (`…hardening.md:2774`).
  - "(exit 129)" comes from :2766, not the cited :2772-2774.
- **N4 — `docs/ai-workflow.md:529-534`.**
  - (7) says "Settling: not needed; the behavior is the code's". No test pins the quoted-value cut: `guard-common.test.js:214-215` covers only an unquoted `&&`. By the doc's own definition (:478-479), no record settles the claim.
  - My static trace of rule 3 agrees with the claim. A `node -e` settling command, like (19)'s, would close the gap. Example: `redactSecrets('dotnet user-secrets set k "a;b"')`. The expected `dotnet user-secrets set ***;b"` is UNVERIFIED.
- **N5 — `docs/ai-workflow.md:558-564`.** (10)'s "What only review covers" reads as exhaustive. The Phase 3 review's O4 static-only paths are in no residual item:
  - mutate-run.sh's dotnet mode
  - mutate-clean.sh with several files
  - gate.sh's `missing:` and `sha256=unavailable` branches

  Review :187-192 lists them, and the As built section D (plan:3851) carries them. The plan prescribes (10)'s list and Revision 4 adds only O1, so adding them is a planner/human decision, not a coder defect.
- **N6 — `docs/ai-workflow.md:96`, :292.** "Every hook command ends in `|| exit 2`" holds for every PreToolUse guard (`settings.json:160` and the agent files). It does not hold for the PostToolUse format hook (`settings.json:171`). This is precision only.
- **N7 — As built, record form [M]/[H].**
  - The R4 record says "Exact launch time was not recorded" (plan:3912). Probe protocol 2.4 (plan:241) asks for the time. The deviation is disclosed.
  - The records at plan:3871-3895 and :3915-3932 put a blank line before `exit=`. The plan's `run` (plan:482-490) does not write one for these outputs, yet items 2.4 and 12 ask for `run`. Content is unaffected. Naming the recorder in the next late records would close this.
- **N8 — the Phase 4 code commit body, UNVERIFIED.** Plan:1620 requires it to cite "#15" and "this follow-up" and no unassigned PR number. The reflog subject is `docs(agents): implement Phase 4 workflow documentation` (`.git/logs/HEAD:209`). The body is in no reviewed file. Settle with `git log -1 --format=%B 77f07f8`.

### Carried forward, not re-decided

- O1 stays open: `disable-model-invocation` is not a proven enforcement boundary.
- G10's pattern collision stays accepted.
- P3.4 attempt M1.1 is not a pass. M1.1-r2 is accepted with procedural deviations.
- The residual code-only branches (plan:3846-3852) were never executed.
- The live `/pr` path is first used at P4.4.
- No unexecuted case is treated as verified here.

## Required changes

Both changes touch only `docs/ai-workflow.md`, which is not in the restart list (Probe protocol item 4, plan:268). The checks that read it are D3, D5, D7 and D8.

1. **`docs/ai-workflow.md:595`.** Replace "tester allowlist targets 5a, 5b, 5f and 5d" with "the allowlist targets 5a, 5b and 5f (tester) and 5d (explainer)". Source: `2026-09-25_chore-agents-hardening.md:365`, :3430.
2. **`docs/ai-workflow.md:276-277`.** Do not state human-only invocation as fact. For example: "`/mutate` is meant to be invoked only by the human (`disable-model-invocation: true`); whether the runtime enforces that is unproven (Residual risks, item 22). A coder turn never invokes `/mutate`; it calls the two scripts directly." Keep item (22) unchanged.

NEEDS_REVISION
