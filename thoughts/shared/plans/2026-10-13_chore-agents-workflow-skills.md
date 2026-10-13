# Plan: chore(agents) — complete the deferred Phases 3–4: workflow skills and documentation

Date: 2026-10-13. Branch: `chore/agents-workflow-skills` (slug `chore-agents-workflow-skills`), created from clean `main` at `2e8497a9220ae3ec8789fbc551270dd5538c609c`, the squash commit of PR #15.

Status: planned. Nothing in this plan is implemented, committed, passed, probed or done.

Revision 1 (2026-10-13) applies the human's bounded revision, relayed unchanged by the main session (decisions 4-13 below). The earlier draft was never committed. Its Phase 3A/3B split, validation lists, journal, roadmap and merge texts are replaced, not kept beside the new ones.

## Source and pins

- **Source specification.** `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` at commit `2e8497a9220ae3ec8789fbc551270dd5538c609c`. In this plan, **plan:N** means line N of that file at that commit.
- **This plan leaves alone:**
  - that file
  - its As built sections
  - all evidence under `TestResults/chore-agents-hardening/`

  They stay byte-for-byte unchanged and are never reused as evidence for this branch.
- **PR #15 stays completed.** It delivered Phases 1–2. Revision 11 (plan:401-417) deferred Phases 3–4 as a retained backlog and asked that they be re-evaluated against the current code before implementation (plan:417). This plan is that re-evaluation and the separately approved task.

| Source section | Lines (plan:N) |
|---|---|
| Revision 11 | 401-417 |
| Probe protocol | 471-573 (record types 557-563; `gate.sh` boundary 565; restart 573) |
| Named checks C3.1-C3.3, C4.1-C4.2 | 596-600 |
| Phase 2 Validation (numbered commands, `run`, runner, pass rule) | 3489-3586 (commands 3508-3524; `run` 3532-3540; runner 3544-3550; pass rule 3566-3578) |
| Phase 2 tester profile (the exact `gate.sh` entry, "so Phase 3 doesn't touch the hook") | 3091-3098 |
| Phase 3 | 22820-23037 |
| Phase 4 | 23040-23186 |
| Rollback notes | 23190-23203 |
| M1.1 as built (mutation site `toSlashForm`) | 1654 |
| C1.6 (`CLAUDE_PROJECT_DIR` is `C:/…`) | 1381-1387 |
| P1.13a / P1.13g2 outcomes | 2772-2774 |
| Recorded .NET and node summary formats | 2927-2934, 2899 |
| R9.6 hook-test record (tests 216) | 20836-20845 |
| C2.6 (PreBuildEvent: exec yes) | 22780 |
| Retained non-blocking findings after R9.11 | 22796-22802 |
| R11 As built line-ending clarification | 22810-22815 |

### Human decisions this plan applies

1. **Timing.** Complete Phases 3–4 before roadmap PR 7, on this branch, as a separate PR. PR #15 stays completed.
2. **`/security-review` availability (H0.1).**
   - In the planning session the human saw `/security-review` in the command autocomplete, while `"disableBundledSkills": true` is set (`.claude/settings.json:182`).
   - This is a command availability observation, not a security review. No security review has run for this branch, and H0.1 satisfies no `/pr` precondition. The review runs in P4.4 step 2.
   - The human appends H0.1 to `TestResults/chore-agents-workflow-skills/evidence/phase-3.md` as a human observation relayed on 2026-10-13.
   - `disableBundledSkills` stays unchanged.
3. **Journal numbering.** Numbers stay sequential and independent of GitHub PR numbers. Journals 0001-0009 and `note-2026-09-18-configuration-predicate-guard.md` stay unchanged. Phase 4 corrects `.claude/agents/explainer.md:22`, `:36-37` and `:42`, and CLAUDE.md's explainer bullet to match.

Revision 1 (relayed 2026-10-13):

4. **Two implementation phases, Phase 3 and Phase 4.**
   - The merged `/commit` creates the Phase 3 implementation commit.
   - After the restart, the new `/commit` is exercised by the Phase 3 As built commit (P3.5).
   - The new tester runs after the Phase 3 review (P3.6).
   - The new `/pr` is first used at P4.4.

   No concrete dependency forces a third phase (Bootstrap order).
5. **No duplicate validation within a tester run.** `gate.sh` runs the hook tests, format, build and .NET tests. Their record in `gates.txt` is inspected, never run again (Validation lists).
6. **S0 order.** The plan is committed before plan-reviewer cites its SHA. Then comes the review, then the review-file commit.
7. **Exact commands, recalculated counts.** Old frontmatter lines are explicitly replaced, not duplicated, and `/commit`'s `allowed-tools` gains `Read`.
8. **`gate.sh` never passes on an unwritable or incomplete record.** It exits non-zero on directory, `cd`, creation, truncation, append, read and final-record errors (P3.7).
9. **`mutate-clean.sh` compares the targets with HEAD** in both the index and the working tree. Both mutation scripts handle repository-root, branch and log-write failures (P3.8). Restoration stays the inverse Edit.
10. **Appendix A's compact CLAUDE.md**, within the ≤ 200-line project convention. It keeps the outbox same-transaction invariant and states that the dispatcher is deferred.
11. **A learning journal for this PR**, after the last phase tester and before the final PR-end review. Its number is the next free one at execution time (0010 only if still free). No number is reserved for roadmap PR 7.
12. **Roadmap line 59** reads "Phases 1–2 done (#15); Phases 3–4 in this follow-up", with a relative link to this plan. No post-merge edit is needed, and no commit cites a PR number not yet assigned.
13. **The merge protections used for #15.**
    - Recheck the remote PR head and green checks.
    - Squash with `--match-head-commit` set to the validated SHA.
    - No `--admin` and no force-push.
    - Update `main` with `git pull --ff-only`.

    Title: `chore(agents): complete workflow skills and documentation`.

### Inherited IDs: where they live now

| ID | Original | Here | What changes |
|---|---|---|---|
| Phase 3 Files and Signatures | plan:22822-22986 | Phase 3 | DV2-DV5 |
| P3.1-P3.6, C3.1-C3.3 | plan:22994-23029, 596-598 | Phase 3 | slug paths; DV6-DV8; P3.5 on the As built commit; P3.6 after the review |
| P3.7, P3.8 | — (new: decisions 8-9) | Phase 3 | focused failure checks |
| H4.0, C4.1, C4.2, P4.1-P4.3 | plan:23044-23049, 599-600, 23169-23173 | Phase 4 | P4.2 is case-insensitive (DV9) |
| P4.4 | plan:23174-23182 | Phase 4 | this PR's own PR-end sequence: the first real use of the new `/pr`, with decision 13's merge protections. It is not a repeat of a completed probe |
| Phase 4 Files | plan:23051-23161 | Phase 4 | DV10-DV12 |
| Rollback notes | plan:23190-23203 | Rollback notes | adapted |

## Goal

Deliver the deferred Phases 3–4, so that:
- `/gate` runs `.claude/skills/gate/scripts/gate.sh`, which writes the raw gate output to `TestResults/<slug>/gates.txt`, and replaces `/verify`
- the tester runs that script, inspects its record instead of re-running its commands, takes its file list from `gates.txt`, and records `Gates-sha256:`
- `/mutate` and two scripts standardise mutation proofs against committed code
- `/commit` commits from a message file
- `/pr` refuses verdicts not bound to HEAD or to the branch, and merges only the validated head
- CLAUDE.md, `docs/roadmap.md` and a new `docs/ai-workflow.md` describe the workflow as built
- the residual record is published, and the explainer's journal numbering matches decision 3

This PR's own PR-end sequence (P4.4) is the first real use of the new `/pr`.

## Non-goals

- **No product change.** `src/`, `tests/`, `db/` and `.github/workflows/ci.yml` stay untouched. `dotnet test` stays at 638 (120 + 126 + 392, plan:2932-2934).
- **No hook change.** No file under `.claude/hooks/` changes, tests included. `bash-allowlist.js:36` already allows the tester to run exactly `bash .claude/skills/gate/scripts/gate.sh` (plan:3092; finding R3-3).
- **No CI change.** Hook tests in CI stay a recorded Known gap.
- **No repeat of the Phase 1–2 proofs** ("Regression checks and historical proofs" below). Historical evidence is not rewritten or reused.
- **No settings flips.** `disableBundledSkills`, `disableClaudeAiConnectors`, `permissions.deny`, the agents' model and effort tiers, `maxTurns` and `permissions.defaultMode` do not change.
- **No fix for the open non-blocking review findings.** They are recorded only (Phase 4 residual record).
- **No other repository-wide work.** No README change (`docs/roadmap.md:180`). No line-ending renormalization (`docs/roadmap.md:179`).
- **`gate.sh` never runs `git fetch`.**

## Touches schema? (yes/no — db-reviewer required if yes)

No. db-reviewer is not required. `/pr` precondition 7 is not triggered: no changed path lies under `db/`, `src/Envanex.Infrastructure/Migrations/` or `src/Envanex.Infrastructure/Persistence/`.

## ADR needed? (title, or "no")

No. This is process and tooling, not product architecture (plan:467-469; Revision 11, plan:415). `docs/ai-workflow.md` records the reasoning. No concrete reason to change this was found.

## Bootstrap order

Restarts follow plan:573 and F21: when Claude Code rereads skills, agents and settings mid-session is UNVERIFIED. So every commit that changes such a file is followed by a restart before anything relies on it. A file the coder has written but no restart has loaded is not relied on, even if Claude Code picks it up earlier.

