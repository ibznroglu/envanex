# Plan: Phase 2 — correct Revision 8 so the remaining verification can run (replacement proposal r4)

Status: **PROPOSED**, except the two human decisions recorded under D2 and D3 (2026-10-10). This is document revision r4 of the R8 correction proposal. It supersedes the preserved r3 proposal as the text submitted for review; all earlier proposal files remain unchanged. "r4" names a document revision; it is not Revision 9 of the active plan. Nothing here is applied. This document authorizes no agent run, test, plan edit, commit or pipeline transition, and it closes no probe.

## Revision r4 provenance and review disposition (2026-10-11)

Prepared by Codex from proposal r3, not by a new planner run. Source: `thoughts/shared/plans/2026-10-11_phase2-r8-correction-proposal-r3.md`, sha256 `02197721526edb64968e6ea3aefab972147048a511e9525716350abc10e34073`, preserved at `657b9beca80b4cca01b3aed4cc344a095d2eff58`.

Review: `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-plan-review-r2.md`, sha256 `fc51d2e1fefd7f19d820625c64a52e8a9102496d2c4069ab151c0b93896bac32`, verdict NEEDS_REVISION, preserved alone at `50f889fafc5459a946f111194a83d0dd6bd1fb53`. The human's preservation output shows a clean tree at that commit. The review resolved the original 14 findings and raised five new findings; no proposal has an APPROVED review yet.

This revision changes procedure text only. No test, hook, agent or setting is changed; D1, D4 and D5 remain PROPOSED. D2/D3 remain the decisions of 2026-10-10 recorded under `## Human decisions D2 and D3 (2026-10-10)`.

| Review item | r4 disposition |
|---|---|
| F1 coder task vs report task | E2 explicitly separates R9.5 preconditions, tool/command instructions, Edit/Write/Bash records and post-status from new-report connection checks; Phase 2 repeats the executable instructions. |
| F2 D4-B | E1 uses a neutral final-review label; E2 and Phase 5 specify both options' prechecks, caller values and final record. D4 remains undecided. |
| F3 superseded hash | The version-extraction list identifies the latest recorded hash location for each of the four transcripts. |
| F4 command 2 / count | The quotation matches active-plan command 2. The 211/17 counts are derived expectations for these four additions, not immutable counts after an approved correction. |
| F5 final tester checklist | Phase 5 explicitly requires the applicable APPROVED and DB_APPROVED headers and both changed-path checks. |
| O1 | D2's only CLAUDE.md scheduling exception is line 91; lines 90, 92 and 100-101 remain unchanged. |
| O2 | D3 quotes the human-decision record, including separate run authorization and pending hardening PR. |
| O3 | Journal practice distinguishes unnumbered chore rows from roadmap row 1, chore(solution). |
| O4 | Earlier planner version is labelled a Codex inspection report until human-run extraction. |
| O5 | Code-review and db-review suffixes are allocated independently. |
| O6 | The R9.10 checklist branches explicitly for an unaccepted entry. |

The earlier proposals and reviews remain unchanged. Inherited ev:<line> references describe the r2 source snapshot through ev:15089 unless a later heading or record is identified. They do not claim that the current evidence file ends there. A version not extracted into those cited sections is not necessarily absent from its raw transcript.

Codex's inspection report of `agent-ae5c9090f4055b6e2.jsonl` (sha256 `bc088d8071751fdbeb161752ca7d85ddd1801829a9c7696d9de288bbedf7b10f`) found version set `2.1.292`. This is an inspection report, not the human-run mechanical result record; R9.4 still records extraction before D5. r2's two Writes have their own assessment record; neither r3 nor r4 supplies a new planner connection.

The next proposal review uses suffix `-plan-review-r3` and Scope naming this proposal r4. Proposal and review revision numbers are independent. A later proposal starts at r5; its review takes the next unused review suffix.

Date of inherited r2 source snapshot: 2026-10-10. Branch: `chore/agents-hardening`. HEAD `c0e0b65dee26002974b0619ad5bc80ce9c5f6b0e` with a clean tree is the human's record (ev:15046-15052). The planner ran no command. Statements about hook decisions come from reading the source; statements about runs cite records.

Inputs:
- **Drafting guidance, not evidence:** `TestResults/chore-agents-hardening/validation/r9-correction-brief.md`. The caller verified its sha256 as `0f0f0422d8f476d2a0a40aa0a5dfc05c6aa1e4071d97db3102ddc9bfdb7de7b2`. Codex reports that its earlier planner-version statement is supported by the supplied raw transcript; extraction into the cited evidence section is a separate outstanding step (Source facts, item 9).
- **Review history:** `thoughts/shared/reviews/2026-10-10_chore-agents-hardening-plan-review.md` (NEEDS_REVISION; sha256 `a41e22291211ae695294231398b55ee8b5d5d5d6326d39b70afe7430a6ad9cc1`; preserved at `c0e0b65`). It reviews the first proposal, `thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md` (sha256 `f2839927a1d48d5eaa1a50ed8d995a4af5c71932389e3b08122dcdf6eb0e908b`; preserved at `17a22f0`). Both stay unchanged. The first review is not an approval of this revision.
- **Active plan:** `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` at Revision 8, unchanged since `5064146`. Cited as plan:<line> in its current numbering.
- **Evidence:** `TestResults/chore-agents-hardening/evidence/phase-2.md`, read in bounded sections. It ends at ev:15089 at drafting time. Cited as ev:<line>.
- **Sources read:**
  - the eight `.claude/agents/*.md`;
  - `.claude/hooks/bash-allowlist.js`, `.claude/hooks/write-scope.js`;
  - `.claude/hooks/tests/write-scope.test.js` and the names in `.claude/hooks/tests/bash-allowlist.test.js`;
  - the hook wiring in `.claude/settings.json`;
  - `CLAUDE.md`, `docs/journal/`, `docs/roadmap.md`.

## Goal

Make Revision 8's remaining Phase 2 verification executable with the smallest coherent change, and correct the 14 findings of the review. Revision 8's separations stay:
- decision tests stay apart from live connections;
- uncalled negatives stay UNVERIFIED end to end;
- an allow record is no negative proof.

This proposal adds:
- **An order** that records what actually ran and fixes the remaining steps. Five profile-hook pairs, across three profiles, still lack a connection record: db-reviewer (one), tester (two) and explainer (two).
- **Approval and review-chain rules** that bind each approval to exact content.
- **The preliminary tester run:** an enumerated scope and a correlated explainer trigger.
- **Retention and retry rules,** so no fixed output path is overwritten.
- **A per-run correlation check,** given as parameters rather than copied identities.
- **Identity comparisons** that keep current state and historical state apart.
- **A coverage ledger** with one definition of behaviour coverage and exactly three live-observation categories.
- **Four write-scope block tests** (D1) and one human-run unmutated decision-test record.

Cost:
- seven plan-text edits and four tests;
- no change to hook code, settings, agent definitions or CLAUDE.md.

## Non-goals

- **No authority change.**
  - D2 and D3 are two Phase 2 scheduling accommodations that the human decided. They change no tool list, write scope, file name, header, command allowlist, verdict value or review-only role.
  - Nothing changes in `.claude/hooks/*.js`, `.claude/hooks/lib/`, `.claude/settings.json`, `.mcp.json`, any `.claude/agents/*.md`, any model or effort value, or `CLAUDE.md`.
- **No general reading that makes a role's usage text optional.** Outside D2 and D3, these keep their force:
  - an agent's `description`;
  - a CLAUDE.md pipeline position;
  - a plan's "not required".
- **No new mutation proof.**
  - The Behaviour mapping is unchanged. The four tests are "review only" (plan:3184).
  - No M2 proof is rerun.
- **No new live negative probe.** 16 original targets have no live block. 6 were requested and refused without a tool call (inconclusive), and 10 were never requested. All 16 stay UNVERIFIED end to end (plan:344).
- **No `LIVEPROBE` string in any test file** (C1.7). No out-of-call execution of the exact `LIVEPROBE` strings: that would be a new method needing its own decision.
- **No added tests beyond the four.** In particular, none is added to erase LIVEPROBE-marker differences or to claim byte-identical inputs.
- **No edit to existing records.**
  - No existing evidence record, As built section, research report, proposal or review is edited.
  - Nothing is reclassified (plan:442).
  - Gaps in version or identity history are stated, never filled by inference.
- **No automatic transitions.** A tester verdict is never the human's READY_TO_PUSH.
- **No change to Phases 3 and 4.**

## Touches schema? (yes/no — db-reviewer required if yes)

No. The data model does not require db-reviewer (plan:399). Human decision D2 nevertheless authorizes one genuine Phase 2 database-impact review, at R9.8. It must reach DB_APPROVED before the preliminary tester runs. No schema change is expected or manufactured.

## ADR needed? (title, or "no")

No. This is verification procedure, recorded in the plan.

## Decision status

| Item | Status |
|---|---|
| D1: four write-scope block tests | PROPOSED |
| D2: one Phase 2 db-review | HUMAN DECISION, 2026-10-10 |
| D3: one preliminary Phase 2 learning entry | HUMAN DECISION, 2026-10-10 |
| D4: linked incremental final code review | PROPOSED |
| D5: version and historical-evidence boundary | PROPOSED |
| E1-E7, the order R9.3-R9.12, all procedures below | PROPOSED |

D2 and D3 authorize the planning accommodations only. They do not authorize running db-reviewer or explainer now, implementing tests, applying Revision 9, or advancing the pipeline. No record of these decisions appears in the evidence file through ev:15089. The human has subsequently reported preserving them before the re-review under `## Human decisions D2 and D3 (2026-10-10)`.

## Completed history (records that exist)

| Record | Evidence | Agent / session | Time | HEAD | Claude Code version, as recorded |
|---|---|---|---|---|---|
| Preflight before the Phase 2 probe restart | ev:14070-14090 | — | — | `a68dd16` (ev:14080) | `claude --version` printed 2.1.292 (ev:14089). This precedes the restart and is not a session-start record. |
| P2.2 coder, two attempts | ev:14092-14233 | `a8d7ab08ba294c565` / `340a3876-cdcb-4d00-b7ea-9aa2e4731896` | 2e block 2026-10-07T23:26:01.366Z; 2f allow 23:26:01.979Z (ev:14153-14154) | `a68dd16` | 2.1.292, from the transcript (ev:14110, ev:14181) |
| P2.4 code-reviewer (dummy write; targets 1-2 not called) | ev:14253-14299 | `a49c1b7cd999418ca` / same session | Write 2026-10-08T12:38:22.588Z; allows 12:38:22.780Z and .795Z | `a68dd16` | not recorded. `code-reviewer.md` sha256 `a3ec84be…` (ev:14263). |
| P2.6 code-reviewer LSP | ev:14315-14349 | `a49c1b7cd999418ca` (same transcript) | 2026-10-08T12:47Z-13:03Z | `a68dd16` | not recorded |
| P2.6 researcher Microsoft Learn | ev:14350-14383 | `a350ad26febf21748` | 2026-10-08T13:16:51Z | `a68dd16` | not recorded |
| Revision 8 commit | ev:14511-14567 | — | — | `5064146`. `a68dd16..5064146` is the plan only (ev:14552); `ce5e7cf..5064146` is empty for `.claude .mcp.json CLAUDE.md` (ev:14560); status empty (ev:14564). | — |
| R8 researcher Write connection | ev:14568-14596 | `a00e30037b86b0fae` | allows 2026-10-08T23:12:58.845Z and .851Z (ev:14585-14586) | `5064146` | not recorded |
| Research preserved | ev:14598-14650 | — | 2026-10-09T02:22:09+03:00 | `561624241a31754d3c028e32b4c32d1a5ab83331`. The diff is the research only (ev:14640); status empty (ev:14648). | — |
| R8 planner Write connection | ev:14653-14775 | `ae5c9090f4055b6e2` | allows 2026-10-08T23:51:36.565Z and .566Z; recorded 2026-10-10T16:08:30.255Z | `5616242` | not recorded. The record extracts no `version` field. |
| First proposal preserved | ev:14777-14833 | — | 2026-10-10T19:10:33+03:00 | `17a22f0a264e3b0cef49580f38b3d0b4ccf43dee`. The diff is the proposal only (ev:14823); status empty (ev:14831). | — |
| R8 plan-reviewer Write connection (first review, NEEDS_REVISION) | ev:14836-14974 | `a0917139b31a99214` | Write 2026-10-10T16:35:40.503Z; allows .725Z; recorded 17:00:24.075Z | `17a22f0` | 2.1.293, from the transcript (ev:14840) |
| First review preserved | ev:14976-15032 | — | 2026-10-10T20:01:13+03:00 | `c0e0b65dee26002974b0619ad5bc80ce9c5f6b0e`. The diff is the review only (ev:15022); status empty (ev:15030). | — |
| Current identity record (R9.1 baseline) | ev:15034-15089 | — | 2026-10-10T20:03:14+03:00 | `c0e0b65`, status empty | none recorded (no `claude --version`) |

