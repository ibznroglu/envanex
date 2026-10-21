Verdict: NEEDS_REVISION
Reviewer: plan-reviewer
Scope: focused review of the O1 documentation amendment (thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md, Revision 4)
Range: 8148d322055915ca2908f25774ca83eb715c66f0..558fb026b7b2e45d98777b56e966cebdec0eae3c
Head: 558fb026b7b2e45d98777b56e966cebdec0eae3c

## Findings

### Method

- **Subject.** Revision 4 at 558fb02: the four hunks the caller relayed, at plan:38-43, :1640, :1695 and :1715-1720.
- **Read:**
  - those lines and their surroundings, and the sections they bear on: plan:1-60, :405-525, :530-600, :984-997, :1218-1227, :1484-1507, :1560-1888, :1976-1999, :2086-2097, :2484-2540 and :2925-2947
  - the Phase 3 code review, `thoughts/shared/reviews/2026-10-19_chore-agents-workflow-skills-code-review-phase3.md` (cited as `review-p3:N`; O1 is at :171-176), and the r3 plan review
  - the frontmatter of `.claude/skills/gate/SKILL.md`, `mutate/SKILL.md`, `commit/SKILL.md` and `pr/SKILL.md`; `.claude/agents/planner.md:4`; `.claude/agents/coder.md:36`
  - the source residual list, `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md:23088-23137` (cited as `hardening.md:N`)
- **Ran:** nothing. I have no shell.
  - The hunk list, the `cmp` results and the hashes come from the main session, relayed; I did not recompute them, so they are UNVERIFIED by me.
  - To settle them: `git diff -U0 8148d32 558fb02 -- thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md`, plus the marker-based `cmp` the caller describes.
- **Notation.** `plan:N` is line N of the plan at 558fb02. `file:N` is a line of the working tree, which is clean at 558fb02.

### Blocking findings

**F1 — Only one of the two runtime readings is marked as not established (plan:1640, plan:1717).**
- **Text.** Both the roadmap item and section 9's bullet say that M1.1 does not establish "autonomous invocation without a human request, a runtime bug, or the same behavior for `/commit` and `/pr`".
- **The record is symmetric.**
  - plan:2524-2525: "Whether the runtime should have refused that call is not established."
  - O1 likewise leaves the runtime's behavior UNVERIFIED (review-p3:175).
- **Why it matters.**
  - Ruling out only the bug reading leaves the opposite reading standing as the default: that a model-made call after a human request naming the skill is intended runtime behavior, and the flag works as designed. That reading is equally unestablished.
  - The roadmap's "succeeded although ... sets the flag" leans toward the bug reading, so the record pulls in two directions.
  - Scope item 2 requires both readings to be marked as not established.
- **Not relied on.** I did not check what Claude Code documents for the flag, because I have no access to its docs (UNVERIFIED). The fix does not depend on it.

**F2 — Section 9's settling check is weaker than the roadmap row (plan:1720 against plan:1640).**
- **The two texts.**
  - The roadmap says the verification "uses only a harmless test skill".
  - Section 9 says "it uses a harmless test skill, never a real push or PR".
- **The gap.**
  - Without "only", section 9 does not exclude a real `/commit` as the test subject.
  - "The same behavior for `/commit`" is listed as not established, so `/commit` is the obvious subject for a future verifier.
- **Why it matters.** The detail must not allow what the record forbids (scope items 1 and 4).

**F3 — The doc's transcript citations in (22) cannot be resolved (plan:1715-1716).**
- **What plan:1715 says.** It gives the source and the record as facts about (22) ("Its source is ..., and its record is ..."), then "It states:".
- **What the five bullets omit.**
  - They do not include the source or the record.
  - Compare (21), which is told to list "every open finding and its source file" (plan:1703).
- **What bullet 1 cites.**
  - "main-session transcript row 498" and rows 508-510, without naming a transcript.
  - The transcript is identified only in the As built entry: plan:2520, `901514d2-f7c7-4bb6-b9c7-ee3425bb0618.jsonl`.
- **Why it matters.** The coder must guess whether `docs/ai-workflow.md` cites the source and the record. If it does not, a reader cannot resolve the rows.

