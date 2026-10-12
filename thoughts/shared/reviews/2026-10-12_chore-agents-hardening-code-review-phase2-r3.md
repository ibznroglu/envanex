Verdict: APPROVED
Reviewer: code-reviewer
Scope: thoughts/shared/plans/2026-09-25_chore-agents-hardening.md phase 2
Range: 02b7923117d70a76779d975e7a8ef1238a3aaa94..d25536afc8c6e1e02b75426db1c494943440c15b
Head: d25536afc8c6e1e02b75426db1c494943440c15b

## Findings

### Stage, inputs and method

- **Stage.** This is the R9.12 final Phase 2 code review under option D4-A (plan:375, :516, :518), which Revision 11 keeps selected (plan:407). It is link 2 of the two-link chain. It reviews:
  - every change in the Range
  - Revision 11's scope decision
  - the R9.11 ledger and assessments
  - the As built copy and its line-ending clarification
  - the 0009 journal entry
- **Link 1.** `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-code-review-phase2-r2.md`: APPROVED, Range 84ebd9072f3a06783d71f4af4a7d95f4644c9361..02b7923117d70a76779d975e7a8ef1238a3aaa94, Head 02b7923…. Its sha256 ecee5686… is the caller's value and equals plan:22677.
- **DB review.** `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-db-review-phase2.md`: DB_APPROVED, Range 84ebd90..a42624d1b1a526b9feaa985d24dc1b27ed4bc856. Its sha256 e8f66d13… is the caller's value and equals plan:22678.
- **Independence.** I did not treat either approval as approval of Revision 11; plan:409 says they are not. I assessed the Range on its own.
- **Inputs.**
  - the caller's preflight: four changed paths, and an empty `git diff --name-only 02b7923… HEAD -- .claude .mcp.json CLAUDE.md src tests db`
  - under `TestResults/chore-agents-hardening/validation/`: `r11-closure-Y6fZUs/result.json`
  - `r11-commit-SWvdnF/commit.log`, the stopped first attempt
  - `r11-lf-s1wlFG/commit.log`, with the same directory's `evidence-lf.md` and `as-built-copy.md`
- **Method.**
  - I read files and ran nothing.
  - Git results, hashes and exit codes are recorded values, cited by line.
  - A runtime claim I could not settle by reading is marked UNVERIFIED and names its settling command.
- **Abbreviations.**
  - plan = the active plan at Head.
  - ev:N = line N of `TestResults/chore-agents-hardening/evidence/phase-2.md`.
  - Inside the As built block, ev:N is plan line N+3592: ev:1 is plan:3593 and ev:19223 is plan:22815. Every anchor I checked keeps this offset.

### D4-A: the two links cover every Phase 2 change

- **Link 2 holds records only.** The Range changes four paths:
  - the two preserved review files
  - `docs/journal/0009-chore-agents-phase-2-guards.md`
  - the plan

  That is exactly the set plan:518 permits under option A.
- **No code or configuration changed.**
  - The caller's restricted diff is empty.
  - At 32599d2, `git diff --exit-code 02b7923… HEAD -- .claude/hooks` exits 0 (plan:22672-22673).
  - At 32599d2, the diff from ce5e7cf over `.claude .mcp.json CLAUDE.md`, excluding the hook tests, exits 0 (plan:22642-22643).
  - d25536a's sole parent is 32599d2, and d25536a changes only the plan (r11-lf commit.log:35-40).
- **Coverage.**
  - Every Phase 2 implementation commit lies in link 1's Range, which was reviewed APPROVED: ce5e7cf and the R9 and R10 test commits.
  - Link 2 adds only the R9.11 and Revision 11 records, the journal entry and the preserved verdict files.
  - **Together the two links cover every Phase 2 change in 84ebd90..d25536a.**
- **DB.** Since a42624d, only review paths, the 0009 entry and the plan have changed. R9.12's db condition holds (plan:516, :525), so no R9.8 re-review is needed.
- **No earlier plan text changed.**
  - d25536a's numstat is 19249 insertions and 0 deletions (r11-lf commit.log:25-26).
  - The line counts reconcile: 23203 plan lines = 3954 before + 21 for Revision 11 (plan:399-419) + 19228 for the As built block (plan:3590-22817).
  - The As built block is a heading, a blank line, the start marker, 19223 evidence lines, a blank line and the end marker.