Notes on the history:
- **Actual order.** It ran as R9.2 (planner), then R9.3 iteration 1 (plan review), then R9.1 (identity). No identity record preceded the planner or plan-reviewer record. The first proposal's dependency "R9.2 needs R9.1" and its pre-commit status expectation describe an order that did not run, and both are withdrawn.
- **First review.** Range `561624241a31754d3c028e32b4c32d1a5ab83331..17a22f0a264e3b0cef49580f38b3d0b4ccf43dee`, Head `17a22f0a264e3b0cef49580f38b3d0b4ccf43dee`, from its header. It is review history of the first proposal.
- **Identity at c0e0b65 (ev:15054-15089).**
  - `ce5e7cf..HEAD` and `HEAD..worktree` are both empty for `.claude .mcp.json CLAUDE.md`.
  - 18 sha256 values are recorded: CLAUDE.md, settings, MCP, the eight agents, the five hook files, `.claude/settings.local.json` and the user settings.
  - `.claude/settings.local.json` equals `b1db1176…`, the H2.1 value (ev:181).
  - `code-reviewer.md` equals `a3ec84be…`, the P2.4 value (ev:14263).
  - The user-settings hash `767bac15…` is context. No earlier value is compared.
  - Equality at recorded points is not continuous immutability. Current hashes do not supply historical ones.
- **Historical tracked state.** It rests on the commit chain above and on each record's status check.
- **Process history.** No record shows when a process running 2.1.293 started. A continuing session id does not show one continuous process, version or configuration.
- **Caller overrides.** The main-session Agent inputs of the researcher, planner and plan-reviewer runs have not been extracted. The plan-reviewer record states this itself (ev:14973).

## Source facts verified for this revision