### Checked and correct

1. **Revision 4 note, placement and form (scope 1).**
   - **The note (plan:38) is accurate.**
     - review-p3:1 and :5 give `APPROVED` and Head `ec46b2df...`.
     - `.claude/agents/planner.md:4` lists `Write` and no `Edit`, so "the planner's only write tool replaces the whole file" holds.
   - **The finding table (plan:40-42)** has Revision 3's form (plan:32-36), and its row describes the actual placement.
   - **The residual row (plan:1640).**
     - It is still one line, inside its double-backtick code span.
     - Item (21) now ends "record.", and (22) follows with the row's `. (N) ` separator.
     - (22) contains no `|` and no run of two backticks, so the code span and the table cell stay intact.
     - (22) ends at the cell's ` | `.
   - **The item count (plan:1695)** reads "items (1)-(22), in the same order and with the same names". A grep finds no other "(1)-(21)" and no other count of the items.
   - **The (22) sub-bullet (plan:1715-1720).**
     - Its indentation matches (10) and (21).
     - It sits after "what this PR resolves" (plan:1709-1714). That does not change the document's item order, which plan:1695 fixes.
   - **The two texts agree** on the observation, the `/pr` implication, the merge protections and the verification constraints, apart from F1 (a gap they share) and F2.
2. **Observed versus not established (scope 2).**
   - **The observation matches the record (plan:2519-2525):**
     - the multiline paste
     - row 498 with `queueSkipSlashCommands=true`, and no `<command-name>/mutate` row
     - row 508, the Skill tool_use `toolu_017hKZ6yxZawB5ZfHKFL19V6`
     - row 509, "Launching skill: mutate"
     - row 510, the skill body
   - `.claude/skills/mutate/SKILL.md:4` sets the flag.
   - "then" orders the model-made call after the human request.
   - **Stated as not established:** autonomous invocation without a human request, and the same behavior for `/commit` and `/pr`.
   - The remaining gap is F1.
3. **`/pr` (scope 3).**
   - **The citations at plan:1718 are correct:**
     - `.claude/skills/commit/SKILL.md:4` and `.claude/skills/pr/SKILL.md:4` set the flag
     - `.claude/skills/pr/SKILL.md:6` contains `Bash(git push *)` and `Bash(gh pr *)`
   - **Both texts** call it "a potential implication only, not evidence of any automatic push or PR", and state that `/pr`'s existing merge protections are unchanged.
   - **The citations stay valid** when the coder writes the doc, because Phase 4 changes no skill file (plan:1735-1742).
4. **Verification (scope 4).**
   - Both texts say the verification is UNVERIFIED and unscheduled, uses a harmless test skill, and distinguishes a human request that names the skill from one that does not.
   - Section 9 adds "never a real push or PR".
   - The remaining gap is F2.
5. **Cross-references (scope 5).**
   - **D1-D10 (plan:442-472)** neither count the residual items nor read the row.
     - (22) adds no markdown link (D5) and no `/verify` (D3).
     - It touches no line of CLAUDE.md (D1, D2, D4).
   - **Not stale:** Phase 4 Validation (plan:1836-1850), Tests to add (plan:1750), DV11 (plan:583-587), the goals (plan:166, :177) and the PR body (plan:1827-1832).
   - **Nothing contradicts (22).**
     - No named check claims the model cannot invoke a skill (C3.1-C3.3: plan:608-609, :1225-1227).
     - X10 (plan:430-440) checks only that the flag is present.
     - Appendix A's `/commit` and `/pr` bullets (plan:2029-2030) make no manual-only claim.
   - **The Revision 4 note's line-number statement is accurate.** Checked examples:
     - the As built entry's "plan:1483" (plan:2529) is now plan:1489, "The human runs `/mutate`"
     - its "plan:1494-1498" (plan:2532) is now plan:1500-1504
     - review-p3:176's "plan:1634" is now plan:1640
     - review-p3:12's As built range, plan:2081-2935, is now plan:2093-2947
   - **The mapping**, from pre-Revision-4 line N:
     - lines up to 37 are unchanged
     - lines 38-1708 move by +6
     - lines from 1709 on move by +12