### Revision 11 (plan:401-417): conforms

- **Delivery scope (plan:405).**
  - It claims nothing for Phases 3-4: they are "not implemented, passed or cancelled".
  - It forbids labelling the roadmap chore complete.
  - It requires the PR to state "Phases1–2 delivered; Phases3–4 deferred".
  - It supersedes only the requirement to execute Phases 3-4 on this branch.
- **Gates (plan:407).** Every gate is kept:
  - R9.12's chain (plan:516): the final review and its review-file commit, the final tester (plan:535), the human's Validation, READY_TO_PUSH, and at most one validation-record doc commit (plan:3493-3499)
  - the 17 Validation commands (plan:3508-3524)
  - the correction loop
  - CI before merge

  The recorded end state is "not pushed" (r11-lf commit.log:47).
- **Bounded update (plan:409).**
  - It asks this review to assess it.
  - It does not relabel earlier approvals.
  - It leaves historical sections unchanged (0 deletions, above).
  - I accept it as a scope and recording update.
- **P2.1 limit (plan:411).** This changes acceptance scope, not only recording: it drops P2.1's `/tasks` observation (plan:3395) and the warning history (plan:3402). I accept it:
  - plan:3402 already makes the transcript's model field the evidence of the model that ran. It calls `/tasks` a human observation and says the absence of a warning proves nothing.
  - Both mechanical conditions remain and are met. Every profile's transcript model and effort equals its frontmatter (`.claude/agents/*.md:5-6` against plan:22758-22767), and the tester's Agent input has no model or effort field (plan:22680-22699).
  - The human's acceptance is recorded (plan:22807).
- **Residuals, journal, ADR and handoff (plan:413-417).**
  - These agree with "ADR needed? No" (plan:467-469) and with the current `.claude/skills/pr/SKILL.md`.
  - "Do not assume the unimplemented Phase3 /pr preconditions apply" is bounded by "No existing security/review requirement is waived".
  - The seams the split leaves are R3-1 to R3-5 below. None is blocking.

### R9.11 ledger and assessments (plan:22715-22802): conforms

Every citation I checked is correct.

- **Structure.** The ledger has the seven columns of plan:3437-3443 for all 17 rows and the ten-row C2.2 table of plan:3445. The researcher and planner rows state the allow-path and block-path basis.
- **R9.6 results.** All 15 distinct citations land on the named passing (✔) line:

  | ev | plan |
  |---|---|
  | 17089, 17091, 17119, 17116, 17092 | 20681, 20683, 20711, 20708, 20684 |
  | 17232, 17233 | 20824, 20825 |
  | 17240-17243 | 20832-20835 |
  | 17040, 17044, 17071, 17083 | 20632, 20636, 20663, 20675 |

  The summary is tests 216, pass 216, exit 0 (plan:20836-20845).
- **R7 baselines.** ev:5939, 5945, 9225, 9249, 9381 and 9453 are `ok` lines of passing runs (plan:9531, 9537, 12817, 12841, 12973, 13045).
- **Static decision sites, checked against the code.**
  - coder-bash-guard.js:119→:128 (2a, 2b, 2e), :125-128 (2d) and :131-132 (2c)
  - write-scope.js:23-24
  - bash-allowlist.js:91-92→:171-173 (5a), :141→:194-196 (5f) and :190-192 (5b, 5d)
  - The length-versus-token reading at bash-allowlist.js:98 is correct for 5b (`--include` against `--verify-no-changes`) and for 5d (`build` against `list`).
- **Reasons and arguments.**
  - The asserted reasons equal the test constants REDIRECTION, NOT_IN_TESTER, `dotnetToken(...)` and NOT_IN_EXPLAINER (bash-allowlist.test.js:72-74, :134, :150, :269, :325).
  - The write-scope inputs and scopes match write-scope.test.js:95-100 and :131-160.
  - Each row's hook argument equals the agent file's hook line (`.claude/agents/*.md:12` and `:16`).