1. **Write-scope tests by scope.**
   - `write-scope.test.js` "Other scopes" (`:121-129`) holds two allow tests, for `docs/journal` and `TestResults`.
   - The `thoughts/shared/research` and `thoughts/shared/plans` scopes have no test at all.
   - The file holds 13 tests, and the five test files hold 207 (the review's count). After E6 they hold 17 and 211.
2. **5b.**
   - Test `bash-allowlist: blocks dotnet format without --verify-no-changes` (`bash-allowlist.test.js:149-151`): input `dotnet format`, reason `command is not in the tester allowlist`.
   - Against the closest entry, `dotnet format --verify-no-changes` (`bash-allowlist.js:46`), the input fails the length condition of `startsWithTokens` (`:97-99`).
   - The target `dotnet format --include src/LIVEPROBE-5b.cs` passes the length condition and fails the third-token comparison.
   - Both reach the no-matching-entry block (`:191-192`).
3. **5d.**
   - Test `bash-allowlist: blocks dotnet build for the explainer` (`:304-306`): input `dotnet build`. It fails the length condition against `dotnet list package` (`bash-allowlist.js:56`).
   - The target `dotnet build -p:Probe=LIVEPROBE-5d` fails the second-token comparison.
   - Both reach `:191-192` with `command is not in the explainer allowlist`.
4. **5a and 5f.**
   - 5a: test `bash-allowlist: blocks echo x > src/a.cs` (`:133`) and the target both reach the redirection pre-check reason (`bash-allowlist.js:91-92`), blocked at `:171-174`.
   - 5f: test `bash-allowlist: blocks dotnet build -p:PreBuildEvent=x` (`:248-250`) and the target both reach `checkDotnetArgs` (`:141`), blocked at `:194-196`. Only the reason's token differs.
5. **Write-scope decision site.** The four E6 tests and the eight write-scope targets reach `write-scope.js:23-24` (verified by the review).
6. **Hooks per tool.** `.claude/settings.json:151-157` wires `path-guard` to `Edit|Write|MultiEdit|NotebookEdit` for every agent. A Bash call meets only the agent's own Bash hook: `bash-allowlist:<profile>` or `coder-bash-guard`.
7. **Runtime tools.**
   - The runtime tool list of coder agent `a8d7ab08ba294c565`, recorded under P2.3 (ev:14247), is Read, Write, Edit, Bash, Glob, Grep, GetTask and SubagentHandback. Two of these, GetTask and SubagentHandback, are not in its frontmatter.
   - plan:3237 names ToolSearch and SubagentHandback as built-ins of a background subagent.
8. **Transcript version field.** Rows carry a top-level `version` field (ev:14110, ev:14840). This settles, for those transcripts, the review's question whether the field exists.
9. **Planner version (Codex inspection report).** Codex reports version set `2.1.292` in the supplied earlier planner transcript (r4 provenance). This is not yet a human-run mechanical extraction in phase-2.md. The cited planner evidence section did not extract that field; R9.4 records it before D5.
10. **Journal practice.**
    - `docs/journal/` holds `0001`-`0008` and `note-2026-09-18-configuration-predicate-guard.md`.
    - No entry begins `0009-` at drafting time. This comes from a file listing, not a runtime record, and R9.10 checks it again.
    - Headings read `# PR 000N — …` with the journal number. For example, `0005` is roadmap row 5a, merged as GitHub #6 (docs/roadmap.md:54).
    - The unnumbered chore rows (docs/roadmap.md:62-64) have no entry; roadmap row 1 is chore(solution) and has entry 0001.
11. **Review file names.** The `[-rN]` suffix marks re-reviews (plan-reviewer.md:45, code-reviewer.md:46, db-reviewer.md:54). This branch numbers the first re-review `-r2` (plan:3: `2026-09-25_chore-agents-plan-review-r2.md`, `2026-09-29_chore-agents-code-review-phase-1-r2.md`).
12. **Routing.**
    - CLAUDE.md:111-112: NEEDS_REVISION goes to the planner; NEEDS_FIXES and DB_NEEDS_REVISION go to the coder.
    - Probe protocol step 1 (plan:420): a code-review NEEDS_REVISION sends the phase back to the coder.
    - This proposal adds an evaluation step before routing. It does not replace either rule.
13. **Tester.**
    - tester.md:3 runs it after APPROVED code review and, "if applicable", db review.
    - tester.md:32 and :36 make it read and run the plan's validation commands.
    - tester.md:41-49 fix the verdict file and its three-line header.

## Decisions

### D1 — four write-scope block tests (PROPOSED)

**Conflict.** Revision 8 forbids "an assumed equivalence" (plan:3279). It routes missing decision coverage through the approved-plan/code/test workflow (plan:346). Four write-scope arguments have no block test:
- `thoughts/shared/research`, `thoughts/shared/plans`, `TestResults` and `docs/journal`;
- every block test uses `thoughts/shared/reviews` (`write-scope.test.js:95-119`).

Existing coverage of the other four scopes differs:
- `TestResults` and `docs/journal` have one allow test each (`:123-129`);
- `thoughts/shared/research` and `thoughts/shared/plans` have no test at all. Their allow path rests only on the live allow records of the researcher (ev:14586) and planner (ev:14772) connections. The ledger says so (E5, second table).

**Alternatives.**
- **A, PROPOSED:** one block test per argument (E6), review only.
  - The suite grows from 207 to 211 tests.
  - No restart: no file Claude Code loads changes.
- **B:** no tests, accepting the scope-generic `decide()` as coverage.
  - This is the forbidden assumed equivalence, and it needs an amendment of plan:346.
  - This proposal holds no text for that amendment, so choosing B returns to R9.3 for a revision. Not recommended.

**No other test is proposed.** For 2a-2e, the eight write-scope targets, 5a, 5b, 5d and 5f, the ledger records behaviour coverage under the definition in E1. It also states each input, reason-token and predicate-path difference:
- **5b:** the test input fails `startsWithTokens`' length condition against `dotnet format --verify-no-changes`, while the target fails its third-token comparison.
- **5d:** the test input fails the length condition against `dotnet list package`, while the target fails its second-token comparison.
- Both reach the no-matching-entry block. That does not show that every predicate subcondition was exercised by the same test.
- **5f:** the reason carries a different token.
- **5a:** only the input bytes differ.

No test is added to erase these differences or the LIVEPROBE marker.

### D2 — one Phase 2 db-review (HUMAN DECISION, 2026-10-10)

Decision as recorded by the caller: "Authorize the proposed genuine Phase 2 database-impact review, despite this phase not normally requiring a schema review. It follows an APPROVED code review and precedes the preliminary tester. No verdict is prescribed. All db-reviewer tool, write-scope and review-only restrictions remain."

Execution, PROPOSED as part of the order (E2, R9.8):
- **When:** after the latest preliminary code review is APPROVED, and only review files have changed since its Head.
- **Task:** determine whether the Phase 2 change range affects schema, EF mappings or configuration, migrations, queries or database behaviour, and report any findings with file:line. Nothing is manufactured.
- **Caller values:**
  - plan path and `phase 2`;
  - branch slug;
  - Range `84ebd9072f3a06783d71f4af4a7d95f4644c9361..<HEAD>` and Head `<HEAD>`;
  - the human-recorded changed-path list.
- **File:** `thoughts/shared/reviews/<date>_chore-agents-hardening-db-review-phase2[-rN].md` (db-reviewer.md:54).
- **Gate:** DB_APPROVED is required before the preliminary tester, for the same implementation state as the APPROVED code review.
- **DB_NEEDS_REVISION:**
  1. The human evaluates each finding.
  2. A confirmed finding that needs a code change goes to a coder turn, then a code re-review, then a db re-review (`-rN`).
  3. A finding resolved by documented analysis goes to a db re-review with that analysis as caller input.
  - No model claim goes to the coder as an established defect without evaluation.
- **A declined write:**
  - the refusal is recorded;
  - the connection stays pending;
  - the human returns to D2;
  - no other identity writes in its place.
- **Unchanged:**
  - db-reviewer.md:14 (only reviews, never edits, never runs commands);
  - db-reviewer.md:48-51 (no Bash, so runtime claims are UNVERIFIED);
  - the file name, the five-line header and the verdict values (:54-67, :78);
  - CLAUDE.md:100-101.
- **Scope of the accommodation:** it reads db-reviewer.md:3 and CLAUDE.md:91 as non-forbidding for this one human-authorized review only. Outside it, both apply as written.

### D3 — one preliminary Phase 2 learning entry (HUMAN DECISION, 2026-10-10)

Decision recorded under `## Human decisions D2 and D3 (2026-10-10)`: "D3 — Authorize a preliminary Phase 2 learning entry at docs/journal/0009-chore-agents-phase-2-guards.md, subject to the applicable preliminary tester READY_TO_PUSH prerequisite, a free filename and numeric prefix, and separate human authorization when the explainer run is reached. The human accepts that this consumes journal number 0009; a later roadmap PR 7 journal entry takes the next available journal number. Roadmap and GitHub numbering do not change. The entry must disclose that the final Phase 2 review, human Validation and the hardening PR are still pending."

The tradeoff, stated for the record:
- **Repository practice.**
  - Numbered entries `0001`-`0008` follow merged roadmap rows 1, 2, 3, 4, 5a, 5b, 6a and 6b, which merged as GitHub #1, #2, #3, #4, #6, #8, #11 and #12.
  - Unnumbered chore rows have no numbered entry; roadmap row 1 is a numbered chore(solution) PR with entry 0001.
  - The one entry that is not a PR is a `note-` file.
- **0009 would be the first numbered entry** for an unnumbered chore row and for an unfinished PR phase; it is not the first entry whose PR type is chore.
- **Consequences of 0009.**
  - A later journal for roadmap PR 7 takes the next free number: `0010` if nothing intervenes.
  - Neither GitHub PR #9 nor a completed hardening PR is implied.
  - The heading follows explainer.md:42 and reads `# PR 0009 — <title>`, as existing headings carry journal numbers.
  - The entry's text states what 0009 means.

Execution, PROPOSED as part of the order (E2, R9.10):
- **Trigger:**
  - the latest preliminary tester run's READY_TO_PUSH, correlated under E2's "Explainer trigger";
  - plus the human's separate run authorization.
  - The final human READY_TO_PUSH is not the trigger.
- **Input:**
  - the active plan's path;
  - journal number `0009`;
  - the exact path `docs/journal/0009-chore-agents-phase-2-guards.md`.

  The explainer does not invent the slug.
- **Collision checks before the run:**
  - the exact path does not exist;
  - no entry begins with `0009-`.

  Never overwrite. One unique numeric prefix per entry; no number is reused with another slug. A later PR-end entry, which is the human's call (plan:3805), is optional and takes a new free number.
- **Disclosure the prompt requires:**
  - the entry covers the implemented Phase 2 work and the preliminary tester run (its Head);
  - Phase 2's final code review, the human's Validation and READY_TO_PUSH decision, Phases 3-4 and the hardening PR are pending;
  - `0009` is a journal sequence number.
- **Acceptance:** the human checks the disclosure as a human observation before committing.
- **An entry that lacks the disclosure or overstates acceptance:**
  - it is not committed;
  - it is retained by move to `TestResults/chore-agents-hardening/validation/journal-0009-attempt-<n>.md`;
  - the human returns to D3.
- **Never the fallback:**
  - no silent rename to a `note-` file;
  - no waiver of the explainer connection.
  If the human revokes the numbering accommodation, the work returns to D3.

### D4 — linked incremental final code review (PROPOSED)

- **A, PROPOSED.** The final Phase 2 code review is link 2 of a two-link chain.
  - **Link 1** is the last applicable APPROVED preliminary code review. "Applicable" means:
    - it is the latest R9.7 review;
    - its verdict is APPROVED;
    - its Range starts at `84ebd9072f3a06783d71f4af4a7d95f4644c9361`;
    - a DB_APPROVED R9.8 review covers the same implementation state.

    Its Head is H_A. Link 1 is never merely "the last review".
  - **Link 2:**
    - Range `H_A..<As built commit>`, Head the As built commit;
    - it reviews every change in that range;
    - it considers Phase 2's complete requirements, including Revision 9;
    - it states whether the two links together cover every Phase 2 change, even where unchanged source is not reread in full.
  - **Reading of plan:430.** Revision 8 says preliminary reviews do not "replace the review … of the final evidence-bearing state". Under A, link 2 is that review. Link 1 does not replace it; it supplies acknowledged prior coverage that link 2 confirms.
  - **When the links do not cover every change,** the final review is widened to the full range (B). Coverage is never inferred from file names.
  - The Phase 1 re-review precedent cited by the first proposal is withdrawn. It is not needed, and the review showed it does not match.
- **B.** A full-range final review: `84ebd9072f3a06783d71f4af4a7d95f4644c9361..<As built commit>`. It needs no reading of plan:430. Its cost is one full re-review at max effort.

The human may choose A or B at R9.4. E1 gives both alternative bullets under the same label; E2 and Phase 5 give the corresponding prechecks, caller values and final records. Both options are specified without any further unreviewed edit.

### D5 — version and historical-evidence boundary (PROPOSED)

**Proposal:**
- **Versions on every record.** Every record keeps the actual Claude Code version that its transcript records, or states "not recorded".
- **Earlier observations stay as recorded:**
  - the P2.2 coder ran under 2.1.292;
  - the R8 plan-reviewer ran under 2.1.293;
  - the P2.4 and P2.6 code-reviewer, the P2.6 researcher, the R8 researcher and the R8 planner have no recorded version until R9.4's version records.
- **What an update means.** An update neither invalidates an earlier observation nor proves its behaviour under a newer version.
- **Mixed versions at closure.** For Phase 2 closure, the human accepts this mixed-version evidence with its stated limitation. Nothing asserts that all profiles were verified under one version.
- **Process starts are recorded truthfully.**
  - When a Claude Code process starts or resumes, the human records a dated observation: the time, `claude --version`, and the session id, marked new or resumed.
  - A resumed session id does not show that one process, version or configuration ran continuously.
  - No session-start record is invented from a preflight timestamp.
  - If the human knows when the 2.1.293 process started, a dated observation adds it. Otherwise the gap stays stated.
- **The restart rule is unchanged.** Revision 8's restart and re-collection rule for relevant executable configuration files stays in force (E2, "Identity comparison"). A Claude Code update is not such a file change, and by itself triggers no re-collection.

**Alternative B: all-profile verification on one version.** This is a different decision, because it needs new observations for every affected profile, including a new live coder probe. This proposal holds no procedure for that, so choosing B returns to R9.3. Not recommended.

## Why the order is non-circular

**Revision 8's cycle:**
1. The final review and tester need the As built commit (plan:416-418).
2. As built needs every connection (plan:346, :430).
3. The explainer's connection needs a READY_TO_PUSH tester (plan:430).

Revision 8 cut the cycle with a preliminary review and validation.

**How each remaining point is cut:**
- **Fixed tester path:** report retention.
- **Journal commit placement:** R9.10, before As built and before the final review.
- **Journal number:** D3.
- **Unmutated results for the ledger:** R9.6.
- **Tasks for plan-reviewer and db-reviewer:** R9.3, already run, and R9.8.
- **The tester's judgment re-forming the cycle:** the scope enumeration in E2 states what is later by design (finding 3).

| Step | Needs | Produces |
|---|---|---|
| R9.2 (done) | — | planner connection; first proposal commit |
| R9.3 iteration 1 (done) | R9.2 | review NEEDS_REVISION; review commit |
| R9.1 (done) | — | identity baseline at `c0e0b65` |
| D2, D3 (decided) | — | human decisions; record to preserve |
| R9.3 iteration ≥ 2 | D2/D3 record | replacement proposal(s), commit(s); re-review(s), commit(s) |
| R9.4 | the latest review of the applied proposal is APPROVED; version records; human decision | Revision 9 plan commit |
| R9.5 | R9.4 commit; D1-A | test commit; identity comparison |
| R9.6 | R9.5 | unmutated decision-test record |
| R9.7 | R9.6 | preliminary code review(s), each committed |
| R9.8 | latest R9.7 APPROVED | db review(s), each committed |
| R9.9 | latest R9.7 APPROVED and latest R9.8 DB_APPROVED, same implementation state | tester connections; retained verdict |
| R9.10 | explainer trigger plus human authorization | explainer connections; journal commit |
| R9.11 | R9.1-R9.10 | ledger, assessments, As built commit |
| R9.12 | R9.11 | final review, final tester, Validation, READY_TO_PUSH |

## Proposed wording — edit set E1-E7 for the active plan

The edit set rules:
- **Anchors:** each must match exactly once in the plan (verified by the review at plan:350, :430, :444, :3170, :3292, :3298 and :3347).
- **Placeholders:** `<<...>>` marks a value filled at R9.4 from a record. Generic placeholders such as `<HEAD>` or `<date>` are plan text and stay.
- **Allowed changes:** the edit set applies each block unchanged, except two kinds:
  - filled `<<...>>` placeholders;
  - the B text of D4, where the human chose B.
- **Anything else** is new semantics and returns to R9.3.
- **Mechanical check:** no `<<` occurs in E1-E7 except in those placeholders.

### E1 — new section `## Revision 9`

Insert after the paragraph that ends "No new permission to bypass a role rule is introduced." (plan:350), before the `---` line (plan:352).

```text
## Revision 9

Human decisions: 2026-10-10, D2 and D3 (below); <<DATE>>, <<DECISION: the accepted proposal revision and the choices for D1, D4 and D5>>. Proposal: <<PROPOSAL PATH>> (sha256 <<PROPOSAL SHA256>>, preserved at <<PROPOSAL COMMIT>>). Plan review: <<APPROVING REVIEW PATH>> (APPROVED, Head <<PROPOSAL COMMIT>>). Review history: thoughts/shared/reviews/2026-10-10_chore-agents-hardening-plan-review.md (NEEDS_REVISION, on thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md)<<LATER REVIEW HISTORY>>. Research (a model report): thoughts/shared/research/2026-10-09_phase2-r8-coverage.md.

This revision makes Revision 8's remaining Phase 2 verification executable. It adds four write-scope block tests and fixes an execution order. It changes no hook code, permission, tool list, write scope, agent definition, model or effort value, CLAUDE.md, evidence record or As built section, and it closes no probe.

Phase 2 accommodations (human decisions of 2026-10-10; exactly two):
- D2, db-review. One genuine, human-authorized review of the Phase 2 change range for database impact (schema, EF mappings or configuration, migrations, queries, database behaviour), although "Touches schema?" is No. It runs after an APPROVED Phase 2 code review and before the preliminary tester, prescribes no verdict, and must reach DB_APPROVED before the tester runs. db-reviewer.md's tools, write scope, file name, header, verdict values, command prohibition and review-only role are unchanged. A declined write is recorded and leaves the connection pending; no other identity writes in its place.
- D3, learning entry. One preliminary Phase 2 entry at docs/journal/0009-chore-agents-phase-2-guards.md, written by the explainer only after the trigger in Probe protocol step 3, "Revision 9 execution order" (R9.10), and a separate human authorization. It consumes journal number 0009; a later roadmap PR 7 entry takes the next free number; roadmap and GitHub numbering do not change. The entry states that it covers the implemented Phase 2 work and the preliminary tester run, and that Phase 2's final code review, the human's Validation and READY_TO_PUSH decision, Phases 3-4 and the hardening PR are pending. Every journal entry has its own numeric prefix; no number is reused.

Every other role and pipeline condition keeps its force: coder.md:3; tester.md:3; explainer.md:3 outside D3; db-reviewer.md:3 outside D2; CLAUDE.md:90, :92 and :100-101 unchanged, and :91 outside D2; CLAUDE.md:104-105 outside D3; and "db-reviewer is not required" (Touches schema?) for every purpose other than D2. Both accommodations are limited scheduling accommodations in the sense of Revision 8's collection order, not permission to relax a file, command, tool or verdict restriction.

What changes:
- Count. P2.2, P2.4 and P2.5 retain 17 original negative targets: 5 coder (2a-2e), 8 write-scope (the seven `src/LIVEPROBE-4-<agent>.txt` targets and `thoughts/shared/reviews-probe/LIVEPROBE-4-code-reviewer.md`) and 4 allowlist (5a, 5b, 5f, 5d). Their live observation falls into exactly three categories: exact live block (2e); requested and refused without a tool call, inconclusive (2a-2d and the two code-reviewer targets: 6); never requested (the six other `src/LIVEPROBE-4-<agent>.txt` targets and 5a, 5b, 5f, 5d: 10). All 16 without a live block stay UNVERIFIED end to end. A missing log line does not prove that no call occurred. Existing records are not rewritten.
- Coverage. Byte-identical decision coverage of an original target is impossible, because no test file may contain `LIVEPROBE` (C1.7; Validation command 3); the ledger records "none (C1.7)", a structural limit, not a gap. A named test gives behaviour coverage of a target when it runs the same hook script with the same profile or scope argument (coder-bash-guard takes none), its input reaches the same final decision site (the same `block` call, by file:line), and it asserts that site's reason. The ledger states every input difference, every reason-token difference and every predicate-path difference before that site. For 5b and 5d the test inputs fail `startsWithTokens` (bash-allowlist.js:97-99) on its length condition against the closest entry, while the targets fail a token comparison (the third token against `dotnet format --verify-no-changes`; the second against `dotnet list package`); both reach the no-matching-entry block (bash-allowlist.js:191-192), which does not show that every predicate subcondition was exercised by the same test. Tests to add gains one block test for each of the four write-scope arguments that had none, review only. No mutation proof is added.
- Results. The ledger's unmutated test results come from one human-run record of Validation command 2's command at the test commit (R9.6). The final Validation run checks them again at the validated HEAD.
- Order. Probe protocol step 3, "Revision 9 execution order (Phase 2 only)", fixes each remaining step (genuine task, caller values, file, record, retention, commit), the approval and review chain, the preliminary tester's scope, and the final review and validation.
- The concurrency sentence in Probe protocol step 3 is made exact. P2.4 points to the order. A Coverage ledger item follows P2.5. Validation "Order" names the added commits.

Settled readings:
- READY_TO_PUSH. In the collection order the explainer's trigger is the latest preliminary tester run's report, correlated as R9.10 states; it is a model report and serves only as the trigger. The human's authorization of the entry is a separate human decision. Neither is Phase 2's READY_TO_PUSH, which stays the human's decision on the final validation log. This settles Revision 6's open item for Phase 2's collection order only.
- Fixed tester path. `TestResults/<branch-slug>/tester-verdict.md` has no -rN. Before every tester run the path is free; an earlier report is recorded and moved to a retained name first. No tester run overwrites a report.
- Reuse without approval. A connection record shows that the named agent made the correlated call, that the named hook logged its decision for that call, and what the tool returned. It approves nothing the agent wrote. Existing records keep their own commit, session, time and recorded version.
- Final review procedure. The final Phase 2 code review is link 2 of a two-link chain. Link 1 is the last applicable APPROVED preliminary code review: the latest R9.7 review, APPROVED, with Range 84ebd9072f3a06783d71f4af4a7d95f4644c9361..H_A and a DB_APPROVED R9.8 review at the same implementation state. Link 2 reviews every change H_A..<As built commit>, with the As built commit as Head, considers Phase 2's complete requirements, and states whether the two links cover every Phase 2 change. Revision 8's "replace the review … of the final evidence-bearing state" reads: link 2 is that review; link 1 does not replace it and supplies only acknowledged prior coverage that link 2 confirms. If the links cannot cover every change, the final review is widened to the full range.
- Versions. Every record states the Claude Code version its transcript records, or "not recorded". Records made under different versions are accepted together as mixed-version evidence with this limit: an observation under one version is no evidence of behaviour under another, and an update neither invalidates an earlier observation nor triggers its re-collection. Process starts and resumes are recorded as dated human observations when they occur; a resumed session id does not show that one process, version or configuration ran continuously. The restart and re-collection rule for relevant executable configuration files is unchanged.
- As built. The Revision 8 limitation and every Revision 9 record reach As built only through the evidence file; the human appends them there and Probe protocol step 1.7 copies the file mechanically.

The test commit changes no file Claude Code loads, so it needs no restart and no rerun of an existing observation.

Unchanged: Revision 8's accepted limit, closure conditions and Unchanged list; Phase 1; the M2 records and assessments; the conditions of P2.1, P2.3, P2.6, P2.7 and P2.8; Phases 3 and 4.
```

D4-B replacement text for the "Final review procedure" bullet, used only if the human chooses B:

```text
- Final review procedure. Under D4-B, the final Phase 2 code review covers the full range 84ebd9072f3a06783d71f4af4a7d95f4644c9361..<As built commit>, with the As built commit as Head. The caller supplies that complete changed-path list, the applicable DB_APPROVED review and Phase 2 requirements; it requests review of the whole range, without relying on link 1 for coverage. The human checks the report destination is free and records branch, clean status and the As built HEAD before the run. The final READY_TO_PUSH record identifies this full-range review and the DB_APPROVED review, not a two-link chain.
```

### E2 — new paragraphs in Probe protocol step 3

Placement:
- **Where:** after the paragraph that ends "All retained report paths and commits are accounted for; no dummy report is a substitute." (plan:430), and before "**Record types (Revision 6).**" (plan:432).
- **Indentation:** three spaces, like plan:430.

```text
   **Revision 9 execution order (Phase 2 only).** The human advances one step at a time. No step starts before the previous one is recorded, and no agent runs without the human's request for that step. Each agent task is genuine work in its role: its prompt states the task, the stage and the caller-supplied values, carries the probe preamble's delegation wording (no model parameter; "For the task, call only ..."; SubagentHandback for report delivery only), and prescribes no verdict. Before each agent task the human records the date, branch, full HEAD and `git --no-optional-locks status --porcelain --untracked-files=all`. For a new-report task, also check `test ! -e <target path>`; after it use the connection correlation check below and preserve the report as the step says. R9.5 edits an existing tracked test file, creates no report and uses the separate coder record specified in R9.5 instead of the new-report absence, single-Write, hook-pair and untracked-status checks. Every review file is committed alone, whatever its verdict; no verdict file is edited, overwritten or deleted.

   Completed before this paragraph existed, in the order they ran (Revision 8 rules):
   - R9.2 Planner: thoughts/shared/plans/2026-10-09_phase2-r8-correction-proposal.md; "## R8 planner — Write connection records"; preserved alone at 17a22f0a264e3b0cef49580f38b3d0b4ccf43dee.
   - R9.3, iteration 1: thoughts/shared/reviews/2026-10-10_chore-agents-hardening-plan-review.md, NEEDS_REVISION, Range 561624241a31754d3c028e32b4c32d1a5ab83331..17a22f0a264e3b0cef49580f38b3d0b4ccf43dee; "## R8 plan-reviewer — Write connection records"; preserved alone at c0e0b65dee26002974b0619ad5bc80ce9c5f6b0e.
   - R9.1 Identity baseline: "## Current identity comparison after R8 plan review", at c0e0b65dee26002974b0619ad5bc80ce9c5f6b0e, made after the two records above.
   - The human's decisions D2 and D3 of 2026-10-10 (Revision 9, "Phase 2 accommodations").
   - <<R9.3 LATER ITERATIONS: for each, the proposal path and preservation commit, and the re-review path, Range, Head and verdict>>.
   - R9.4: the version records of earlier connections, the human's decision, and this revision's plan-only commit.

   Remaining steps:
   - R9.5 Coder turn. Input: this plan's path and phase 2, limited to the four Revision 9 items under Tests to add, write-scope.test.js, "Other scopes". The caller says "For the task, call only Read, Glob, Grep, Edit, Write and Bash; SubagentHandback, if available, is for report delivery only", and quotes these four commands and their conditions: `node --test .claude/hooks/tests/write-scope.test.js` (exit 0; all names pass, including the four additions); `node --test .claude/hooks/tests/*.test.js` (exit 0 and Validation command 2's complete condition); `dotnet build -warnaserror` (exit 0, 0 Warning(s), 0 Error(s)); `git status --short` (exactly ` M .claude/hooks/tests/write-scope.test.js`). The prompt ends with "do NOT run any other agent, do NOT commit, wait for me". Before the turn: empty status, `test -f .claude/hooks/tests/write-scope.test.js` (exit 0) and `git diff --exit-code HEAD -- .claude/hooks/tests/write-scope.test.js` (exit 0). No `test ! -e` applies to this existing file. This is an implementation record, not a new C2.2 connection: the coder connection remains P2.2. Record identity, transcript/meta hashes, version, model/effort, the tool-call list, and every Edit/Write/Bash input and matching result. Match each Edit/Write to path-guard and each Bash call to coder-bash-guard by agent, session, target and call/result window; no write-scope or bash-allowlist hook is expected for coder. List actual decisions or missing records; an error, unexpected block or missing expected record is assessed, never silently passed. Multiple edits are allowed; no report Write or exactly-one-Write condition applies. Post-status is exactly ` M .claude/hooks/tests/write-scope.test.js`. After PHASE_COMPLETE and successful validation the human runs /commit for the test file alone and records an identity comparison. No restart follows: no file Claude Code loads changes.
   - R9.6 Unmutated decision-test record (below).
   - R9.7 Preliminary code review. Caller values: this plan's path; phase 2; branch slug chore-agents-hardening; Range 84ebd9072f3a06783d71f4af4a7d95f4644c9361..<HEAD>; Head <HEAD>; the human-recorded output of `git diff --name-only 84ebd9072f3a06783d71f4af4a7d95f4644c9361 <HEAD>`; for a re-review, the earlier review files and the human's evaluation records. File: thoughts/shared/reviews/<date>_chore-agents-hardening-code-review-phase2[-rN].md. Every preliminary review and re-review uses this full Range. The C2.2 code-reviewer row cites this record beside the P2.4 observation; closure does not depend on it.
   - R9.8 Phase 2 db-review (decision D2), only when the latest R9.7 review is APPROVED and `git diff --name-only <its Head> HEAD` prints only paths under thoughts/shared/reviews/. Task: determine whether the Phase 2 changes affect schema, EF mappings or configuration, migrations, queries or database behaviour, and report findings with file:line. Caller values as for R9.7, at the then-current HEAD. File: thoughts/shared/reviews/<date>_chore-agents-hardening-db-review-phase2[-rN].md. A declined write is recorded; the connection stays pending and the human returns to D2.
   - R9.9 Preliminary tester, only when the latest R9.7 review is APPROVED, the latest R9.8 review is DB_APPROVED, and for each `git diff --name-only <its Head> HEAD` prints only paths under thoughts/shared/reviews/. Scope: "Preliminary tester scope" below. After the run: the correlation check, then report retention. A NEEDS_FIXES is examined as step 1.11 says: a confirmed claim enters the correction loop; if every claim is refuted, the human records the refutations and may authorize a new run whose prompt cites them.
   - R9.10 Explainer (decision D3), only under "Explainer trigger" below. Input: this plan's path; journal number 0009; exact path docs/journal/0009-chore-agents-phase-2-guards.md. The prompt states that the entry covers the implemented Phase 2 work and the preliminary tester run at its Head; that Phase 2's final code review, the human's Validation and READY_TO_PUSH decision, Phases 3 and 4, and the hardening PR are pending; and that 0009 is a journal sequence number, not a GitHub PR or roadmap number. P2.5's `git log --oneline -3 --grep=LIVEPROBE-5e` is part of the task, as one Bash call. Before the run, `test ! -e docs/journal/0009-chore-agents-phase-2-guards.md` exits 0 and no entry in docs/journal/ begins with `0009-`. After it: the correlation check; the Write's target is exactly that path; and the human records, as a human observation, whether the entry carries the statements above. If it does, the human commits the entry alone. If not, the entry is not committed: the human records it, retains it as TestResults/chore-agents-hardening/validation/journal-0009-attempt-<n>.md by the report-retention procedure, and returns to D3. A declined write leaves the connection pending; no note-file entry replaces it.
   - R9.11 Ledger and As built. The human records any version record still missing and an identity comparison, then appends to the evidence file: the Coverage ledger and C2.2 table (the item after P2.5); the Phase 2 records and assessments not yet recorded (P2.1 for every profile, C2.1, C2.3, C2.4, and the assessments of P2.2-P2.8); step 4's cleanup checks; the separate human assessment that accepts Revision 8's residual limit for exactly the 16 rows without a live block; and the limitation text for As built. Then steps 1.6-1.8: cleanup, the mechanical As built copy verified with `diff`, and the As built commit.
   - R9.12 Final review and validation on the As built commit: the final code review (Revision 9, "Final review procedure"); its review-file commit; the tester on that commit ("Final tester scope" below); the human's Validation run; the human's READY_TO_PUSH decision; and at most one later doc commit that adds only the validation record (Validation, "Order"). Before the final tester, `git diff --name-only <the DB_APPROVED review's Head> HEAD` prints only paths under thoughts/shared/reviews/, docs/journal/0009-chore-agents-phase-2-guards.md and this plan; any other path needs an R9.8 re-review first. A correction after the As built commit returns the phase to the correction loop; the order resumes at the earliest step the correction invalidates, and the added evidence reaches As built by a further mechanical copy that only appends.

   **Final-review option values (R9.12).** The human records which D4 option applies. Under A, before the review check that `git diff --name-only <H_A> <As built commit>` lists only review paths, the 0009 journal entry and this plan; otherwise obtain the newer applicable preliminary approvals through the correction loop. Supply Range H_A..<As built commit>, Head the As built commit, link 1's path/Range/Head/APPROVED verdict, the DB_APPROVED review and the changed-path list; request every change in the range and confirmation that the two reviews cover all Phase 2 changes. Under B, record `git diff --name-only 84ebd9072f3a06783d71f4af4a7d95f4644c9361 <As built commit>` as the full changed-path list (not a review-path-only test); supply that full Range and Head, the applicable DB_APPROVED review and Phase 2 requirements; request review of the entire range with no reliance on link 1 for coverage. Under either option, the report path must be free and branch, full HEAD and clean status are recorded before the run. The final approval must cover the selected Range at the As built Head. In the final READY_TO_PUSH record, A names both links and B names the full-range final review; both name the applicable DB_APPROVED review. The final tester and other final validation gates apply equally to A and B.

   **Approval and review chain (Revision 9).**
   - Plan review. A plan review approves only the exact proposal file named in its Scope line, at the commit that preserved that file. A revised proposal is a new file, preserved alone; its review is a re-review and takes the next -rN of thoughts/shared/reviews/<date>_chore-agents-hardening-plan-review[-rN].md, -r2 first, as in this plan's header. R9.4 needs the latest review naming the applied proposal to be APPROVED, and the human's decision. The edit set applies that proposal's blocks unchanged except values filled into its double-angle-bracket placeholders and the reviewed alternative the human chose. Any other change returns to R9.3 before application or R9.5.
   - Code review. On NEEDS_REVISION the human evaluates each finding in a dated record. Confirmed findings are corrected through the existing routing (CLAUDE.md, "Verdicts are terminal for the turn"; Probe protocol step 1). A finding that the evaluation answers needs no code change. Then an R9.7 re-review follows.
   - db review. On DB_NEEDS_REVISION the human evaluates each finding. A confirmed finding that needs a code change is corrected by a coder turn, followed by an R9.7 re-review and then an R9.8 re-review. A finding resolved by documented analysis goes to an R9.8 re-review with that analysis as caller input.
   - After any correction commit: an identity comparison; R9.6 again if a file under .claude/hooks changed; the restart and re-collection rule below if a file Claude Code loads changed.
   - Before the preliminary tester (R9.9), the latest code review is APPROVED and the latest db review is DB_APPROVED, and between each one's Head and HEAD only paths under thoughts/shared/reviews/ change. Before the final tester (R9.12), the latest final code review is APPROVED and the applicable db review is DB_APPROVED: from the final code review's Head to HEAD only review paths may change; from the db review's Head to HEAD the permitted paths are review paths, docs/journal/0009-chore-agents-phase-2-guards.md and this plan, as R9.12 specifies. Other changed paths require the relevant re-review under the correction loop. Every earlier verdict file is kept.

   **Preliminary tester scope (R9.9).** The prompt gives the stage (preliminary Phase 2 validation for this order, at HEAD <H>), this plan's path, branch slug chore-agents-hardening and the verdict path TestResults/chore-agents-hardening/tester-verdict.md, and lists:
   - in scope: tester.md steps 1-9; Phase 2 Validation numbered commands 2, 3, 4, 8, 9, 10, 12, 13, 14, 15, 16 and 17, each quoted verbatim with its expected exit code and output condition (commands 8-10 are tester.md steps 2-4 and run once each); P2.5's `git log --oneline -1 --grep=LIVEPROBE-5c` and `node --test .claude/hooks/tests/write-scope.test.js`, one Bash call each, exactly as written; the manual trace, and the check that every test Phase 2 requires exists and asserts something, including the four Revision 9 tests;
   - human-only, not run: commands 1, 5, 6, 7 and 11;
   - later by design, not conditions of this run: the remaining connection records, including the explainer's; the Coverage ledger; the P2.x and C2.x human assessments; Revision 8's Closure decision; the As built copy and commit; the final code review; and Validation "Order", which governs the human's final Validation run. Their absence is not a finding of this run. An implementation defect stays reportable.
   Every condition excluded here applies before the human's final READY_TO_PUSH.

   **Explainer trigger (R9.10).** Before authorizing the explainer, the human correlates the latest preliminary tester run: its returned report's verdict and its retained verdict file's `Verdict:` line both read READY_TO_PUSH; the file's `Head:` equals the HEAD the human recorded before that run and equals `git rev-parse HEAD` now; its `Branch:` reads chore/agents-hardening; the retained file's sha256 equals the hash captured before the move; and no later tester run exists. A commit after that run fails the trigger and needs a new R9.9 run. An older favourable verdict is never selected over a newer one. The human then authorizes the entry in a separate decision record.

   **Final tester scope (R9.12).** The preliminary scope without P2.5's operations, on the final review-file commit. Nothing is later by design except the human's Validation run and READY_TO_PUSH decision. The prompt adds two read checks: the As built section carries the Coverage ledger, the P2.x and C2.x assessments and the Closure assessment; and the final code review file reads APPROVED, with `git diff --name-only <its Head> HEAD` printing only paths under thoughts/shared/reviews/.

   **Report retention.** Before every tester run, preliminary or final, TestResults/chore-agents-hardening/tester-verdict.md must not exist. If an earlier report occupies it, the human first records that run (correlation check) and the report's bytes and sha256. The human reads the full SHA on its `Head:` line and checks it with `git rev-parse --verify --quiet "<sha>^{commit}"` and against the HEAD recorded before that run. HEAD7 is that SHA's first seven characters; kind is prelim or final; n is the smallest positive integer for which TestResults/chore-agents-hardening/validation/tester-verdict-<kind>-<HEAD7>-<n>.md does not exist, and is recorded. The human checks `test ! -e` on that destination, moves the file with `mv`, and checks that the destination's sha256 equals the recorded hash and that the fixed path no longer exists. For an unaccepted journal entry (R9.10), the human records its bytes, sha256 and originating run separately; selects and records the smallest positive n whose destination TestResults/chore-agents-hardening/validation/journal-0009-attempt-<n>.md does not exist; checks the destination with `test ! -e`; moves the entry; checks its destination hash and the absence of the original path. A journal is not required to have tester Verdict:, Head: or Branch: headers, and the tester-specific header and HEAD7 checks do not apply to it. Any conflict stops the procedure: an invalid or mismatched Head, an existing destination, or a hash difference. The human records it and decides in a dated record; nothing is overwritten.

   **Connection correlation check (per run).** A structural check adapted to each run. "## R8 plan-reviewer — Write connection records" is a worked example, not a set of fixed values. Parameters:
   - identity: agentType (the profile), agent_id and session_id; the subagent transcript and meta paths and their sha256; the meta's description and toolUseId; requestShape recorded as context, not asserted;
   - version: the set of the transcript rows' top-level `version` values, recorded as found;
   - model and effort: every assistant row's model, effort and perTurnEffort, compared with the profile's frontmatter (P2.1); a difference is a P2.1 finding under its fallback rule;
   - caller override: the main-session Agent call whose id equals the meta's toolUseId, extracted from the main-session transcript with its input; for the tester its input has no `model` field (P2.1); for other profiles it is recorded when extracted and stated as "not established" otherwise;
   - tools: every tool call counted by name, in three groups: the profile's frontmatter tools as the prompt limits them; SubagentHandback, for report delivery only; and runtime tools that are absent from the frontmatter but present in the agent's tool list (for example GetTask or ToolSearch), each listed with its input and result. A runtime tool's presence is not by itself a violation. A call outside these groups or outside the prompt's limits is a finding;
   - expected calls: for a Write, exactly one Write whose file_path, normalized for slash spelling, equals the exact report path and whose content equals the report's bytes; for a Bash operation, exactly one Bash call whose command equals the exact text. For each: its tool_use_id, input and timestamp, and exactly one matching tool_result, with is_error and content recorded;
   - hooks: in TestResults/hook-log/hooks.jsonl, the lines with that agent_id, session_id, agent_type and tool_name, the expected hook and the exact normalized target, whose ts lies between the call's and the result's timestamps. There is exactly one allow line per expected hook: path-guard and write-scope:<dir> for a Write; bash-allowlist:<profile> for a Bash call. Tags are never counted over the whole log;
   - other calls of the run: every other Write or Bash call is listed with its hook-log decision; an unexpected block, an error or a missing line is a finding for the human's assessment;
   - repository state: date, branch, full HEAD, `git --no-optional-locks status --porcelain --untracked-files=all` (for a tracked report, exactly `?? <report path>` before its commit), and a record heading that does not already exist in the evidence file.
   Missing evidence is recorded as missing. Nothing is inferred from a model report, a coordinator's prose, or subagent metadata alone.

   **Identity comparison.** A record of: date; branch; full HEAD; empty status; `git diff --name-only <baseline HEAD> HEAD -- .claude .mcp.json CLAUDE.md`; `git diff --exit-code HEAD -- .claude .mcp.json CLAUDE.md`; `git --no-optional-locks status --porcelain --ignored --untracked-files=all -- .claude`; the sha256 of R9.1's 18 paths (CLAUDE.md, .claude/settings.json, .mcp.json, the eight .claude/agents/*.md, .claude/hooks/coder-bash-guard.js, .claude/hooks/bash-allowlist.js, .claude/hooks/write-scope.js, .claude/hooks/path-guard.js, .claude/hooks/lib/guard-common.js, .claude/settings.local.json and C:\Users\jesus\.claude\settings.json), plus every other ignored path under .claude that the status lists; and, as context, `claude --version`. It is compared with the latest accepted baseline and the files changed by approved commits since then: the name-only diff lists exactly those files; every other hash equals its baseline value; .claude/settings.local.json equals the baseline value, which equals the H2.1 value. The user-settings hash and `claude --version` are context, not conditions. Current hashes are present-state records: equality with an earlier recorded value shows equality at the recorded points, not continuous immutability. Historical tracked-file claims rest on the recorded commit chain and each record's status check.

   If an approved commit changes a file Claude Code loads (anything this comparison covers, except files under .claude/hooks/tests/), these follow: step 5's restart with a process-start record (time, `claude --version`, and the session id, marked new or resumed; a human observation); R9.6 again if a file under .claude/hooks changed; and the re-collection of every connection whose relevant files changed. A profile's relevant files are its agent file, the hook scripts its frontmatter names, .claude/hooks/lib/guard-common.js, .claude/hooks/path-guard.js, .claude/settings.json, .claude/settings.local.json, .mcp.json and CLAUDE.md. A Claude Code update is not such a change (Revision 9, "Versions").

   **Unmutated decision-test record (R9.6).** Human, with `run`, while no agent task is running, after the test commit: `date -Iseconds`; `git branch --show-current` (chore/agents-hardening); `git rev-parse HEAD` (the test commit, full SHA); `git --no-optional-locks status --porcelain --untracked-files=all` (no output); `git diff --exit-code HEAD -- .claude/hooks` (exit 0); `node --test .claude/hooks/tests/*.test.js` with its raw output and exit status, under Validation command 2's condition; then `git rev-parse HEAD` and the status again, unchanged. It is the unmutated source of the ledger's results. It does not replace Validation, and no M2 proof is rerun.
```

### E3 — the concurrency sentence in Probe protocol step 3

In plan:444, replace "Do not run local hook tests concurrently with evidence collection; retain the test-log isolation rule." with:

```text
Do not run local hook tests concurrently with evidence collection: from the start of an agent task under check until the human has extracted its tool and hook-log records, no one else runs a hook test. A test run that is itself a recorded call of the agent under check is part of that collection, not concurrent with it; examples are P2.5's tester operation and a Validation command in the tester's own run. The human's R9.6 record and Validation run happen while no agent task is running. Retain the test-log isolation rule: `run-hook.js` gives every test run a temporary `ENVANEX_HOOK_LOG_DIR`.
```

The replacement stays on the same physical line.

### E4 — P2.4 pointer

Insert a bullet after the bullet beginning "  - A role-compatible Write that is itself refused remains pending/inconclusive;" (plan:3292):

```text
  - Revision 9: Probe protocol step 3, "Revision 9 execution order", fixes the genuine task, caller values, file, record, retention and commit for each remaining profile: code-reviewer R9.7 (beside the observation retained above; closure does not depend on it), db-reviewer R9.8 (human decision D2), tester R9.9, and explainer R9.10 (human decision D3, docs/journal/0009-chore-agents-phase-2-guards.md). The researcher, planner and plan-reviewer connections are "## R8 researcher — Write connection records", "## R8 planner — Write connection records" and "## R8 plan-reviewer — Write connection records", reused under that order's identity comparison. No tester run writes over an existing tester-verdict.md, and no explainer run writes over, or reuses the number of, an existing journal entry.
```

### E5 — Coverage ledger, its own item after P2.5

Insert a new list item after P2.5's last bullet, "  - Record P2.1 model/effort and C2.3 hook identity fields. …" (plan:3298). It goes before "- **P2.6 (LSP and Learn):**".

```text
- **Coverage ledger (Revision 9; P2.2, P2.4, P2.5):** a human record in the evidence file, with one row per original negative target. There are 17: 2a-2e; the seven `src/LIVEPROBE-4-<agent>.txt` targets and `thoughts/shared/reviews-probe/LIVEPROBE-4-code-reviewer.md`; and 5a, 5b, 5f and 5d. Columns:
  1. the exact target, the profile, and the hook and argument from that agent file;
  2. byte-identical decision coverage: "none (C1.7)" in every row, a structural limit and not a gap;
  3. behaviour coverage as Revision 9 defines it: the prefixed test name, its input and its asserted reason; the final decision site (file:line) that a static reading assigns to both the test input and the exact target; and every input, reason-token and predicate-path difference. For 5b and 5d, the length shortfall of the test input against the token mismatch of the target inside `startsWithTokens`;
  4. the unmutated result: that test's line in the R9.6 record, plus the R7 copy-runner baseline line where one exists;
  5. the Behaviour mapping category: the M2.x proof only for its mapped item, otherwise "review only";
  6. the live observation, exactly one of three: "exact live block", with its tool input, tool result and hook-log line (2e only); "requested, no tool call (inconclusive)", with each attempt's record and tool-call list (2a-2d from both P2.2 attempts; the two code-reviewer targets from P2.4); or "never requested" (the other ten), stating that no prompt requested it. A `grep -F` absence is corroboration only and does not prove that no call occurred;
  7. the residual: "UNVERIFIED end to end (Revision 8)" for the 16 rows without a live block.

  A second table lists the ten profile-hook pairs that C2.2 names: one per profile, plus `bash-allowlist` for the tester and the explainer. Each row gives the connection record's heading, the Claude Code version its transcript records (or "not recorded"), the P2.1 model and effort record, the main-session Agent input record where one was extracted (required for the tester), the C2.3 fields, and the identity comparison that its reuse rests on. For the `thoughts/shared/research` and `thoughts/shared/plans` scopes, the row also states that their allow path rests on the live allow records of the researcher and planner connections, not on a test, and that their block path rests on the Revision 9 tests. The ledger quotes records; it never restates a model report as a result.
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

Replace plan:3347 ("- Validation runs after all of Phase 2's commits: the code commit that contains `.claude/settings.json` and `.mcp.json`, the commit that copies the probe evidence into As built, and the review-file commit.") with one line:

```text
- Validation runs after all of Phase 2's commits: the code commit that contains `.claude/settings.json` and `.mcp.json`; the Revision 9 plan and test commits and any approved correction commit; the review-file and journal commits of Probe protocol step 3, "Revision 9 execution order"; the commit that copies the probe evidence into As built; and the review-file commit of the final code review (R9.12). The preliminary tester run (R9.9) is not this Validation and closes nothing.
```

## Phase 1: Replacement proposal, re-review, human decision, Revision 9 text (R9.3 iteration 3, R9.4)

### Files
- **`thoughts/shared/plans/2026-10-11_phase2-r8-correction-proposal-r4.md`:** prepared by Codex from r3 with the review corrections above. The human preserves it alone.
- **`thoughts/shared/reviews/<date>_chore-agents-hardening-plan-review-r3.md`:** created by plan-reviewer. The human preserves it alone, whatever its verdict.
  - On NEEDS_REVISION, the loop continues with `thoughts/shared/plans/<date>_phase2-r8-correction-proposal-r5.md` and the next unused plan-review suffix, and so on.
  - The loop never returns to implementation.
- **`TestResults/chore-agents-hardening/evidence/phase-2.md`:** appended by the human. Required:
  - the D2/D3 decision record;
  - the preservation records;
  - the version records of earlier connections;
  - the R9.4 decision record;
  - the edit-set check and the application record.

  The r2 two-Write assessment has already been recorded. This r4 has no agent Write record. Correlation for the re-review Write is optional for C2.2 and is kept separate from approval of its content.
- **`TestResults/chore-agents-hardening/validation/r9-edit-set.md`:** created by the human from the approved blocks, as for Revision 8 (ev:14407-14567).
- **`thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`:** modified by the edit set. Plan-only commit.

### Signatures
None. No code.

### Tests to add
None.

### Validation
The human runs every command with `run` in an external Git Bash at the repository root.

**Preserve this file** (pattern of ev:14777-14833):

Before the commit:
- `git branch --show-current`: exit 0; `chore/agents-hardening`.
- `git rev-parse HEAD`: exit 0; `50f889fafc5459a946f111194a83d0dd6bd1fb53`. If the value differs, the human records why, and the recorded value replaces it below.
- `git --no-optional-locks status --porcelain --untracked-files=all`: exactly `?? thoughts/shared/plans/2026-10-11_phase2-r8-correction-proposal-r4.md`.
- `sha256sum thoughts/shared/plans/2026-10-11_phase2-r8-correction-proposal-r4.md`: exit 0; the value is recorded as S_r4.
- `git add -- <that path>`, then:
  - `git diff --cached --name-only` is exactly that path;
  - `git cat-file blob :<that path> | sha256sum` equals S_r4.

The commit, with the suggested message `docs(agents): preserve R8 correction proposal r4 for review`.

After the commit:
- `git log -1 --format="%H %P"`: the parent is the recorded HEAD. The commit is recorded as P_r4.
- `git diff --name-only HEAD^ HEAD`: exactly that path.
- `git cat-file blob HEAD:<that path> | sha256sum`: S_r4.
- The status: empty.

**Re-review (R9.3 iteration 3).** Supply the already preserved D2/D3 decision record; do not repeat its approval or append a duplicate.

Before the run:
- `ls thoughts/shared/reviews | grep -E '_chore-agents-hardening-plan-review(-r[0-9]+)?\.md$'`: exactly the existing family members `2026-10-10_chore-agents-hardening-plan-review.md` and `2026-10-11_chore-agents-hardening-plan-review-r2.md` (order irrelevant).
- `test ! -e thoughts/shared/reviews/<run date>_chore-agents-hardening-plan-review-r3.md`: exit 0.

Caller values:
- Scope `thoughts/shared/plans/2026-10-11_phase2-r8-correction-proposal-r4.md phase 1-5`;
- Range `<parent of P_r4>..<P_r4>`, expected `50f889fafc5459a946f111194a83d0dd6bd1fb53..<P_r4>`;
- Head `<P_r4>`;
- branch slug `chore-agents-hardening`;
- the file name with `-r3`.

Context supplied: the first proposal and review, preserved r2 and r3, the NEEDS_REVISION re-review of r3, this r4's disposition table, and the recorded D2/D3 decisions. None approves r4. The prompt names no verdict.

After the run:
- `sed -n '1,5p' <review>`:
  - line 1 is `Verdict: APPROVED` or `Verdict: NEEDS_REVISION`;
  - line 2 is `Reviewer: plan-reviewer`;
  - lines 3-5 equal the supplied Scope, Range and Head.
- `sha256sum <review>`: recorded.
- Preserve the review alone, as above. Suggested message: `docs(agents): preserve R8 correction plan re-review r3`.

**Version records of earlier connections.** These are mechanical result records, made before D5 is decided.

The P2.2 coder (ev:14110, ev:14181) and the R8 plan-reviewer (ev:14840) already have recorded versions. The remaining transcripts, each under `C:/Users/jesus/.claude/projects/C--projects-envanex/340a3876-cdcb-4d00-b7ea-9aa2e4731896/subagents/`, are:
- `agent-a49c1b7cd999418ca.jsonl` (P2.4 and P2.6 code-reviewer);
- `agent-a350ad26febf21748.jsonl` (P2.6 researcher);
- `agent-a00e30037b86b0fae.jsonl` (R8 researcher);
- `agent-ae5c9090f4055b6e2.jsonl` (R8 planner).

For each, the human records:
- `sha256sum <transcript>`, compared with the latest hash recorded for that transcript where one exists (ev:14324 for code-reviewer, superseding ev:14262; ev:14359 for the P2.6 researcher; ev:14577 for the R8 researcher; ev:14657 for the planner; use any later explicitly recorded hash if one exists). A difference is recorded, not explained by inference.
- The set of the rows' top-level `version` values, extracted as the plan-reviewer record does (ev:14835). A missing field is recorded as "not recorded".

**R9.4 approval binding.**
- `sed -n '1,5p' <approving review>`:
  - `Verdict: APPROVED`;
  - `Reviewer: plan-reviewer`;
  - `Scope: thoughts/shared/plans/<approved proposal file> phase 1-5`;
  - `Range: <parent>..<P>`;
  - `Head: <P>`.
- `git cat-file blob <P>:thoughts/shared/plans/<approved proposal file> | sha256sum`: the hash recorded at its preservation.
- `git diff --exit-code <P> HEAD -- thoughts/shared/plans/<approved proposal file>`: exit 0.
- `grep -l '^Scope: thoughts/shared/plans/<approved proposal file> ' thoughts/shared/reviews/*.md`: the reviews of that file. If more than one exists, the latest by commit order is the binding review, and it must be the APPROVED one.
- The human-decision record holds:
  - the proposal path, sha256 and P;
  - the review path, sha256, verdict and Head;
  - the choices for D1, D4 and D5;
  - acceptance of R9.1 (ev:15034-15089) as the identity baseline;
  - the edit-set path and sha256.

**Edit-set check and application.**

Before application:
- A recorded `diff` per block between the approved proposal's block and the edit-set block shows only filled `<<...>>` placeholders and, if chosen, the D4-B text.
- `grep -c '<<' TestResults/chore-agents-hardening/validation/r9-edit-set.md`: `0`.
- Each anchor matches exactly once in the plan, and the regions are disjoint.

After application:
- `git diff --check`: exit 0.
- The status: exactly ` M thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`.
- `grep -c '^## Revision 9$' thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`: `1`.
- `grep -c $'\r' thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`: `0`, exit 1.
- `git diff -- thoughts/shared/plans/2026-09-25_chore-agents-hardening.md | grep -c '^+.*<<'`: `0`. The plan's existing heredoc text sits on unchanged lines.

The commit, with the suggested message `docs(agents): plan Revision 9`. After it:
- `git diff --name-only HEAD^ HEAD`: exactly the plan path.
- `git diff --numstat HEAD^ HEAD`: one line whose deletions field is `2`, the E3 line and the E7 line.
- `git diff --exit-code ce5e7cfcb6a90561b69fdb24c0db5384196df3f0 HEAD -- .claude .mcp.json CLAUDE.md`: exit 0.
- The status: empty.

## Phase 2: Four write-scope block tests and the unmutated decision-test record (R9.5, R9.6)

This phase runs only after the R9.4 plan commit, and only under D1-A.

The coder's input names the active plan `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`, phase 2, and "implement only the four Revision 9 items under Tests to add, write-scope.test.js, 'Other scopes'; change no other file or line". It quotes the four commands and expected conditions in this phase's Validation below, and says "For the task, call only Read, Glob, Grep, Edit, Write and Bash; SubagentHandback, if available, is for report delivery only". It ends with "do NOT run any other agent, do NOT commit, wait for me" (CLAUDE.md:96-97).

Before the turn, the human records date, branch, HEAD, empty status, `test -f .claude/hooks/tests/write-scope.test.js` (exit 0) and `git diff --exit-code HEAD -- .claude/hooks/tests/write-scope.test.js` (exit 0). After it, use E2's R9.5 coder implementation record: identity/version/model/effort and each Edit/Write/Bash input, result and applicable path-guard or coder-bash-guard record. No new report, report-path absence check or new C2.2 connection is expected. Post-status must be exactly ` M .claude/hooks/tests/write-scope.test.js`.

### Files
- `.claude/hooks/tests/write-scope.test.js`: modified. The four E6 tests are appended after line 129, inside the `// Other scopes` group. No existing line changes.
- `TestResults/chore-agents-hardening/evidence/phase-2.md`: appended by the human with the identity comparison and R9.6.

### Signatures
No new helper. Each test calls two existing helpers:
- `runWrite(scopeArgs, filePath, projectDir = DEFAULT_PROJECT_DIR)` (`write-scope.test.js:32-35`), with `[<scope>]` and `'src/x.txt'`;
- `assertBlocked(result, hook, reason)` (`:45-49`), with hook `write-scope:<scope>` and reason `target is outside the write scope "<scope>"`.

The project dir stays the default, `C:\projects\envanex`. The `C:/` form is covered by `guard-common.test.js:112`.

### Tests to add
`.claude/hooks/tests/write-scope.test.js`, flat `test()` calls, all review only:

| Test name | Scope argument | Target | Asserted |
|---|---|---|---|
| `write-scope: blocks src/x.txt under thoughts/shared/research` | `thoughts/shared/research` | `src/x.txt` | exit 2; `Blocked by write-scope:thoughts/shared/research:`; `target is outside the write scope "thoughts/shared/research"` |
| `write-scope: blocks src/x.txt under thoughts/shared/plans` | `thoughts/shared/plans` | `src/x.txt` | exit 2; `Blocked by write-scope:thoughts/shared/plans:`; `target is outside the write scope "thoughts/shared/plans"` |
| `write-scope: blocks src/x.txt under TestResults` | `TestResults` | `src/x.txt` | exit 2; `Blocked by write-scope:TestResults:`; `target is outside the write scope "TestResults"` |
| `write-scope: blocks src/x.txt under docs/journal` | `docs/journal` | `src/x.txt` | exit 2; `Blocked by write-scope:docs/journal:`; `target is outside the write scope "docs/journal"` |

### Validation

The coder runs these and reports them. The human checks them against the transcript.
- `node --test .claude/hooks/tests/write-scope.test.js`: exit 0; every required name passes, including a `✔` line for each of the four additions; no failed, cancelled, skipped or todo tests. The derived count for these four additions is `tests 17`, `pass 17`; revise it only for an approved correction changing the test set.
- `node --test .claude/hooks/tests/*.test.js`: exit 0 and Validation command 2's complete names/count condition. For the four additions specified here, the derived expectation is `tests 211` (85 + 126 new names), `pass 211`, and `fail`, `cancelled`, `skipped` and `todo` all 0; an approved correction changing the test set requires a recorded revised expectation.
- `dotnet build -warnaserror`: exit 0; `0 Warning(s)` and `0 Error(s)` (coder.md:26).
- `git status --short`: exactly ` M .claude/hooks/tests/write-scope.test.js`.

The human runs these with `run`, after `/commit` (suggested message `test(agents): block tests for four write-scope arguments`):
- `git diff --name-only HEAD^ HEAD`: exactly `.claude/hooks/tests/write-scope.test.js`.
- `git diff --numstat HEAD^ HEAD`: exactly one line. Its deletions field is `0`, and its path is `.claude/hooks/tests/write-scope.test.js`.
- `git ls-files --eol .claude/hooks/tests/write-scope.test.js`: shows `i/lf` (C1.4).
- `grep -c $'\r' .claude/hooks/tests/write-scope.test.js`: `0`, exit 1.
- Validation command 3: exit 1; the seven paths, each with `:0`.
- Validation command 17: exit 0; no output.
- **Identity comparison** (E2) against R9.1, with the approved change set {`.claude/hooks/tests/write-scope.test.js`}:
  - `git diff --name-only c0e0b65dee26002974b0619ad5bc80ce9c5f6b0e HEAD -- .claude .mcp.json CLAUDE.md` is exactly that path;
  - all 18 hashes equal ev:15063-15080, with the user settings as context only;
  - `.claude/settings.local.json` is `b1db117650e9b182b972ef07b974a3e030a70dc378885d51b79f5c4f330609ca`;
  - the ignored-path listing for `.claude` is recorded. It is a first value and becomes part of the accepted baseline.

**R9.6** (E2, "Unmutated decision-test record"), while no agent task is running:
- `date -Iseconds`.
- `git branch --show-current`: `chore/agents-hardening`.
- `git rev-parse HEAD`: the test commit, as a full SHA.
- `git --no-optional-locks status --porcelain --untracked-files=all`: no output.
- `git diff --exit-code HEAD -- .claude/hooks`: exit 0.
- `node --test .claude/hooks/tests/*.test.js`: exit 0 and Validation command 2's complete condition. The derived count is 211 for the four specified additions, or the recorded updated expectation after an approved correction changing the test set. The raw output is recorded.
- `git rev-parse HEAD` and the status again: unchanged.

## Phase 3: Preliminary code review, db-review, preliminary tester, learning entry (R9.7-R9.10)

### Files
- `thoughts/shared/reviews/<date>_chore-agents-hardening-code-review-phase2[-rN].md`: by code-reviewer. Each file is committed alone.
- `thoughts/shared/reviews/<date>_chore-agents-hardening-db-review-phase2[-rN].md`: by db-reviewer (D2). Each file is committed alone.
- `TestResults/chore-agents-hardening/tester-verdict.md`: by the tester. It is recorded, then moved to `TestResults/chore-agents-hardening/validation/tester-verdict-prelim-<HEAD7>-<n>.md`.
- `docs/journal/0009-chore-agents-phase-2-guards.md`: by the explainer (D3).
  - It is committed alone after the disclosure check.
  - Otherwise it is retained as `TestResults/chore-agents-hardening/validation/journal-0009-attempt-<n>.md`.
- `TestResults/chore-agents-hardening/evidence/phase-2.md`: appended by the human with:
  - the correlation records and the evaluation records;
  - the retention records and the trigger correlation;
  - the authorizations.
- Correction commits, if any, touch only the files of the approved correction.

### Signatures
None.

### Tests to add
None.

### Validation
The human runs every command with `run`.

Expected values for the correlation check (E2), per profile:

| Profile | Frontmatter tools | Model / effort | Expected hooks |
|---|---|---|---|
| code-reviewer, db-reviewer | Read, Glob, Grep, Write, LSP, `mcp__microsoft-learn__microsoft_docs_search`, `mcp__microsoft-learn__microsoft_docs_fetch` | `claude-opus-5-5` / `max` | Write: `path-guard`, `write-scope:thoughts/shared/reviews` |
| tester | Read, Bash, Glob, Grep, Write | `claude-sonnet-5-5` / `low` | Write: `path-guard`, `write-scope:TestResults`; Bash: `bash-allowlist:tester` |
| explainer | Read, Glob, Grep, Bash, Write | `claude-opus-5-5` / `xhigh` | Write: `path-guard`, `write-scope:docs/journal`; Bash: `bash-allowlist:explainer` |

**R9.7 and R9.8, each run.**

Before the run:
- `ls thoughts/shared/reviews | grep -E '_chore-agents-hardening-(code|db)-review-phase2(-r[0-9]+)?\.md$'`: the existing family. Allocate the next suffix independently within each role's family (code-review separately from db-review): no suffix first, then `-r2`, `-r3` and so on.
- `test ! -e <path>`: exit 0.
- `git diff --name-only 84ebd9072f3a06783d71f4af4a7d95f4644c9361 HEAD`: recorded as a caller value.
- For R9.8 only:
  - `sed -n 1p <latest code review>` is `Verdict: APPROVED`;
  - `git diff --name-only <its Head> HEAD` prints only paths under `thoughts/shared/reviews/`.

After the run:
- `sed -n '1,5p' <file>`:
  - the verdict comes from the role's set;
  - `Reviewer:` names the role;
  - `Scope: thoughts/shared/plans/2026-09-25_chore-agents-hardening.md phase 2`;
  - Range and Head are as supplied.
- The correlation check.
- `sha256sum <file>`.

After the file's own commit:
- `git diff --name-only HEAD^ HEAD` is exactly the file;
- its blob hash equals the recorded hash;
- the status is empty.

**R9.9.**

Before the run:
- The R9.9 preconditions of E2 are met.
- `test ! -e TestResults/chore-agents-hardening/tester-verdict.md`: exit 0. If the path is occupied, retention runs first.
- The status: empty.
- `git rev-parse HEAD`: recorded as H.

The prompt quotes these numbered commands verbatim from plan:3362-3377, with their conditions:
- 2. `node --test .claude/hooks/tests/*.test.js` — one run of the five test files. Exit 0. Summary: `tests` = 85 + the number of new names, `suites` 0, `pass` = `tests`, and `fail`, `cancelled`, `skipped` and `todo` 0. Names are taken from the `✔ <name> (<duration>)` lines by dropping the trailing duration in parentheses; the unit is not fixed. No `✖` line appears. Each of the 85 baseline names appears exactly once on a passing line. Each new name appears exactly once and equals no baseline name. Every test name written under "Tests to add" as a prefixed code span appears; items that give cases or commands are not test names; new names not in the plan are listed for review.
- 3. `grep -c LIVEPROBE .claude/hooks/tests/*.js .claude/hooks/tests/fixtures/*.js` — exit 1. The output is exactly this set of 7 paths, each with `:0`, order not part of the condition: `.claude/hooks/tests/bash-allowlist.test.js`, `.claude/hooks/tests/coder-bash-guard.test.js`, `.claude/hooks/tests/guard-common.test.js`, `.claude/hooks/tests/path-guard.test.js`, `.claude/hooks/tests/run-hook.js`, `.claude/hooks/tests/write-scope.test.js`, `.claude/hooks/tests/fixtures/async-decide-guard.js` (C1.7).
- 4. `grep -c $'\r' .claude/hooks/lib/guard-common.js .claude/hooks/path-guard.js .claude/hooks/tests/*.js .claude/hooks/tests/fixtures/*.js` — exit 1. Exactly 9 paths, each with `:0`: `.claude/hooks/lib/guard-common.js`, `.claude/hooks/path-guard.js` and the seven paths of command 3.
- 8. `dotnet format --verify-no-changes` — exit 0; no other condition.
- 9. `dotnet build -warnaserror` — Exit 0; the output has a line matching `^[[:space:]]*0 Warning\(s\)[[:space:]]*$` and a line matching `^[[:space:]]*0 Error\(s\)[[:space:]]*$` (POSIX ERE).
- 10. `dotnet test` — exit 0; per assembly `Failed` 0, `Passed` n, `Skipped` 0, `Total` n, with n = 120 for `Envanex.Domain.Tests.dll`, 126 for `Envanex.Application.Tests.dll` and 392 for `Envanex.IntegrationTests.dll`.
- 12. `git diff --name-only main...HEAD -- src tests db` — exit 0; no output.
- 13. `git status --short` — exit 0; no output.
- 14. `ls .claude/hooks/tests/*.test.js` — exit 0; exactly the five paths, as a set: `.claude/hooks/tests/bash-allowlist.test.js`, `.claude/hooks/tests/coder-bash-guard.test.js`, `.claude/hooks/tests/guard-common.test.js`, `.claude/hooks/tests/path-guard.test.js`, `.claude/hooks/tests/write-scope.test.js`.
- 15. `grep -n "model: haiku" .claude/agents/*.md` — exit 1; no output (C2.5).
- 16. `grep -c $'\r' .claude/hooks/coder-bash-guard.js .claude/hooks/bash-allowlist.js .claude/hooks/write-scope.js` — exit 1; exactly these 3 paths, each with `:0`.
- 17. `git diff --name-only be6477e..HEAD -- .claude/hooks/tests/guard-common.test.js .claude/hooks/tests/path-guard.test.js .claude/hooks/tests/run-hook.js .claude/hooks/tests/fixtures` — exit 0; no output.

Derived count expectation, separate from the quotation: these four additions yield 211 tests in the five-file suite and 17 in write-scope.test.js. If an approved correction changes the test set, derive and record the updated expectation from that approved change. Validation command 2's complete names/count condition remains binding; do not silently accept extra, missing or failed tests.

The prompt also states:
- the two P2.5 operations;
- the human-only commands 1, 5, 6, 7 and 11;
- the later-by-design list, exactly as in E2.

It names no verdict.

After the run:
- **The correlation check:**
  - one Write to the verdict path, with both Write hooks;
  - exactly one Bash call each for `git log --oneline -1 --grep=LIVEPROBE-5c` and `node --test .claude/hooks/tests/write-scope.test.js`, each with its tool_use input and tool_result, and exactly one `bash-allowlist:tester` allow line in its window;
  - every row is `claude-sonnet-5-5` / `low`;
  - the main-session Agent input has no `model` field.
- **`git status --short -- src`:** no output (P2.5).
- **Retention** (E2):
  - `sed -n '1,3p'`, `sha256sum` and `cat` of the verdict file are recorded; its content is a model report;
  - `git rev-parse --verify --quiet "<Head>^{commit}"` equals H;
  - n is chosen and recorded;
  - `test ! -e <destination>`: exit 0;
  - `mv <file> <destination>`: exit 0;
  - `sha256sum <destination>` equals the recorded hash;
  - `test ! -e TestResults/chore-agents-hardening/tester-verdict.md`: exit 0.

**R9.10.**

Before the run:
- The explainer-trigger correlation (E2) is recorded, and then the human's separate authorization.
- `test ! -e docs/journal/0009-chore-agents-phase-2-guards.md`: exit 0.
- `ls docs/journal | grep -c '^0009-'`: `0`, exit 1.
- The status: empty.
- HEAD: equals H.

After the run:
- **The correlation check:**
  - one Write whose target is exactly that path, with both Write hooks;
  - exactly one Bash call `git log --oneline -3 --grep=LIVEPROBE-5e`, with its input and result, and one `bash-allowlist:explainer` allow line in its window;
  - every row is `claude-opus-5-5` / `xhigh`.
- **The status:** exactly `?? docs/journal/0009-chore-agents-phase-2-guards.md`.
- **The disclosure observation:** the human records the line numbers of the D3 statements.
- **If the entry is not accepted:** do not commit it. Record the reason and apply E2's journal retention procedure, then return to D3. A declined Write leaves the connection pending.
- **Only if the entry is accepted, the commit,** with the suggested message `docs(journal): preliminary Phase 2 guards entry`. After it:
  - `git diff --name-only HEAD^ HEAD` is exactly the path;
  - the blob hash equals the recorded hash;
  - the status is empty.

## Phase 4: Ledger, assessments and the As built evidence commit (R9.11)

### Files
- `TestResults/chore-agents-hardening/evidence/phase-2.md`: appended by the human with:
  - any version record still missing;
  - the identity comparison;
  - the Coverage ledger and the C2.2 table (E5);
  - P2.1 for every profile, and C2.1, C2.3, C2.4;
  - the assessments of P2.2-P2.8;
  - the step 4 cleanup checks;
  - the residual-limit assessment, naming the 6 inconclusive rows and the 10 never-requested rows;
  - the limitation text.
- `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`: a `## As built — Phase 2` section, created as a mechanical copy of the evidence file.
  - It goes after the `---` that ends Phase 2's Validation and before `## Phase 3: skills — …`.
  - `## As built — Phase 1` follows the same pattern.

### Signatures
None.

### Tests to add
None.

### Validation
- **Identity comparison** (E2) against the latest accepted baseline and the approved changes: R9.5's test file plus any approved correction's files.
- **Cleanup** (Probe protocol step 4):
  - `git status --short`: no output;
  - `ls obj/LIVEPROBE-*`: "No such file".
- **Step 1.7:** a `diff` between the evidence file and the As built section's body shows no difference.

After the commit (suggested message `docs(agents): As built — Phase 2`):
- `git diff --name-only HEAD^ HEAD`: exactly the plan path.
- `git diff --numstat HEAD^ HEAD`: `0` deletions.
- The status: empty.

## Phase 5: Final review, final tester, human Validation and READY_TO_PUSH (R9.12)

### Files
- `thoughts/shared/reviews/<date>_chore-agents-hardening-code-review-phase2-rN.md`: the final review, numbered next in its family, committed alone.
- `TestResults/chore-agents-hardening/tester-verdict.md`: the final tester's report, recorded.
  - Before any repeated run it is retained as `TestResults/chore-agents-hardening/validation/tester-verdict-final-<HEAD7>-<n>.md`.
- `TestResults/chore-agents-hardening/validation/phase-2-commands.txt`: written only if it does not exist (plan:3381).
- `validation-<sha7>-<time>.log`: one file per run.
- The plan and the evidence file: the validation record, in an optional post-run doc commit.

### Signatures
None.

### Tests to add
None.

### Validation

**Final code review (D4-A).**

Before the run:
- `git diff --name-only <H_A> <As built commit>`: only paths under `thoughts/shared/reviews/`, `docs/journal/0009-chore-agents-phase-2-guards.md` and the plan. Any other path means a later correction exists, and the correction loop has to produce a newer applicable APPROVED review first.
- `test ! -e <path>`: exit 0.

Caller values:
- Range `<H_A>..<As built commit>`, and Head the As built commit;
- link 1's path, Range, Head and verdict;
- the DB_APPROVED review's path;
- the recorded changed-path list;
- the task: review every change in the range, consider Phase 2's complete requirements, and state whether the two links cover every Phase 2 change.

**Final code review (D4-B, instead of the A-only steps above).**

Before the run:
- Record branch, full HEAD (the As built commit) and clean status.
- Record `git diff --name-only 84ebd9072f3a06783d71f4af4a7d95f4644c9361 <As built commit>` as the full changed-path list. It is not restricted to review/journal/plan paths.
- `test ! -e <path>`: exit 0.

Caller values:
- Range `84ebd9072f3a06783d71f4af4a7d95f4644c9361..<As built commit>`, Head the As built commit;
- the applicable DB_APPROVED review's path;
- the full changed-path list and the active plan's complete Phase 2 requirements;
- task: review every change in the full range and the final evidence-bearing state. No link-1 coverage or two-link confirmation is substituted for this review.

Under both A and B:

After the run: the header, the correlation and the commit checks, as in R9.7.

**Before the final tester (human checks, for both D4 options):**
- The applicable final code review's header reads `Verdict: APPROVED`, with the selected final Range and As built Head.
- `git diff --name-only <the final code review's Head> HEAD`: only paths under `thoughts/shared/reviews/`.
- The applicable db review's header reads `Verdict: DB_APPROVED`.
- `test ! -e TestResults/chore-agents-hardening/tester-verdict.md`: exit 0, or retention first.
- The status: empty; HEAD recorded.
- `git diff --name-only <the DB_APPROVED review's Head> HEAD`: only paths under `thoughts/shared/reviews/`, `docs/journal/0009-chore-agents-phase-2-guards.md` and the plan.

**Final tester:**
- The prompt carries the "Final tester scope" of E2.
- The human records the correlation, the P2.1 record for this run, and the report (a model report).
- A repeated final run first applies retention with kind `final`.

**Validation and READY_TO_PUSH:**
- One run of the commands file under Validation's "Runs" and "Pass rule" (plan:3411-3423). Command 2 keeps its complete names/count condition; 211 is the derived expectation for the four specified additions, updated and recorded if an approved correction changes the test set.
- The READY_TO_PUSH record states:
  - Validation "Order" items 1-3 (plan:3349-3352);
  - under D4-A: link 1 and link 2 (paths, Ranges, Heads and verdicts); under D4-B: the full-range final review (path, Range, Head and APPROVED verdict); under either option: the applicable DB_APPROVED review;
  - the retained tester files and their hashes;
  - the journal commit.
- After any doc commit:
  - `git diff --numstat <validated sha>..<last sha>` shows only the plan file, with 0 deletions;
  - `git status --short` shows no output.

## Reports and commits, in order

Commit messages are suggestions; the human writes them.

| # | Report or change | Producer | Retention | Commit |
|---|---|---|---|---|
| 1 | `thoughts/shared/plans/2026-10-11_phase2-r8-correction-proposal-r4.md` (and any later `-rN` revision) | Codex for r4; author recorded per revision | tracked | `docs(agents): preserve R8 correction proposal r4 for review` |
| 2 | `…_chore-agents-hardening-plan-review-r3.md` (and later `-rN`) | plan-reviewer | tracked | review-file commit, alone |
| 3 | active plan, Revision 9 | human edit set | tracked | `docs(agents): plan Revision 9` (R9.4) |
| 4 | `.claude/hooks/tests/write-scope.test.js` | coder | tracked | `test(agents): block tests for four write-scope arguments` (R9.5) |
| 5 | `…_code-review-phase2[-rN].md` | code-reviewer | tracked | one commit per file (R9.7) |
| 6 | `…_db-review-phase2[-rN].md` | db-reviewer | tracked | one commit per file (R9.8) |
| 7 | preliminary `tester-verdict.md` | tester | gitignored; recorded; moved to `validation/tester-verdict-prelim-<HEAD7>-<n>.md` | via As built (R9.11) |
| 8 | `docs/journal/0009-chore-agents-phase-2-guards.md` | explainer | tracked, or retained as `validation/journal-0009-attempt-<n>.md` | `docs(journal): preliminary Phase 2 guards entry` (R9.10) |
| 9 | evidence file | human | gitignored, append-only | via As built |
| 10 | `## As built — Phase 2` | human (mechanical copy) | tracked | `docs(agents): As built — Phase 2` (R9.11) |
| 11 | final `…_code-review-phase2-rN.md` | code-reviewer | tracked | review-file commit (R9.12) |
| 12 | final `tester-verdict.md`, any `-final-` retained files, validation logs | tester; human `run` | gitignored; names and sha256 in the validation record | the optional validation-only doc commit (R9.12) |

## Research open questions: disposition

| OQ | Disposition |
|---|---|
| 1 db-reviewer vs role | D2, a human decision; narrow accommodation |
| 2 explainer contract vs preliminary entry | D3, a human decision; exact path; disclosure |
| 3 READY_TO_PUSH as the explainer's prerequisite | E1 "READY_TO_PUSH"; E2 "Explainer trigger" |
| 4 tester verdict path | E2 "Report retention" |
| 5 preliminary tester vs Validation "Order" | E2 "Preliminary tester scope" |
| 6 "concurrently" | E3 |
| 7 code-reviewer connection basis | the P2.4 observation is kept; R9.7 is cited beside it; closure does not depend on R9.7 |
| 8 planner and plan-reviewer tasks | done: R9.2 and R9.3 iteration 1 (Completed history) |
| 9 journal commit placement | R9.10, before As built and before the final review |
| 10 "actual results" | R9.6 |
| 11 As built | E1 "As built" |
| 12 research Write as a connection | reused; human acceptance at R9.11, under the identity comparison |

## Original review findings 1-14: disposition (confirmed resolved by review r2)

| # | Finding | Disposition | Corrected in |
|---|---|---|---|
| 1 | E1 generalized the usage-description reading | **Agreed.** Exactly two Phase 2 accommodations (human decisions D2 and D3). An explicit list of conditions keeps its force. The plan:430 framing is kept. | Non-goals; D2; D3; E1 "Phase 2 accommodations" |
| 2 | No APPROVED gate; re-review undefined; free adjustment | **Agreed, with content binding** instead of "the highest `-rN`" alone. Approval binds to the exact proposal file, the commit that preserved it, its hash, and the latest review naming it. Re-reviews take the next `-rN`, `-r2` first. Changes beyond the reviewed alternative D4 return to R9.3. | E2 "Approval and review chain"; Phase 1 Validation |
| 3 | Preliminary tester scope, trigger, overstated prompt | **Agreed.** The scope is enumerated, with exclusions that are later by design and all apply at the end. The trigger is bound to the latest run. The prompt states what is pending. **Disagreement with required change 3's last bullet:** it compares `Branch:` with "the current HEAD". The correct check is `Head:` = the commit tested = the then-current HEAD, and `Branch:` = `chore/agents-hardening`. | E2 R9.9, R9.10, "Preliminary tester scope", "Explainer trigger"; Phase 3 Validation |
| 4 | "Decision branch" granularity; 5b and 5d | **Agreed.** One definition, at the final decision site. Predicate-path differences are stated: 5b restated, 5d added. No added tests. | E1 "Coverage"; E5 column 3; D1; Source facts 2-4 |
| 5 | Journal-number rule | **Agreed.** Exact path and unique-prefix rule; the same-number option is removed. Practice and the PR 7 consequence are stated. The human decided it as D3. | D3; E1; E2 R9.10; Phase 3 Validation |
| 6 | Retention and retry | **Agreed.** Destination check, HEAD7, n, and names for final runs. Matching is windowed per call. P2.5 tool input and result are required. | E2 "Report retention", "Connection correlation check"; Phase 3 and 5 Validation |
| 7 | Identity, reuse, version | **Agreed.** CLAUDE.md and `settings.local.json` are hashed: the R9.1 record already does so (ev:15062-15084). Current state is kept apart from historical state. `code-reviewer.md` was compared (ev:14263 = ev:15066). The planner record was added. The actual order is described. D5 is resolved as recorded versions with a mixed-version limit (PROPOSED), and the update and restart rule is stated. **Evidence-extraction gap:** those versions were not extracted into the cited evidence sections. Codex reports 2.1.292 from the earlier planner's raw transcript (r4 provenance); human-run extraction is pending. R9.4 records the extractions. | Completed history; Source facts 8-9; D5; E2 "Identity comparison"; Phase 1 Validation |
| 8 | Live-observation categories | **Agreed:** three categories (1 / 6 / 10). **Note:** the finding's sentence "a `grep -F` absence proves no call" contradicts plan:3290 and its own required change. This proposal uses "corroboration only; absence does not prove that no call occurred". | E1 "Count"; E5 column 6; Non-goals |
| 9 | Approvals and review chain | **Agreed.** DB_APPROVED gate at the same implementation state. DB loop with `-rN`. R9.7 re-reviews use the full Range. D4's base is the last applicable APPROVED review. The Phase 1 precedent is withdrawn rather than qualified. The plan:430 reading is stated. | E2 R9.7-R9.9, "Approval and review chain"; D4; E1 "Final review procedure" |
| 10 | Correlation-check specification | **Agreed in substance:** a structural parameter list, with ev:14835 as the worked example. **Disagreement:** the permitted set is not "frontmatter plus SubagentHandback". Runtime tools outside the frontmatter (GetTask at ev:14247; ToolSearch at plan:3237) are listed separately and are not violations by presence. `requestShape` is recorded as context (plan:3248), not asserted. | E2 "Connection correlation check"; Phase 3 Validation table |
| 11 | Comparison against an obsolete record | **Agreed.** Comparisons use the latest accepted baseline plus approved changes. | E2 "Identity comparison"; Phase 2 and 4 Validation |
| 12 | P:117, scopes without tests | **Agreed.** Research and plans have no test. Their allow path rests on ev:14586 and ev:14772. | D1; E5 second table |
| 13 | R9.6 without HEAD | **Agreed.** The record's full content is specified. | E2 "Unmutated decision-test record"; Phase 2 Validation |
| 14 | Range, ledger placement, coder input | **Agreed.** The actual first-review Range is recorded. The ledger is placed as its own item after P2.5 everywhere. The coder's input is named. | Completed history; E1; E5; E2 R9.5; Phase 2 |

## Open items for the human

1. **Decide at R9.4:** D1 (A recommended), D4 (A or B, both apply-ready) and D5 (A recommended). None is approved by this document.
   - D1-B and D5-B have no apply-ready text.
   - Choosing either returns to R9.3.
2. **D2/D3 decision record:** the human reported `r9-d2-d3-decisions-recorded` after the r2 source snapshot. Supply that existing record to the re-review; do not request the same approval again.
3. **Settle the unrecorded versions.** The P2.4/P2.6 code-reviewer, P2.6 researcher, R8 researcher and R8 planner versions are not recorded. Codex's inspection reports 2.1.292 in the earlier planner's raw transcript; R9.4 records its extraction alongside the other transcripts before D5.
4. **The 2.1.293 process start is unrecorded.** If the human knows when that process started or resumed, a dated human observation records it. Otherwise the gap stays stated.
5. **Provenance:** r2's two Writes have an assessment record. This r4 is prepared by Codex and supplies no new planner connection. A re-review Write may be recorded separately.
6. **Earlier caller overrides are not established.** The main-session Agent inputs of the earlier runs were not extracted. Only the tester requires one (plan:3265). For the others, the ledger states "not established" unless the human extracts them.

## Rollback notes

- **Plan text.**
  - `git revert <Revision 9 plan commit>` restores Revision 8.
  - Records made under Revision 9 stay as records of their state, with a dated note.
- **Tests.**
  - `git revert <test commit>`.
  - Identity comparisons return to the R9.1 baseline.
  - The four scope rows revert to "gap", and D1-B would then need a reviewed revision.
- **Review and journal files.**
  - Never deleted as cleanup.
  - A withdrawal is a `git revert` of the file's commit plus a dated note.
  - An unaccepted journal entry is retained by move, not deleted.
- **Proposal revisions.** A rejected revision stays preserved as review history. The next revision is a new file.
- **Evidence.** Append-only. A wrong record is followed by a dated correction (plan:442).
- **Retained tester verdicts.** Kept, with their moves recorded. Nothing needs rolling back.
- **Declined writes.**
  - The connection stays pending, and the human returns to D2 or D3.
  - No main-session write, and no write under another identity, substitutes.
- **P2.1 fallback (plan:3268), or any approved change to a file Claude Code loads:**
  - restart, with a process-start record;
  - R9.6 again if `.claude/hooks` changed;
  - re-collection of the affected connections (E2, "Identity comparison").