6. **Preservation (scope 6).**
   - **The markers are where the caller says:**
     - `<!-- APPENDIX-A-START -->` at plan:1889 and `<!-- APPENDIX-A-END -->` at plan:2091
     - `## As built — Phase 3` at plan:2093
     - the evidence markers at plan:2095 and plan:2947, the last line
   - 2947 = 2935 + 12, which matches the hunks' net growth. Every hunk lies before plan:1889.
   - Byte identity rests on the caller's `cmp` and sha256 results, which I did not recompute (UNVERIFIED by me).

### Non-blocking observations

- **N1 — `/gate` also sets the flag** (`.claude/skills/gate/SKILL.md:4`; O1 lists it at review-p3:173).
  - (22) names only `/mutate`, `/commit` and `/pr`.
  - A model-made `/gate` call would rewrite the live record. P4.4 step 5's rules already cover provenance: the record is absent before the call and the hash must match (plan:1798-1809).
  - Naming `/gate` in (22) is optional.
- **N2 — (22) has no reopen trigger** (plan:1716-1720), although the row's opener promises one for each item (plan:1640).
  - Several source items lack one too (hardening.md:23097-23104, against :23105-23107), so this is not blocking.
  - A possible trigger: a model-made call to a flagged skill is observed again, notably `/commit` or `/pr`; or a flagged skill gains a side-effecting allowed tool.
- **N3 — Notation.**
  - In the As built block, "plan:N" means this plan's lines (plan:2529 cites P3.4). That differs from plan:46's definition, which points to the source plan.
  - The Revision 4 note's wording, "Line numbers of this plan", covers this. Saying it explicitly, with the mapping above, would prevent a misreading.
  - Phase 4's late records, appended to the same section, will use Revision 4 numbering. A note saying so when they are appended would keep the two numberings apart.
- **N4 — `.claude/agents/coder.md:36` says "only the human can" invoke `/mutate`.** After (22), that statement holds as a rule, not as a runtime guarantee. Phase 3 is closed, and this amendment requires no change.
- **N5 — P4.4 step 7 does not record who invoked `/pr`** (plan:1811-1820).
  - C3.2 is an effort check (plan:1225-1227), not a provenance check.
  - M1.1-r2 checked for the `<command-name>/mutate` slash-command row (plan:2610-2611). The same record for `/pr` would document the human's invocation.
  - This lies outside the amendment and is a suggestion only.
- **N6 — Wording at plan:1640 and plan:1720.**
  - In "any verification is UNVERIFIED and unscheduled", what is UNVERIFIED is the runtime behavior, not a verification.
  - plan:1720's settling question could add two facts:
    - the case where the human request names the skill was observed once (M1.1)
    - O1's open case is a request that does not name it (review-p3:175)

## Required changes

1. **F1 (plan:1640 and plan:1717).** Mark both runtime readings as not established, in line with plan:2524-2525.
   - For example, replace "a runtime bug," with "whether the runtime should have refused the call (a runtime bug or intended runtime semantics),".
   - The roadmap text must stay free of `|` and of two-backtick runs.
2. **F2 (plan:1720).** Change "it uses a harmless test skill" to "it uses only a harmless test skill", to match plan:1640.
3. **F3 (plan:1715-1720).** Make (22) in `docs/ai-workflow.md` resolvable, in one of two ways:
   - **(a) Cite source and record.** Add a sub-bullet stating that (22) cites its source, O1 of `thoughts/shared/reviews/2026-10-19_chore-agents-workflow-skills-code-review-phase3.md`, and its record, this plan's As built entry "P3.4 attempt M1.1 assessment".
   - **(b) Name the transcript.** Name the main-session transcript `901514d2-f7c7-4bb6-b9c7-ee3425bb0618.jsonl` in the "what was observed" bullet.

   Either option keeps D3 at one line. Written as a markdown link, the source path is checked by D5, and the file exists.
4. **Constraints on the revision.**
   - Update the Revision 4 finding row (plan:42) if the locations change.
   - Change nothing outside plan:38-43, :1640, :1695 and :1715-1720.
   - Appendix A and the complete Phase 3 As built section stay byte-identical, as settled by the same marker-based `cmp`.

NEEDS_REVISION