- **The 2e live block.** The tool_use_id, result text, log timestamp, agent and session are quoted exactly (plan:17705-17711, :17745).
- **Anchors.** Every cited heading resolves to its record:

  | Record | plan |
  |---|---|
  | M2 assessments | 6610, 9884, 17554, 17631 |
  | P2.2-P2.8 | 17684, 17757, 17826, 17845, 17892, 17907, 17942, 17976, 17988 |
  | R8 connections | 18160, 18245, 18428 |
  | Identity baseline | 18626 |
  | R9.4 | 19046 |
  | R9.8 | 21169 |
  | R9.9 | 21304, 21456 |
  | R9.10 | 21884, 22208 |
  | C2.6 | 6103 |

  The versions agree with plan:17702, :19067-19074 and :22701.
- **Counts.**
  - The 16 targets without a live block are 4 coder, 8 write-scope and 4 allowlist targets.
  - 6 were requested but never called; 10 were never requested.
- **Identity and cleanup.**
  - The R9.11 identity record (plan:22625-22679) has every element of plan:551.
  - `.claude/settings.local.json` equals the H2.1 value (plan:3773) and the R9.1 value (plan:18671).
  - The step-4 cleanup checks are recorded (plan:22703-22710).
- **Closure items.** The final tester will read these (plan:535), and all are present:
  - the limitation text (plan:22802)
  - the residual assessment (plan:22790-22794)
  - the recorded acceptance (plan:22804-22807)

### As built copy and its line-ending clarification (plan:22810-22815): consistent

- **Why a clarification was needed.**
  - `.gitattributes:4` (`* text=auto eol=lf`) normalizes CRLF on staging.
  - CLAUDE.md forbids committing CRLF.
  - So a raw-byte copy of an evidence file that holds CRLF cannot be committed without changing a Git attribute. None was changed.
- **The recorded sequence is consistent.**
  1. The application run left the raw plan at 6db446ad… and did not commit (result.json:4, :11).
  2. The first attempt warned "CRLF will be replaced by LF", staged 71bce98d… and stopped on the hash difference (r11-commit-SWvdnF/commit.log:16-27).
  3. The second run recorded "exactly 28 CRLF pairs normalized" and "Canonical evidence diff exit=0", then committed d25536a (r11-lf commit.log:16-31).
- **What I checked by reading.**
  - The evidence file has exactly 28 lines that contain a CR. All are captured Windows output in the original prefix: ev:4-5, 9-12, 521-523, 2465-2473, 2477 and 2486-2494.
  - The working-tree plan has no CR.
  - The evidence file has 19223 lines. That equals the span plan:3593-22815 and the line counts of `evidence-lf.md` and `as-built-copy.md`, which contain no CR either.
  - Their first lines and their ledger headings agree.
  - The evidence file's tail equals plan:22804-22815.
- **Consistency with the plan.**
  - The evidence bytes are unchanged (plan:409, :567).
  - Only step 1.7's comparison (plan:480) is qualified, and only by CRLF-to-LF normalization.
  - The clarification is appended, not edited into earlier text.
  - The start and end markers (plan:3592, :22817) are not in step 1.7's wording. They are harmless and bound the compared block.
- **UNVERIFIED.** The byte equality after normalization is a recorded result; I did not re-run it. Settle it with the following command, which should print `cmp=0`:
  `sed 's/\r$//' TestResults/chore-agents-hardening/evidence/phase-2.md | cmp - <(sed -n '3593,22815p' thoughts/shared/plans/2026-09-25_chore-agents-hardening.md); echo "cmp=$?"`

  See also R3-6.

### Journal `docs/journal/0009-chore-agents-phase-2-guards.md`: conforms

