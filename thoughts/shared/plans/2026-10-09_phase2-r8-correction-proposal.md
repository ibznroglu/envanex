# Plan: Phase 2 — correct Revision 8 so the remaining verification can run (PROPOSED)

Status: PROPOSED. This is a planning document, not an authorization. Nothing in it is approved or applied, and it closes no probe. Every recommendation is marked PROPOSED and waits for the human's decision.

Date: 2026-10-09. Branch: `chore/agents-hardening`. HEAD `561624241a31754d3c028e32b4c32d1a5ab83331` and the clean tree are the human's supplied observations. The planner ran no command.

Inputs:
- `thoughts/shared/research/2026-10-09_phase2-r8-coverage.md`: a model report, verified below.
- `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` at Revision 8. Below it is "the plan", cited as plan:<line>.
- `.claude/agents/*.md`, `CLAUDE.md`, `.claude/hooks/*.js`, `.claude/hooks/tests/*.js`, `docs/journal/` and `docs/roadmap.md`.
- `TestResults/chore-agents-hardening/evidence/phase-2.md`, read through line 14650 and cited as ev:<line>.

Statements about hook decisions come from reading the source. They are not executed results.

## Goal

Make Revision 8's remaining Phase 2 verification executable with the smallest coherent change. Nothing below reverses Revision 8's separations: decision tests stay apart from live connections, uncalled negatives stay unverified, and an allow record is still no negative proof.

What this proposal adds:
- **A non-circular order** for the five connections still missing:
  - planner, plan-reviewer, db-reviewer, tester and explainer;
  - the preliminary and the final review and validation;
  - report retention and commits.
- **Rules for the fixed output paths.** No tester run and no explainer run writes over an existing report.
- **A coverage ledger** that keeps two kinds of coverage apart:
  - byte-identical target coverage, which is impossible under C1.7;
  - behaviour coverage, from named tests.
  
  It uses the corrected count: 17 original negative targets, of which one (2e) has a recorded live block and 16 do not.
- **Four write-scope block tests** for the four scope arguments that have none. They are "review only" under the unchanged Behaviour mapping.
- **One human-run unmutated decision-test record** as the source of the ledger's results.

Cost:
- Seven plan-text edits and four tests.
- No change to hook code, settings, agent definitions or CLAUDE.md.
- No new mutation proof and no new probe type.

## Non-goals

- **No authority change.** No change to:
  - `.claude/hooks/*.js` and `.claude/hooks/lib/`;
  - `.claude/settings.json` and `.mcp.json`;
  - any `.claude/agents/*.md` (frontmatter or body), or any model or effort value;
  - `CLAUDE.md`.
- **No new mutation proof.** The Behaviour mapping is unchanged (plan:3172-3186). The new tests fall under its last row, "every other item in this section → review only" (plan:3184).
- **No new live negative probe.** No prompt asks an agent to act against its role. The 16 uncalled negatives stay inconclusive and UNVERIFIED end to end (plan:344).
- **No `LIVEPROBE` string in any test file** (C1.7, plan:460; Validation command 3, plan:3363).
- **No out-of-call execution of the exact `LIVEPROBE` strings.** Running them through the hook scripts outside a live call would be a new method, which needs its own plan decision (plan:3730). A wrong `ENVANEX_HOOK_LOG_DIR` would also put non-agent `LIVEPROBE` lines into the project log, and C1.7's inference (plan:444) depends on there being none.
- **No edit to existing records.** No existing evidence record, As built section or the research report is edited, and no past record is reclassified (plan:442).
- **No automatic transitions.** No automatic READY_TO_PUSH and no automatic pipeline transition. A tester verdict is never the human's decision.
- **No change to Phases 3 and 4.**

## Touches schema? (yes/no — db-reviewer required if yes)

No. The data model does not require db-reviewer. Phase 3 of this proposal asks the human to authorize a genuine db-review of Phase 2. Its only purpose is to be the db-reviewer's role-compatible task for C2.2 (decision D2). That run reviews no schema change and needs no db-reviewer gate.

## ADR needed? (title, or "no")

No. This is verification procedure, recorded in the plan.

## Verification of the research report

### Confirmed against the sources

1. **No test can carry an original target byte for byte.** Every target contains `LIVEPROBE-<id>`, and no test file may contain `LIVEPROBE` (plan:460, plan:3363).
2. **Harness versus live hook.**
   - `run-hook.js:10-15` gives every test run a temporary `ENVANEX_HOOK_LOG_DIR`. The tests that disable the override use temporary project directories (`guard-common.test.js:355-404`, `path-guard.test.js:240-256`). No test writes to the project log.
   - The live log shows `project_dir_raw` as `C:/projects/envanex` (ev:14153, ev:14287). The tests use `C:\projects\envanex`.
   - That difference is unit-tested by `normalizeProjectDir gives the same result for C:\, C:/ and /c/ forms` (`guard-common.test.js:112`). This settles the research's project-dir risk.
3. **No unmutated `coder-bash-guard.test.js` run exists in phase-2.md.**
   - The only runs are M2.1 call 5 (ev:2794-2795) and M2.2 call 4 (ev:3112-3113), both made with the mutation applied.
   - Each coder's later Bash call is `git diff --exit-code` only (ev:3003-3004, ev:3297-3298).
   - The R7 runner copied `coder-bash-guard.test.js` (ev:5888) but ran only `write-scope.test.js` and `bash-allowlist.test.js` (for example ev:5904, ev:6010, ev:6136).
   - The code-commit check ran no test (ev:2555-2607).
4. **The assertion helpers are at the cited lines:** `coder-bash-guard.test.js:44-48`, `write-scope.test.js:45-49`, `bash-allowlist.test.js:46-50`.
5. **The static readings for all 17 targets match the source:**
   - `coder-bash-guard.js:76-134`;
   - `write-scope.js:10-26`, which is generic over the scope argument;
   - `bash-allowlist.js:51-58` (explainer profile), `:62-95` (pre-check), `:113-144` (dotnet tokens) and `:161-204` (decide).
6. **Only 2e has a live block** (ev:14113-14119, ev:14153).

### Corrected or brought up to date

1. **Count.**
   - The research says 15 of 16 targets lack a live block (research:129). The correct numbers: 17 negatives (5 coder, 8 write-scope, 4 allowlist); 1 with a live block (2e); 16 without.
   - The human has already recorded this correction (ev:14600).
2. **Stale cut-off.** The research stops at ev:14567, but:
   - ev:14568-14596 record the researcher's Write connection. The Write is `toolu_01Ufgp73r6B4bnVrjykr4i6t`, logged as a `write-scope:thoughts/shared/research` allow at 2026-10-08T23:12:58.851Z, inside the call/result window.
   - Five profiles still have no connection record, not six: planner, plan-reviewer, db-reviewer, tester and explainer.
3. **The research's OQ1 overstates the db-reviewer limit.**
   - CLAUDE.md:91 and db-reviewer.md:3 say when the step runs.
   - CLAUDE.md:101 makes the step mandatory for schema phases only.
   - No line forbids a review the human requests. See D2.