| Stage | What happens | Workflow in force |
|---|---|---|
| S0.1 | The planner finishes this plan's revisions | merged in #15: CLAUDE.md:88-116; `/commit`, `/pr`, `/verify` as merged; tester.md:18-63 (no `gate.sh`, no `Gates-sha256:`); reviewers' verdict files; all hooks |
| S0.2 | The human commits this plan file alone with the merged `/commit`. Call that commit P0 | same |
| S0.3 | plan-reviewer reviews P0 and writes `thoughts/shared/reviews/<date>_chore-agents-workflow-skills-plan-review[-rN].md` | same |
| S0.4 | The human commits the review file alone, whatever its verdict | same |
| S1 | Phase 3 coder turn. The human commits the code with the merged `/commit`; its message holds no `"` (F16's split risk under the merged recipe) | same |
| **R3** | restart after the Phase 3 code commit (`.claude/skills/**`, `.claude/agents/{tester,coder}.md`, `.claude/settings.json`) | from here: `/gate`, `/mutate`, the new `/commit`, the new tester, the new `/pr` (installed, not yet used), the three allow entries; `/verify` is gone |
| S2 | Phase 3 probes. The Phase 3 As built commit, made with the new `/commit`, is P3.5. Then the Phase 3 review, its review-file commit, the Phase 3 tester (= P3.6) and the human's validation | as after R3 |
| S3 | Phase 4 coder turn; code commit with the new `/commit` | as after R3 |
| **R4** | restart after the Phase 4 code commit (`CLAUDE.md`, `.claude/agents/explainer.md`) | from here: CLAUDE.md's light lane, per-phase review-file commit, PR-end sequence, Verification rules and Windows notes; the explainer's journal numbering |
| S4 | Phase 4 probes, As built, review, tester and validation; the learning journal; the final evidence commit | as after R4 |
| S5 | P4.4, this PR's PR-end sequence and the first real use of the new `/pr`. `/security-review` runs here for the first time in this branch; H0.1 and H4.1 are availability observations only | as after R4 |

**S0.3 caller values:**
- Range: `2e8497a9220ae3ec8789fbc551270dd5538c609c..P0`
- Head: P0
- changed paths: the human's `git diff --name-only 2e8497a9220ae3ec8789fbc551270dd5538c609c P0`. Expected: this plan only.

**S0.4 outcomes.**
- NEEDS_REVISION returns to S0.1. The next plan commit gets a re-review, `-r2` first, against that commit, and then S0.4 runs again.
- Phase 3 starts only after an APPROVED review file is committed.

**S1 record.** The human records whether that `/commit` run wrote `TestResults/COMMIT_MSG.txt`. It would only if the session had already loaded the new skill file.

**Why the order holds.** No step depends on a tool before the restart that installs it. If the session loads the new `/commit` before R3, the S1 commit simply runs the new recipe. P3.5 is defined as the first commit after R3, so the order holds either way. None of S0's commits is made in the planning turn.

## Probe protocol for this branch

Inherits plan:471-573 with the changes below.

**Not inherited.** The Phase-2-only procedures (they say so themselves):
- Revision 8 collection order
- Revision 9 execution order and steps R9.x
- the identity comparison
- the connection correlation check
- `LIVEPROBE` targets: no probe here tests a hook decision

1. **Evidence paths.**
   - Evidence files: `TestResults/chore-agents-workflow-skills/evidence/phase-3.md`, `phase-4.md` and `pr-end.md`. `pr-end.md` is local only; the PR body is P4.4's evidence.
   - Validation material goes under `TestResults/chore-agents-workflow-skills/validation/`, mutation logs under `TestResults/chore-agents-workflow-skills/mutations/`.
   - `TestResults/` is gitignored (`.gitignore:31`).
   - The human appends records using the record types of plan:557-563. The main session never writes to an evidence file.
   - Nothing is written under `TestResults/chore-agents-hardening/`.
2. **Order within a phase** (plan:473-484, adapted). Steps:
   1. The coder implements, then outputs PHASE_COMPLETE.
   2. The human runs `/commit` for the code commit.
   3. No mutation proof is required in this plan. P3.4 is a probe.
   4. The human restarts Claude Code. The restart record holds the time, `claude --version` (recorded with `run`) and the session id marked "new" (a human observation).
   5. Probes run in the fresh session. The human appends the records.
   6. Cleanup. `git status --short` is empty, recorded with `run`.
   7. The human appends the evidence file to this plan:
      - layout: `## As built — Phase <N>`, a blank line, `<!-- PHASE<N>-EVIDENCE-START -->`, the evidence with only CRLF→LF normalization, a blank line, `<!-- PHASE<N>-EVIDENCE-END -->`
      - copy check: `sed 's/\r$//' <evidence file> | cmp - <(sed -n '<first>,<last>p' thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md); echo "cmp=$?"` prints `cmp=0`. `<first>` and `<last>` are the recorded line numbers of the copied evidence (plan:22815).
   8. The human commits the As built copy alone as `docs(agents)`. In Phase 3, this commit is P3.5.
   9. code-reviewer reviews that commit and writes `thoughts/shared/reviews/<date>_chore-agents-workflow-skills-code-review-phase<N>[-rN].md`. The caller supplies:
      - the plan path and the phase number
      - the slug
      - Range: `<HEAD recorded before the coder turn>..<As built commit>`
      - Head: the As built commit
      - the human's `git diff --name-only <HEAD recorded before the coder turn> <As built commit>`
   10. **The human commits the review file alone, whatever its verdict.**
   11. The tester runs on that review-file commit, so its `Head:` is that commit. Then, while no agent runs, the human runs the phase's validation (Validation lists) at the same HEAD.

   Two more rules:
   - A failing probe sends the phase back to the coder before review. NEEDS_REVISION goes through the correction loop after step 10:
     1. a coder fix commit
     2. a restart, if a loaded file changed
     3. the affected probes again
     4. an append-only As built copy
     5. a re-review, `-r2` first
   - Any other commit after a tester run invalidates that run.
3. **Late records.**
   - Records made after a phase's As built commit go into the next phase's evidence file under `## Records for Phase <N> made after its As built commit`. These are P3.5, P3.6, the review and tester correlations, and the validation logs' names and sha256.
   - Phase 4's late records, the journal's included, reach As built through the final evidence commit before P4.4. It is an append-only copy with the same `cmp` check.
4. **Restart rule.** After any commit that changes `.claude/settings.json`, `.claude/agents/**`, `.claude/skills/**`, `.claude/hooks/**` (tests excluded), `.mcp.json` or `CLAUDE.md`.
5. **Agents.**
   - Agents are spawned without a model parameter (plan:3389).
   - A prompt that limits tools names them. `SubagentHandback` is for report delivery only (plan:3390).
   - The tester's prompt names the plan path and the stage (`Phase 3`, `Phase 4` or `PR-end`) and gives no file list (F13).
6. **Tester runs: retention.**
   - Before every tester run, `test ! -e TestResults/chore-agents-workflow-skills/tester-verdict.md` must exit 0.
   - An earlier report is first moved with `mv` to `TestResults/chore-agents-workflow-skills/validation/tester-verdict-<stage>-<HEAD7>-<n>.md`:
     - `<stage>` is `3`, `4` or `pr-end`
     - `<HEAD7>` comes from its `Head:` line
     - `n` is the smallest free integer
     - the destination is checked with `test ! -e`
     - the sha256 is recorded before the move and re-checked after it
7. **Tester runs: correlation.** After every tester run, the human makes the P3.6 records (Phase 3, Probes) as that run's correlation record. They are a probe only in Phase 3.
8. **Gate copies.**
   - `gates.txt` is overwritten by every `gate.sh` run. Right after each relevant run, the human copies it to `TestResults/chore-agents-workflow-skills/validation/gates-<label>-<HEAD7>.txt` and records both sha256 values.
   - Labels: `p3.1`, `p3.2`, `3-tester`, `4-tester`, `pr-end-tester` and `pr-end-gate`.
9. **Hook log.** `TestResults/hook-log/hooks.jsonl` is shared with the earlier branch. Lines are selected by session_id, agent_id, hook, target and the call/result window, never by counting over the whole log (plan:546).
10. **Transcripts.**
    - Main session: `~/.claude/projects/C--projects-envanex/<session-id>.jsonl`.
    - Subagents: `~/.claude/projects/C--projects-envanex/<session-id>/subagents/agent-<id>.jsonl`. Its agent type comes from `grep -H '"agentType"' ~/.claude/projects/C--projects-envanex/<session-id>/subagents/agent-*.meta.json`.
    - Rows are recorded with `grep -n -F '<needle>' "$T" | cut -c1-6000`, where `$T` is the transcript and `<needle>` an exact command string or a tool_use id.
11. **Main-session writes.**
    - These are written by the main session, not by an agent:
      - `/commit` writes `TestResults/COMMIT_MSG.txt`
      - `/pr` writes `TestResults/PR_BODY.md`
      - P4.4 step 2 writes the security-review file under `thoughts/shared/reviews/`
    - Write-scope hooks run only from agent frontmatter, so none applies to these writes. path-guard (`.claude/settings.json:151-160`) applies and allows both directories.
12. **Index line endings (H0.2, before each coder turn).**
    - The human records `git ls-files --eol -- <the files the phase modifies>` with `run`. Every line must start with `i/lf`.
    - An `i/crlf` file would be renormalized whole on commit (`.gitattributes:4`; `docs/roadmap.md:179`). The human then decides before the coder turn: accept a whole-file diff for that file in this PR (noted in the commit body), or stop.

## Validation lists and the runner

**Who runs what:**
- The tester runs `gate.sh` once per run and inspects the record (tester.md step 5).
- The human runs, while no agent runs:
  - the extra checks, with the runner on a stage's commands file
  - the gate record checks, against the copy of the gates file from the tester's or `/gate`'s run

Neither re-runs `node --test`, `dotnet format`, `dotnet build` or `dotnet test`. Expected exit codes and output conditions follow the pass rule and condition forms of plan:3566-3578.

### Gate record checks G0-G11 (inspected, never re-run)

**Commands file.** `TestResults/chore-agents-workflow-skills/validation/gate-checks.txt`, exactly these lines:

```
printf 'GC=%s\n' "$GC"; sha256sum "$GC"
head -n 1 "$GC"
grep -x -E 'exit=[0-9]+ step=[^ ]+ kind=(required|info)' "$GC"
grep -A1 -x '\$ git status --short' "$GC"
awk '/^\$ tools_check$/{f=1;next} /^exit=[0-9]+ step=tools-check kind=required$/{f=0} f' "$GC"
awk '/^\$ git diff --name-only main\.\.\.HEAD$/{f=1;next} /^exit=[0-9]+ step=files kind=info$/{f=0} f' "$GC"
awk '/^\$ git diff --name-only main\.\.\.HEAD$/{f=1;next} /^exit=[0-9]+ step=files kind=info$/{f=0} f' "$GC" | grep -E '^(src/|tests/|db/|\.claude/hooks/)|^\.mcp\.json$'
awk '/^\$ node --test \.claude\/hooks\/tests\/\*\.test\.js$/{f=1;next} /^exit=[0-9]+ step=node kind=required$/{f=0} f' "$GC" | grep -E '^(# |ℹ )(tests|suites|pass|fail|cancelled|skipped|todo) [0-9]+$'
awk '/^\$ node --test \.claude\/hooks\/tests\/\*\.test\.js$/{f=1;next} /^exit=[0-9]+ step=node kind=required$/{f=0} f' "$GC" | grep -c -E '✖|^not ok'
awk '/^\$ dotnet build -warnaserror$/{f=1;next} /^exit=[0-9]+ step=build kind=required$/{f=0} f' "$GC" | grep -E '^[[:space:]]*0 (Warning|Error)\(s\)[[:space:]]*$'
grep -E 'Passed!|Failed!' "$GC"
tail -n 1 "$GC"
```

Before a run, the human sets `export GC=<the copy's path>`.

| # | Expected |
|---|---|
| G0 | exit 0; `GC=<path>`, and a sha256 equal to the one recorded when the copy was made |
| G1 | exit 0; one line `gate.sh <ts> branch=chore/agents-workflow-skills head=<the HEAD the run had> main=<sha> origin_main=<sha> base_main=<B> base_origin=<B>`, the two bases equal, nothing `missing` |
| G2 | exit 0; exactly 10 lines, in this order: `exit=0 step=tools-check kind=required`, `exit=0 step=status kind=info`, `exit=0 step=base-check kind=required`, `exit=0 step=files kind=info`, `exit=0 step=log kind=info`, `exit=<any> step=docker kind=info`, `exit=0 step=node kind=required`, `exit=0 step=format kind=required`, `exit=0 step=build kind=required`, `exit=0 step=test kind=required` |
| G3 | exit 0; exactly two lines, `$ git status --short` and `exit=0 step=status kind=info`: the status section is empty |
| G4 | exit 0; exactly 4 lines, the paths of `git`, `node`, `dotnet` and `sha256sum`; no `missing:` line |
| G5 | exit 0; the changed-file list. It is recorded; it is the tester's and the reviewers' list |
| G6 | exit 1; no output: no changed path under `src/`, `tests/`, `db/` or `.claude/hooks/`, and no `.mcp.json` |
| G7 | exit 0; exactly 7 lines: tests 216, suites 0, pass 216, fail 0, cancelled 0, skipped 0, todo 0, each with its reporter's prefix (`ℹ ` for spec, as at plan:2899; `# ` for TAP) |
| G8 | exit 1; `0` (no failed hook test) |
| G9 | exit 0; exactly two lines, `0 Warning(s)` and `0 Error(s)`, with leading spaces |
| G10 | exit 0; exactly 3 lines, all `Passed!`, each with `Failed:` 0 and `Skipped:` 0, and Passed = Total = 120 (`Envanex.Domain.Tests.dll`), 126 (`Envanex.Application.Tests.dll`) and 392 (`Envanex.IntegrationTests.dll`), as at plan:2932-2934 |
| G11 | exit 0; `gate-exit=0 failed=` |

**The 216 baseline.** It is the R9.6 record at the R10 test commit (plan:20836-20845). The final #15 code review records no hook change after it (`thoughts/shared/reviews/2026-10-12_chore-agents-hardening-code-review-phase2-r3.md:44-46`), and G6 shows that this branch changes none. A different count stops the run for the human's assessment.

### Extra checks X1-X12 (Phase 3 on)

These are commands `gate.sh` does not run. Commands file: `TestResults/chore-agents-workflow-skills/validation/commands-3.txt`, exactly:

```
node --version
node -e "JSON.parse(require('fs').readFileSync('.claude/settings.json','utf8'))"
diff <(git show 2e8497a9220ae3ec8789fbc551270dd5538c609c:.claude/settings.json) .claude/settings.json
node -e "const s=require('./.claude/settings.json');const a=s.permissions.allow,d=s.permissions.deny;const c=e=>a.filter(x=>x===e).length;const g=c('Bash(bash .claude/skills/gate/scripts/gate.sh)'),r=c('Bash(bash .claude/skills/mutate/scripts/mutate-run.sh *)'),k=c('Bash(bash .claude/skills/mutate/scripts/mutate-clean.sh *)');console.log('allow='+a.length+' deny='+d.length+' gate='+g+' mutate-run='+r+' mutate-clean='+k);process.exit(a.length===51&&d.length===94&&g===1&&r===1&&k===1?0:1)"
ls .claude/skills
ls .claude/skills/gate/scripts .claude/skills/mutate/scripts
grep -c $'\r' .claude/skills/*/SKILL.md .claude/skills/*/scripts/*.sh .claude/agents/coder.md .claude/agents/tester.md .claude/settings.json
git ls-files --eol .claude/skills .claude/agents .claude/settings.json
grep -H "^allowed-tools:" .claude/skills/commit/SKILL.md .claude/skills/gate/SKILL.md .claude/skills/mutate/SKILL.md .claude/skills/pr/SKILL.md
grep -H -e "^disable-model-invocation:" -e "^effort:" .claude/skills/commit/SKILL.md .claude/skills/gate/SKILL.md .claude/skills/mutate/SKILL.md .claude/skills/pr/SKILL.md
git diff -U0 2e8497a9220ae3ec8789fbc551270dd5538c609c HEAD -- .claude/agents | grep -e '^[-+]name:' -e '^[-+]description:' -e '^[-+]tools:' -e '^[-+]model:' -e '^[-+]effort:' -e '^[-+] *hooks:' -e '^[-+] *PreToolUse:' -e '^[-+] *- matcher:' -e '^[-+] *- type:' -e '^[-+] *command:'
for f in .claude/skills/gate/scripts/gate.sh .claude/skills/mutate/scripts/mutate-run.sh .claude/skills/mutate/scripts/mutate-clean.sh; do bash -n "$f" || echo "syntax-error $f"; done
```

| # | Who | Expected |
|---|---|---|
| X1 | human-only | exit 0; one line `v<major>.<minor>.<patch>`, major ≥ 20 |
| X2 | human-only | exit 0; no output |
| X3 | human-only | exit 1; exactly the four lines below |
| X4 | human-only | exit 0; `allow=51 deny=94 gate=1 mutate-run=1 mutate-clean=1` |
| X5 | both | exit 0; exactly `commit`, `gate`, `mutate`, `pr` (no `verify`) |
| X6 | both | exit 0; exactly the six lines below |
| X7 | both | exit 1; exactly 10 lines, each `<path>:0` (four `SKILL.md`, three `.sh`, coder.md, tester.md, settings.json) |
| X8 | both | exit 0; exactly 16 lines, each starting `i/lf` and containing `w/lf`: the eight `.claude/agents/*.md`, `.claude/settings.json`, the four `SKILL.md` and the three scripts |
| X9 | both | exit 0; exactly the four `allowed-tools` lines below |
| X10 | both | exit 0; exactly the seven lines below |
| X11 | both | exit 1; no output: no agent's name, description, tools, model, effort or hooks line changed since 2e8497a |
| X12 | human-only | exit 0; no output |

X3 output (only the three allow entries are added; the deny list and every other entry are as at 2e8497a):

```
48a49,51
>       "Bash(bash .claude/skills/gate/scripts/gate.sh)",
>       "Bash(bash .claude/skills/mutate/scripts/mutate-run.sh *)",
>       "Bash(bash .claude/skills/mutate/scripts/mutate-clean.sh *)",
```

X6 output:

```
.claude/skills/gate/scripts:
gate.sh

.claude/skills/mutate/scripts:
mutate-clean.sh
mutate-run.sh
```

X9 output:

```
.claude/skills/commit/SKILL.md:allowed-tools: Read, Write, Bash(git status *), Bash(git diff *), Bash(git add *), Bash(git commit -F TestResults/COMMIT_MSG.txt), Bash(git log *), Bash(git branch --show-current), Bash(git switch -c *), Bash(git reset --soft HEAD~1)
.claude/skills/gate/SKILL.md:allowed-tools: Bash(bash .claude/skills/gate/scripts/gate.sh), Bash(git status *), Bash(git diff *), Bash(grep *), Bash(cat *), Bash(ls *)
.claude/skills/mutate/SKILL.md:allowed-tools: Read, Edit, Bash(bash .claude/skills/mutate/scripts/mutate-run.sh *), Bash(bash .claude/skills/mutate/scripts/mutate-clean.sh *)
.claude/skills/pr/SKILL.md:allowed-tools: Read, Glob, Grep, Write, Bash(git status), Bash(git status *), Bash(git log *), Bash(git diff *), Bash(git rev-parse *), Bash(git merge-base *), Bash(git push *), Bash(git switch main), Bash(git pull --ff-only), Bash(git branch --show-current), Bash(gh pr *), Bash(gh run *)
```

X10 output:

```
.claude/skills/commit/SKILL.md:disable-model-invocation: true
.claude/skills/commit/SKILL.md:effort: low
.claude/skills/gate/SKILL.md:disable-model-invocation: true
.claude/skills/gate/SKILL.md:effort: low
.claude/skills/mutate/SKILL.md:disable-model-invocation: true
.claude/skills/pr/SKILL.md:disable-model-invocation: true
.claude/skills/pr/SKILL.md:effort: low
```

### Doc checks D1-D10 (Phase 4 on) and E1 (PR end)

**Commands files.**
- `commands-4.txt` holds the twelve lines of `commands-3.txt`, then these ten.
- `commands-pr-end.txt` holds the lines of `commands-4.txt`, then `git remote -v` (E1, which is C4.2).

```
wc -l < CLAUDE.md
awk '/^<!-- APPENDIX-A-START -->$/{f=1;next} /^<!-- APPENDIX-A-END -->$/{f=0} f' thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md | sed '1d;$d' | diff - CLAUDE.md
grep -rn "/verify" CLAUDE.md docs/roadmap.md docs/ai-workflow.md .claude
grep -n -i -e purchasing -e SoapCore -e "Windows Service" CLAUDE.md
grep -ohE '\]\([^)#]+' docs/ai-workflow.md docs/roadmap.md | cut -c3- | grep -vE '^(https?:|mailto:)' | sort -u | (cd docs && while IFS= read -r t; do if [ -e "$t" ]; then echo "ok $t"; else echo "MISSING $t"; fi; done)
grep -H -e "^model:" -e "^effort:" .claude/agents/*.md
grep -c $'\r' CLAUDE.md docs/roadmap.md docs/ai-workflow.md .claude/agents/explainer.md
git ls-files --eol CLAUDE.md docs/roadmap.md docs/ai-workflow.md .claude/agents/explainer.md
ls docs/journal
grep -n -e NNNN -e "PR number" .claude/agents/explainer.md
```

| # | Who | Expected |
|---|---|---|
| D1 (C4.1) | both | `199` (Appendix A by the planner's count). C4.1's condition is ≤ 200; D2, not this count, establishes equality |
| D2 | human-only | exit 0; no output (CLAUDE.md equals Appendix A) |
| D3 (P4.1) | both | exit 0; exactly one line, from `docs/ai-workflow.md`, containing "`/gate` replaces the project's earlier `/verify` skill". A match in the gitignored `.claude/settings.local.json` is recorded separately, as outside the PR |
| D4 (P4.2) | both | exit 0; exactly one line, starting `8:`, the Overview's out-of-scope sentence |
| D5 (P4.3) | human-only | exit 0; every line starts with `ok `; no `MISSING` line. Anchors are not checked |
| D6 | both | exit 0; exactly 16 lines, as a set: code-reviewer, db-reviewer, plan-reviewer and planner `model: claude-opus-5-5` and `effort: max`; coder, explainer and researcher `model: claude-opus-5-5` and `effort: xhigh`; tester `model: claude-sonnet-5-5` and `effort: low` |
| D7 | both | exit 1; exactly 4 lines, each `<path>:0` |
| D8 | both | exit 0; exactly 4 lines, each starting `i/lf` and containing `w/lf` |
| D9 | both | exit 0. In `commands-4.txt`: exactly the 10 existing entries (0001-…-0009 and the note file). In `commands-pr-end.txt`: those 10 plus exactly one `<N>-<slug>.md`, N being the number recorded before the journal run |
| D10 | both | exit 0; exactly 3 lines, starting `36:`, `37:` and `44:` (`44:# NNNN — <title>`) |
| E1 (C4.2) | human-only | exit 0; lines for `origin` (fetch and push) |

**Tester-runnable.** "both" marks the commands the tester's allowlist permits. None contains a `|` inside a pattern: bash-allowlist splits on every `|` regardless of quotes.

### Runner

The `run` function is plan:3532-3540, unchanged:

```
run() {
  local rc t
  printf '\n$ %s\n' "$1" >> "$E" || return 1
  bash -c "$1" < /dev/null >> "$E" 2>&1
  rc=$?
  t=$(tail -c1 "$E") || return 1
  if [ -n "$t" ]; then printf '\n' >> "$E" || return 1; fi
  printf 'exit=%s\n' "$rc" >> "$E" || return 1
}
```

The runner is plan:3544-3550, with:
- `C`: the commands file
- `K`: the log kind, for example `validation-3`, `gate-checks-3-tester` or `validation-pr-end`
- `V=TestResults/chore-agents-workflow-skills/validation`

```
if h=$(git rev-parse HEAD) && s=$(sha256sum "$C"); then
  L=$V/$K-${h:0:7}-$(date -u +%Y%m%dT%H%M%S).log
  if [ -e "$L" ]; then echo "STOP: $L exists"
  elif printf 'HEAD %s\n%s\n' "$h" "$s" > "$L"; then
    if ( E=$L; while IFS= read -r c || [ -n "$c" ]; do run "$c" || exit 3; done < "$C" ); then echo "recorded: $L"; else echo "STOP: writing $L failed; this run is incomplete"; fi
  else echo "STOP: cannot create $L"; fi
else echo "STOP: no HEAD or no commands file"; fi
```

- **Commands files.** Each is written once, with a quoted heredoc and only if it does not exist yet. Its content and sha256 are recorded.
- **Where.** Runs happen in an external Git Bash at the repository root.

## Regression checks and historical proofs

- **Routine regression checks:**
  - `gate.sh`'s steps: hook tests, format, build and the .NET suite
  - G0-G11, X1-X12, D1-D10 and E1

  They show that the current code and configuration still pass at this branch's HEAD. They run at each phase's tester run and validation, and at PR end. They re-execute the existing suites; they do not re-establish any earlier proof.
- **Historical Phase 1–2 proofs, not repeated:**
  - mutation proofs M1.x and M2.x
  - live probes P1.x and P2.x, with their connection records
  - named checks C1.x and C2.x
  - the Phase 2 coverage ledger
  - #15's human validation logs

  They stay as recorded in the original plan's As built sections and in `TestResults/chore-agents-hardening/`. P3.4 runs M1.1's mutation once more only to exercise `/mutate`; it does not re-prove or re-assess M1.1 (plan:1654).
- **Checks folded into exact diffs instead of re-run** (plan:3508-3524 numbering):
  - X3 shows `.claude/settings.json` unchanged except the three allow entries. So C1.9's `mirror-ok 47` (command 6) and the allow-pattern check (command 7) hold as at 2e8497a.
  - G6 shows no changed path under `.claude/hooks/`, `src/`, `tests/` or `db/`, and no `.mcp.json`. With G3's empty status, that covers C1.7 (command 3), the hook CR checks (4, 16), the test-file list (14), and commands 12 and 17.
  - X11 covers the agents' frontmatter, including the model tiers. It replaces command 15 (`model: haiku`).

## Deviations from the inherited specification

Each is a planner decision for the plan review.

- **DV1. A new slug for every path.** Historical paths are untouched and unused.
- **DV2. `gate.sh` robustness (decision 8).**
  - Start-up failures exit 2 and write nothing. Record I/O failures exit 3.
  - The record is opened once, and every write and read of it is checked. A final verification re-reads the last line.
  - Each step's stdin is `/dev/null`.
  - Callers set the Bash tool `timeout` to 600000. #15's logged integration suite alone took about a minute (plan:2934), so a full run is likely longer than the default 2 minutes; P3.1 records the real time.
  - The stdout format is pinned. Node summary lines are matched for both reporters.
  - `sha256=unavailable` is printed only when `sha256sum` itself is missing.
- **DV3. Mutation scripts (decision 9).**
  - Argument checks:
    - a label matching `^[A-Za-z0-9][A-Za-z0-9._-]*$`, because it becomes a file name and `../x` must not escape `mutations/`
    - the mode `dotnet` or `node`
    - the argument count
    - every target file tracked
  - `mutate-clean.sh` checks both `git diff --exit-code HEAD --` (working tree against HEAD) and `git diff --cached --exit-code --` (index against HEAD). Plain `git diff --exit-code --` compares the working tree with the index and misses a staged change that the working tree matches.
  - The mutation record is `git diff HEAD -- <file>`.
  - Repository-root, `cd`, branch and directory failures exit 2. Log I/O failures exit 3.
  - Neither script changes the target, the index or HEAD.
- **DV4. `/commit` and `/pr` are given as exact files.**
  - Old frontmatter is replaced, not duplicated.
  - `Read` is in `/commit`'s `allowed-tools`, because the Write tool refuses to overwrite a file it has not read.
  - `/pr` takes the plan path as `$ARGUMENTS`; precondition 7 and the body need it.
  - A review "kind" is defined, so "the highest -rN" is unambiguous.
  - Decision 13's merge protections.
- **DV5. tester.md.**
  - It is rebased on the current text (tester.md:18-63; model and effort lines unchanged).
  - It states the one Bash write exception, `gate.sh`, and its bounds.
  - It inspects `gates.txt` instead of re-running the gate's commands (decision 5), and fails on a non-zero `gate.sh` exit.
  - Its output sections let P3.6's byte comparison run mechanically.
  - It never overwrites an existing verdict file.
- **DV6. P3.2 targets one test.** "Edit path-guard.test.js so one assertion expects 0 instead of 2" (plan:23004) would hit the shared helper `assertBlocked` at `.claude/hooks/tests/path-guard.test.js:44` and fail every block test, not one. The mutation is pinned to line 59.
- **DV7. P3.4 pins its files.** M1.1's site is `toSlashForm` in `.claude/hooks/lib/guard-common.js:45` (plan:1654), not `path-guard.js`. "node mode on path-guard.js" (plan:23018) is read as node mode with `path-guard.test.js`. A safety reading and a restore fallback are added, because the live guard is mutated.
- **DV8. P3.5's decisive comparison.**
  - P3.5 compares the stored message with `git stripspace` of the file.
  - `git log --format=%B` is a tformat and appends a line terminator, so a byte diff against it can differ by one trailing blank line (UNVERIFIED). The original command is recorded as context only.
- **DV9. P4.2 is case-insensitive.** The command is `grep -n -i -e …`: the new sentence capitalizes "Purchasing", and `-e` patterns keep it runnable by the tester.
- **DV10. CLAUDE.md is given as exact text (Appendix A, 199 lines by the planner's count).**
  - **Why.** Written out in full, the original Phase 4 content measured about 205-215 lines, against the project's ≤ 200-line convention (C4.1, stated at CLAUDE.md:160). The plan treats that budget as a convention, not as evidence that longer instructions are ignored.
  - **What Appendix A changes:**
    - It compacts the wording of the new Orchestration bullets.
    - It puts the factory note in Windows notes item 4, the same F20 fact; the `export` line points to it.
    - It drops SOAP from the Web line and the "One host" paragraph.
    - It places the journal and ADR before the PR-end sequence.
    - It keeps the outbox same-transaction invariant and states that the dispatcher is deferred (decision 10).
  - **What stays.** Every item of plan:23053-23090 is present. H4.0 and the C4.1 fallback stay as written.
- **DV11. Roadmap.**
  - Line 59 follows decision 12.
  - The removal of rows 197-198 cites #15 and "this follow-up", never an unassigned PR number.
  - The tier table lives in `docs/ai-workflow.md`, with current frontmatter values (F17; "or a link", plan:23137).
  - The residual row carries the open findings.
- **DV12. explainer.md.** The journal-number wording follows decision 3. A stop rule enforces "no number is reused" (plan:360).
- **DV13. Validation without duplicates (decision 5).** G inspects the gate record. X, D and E cover only what `gate.sh` does not run. The checks folded into exact diffs are listed in "Regression checks and historical proofs".
- **DV14. Phase 4 runs the full protocol (steps 1-11).** Then come the learning journal (decision 11), the final evidence commit, and P4.4.
- **DV15. Two new focused checks.** P3.7 covers `gate.sh`'s failure exits and P3.8 the mutation scripts (decisions 8-9). The human runs both from a throwaway script under `TestResults/` against a scratch repository outside the project. No test framework is introduced.

## Phase 3: skills — `/gate`, `/mutate`, `/commit`, `/pr`; the tester and coder contracts

### Human steps

- **H0.1:** append the relayed 2026-10-13 availability observation (decision 2) to `phase-3.md`.
- **H0.2:** `git ls-files --eol -- .claude/agents/tester.md .claude/agents/coder.md .claude/settings.json .claude/skills/commit/SKILL.md .claude/skills/pr/SKILL.md .claude/skills/verify/SKILL.md`. Each line starts with `i/lf`.
- **C3.3 (before the probes):** `command -v sha256sum`, recorded with `run`, prints a path.
- **C3.1 (before the probes):** `ls ~/.claude/skills`. There must be no `commit`, `pr`, `gate`, `mutate` or `verify`. "No such file or directory" passes.
- **Expected prompts in the coder turn.**
  - `rm -r .claude/skills/verify` matches no allow or deny rule in `.claude/settings.json`. coder-bash-guard inspects only `git` and `gh` words (`coder-bash-guard.js:9-10`), so the hook allows it.
  - A permission prompt is expected. The human answers it for this one call ("Yes", not a lasting answer; plan:23080) and records the observation, or records which mode or rule allowed it.
  - `bash -n`, the mutation-script smoke calls and the first `gate.sh` run may prompt as well, because the new allow entries are not yet loaded. They are answered the same way.

### Files

- `.claude/skills/verify/SKILL.md` — deleted. The coder runs `rm -r .claude/skills/verify`; `git mv` is blocked by its hook. The commit stages the deletion with `git add .claude/skills`.
- `.claude/skills/gate/SKILL.md` — created; exact text below.
- `.claude/skills/gate/scripts/gate.sh` — created, at exactly the path `bash-allowlist.js:36` allows; specification below.
- `.claude/skills/mutate/SKILL.md` — created; exact text below.
- `.claude/skills/mutate/scripts/mutate-run.sh` — created; specification below.
- `.claude/skills/mutate/scripts/mutate-clean.sh` — created; specification below.
- `.claude/skills/commit/SKILL.md` — rewritten; its whole content, frontmatter included, becomes the exact text below.
- `.claude/skills/pr/SKILL.md` — rewritten; its whole content becomes the exact text below.
- `.claude/agents/tester.md` — lines 18-63 replaced by the exact text below. Lines 1-17 are unchanged (frontmatter, model `claude-sonnet-5-5`, effort `low`, tools, hooks).
- `.claude/agents/coder.md` — lines 35-36 replaced by the exact text below. Nothing else changes.
- `.claude/settings.json` — three allow entries inserted directly after line 48 (`"Bash(node --test .claude/hooks/tests/*.test.js)",`), in this order:
  - `"Bash(bash .claude/skills/gate/scripts/gate.sh)",`
  - `"Bash(bash .claude/skills/mutate/scripts/mutate-run.sh *)",`
  - `"Bash(bash .claude/skills/mutate/scripts/mutate-clean.sh *)",`

  Nothing else changes (X3). The `gate.sh` entry completes the wiring that R3-3 called dormant.
- **Not touched:** `.claude/hooks/**`, `.mcp.json`, every other agent file, `CLAUDE.md` (Phase 4).

Expected `git status --short` after the coder turn (order not a condition):

```
 M .claude/agents/coder.md
 M .claude/agents/tester.md
 M .claude/settings.json
 M .claude/skills/commit/SKILL.md
 M .claude/skills/pr/SKILL.md
 D .claude/skills/verify/SKILL.md
?? .claude/skills/gate/
?? .claude/skills/mutate/
```

### Signatures

**`.claude/skills/gate/SKILL.md` (exact):**

~~~~markdown
---
name: gate
description: Run the project's gate script and print its summary verbatim. Invoked manually via /gate — never runs automatically.
disable-model-invocation: true
effort: low
allowed-tools: Bash(bash .claude/skills/gate/scripts/gate.sh), Bash(git status *), Bash(git diff *), Bash(grep *), Bash(cat *), Bash(ls *)
---

# Gate

Run the gate script and paste its **raw output**. The script writes every command's full output
to `TestResults/<branch-slug>/gates.txt` and prints a summary computed from that file, so the
human gets the evidence, not your reading of it.

## The one rule

**Never summarise, interpret, or judge.** Do not write "all good", "build passes", "tests green",
"looks correct", or any equivalent. Do not add a verdict line. Do not explain what the output
means. A failing gate — a `gate-exit=1` line, or an exit status of 2 or 3 — is a successful run of
this skill: print it exactly as it came back.

## Command

Run this once, from the repository root, with the Bash tool's `timeout` set to 600000:

    bash .claude/skills/gate/scripts/gate.sh

Paste its stdout verbatim in a fenced block. For details, read `gates.txt`; never retype them.

## Arguments

`$ARGUMENTS` may contain extra checks the human wants in this run — usually a `grep` over a file,
or a path to inspect. Run them after the script, under the heading `## Extra checks`, with the same
rule: print `$ <command>`, then its raw output.

## Gotchas

- `dotnet test` needs Docker running for the integration tests. If the `docker` step shows no
  container, keep going — do not start it yourself and do not warn about it.
- `grep` exits 1 when it finds nothing. That is a result, not an error. Print it as-is.
- Never run `git clean`, `git reset`, `git checkout --`, or anything else that discards work.
- Never edit a file. The script writes only `TestResults/<branch-slug>/gates.txt`.
~~~~

**`.claude/skills/gate/scripts/gate.sh`** (plan:22849-22896, with DV2). It takes no arguments. It sets `set -u`, never `set -e`, and never fetches. It works on the repository that contains the current directory.

- **Start-up — exit 2, nothing written.** It prints one reason line to stderr and exits 2 when:
  - `git rev-parse --show-toplevel` fails (not inside a work tree)
  - `cd` to that directory fails
  - `git branch --show-current` fails or prints nothing (detached HEAD)
  - `mkdir -p TestResults/<slug>` fails. `<slug>` is the branch with `/` replaced by `-`.
- **The record.**
  - `TestResults/<slug>/gates.txt` is opened once, truncating it, on one file descriptor. Every later write uses that descriptor.
  - A failed open prints `gate.sh: I/O error: open <path>` to stderr and exits 3.
- **Header (the first line):**

  ```
  gate.sh <date -u +%Y-%m-%dT%H:%M:%SZ> branch=<b> head=<git rev-parse HEAD> main=<sha|missing> origin_main=<sha|missing> base_main=<sha|missing> base_origin=<sha|missing>
  ```

  The values come from `git rev-parse --verify --quiet main`, `git rev-parse --verify --quiet origin/main`, `git merge-base main HEAD` and `git merge-base origin/main HEAD`. `missing` is written when the command fails.
- **`run_step <name> <required|info> <command string>`:**
  - writes `$ <command string>`
  - runs the string with `eval` in the script's own shell, with stdin from `/dev/null` and stdout and stderr to the record's descriptor
  - if the step's output is non-empty and does not end with a newline, writes one. It reads the record's size and last byte to decide. Empty output writes nothing.
  - writes `exit=<n> step=<name> kind=<kind>`
- **`tools_check`:**
  - for each of `git`, `node`, `dotnet` and `sha256sum` separately, prints `command -v <name>`'s path, or `missing: <name>`
  - returns 1 if any is missing, else 0
- **`base_check`:**
  - prints the two merge-bases
  - returns 1 with a reason line when `origin/main` is missing, when either `git merge-base` fails, or when they differ; else 0
- **`final_status`:**
  - reads back from the record every line matching `grep -E '^exit=[0-9]+ step=[^ ]+ kind=required$'`. A read error (`grep` exit 2) is an I/O error, never "no match".
  - each of `tools-check`, `base-check`, `node`, `format`, `build` and `test` needs exactly one such line. A step fails on a non-zero `exit=`, a missing line, or more than one line.
  - writes `gate-exit=<0|1> failed=<comma-separated failed steps>` as the last line
- **Steps, in order:**

  | # | Step | Kind | Command string |
  |---|---|---|---|
  | 1 | `tools-check` | required | `tools_check` |
  | 2 | `status` | info | `git status --short` |
  | 3 | `base-check` | required | `base_check` |
  | 4 | `files` | info | `git diff --name-only main...HEAD` |
  | 5 | `log` | info | `git log --oneline main..HEAD` |
  | 6 | `docker` | info | `docker compose ps` |
  | 7 | `node` | required | `node --test .claude/hooks/tests/*.test.js` |
  | 8 | `format` | required | `dotnet format --verify-no-changes` |
  | 9 | `build` | required | `dotnet build -warnaserror` |
  | 10 | `test` | required | `dotnet test` |
  | 11 | — | — | `final_status` |

- **I/O rule — exit 3.**
  - Every write to the record is checked: the header, each `$` line, each newline fix, each `exit=` line, the final line.
  - Every read of it is checked: the size and last-byte reads, `final_status`'s read-back, the summary's reads, the final verification.
  - A failed write or read prints `gate.sh: I/O error: <operation> <path>` to stderr and exits 3 at once.
- **Final verification.** After the final line, the script closes the descriptor (checked) and re-reads the record's last line. If it is not exactly the line `final_status` wrote, exit 3.
- **Exit status:**
  - 0 — the record is complete and verified, and its last line is `gate-exit=0 failed=`
  - 1 — the record is complete and verified, and a required step failed
  - 2 — start-up failure; nothing written
  - 3 — I/O error on the record

  Only 0 is a passing gate, and no other path exits 0. A failed summary read after a verified record also exits 3. The record stays as verified, but the caller sees a non-zero exit, and the tester returns NEEDS_FIXES on it.
- **Stdout**, computed from the record after the final verification, in this order:
  1. the line `TestResults/<slug>/gates.txt`
  2. `sha256sum`'s output line for it, or `sha256=unavailable` when `sha256sum` is missing
  3. `files:`, then the `files` section's lines
  4. `steps:`, then every line of the record that starts with `exit=`, in record order
  5. `build (last 5 lines):`, then the last 5 output lines of the build section
  6. `tests:`, then every test-section line that contains `Passed!` or `Failed!`
  7. `dotnet test passed total: <sum of the numbers after "Passed:" on those lines>`
  8. `node:`, then the node section's lines matching `^(# |ℹ )(pass|fail) [0-9]+$`
  9. the record's last line

  A section's lines are those between its `$ <command>` line and its `exit=` line.
- **Line endings.** The file is LF only. It is invoked as `bash <path>`, so no exec bit is needed.

**`.claude/agents/tester.md`: lines 18-63 become exactly:**

~~~~markdown
You are a QA engineer. Your ONLY job is validation — you NEVER fix anything.

**STRICT VALIDATION-ONLY ROLE:**
You NEVER edit or modify any file. The only file you write with the Write tool is your verdict
file, `TestResults/<branch-slug>/tester-verdict.md`. A hook blocks every Write outside
`TestResults/`. You write nothing through Bash, with one exception: the gate script, run as the
exact command `bash .claude/skills/gate/scripts/gate.sh`, with no arguments, pipe or redirection.
It writes `TestResults/<branch-slug>/gates.txt`, and its `dotnet` steps write build output under
`bin/` and `obj/` as `dotnet build` and `dotnet test` always do; it writes nothing else. Every
other Bash command is read-only: a hook (bash-allowlist) allows only the commands of your
allowlist, with no `;`, `&&`, `||`, background `&`, command substitution, or redirection other
than the token `2>&1`, and pipes only into `grep`, `head`, `tail` or `wc`. If you discover a
problem, describe it precisely (file:line + what is wrong) so the CODER can fix it. You do not
fix it yourself. Violating this rule corrupts the pipeline.

Input: $ARGUMENTS (plan file path, and the phase or stage to validate)

Steps:
1. Read the plan's validation for that phase or stage.
2. Run `bash .claude/skills/gate/scripts/gate.sh` once, with the Bash tool's `timeout` set to
   600000. Paste its stdout verbatim. Its first two lines are the path of `gates.txt` and the
   `sha256sum` line for it.
3. Read `TestResults/<branch-slug>/gates.txt`; use Grep to find the lines you need, because the
   file is long. Your file list is its `files` section: the lines between
   `$ git diff --name-only main...HEAD` and the next `exit=` line. Ignore any file list typed in
   the prompt.
4. Return NEEDS_FIXES when gate.sh exited with a status other than 0, when the line after
   `$ git status --short` in `gates.txt` is not exactly `exit=0 step=status kind=info` (the
   working tree was not clean), or when the last line of `gates.txt` is not exactly
   `gate-exit=0 failed=`.
5. Check the outcomes gate.sh recorded — each step's `exit=` line, the hook-test summary, the
   build's warning and error counts, and the test totals — against the plan's expectations, with
   Read and Grep. Do not run `node --test`, `dotnet format`, `dotnet build` or `dotnet test`
   yourself: gate.sh ran them, and `gates.txt` is their record.
6. Run the plan's other validation commands that your allowlist permits, each exactly as the plan
   writes it. List the commands the plan marks human-only as not run.
7. Manually trace the logic: read the files of step 3 and verify the implementation matches the
   plan's goal, not just that it runs.
8. Confirm the tests and checks the plan requires exist and actually assert something: grep for
   each named test.
9. Paste raw output; never compose it. A verdict without the commands' real output is incomplete
   and will be rejected.
10. Write `TestResults/<branch-slug>/tester-verdict.md`. `<branch-slug>` is the output of
    `git branch --show-current` with `/` replaced by `-`. If that file already exists, do not
    write: report it and stop, because an earlier report is never overwritten. The file records
    your run; it is not a gate. Its first lines are exact:

    ```
    Verdict: <READY_TO_PUSH|NEEDS_FIXES>
    Head: <git rev-parse HEAD>
    Branch: <branch>
    Gates-sha256: <the 64-character hash on the second line of gate.sh's stdout in step 2>
    ```

    The rest of the file is your report below.

If the plan asks for a `dotnet` command that gate.sh does not run, it takes only the flags the
allowlist lists: `-warnaserror`, `--no-restore`, `--filter`, `-v`/`--verbosity`, a `src/` or
`tests/` path, and `2>&1`.

Output:
## gate.sh stdout
(verbatim, in a fenced block)
## Changed files
(the `files` section of `gates.txt`, verbatim, in a fenced block)
## Recorded gate outcomes
(the `gates.txt` lines checked in step 5, verbatim)
## Other commands run
(command — exit status — relevant output)
## Manual trace
## Verdict
READY_TO_PUSH | NEEDS_FIXES
## Issues
(file:line — what is wrong)
~~~~

The script-indirection residual stays recorded in Phase 4, residual item (2). No hook inspects what `gate.sh` runs or writes; its writes are bounded by its reviewed content.

**`.claude/skills/mutate/SKILL.md` (exact; the Rules are F18's, verbatim from plan:22906-22911):**

~~~~markdown
---
name: mutate
description: Run one mutation proof against committed code and log it under TestResults. Invoked manually via /mutate — never runs automatically.
disable-model-invocation: true
allowed-tools: Read, Edit, Bash(bash .claude/skills/mutate/scripts/mutate-run.sh *), Bash(bash .claude/skills/mutate/scripts/mutate-clean.sh *)
---

# Mutate

Prove that a test protects a behaviour: break the code on purpose, watch the test go red, restore
the code, and prove the restore. Every step's raw output goes to
`TestResults/<branch-slug>/mutations/<label>.txt`.

`$ARGUMENTS`: a label, the target file, the mode (`dotnet "<filter>" [<project>]` or
`node <test-file>`), and the mutation to apply.

## Procedure

0. `bash .claude/skills/mutate/scripts/mutate-clean.sh <label> <file>` must exit 0. It compares the
   file with HEAD, both staged and unstaged. Mutations run only against committed code; otherwise
   STOP.
1. Apply the mutation with Edit.
2. `bash .claude/skills/mutate/scripts/mutate-run.sh <label> <file> <mode arguments>`, with the Bash
   tool's `timeout` set to 600000.
3. Apply the inverse Edit.
4. `bash .claude/skills/mutate/scripts/mutate-clean.sh <label> <file>` must exit 0. If it does not,
   STOP and tell the human. Never use `git checkout --`.

## Rules

- no constant the compiler folds (CS0162 under warnings-as-errors)
- never `--no-build`
- a red counts only when the test's own assertion fails; infrastructure failures (for example a Docker API 500) are rerun and logged as `<label>-infra-N`
- report the failing assertion for every red
- restore only with the inverse Edit, never `git checkout --` (denied)

## Report

The log path; every failing test and assertion line that `mutate-run.sh` printed; the `exit=`
lines of both clean checks. Raw output only.

## Gotchas

- Mutating a file under `.claude/hooks/` changes a live guard for every tool call until the
  inverse Edit. If a mutation blocks the inverse Edit, stop and tell the human, who restores the
  file outside Claude Code.
- Integration-test filters need Docker running.
- A coder turn never invokes `/mutate`; it calls the two scripts directly (see `coder.md`).
~~~~

**`mutate-run.sh`** (plan:22913-22918, with DV3). Usage:

```
mutate-run.sh <label> <file> dotnet "<filter>" [<project>]
mutate-run.sh <label> <file> node <test-file>
```

- **Argument check — exit 2, nothing written, a `usage:` line on stderr.**
  - the label matches `^[A-Za-z0-9][A-Za-z0-9._-]*$`
  - the mode is `dotnet`, with 4 or 5 arguments, or `node`, with exactly 4
- **Start-up — exit 2, nothing written.** It fails when:
  - `git rev-parse --show-toplevel` fails, or `cd` to it fails
  - `git branch --show-current` fails or prints nothing
  - `git ls-files --error-unmatch -- <file>` fails (untracked target)
  - `mkdir -p TestResults/<slug>/mutations` fails
- **The log.** `TestResults/<slug>/mutations/<label>.txt` is opened once in append mode on one descriptor; a failed open exits 3. It records, in order:
  1. `$ git diff HEAD -- <file>` (the mutation against the commit), its output, `exit=<n>`
  2. `$ <test command>`, its raw stdout and stderr (a final newline is added if missing), `exit=<n>`

  The test command is `dotnet test [<project>] --filter "<filter>"` (never `--no-build`) or `node --test <test-file>`. Stdin is `/dev/null`.
- **I/O rule — exit 3.** Every write to the log and every read of it (the last-byte check) is checked. A failure prints `mutate-run.sh: I/O error: <operation> <path>` to stderr and exits 3 at once.
- **Stdout:**
  - the log path
  - the test-output lines matching `^not ok|✖|AssertionError|^\s+(expected|actual)` (node) or `^\s*Failed |Assert|Shouldly|Expected|Actual|Error Message` (dotnet)
  - the test's `exit=` line
- **Exit status:**
  - 0 — both records are complete; red or green is the logged `exit=`
  - 2 — an argument or start-up failure
  - 3 — a log I/O failure
- **No changes.** It never changes the target file, the index or HEAD. Restoration stays the inverse Edit.

**`mutate-clean.sh <label> <file>...`** (plan:22919, with DV3). It needs at least one file.

- **Checks.** Argument check and start-up as `mutate-run.sh`; every `<file>` must be tracked.
- **Records**, in the same log:
  1. `$ git diff --exit-code HEAD -- <files>` (working tree against HEAD), its output, `exit=<n>`
  2. `$ git diff --cached --exit-code -- <files>` (index against HEAD), its output, `exit=<n>`

  It prints those lines. The I/O rule is as in `mutate-run.sh`.
- **Exit status:**
  - 0 — both diffs exit 0: the files equal HEAD in the index and in the working tree
  - 1 — a diff exits 1, and neither git call failed otherwise
  - 2 — an argument or start-up failure, or a git call exiting with another code
  - 3 — a log I/O failure
- **No changes.** It never changes the files, the index or HEAD.

**`.claude/agents/coder.md`: lines 35-36 become exactly:**

```
- Mutation proofs follow `.claude/skills/mutate/SKILL.md` (its steps 0-4 and its rules), but you
  never invoke `/mutate`: only the human can. Call its scripts directly with the Bash tool:
  `bash .claude/skills/mutate/scripts/mutate-clean.sh <label> <file>` first (it must exit 0), then
  the mutation with Edit, then `bash .claude/skills/mutate/scripts/mutate-run.sh <label> <file> ...`
  (timeout 600000 in `dotnet` mode), then the inverse Edit, then `mutate-clean.sh` again (it must
  exit 0). Never use `git checkout --`.
```

The Phase 1 and Phase 2 proofs predate the scripts and are not rerun (plan:22912).

**`.claude/skills/commit/SKILL.md`: the whole file becomes exactly** (plan:22920-22928, with DV4; the `description:` line is unchanged from line 3):

~~~~markdown
---
name: commit
description: Stage and commit changes on the current feature branch, following the project's commit conventions. Invoked manually via /commit — never runs automatically. Does NOT push and does NOT touch main.
disable-model-invocation: true
effort: low
allowed-tools: Read, Write, Bash(git status *), Bash(git diff *), Bash(git add *), Bash(git commit -F TestResults/COMMIT_MSG.txt), Bash(git log *), Bash(git branch --show-current), Bash(git switch -c *), Bash(git reset --soft HEAD~1)
---

# Commit (feature branch only)

## Procedure

1. Run `git branch --show-current`. **If the current branch is `main`, STOP.** Tell the user which
   branch name you would create (`feat/<slug>`, `fix/<slug>` or `chore/<slug>`, derived from the
   active plan file name) and ask them to confirm before running `git switch -c`.
2. Run `git status --short` and `git diff --stat`. Show the user the list of changed files.
3. Stage only the intended files with `git add <path>...`. Never blindly `git add -A` — if an
   unexpected file appears, ask the user before staging it. Unstaging is the user's job, because
   `git restore` is denied.
4. Write the message to `TestResults/COMMIT_MSG.txt` with the Write tool: a Conventional Commit
   `type(scope): summary`, plus a bullet body for non-trivial changes. English only. If the file
   already exists, Read it first; the Write tool refuses to overwrite a file it has not read.
5. Commit with `git commit -F TestResults/COMMIT_MSG.txt` through the **Bash** tool — never
   PowerShell, never `-m`. Do NOT push.
6. Print `git log -1 --format=%B` raw, then report the hash and the branch name.

## Conventions

- Commits inside a feature branch should be **atomic**: one logical change each. They exist to
  make the PR readable. Because the PR is squash-merged, they will collapse — so favor clarity
  over ceremony, and do not agonize over splitting.
- Do keep code and docs/plans in separate commits when both changed. It makes the diff readable.
- Types: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`.
- Scopes: `domain`, `app`, `infra`, `api`, `soap`, `worker`, `web`, `db`, `ci`, `agents`, `auth`,
  `solution`, `roadmap`, `adr`, `journal`.

## Rules

- NEVER commit automatically or mid-phase — only when the user explicitly runs `/commit`.
- NEVER commit directly on `main`.
- NEVER push. Pushing and merging is `/pr`.
- If unsure which files belong in the commit, ask before staging.
- `git reset --soft HEAD~1` is allowed only on a commit this invocation made, and only while
  `git status -sb` shows it unpushed (no upstream, or `ahead`).
~~~~

**`.claude/skills/pr/SKILL.md`: the whole file becomes exactly** (plan:22929-22979, with DV4 and decision 13; the `description:` line is unchanged from line 3):

~~~~markdown
---
name: pr
description: Push the current feature branch, open a pull request, wait for CI, squash-merge it, and return to an updated main. Invoked manually via /pr — never runs automatically.
disable-model-invocation: true
effort: low
allowed-tools: Read, Glob, Grep, Write, Bash(git status), Bash(git status *), Bash(git log *), Bash(git diff *), Bash(git rev-parse *), Bash(git merge-base *), Bash(git push *), Bash(git switch main), Bash(git pull --ff-only), Bash(git branch --show-current), Bash(gh pr *), Bash(gh run *)
---

# Open and squash-merge a PR

`$ARGUMENTS` names the active plan file. Without it, ask for the path and STOP.

## Definitions

- `<slug>` is `git branch --show-current` with `/` replaced by `-`.
- A review file belongs to this branch when its name matches `thoughts/shared/reviews/*_<slug>-*.md`.
- A review file's kind is its name without the leading `YYYY-MM-DD_<slug>-` and without a trailing
  `-r<N>` before `.md`: for example `code-review-branch`, `code-review-phase3`, `security-review`.
  Within one kind only the file with the highest N counts; a file without `-r<N>` has N = 1. Two
  files of one kind with the same N fail every precondition that reads that kind.
- A file's header is its first five lines: `Verdict:`, `Reviewer:`, `Scope:`, `Range:`, `Head:`.
- **Head rule R:**
  1. `git rev-parse --verify --quiet "<Head>^{commit}"` exits 0. An unknown or mistyped SHA fails here.
  2. The file's `Head:` equals `git rev-parse HEAD`, or `git diff --name-only <Head> HEAD` exits 0
     and every path it prints is under `thoughts/shared/reviews/`.
- **Git errors:** any non-zero exit from `git rev-parse`, `git diff` or `git merge-base` during a
  precondition check fails that precondition. The output of a failed command is never read as an
  empty list. The one exception is `git merge-base --is-ancestor`, where exit 1 means "not an
  ancestor"; that also fails the precondition.

## Preconditions — verify all seven, STOP if any fails

1. The current branch is NOT `main`.
2. The working tree is clean (`git status --short` is empty).
3. `TestResults/<slug>/gates.txt` meets three conditions:
   - the `head=` value on its first line equals `git rev-parse HEAD`
   - its `status` section is empty: the line after `$ git status --short` is exactly
     `exit=0 step=status kind=info`
   - its last line is exactly `gate-exit=0 failed=`
4. `TestResults/<slug>/tester-verdict.md` has `Verdict: READY_TO_PUSH`, and its `Head:` equals
   `git rev-parse HEAD`. An in-session verdict alone does not suffice.
5. **Branch code review:** a review file of this branch whose header has `Reviewer: code-reviewer`
   and `Verdict: APPROVED`, satisfying rule R, that either is of kind `code-review-branch`, or has
   a `Range:` whose base equals `git merge-base main HEAD` (the light lane).
6. **Security review:** required unless every path in the `files` section of `gates.txt` starts
   with `docs/` or `thoughts/`. It needs the file of kind `security-review` with
   `Verdict: SECURITY_CLEAR`, satisfying rule R. Its first five lines are exact; the `Verdict:`
   line carries exactly one of the three values:

   ```
   Verdict: PENDING | SECURITY_CLEAR | SECURITY_FINDINGS
   Reviewer: /security-review
   Scope: whole branch
   Range: <git merge-base main HEAD>..<head>
   Head: <sha>
   ```

   The verbatim `/security-review` output follows the header. The main session writes
   `Verdict: PENDING` when it saves the file and never writes any other value. Only the human
   changes the line, after reading the output. `PENDING` and `SECURITY_FINDINGS` both stop `/pr`.
7. **DB review:**
   - required when the plan's `Touches schema?` answer is yes, or the `files` section has a path
     under `db/`, `src/Envanex.Infrastructure/Migrations/` or `src/Envanex.Infrastructure/Persistence/`
   - needs at least one file of a `db-review-*` kind, and every such file (highest N per kind) has
     `Verdict: DB_APPROVED`
   - each such file's `Head:` passes step 1 of rule R and is an ancestor of HEAD
     (`git merge-base --is-ancestor <Head> HEAD` exits 0)
   - the newest db-review Head is the one every other db-review Head is an ancestor of
     (`git merge-base --is-ancestor <other> <this>` exits 0); if no single such Head exists, this fails
   - no path under `db/`, `src/Envanex.Infrastructure/Migrations/` or
     `src/Envanex.Infrastructure/Persistence/` appears in `git diff --name-only <newest db-review Head> HEAD`

## Procedure

1. Record the validated head: `git rev-parse HEAD`. Call it V. Every later step refers to V.
2. `git push -u origin <branch>`.
3. Compose the PR **title** as a Conventional Commit: `type(scope): summary`. This is the single
   commit that will land on `main` after squash — it matters more than any commit inside the branch.
4. Write the PR **body** to `TestResults/PR_BODY.md` with the Write tool (Read the file first if it
   exists):

   ```
   ## What
   (one paragraph)

   ## Why
   (link the plan: thoughts/shared/plans/YYYY-MM-DD_topic.md)

   ## Phases included
   ## Schema changes
   (migration name, or "none")
   ## Verdicts
   - <review file path> — <its Verdict: line> — <its Head: line>   (one per file preconditions 5-7 used)
   - TestResults/<slug>/tester-verdict.md — <Verdict:> — <Head:> — <Gates-sha256:>
   - <plan-review file path> — <its Verdict: line> (cited, not gated)
   ## Validation
   (the stdout of the last `/gate` run, verbatim)
   ## ADR
   (docs/adr/NNNN-title.md — or "none")
   ```

5. `gh pr create --title "<title>" --body-file TestResults/PR_BODY.md --base main`.
6. `gh pr checks --watch`. If CI fails, report the failure with file:line and STOP — the human
   routes the fix back to the coder. Do not fix it yourself.
7. Recheck before merging, and STOP if either check fails:
   - `gh pr view --json headRefOid --jq .headRefOid` prints V
   - `gh pr checks` exits 0, with every check passing
8. `gh pr merge --squash --delete-branch --match-head-commit V`.
9. `git switch main`, then `git pull --ff-only` (two calls).
10. Report the PR number, the squashed commit hash on `main`, and `git status --short`.

## Rules

- NEVER merge with red or pending CI.
- NEVER force-push, and NEVER use `gh pr merge --admin`. The deny list also blocks `git push`
  with `--force`, `-f` or a `+refspec`.
- Merge only with `--match-head-commit` set to V, the head the preconditions validated.
- Update `main` only with `git pull --ff-only`.
- NEVER merge a PR whose body cites a verdict that no file satisfying preconditions 3-7 carries.
- Evidence and doc commits land before the PR-end sequence. A commit after the tester ran
  invalidates its verdict (precondition 4). Re-run the tester.
- No agent writes `SECURITY_CLEAR` or `SECURITY_FINDINGS`. The saved security-review file starts as
  `Verdict: PENDING`, and only the human changes it.
- If the ADR file listed in the plan does not exist yet, STOP and tell the user to write it.
  The agents do not write ADRs.
~~~~

### Tests to add

None automated (plan:22990):
- A gate run takes minutes.
- The repository has no shell-test framework, and none is started.
- Hook code and hook tests do not change.

The scripts and contracts are proven by these named probes:
- **P3.1** — the positive control, matching its file.
- **P3.2** — a failure that only `final_status` can produce.
- **P3.3** — the rename.
- **P3.4** — `/mutate` end to end.
- **P3.5** — the new `/commit`.
- **P3.6** — the new tester.
- **P3.7** — `gate.sh`'s failure exits.
- **P3.8** — the mutation scripts' failure handling and HEAD comparison.
- **C3.1-C3.3**.

### Probes (fresh session after R3)

`GATES=TestResults/chore-agents-workflow-skills/gates.txt`. The human records with `run` in an external Git Bash.

**P3.3 (rename) and P3.1 (`/gate` matches its file; positive control).** On a clean tree at the Phase 3 code commit:
1. The human runs X5 (`ls .claude/skills`; no `verify`) and invokes `/gate`.
2. From the main-session transcript `T`, the human records two rows:
   - `grep -n -F 'bash .claude/skills/gate/scripts/gate.sh' "$T" | cut -c1-6000`, the tool_use row
   - the matching tool_result row, by its tool_use id
3. The human copies the record:

   ```
   cp "$GATES" TestResults/chore-agents-workflow-skills/validation/gates-p3.1-<HEAD7>.txt
   sha256sum "$GATES" TestResults/chore-agents-workflow-skills/validation/gates-p3.1-<HEAD7>.txt
   ```

   The two hashes are equal.
4. With `GC` pointing at that copy, the human runs `gate-checks.txt` with `K=gate-checks-p3.1`.

**P3.3 passes when:**
- the tool result's first line is `TestResults/chore-agents-workflow-skills/gates.txt`
- X5 shows no `verify`
- C3.1 is recorded

**P3.1 passes when:**
- G0-G11 are as expected
- the tool result's second line carries the same hash as G0
- its `steps:` block equals G2's ten lines
- it shows `dotnet test passed total: 638`

The two rows' timestamps give the run time (DV2).

**C3.2 (`/gate`).**
- The human runs `sed -n '<first>,<last>p' "$T" | grep -o -E '"(effort|perTurnEffort)":"[^"]*"' | sort | uniq -c`. `<first>` is the transcript line of the `/gate` invocation (`grep -n -F '/gate' "$T"`) and `<last>` that of its tool_result.
- The outcome is recorded as "observed at <line>: low", or as "set, not observable" with the values found. This is a named check, not pass or fail.

**P3.2 (`/gate` catches a failure through `final_status`).**
1. At the human's request, the main session applies, with Edit, at `.claude/hooks/tests/path-guard.test.js:59`:

   ```
     assertBlocked(runWrite('C:\\projects\\envanex\\obj\\guard-test.txt'), protectedSegment('obj'));
   ```

   becomes

   ```
     assert.equal(runWrite('C:\\projects\\envanex\\obj\\guard-test.txt').status, 0);
   ```

2. The human records `git diff -- .claude/hooks/tests/path-guard.test.js`. It shows exactly that one-line change.
3. The human runs `bash .claude/skills/gate/scripts/gate.sh; echo "gate=$?"`.
4. The human runs `grep -x 'exit=1 step=node kind=required' "$GATES"`.
5. The human runs `awk '/^\$ node --test \.claude\/hooks\/tests\/\*\.test\.js$/{f=1;next} /^exit=[0-9]+ step=node kind=required$/{f=0} f' "$GATES" | grep -E '^(# |ℹ )fail [0-9]+$'`.
6. The human copies the record to `gates-p3.2-<HEAD7>.txt`.
7. The main session applies the inverse Edit.
8. The human runs `git diff --exit-code -- .claude/hooks/tests/path-guard.test.js; echo "restored=$?"` and `git status --short`.

**P3.2 passes when:**
- stdout's last two lines are `gate-exit=1 failed=node` and `gate=1`
- step 4 prints that line
- step 5 prints `ℹ fail 1` (or `# fail 1`)
- step 8 prints `restored=0` and an empty status

Only `final_status` writes `failed=`. The contrast with P3.1's `gate-exit=0 failed=` ties the change to the mutation. No agent runs during P3.2.

**P3.7 and P3.8 (focused failure checks).**
- **Setup.** The human saves the script below verbatim as `TestResults/chore-agents-workflow-skills/validation/script-checks.sh`, with a quoted heredoc and only if absent, and records its sha256. With `E` set to `phase-3.md`, the human runs `run 'bash TestResults/chore-agents-workflow-skills/validation/script-checks.sh'` once.
- **Scope.** It touches only a scratch repository outside the project, which it creates and removes.
- **What is not simulated.** Base-check failure, and append, read and final-record failures. The code review checks that every write and read is checked and leads to exit 3. `gate.sh`'s final verification covers an incomplete record. Residual item (10) records what stays unexecuted.

```
S=/c/Users/jesus/AppData/Local/Temp/envanex-script-check
P=/c/projects/envanex/.claude/skills
echo "== setup"
test ! -e "$S"; echo "free=$?"
mkdir "$S" && cd "$S" || exit 9
git init -q -b scratch && printf 'a\n' > f.txt && git add f.txt && git -c user.name=check -c user.email=check@example.invalid commit -q -m init; echo "setup=$?"
echo "== P3.7 gate.sh"
(cd .. && git rev-parse --show-toplevel >/dev/null 2>&1; echo "parent-is-worktree=$?")
out=$(cd .. && bash "$P/gate/scripts/gate.sh" 2>&1); echo "a-notree rc=$?"; printf '%s\n' "$out"
git checkout -q --detach
out=$(bash "$P/gate/scripts/gate.sh" 2>&1); echo "b-detached rc=$?"; printf '%s\n' "$out"
git checkout -q scratch
test ! -e TestResults; echo "b-nothing-written=$?"
printf 'x\n' > TestResults
out=$(bash "$P/gate/scripts/gate.sh" 2>&1); echo "c-dir rc=$?"; printf '%s\n' "$out"
rm TestResults
mkdir -p TestResults/scratch && printf 'old\n' > TestResults/scratch/gates.txt && chmod a-w TestResults/scratch/gates.txt
test ! -w TestResults/scratch/gates.txt; echo "d-readonly=$?"
out=$(bash "$P/gate/scripts/gate.sh" 2>&1); echo "d-record rc=$?"; printf '%s\n' "$out"
cat TestResults/scratch/gates.txt
chmod u+w TestResults/scratch/gates.txt && rm -r TestResults
echo "== P3.8 mutation scripts"
out=$(bash "$P/mutate/scripts/mutate-run.sh" 2>&1); echo "e-usage rc=$?"; printf '%s\n' "$out"
out=$(bash "$P/mutate/scripts/mutate-clean.sh" ../x f.txt 2>&1); echo "f-label rc=$?"; printf '%s\n' "$out"
test ! -e TestResults; echo "f-nothing-written=$?"
printf 'u\n' > u.txt
out=$(bash "$P/mutate/scripts/mutate-clean.sh" chk u.txt 2>&1); echo "g-untracked rc=$?"; printf '%s\n' "$out"
rm u.txt
bash "$P/mutate/scripts/mutate-clean.sh" chk f.txt; echo "h-clean rc=$?"
printf 'b\n' > f.txt && git add f.txt
bash "$P/mutate/scripts/mutate-clean.sh" chk f.txt; echo "i-staged-same rc=$?"
printf 'a\n' > f.txt
bash "$P/mutate/scripts/mutate-clean.sh" chk f.txt; echo "j-staged-only rc=$?"
git reset -q
bash "$P/mutate/scripts/mutate-clean.sh" chk f.txt; echo "k-reset rc=$?"
printf 'c\n' > f.txt
bash "$P/mutate/scripts/mutate-clean.sh" chk f.txt; echo "l-unstaged rc=$?"
printf 'a\n' > f.txt
cat TestResults/scratch/mutations/chk.txt
chmod a-w TestResults/scratch/mutations/chk.txt
test ! -w TestResults/scratch/mutations/chk.txt; echo "m-readonly=$?"
out=$(bash "$P/mutate/scripts/mutate-clean.sh" chk f.txt 2>&1); echo "m-log rc=$?"; printf '%s\n' "$out"
chmod u+w TestResults/scratch/mutations/chk.txt
git checkout -q --detach
out=$(bash "$P/mutate/scripts/mutate-clean.sh" chk f.txt 2>&1); echo "n-detached rc=$?"; printf '%s\n' "$out"
git checkout -q scratch
rm -r TestResults && printf 'x\n' > TestResults
out=$(bash "$P/mutate/scripts/mutate-clean.sh" chk f.txt 2>&1); echo "o-dir rc=$?"; printf '%s\n' "$out"
rm TestResults
out=$(cd .. && bash "$P/mutate/scripts/mutate-clean.sh" chk f.txt 2>&1); echo "p-notree rc=$?"; printf '%s\n' "$out"
echo "== cleanup"
cd / && rm -rf "$S"; test ! -e "$S"; echo "removed=$?"
```

| Line | Expected |
|---|---|
| `free=`, `setup=` | `0`, `0` |
| `parent-is-worktree=` | non-zero. Otherwise case a is inconclusive and recorded so |
| `a-notree rc=` | `2`; the output is one line naming the work-tree failure, and no `gate-exit=` line |
| `b-detached rc=`, `b-nothing-written=` | `2`, `0` |
| `c-dir rc=` | `2` (`TestResults` is a file, so the directory cannot be made); no `gate-exit=` line |
| `d-readonly=` | `0`. Otherwise case d is inconclusive on this filesystem: `chmod` did not set the read-only attribute |
| `d-record rc=` | `3`; an `I/O error` line and no `gate-exit=` line; `cat` prints `old`, so the record was neither truncated nor written |
| `e-usage rc=` | `2`, with a `usage:` line |
| `f-label rc=`, `f-nothing-written=` | `2`, `0` |
| `g-untracked rc=` | `2` |
| `h-clean rc=` | `0` |
| `i-staged-same rc=` | `1`. The index and working tree both differ from HEAD; plain `git diff --exit-code` would have said 0 |
| `j-staged-only rc=` | `1` (the working tree equals HEAD, the index does not) |
| `k-reset rc=` | `0` |
| `l-unstaged rc=` | `1` |
| `cat …/chk.txt` | for each of h-l: `$ git diff --exit-code HEAD -- f.txt` and `$ git diff --cached --exit-code -- f.txt`, each followed by its `exit=` line |
| `m-readonly=` | `0`. Otherwise case m is inconclusive |
| `m-log rc=` | `3`, with an `I/O error` line |
| `n-detached rc=`, `o-dir rc=`, `p-notree rc=` | `2`, `2`, `2` |
| `removed=` | `0` |

**P3.4 (`/mutate`).** The human runs `/mutate` with:
- label `M1.1`
- file `.claude/hooks/lib/guard-common.js`
- mode `node .claude/hooks/tests/path-guard.test.js`
- the mutation: line 45 `  return rawPath.replace(/\\/g, '/').replace(/^\/([A-Za-z])(\/|$)/, '$1:/');` becomes `  return rawPath.replace(/^\/([A-Za-z])(\/|$)/, '$1:/');`

The run:
- **Purpose.** It re-runs M1.1's mutation only to exercise `/mutate`. It does not replace or re-assess the Phase 1 proof (plan:1654).
- **Preconditions.** A clean tree; no agent running.
- **Safety reading** (static, UNVERIFIED). With `CLAUDE_PROJECT_DIR` in the `C:/…` form (C1.6), the live project dir still normalizes, and the inverse Edit's target holds no protected segment. path-guard is therefore expected to allow the restore.
- **If the restore is blocked**, the human:
  1. restores the file in an external editor
  2. records `git diff --exit-code HEAD -- .claude/hooks/lib/guard-common.js; git diff --cached --exit-code -- .claude/hooks/lib/guard-common.js` (both exit 0)
  3. records P3.4 as failed at the restore step
- **Pass.** `TestResults/chore-agents-workflow-skills/mutations/M1.1.txt` holds, in order:
  - step 0's two clean checks, each with `exit=0`
  - `$ git diff HEAD -- .claude/hooks/lib/guard-common.js`, showing the one-line change
  - `$ node --test .claude/hooks/tests/path-guard.test.js`, with a red on a test's own assertion
  - step 4's two clean checks, each with `exit=0`

  Then `git status --short` is empty. The human records the log's sha256, and the Edit and Bash rows from the main-session transcript.

**P3.5 (`/commit`)** — the Phase 3 As built commit, the first commit after R3, made with the new `/commit`. Its message body contains the word `"quoted"` in double quotes. Right after it, before any other `/commit`, the human records:
- `git log -1 --format=%B | diff - TestResults/COMMIT_MSG.txt; echo "diff=$?"`. This is the original command, recorded as context; a difference of one trailing blank line is the tformat terminator (DV8).
- `git cat-file commit HEAD | sed '1,/^$/d' | diff - <(git stripspace < TestResults/COMMIT_MSG.txt); echo "msg-diff=$?"` must print `msg-diff=0`. Trailer lines added outside the file are noted, not a failure (plan:23023).
- `git log -1 --format=%B | grep -c '"quoted"'` must print at least 1.
- From the main-session transcript:
  - a Write to `TestResults/COMMIT_MSG.txt` (`grep -n -F 'COMMIT_MSG.txt' "$T"`)
  - exactly one Bash call whose command is `git commit -F TestResults/COMMIT_MSG.txt`
  - no PowerShell commit and no `-m`
- C3.2 for `/commit`, by the method above, on this invocation.

These are late records, so they go to `phase-4.md`.

**P3.6 (the new tester).** The Phase 3 per-phase tester (Probe protocol step 2.11) runs after retention, with HEAD H = the Phase 3 review-file commit. The records are late, so they go to `phase-4.md`:

```
git rev-parse HEAD
head -n 4 TestResults/chore-agents-workflow-skills/tester-verdict.md
sha256sum "$GATES"
head -n 1 "$GATES"
diff <(awk '/^\$ git diff --name-only main\.\.\.HEAD$/{f=1;next} /^exit=[0-9]+ step=files kind=info$/{f=0} f' "$GATES") <(awk '/^## Changed files$/{s=1;next} s&&/^```/{if(f)exit;f=1;next} f' TestResults/chore-agents-workflow-skills/tester-verdict.md); echo "list-diff=$?"
git diff --exit-code HEAD -- .claude/skills/gate/scripts/gate.sh; echo "gate-sh-clean=$?"
cp "$GATES" TestResults/chore-agents-workflow-skills/validation/gates-3-tester-<HEAD7>.txt && sha256sum TestResults/chore-agents-workflow-skills/validation/gates-3-tester-<HEAD7>.txt
```

**Pass:**
- the first command prints H
- `head -n 4` shows `Verdict:`, `Head: H`, `Branch: chore/agents-workflow-skills` and `Gates-sha256: <hash>`
- `sha256sum` gives `<hash>`, which also equals the second line of the tester's `gate.sh` tool_result
- the record's first line contains `head=H`
- `list-diff=0`
- `gate-sh-clean=0`. This is plan:565's known limit: no hash of the executed content is kept.
- `gate-checks.txt`, run on the copy with `K=gate-checks-3-tester`, shows G0-G11 as expected

**From the tester's transcript `T`** (found with the `agentType` grep):
- exactly one Bash tool_use whose command is exactly `bash .claude/skills/gate/scripts/gate.sh`, with its tool_result
- no Bash call of `node --test`, `dotnet format`, `dotnet build` or `dotnet test` (decision 5)
- the matching `bash-allowlist:tester` allow line: `grep -n -F '"hook":"bash-allowlist:tester"' TestResults/hook-log/hooks.jsonl | grep -F '<agent_id>'`, inside the call window
- the Write of `tester-verdict.md`, with its `path-guard` and `write-scope:TestResults` allow lines

A missing record is recorded as missing. The tester's verdict is a model report; a concrete claim is examined mechanically (plan:484).

### Validation

**Coder (in the turn).**
- **Syntax:** `bash -n .claude/skills/gate/scripts/gate.sh`, `bash -n .claude/skills/mutate/scripts/mutate-run.sh` and `bash -n .claude/skills/mutate/scripts/mutate-clean.sh`. Each exits 0 with no output.
- **Mutation-script smoke:**
  - `bash .claude/skills/mutate/scripts/mutate-run.sh` prints `usage:` and exits 2
  - `bash .claude/skills/mutate/scripts/mutate-clean.sh coder-smoke .claude/hooks/path-guard.js` prints both diff commands with `exit=0` and exits 0
- **One gate run:** `bash .claude/skills/gate/scripts/gate.sh`, with the Bash tool `timeout` 600000, and its stdout pasted.
  - The tree is dirty, so the status section is non-empty.
  - `gate-exit=0 failed=` is expected.
  - This one run executes the hook tests, format, build (coder.md:26) and .NET tests; no separate runs.
- **Status:** `git status --short`, as listed under Files.

**Tester (step 2.11).**
- Its prompt: this plan's path and `Phase 3`.
- In scope: tester.md steps 1-10, with step 5 against G2-G11's expectations, and the "both" commands of X5-X11.

**Human, after the tester:**
- `commands-3.txt` (X1-X12) with `K=validation-3`
- `gate-checks.txt` on the `3-tester` copy

## Phase 4: CLAUDE.md, roadmap, `docs/ai-workflow.md`, explainer.md; journal; PR end

### Human steps

- **H4.0** (as written, plan:23044-23049). Before the coder turn, the human reads https://code.claude.com/docs/en/memory and records three facts:
  - whether `@path` imports exist
  - whether `.claude/rules/*.md` files are supported, and when they load
  - whether subagents load `CLAUDE.md`

  The plan keeps everything in CLAUDE.md. If C4.1 fails, the plan goes back to the planner to move the Windows notes into a rules file.
- **H0.2:** `git ls-files --eol -- CLAUDE.md docs/roadmap.md .claude/agents/explainer.md`. Each line starts with `i/lf`.
- **H4.1 (fresh session after R4).** The human records, as a human observation, whether `/security-review` appears in the command autocomplete. Availability only, like H0.1.
- **C4.2 (before P4.4):** E1, `git remote -v`, shows `origin`.

### Files

- **`CLAUDE.md` — becomes exactly Appendix A** (DV10). Every item of plan:23053-23090 is present:
  - the in-scope Overview and the out-of-scope sentence
  - Commands with `dotnet tool restore`, the `export` line and `node --test`; no Worker line and no SOAP
  - Architecture with the empty-shell marks; Infrastructure without outbox
  - the Outbox bullet: dispatcher deferred, same-transaction invariant kept
  - Orchestration with:
    - the light lane
    - the per-phase review-file commit
    - the PR-end sequence with the security-review save and the human-only verdict
    - the tester's generated file list and the verdict-file naming
    - background-subagent completion and lasting permission answers
  - the Verification rules (7)
  - the Windows notes (the four F20 items)
  - the pointer to `docs/ai-workflow.md`
  - the explainer's journal numbering (decision 3)
- **`docs/roadmap.md` — modified.** Each edit is named by its current text at 2e8497a, with the line number as orientation only:
  - **Line 58**, `` | — | `fix(db)` — stop publishing the local SQL Server SA password | | ``, becomes `` | — | `fix(db)` — stop publishing the local SQL Server SA password | done (#14) | ``.
  - **Line 59**, `` | — | `chore(agents)` — enforce the agent pipeline's rules and tier models by risk | | ``, becomes `` | — | `chore(agents)` — enforce the agent pipeline's rules and tier models by risk | Phases 1–2 done (#15); Phases 3–4 in [this follow-up](../thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md) | `` (decision 12).
  - **Rows 197-198 are deleted:** the rows beginning `| The agent pipeline's rules are enforced only by prompts.` and `| Review verdicts are not persisted.`.
    - #15 closed the deny-and-hook part, the tiers and the persisted verdict files.
    - This follow-up closes `/gate`, `/mutate`, the `/commit` scope list, CLAUDE.md, the roadmap section, and `/pr` citing and gating the verdict files.
    - The Phase 4 code commit body says this and cites "#15" and "this follow-up". No commit cites a PR number not yet assigned.
  - **Two rows are appended** after the row beginning `| No test proves that the REST login and the form login share one rate-limit bucket.`:
    1. Exact: `` | Hook tests are not run in CI. `node --test .claude/hooks/tests/*.test.js` runs only locally and through `gate.sh` (the `/gate` skill and the tester). `normalizePath` is pure string logic, so a step on the `ubuntu-latest` CI job can be added without a rewrite | not scheduled — recorded | ``. R3-4 is resolved, because `gate.sh` exists.
    2. The residual record row below.
  - **`Each PR follows the same loop, and the human drives every transition — agents stop and wait.`** becomes `Each PR follows one of two lanes, and the human drives every transition — agents stop and wait.`
  - **The three lines inside the fenced diagram** (`researcher → planner → …`, `→ code-reviewer → …`, `→ explainer → …`) become exactly:

    ```
    feature lane    researcher → planner → plan-reviewer → coder (one phase at a time)
                    → code-reviewer → db-reviewer (if schema changed) → tester
    light lane      short plan file → coder (one phase at a time) → code-reviewer → tester
    before PR end   explainer → human writes the ADR
    PR end          whole-branch code-reviewer → /security-review → tester → /gate → /pr
    ```

    A blank line and one paragraph follow the closing fence: "In both lanes the human commits every review file before the tester runs, and every evidence, journal and ADR commit lands before the PR end. `/security-review` is skipped only when every changed file is under `docs/` or `thoughts/`, and `/pr` refuses a verdict file that is not bound to the current `HEAD`. [`docs/ai-workflow.md`](ai-workflow.md) explains each step."
  - **In the bullet `**Reviewers have no shell.**`**, the sentence `` Use `/verify`, which prints raw command output and never summarises. `` becomes `` Use `/gate`: its script writes the raw output to `TestResults/<branch-slug>/gates.txt` and prints a summary computed from that file, never composed by the model. ``, wrapped at about 100 characters.
  - **The paragraph beginning `Agent models: `planner``** (two lines) becomes `` Agent models and effort tiers: see [`docs/ai-workflow.md`](ai-workflow.md#roles-and-tiers). ``
  - **Residual record row (exact).** The roadmap row stays the residual record (Revision 6 decision 13, plan:293). `docs/ai-workflow.md` repeats the same items with details:

    `` | **Residual risks of the agent guards** — the residual record; [`docs/ai-workflow.md`](ai-workflow.md#residual-risks) repeats it with each item's detail, settling command and reopen trigger. (1) Threat model: the guards stop accidental destructive actions by a cooperative agent and are no boundary against code the model writes and runs itself (`dotnet run`, including file-based `dotnet run x.cs`, `dotnet test`, `dotnet build`, `node`, a script), which can call any denied command. (2) The Bash guards are string heuristics: a variable holding `git`, or script indirection, bypasses them; `gate.sh` and the `/mutate` scripts run commands no hook inspects, so what they write is bounded by their reviewed content, not by a hook. (3) coder-bash-guard blocks any command that names `git` as a word. (4) bash-allowlist splits on `\|` without regard to quotes. (5) path-guard does not see Bash writes. (6) Windows path aliasing, UNVERIFIED. (7) A quoted `user-secrets set` value is redacted only up to a `;`, `\|` or `&&`. (8) Agents can edit `.claude/`, including `settings.json`, which path-guard does not cover; `disableBundledSkills` and `disableClaudeAiConnectors` are no authorization boundary. (9) User-level settings sit outside the repository; `Bash(find *)` there allows `find -delete`. (10) Reviewed, not executed: the base-check failure path of `gate.sh`, and the append, read and final-record failure paths of `gate.sh` and the mutation scripts. (11) No allowlist rule for pipes: a tester pipeline's exit status is its last segment's. (12) Runtime tool access outside P2.6's two pairs is not verified. (13) 16 negative live paths of Phase 2 stay UNVERIFIED end to end. (14) Deny forms not covered: other global git options, `git.exe`, a flag after another flag, bare `git rebase`, some `git checkout` and `git branch` forms. (15) git long-option abbreviations, UNVERIFIED. (16) The `*.test.js` allow entry's wildcard crosses `..` (P1.13g2). (17) Matcher case: `git switch -C*` did not deny `git switch -c` (P1.13a). (18) Path-guard alias classes (8.3 names, junctions and links, drive-relative paths, case folding), UNVERIFIED. (19) Hook timeouts and regex cost, UNVERIFIED. (20) Redaction is pattern-based. (21) Open non-blocking review findings F2, F3, N1, N2, N4 and DBR-1; F4–F6, DBR-2 and R3-6 to R3-8 stay as their review files record | not scheduled — recorded | ``

    (`\|` is a table escape of `|`.) Item (10) extends the original "the base-check failure path of `gate.sh` is not probed" (plan:23104). That path stays unprobed. P3.7 and P3.8 now execute the start-up failures and the unwritable-record and unwritable-log cases; item (10) names the paths that only review covers.
- **`docs/ai-workflow.md` — created.** English, reader-facing. Sections from plan:23139-23161:
  1. **The pipeline and the light lane.**
     - Why the human drives every transition.
     - Why the security review applies in both lanes: fix(db) was a security fix with no `src/` change.
     - The per-phase review-file commit, and the PR-end sequence with the journal and ADR before it.
     - Journal numbers are a sequence independent of PR numbers.
     - `/security-review` is a built-in command, not a project skill. With `disableBundledSkills: true` it still appeared in the command list on 2026-10-13 (a human observation), and its availability is re-checked before each PR-end sequence.
     - `/pr` merges only the validated head (`--match-head-commit`), never with `--admin`, and updates `main` with `git pull --ff-only`.
  2. **Roles and tiers** (heading exactly `## Roles and tiers`, the roadmap's anchor). The table, from the frontmatter at the Phase 4 code commit (D6, X11):

     | Agent | Model | Effort |
     |---|---|---|
     | researcher | claude-opus-5-5 | xhigh |
     | planner | claude-opus-5-5 | max |
     | plan-reviewer | claude-opus-5-5 | max |
     | code-reviewer | claude-opus-5-5 | max |
     | db-reviewer | claude-opus-5-5 | max |
     | coder | claude-opus-5-5 | xhigh |
     | tester | claude-sonnet-5-5 | low |
     | explainer | claude-opus-5-5 | xhigh |

     - Each row also gives the tools and hooks of `.claude/agents/<agent>.md:4` and `:7-16`.
     - The principle is F3's: judges at max, executors at xhigh, mechanical steps at low.
     - The tester runs on Sonnet 5.5 at low (Revision 6 decision 1).
     - The coder stays at xhigh, because a usage-limit cutoff leaves a half-written phase. Raising it to max for one phase means editing `coder.md` in its own commit, then a restart, the phase, the revert commit, and another restart.
  3. **Threat model.** The two statements (plan:443-448) and what follows from them (plan:23143-23145).
  4. **The guards.**
     - The deny list, path guard, coder git block, tester and explainer allowlists, and write scopes.
     - The fixed-token dotnet entries and why: PreBuildEvent runs its command (C2.6: exec yes, plan:22780), plus `--results-directory` and `--report`.
     - The incidents behind them (plan:23147-23150).
     - What prompts on purpose (plan:23151-23155). Note: `git commit` is pre-approved only inside `/commit`.
     - The tester's one Bash write exception (`gate.sh`) and the mutation scripts, with their bounds.
  5. **Why hooks fail closed** — the exit-1 trap. Also why `gate.sh` and the mutation scripts fail non-zero on any I/O failure (exit 2 and 3).
  6. **What the hook log holds** — redaction rules 0-9, `SENSITIVE` and `VALUE`, `LIVEPROBE` tags, and test runs writing to a temp dir.
  7. **Verdict files and `/gate` evidence:**
     - the branch slug in the name, `Head:`, and Head rule R (SHA verification, git errors)
     - `gates.txt`'s header, sections, `exit=` lines and last line, and the empty `status` section
     - the tester inspects the record instead of re-running its commands
     - `Gates-sha256:` is recorded, not compared, because `/gate` re-runs after the tester
     - the security-review file's `Verdict: PENDING` until the human sets it
     - the newest-db-review check
     - the main-session writes are outside every write-scope hook; path-guard applies
     - exactly one historical sentence: "`/gate` replaces the project's earlier `/verify` skill."
  8. **How to re-run the probes.**
     - `node --test .claude/hooks/tests/*.test.js`, or `/gate`
     - the P1.x/P2.x lists in `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md` (as of 2e8497a)
     - the P3.x/P4.x lists in this plan
     - the difference between regression checks and historical proofs (the section of that name above)
  9. **Residual risks.** The roadmap row's items (1)-(21), in the same order and with the same names, each with its detail from plan:23095-23131 updated as follows:
     - (2) names `gate.sh` and the mutation scripts.
     - (10) says which failure paths P3.7 and P3.8 execute and which only review covers, the base-check failure path included.
     - (11) is in the present tense.
     - (16) and (17) cite plan:2772-2774.
     - (21) lists every open finding and its source file:
       - F2 and F3: `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-code-review.md`, runtime UNVERIFIED
       - N1, N2 and N4: `…-code-review-phase2-r2.md`
       - N3: supplemented (plan:22799); no action
       - DBR-1: `…-db-review-phase2.md`. `db-reviewer.md:45` checks the same-transaction rule only when a phase "adds an outbox". CLAUDE.md keeps the invariant unconditional while the dispatcher is deferred, so DBR-1's mitigation holds. Its proposed reword ("adds an outbox or writes to one") stays open until the first outbox work.
       - F4-F6, DBR-2 and R3-6 to R3-8: as recorded
     - It also names what this PR resolves:
       - R3-1, the roadmap rows
       - R3-2/N5, the explainer wording
       - R3-3, `gate.sh` exists at the allowlisted path and the settings entry completes the wiring
       - R3-4, the hook tests run through `gate.sh`
       - R3-5, `/pr` binds verdicts to HEAD
  10. **What is deliberately not used:** Fable, `superpowers`, memory plugins, `maxTurns`.
- **`.claude/agents/explainer.md` — modified, exactly three edits.** Line 3 (`description:`) is unchanged.
  - Line 22 becomes `Input: $ARGUMENTS (plan file path + journal number)`.
  - Lines 36-37 become:

    ```
    3. Write `docs/journal/NNNN-<slug>.md` using the structure below. NNNN is the journal number the
       caller gives, zero-padded to four digits: the next number after the highest `NNNN-` entry in
       `docs/journal/`. It is a sequence number, not the GitHub PR or roadmap number. If an entry with
       that number already exists, stop and report it; a number is never reused.
    ```

  - The template heading (old line 42, new line 44) becomes `# NNNN — <title>`.

Expected `git status --short` after the coder turn:

```
 M .claude/agents/explainer.md
 M CLAUDE.md
 M docs/roadmap.md
?? docs/ai-workflow.md
```

### Signatures

No code. The exact texts are above and in Appendix A.

### Tests to add

None (docs only, plan:23165). The checks are D1-D10, C4.1, C4.2 and H4.1.

### Probes and checks (fresh session after R4)

C4.1 = D1, P4.1 = D3, P4.2 = D4 and P4.3 = D5. The tier check is D6, against the `docs/ai-workflow.md` table. They run in the Phase 4 validation, with H4.1 recorded in the fresh session.

Then Probe protocol steps 2.6-2.11:
- the As built copy, which carries Phase 3's late records (P3.5, P3.6, review and tester correlations, validation logs)
- the review, `code-review-phase4`
- the review-file commit
- the Phase 4 tester
- the human's validation

### Learning journal (decision 11)

Order: after the Phase 4 tester's READY_TO_PUSH and the human's Phase 4 validation; before the final evidence commit and P4.4 step 1. A NEEDS_FIXES goes through the correction loop first. The tester's verdict is a model report, used here only as the trigger (plan:372).

1. The human records `ls docs/journal`. N is the highest four-digit prefix plus one, zero-padded (0010 if still free); `ls docs/journal/<N>-* 2>/dev/null | wc -l` prints `0`.
2. The explainer runs with this plan's path and journal number N. It writes `docs/journal/<N>-<slug>.md` under the R4 explainer contract.
3. The human records the explainer's Write and its `write-scope:docs/journal` allow line, and its Bash calls with their `bash-allowlist:explainer` lines.
4. The human commits the entry alone with `/commit` (`docs(journal): …`). The whole-branch review covers it.

### Final evidence commit

An append-only As built copy of Phase 4's late records: the review and tester correlations, the validation logs, the journal record. It is made with the same `cmp` check, and its HEAD is H_E.

### P4.4 (this PR's PR-end sequence: the first real use of `/pr` preconditions 3-7)

It starts on a clean tree at H_E, with C4.2 and H4.1 recorded.

1. **Whole-branch code review.** code-reviewer, with "plan path + `branch`", writes `thoughts/shared/reviews/<date>_chore-agents-workflow-skills-code-review-branch[-rN].md`. The caller supplies:
   - Range: `<git merge-base main HEAD>..H_E`
   - Head: H_E
   - the human's `git diff --name-only <merge-base> H_E`

   NEEDS_REVISION enters the correction loop.
2. **Security review.**
   - The human runs `/security-review`. This is its first execution for this branch.
   - The main session saves the output verbatim to `thoughts/shared/reviews/<date>_chore-agents-workflow-skills-security-review.md`, under the five-line header with `Verdict: PENDING`, Range `<merge-base>..H_E` and Head H_E. The human supplies the two SHAs from a `run` record; `git merge-base` has no allow entry in the main session.
   - The human reads the output and sets the `Verdict:` line in an external editor.
   - This PR touches `.claude/`, so it is not exempt.
   - SECURITY_FINDINGS enters the correction loop.
3. **Review-file commit.** The human commits both files in one `/commit`. HEAD is now H_R. `git show --name-only --format= H_R` lists exactly those two files.
4. **Tester.** After retention, the tester runs (`PR-end`) on H_R. The human makes the P3.6 records as its correlation, copies the record (`pr-end-tester`), and runs `gate-checks.txt` on the copy.
5. **`/gate`.** The PR-end sequence re-runs the gate on purpose, at the same head as the tester (plan:66). The human copies the record (`pr-end-gate`) and runs `gate-checks.txt` on it. G1 must show `head=H_R`; G3 and G11 must pass.
6. **Human validation and READY_TO_PUSH.** The human runs `commands-pr-end.txt` with `K=validation-pr-end` at H_R. Then, in `pr-end.md`, the human records READY_TO_PUSH and V = H_R. This is the human's decision.
7. **`/pr thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md`.**
   - preconditions 1-7; precondition 7 is not required here
   - V, then push
   - the body, with the files cited and step 5's summary pasted
   - `gh pr create`, `gh pr checks --watch`
   - the remote head and checks rechecked
   - `gh pr merge --squash --delete-branch --match-head-commit V`
   - `git switch main`, `git pull --ff-only`

   C3.2 for `/pr` is recorded from this invocation.

**If `/pr` stops:**
- **A missing or stale record** (for example a tester `Head:` that is not HEAD): redo from the step that produces it.
- **A defect in `pr/SKILL.md`:** the correction loop runs (coder, commit, restart), and P4.4 restarts at step 1.
- **No bypass.** No manual merge, `--admin` merge or force-push bypasses a failed precondition.

**The PR body is the evidence.** Its `## What` states:
- Phases 1–2 were delivered in #15, and Phases 3–4 here.
- The removal of the two Known-gaps rows, citing #15 and this follow-up.
- H0.1 and H4.1 are availability observations, distinct from step 2's review.
- The residual record's location.
- The carried non-blocking findings.

Title: `chore(agents): complete workflow skills and documentation`.

### Validation

**Coder (in the turn):**
- `dotnet build -warnaserror` (coder.md:26)
- D1, D2, D3, D4 and D10
- `git status --short`, as listed

**Tester:**
- `Phase 4`, then `PR-end`
- tester.md steps 1-10, with step 5 against G2-G11
- the "both" commands of X5-X11 and D1, D3, D4 and D6-D10

**Human:**
- `commands-4.txt` with `K=validation-4` after the Phase 4 tester, and `gate-checks.txt` on the `4-tester` copy
- at PR end, steps 4-6 above

## Rollback notes

- **Commits.** Each phase is a code commit, an As built commit and a review-file commit; late records land in the next As built copy. The human reverts with `git revert <sha>`, which is not denied. `git reset --hard` is denied by design (plan:23192).
- **Reverting Phase 3's code commit:**
  - restores `/verify`, the merged tester, coder, `/commit` and `/pr`
  - removes `/gate`, `/mutate` and the three allow entries, so the hook's `gate.sh` entry is dormant again (R3-3 reopens)
  - then restart
- **Reverting Phase 4's code commit:**
  - restores CLAUDE.md, the roadmap and explainer.md, and removes `docs/ai-workflow.md`
  - then restart
  - A reverted journal entry keeps its number consumed (no number is reused).
- **If a hook blocks everything:** follow plan:23193. Edit the file in an external editor, or set `"disableAllHooks": true` in `.claude/settings.local.json`; restart; record it.
- **If `gate.sh` exits 2 or 3 in the project:** the gate has not passed. The human fixes the cause (repository, branch, directory, permissions, disk) and re-runs. A record left by a failed run is never cited.
- **If P3.4's inverse Edit is blocked:**
  1. Restore `.claude/hooks/lib/guard-common.js` in an external editor.
  2. Record both HEAD diffs (exit 0).
  3. P3.4 fails at that step.
  4. No agent runs until the restore is recorded.
- **If the new `/commit` cannot write `TestResults/COMMIT_MSG.txt`:** the human commits with `git commit -F` in an external Git Bash and records it. P3.5 fails, and the correction loop follows.
- **If `/security-review` is unavailable or errors at P4.4 step 2:** precondition 6 cannot pass. `/pr` stops, and the human decides and records the decision. No agent writes a security verdict.
- **If `/pr` stops after `gh pr create`:**
  - The PR stays open and nothing merges.
  - A remote head that differs from V means a push happened outside `/pr`; the human investigates before any merge.
- **If C4.1 fails:** back to the planner to move the Windows notes into a rules file per H4.0 (plan:23049).
- **If the tester's dotnet allowlist blocks a needed flag:** plan:23196 applies unchanged. That is a hook change, so it is outside this plan.
- **Local state.**
  - `TestResults/chore-agents-workflow-skills/**` is local; deleting it is the human's choice.
  - `TestResults/chore-agents-hardening/**` is never touched.
  - The P3.7/P3.8 scratch repository removes itself (`removed=0`). Otherwise the human deletes `/c/Users/jesus/AppData/Local/Temp/envanex-script-check` by hand.

## Appendix A — CLAUDE.md after Phase 4 (exact)

The coder writes this text verbatim: the lines between the two fence lines, LF only, with a final newline. D2 checks it. By the planner's count it has 199 lines; C4.1's condition is ≤ 200.

<!-- APPENDIX-A-START -->
~~~~text
# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview

**Envanex** — an inventory ERP core: master data, an append-only stock ledger, moving-average
then FIFO costing, and the REST and grid surface that serves them. Purchasing, sales, invoicing,
SOAP, the worker and reporting are out of scope; see the Scope section of `docs/roadmap.md`.

Stack: .NET 10 (LTS) · Blazor Web App (Server interactive) · Radzen Blazor Components (MIT) ·
EF Core · SQL Server 2022 · `DevExtreme.AspNet.Data` (MIT) for grid data endpoints.

## Commands

```bash
docker compose up -d                          # SQL Server 2022 on 127.0.0.1:1433 (needs .env, copied from .env.example)
dotnet tool restore                           # the pinned EF Core CLI (dotnet ef)
dotnet build -warnaserror                     # build (warnings are errors)
dotnet test                                   # all tests
dotnet format --verify-no-changes             # style check
node --test .claude/hooks/tests/*.test.js     # hook tests
export ENVANEX_CONNECTION_STRING='Server=127.0.0.1,1433;Database=EnvanexDev;User Id=sa;Password=<password>;TrustServerCertificate=True'   # read by dotnet ef; see Windows notes
dotnet ef migrations add <Name> -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexDbContext
dotnet ef migrations add <Name> -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext --output-dir Migrations/Identity
dotnet ef database update -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexDbContext
dotnet ef database update -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext
dotnet run --project src/Envanex.Web          # Blazor UI + REST API + Scalar
```

After every `migrations add`, normalize the generated files to LF with no BOM. `dotnet ef` emits
CRLF with a UTF-8 BOM, which violates the LF rule in `.gitattributes` and fails
`dotnet format --verify-no-changes`. It is an encoding fix only — never touch the DDL.

## Architecture

```
src/
  Envanex.Domain          entities, value objects, domain events. ZERO project references.
  Envanex.Application     use cases, DTOs, validators. References Domain only.
  Envanex.Infrastructure  EF Core, SQL Server, repositories. References Domain + Application.
  Envanex.SoapApi         empty shell; scope cut, see docs/roadmap.md.
  Envanex.Web             THE deployable host — Blazor components, REST controllers, OpenAPI/Scalar.
  Envanex.Worker          empty shell; scope cut, see docs/roadmap.md.
tests/
  Envanex.Domain.Tests  Envanex.Application.Tests  Envanex.IntegrationTests (Testcontainers)
db/
  scripts/  views/  procs/          raw T-SQL not expressible in EF
docs/adr/                           one file per architectural decision
```

**Dependency rule:** dependencies point inward only. `Envanex.Domain` never references anything.
A build error caused by adding a reference to Domain is the correct outcome, not a problem to fix.

**One host on purpose.** Blazor UI and the REST API live in `Envanex.Web`, so the whole app is a
single deployment on the Azure free tier, separated by project boundaries and folders, not processes.

## Key Patterns

- **Result<T>, not exceptions**, for expected failures (validation, business rule violations).
  `Result<T>` and `Error` live in `Envanex.Domain.Common` so that aggregates can return
  domain errors directly. Exceptions are for bugs and infrastructure faults only.
-   A `null` reference passed where a value object is required is a caller bug, not a business
    rule violation: guard it with `ArgumentNullException.ThrowIfNull` even in a method that
    returns `Result`. An invalid *value* (an empty code, `Guid.Empty`, a negative factor) is a
    business rule violation and returns `Result.Failure`.
- **Aggregates own their invariants.** State changes go through methods on the aggregate root.
  All setters are `private set`.
- **Stock ledger is append-only.** `StockMovement` rows are never updated or deleted.
  `StockBalance` is a projection. Any change to stock is a new movement.
- **Costing is a strategy** (`ICostingStrategy`): moving average is the default, FIFO is the
  second implementation. Never inline a costing formula into a use case.
- **EF Core conventions:** money is `decimal(18,4)`, quantity is `decimal(18,6)`, every aggregate
  root has a `RowVersion` concurrency token (EF shadow property configured in Infrastructure),
  every FK is explicitly configured, no lazy loading.
- **Blazor components never touch EF or `DbContext`.** They call Application use cases. A
  component that injects `EnvanexDbContext` is a defect.
- **Radzen `RadzenDataGrid` is the standard grid.** Server-side paging via `LoadData`. Never load
  a full table and page client-side.
- **Grid data endpoints** under `/api/*/datasource` use `DataSourceLoader.Load` from
  `DevExtreme.AspNet.Data`. They exist so the same data protocol works for a DevExpress client.
- **Outbox pattern:** the dispatcher is deferred and does not exist yet (see `docs/roadmap.md`).
  Any outbox row is written in the same transaction as the business-state change it records.
- **All code is English** — identifiers, comments, ADRs, commit messages, test names. User-facing
  strings (UI labels, validation messages) are Turkish with correct characters (ş, ğ, ı, ö, ü, ç).
  One exception: `docs/journal/` is the human's study material and is written in Turkish.

## Orchestration (human-in-the-loop)

- Pipeline: researcher → HUMAN → planner → HUMAN → plan-reviewer → HUMAN → coder → HUMAN →
  code-reviewer → HUMAN → db-reviewer (only if the phase touched schema, migrations or queries)
  → HUMAN → tester → HUMAN. The human drives every transition. Agents STOP and wait.
- **Light lane:** a small docs, fix or chore PR with no schema change and no new public behavior
  skips the researcher, planner and plan-reviewer and starts from a short plan file. Its branch
  review is the code review whose `Range:` starts at `git merge-base main HEAD`.
- **Per phase, in both lanes:** the human commits each review file, whatever its verdict, before
  the tester runs; the tester's `Head:` is that commit. An uncommitted one makes the tester return NEEDS_FIXES.
- **PR-end sequence, in both lanes:** (1) whole-branch code-reviewer (light lane: the review above);
  (2) `/security-review`, skipped only if every changed file is under `docs/` or `thoughts/`; (3) the
  human commits the review files; (4) tester; (5) `/gate`; (6) `/pr`. Evidence, doc, journal and ADR
  commits land before (1); a commit after (4) means re-running the tester. In (2) the main session
  first saves the output verbatim to `thoughts/shared/reviews/YYYY-MM-DD_<branch-slug>-security-review.md`
  under the header in `.claude/skills/pr/SKILL.md`, with `Verdict: PENDING`. Only the human then sets
  `SECURITY_CLEAR` or `SECURITY_FINDINGS`, after reading the output; no agent writes either value.
- **coder**: ONE phase per turn, ends with `PHASE_COMPLETE` (or, if the plan leaves open a
  decision that affects user-facing behavior, a public contract, security or data integrity,
  stops without it and reports the decision; the human routes it to the planner), never commits,
  never pushes, and NEVER runs another agent. Every coder prompt must contain an explicit stop instruction
  ("do NOT run any other agent, do NOT commit, wait for me").
- **code-reviewer**: verdict only — `APPROVED` / `NEEDS_REVISION` with file:line. Writes only
  its verdict file under `thoughts/shared/reviews/`.
- **db-reviewer**: verdict only — `DB_APPROVED` / `DB_NEEDS_REVISION` with file:line.
  Never edits files. Skipping it on a schema-touching phase is a defect.
- **tester**: verdict only — `READY_TO_PUSH` / `NEEDS_FIXES` with file:line. Runs `gate.sh` and
  takes its file list from `gates.txt`; the main agent never types that list. NEVER edits files,
  and writes only under `TestResults/` — never via shell redirection.
- **explainer**: runs after the last phase's tester returns `READY_TO_PUSH`, before the ADR and the
  PR-end sequence. It writes only `docs/journal/NNNN-<slug>.md`, where NNNN is the next journal
  sequence number, not the PR number. It never writes ADRs or answers to its own questions.
- **Write scopes:** researcher, planner and the reviewers each write only to their own `thoughts/shared/`
  directory (`research/`, `plans/`, `reviews/`), enforced by hooks. Verdict files carry the branch slug and `Head:`.
- **Verdicts are terminal for the turn.** When any reviewer returns a verdict, the main agent
  MUST NOT edit or create any file (including plans and docs) and MUST NOT spawn any agent.
  `NEEDS_REVISION` -> human routes to planner. `NEEDS_FIXES` / `DB_NEEDS_REVISION` -> human
  routes to coder. The main agent applying "Required Changes" itself is a defect.
- When a subagent returns, the main agent's turn ENDS: output the verdict verbatim and stop. A verdict line is
  always the LAST content of the turn. "The next pipeline step" is never a reason to spawn an agent — the human
  advances the pipeline. A background subagent returns through its completion notification, not the launch
  result; its permission prompts surface here, and an answer that lasts beyond one call applies to the session.
- Execute only what the current human message asks for. One delegated step per turn.
- Model tiers, the guards and their residual risks: `docs/ai-workflow.md`.

## Git & PR Workflow

- `main` is protected. All work happens on `feat/<slug>`, `fix/<slug>` or `chore/<slug>` branches.
- Commits **inside** a branch are atomic and freely made — they exist for review readability.
- Merging is **squash merge**, so the PR title becomes the single commit on `main`.
  The PR title MUST be a Conventional Commit: `type(scope): summary`.
- `/commit` stages and commits on the current feature branch. It never pushes to `main`.
- `/pr` pushes the branch, opens the PR, waits for CI, squash-merges, deletes the branch,
  and returns to an updated `main`.
- Every PR that makes an architectural decision must add a file under `docs/adr/`.
  **The human writes ADRs, not the agents.** An agent may create the empty file with the title.

## Context Management

- `/compact` at ~55–60% context, especially before running an agent.
- Delegate investigation to the researcher subagent to keep main context clean.
- On compaction preserve: changed file list, active plan path, running commands, failed
  approaches and why they failed. Drop: contents of files read but not modified, intermediate
  error output, exploration noise.
- Keep phases small — one phase should validate one behavior.
- For manual debugging above 60% context: save a snapshot to
  `thoughts/shared/debug/YYYY-MM-DD_topic.md` (status, repro, expected vs actual, hypothesis,
  file paths), then `/clear` and reload only the plan phase + debug note + 1–2 files.
- Research and plan files are named `thoughts/shared/<kind>/YYYY-MM-DD_<slug>.md`. No numeric
    prefixes — the date is the ordering.

## Constraints

- Do not fix the same error more than twice — `/clear` and write a better prompt.
- Never edit `bin/`, `obj/`, `.vs/`, `.idea/`, `packages/`, `*.user`, `*.pfx`, or `*.Local.json`.
- Never put secrets in `appsettings.json`. Local secrets go in user-secrets.
- Never hand-edit a generated migration to change its intent — create a new migration.
- Never reference `Envanex.Infrastructure` from `Envanex.Domain` or `Envanex.Application`.
- No raw SQL in components, controllers or use cases. Raw T-SQL lives in `db/` and is called
  through a repository method.
- Every new endpoint, use case and component behavior gets a test. "It builds" is not validation.
- Files are LF-only (`.gitattributes` enforces this). Never commit CRLF.

## Verification rules

- A passing test is not a protecting test. Mutations run against committed code.
- Raw output is pasted, never narrated. Agents without a shell mark runtime claims UNVERIFIED.
- Re-run a reviewer only when it will learn something new. Code and docs go in separate commits.
- Record an outcome only once it exists; mark it pending until then.

## Windows notes

- `localhost` resolves to `::1` first, so with an IPv4-only port binding, connect to `127.0.0.1`.
- In Git Bash, double quotes still expand `!`, backticks and `\`. Single-quote literals.
- Git Bash rewrites a native program's argument that starts with `/` into a Windows path. For
  generated secrets, prefer hex to base64.
- The EF design-time factories (`EnvanexDbContextFactory.cs:12`, `EnvanexIdentityDbContextFactory.cs:12`)
  read `ENVANEX_CONNECTION_STRING`, not user-secrets; the hooks redact its password from their log.

## Common Failure Patterns — Never Do These

- Kitchen sink session: starting one task, asking something unrelated, returning. `/clear` first.
- Correcting over and over: wrong twice in a row -> `/clear` and rewrite the prompt.
- Over-specified CLAUDE.md: if this file grows past ~200 lines, rules start getting ignored.
- Trust-then-verify gap: never mark a phase done without build + test output.
- Infinite exploration: never investigate without scope. Use the researcher subagent.
- Migration drift: applying a migration, then changing the model without a new migration.

## Session Management

- `/clear` between unrelated tasks. `/compact <focus>` for scoped compaction.
- Name multi-session work with `/rename`. Resume with `claude --continue` / `--resume`.
~~~~
<!-- APPENDIX-A-END -->