- **Identity.** The committed entry is the accepted one: sha256 9bcb2948… at acceptance (plan:22228) and at 32599d2 (plan:22674). d25536a does not touch it.
- **Scope statements.** They match D3 and R9.10 (plan:360, :514):
  - 0009 is a sequence number, not a PR or roadmap number (journal:7).
  - The pending items are listed (journal:8).
  - The preliminary READY_TO_PUSH served only as the trigger (journal:9).
  - 1 of the 17 negatives was blocked live (journal:11).
- **Spot checks against the code and records.** All are correct:
  - `startsWithTokens` at bash-allowlist.js:97-99 and `precheck` at :62-95
  - `logDecision` at guard-common.js:216
  - the reason assertion at bash-allowlist.test.js:49
  - the `\b` cases and the `ls .git/hooks` and `grep -rn "git" docs` false positives (coder-bash-guard.js:9, :86-87, :119)
  - the M2.1-M2.8 red counts 6/5/1/1/3/2/1/9 (plan:6616, :17642, :9897, :17572, :17582, :17595, :17605, :17615)
  - 29 symbols in about 21 s (plan:17925-17926)
  - about 7 s for 216 tests (plan:21632)
- **Time-bound statements.** Two statements are now superseded:
  - "ledger and As built not yet written" (journal:8, :175)
  - "Phase 4 will write the residual record" (journal:192, :234)

  Both were true when written, and Revision 11 keeps the entry's historical scope (plan:415). This is not a defect.

### Non-blocking findings

**R3-1 — Low — docs/roadmap.md:197-198 and :236-237 will misdescribe the merged state.**
- **What the roadmap says.**
  - :197 says the pipeline's rules "are enforced only by prompts" and keeps the whole chore "before PR 7".
  - :236-237 says code-reviewer runs on Sonnet, and researcher and tester on Haiku.
- **What ships.** Seven agents run `claude-opus-5-5` and the tester runs `claude-sonnet-5-5` (`.claude/agents/*.md:5`).
- **Why it stays stale.** Phase 4 owned these edits: remove :197-198 and replace :236-237 (plan:23092-23093, :23137). Revision 11 defers Phase 4 and forbids labelling the chore complete (plan:405), but it does not name this drift.
- **Ordering conflict.** :197's "before PR 7" also conflicts with Revision 11's "return to the product roadmap" (plan:403, :417).
- **Why not blocking.**
  - It is documentation only, and no Phase 2 requirement covers it.
  - A change now would reopen the correction loop (plan:516), against the human's instruction not to expand the scope (plan:403).

**R3-2 — Low — the R9.11 retained-findings list (plan:22799) omits link 1's N5.**
- **N5 is still open.**
  - explainer.md:36-37 says "NNNN matches the PR number", and explainer.md:42 says `# PR NNNN —`.
  - D3 instead gives the PR 7 entry the next free journal number (plan:360).
- **History.**
  - The human kept N5 "an explicit consideration for R9.10" (plan:21119).
  - R9.10 ran under D3, but the instruction itself was not changed.
  - The journal flags this conflict too (journal:241).
- **Why not blocking.** Revision 11 already requires carrying existing non-blocking review findings forward (plan:413). Only the list most likely to be copied into the PR misses N5.

**R3-3 — Info — dormant tester allow entry for gate.sh.**
- **The entry.**
  - bash-allowlist.js:36 holds `exact('bash .claude/skills/gate/scripts/gate.sh')`, with tests at bash-allowlist.test.js:83-84, :185-186 and :255-258.
  - `.claude/skills/` holds only `commit`, `pr` and `verify` (Glob).
  - So when Phases 1-2 ship, the entry allows a script that does not exist.
- **No current risk.**
  - `.claude/settings.json` has no allow entry for this command, so a call would prompt.
  - tester.md does not instruct it.
  - The tester cannot create the file: its write scope is `TestResults`.
- **Gap.** Revision 11 names only the gate.sh base-check risk (plan:413), not this dormant entry.

**R3-4 — Info — the "hook tests not run in CI" known gap (plan:23133).**
- The row says the tests run "only locally and through `gate.sh`".
- Revision 11 carries Phase 4's known-gap descriptions over to the shipped state (plan:413). It also forbids describing deferred tools as installed (plan:405).
- Until Phase 3, the hook tests run only when the human or the tester runs `node --test`.