4. **The research's OQ2 premise does not hold.** NNNN does not need an existing GitHub PR.
   - NNNN is the caller-supplied "PR number" (explainer.md:22, :36-37).
   - The journal numbers 0001-0008 belong to roadmap rows 1-6b, which merged as GitHub #1-#12 (docs/roadmap.md:45-57). Roadmap numbers and GitHub numbers differ (docs/roadmap.md:62-64).
5. **The research's OQ8: existing review files.** I inspected them.
   - No `*_chore-agents-hardening-*` review file exists.
   - This branch's five earlier reviews use the older slug `chore-agents` (2026-09-25 to 2026-09-29).
   - A first `chore-agents-hardening` plan review, Phase 2 code review or Phase 2 db review therefore collides with nothing.
6. **Reuse record.** The research's risk list understates one gap.
   - Plan:444 asks for "the relevant hook and agent-file hashes" with each report-write connection.
   - The researcher record (ev:14576-14587) carries the report hash but no hook or agent-file hash.
   - The R8 commit record (ev:14560-14562) shows `.claude`, `.mcp.json` and `CLAUDE.md` unchanged since ce5e7cf, but no hashes.
   - Order step R9.1 supplies them.

## Decisions for the human (each recommendation PROPOSED, none approved)

### D1 — the four missing scope-argument block tests

**Conflict.** Revision 8 forbids "an assumed equivalence" (plan:3279). It requires missing decision coverage to be "resolved through the existing approved-plan/code/test workflow" (plan:346). Four profiles' write-scope arguments have no block test:
- `thoughts/shared/research` (researcher);
- `thoughts/shared/plans` (planner);
- `TestResults` (tester);
- `docs/journal` (explainer).

All block tests use `thoughts/shared/reviews` (`write-scope.test.js:95-119`). The other scopes have allow tests only (`:123-129`).

**Alternatives.**
- **A, PROPOSED:** add one block test per argument (Phase 2 below), review only.
  - Cost: one coder turn and one commit. The suite grows from 207 to 211 tests.
  - No restart: no file Claude Code loads changes.
- **B:** add no tests, and accept the static reading of the scope-generic `decide()` as coverage.
  - This is the "assumed equivalence" that plan:3279 forbids. It needs an explicit amendment of Revision 8's closure (plan:346).
  - Not recommended.

**No other gap is genuine.** Each of these is behaviour-covered by a test with the same hook and the same argument:
- 2a-2e;
- `reviews-probe`;
- the three reviews-scope `src/` targets;
- 5a, 5b, 5d and 5f.

For each, the ledger states the input differences:
- **5b:** the length-shortfall branch versus a third-token mismatch, both ending in `command is not in the tester allowlist`.
- **5f:** the reason text carries a different token.
- **5d:** the explainer profile has no `dotnet build` entry at all.

### D2 — the db-reviewer connection

**Restriction versus usage description.** A restriction governs what the agent does or writes. A usage description schedules when the step normally runs.

| Source | Text | Kind |
|---|---|---|
| db-reviewer.md:14 | "You ONLY review. You NEVER edit existing files and NEVER run commands. The only file you write is your verdict file" | restriction |
| db-reviewer.md:53-67, :78 | file `…-db-review-phaseN[-rN].md`, five-line header, `DB_APPROVED` / `DB_NEEDS_REVISION` | output contract |
| CLAUDE.md:100-101, :107-108 | verdict only, never edits files; reviewers write only under `thoughts/shared/reviews/` | restriction |
| db-reviewer.md:3 | "Use after code-reviewer approves any phase that touched the data model, a migration, or a query." | usage description |
| CLAUDE.md:91 | "db-reviewer (only if the phase touched schema, migrations or queries)" | pipeline scheduling |
| CLAUDE.md:101 | "Skipping it on a schema-touching phase is a defect." | obligation for schema phases only |
| plan:399 | "db-reviewer is not required" | requirement, not a prohibition |
| plan:3588-3596 | `/pr` precondition 7 applies only when a review is required | gate scope; an extra db-review file is not rejected |

Read strictly, "only if" (CLAUDE.md:91) could be taken to bar the step on a non-schema phase. The sentence describes the sequence of steps that "the human drives" (CLAUDE.md:92). Neither the db-reviewer's own definition nor its CLAUDE.md bullet forbids a review the human asks for. Because that reading is not certain, this is the human's decision.

**Alternatives.**
- **A, PROPOSED:** the human authorizes a genuine db-review of Phase 2 at the db-reviewer stage, after an APPROVED Phase 2 code review and before the tester (CLAUDE.md:90-92). Order step R9.8.
  - No verdict is prescribed.
  - If the db-reviewer declines without writing, the connection stays pending (plan:3292) and the human returns to this decision.
- **B:** defer the db-reviewer connection to the first schema-touching phase of a later branch, and record it as a residual.
  - This leaves C2.2 and P2.4 open for Phase 2 and needs an explicit amendment of plan:340 and plan:346.
  - Not recommended.
- **C, rejected:** cite another reviewer's write to the same directory. Plan:3289 rules that out.

### D3 — the explainer's journal number and file

**Facts.**
- The explainer writes `docs/journal/NNNN-<slug>.md`, with NNNN "the PR number" that the caller supplies as input (explainer.md:22, :36-37; CLAUDE.md:104-105).
- Repository practice is sequential journal numbers (0001-0008).
- A PR-end journal for this branch is "the human's call" (plan:3805).

**Alternatives.**
- **A, PROPOSED:** the human supplies `0009`, the next free number.
  - The prompt says the entry covers the validated Phase 2 state of an unfinished PR. A slug that says so (for example `chore-agents-phase-2-guards`) keeps the path distinct.
  - A later PR-end entry, if the human wants one, takes another path: a different slug, or the next number.
- **B:** another number the human assigns, under the same rules.
- **C, rejected:** a `note-YYYY-MM-DD-<slug>.md` name like `docs/journal/note-2026-09-18-configuration-predicate-guard.md`. It contradicts explainer.md:36-37 and CLAUDE.md:104-105.
- **D, rejected unless deferral is decided explicitly:** wait for a GitHub PR number. That number exists only at `/pr` (P4.4), so the explainer profile would be deferred beyond Phase 2.

### D4 — the range of the final code review (R9.12)

- **A, PROPOSED:** a re-review `-rN` with:
  - Range `<Head of the last R9.7 review>..<As built commit>`;
  - Head = the As built commit.
  
  It satisfies Probe protocol step 1.9 (plan:416) and follows the Phase 1 re-review precedent (plan:3, `3d26eed..b8c796f`). It does not re-read unchanged code.
- **B:** a full range `84ebd9072f3a06783d71f4af4a7d95f4644c9361..<As built commit>`. Choose B if "replace the review … of the final evidence-bearing state" (plan:430) is read as requiring a full re-read.

### D5 — the boundary of the reuse record

Plan:348 requires the human to record that "the relevant hook, agent and configuration files did not change".

**PROPOSED:**
- The R9.1 identity record holds the repository files' sha256 and the `git diff --name-only` check.
- As context, not as a condition, it also records:
  - `claude --version`;
  - the sha256 of `C:\Users\jesus\.claude\settings.json`.