**R3-5 — Info — the current /pr enforces none of Phase 3's binding to HEAD.**
- **What /pr checks.** `.claude/skills/pr/SKILL.md:10-16` checks:
  - the branch
  - a clean tree
  - an in-session tester READY_TO_PUSH
  - DB_APPROVED for schema phases

  It reads no verdict file and compares nothing with HEAD (compare plan:21).
- **Consequences under Revision 11 (plan:417).**
  - Two rules rest on the human's READY_TO_PUSH record alone: "any other commit after the tester ran invalidates its verdict" (plan:486) and the doc-commit check (plan:3498).
  - The template line "plan-reviewer: APPROVED" (SKILL.md:36) must not present Revision 11 as plan-reviewed (plan:409; SKILL.md:57).
  - Phase 3's security review (plan:27) does not run for this PR.

**R3-6 — Info — provenance and scope of the line-ending clarification.**
- **Provenance.**
  - The second run records its comparisons as runner-printed lines, without their command text or a runner sha256 (r11-lf commit.log:16-17).
  - The application runner, by contrast, recorded its hash (plan:22625).
  - The first attempt's "byte-exact As built: matched" (r11-commit-SWvdnF/commit.log:13) is of the same kind.
  - The retained inputs make the check repeatable; the settling command is above.
- **Scope.**
  - The rule says "For this As built copy" (plan:22815).
  - Later evidence reaches the same block only through an append-only copy (plan:516) or the one validation-record doc commit (plan:3498). Both comparisons meet the same 28 CR lines.
  - Read "this As built copy" as the Phase 2 As built block, including later append-only additions. Otherwise the raw-byte condition cannot be met.

**R3-7 — Info — the C2.1 row (plan:22776) describes a /status screenshot that is not retained.**
- No image exists under `TestResults/` (Glob), so the description is a model report. The section header nevertheless calls the assessments assessments of "retained mechanical records" (plan:22717).
- The C2.1 conclusion stands. It rests on observed frontmatter hook execution, which C2.1 permits (plan:588), and Revision 11 calls the screenshot context only (plan:411).

**R3-8 — Carried forward, unchanged.**
- **UNVERIFIED, from link 1:** the agent files' `description:` lines must stay unchanged (plan:3116).
  - No record in `thoughts/` holds the text before Phase 2.
  - Link 2 changes no agent file.
  - Settle with `git diff 84ebd90 ce5e7cf -- .claude/agents | grep -E '^[-+]description:'`, which should print nothing.
- **Residuals.** F2-F6, link 1's N1-N4, DBR-1 and DBR-2 stay as disclosed (plan:22799).
- **The Revision 8 closure acceptance (plan:22804-22807).**
  - It is a model-formulated decision that the human adopted by running `--apply`, and it is labelled as such.
  - Earlier records follow the same pattern (plan:21891, :22234).
  - It meets Revision 8's "separate human assessment" (plan:346) in that form.

## Required changes

None.

- The two links cover every Phase 2 change.
- Revision 11 defers Phases 3-4 without claiming them and keeps every final gate.
- The ledger's citations and decision sites are correct.
- The As built copy and its line-ending rule are consistent with the plan and with the repository's LF rule.
- The journal entry is accurate for its preliminary scope.

Not required. For the human's evaluation, in the PR description or the follow-up that Revision 11 prescribes (plan:405, :413):

- **R3-1:** list the stale roadmap lines (:197-198, :236-237) and the "before PR 7" ordering.
- **R3-2:** add N5 to the follow-up list, and decide it before the next explainer run.
- **R3-3 and R3-4:** name the dormant gate.sh entry, and carry the CI gap without "and through gate.sh".
- **R3-5:** state three things:
  - /pr does not bind verdicts to HEAD.
  - Revision 11 had no plan review.
  - No security review ran.
- **R3-6:** run the `cmp` settling command if a mechanical copy check is wanted, and apply the same normalization to later append-only copies.
- **R3-8:** run the description-line settling command if wanted.