- No user-settings hash exists for the earlier records:
  - the P2.2 and P2.4 records at a68dd16;
  - the researcher record at 5064146.
  
  For these, the ledger states that reuse rests on four things: the repository files, session `340a3876-cdcb-4d00-b7ea-9aa2e4731896`, which started after the restart (ev:14070-14090), and the recorded version 2.1.292.

**Alternative:** repository files only, exactly as plan:348 lists them, with the boundary stated.

## Why Revision 8 is circular as written, and where the order cuts it

**The cycle.**
- Probe protocol step 1.9 runs code-reviewer on the As built evidence commit (plan:416). Step 1.11 then runs the tester (plan:418).
- Revision 8 needs every connection recorded before the As built copy (plan:346, plan:430).
- It also starts the explainer only after the tester returns READY_TO_PUSH (plan:430; CLAUDE.md:104).

So the explainer's record would need a tester, which needs a review, which needs the As built commit, which needs the explainer's record. Revision 8 cut this with a preliminary review and validation (plan:430).

**Five points stayed open:**
1. **The tester's path.** `tester-verdict.md` has one fixed path (tester.md:22, :41).
   - The final tester run (step 1.11) would overwrite the preliminary file, which plan:3288 forbids.
   - `TestResults/` is gitignored, so commit-based retention (plan:3291) cannot keep it.
2. **The journal commit.** The journal entry is a tracked file written after the preliminary tester, and no rule placed its commit. Plan:420 allows only the review-file commit between code-reviewer and tester.
3. **The journal number.** It had no stated source (explainer.md:22, :36-37).
4. **The ledger's test results.** The only unmutated source in view was the final Validation run. That run comes after the As built commit, which must already hold the ledger and the closure assessment (plan:346, plan:3347).
5. **Missing tasks.** Plan-reviewer and db-reviewer had no named task (plan:3283).

**How the Revision 9 order cuts each:**
1. capture and move the preliminary verdict file (R9.9);
2. the journal commit lands before As built and before the final reviewer (R9.10);
3. a human-assigned number (D3);
4. the R9.6 record;
5. R9.3 and R9.8.

**No step depends on a later step:**

| Step | Needs | Produces |
|---|---|---|
| R9.1 | nothing | identity record |
| R9.2 | R9.1 | planner connection; proposal commit |
| R9.3 | R9.2 | plan-reviewer connection; review commit |
| R9.4 | R9.3 | human decision; Revision 9 plan commit |
| R9.5 | R9.4 | test commit |
| R9.6 | R9.5 | unmutated decision-test record |
| R9.7 | R9.5 | code-review file and commit (and record) |
| R9.8 | APPROVED R9.7 | db-review file, connection, commit |
| R9.9 | R9.8 | tester connections; preliminary verdict captured and moved |
| R9.10 | R9.9 READY_TO_PUSH verdict line, plus the human's authorization | explainer connections; journal commit |
| R9.11 | R9.1-R9.10 | ledger, assessments, As built commit |
| R9.12 | R9.11 | final review, final tester, Validation, READY_TO_PUSH (human) |

## Disposition of the research's open questions

1. **db-reviewer connection vs role.** It is not categorically forbidden. The genuine restrictions and the usage descriptions are separated in D2.
   - PROPOSED: a human-authorized genuine Phase 2 db-review (R9.8).
   - The human decides (D2).
   - Sources: db-reviewer.md:3, :14, :53-67, :78; CLAUDE.md:90-92, :100-101; plan:399, plan:3588-3596.
2. **Explainer contract vs a preliminary entry.**
   - Restrictions, which stay: write only under `docs/journal/`; read-only allowlisted Bash; no ADR; no answers; the `NNNN-<slug>.md` name (explainer.md:24-30, :36-37, :81-82, :87-88).
   - Usage descriptions: "for a completed PR" and "finished PR" (explainer.md:3, :18-20). Revision 8 already requires the entry to say the PR is unfinished (plan:430).
   - The number is a caller input: D3, PROPOSED `0009`.
   - Coexistence with a PR-end entry: a different path, never an overwrite (E4).
3. **READY_TO_PUSH as the explainer's prerequisite.**
   - Revision 8 already makes it a conjunction (plan:430):
     - the tester's returned READY_TO_PUSH, which is a model report used only as a trigger;
     - the human's separate authorization.
   - Neither part is Phase 2's READY_TO_PUSH, which stays the human's decision on the final validation log (plan:3344).
   - E1 states this and settles Revision 6's open item (plan:305) for the Phase 2 collection order only. CLAUDE.md:104 stays as it is for Phase 4.
   - A mismatch between the returned verdict and the file's `Verdict:` line is an unexplained finding (plan:418).
4. **Tester verdict file path.**
   - The path stays fixed. The tester's contract is unchanged.
   - After each tester run, and before the next one, the human records the file's sha256 and content into the evidence with `run`. The content is a model report.
   - The human then moves the file to `TestResults/chore-agents-hardening/validation/tester-verdict-prelim-<HEAD7>-<n>.md` and records its sha256 again (R9.9).
   - No later run overwrites it. The content reaches the committed As built through the evidence file.
5. **Preliminary tester vs Validation "Order" and the human-only commands.**
   - Validation "Order" governs the human's Validation run, not the preliminary tester run.
   - The tester runs only allowlisted commands (plan:3344).
   - The R9.9 prompt says truthfully that As built and the final review do not exist yet, and that the human-only commands are out of scope.
   - A NEEDS_FIXES is examined as step 1.11 requires (plan:418):
     - a confirmed claim goes to the coder workflow;
     - a fully refuted report is recorded, and the human may authorize a new run.
   - The explainer waits. This outcome is expected and is not overridden.
6. **"Concurrently."**
   - The tests never write to the project log (see "Confirmed", item 2).
   - The rule keeps the evidence window attributable. A test run that is itself the agent's recorded call is part of the collection: P2.5's tester operation, or a Validation command in the tester's run.
   - E3 makes this exact.
   - The human's R9.6 run and Validation run happen while no agent task is running.
7. **Code-reviewer connection basis.** The two lines do not conflict:
   - Plan:3281 keeps the existing dummy write as an actual connection observation.
   - Plan:3288's "unrelated or fake report" rule governs new checks for the remaining profiles.
   - The genuine R9.7 review writes a real verdict file through the same hook. Its record is added to the C2.2 row. Closure does not depend on it.
8. **Genuine planner and plan-reviewer tasks.**
   - **Planner:** this run. Its new file `thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md` follows planner.md:16-21 and overwrites nothing (R9.2).
   - **Plan-reviewer:** its review of this proposal, the pipeline step after a planner (CLAUDE.md:90).
     - File: `thoughts/shared/reviews/<date>_chore-agents-hardening-plan-review.md`, with `-rN` only for a re-review of this proposal (R9.3).
     - Skipping this review round leaves the plan-reviewer connection pending.
   - Whether each run counts is the human's decision under plan:444.
9. **Journal commit placement.**
   - Its own commit (R9.10), after its connection is recorded.
   - It lands before the As built commit and before the final reviewer (plan:420: "Every evidence and doc commit lands before the reviewers run").
   - It makes the preliminary tester verdict stale. That verdict is preliminary and closes nothing (plan:430).
10. **"Actual results" for the coder rows.**
    - Not the final Validation run: that is circular, because the closure assessment must precede the As built commit.
    - Not the tester's report: it is a model report, its output may be truncated or piped, and it is not evidence (plan:3344).
    - Source: one human-run mechanical record of `node --test .claude/hooks/tests/*.test.js` (R9.6), made after the test commit and bound to HEAD by `git diff --exit-code HEAD -- .claude/hooks`.
11. **As built.** Yes. The human appends the limitation, and every Revision 9 record, to the evidence file. Step 1.7's mechanical copy then carries them (plan:414). E1 states it.
12. **The research Write as a researcher connection.**
    - The mechanical correlation exists (ev:14568-14596).
    - Still open:
      - the human's acceptance, as a human decision record;
      - the hook and agent-file hashes, supplied by R9.1.
    - Acceptance approves no content. The report is preserved as a model report with a recorded count correction (ev:14600). This proposal corrects it further (above).

How reuse avoids approving content (applies to items 7 and 12, and to the coder records):
- **What a connection record shows:** a mechanical result record (plan:433) of three things:
  - the named agent (`agentType`, `agent_id`, session) made the correlated Write or Bash call;
  - the named hook logged its decision for that call;
  - the tool result.
- **What accepting it is:** a human decision (plan:435) that the record satisfies one C2.2 pair.
- **What the content stays:** a model report (plan:436). The dummy file was no review and was deleted (ev:14290-14298). The research is a draft input to the ledger, not the ledger.
- **Reuse conditions:** each record keeps its own commit and session. It is reused only while R9.1 shows the profile's relevant files unchanged (plan:348).

## Proposed wording — edit set E1-E7 for the plan

The human builds an edit set from these blocks, adjusted to the decisions, and applies it as Revision 8 was applied: each anchor must match exactly once (ev:14434-14449). Anchors are quoted from the plan at 5064146.

### E1 — new section `## Revision 9`

Insert after the paragraph that ends "No new permission to bypass a role rule is introduced." (plan:350). It goes before the `---` line (plan:352).

```text
## Revision 9

Human decision (<YYYY-MM-DD>): <the human's wording>. Proposal: thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md. Plan review: <review file>. Research (a model report): thoughts/shared/research/2026-10-09_phase2-r8-coverage.md. This revision makes Revision 8's remaining Phase 2 verification executable. It adds four write-scope tests and fixes an order. It changes no hook code, permission, agent definition, model or effort value, CLAUDE.md, evidence record or As built section, and it closes no probe.

What changes:
- Count. P2.2, P2.4 and P2.5 retain 17 original negative targets: 5 coder (2a-2e), 8 write-scope (the seven `src/LIVEPROBE-4-<agent>.txt` targets and `thoughts/shared/reviews-probe/LIVEPROBE-4-code-reviewer.md`) and 4 allowlist (5a, 5b, 5f, 5d). Only 2e has a recorded live block. The other 16 stay inconclusive and UNVERIFIED end to end.
- Coverage. Byte-identical decision coverage of an original target is impossible, because no test file may contain `LIVEPROBE` (C1.7; Validation command 3). The ledger records "none (C1.7)"; this is a structural limit, not a gap. Behaviour coverage is a named test with the same hook and the same profile or scope argument, whose input reaches the same decision branch; the ledger states every input and reason difference. Four scope arguments had no block test; Tests to add gains one for each, review only. No mutation proof is added.
- Results. The ledger's unmutated test results come from one human-run mechanical record of Validation command 2's command at a stated HEAD after the test commit (Probe protocol step 3, "Revision 9 execution order", R9.6). The final Validation run checks them again at the validated HEAD.
- Order. Probe protocol step 3, "Revision 9 execution order (Phase 2 only)", fixes, for each remaining connection, the genuine task, file, record, retention and commit, and the preliminary and final review and validation steps.
- The concurrency sentence in Probe protocol step 3 is made exact. P2.4 points to the order. P2.5 gains the coverage ledger. Validation "Order" names the added commits.

Settled readings:
- Restriction and usage description. A role restriction governs what the agent does or writes: its tools, write scope, file name, header, command allowlist and verdict values. A description of when a role is normally used schedules the step and does not forbid a human-authorized task. Examples are an agent's `description`, a pipeline position in CLAUDE.md, and a plan's "not required". For db-reviewer, the restrictions are db-reviewer.md:14, :53-67 and :78 and CLAUDE.md:100-101. CLAUDE.md:91, db-reviewer.md:3 and "db-reviewer is not required" (Touches schema?) are scheduling statements. A human-authorized db-review of Phase 2 is role-compatible; it runs after an APPROVED code review and before the tester. For the explainer, the restrictions are explainer.md:24-30, :36-37, :81-82 and :87-88. "For a completed PR" (explainer.md:3, :18-20) describes normal use, and the Phase 2 entry states that the PR is unfinished (Revision 8).
- READY_TO_PUSH. In the collection order, the explainer's trigger is the tester's returned report: its verdict line and its verdict file's `Verdict:` line both read READY_TO_PUSH. That report is a model report and serves only as the trigger. The human's authorization of the entry is a separate human decision. Neither is Phase 2's READY_TO_PUSH, which stays the human's decision on the final validation log. This settles Revision 6's open item for Phase 2's collection order only.
- Journal number. The explainer's "PR number" input is the journal number the human assigns. It need not be a GitHub PR number: docs/roadmap.md says roadmap and GitHub numbers differ, and docs/journal numbers its entries 0001-0008.
- Fixed tester path. `TestResults/<branch-slug>/tester-verdict.md` has no `-rN`. Before any later tester run, the human captures the existing file into the evidence file and moves it to a retained name. No tester run overwrites a report.
- Reuse without approval. A connection record shows that the named agent made the correlated call, that the named hook logged its decision for that call, and what the tool returned. It approves nothing the agent wrote. The P2.2 coder records, the P2.4 code-reviewer observation and "## R8 researcher — Write connection records" are reused under R9.1. The research report stays a model report. The code-reviewer's probe file was no review.
- As built. The Revision 8 limitation and every Revision 9 record reach As built only through the evidence file. The human appends them there, and Probe protocol step 1.7 copies the file mechanically.

The test commit changes no file Claude Code loads, so it needs no restart. It requires no rerun of an existing observation.

Unchanged: Revision 8's accepted limit, closure conditions and Unchanged list; Phase 1; the M2 records and assessments; the conditions of P2.1, P2.3, P2.6, P2.7 and P2.8; Phases 3 and 4.
```

### E2 — new paragraph in Probe protocol step 3

Insert after the paragraph that ends "All retained report paths and commits are accounted for; no dummy report is a substitute." (plan:430). It goes before "**Record types (Revision 6).**" (plan:432) and is indented three spaces, like that paragraph.

```text
   **Revision 9 execution order (Phase 2 only).** The human advances one step at a time, and no step starts before the previous one is recorded. Each agent task is genuine work in its role. Its prompt states the task, the stage and the caller-supplied values (branch slug, Range, Head, plan path, phase or number), carries the probe preamble's delegation wording, and prescribes no verdict. Before each agent task the human records that the target report path does not exist. After it, the human records:
   - the connection, by the report-write correlation of this step: session_id, agent_id, tool_name, exact target path, call and result timestamps, tool_use_id, and the report path and sha256;
   - P2.1's model and effort;
   - the C2.3 fields;
   - `git status --porcelain --untracked-files=all`.

   R9.1-R9.4 run before this paragraph exists, under Revision 8's rules. They are listed here so the order is complete.
   - R9.1 Identity record (human, `run`). It holds:
     - branch, HEAD and `git status --porcelain --untracked-files=all`;
     - `git diff --name-only ce5e7cfcb6a90561b69fdb24c0db5384196df3f0 HEAD -- .claude .mcp.json CLAUDE.md`, whose output is empty before R9.5 and exactly `.claude/hooks/tests/write-scope.test.js` after it;
     - the sha256 of `.claude/hooks/coder-bash-guard.js`, `.claude/hooks/bash-allowlist.js`, `.claude/hooks/write-scope.js`, `.claude/hooks/path-guard.js`, `.claude/hooks/lib/guard-common.js`, the eight `.claude/agents/*.md`, `.claude/settings.json` and `.mcp.json`;
     - as context, `claude --version` and the sha256 of `C:\Users\jesus\.claude\settings.json`.

     It is the "unchanged" record that reuse needs for three earlier records: the P2.2 coder records and the P2.4 code-reviewer observation (made at a68dd16d9b8f9bf670396c5aaf8d859a03337e81), and the researcher record (made at 5064146a6b9ca38152f81fc2a798b44536ea4793). All three are in session 340a3876-cdcb-4d00-b7ea-9aa2e4731896. A profile's record stays reusable while these files are unchanged: its agent file, the hook scripts its frontmatter names, `guard-common.js`, `path-guard.js`, `.claude/settings.json`, `.mcp.json` and `CLAUDE.md`.
   - R9.2 Planner: the run that wrote `thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md`. The human records the connection (`write-scope:thoughts/shared/plans`), then commits the file alone.
   - R9.3 Plan-reviewer: it reviews that proposal.
     - Caller values: Scope `thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md phase 1-5`; Range `<git merge-base main HEAD>..<HEAD>`; Head `<HEAD>`.
     - File: `thoughts/shared/reviews/<date>_chore-agents-hardening-plan-review.md`. `-rN` is used only for a re-review of this proposal.
     - The human records the connection (`write-scope:thoughts/shared/reviews`), then commits the review file alone, whatever its verdict. NEEDS_REVISION goes to the planner, whose revision is a new file.
   - R9.4 Human decision on the proposal. The human applies the edit set to this plan with a script and makes a plan-only commit.
   - R9.5 Coder turn for the four Revision 9 tests (Tests to add, "Other scopes"). It ends with PHASE_COMPLETE, then the human runs `/commit` and repeats R9.1. No restart follows: no file Claude Code loads has changed.
   - R9.6 Decision-test record (human, `run`, while no agent task is running), at the R9.5 HEAD.
     - Preconditions: `git diff --exit-code HEAD -- .claude/hooks` at exit 0 and an empty status.
     - Command: `node --test .claude/hooks/tests/*.test.js`, under Validation command 2's condition.
     - It is the unmutated source of the ledger's results. It does not replace Validation.
   - R9.7 Code-reviewer: a genuine review of Phase 2.
     - Caller values: this plan's path; phase 2; Range `84ebd9072f3a06783d71f4af4a7d95f4644c9361..<HEAD>`; Head `<HEAD>`.
     - File: `thoughts/shared/reviews/<date>_chore-agents-hardening-code-review-phase2.md`.
     - The human records its verdict-file Write like the others. The C2.2 code-reviewer row cites it beside the P2.4 observation; closure does not depend on it.
     - The human commits the review file. NEEDS_REVISION follows the existing correction workflow, and each re-review is `-rN`.
   - R9.8 db-reviewer, only after an APPROVED R9.7: a genuine db-review of Phase 2.
     - Caller values: this plan's path; phase 2; Range `84ebd9072f3a06783d71f4af4a7d95f4644c9361..<HEAD>`; Head `<HEAD>`.
     - File: `thoughts/shared/reviews/<date>_chore-agents-hardening-db-review-phase2.md`.
     - The human records the connection (`write-scope:thoughts/shared/reviews`) and commits the review file. DB_NEEDS_REVISION goes to the coder.
     - In this order, step 1.10's review-file commit is the R9.7 commit followed by the R9.8 commit. Each changes only paths under `thoughts/shared/reviews/`.
   - R9.9 Tester, preliminary run, on the R9.8 commit. `TestResults/chore-agents-hardening/tester-verdict.md` must not exist before it.
     - The prompt says three things. The As built evidence commit and the final review do not exist yet, by design. Human-only Validation commands are out of scope. P2.5's operations `git log --oneline -1 --grep=LIVEPROBE-5c` and `node --test .claude/hooks/tests/write-scope.test.js` are part of the task, one Bash call each.
     - The human records both connections (`bash-allowlist:tester`, `write-scope:TestResults`). The human also records P2.1 for the tester, including the main-session Agent input without a `model` field.
     - With `run`, the human records the verdict file's sha256 and content; the content is a model report. The human then moves the file to `TestResults/chore-agents-hardening/validation/tester-verdict-prelim-<HEAD7>-<n>.md` and records the moved file's sha256.
     - A NEEDS_FIXES is examined as step 1.11 says. A confirmed claim goes to the coder workflow, and R9.7-R9.9 repeat on the fix. If every claim is refuted, the human records the refutations and may authorize a new tester run whose prompt cites them.
   - R9.10 Explainer, only when two conditions hold: an R9.9 run's returned verdict and verdict file both read READY_TO_PUSH, and the human has authorized the entry separately.
     - Input: this plan's path and the journal number the human assigns.
     - The prompt says that the entry covers the validated Phase 2 state, that the hardening PR and Phases 3-4 are unfinished, and that P2.5's operation `git log --oneline -3 --grep=LIVEPROBE-5e` is part of the task, as one Bash call.
     - `docs/journal/<NNNN>-<slug>.md` must not exist before the run.
     - The human records both connections (`bash-allowlist:explainer`, `write-scope:docs/journal`), then commits the entry alone.
   - R9.11 The human repeats R9.1, then appends to the evidence file:
     - the coverage ledger and the C2.2 connection table (P2.5, "Coverage ledger");
     - the Phase 2 records and assessments not yet recorded: P2.1 for every profile, C2.1, C2.3, C2.4, and the assessments of P2.2-P2.8;
     - Probe protocol step 4's cleanup checks;
     - the separate human assessment that accepts Revision 8's residual limit for exactly the 16 rows without a live block;
     - the limitation text for As built.

     Then steps 1.6-1.8 follow: cleanup, the mechanical As built copy verified with `diff`, and the As built commit.
   - R9.12 Steps 1.9-1.11 run on the As built commit:
     - a code-reviewer re-review `-rN` (Range `<Head of the last R9.7 review>..<As built commit>`, Head the As built commit), then its review-file commit;
     - the tester on that commit. Its verdict file is a new file, because R9.9 moved the preliminary one.

     Then come the human's Validation run and the human's READY_TO_PUSH decision. A doc commit after the run carries only the validation record (Validation, "Order"), appended to the evidence file and copied into As built: the log's name and sha256, the READY_TO_PUSH record, and the final tester's report and verdict-file sha256, recorded as a model report.

   Every commit of R9.2-R9.11 lands before R9.12's reviewer. A commit after R9.9 makes R9.9's verdict stale; that verdict is preliminary and closes nothing. If a finding changes a file that R9.1 lists:
   - step 5's restart and a session-start record follow;
   - R9.6 is repeated if a file under `.claude/hooks` changed;
   - every connection whose relevant files changed is collected again.
```

### E3 — the concurrency sentence in Probe protocol step 3

Replace, in plan:444: "Do not run local hook tests concurrently with evidence collection; retain the test-log isolation rule."

With:

```text
Do not run local hook tests concurrently with evidence collection: from the start of an agent task under check until the human has extracted its tool and hook-log records, no one else runs a hook test. A test run that is itself a recorded call of the agent under check is part of that collection, not concurrent with it; examples are P2.5's tester operation and a Validation command in the tester's own run. Retain the test-log isolation rule: `run-hook.js` gives every test run a temporary `ENVANEX_HOOK_LOG_DIR`.
```

### E4 — P2.4 pointer

Insert a bullet after the bullet beginning "  - A role-compatible Write that is itself refused remains pending/inconclusive;" (plan:3292):

```text
  - Revision 9: Probe protocol step 3, "Revision 9 execution order", fixes the genuine task, file, record, retention and commit for each remaining profile: planner R9.2, plan-reviewer R9.3, code-reviewer R9.7 (beside the observation retained above), db-reviewer R9.8, tester R9.9, explainer R9.10. The researcher's connection is "## R8 researcher — Write connection records", reused under R9.1. No tester run writes over an existing `tester-verdict.md` (R9.9), and no explainer run writes over an existing journal file (R9.10).
```

### E5 — coverage ledger after P2.5

Insert a new list item after P2.5's last bullet, "  - Record P2.1 model/effort and C2.3 hook identity fields. …" (plan:3298). It goes before "- **P2.6 (LSP and Learn):**".

```text
- **Coverage ledger (Revision 9; P2.2, P2.4, P2.5):** a human record in the evidence file, with one row per original negative target. There are 17: 2a-2e; the seven `src/LIVEPROBE-4-<agent>.txt` targets and `thoughts/shared/reviews-probe/LIVEPROBE-4-code-reviewer.md`; and 5a, 5b, 5f and 5d. Columns:
  1. the exact target, the profile, and the hook and argument from that agent file;
  2. byte-identical decision coverage: "none (C1.7)" in every row, a structural limit and not a gap;
  3. behaviour coverage: the prefixed test name, its input and its asserted reason substring, plus the source lines on which a static reading puts the exact target on the same decision branch; every input and reason difference is stated;
  4. the unmutated result: that test's line in the R9.6 record, plus the R7 copy-runner baseline line where one exists;
  5. the Behaviour mapping category: the M2.x proof only for its mapped item, otherwise "review only";
  6. the live record: "exact live block" with its tool result and hook-log line (2e only), or "not called" with the tool-call list and `grep -F` output that show it;
  7. the residual: "UNVERIFIED end to end (Revision 8)" for the 16 rows without a live block.

  A second table lists the ten profile-hook pairs that C2.2 names: one per profile, plus `bash-allowlist` for the tester and the explainer. Each row gives the connection record's heading, the P2.1 model and effort record, the C2.3 fields, and the R9.1 identity record that its reuse rests on. The ledger quotes records; it never restates a model report as a result.
```

### E6 — Tests to add, write-scope "Other scopes"

Insert four bullets after "  - `write-scope: allows TestResults/chore-agents-hardening/tester-verdict.md under TestResults`" (plan:3170):

```text
  - `write-scope: blocks src/x.txt under thoughts/shared/research` — argument `thoughts/shared/research`, target `src/x.txt`; asserts exit status 2 and stderr containing `Blocked by write-scope:thoughts/shared/research:` and `target is outside the write scope "thoughts/shared/research"` (Revision 9)
  - `write-scope: blocks src/x.txt under thoughts/shared/plans` — argument `thoughts/shared/plans`, target `src/x.txt`; asserts exit status 2 and stderr containing `Blocked by write-scope:thoughts/shared/plans:` and `target is outside the write scope "thoughts/shared/plans"` (Revision 9)
  - `write-scope: blocks src/x.txt under TestResults` — argument `TestResults`, target `src/x.txt`; asserts exit status 2 and stderr containing `Blocked by write-scope:TestResults:` and `target is outside the write scope "TestResults"` (Revision 9)
  - `write-scope: blocks src/x.txt under docs/journal` — argument `docs/journal`, target `src/x.txt`; asserts exit status 2 and stderr containing `Blocked by write-scope:docs/journal:` and `target is outside the write scope "docs/journal"` (Revision 9)
```

The Behaviour mapping table is not edited. Its last row, "every other item in this section → review only" (plan:3184), covers these four items.

### E7 — Validation "Order"

Replace (plan:3347): "- Validation runs after all of Phase 2's commits: the code commit that contains `.claude/settings.json` and `.mcp.json`, the commit that copies the probe evidence into As built, and the review-file commit."

With:

```text
- Validation runs after all of Phase 2's commits: the code commit that contains `.claude/settings.json` and `.mcp.json`, the Revision 9 test commit, the commits of R9.2-R9.10 (Probe protocol step 3, "Revision 9 execution order"), the commit that copies the probe evidence into As built, and the review-file commit of R9.12.
```

## Phase 1: Planner and plan-reviewer connections, human decision, Revision 9 text (R9.1-R9.4)

### Files
- `thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md`: created by this planner run. The human commits it alone.
- `thoughts/shared/reviews/<date>_chore-agents-hardening-plan-review[-rN].md`: created by the plan-reviewer. The human commits it alone, whatever its verdict.
- `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`: modified by the human's edit set (E1-E7, as decided). Plan-only commit.
- `TestResults/chore-agents-hardening/evidence/phase-2.md`: appended by the human (R9.1 identity record, the R9.2 and R9.3 connections, the decisions, the application record).
- `TestResults/chore-agents-hardening/validation/r9-edit-set.md` and its apply script: created by the human, as for Revision 8 (ev:14427-14497).

### Signatures
None. No code.

### Tests to add
None.

### Validation
The human runs these with `run` in an external Git Bash at the repository root.

R9.1:
- `git branch --show-current`: exit 0; `chore/agents-hardening`.
- `git rev-parse HEAD`: exit 0; the value is recorded.
- `git --no-optional-locks status --porcelain --untracked-files=all`: exit 0. Before the R9.2 commit the output is exactly `?? thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md`; otherwise there is no output.
- `git diff --name-only ce5e7cfcb6a90561b69fdb24c0db5384196df3f0 HEAD -- .claude .mcp.json CLAUDE.md`: exit 0; no output.
- `sha256sum .claude/hooks/coder-bash-guard.js .claude/hooks/bash-allowlist.js .claude/hooks/write-scope.js .claude/hooks/path-guard.js .claude/hooks/lib/guard-common.js .claude/agents/*.md .claude/settings.json .mcp.json`: exit 0; 15 lines.
- `/c/Users/jesus/.local/bin/claude.exe --version` and `sha256sum /c/Users/jesus/.claude/settings.json`: exit 0; recorded as context (D5).

R9.2:
- Run the correlation check whose assertions are those of ev:14576-14587, changed to agentType `planner`, hook `write-scope:thoughts/shared/plans` and target `C:/projects/envanex/thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md`. It prints its match line and exits 0.
- After the commit:
  - `git diff --name-only HEAD^ HEAD` is exactly the proposal path;
  - the committed blob's sha256 equals the recorded file sha256;
  - the status is empty.

R9.3:
- Before: `test ! -e thoughts/shared/reviews/<date>_chore-agents-hardening-plan-review.md` exits 0.
- After: the correlation check with agentType `plan-reviewer` and hook `write-scope:thoughts/shared/reviews`, then the same commit checks as R9.2.

R9.4:
- The edit-set check reports exactly one anchor match per edit and disjoint regions.
- `git diff --check`: exit 0.
- `git --no-optional-locks status --porcelain --untracked-files=all`: exactly ` M thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`.
- `grep -c '^## Revision 9$' thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`: `1`.
- `grep -c $'\r' thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`: `0`, exit 1.
- After the commit:
  - `git diff --name-only HEAD^ HEAD` is exactly the plan path;
  - `git diff --exit-code ce5e7cfcb6a90561b69fdb24c0db5384196df3f0 HEAD -- .claude .mcp.json CLAUDE.md` exits 0;
  - the status is empty.

## Phase 2: Four write-scope block tests and the unmutated decision-test record (R9.5-R9.6)

Runs only if D1-A is accepted.

The coder prompt names this plan path and "Revision 9 tests only". It ends with: "do NOT run any other agent, do NOT commit, wait for me" (CLAUDE.md:96-97).

### Files
- `.claude/hooks/tests/write-scope.test.js`: modified. The four E6 tests are appended after line 129, in the "// Other scopes" group. No existing line changes.
- `TestResults/chore-agents-hardening/evidence/phase-2.md`: appended by the human (the repeated R9.1 and R9.6).

### Signatures
No new helper. Each test calls the existing helpers:
- `runWrite(scopeArgs, filePath, projectDir = DEFAULT_PROJECT_DIR)` (`write-scope.test.js:32-35`), with `[<scope>]` and `'src/x.txt'`;
- `assertBlocked(result, hook, reason)` (`:45-49`), with hook `write-scope:<scope>` and reason `target is outside the write scope "<scope>"`.

The project dir stays `C:\projects\envanex`. The `C:/` form is covered by `guard-common.test.js:112`.

### Tests to add
`.claude/hooks/tests/write-scope.test.js`, flat `test()` calls, all review only:

| Test name | Scope argument | Target | Asserted |
|---|---|---|---|
| `write-scope: blocks src/x.txt under thoughts/shared/research` | `thoughts/shared/research` | `src/x.txt` | exit 2; `Blocked by write-scope:thoughts/shared/research:`; `target is outside the write scope "thoughts/shared/research"` |
| `write-scope: blocks src/x.txt under thoughts/shared/plans` | `thoughts/shared/plans` | `src/x.txt` | exit 2; `Blocked by write-scope:thoughts/shared/plans:`; `target is outside the write scope "thoughts/shared/plans"` |
| `write-scope: blocks src/x.txt under TestResults` | `TestResults` | `src/x.txt` | exit 2; `Blocked by write-scope:TestResults:`; `target is outside the write scope "TestResults"` |
| `write-scope: blocks src/x.txt under docs/journal` | `docs/journal` | `src/x.txt` | exit 2; `Blocked by write-scope:docs/journal:`; `target is outside the write scope "docs/journal"` |

The target keeps the original targets' form: relative, forward slashes, `.txt` under `src/`. Only the basename differs, because `LIVEPROBE` is excluded (C1.7).

### Validation
The coder runs these and reports them. The human checks them against the transcript.
- `node --test .claude/hooks/tests/write-scope.test.js`: exit 0; `tests 17`, `pass 17`, `fail 0`; a `✔` line for each of the four names.
- `node --test .claude/hooks/tests/*.test.js`: exit 0; `tests 211` (85 + 126), `pass 211`; `fail`, `cancelled`, `skipped` and `todo` all 0.
- `dotnet build -warnaserror`: exit 0; `0 Warning(s)` and `0 Error(s)` (coder.md:26).
- `git status --short`: exactly ` M .claude/hooks/tests/write-scope.test.js`.

The human runs these with `run`, after `/commit` and the repeated R9.1:
- `git diff --name-only HEAD^ HEAD`: exactly `.claude/hooks/tests/write-scope.test.js`.
- `git diff --numstat ce5e7cfcb6a90561b69fdb24c0db5384196df3f0 HEAD -- .claude/hooks/tests/write-scope.test.js`: one line whose second field is `0`.
- `git diff --name-only ce5e7cfcb6a90561b69fdb24c0db5384196df3f0 HEAD -- .claude .mcp.json CLAUDE.md`: exactly `.claude/hooks/tests/write-scope.test.js`.
- `grep -c LIVEPROBE .claude/hooks/tests/*.js .claude/hooks/tests/fixtures/*.js`: exit 1; the seven paths of Validation command 3, each with `:0`.
- `grep -c $'\r' .claude/hooks/tests/write-scope.test.js`: `0`, exit 1.
- `git diff --name-only be6477e..HEAD -- .claude/hooks/tests/guard-common.test.js .claude/hooks/tests/path-guard.test.js .claude/hooks/tests/run-hook.js .claude/hooks/tests/fixtures`: exit 0; no output (Validation command 17).

R9.6 runs while no agent task is running:
- `git diff --exit-code HEAD -- .claude/hooks`: exit 0.
- `git --no-optional-locks status --porcelain --untracked-files=all`: no output.
- `node --test .claude/hooks/tests/*.test.js`: meets Validation command 2's condition (plan:3362), with `tests` 211 at the time of writing.

## Phase 3: Remaining connections through genuine pipeline work, and the journal commit (R9.7-R9.10)

### Files
- `thoughts/shared/reviews/<date>_chore-agents-hardening-code-review-phase2[-rN].md`: created by code-reviewer and committed by the human.
- `thoughts/shared/reviews/<date>_chore-agents-hardening-db-review-phase2.md`: created by db-reviewer (D2-A) and committed by the human.
- `TestResults/chore-agents-hardening/tester-verdict.md`: created by the tester. The human captures it into the evidence and moves it to `TestResults/chore-agents-hardening/validation/tester-verdict-prelim-<HEAD7>-<n>.md`.
- `docs/journal/<NNNN>-<slug>.md` (D3-A: `0009`): created by the explainer and committed alone by the human.
- `TestResults/chore-agents-hardening/evidence/phase-2.md`: appended by the human.

### Signatures
None.

### Tests to add
None.

### Validation
The human runs these with `run`. Each correlation check has the R9.2 assertions, with that run's agentType, hook and target.

R9.7 and R9.8, for each review:
- Before: `test ! -e <review path>` exits 0.
- After: the correlation check.
- After the commit: `git diff --name-only HEAD^ HEAD` is exactly the review path, and the status is empty.

R9.9:
- Before: `test ! -e TestResults/chore-agents-hardening/tester-verdict.md` exits 0, and the status is empty.
- `grep -F LIVEPROBE-5c TestResults/hook-log/hooks.jsonl` gives exactly one line: hook `bash-allowlist:tester`, decision `allow`, agent_type `tester`, and the run's agent_id and session_id.
- Correlation checks:
  - the Bash call `node --test .claude/hooks/tests/write-scope.test.js` has one `bash-allowlist:tester` allow line with that exact target, inside the call/result window;
  - the verdict-file Write has a `write-scope:TestResults` allow line.
- P2.1: the transcript shows `claude-sonnet-5-5` with effort `low`, and the main-session Agent input has no `model` field.
- `git status --short -- src`: no output (P2.5).
- Capture and move:
  - `sha256sum TestResults/chore-agents-hardening/tester-verdict.md`, then `cat TestResults/chore-agents-hardening/tester-verdict.md`;
  - `mv TestResults/chore-agents-hardening/tester-verdict.md TestResults/chore-agents-hardening/validation/tester-verdict-prelim-<HEAD7>-<n>.md`;
  - `sha256sum` of the moved file equals the first hash;
  - `test ! -e TestResults/chore-agents-hardening/tester-verdict.md` exits 0.

R9.10:
- Before: `ls docs/journal` shows no entry beginning `<NNNN>-`.
- `grep -F LIVEPROBE-5e TestResults/hook-log/hooks.jsonl` gives exactly one line: hook `bash-allowlist:explainer`, decision `allow`, agent_type `explainer`.
- The correlation check for the journal Write (`write-scope:docs/journal`).
- After the commit: `git diff --name-only HEAD^ HEAD` is exactly the journal path, and the status is empty.

## Phase 4: Ledger, assessments and the As built evidence commit (R9.11)

### Files
- `TestResults/chore-agents-hardening/evidence/phase-2.md`: appended by the human with:
  - the repeated R9.1;
  - the ledger and the C2.2 table;
  - the remaining P2.x and C2.x records and assessments;
  - the closure assessment;
  - the limitation text.
- `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`: a `## As built — Phase 2` section is created as a mechanical copy of the evidence file. It goes after the `---` that follows Phase 2's Validation (plan:3441) and before `## Phase 3: skills — …` (plan:3443), as for Phase 1 (plan:1098-1100).

### Signatures
None.

### Tests to add
None.

### Validation
- R9.1 repeated: the name-only diff is exactly `.claude/hooks/tests/write-scope.test.js`, and every hash equals the post-R9.5 record.
- Cleanup (Probe protocol step 4):
  - `git status --short`: no output;
  - `ls obj/LIVEPROBE-*`: "No such file".
- Step 1.7's `diff` between the evidence file and the As built section: no difference.
- After the commit:
  - `git diff --name-only HEAD^ HEAD` is exactly the plan path;
  - `git diff --numstat HEAD^ HEAD` has `0` deletions;
  - the status is empty.

## Phase 5: Final review, final tester, human Validation and READY_TO_PUSH (R9.12)

### Files
- `thoughts/shared/reviews/<date>_chore-agents-hardening-code-review-phase2-rN.md`: created by code-reviewer and committed by the human.
- `TestResults/chore-agents-hardening/tester-verdict.md`: a new file from the final tester. Its sha256 and content are recorded with the validation record.
- `TestResults/chore-agents-hardening/validation/phase-2-commands.txt`: written by the human only if it does not exist (plan:3381). Then `validation-<sha7>-<time>.log`.
- The plan and the evidence file: the validation record, appended to the evidence and copied into As built, in an optional post-run doc commit.

### Signatures
None.

### Tests to add
None.

### Validation
- Re-review: `test ! -e <review path>` exits 0 before the run. The review-file commit check is as in R9.7.
- Before the tester: `test ! -e TestResults/chore-agents-hardening/tester-verdict.md` exits 0.
- One run of the commands file under Validation's "Runs" and "Pass rule" (plan:3411-3423), with command 2's `tests` at 211 under D1-A.
- The READY_TO_PUSH record states items 1-3 of Validation "Order" (plan:3349-3352).
- After any doc commit:
  - `git diff --numstat <validated sha>..<last sha>` shows only the plan file with 0 deletions;
  - `git status --short` shows no output.
- Precedent for keeping the tester report in the validation record: plan:2759-2858.

## Reports and commits, in order

Commit messages are suggestions; the human writes them.

| # | Report or change | Producer | Retention | Commit |
|---|---|---|---|---|
| 1 | `thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md` | planner (this run) | tracked | `docs(agents): preserve the R8 correction proposal` (R9.2) |
| 2 | `thoughts/shared/reviews/<date>_chore-agents-hardening-plan-review.md` | plan-reviewer | tracked | review-file commit (R9.3) |
| 3 | plan, Revision 9 text | human edit set | tracked | `docs(agents): plan Revision 9` (R9.4) |
| 4 | `.claude/hooks/tests/write-scope.test.js` | coder | tracked | `test(agents): block tests for four write-scope arguments` (R9.5) |
| 5 | `…_code-review-phase2[-rN].md` | code-reviewer | tracked | review-file commit (R9.7) |
| 6 | `…_db-review-phase2.md` | db-reviewer | tracked | review-file commit (R9.8) |
| 7 | preliminary `tester-verdict.md` | tester | gitignored. Content and sha256 go into the evidence; the file moves to `validation/tester-verdict-prelim-<HEAD7>-<n>.md` | via As built (R9.11) |
| 8 | `docs/journal/<NNNN>-<slug>.md` | explainer | tracked | `docs(journal): Phase 2 guards entry` (R9.10) |
| 9 | evidence file | human | gitignored, append-only | via As built |
| 10 | `## As built — Phase 2` | human (mechanical copy) | tracked | `docs(agents): As built — Phase 2` (R9.11) |
| 11 | `…_code-review-phase2-rN.md` (final) | code-reviewer | tracked | review-file commit (R9.12) |
| 12 | final `tester-verdict.md`, validation log | tester, human `run` | gitignored. Name and sha256 go into the validation record | the optional post-run doc commit (R9.12) |

## Rollback notes

- **Plan text.** `git revert <Revision 9 plan commit>` restores Revision 8. Records made under Revision 9 stay in the evidence as records of their own state. A dated note explains the revert.
- **Tests.** `git revert <test commit>`. R9.1's name-only diff returns to empty. The four scope rows of the ledger fall back to "gap", and D1-B would then need an explicit decision.
- **Review and journal files.** They are never deleted as cleanup. If the human withdraws one, `git revert` its commit and append the reason.
- **Evidence.** It is append-only. A wrong record is followed by a dated correction, never edited (plan:442).
- **Preliminary tester verdict.** The moved file is kept, and its move is recorded. Nothing needs rolling back.
- **A declined write.** If the db-reviewer or the explainer declines to write, the connection stays pending (plan:3292) and the human returns to D2 or D3. The main session never writes in its place.
- **P2.1 fallback.** If it changes an agent file (plan:3268), the restart, the session-start record and the re-collection of affected connections in E2's last sentence apply.
