# Plan: chore(agents) — enforce the pipeline's rules and tier models by risk

Date: 2026-09-25. Branch: `chore/agents-hardening`. Research: `thoughts/shared/research/2026-09-24_agent-workflow-hardening.md` (F1-F21). Roadmap: the `chore(agents)` row and the two Known gaps rows at `docs/roadmap.md:197-198`. Carried-forward notes: `thoughts/shared/plans/2026-09-24_fix-db-compose-sa-password.md:47,74-76,85`. Reviews: `thoughts/shared/reviews/2026-09-25_chore-agents-plan-review.md` (NEEDS_REVISION) and `thoughts/shared/reviews/2026-09-25_chore-agents-plan-review-r2.md` (NEEDS_REVISION, on Revision 1). Phase 1 code review: `thoughts/shared/reviews/2026-09-26_chore-agents-code-review-phase-1.md` (NEEDS_REVISION on `f6df221..de44869`; applied as Revision 4).

## Revision 1

This revision applies the plan review. The human accepted all of the following:
- every ruling on the five pre-review points
- required changes 1-14
- the review's unmarked assumptions
- its list of probes that would pass with the guard missing

It also adds one human item. Everything the review did not question is unchanged.

### Required changes

| # | Required change | Plan section that now carries it |
|---|---|---|
| 1 | Separate the test log from the probe log | Phase 1 Signatures (`resolveLogDir`, `ENVANEX_HOOK_LOG_DIR`); `run-hook.js`; Phase 1 Tests (`does not write to the project hook log when the override is set`); Probe protocol step 3; C1.7 (`LIVEPROBE` targets) |
| 2 | P2.4 and P2.5 prove the hook fired | Phase 2 Probes P2.4 and P2.5 (tool result plus a matching log line; a refusal is inconclusive) |
| 3 | Bind verdicts to HEAD and to the branch | Phase 2 Body changes (reviewer header gains `Head:`; file name carries the branch slug; tester header); Phase 3 `pr/SKILL.md` preconditions 3-7 and Rules; Probe protocol step 1 (evidence commits land before review) |
| 4 | Redact before logging | Phase 1 Signatures (`redactSecrets`); Phase 1 Tests (`guard-common.test.js`, `path-guard.test.js`); M1.5, M1.6 |
| 5 | Tighten bash-allowlist | Phase 2 Signatures (token-wise, `node --test` regex, `--output`, quote-unaware split); Phase 2 Tests; M2.6, M2.7 |
| 6 | `normalizePath` as a pure string algorithm | Phase 1 Signatures; Phase 1 `guard-common.test.js`; Phase 2 write-scope tests (git-bash forms) |
| 7 | One exact frontmatter `hooks:` YAML example | Phase 2 "Frontmatter hooks block (exact)" |
| 8 | Fix coder-bash-guard | Phase 2 Signatures; Phase 2 Tests (the false-positive cases, `allows bash -c "git status"`); Phase 2 body change to `coder.md`; Phase 4 `docs/ai-workflow.md` Residual risks and the roadmap row |
| 9 | Point 1 ruling (security review in both lanes, computed exemption, merge-base Range, defined header) | Phase 3 `pr/SKILL.md` preconditions 5-6 and the security-review header; Phase 4 `CLAUDE.md` Orchestration |
| 10 | Point 2 ruling (`base-check`, both SHAs in the header) | Phase 3 `gate.sh`; Phase 1 Validation (the same check) |
| 11 | Make the P1.7 destructive probes safe | H1.2 (scratch dir); C1.5; Phase 1 P1.7 |
| 12 | `gate.sh` exit from one shared function; tester returns NEEDS_FIXES on a dirty status; `sha256sum` check | Phase 3 `gate.sh` (`run_step`, `final_status`, `tools-check`); Phase 3 `tester.md`; P3.2; C3.3 |
| 13 | Known-gaps row: hook tests not run in CI | Phase 4 `docs/roadmap.md` |
| 14 | Minor items 14-18 | 14: Phase 3 Files (`coder.md`) and Phase 3 "Mutation proofs after Phase 3". 15: M2.1 definition. 16: Probe protocol steps 1 and 3. 17: Phase 1 Signatures (synchronous log and stderr) and the test `block writes the reason to stderr before exiting`. 18: C1.3, P2.1, Phase 4 Residual risks |

### Rulings on the pre-review points

- **Point 1:** see required change 9.
- **Point 2:** see required change 10.
- **Point 3:** see required change 4. H1.1 also deletes the allow entry that holds an SA password literal.
- **Point 4:** see required change 8.
- **Point 5:** the non-goal is kept, `normalizePath` is pure string logic (required change 6), and there is a Known-gaps row (required change 13).

### Unmarked assumptions, now checked or tested

| Assumption | Now settled by |
|---|---|
| `--help` is honored after the destructive commands | C1.5, plus P1.7 running those commands in an empty scratch dir |
| `sha256sum` exists | C3.3, plus the `tools-check` step in `gate.sh` |
| stderr is flushed before exit | synchronous `fs.writeSync(2, …)` in `block`, plus the test `block writes the reason to stderr before exiting` |
| The form of `CLAUDE_PROJECT_DIR` | C1.6 (the `project_dir_raw` log field), plus `normalizePath` tests for all three forms |

### Probes that could pass with the guard missing

Each now has evidence that only the guard can produce:
- **P1.7:** Claude Code's deny text, no prompt, and no command output. The destructive commands run in an empty scratch dir.
- **P2.4 and P2.5:** a `write-scope:<dir>` or `bash-allowlist:<profile>` block line whose target carries a `LIVEPROBE-…` tag. Only a live hook can write that line (C1.7).
- **P3.2:** the line `gate-exit=1 failed=node`, which only `final_status` produces. It is compared with P3.1's `gate-exit=0 failed=`.

### Added human item

H1.1 deletes the `settings.local.json` allow entry that holds an SA password literal (`.claude/settings.local.json:14`).

### Interpretations for the re-review

- **A. Reviewer `Head:`.** Required change 3 says `Head:` must equal HEAD. Committing the review file itself always moves HEAD. So a reviewer or security-review file is accepted when its `Head:` equals HEAD, or when every path in `git diff --name-only <Head> HEAD` is under `thoughts/shared/reviews/`. The tester's `Head:` must equal HEAD exactly.
- **B. db-review `Head:`.** db-review runs per phase, so its `Head:` can't equal the final HEAD. `/pr` requires it to be an ancestor of HEAD (`git merge-base --is-ancestor`) and the file name to carry the branch slug.
- **C. The tester's `Gates-sha256:`.** It is recorded and cited, not compared with the current `gates.txt`, because `/gate` re-runs after the tester in the PR-end sequence. `/pr` instead checks that the `gates.txt` header `head=` equals HEAD and that its last line is `gate-exit=0 failed=`.
- **D. In-session verdicts.** An in-session tester verdict alone no longer satisfies `/pr`, because it can't be tied to HEAD. The file is required.
- **E. The security-review verdict.** The human sets the `Verdict:` line of the security-review file after reading the output. The saved output below the header is verbatim.
- **F. Additions beyond the review.**
  - Probe targets carry the tag `LIVEPROBE` rather than `probe`.
  - The git match is case-insensitive, because `GIT` runs on Windows.
  - There are extra mutation proofs (M1.5-M1.7, M2.6, M2.7) for the new branches, so that each new branch has a test that goes red.

## Revision 2

This revision applies the re-review (`thoughts/shared/reviews/2026-09-25_chore-agents-plan-review-r2.md`). The human accepted the following, as written:
- the rulings on deviations A-F, including the conditions attached to A, B, C, D and E
- required changes 1-11, with these choices:
  - change 2: add `merge-base` to coder-bash-guard's read allowlist, with the test `allows git merge-base main HEAD`
  - change 9: the fixed-token allowlist for `dotnet build`, `dotnet test` and `dotnet format`, with its tests and its mutation proof; PreBuildEvent is marked UNVERIFIED, with its settling command
  - change 11: both minor fixes

Everything the two reviews did not question is unchanged.

### Required changes

| # | Required change | Plan section that now carries it |
|---|---|---|
| 1 | Commit the review file between code-reviewer and tester; the tester's `Head:` is that commit | Probe protocol step 1 (sub-steps 9-11 and the paragraph below them); Phase 4 `CLAUDE.md` Orchestration (addition P) |
| 2 | `merge-base` in coder-bash-guard's read allowlist | Phase 2 Signatures (coder-bash-guard step 4); Phase 2 Tests (`allows git merge-base main HEAD`, plus `allows the Phase 1 base-check command line`); Phase 2 Validation |
| 3 | `redactSecrets`: quoted values, sqlcmd `-P`, simpler user-secrets rule | Phase 1 Signatures (`redactSecrets` rules 1-4); Phase 1 Tests (`redacts a quoted MSSQL_SA_PASSWORD assignment`, `redacts the sqlcmd -P value`, `redacts user-secrets set with a --verbose flag before the key`, and additions); M1.8, M1.9; Phase 4 Residual risks (a value containing `;`) |
| 4 | Target source for Bash hooks | Phase 1 Signatures (`getCommand`, and the `extractTarget` parameter of `runGuard`); Phase 1 Tests (`getCommand returns tool_input.command for a Bash-shaped input` and two more); Phase 2 Signatures (intro) and Tests (input shape) |
| 5 | Precondition 7: no schema path after the newest db-review | Phase 3 `pr/SKILL.md` precondition 7 |
| 6 | Precondition 3: the `status` section of `gates.txt` is empty | Phase 3 `pr/SKILL.md` precondition 3 |
| 7 | Security-review file starts as `Verdict: PENDING`; only the human changes it | Phase 3 `pr/SKILL.md` precondition 6 and Rules; Phase 4 `CLAUDE.md` PR-end step 2; Phase 4 `docs/ai-workflow.md` "Verdict files" |
| 8 | "verify all four" becomes seven; a `git` error in rule R fails the precondition | Phase 3 `pr/SKILL.md` (heading, Definitions: Head rule R, and the "git errors" rule) |
| 9 | Fixed-token allowlist for the tester's dotnet entries; tests; mutation proof; PreBuildEvent UNVERIFIED | Phase 2 Signatures (tester profile, "dotnet entries"); Phase 2 Tests; M2.8; C2.6; P2.5 (item 5f) |
| 10 | Residual risk: Windows path aliasing, UNVERIFIED | Phase 4 `docs/roadmap.md` residual-risks row and `docs/ai-workflow.md` Residual risks |
| 11 | `tools-check` loops over the names; `final_status` regex anchored to the full line | Phase 3 `gate.sh` (`tools_check`, `run_step`, `final_status`) |

### Rulings on the six deviations, and where their conditions now live

- **A (reviewer `Head:`):** accepted. Condition: a `git diff` or `git merge-base` error, including an unknown SHA, fails the check. See required change 8 (Head rule R, step 1, and the "git errors" rule).
- **B (db-review `Head:` as an ancestor):** accepted. Condition: no schema path may change after the newest db-review. See required change 5.
- **C (`Gates-sha256:` recorded, not compared):** accepted. Condition: precondition 3 also checks the `status` section. See required change 6.
- **D (tester verdict file required):** accepted. Condition: the heading "verify all four" becomes seven. See required change 8.
- **E (the human sets the security `Verdict:`):** accepted. Clarification: the main session writes `Verdict: PENDING`. See required change 7.
- **F (additions):** accepted; unchanged.

### Interpretations and additions for the next review

- **G. Target extractor.** Required change 4 allowed either `?? tool_input.command` in `getTargetPath` or a target extractor in `runGuard`. The plan uses the extractor. With it, a path hook attached to the Bash matcher by mistake fails closed, rather than treating a command string as a path. The test `getTargetPath throws on a Bash-shaped input` pins that down.
- **H. Redaction details beyond the review.**
  - The user-secrets rule starts at the first `set` token after `user-secrets`, not only directly after it. That also covers options placed before `set`, which finding 3(c) names.
  - Added tests:
    - `redacts user-secrets set when options precede set`
    - `redacts user-secrets set only up to the next &&`
    - `redacts a double-quoted SA_PASSWORD assignment`
  - Because the user-secrets rule stops at `;`, `|` or `&&`, a quoted value containing one of those is only partly redacted. The rest of the value is covered only by the other rules. This is recorded as a residual risk.
- **I. `-P` is case-sensitive.** In sqlcmd, `-p` is a different option.
- **J. The precondition 7 trigger is aligned.** The trigger gains `src/Envanex.Infrastructure/Persistence/`, the same set of paths the post-review diff check uses. The "newest db-review Head" is defined as the Head that every other db-review Head is an ancestor of. If no single such Head exists, the precondition fails.
- **K. Rule R verifies the SHA first:** `git rev-parse --verify --quiet "<Head>^{commit}"`.
- **L. Dotnet allowlist details.**
  - One shared token list serves all three entries. A flag that doesn't apply to a command makes that command fail harmlessly.
  - The `-v`/`--verbosity` values are listed explicitly.
  - The `--filter` argument must not start with `-` or `/`.
  - Added block tests: `-o`, `-bl`, `--no-build`, `--filter -p:…` and a `..` project path.
  - P2.5 gains a tester item, `5f`.
  - C2.6 runs the review's echo command, plus a file side effect, so the result doesn't depend on log verbosity.
- **M. `gate.sh` robustness, which follows from required change 11.**
  - `run_step` evaluates its command with `eval`, so `tools_check` and `base_check` can be script functions.
  - `run_step` guarantees a newline before its `exit=` line.
  - `final_status` requires exactly one anchored `exit=` line for each required step; a missing or duplicated line counts as a failure. Without this, the anchored regex would miss an `exit=` line glued to step output that lacks a final newline, and the gate would pass.
- **N. Extra mutation proofs M1.8 and M1.9** for the new redaction branches, following Revision 1's addition F. M2.8 is required by change 9.
- **O. `allows the Phase 1 base-check command line`.** This tests finding 2's exact command string, alongside the required test.
- **P. The general pipeline rule.** The rule from required change 1 also goes into `CLAUDE.md` Orchestration: the human commits each review file before the tester runs. Without it, every future feature phase would hit finding 1: a dirty `status` section, so NEEDS_FIXES.

## Revision 3

These seven edits are the r3 recipe from the plan-reviewer's focused review of Revision 2. As the human decided, they are applied without a further review round.
1. `gate.sh` `run_step`: a newline is appended only to non-empty output that lacks one, so an empty section is the `$ <command>` line immediately followed by the `exit=` line.
2. `/pr` precondition 3: the line after `$ git status --short` must be exactly `exit=0 step=status kind=info`.
3. Tester steps: the `status` section is not empty when the line after `$ git status --short` is not `exit=0 step=status kind=info`.
4. P3.1: a new bullet checks, with `grep -A1`, that the line after `$ git status --short` in `gates.txt` is exactly `exit=0 step=status kind=info`.
5. `redactSecrets`: every rule replaces every occurrence (global flag).
6. Phase 1 redaction tests: a new test, `redacts every Password occurrence`.
7. `--filter`: its argument may start with neither `-` nor `/`. This applies in the dotnet token list and in Revision 2's note L, and the single `--filter -p:…` block test becomes two tests, for `-p:` and `/p:`.

## Revision 4

This revision applies the Phase 1 code review (`thoughts/shared/reviews/2026-09-26_chore-agents-code-review-phase-1.md`, NEEDS_REVISION), with the human's decisions. Everything the review did not question is unchanged. The Phase 1 text stays as the record of what `ac397c9` and `726338a` built. Where they differ, the new "Phase 1 amendments" subsection wins.

### Threat model (every decision below follows from it)

- The guards stop accidental destructive actions by a cooperative agent.
- They are not a boundary against code the model writes and runs itself (`dotnet run`/`test`/`build`, `node`). That class is a residual risk.

What follows from it:
- An allow entry that reopens a deny is removed, not patched with more denies.
- The deny list covers the forms a cooperative agent commonly types. Every other form is a named residual risk.
- Redaction covers the secret forms this repository's commands and files actually carry.

### Items and where they now live

| # | Item | Decision | Where it lives now |
|---|---|---|---|
| 1 | Threat model | stated | Goal ("Threat model"); Phase 4 `docs/ai-workflow.md` section "Threat model" |
| 2 | Finding 1: two block tests pass on the wrong guard error | coder-level | Phase 1 amendments A1; M1.10, M1.11 |
| 3 | Finding 2: `decide` not awaited | coder-level | A2 (signature, fixture, two tests); M1.12 |
| 4 | Finding 3: `NotebookEdit` untested | coder-level | A3 (two tests); M1.13; P1.15 |
| 5 | Finding 4: early guard errors write no log line | coder-level | A4 (`resolveLogDir` fallback to `CLAUDE_PROJECT_DIR`, spawned test); M1.14; P1.14 |
| 6 | Finding 5: fallback log dir built from the lowercased project dir | coder-level | A5 (`resolveLogDir(projectDirRaw)`); M1.15, M1.16, M1.27 |
| 7 | UNC and device-path tests | coder-level | A6 (three tests); M1.17 |
| 8 | Finding 6: `git switch *` reopens the switch denies | allow entry removed | B1 (`git switch -c *`, `git switch main`); B2 entries 4-9; P1.11d-i; P1.13a-b; Phase 3 `pr/SKILL.md` `allowed-tools` (interpretation U) |
| 9 | Finding 7: `dotnet ef database update *` | allow entry removed; now prompts | B1; P1.13c; Phase 4 `docs/ai-workflow.md` "What prompts on purpose" |
| 10 | Finding 8: `dotnet new *`, and `--output` on `git log`/`diff`/`show` | `dotnet new *` removed; `--output` denied; `git log *`/`diff *`/`show *` kept | B1; B2 entries 14-19; P1.11n-s; P1.13d, P1.13h |
| 11 | Finding 9: `node --test .claude/hooks/tests/*` | narrowed to the exact test files plus the literal gate command | B1; P1.13e-g; Phase 2 Files (exact entries for the Phase 2 test files); Phase 4 residual "code the model runs" |
| 12 | Finding 10: deny patterns miss common forms | `git -C *` and `git -c *` denied entirely, plus the listed forms; the rest are named residual risks | B2 entries 1-27; P1.11a-aa (Bash); P1.12 (PowerShell); C1.9 (mirror check); Phase 4 residual list "Deny forms not covered" |
| 13 | Finding 11: redaction gaps | a rule, a test and a mutation proof for every form, before Phase 2 logs commands | A7 (rules 0, 1 amended, 5-9); 16 tests; M1.18-M1.26 |
| 14 | Finding 12: path-alias classes | residual risk, UNVERIFIED, with a settling command | Phase 4 roadmap row and `docs/ai-workflow.md` Residual risks |
| 15 | Finding 13: hook timeout, rule 3 backtracking | residual risk, UNVERIFIED, with a settling command | same as 14 |
| 16 | New probes and checks | added | H1.3, C1.8, C1.9, P1.11-P1.15 (Phase 1 amendments) |

### Interpretations and additions for the next review

- **Q. `docker compose * down -v*` rather than one entry each for `-f` and `-p`.** One wildcard entry covers `-f` and `-p`, plus `--file`, `--project-name`, `--env-file` and `--profile`. The probes exercise `-f` (P1.11t) and `-p` (P1.11u). The same applies to `docker-compose`.
- **S. Paired `--output` denies.** `git diff --output*` covers the flag in first position, and `git diff * --output*` covers it anywhere later. It is not established whether `*` matches an empty string, so both forms are listed.
- **T. First-position switch forms.** `git switch --force*` and `git switch -C*` are added next to their after-a-positional forms. The existing prefix denies cover only `-f` and `--discard-changes`.
- **U. `/pr` `allowed-tools`.** The skill's own allow list had the same hole as finding 6. Phase 3 narrows `Bash(git switch *)` to `Bash(git switch main)` and `Bash(git branch *)` to `Bash(git branch --show-current)`. `/commit` already uses `Bash(git switch -c *)`.
- **V. Rule 1 errs toward over-redacting.** A quote directly after a value (a mid-value quote, or the closing quote of an enclosing string) extends the redaction to the matching quote or to the end of the string. This is a lexical ambiguity, and the rule resolves it toward redacting more. The log only loses context, never a secret.
- **W. Rule 0 applies to every rule.** Line continuations are joined before all rules run, not only before rule 3.
- **X. Case sensitivity of the rule matcher is UNVERIFIED.** If the matcher folds case, the deny `git switch -C*` would also deny the allowed `git switch -c *`. P1.13a settles it, and Rollback notes give the fallback. `git branch -D*` could then also deny `git branch -d`. No allow entry or skill uses `-d` (`/pr` deletes the branch through `gh pr merge --delete-branch`), so that outcome is only recorded.
- **Y. `Bash(node --test .claude/hooks/tests/*.test.js)` still contains a rule wildcard.** It is the literal gate command, but to the matcher its `*` is a wildcard and may cross `/` and `..`. P1.13g records whether it does. Either way the risk falls inside the threat model's residual class.
- **Z. `git switch main` is not probed live.** Switching branches mid-session swaps `.claude/` under the running session: the hook scripts don't exist on `main`, and settings may be reread (F21). P4.4 exercises it through `/pr` step 7.
- **Z2. `git branch -D*` only.** As decided, there is no after-a-positional form. `git branch <b> -D`, `--delete --force`, `-df`, `-f` and `-M` are residual risks.
- **Z3. Bare `git rebase`** stays a residual risk, as decided. Denying it would take one entry pair, `Bash(git rebase)` and `PowerShell(git rebase)`, if the human wants to reconsider.

Human decisions (2026-09-26): Q, U, V and Z3 accepted as written; X and Z stand; no plan-review round for Revision 4, because the amendments' code review and probes check the same specifics.

---

## Goal

Turn the pipeline's hard rules into things that are enforced, not just written in prompts:
- a permission deny list, mirrored for Bash and PowerShell
- a narrowed allow list
- a Windows-safe path guard that fails closed and redacts secrets from its log
- per-agent PreToolUse hooks:
  - a git-write block for the coder
  - Bash allowlists for the tester and explainer
  - one write scope per agent
- the model and effort tiers from F3's final table, pinned by full model ID
- write-to-file contracts and persisted verdict files (F4, F11), bound to the branch and to HEAD
- LSP and Microsoft Learn tools for the read-only agents (F10)

On top of that:
- a scripted `/gate` that replaces `/verify`
- a `/mutate` skill
- a fixed `/commit` recipe
- a generated file list for the tester
- `CLAUDE.md`, the roadmap and a new `docs/ai-workflow.md` brought up to date

Every guard is proven by a probe with raw output. A probe is a mutation proof: it shows the guard blocks what it should and allows what it should. Each probe's evidence is something only the guard can produce.

### Threat model

- The guards stop accidental destructive actions by a cooperative agent.
- They are not a boundary against code the model writes and runs itself (`dotnet run`/`test`/`build`, `node`). That class is a residual risk (Phase 4).

Every guard decision in this plan follows from this model. Allow entries that reopen a deny are removed, not patched. The deny list covers the forms commonly typed, and every other form is a named residual risk. Redaction covers the secret forms this repository actually carries.

## Non-goals

- No product change. `src/`, `tests/`, `db/`, the migrations and `.github/workflows/ci.yml` stay untouched. `dotnet test` stays at **638**.
- Fable is not used (Q1). The bundled `/verify` is not evaluated (Q4).
- The PostToolUse `dotnet format` hook stays as it is. Probing a rewrite would mean editing a `.cs` file under `src/`.
- No `maxTurns` on any agent. A turn cap on the coder recreates the half-written-phase failure F3 describes; on a reviewer it cuts the review short.
- No `permissions.defaultMode` change. The guarantees come from deny rules and hooks, and the probes run in whatever mode the human normally uses (C1.1).
- Hook tests are not added to CI. They run locally and through `gate.sh`. This is recorded as a Known-gaps row (Phase 4). `normalizePath` is pure string logic, so a CI step on ubuntu can be added later without a rewrite.
- The coder's per-phase max override (F3) is documented in `docs/ai-workflow.md`, not automated.
- `gate.sh` never runs `git fetch`.
- Windows path aliasing (a trailing `.` or space, NTFS stream suffixes) is not handled by `normalizePath`. The same holds for the alias classes from the Phase 1 code review: 8.3 short names, junctions and links, drive-relative paths and case folding. Each is recorded as a residual risk, marked UNVERIFIED with its settling command (Phase 4).
- The guards are no boundary against code the model writes and runs itself (Goal, "Threat model"). Closing that gap needs a sandbox, not more patterns.

## Touches schema? (yes/no — db-reviewer required if yes)

No. db-reviewer is not required.

## ADR needed?

No (Q5): this is process, not product architecture. `docs/ai-workflow.md` takes its place (Phase 4).

## Probe protocol (applies to every phase)

1. **Order within a phase:**
   1. The coder implements, ending with PHASE_COMPLETE.
   2. The human runs `/commit`.
   3. A separate coder turn runs the mutation proofs against that commit.
   4. The human restarts Claude Code.
   5. The probes run in the fresh session. The main session appends raw evidence, as it happens, to `TestResults/chore-agents-hardening/evidence/phase-N.md`. `TestResults/` is gitignored, so the tree stays clean for later probes.
   6. Cleanup, then `git status --short` is empty.
   7. The main session copies the evidence file into this plan under `## As built — Phase N`.
   8. The human commits that as a separate `docs(agents)` commit.
   9. code-reviewer runs on the evidence commit. Its `Head:` is that commit.
   10. **The human commits the review file, whatever its verdict.** This keeps the tree clean for the tester's `status` section and for the next phase's probes. In Phase 1 the reviewers still work under their old contract and write no file, so this step does nothing there.
   11. The tester runs on the review-file commit. **The tester's `Head:` is that commit** (in Phase 1, the evidence commit). The code-review file's `Head:` still satisfies Head rule R, because the only paths changed since it are under `thoughts/shared/reviews/`.

   A failing probe sends the phase back to the coder before any review. A NEEDS_REVISION from code-reviewer does the same, after step 10. Every evidence and doc commit lands before the reviewers run and before the PR-end sequence. The only commit between code-reviewer and tester is the review-file commit from step 10. Any other commit after the tester ran invalidates its verdict (Phase 3, `/pr`).
2. **Preconditions:** `git status --short` is empty, and `git rev-parse HEAD` is recorded.
   - Every probe command is chosen so that, if the guard fails, it does no harm on a clean tree: `-h`/`--help`, `--dry-run`, `-n`, and `git stash` on a clean tree.
   - The three probes whose `--help` behaviour is UNVERIFIED (C1.5) also run inside the empty scratch dir from H1.2.
   - No probe command contains a secret.
3. **Evidence is raw:**
   - the tool result text, exactly as returned
   - the hook-log lines for that probe: `grep 'LIVEPROBE-<id>' TestResults/hook-log/hooks.jsonl`
   - `git status --short`

   Every probe target carries a `LIVEPROBE-<id>` tag. No test file contains the string `LIVEPROBE` (C1.7), and tests log to a temp directory (`ENVANEX_HOOK_LOG_DIR`), so a `LIVEPROBE` line in the project log can only come from a live hook. Log lines are redacted by the hook itself (Phase 1).

   An agent's description of the result is not evidence. An agent that refuses without calling the tool makes the probe **inconclusive**: it is re-prompted, not recorded as a pass. An outcome is recorded only once it exists, and stays marked `pending` until then (F19).
4. **Cleanup:** probe files are deleted. `git status --short` must be empty again, and `ls obj/LIVEPROBE-*` must answer "No such file".
5. **Restart:** quit and start again with `claude` in `C:\projects\envanex`, and record `claude --version`. When Claude Code rereads configuration mid-session is not tested (F21). The restart avoids the question.

## Named checks (UNVERIFIED items that aren't behavioural probes)

| ID | Check | Where |
|---|---|---|
| C1.1 | Record the permission mode shown in the prompt footer during the Phase 1 probes. Deny probes run in that mode; allow probes (P1.9) run in `default` mode. | Phase 1 |
| C1.2 | `node --version` ≥ 20, so `node:test` is stable. No `package.json` with `"type":"module"` in the repo root or `.claude/` (none today). | Phase 1 |
| C1.3 | `echo "${CLAUDE_CODE_EFFORT_LEVEL-unset}"` prints `unset`. No `maxEffortLevel` in any of the three settings files. Record that `C:\Users\jesus\.claude\settings.json:62-66` sets `modelSettings.claude-opus-5-5.effortLevel: "medium"`; P2.1 confirms that frontmatter `effort` overrides it. | Phase 1, P2.1 |
| C1.4 | `grep -c $'\r'` is 0 for every new `.js`/`.sh` file. After staging, `git ls-files --eol <file>` shows `i/lf`. | Every phase |
| C1.5 | `--help` after the destructive commands (UNVERIFIED, never relied on). See the steps below this table. Every outcome is harmless there. | Phase 1 |
| C1.6 | The form of `CLAUDE_PROJECT_DIR` on Windows: the `project_dir_raw` field of P1.1's log line, recorded as `C:\…`, `C:/…` or `/c/…`. All three are unit-tested; the check records which one runs. | Phase 1 |
| C1.7 | `grep -c LIVEPROBE .claude/hooks/tests/*.js .claude/hooks/tests/fixtures/*.js` prints 0 for every file. | Every phase |
| C1.8 | The behaviour of the Revision 4 probe commands when run for real (UNVERIFIED, never relied on): whether `-h`/`--help` is honored late, and whether `docker-compose` and `dotnet-ef` exist. See the steps below this table. Every outcome is harmless there. | Phase 1 amendments |
| C1.9 | Mirror check: every `Bash(<p>)` deny entry has a `PowerShell(<p>)` twin, and the reverse. The command below prints `mirror-ok 47`. | Phase 1 amendments, every later phase |
| C2.1 | Workspace trust was accepted for `C:\projects\envanex`, which frontmatter hooks require (F1). The path guard firing in fix(db) implies it; record the `/status` or trust state. | Phase 2 |
| C2.2 | `/agents` lists all eight project agents with no load error. This proves the YAML frontmatter parses; that the hooks are nested correctly is proven by P2.2, P2.4 and P2.5. | Phase 2 |
| C2.3 | The hook log shows whether `agent_type`/`agent_id` appear in hook input for subagent calls. Record present or absent. | Phase 2 |
| C2.4 | Order of deny rule and PreToolUse hook: on the coder's `git stash list --grep=LIVEPROBE-2e`, record whether a `coder-bash-guard` line with that target appears in the log. | Phase 2 |
| C2.5 | `grep -n "model: haiku" .claude/agents/*.md` returns nothing, which makes F3's "Haiku ignores effort" moot. | Phase 2 |
| C2.6 | Whether `dotnet build -p:PreBuildEvent=<cmd>` runs `<cmd>` (UNVERIFIED; the tester allowlist blocks `-p:` either way, so the result only records whether the risk was real). See the steps below this table. | Phase 2 |
| C3.1 | `ls ~/.claude/skills` has no `commit`, `pr`, `gate`, `mutate` or `verify` (F5). | Phase 3 |
| C3.2 | Whether a skill's `effort: low` can be observed while it runs. Record "observed at …" or "set, not observable". | Phase 3 |
| C3.3 | `command -v sha256sum` prints a path in Git Bash. `gate.sh`'s `tools-check` step fails the gate otherwise. | Phase 3 |
| C4.1 | `wc -l CLAUDE.md` ≤ 200. | Phase 4 |
| C4.2 | `git remote -v` shows `origin`, which `/security-review` needs. | Phase 4 |

C1.5 steps: the human runs these in an external Git Bash inside `SCRATCH=/c/Users/jesus/AppData/Local/Temp/envanex-probe-empty` (H1.2).
1. `echo "${COMPOSE_FILE-unset} ${COMPOSE_PROJECT_NAME-unset}"` must print `unset unset`.
2. `for d in "$SCRATCH" /c/Users/jesus/AppData/Local/Temp /c/Users/jesus/AppData/Local /c/Users/jesus/AppData /c/Users/jesus /c/Users /c; do ls "$d"/compose.y*ml "$d"/docker-compose.y*ml 2>/dev/null; done` must print nothing. Docker Compose searches parent directories for a compose file.
3. Run each of these and record whether it printed help or an error:
   - `docker compose down -v --help; echo "exit=$?"`
   - `docker compose down --volumes --help; echo "exit=$?"`
   - `dotnet ef database drop --help; echo "exit=$?"`

C1.8 steps: the human runs these in an external Git Bash, after H1.3. Let `G=/c/Users/jesus/AppData/Local/Temp/envanex-probe-git` and `SCRATCH=/c/Users/jesus/AppData/Local/Temp/envanex-probe-empty`.
1. In `$G`, run each of these and record whether it printed usage (git exits 129) or did something:
   - `git reset HEAD --hard -h; echo "exit=$?"`
   - `git switch -C lp -h; echo "exit=$?"`
   - `git switch main -C lp -h; echo "exit=$?"`
   - `git switch -c lp -h; echo "exit=$?"`
2. In `$G`, `git branch --list lp` prints nothing.
3. In `$SCRATCH`, run `docker system prune --help; echo "exit=$?"`.
4. In `$SCRATCH`, run `command -v docker-compose; echo "exit=$?"` and `command -v dotnet-ef; echo "exit=$?"`.
5. For each binary step 4 found, run in `$SCRATCH`: `docker-compose down -v --help; echo "exit=$?"`, `docker-compose down --volumes --help; echo "exit=$?"`, `dotnet-ef database drop --help; echo "exit=$?"`.

C1.9 command:
`node -e "const d=require('./.claude/settings.json').permissions.deny;const b=d.filter(x=>x.startsWith('Bash(')).map(x=>x.slice(5));const p=d.filter(x=>x.startsWith('PowerShell(')).map(x=>x.slice(11));const ok=b.length===p.length&&b.every(x=>p.includes(x))&&b.length+p.length===d.length;console.log(ok?'mirror-ok '+b.length:'mirror-FAIL');process.exit(ok?0:1)"`

C2.6 steps: the human runs these in an external Git Bash, outside the repo and outside the H1.2 scratch dir.
1. `D=/c/Users/jesus/AppData/Local/Temp/envanex-prebuild-check && dotnet new console -o "$D" && cd "$D"`
2. The review's settling command: `dotnet build -p:PreBuildEvent="echo PWNED" 2>&1 | grep -n PWNED`. Record the raw output.
3. A file side effect, so the result doesn't depend on log verbosity: `dotnet build -p:PreBuildEvent="echo PWNED > pwned.txt"; test -f pwned.txt; echo "prebuild-exec=$?"`. `prebuild-exec=0` means the command ran.
4. `cd / && rm -r "$D"`. Record "exec: yes" or "exec: no" under As built.

---

## Phase 1: global guards (F1, F2, F15, F9 pattern form)

### Human steps

- **H1.1 (before the probes; outside the PR, since both files are untracked or outside the repo):**
  - Remove `Bash(git:*)` and `Bash(node:*)` from `C:\projects\envanex\.claude\settings.local.json`.
  - Delete the allow entry that holds an SA password literal (`.claude/settings.local.json:14`, the `export ENVANEX_CONNECTION_STRING=…` entry). The file is gitignored and was never committed, but the entry still goes.
  - Recommended: if the local `.env` SA password equals the deleted literal, rotate it, because the literal also sits in past session transcripts. Record the decision.
  - Decide whether to remove `Bash(git add *)`, `Bash(git commit *)` and the `PowerShell(git commit ...)` entries from `C:\Users\jesus\.claude\settings.json`. Record the decision under As built.
  - Edit both files in an external editor. The path guard blocks `*.local.json`.
  - Evidence:
    - `grep -n 'git:\*\|node:\*' .claude/settings.local.json` returns nothing.
    - `grep -c 'Password=' .claude/settings.local.json` prints `0`.
- **H1.2:** `mkdir -p /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty`, then `ls -A` of it prints nothing. Run C1.5.

### Files

- `.claude/hooks/lib/guard-common.js` — created. Shared helpers for stdin, paths, targets, redaction, logging and failing closed. Exports every function listed below, so the unit tests can `require` them.
- `.claude/hooks/path-guard.js` — created. Replaces the inline PreToolUse `node -e` guard.
- `.claude/hooks/tests/run-hook.js` — created. Test helper that spawns a hook script with JSON on stdin and an isolated log dir.
- `.claude/hooks/tests/guard-common.test.js` — created. In-process unit tests for `normalizePath`, `getTargetPath`, `getCommand`, `redactSecrets` and `logDecision`.
- `.claude/hooks/tests/path-guard.test.js` — created.
- `.claude/settings.json` — modified:
  - `permissions.allow` narrowed and converted to the space-wildcard form
  - `permissions.deny` added
  - the PreToolUse `command` points at the script
  - the matcher becomes `Edit|Write|MultiEdit|NotebookEdit`

  `statusLine` and the PostToolUse hook are unchanged. `MultiEdit` stays in the matchers on purpose: a matcher naming an absent tool does nothing, and it still covers foreground sessions.

### Signatures (CommonJS, Node built-ins only)

`.claude/hooks/lib/guard-common.js`:

- `readHookInput(): Promise<object>` — reads all of stdin and `JSON.parse`s it. Throws on empty or invalid input.
- `getProjectDir(input: object): string` — returns the **raw** value of `process.env.CLAUDE_PROJECT_DIR`, else `input.cwd`, else throws.
- `normalizeProjectDir(raw: string): string` — `normalizePath` without the join step. Throws if the value is not absolute after step 2.
- `normalizePath(rawPath: string, normalizedProjectDir: string): string` — a **pure string algorithm**, with no `path.resolve`, no `path.win32` and no other `path` module call:
  1. Replace every `\` with `/`.
  2. Map a leading `/<letter>/` (or `/<letter>` at end of string, letter case-insensitive, regex `^/([A-Za-z])(/|$)`) to `<letter>:/`.
  3. The path is absolute if it matches `^[A-Za-z]:/` or `^/`. Anything else is joined as `normalizedProjectDir + '/' + p`.
  4. Split on `/`. Drop empty segments (except the root) and `.` segments. A `..` removes the previous segment but never the root (`c:` or the leading `/`), so a `..` at the root is dropped.
  5. Lowercase, because Windows paths are case-insensitive.
  6. Strip a trailing `/` unless the result is a bare root.
- `getTargetPath(input: object): string` — the target extractor for file-tool hooks: `tool_input.file_path ?? tool_input.path ?? tool_input.notebook_path`. Throws if empty. It never reads `tool_input.command`, so a path hook attached to a Bash matcher by mistake fails closed.
- `getCommand(input: object): string` — the target extractor for Bash-matcher hooks: `tool_input.command`. Throws if it is missing, not a string, or empty.
- `redactSecrets(s: string): string` — applies these rules in order and replaces each secret value with `***`. Every rule replaces every occurrence (global flag). Every rule is case-insensitive unless stated otherwise:
  1. **Password assignments:** `(password|pwd)(\s*=\s*)(?:'[^']*'|"[^"]*"|[^;'"\s]+)` becomes `$1$2***`. It keeps the key and redacts a quoted or unquoted value. This covers connection strings, `export MSSQL_SA_PASSWORD='…'` and `SA_PASSWORD="…"` (`.env.example:21`, `docker-compose.yml:8`).
  2. **sqlcmd `-P`:** applies only when the string contains `sqlcmd`. `(\s-P\s*)(?:'[^']*'|"[^"]*"|\S+)` becomes `$1***`. The `-P` is matched **case-sensitively**, because sqlcmd's `-p` is a different option. This covers `docker-compose.yml:16`'s form `sqlcmd … -P "<pw>"`.
  3. **user-secrets:** applies only when the string contains `user-secrets`. Everything after the first whitespace-delimited `set` token that follows `user-secrets` is replaced with ` ***`, up to the next `&&`, `;`, `|`, newline or end of string. Losing the key from the log is harmless. Options before or after `set`, and every value form, are all covered. A quoted value that itself contains `;`, `|` or `&&` is redacted only up to that character (residual risk, Phase 4).
  4. **Tokens and keys:** `(secret|token|api[_-]?key)\s*[=:]\s*\S+` (unchanged).
- `resolveLogDir(projectDir: string): string` — `process.env.ENVANEX_HOOK_LOG_DIR` if set and non-empty, else `<projectDir>/TestResults/hook-log`.
- `logDecision(ctx, entry: {hook, decision, reason, tool_name, target, agent_type?, agent_id?, session_id?}): void`
  - appends one JSON line to `<resolveLogDir>/hooks.jsonl` with `fs.mkdirSync(…, {recursive: true})` and `fs.appendFileSync`, both synchronous
  - the line carries an ISO `ts` and `project_dir_raw` (C1.6)
  - applies `redactSecrets` to `target` and `reason` **before** truncating `target` to 300 characters
  - swallows its own errors, because logging must never change a decision
- `block(ctx, reason: string): never`
  1. logs
  2. writes `Blocked by <hook>: <reason> -> <target>`, passed through `redactSecrets`, to stderr with the synchronous `fs.writeSync(2, …)`
  3. `process.exit(2)`
- `allow(ctx): never` — logs, exits 0.
- `runGuard(hookName: string, decide: (input, ctx) => void, extractTarget: (input: object) => string = getTargetPath): void` — the entry point every guard uses.
  - It builds `ctx = {hook, projectDirRaw, projectDir, input, target}`, with `target = extractTarget(input)`.
  - File-tool hooks (`path-guard`, `write-scope`) use the default.
  - Bash hooks (`coder-bash-guard`, `bash-allowlist`) pass `getCommand`.
  - Any throw or rejection, including one from `extractTarget`, becomes `block` with `guard error: <message>`: fail closed.

`.claude/hooks/path-guard.js` calls `runGuard('path-guard', decide)` with the default extractor. It blocks when the normalized path has:

- **a segment equal to** one of `bin`, `obj`, `packages`, `.vs`, `.idea`, `.git`
- **a basename that is:**
  - exactly `.env`
  - starting with `.env.` other than exactly `.env.example`
  - ending in `.env`, `.user`, `.pfx`, `.snk` or `.local.json`
  - exactly `secrets.json`

`.idea`, `.snk` and `secrets.json` come from `CLAUDE.md:143` and the `.gitignore` secrets section. Blocking `.local.json` case-insensitively now also covers `.claude/settings.local.json`, which is deliberate. Everything else is allowed.

`.claude/hooks/tests/run-hook.js`:

- `runHook(scriptPath: string, args: string[], stdinText: string, env?: object): {status: number, stdout: string, stderr: string, logDir: string}`
- uses `child_process.spawnSync`
- sets `ENVANEX_HOOK_LOG_DIR` to a fresh `fs.mkdtempSync(os.tmpdir() + '/envanex-hooklog-')` unless `env` supplies it, so no test ever writes to the project's hook log

### `.claude/settings.json` permissions (exact)

`allow` (replaces the whole current list):

```
Bash(dotnet build), Bash(dotnet build *), Bash(dotnet test), Bash(dotnet test *), Bash(dotnet format *),
Bash(dotnet restore), Bash(dotnet restore *), Bash(dotnet list *), Bash(dotnet package *), Bash(dotnet run *),
Bash(dotnet new *), Bash(dotnet sln *), Bash(dotnet add *), Bash(dotnet tool restore),
Bash(dotnet ef migrations add *), Bash(dotnet ef migrations list *), Bash(dotnet ef database update *),
Bash(docker compose ps), Bash(docker compose ps *), Bash(docker compose up -d *), Bash(docker compose up -d),
Bash(docker compose logs *),
Bash(git status), Bash(git status *), Bash(git log *), Bash(git diff), Bash(git diff *), Bash(git show *),
Bash(git ls-files *), Bash(git rev-parse *), Bash(git branch --show-current),
Bash(git switch *), Bash(git checkout -b *), Bash(git pull), Bash(git pull *),
Bash(gh pr view *), Bash(gh pr list *), Bash(gh pr checks *), Bash(gh run list *), Bash(gh run view *),
Bash(node --test .claude/hooks/tests/*)
```

What was removed:
- `git add*`, `git commit*` and `git push*` (F1)
- the broad `dotnet ef*`, `docker compose*` and `gh pr*`/`gh run*` entries

`gh` is narrowed to read-only by F1's own reasoning: `/pr` already grants `gh pr` through `allowed-tools`.

`deny`: each entry below appears twice, once as `Bash(<p>)` and once as `PowerShell(<p>)`:

```
git stash | git stash * | git clean * | git reset --hard* | git checkout -- * | git checkout . |
git checkout -f* | git switch -f* | git switch --discard-changes* | git restore * | git rebase * |
git push --force* | git push -f* | git push * --force* | git push * -f* |
docker compose down -v* | docker compose down --volumes* | docker volume rm * | docker volume prune* |
dotnet ef database drop*
```

PreToolUse hook command, JSON-escaped in the file:

`node "$CLAUDE_PROJECT_DIR/.claude/hooks/path-guard.js" || exit 2`

### Tests to add

No test file contains the string `LIVEPROBE` (C1.7). Test targets use `guard-test.txt`, `x` and similar. Every secret in a test is a distinct `S3cretValueN` string, so a test that leaks one secret can't pass because of another test's line.

`.claude/hooks/tests/guard-common.test.js` (`node:test`, in-process `require`; each test sets `process.env.ENVANEX_HOOK_LOG_DIR` to its own `fs.mkdtempSync` directory):

- **normalizePath:**
  - `normalizePath converts backslashes` — `C:\projects\envanex\obj\x` → `c:/projects/envanex/obj/x`
  - `normalizePath maps /c/ to c:/` — `/c/projects/envanex/obj/x` → `c:/projects/envanex/obj/x`
  - `normalizePath maps an uppercase /C/ drive`
  - `normalizePath joins a relative path to the project dir`
  - `normalizePath treats a leading / without a drive letter as absolute` — `/tmp/x` stays `/tmp/x`
  - `normalizePath collapses . and .. segments`
  - `normalizePath drops .. above the root` — `c:/../x` → `c:/x`
  - `normalizePath lowercases and strips a trailing slash`
  - `normalizeProjectDir gives the same result for C:\, C:/ and /c/ forms`
  - `normalizeProjectDir rejects a relative dir`
- **target extractors:**
  - `getCommand returns tool_input.command for a Bash-shaped input` — `{"tool_name":"Bash","tool_input":{"command":"git status"}}` → `git status`
  - `getCommand throws on a missing or empty command` — two sub-cases: `tool_input: {}` and `command: ""`
  - `getTargetPath throws on a Bash-shaped input` — the same input as the first case; the call throws
- **redaction:**
  - `redacts Password in a connection string` — `logDecision` with target `export ENVANEX_CONNECTION_STRING='Server=x;Password=S3cretValue1;TrustServerCertificate=True'`. The log line lacks `S3cretValue1` and contains `Password=***`.
  - `redacts every Password occurrence` — the target is `a Password=S3cretValue10; b Password=S3cretValue11`, and the log line contains neither value.
  - `redacts a quoted MSSQL_SA_PASSWORD assignment` — target `export MSSQL_SA_PASSWORD='S3cretValue4'`. The log line lacks `S3cretValue4` and contains `MSSQL_SA_PASSWORD=***`.
  - `redacts a double-quoted SA_PASSWORD assignment` — target `SA_PASSWORD="S3cretValue8" docker compose up -d`. The log line lacks `S3cretValue8` and still contains `docker compose up -d`.
  - `redacts the sqlcmd -P value` — target `docker exec envanex-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "S3cretValue7" -C -Q "SELECT 1"`. The log line lacks `S3cretValue7` and still contains `-U sa`.
  - `redacts the user-secrets set value` — target `dotnet user-secrets set "Jwt:SigningKey" "S3cretValue3" --project src/Envanex.Web`. The log line lacks `S3cretValue3`.
  - `redacts user-secrets set with a --verbose flag before the key` — target `dotnet user-secrets set --verbose "Jwt:SigningKey" "S3cretValue5"`. The log line lacks `S3cretValue5`.
  - `redacts user-secrets set when options precede set` — target `dotnet user-secrets --project src/Envanex.Web set "Jwt:SigningKey" "S3cretValue6"`. The log line lacks `S3cretValue6`.
  - `redacts user-secrets set only up to the next &&` — target `dotnet user-secrets set k S3cretValue9 && dotnet build`. The log line lacks `S3cretValue9` and contains `&& dotnet build`.
  - `redacts a token or api key assignment` — `GH_TOKEN=abc123` and `api_key: abc123`
  - `leaves a command without secrets unchanged` — `dotnet test --filter Category=Unit`
  - `redacts before truncating to 300 characters` — a `Password=` value starting at character 290 does not survive in the log line
- **log:**
  - `does not write to the project hook log when the override is set` — the project dir is temp dir A and the override is temp dir B. Asserts that `A/TestResults/hook-log/hooks.jsonl` does not exist and that `B/hooks.jsonl` has the line.
  - `falls back to <projectDir>/TestResults/hook-log without the override` — the override is empty and the project dir is a temp directory
  - `logDecision swallows its own errors` — the override points at an existing *file*; the call does not throw

`.claude/hooks/tests/path-guard.test.js` (`node:test`, via `runHook`). The script is spawned with `CLAUDE_PROJECT_DIR=C:\projects\envanex` unless the case says otherwise, so every case also exercises the stdin wiring.

**Blocks** (exit code 2):
- `blocks a backslash obj path` — `C:\projects\envanex\obj\guard-test.txt`
- `blocks a forward-slash obj path` — `obj/guard-test.txt`
- `blocks a nested bin path` — `src\Envanex.Web\bin\Debug\x.dll`
- `blocks a git-bash style path` — `/c/projects/envanex/obj/x`
- `blocks with a git-bash-style CLAUDE_PROJECT_DIR` — env `/c/projects/envanex`, target `obj\x.txt`
- `blocks an uppercase OBJ segment` — `OBJ\x`
- `blocks .git, .vs, .idea and packages segments` — four sub-cases
- `blocks .env`, `blocks .env.local`, `blocks .env.example.bak`
- `blocks settings.local.json` — `.claude\settings.local.json`
- `blocks appsettings.Development.Local.json`, `blocks .user`, `blocks .pfx`, `blocks .snk`, `blocks secrets.json`
- `blocks on malformed JSON` — stdin `{not json`
- `blocks on an empty file path`
- `blocks when no project dir can be determined` — `CLAUDE_PROJECT_DIR` unset, no `cwd` in the input
- `block writes the reason to stderr before exiting` — stderr contains `Blocked by path-guard:` and `obj/guard-test.txt`
- `redacts the block message on stderr` — target `obj/Password=S3cretValue2.txt`; neither stderr nor the log line contains `S3cretValue2`

**Allows** (exit code 0):
- `allows .env.example` — both relative and absolute backslash forms
- `allows src\Envanex.Web\Program.cs`
- `allows a segment that only contains obj` — `docs/objects.md`, `src/Binary/x.cs`
- `allows .github/workflows/ci.yml`, `allows .gitignore`, `allows .gitattributes`
- `allows thoughts/shared/plans/x.md`

**Log:**
- `writes one hook-log line per decision` — asserts that `runHook`'s `logDir/hooks.jsonl` has a line with `"decision":"block"`, `"hook":"path-guard"` and a `project_dir_raw` field

### Mutation proofs (separate coder turn, after `/commit`)

Each proof follows the same steps:
1. mutate `.claude/hooks/path-guard.js` (or `lib/guard-common.js`) with Edit
2. run `node --test .claude/hooks/tests/*.test.js` and capture the raw output
3. apply the inverse Edit
4. run `git diff --exit-code -- <file>`, which must exit 0

| ID | Mutation | Expected red |
|---|---|---|
| M1.1 | drop the `\` → `/` replacement | `blocks a backslash obj path`, `blocks a nested bin path` and `normalizePath converts backslashes` |
| M1.2 | remove the `.env.example` exception | `allows .env.example` |
| M1.3 | make `runGuard` exit 0 on a throw | `blocks on malformed JSON` |
| M1.4 | remove the lowercasing | `blocks an uppercase OBJ segment` |
| M1.5 | remove the `redactSecrets` call in `logDecision` | `redacts Password in a connection string` |
| M1.6 | remove the `redactSecrets` call in `block`'s stderr write | `redacts the block message on stderr` |
| M1.7 | make `resolveLogDir` ignore `ENVANEX_HOOK_LOG_DIR` | `does not write to the project hook log when the override is set` |
| M1.8 | in `redactSecrets` rule 1, drop the two quoted alternatives, leaving `[^;'"\s]+` | `redacts a quoted MSSQL_SA_PASSWORD assignment` |
| M1.9 | remove `redactSecrets` rule 2 (sqlcmd `-P`) | `redacts the sqlcmd -P value` |

### Probes (fresh session after restart; main session)

Every target carries `LIVEPROBE`.

- **P1.1 (block):** "Use the Write tool to create `obj\LIVEPROBE-1-1.txt`." Blocked. The log line for `LIVEPROBE-1-1` shows `path-guard` `block`, and its `project_dir_raw` settles C1.6.
- **P1.2 (block):** the same with `obj/LIVEPROBE-1-2.txt`. Blocked; log line present.
- **P1.3 (block):** Write `C:\projects\envanex\src\Envanex.Web\bin\LIVEPROBE-1-3.txt`. Blocked; log line present.
- **P1.4 (block):** Write `.env.local`. Blocked. The target has no tag, so the evidence is the tool result's `Blocked by path-guard:` text plus the newest `path-guard` line whose target ends in `.env.local`.
- **P1.5 (allow):**
  - Edit `.env.example` to append `# LIVEPROBE-1-5` — allowed; log `allow` line present
  - apply the inverse Edit — allowed
  - `git diff --exit-code -- .env.example` exits 0

  This closes F15.
- **P1.6 (allow):**
  - Write `TestResults/LIVEPROBE-1-6.txt` — allowed; log `allow` line present
  - delete the file

  Together with P1.1 this proves `$CLAUDE_PROJECT_DIR` expands in Claude Code's hook shell on Windows.
- **P1.7 (deny, Bash tool):** each command must be denied without running. Let `SCRATCH=/c/Users/jesus/AppData/Local/Temp/envanex-probe-empty` (H1.2).
  - `git stash list`
  - `git stash`
  - `git clean -n`
  - `git reset --hard -h`
  - `git checkout -- CLAUDE.md`
  - `git restore CLAUDE.md`
  - `git rebase -h`
  - `git push --force --dry-run`
  - `git push -f --dry-run`
  - `git push origin HEAD --force --dry-run` (tests the inner wildcard)
  - `cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && docker compose down -v --help`
  - `cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && docker compose down --volumes --help`
  - `cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && dotnet ef database drop --help`
  - `git status && git stash list` (tests the compound-command split)

  **Evidence per command** (only a deny rule produces all three):
  1. the raw tool result is Claude Code's permission-denied text, naming the command
  2. the human's note that no permission prompt appeared
  3. no command output in the result: no stash list, no usage text, no help text, no "no configuration file provided", no "No project was found"

  Without the deny rule, `default` mode prompts, which fails (2). A permissive mode runs the command, which fails (1) and (3), and does so harmlessly: the tree is clean, `-h`/`-n`/`--dry-run` are used, and the three scratch-dir commands find no compose file and no project (C1.5).

  **Control (P1.7-ctl):** `git status --short` in the same session returns its (empty) output without a prompt.

  If a `cd` prompt appears instead of the deny, the human declines and the probe **fails**: the deny did not pre-empt it.
- **P1.8 (deny, PowerShell tool):** `git stash list` and `git clean -n` through the PowerShell tool. Both are denied, with the same three-part evidence as P1.7.
- **P1.9 (allow, in `default` mode):**
  - These run without a prompt: `git status --short`, `git log --oneline -1`, `docker compose ps`, `node --test .claude/hooks/tests/path-guard.test.js`.
  - These prompt, and the human declines: `git commit --dry-run -m LIVEPROBE-1-9` and `docker compose down --help`. If the first one does not prompt, record that user-level settings allowed it (H1.1).
- **P1.10 (fail-closed wiring, shell level):** `CLAUDE_PROJECT_DIR=/c/projects/envanex bash -c 'node "$CLAUDE_PROJECT_DIR/.claude/hooks/missing.js" || exit 2'; echo "exit=$?"` prints `exit=2`.

### Validation

```bash
node --version
node --test .claude/hooks/tests/*.test.js
grep -c LIVEPROBE .claude/hooks/tests/*.js                     # 0 for every file (C1.7)
node -e "JSON.parse(require('fs').readFileSync('.claude/settings.json','utf8'))"
grep -c $'\r' .claude/hooks/lib/guard-common.js .claude/hooks/path-guard.js .claude/hooks/tests/*.js
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test            # 638 passed in total
git rev-parse --verify origin/main && test "$(git merge-base main HEAD)" = "$(git merge-base origin/main HEAD)"; echo "base-check=$?"   # base-check=0
git diff --name-only main...HEAD -- src tests db   # empty
git status --short
```

### Phase 1 amendments (Revision 4)

These amend Phase 1 after its code review. The text above records what `ac397c9` and `726338a` built. Where an amendment differs, the amendment wins.

**Order.** The amendments run as one round of the Probe protocol:
1. The coder implements A1-A7, B1 and B2 in one turn, ending with PHASE_COMPLETE.
2. The human runs `/commit`.
3. A separate coder turn runs M1.10-M1.27 against that commit, using Phase 1's four mutation steps.
4. The human runs H1.3 and C1.8 in an external Git Bash, then restarts Claude Code.
5. In the fresh session, the main session runs C1.9 and P1.11-P1.15. It appends raw evidence, as it happens, to `TestResults/chore-agents-hardening/evidence/phase-1-amendments.md`.
6. Cleanup, as in Probe protocol step 4. In addition:
   - `git branch --list 'LIVEPROBE*'` prints nothing
   - `ls TestResults/LIVEPROBE-*` answers "No such file"
7. The evidence is copied into this plan under `## As built — Phase 1 amendments`, placed after `## As built — Phase 1`, and the human commits it as `docs(agents)`.
8. code-reviewer re-reviews Phase 1 with `Range: <the commit that records Revision 4>..<the amendments evidence commit>`.
   - Under the old contract it writes no file. The main session saves its output verbatim as `thoughts/shared/reviews/2026-09-DD_chore-agents-code-review-phase-1-r2.md`.
   - The human commits that file, whatever its verdict.
9. The tester runs on that commit.

Phase 2 starts only after this round ends with READY_TO_PUSH, because Phase 2's hooks log whole commands and need A7 in place first.

#### Human steps

- **H1.3:** create a scratch git repo outside the repository:
  `G=/c/Users/jesus/AppData/Local/Temp/envanex-probe-git && git init -b main "$G" && cd "$G" && git commit --allow-empty -m init`
  Then run C1.8.

#### Files

- `.claude/hooks/lib/guard-common.js` — modified: A2 (`await decide`), A4 and A5 (`resolveLogDir`, `logDecision`), A7 (`redactSecrets`).
- `.claude/hooks/tests/fixtures/async-decide-guard.js` — created (A2). CommonJS, LF, no `LIVEPROBE`.
- `.claude/hooks/tests/path-guard.test.js` — modified:
  - A1, A3, A4 (the spawned test) and A6
  - the `after` cleanup skips an undefined `logDir`
- `.claude/hooks/tests/guard-common.test.js` — modified:
  - A2, A4, A5 and A7 tests
  - saves and restores `process.env.CLAUDE_PROJECT_DIR` around the tests that set it
- `.claude/settings.json` — modified: B1 (the `allow` list, replaced) and B2 (27 deny patterns appended, each as a `Bash(…)` and `PowerShell(…)` pair)

`.claude/hooks/path-guard.js` and `.claude/hooks/tests/run-hook.js` are not changed. M1.17 mutates `path-guard.js` only temporarily.

#### Signatures and rules

- **A1 (finding 1):** no code change. `blocks on malformed JSON` asserts `invalid hook input`, and `blocks on an empty file path` asserts `target path is missing or empty`.
- **A2 (finding 2):**
  - Signature: `runGuard(hookName: string, decide: (input: object, ctx: object) => void | Promise<void>, extractTarget: (input: object) => string = getTargetPath): void`.
  - The inner function awaits `decide` before it calls `allow`. A rejected promise becomes `block` with `guard error: <message>`, and an async `block` exits 2 before `allow` can run.
  - Fixture `.claude/hooks/tests/fixtures/async-decide-guard.js <mode>`: calls `runGuard('async-decide-fixture', decide)` with the default extractor. `decide` is async and first awaits `Promise.resolve()`. Then:
    - mode `reject` throws `Error('async decide rejected')`
    - mode `block` calls `block(ctx, 'async decide blocked')`
    - any other mode throws `Error('unknown fixture mode')`
  - The fixture's name doesn't end in `.test.js`, so `node --test .claude/hooks/tests/*.test.js` doesn't run it as a test.
- **A3 (finding 3):** no code change; tests only.
- **A4 and A5 (findings 4 and 5):** `resolveLogDir(projectDirRaw?: string): string`:
  1. Return `ENVANEX_HOOK_LOG_DIR` if it is set and non-empty (unchanged).
  2. Otherwise the base is `projectDirRaw` if that is a non-empty string, else `process.env.CLAUDE_PROJECT_DIR`. If neither exists, throw.
  3. Convert the base with steps 1 and 2 of `normalizePath` only: `\` → `/`, and `/<letter>/` → `<letter>:/`. Don't lowercase it and don't collapse `..`. Strip a trailing `/`. If the result doesn't match `^[A-Za-z]:/` or `^/`, throw.
  4. Return `<base>/TestResults/hook-log`.

  `logDecision` calls `resolveLogDir(ctx && ctx.projectDirRaw)` and never uses `ctx.projectDir`. As a result:
  - A guard error raised before `getProjectDir` runs (empty or malformed stdin) still logs under `CLAUDE_PROJECT_DIR`.
  - The fallback log dir keeps the raw case.
  - A relative raw project dir writes no log line. The block still happens.
  - Decisions don't change, and `logDecision` still swallows its own errors.
  - In production the log stays at `C:/projects/envanex/TestResults/hook-log/hooks.jsonl` (C1.6). P1.14 confirms this.
- **A6:** tests only. They pin the current behaviour: every one of these forms still reaches a protected segment or basename after `normalizePath`.
- **A7 (finding 11): `redactSecrets(s: string): string`.** Rules run in the order below. Every rule replaces every occurrence and is case-insensitive unless stated otherwise. Shared definitions:
  - `SENSITIVE` = `password|passwd|pwd|secret|token|api[_-]?key|signing[_-]?key|access[_-]?key|private[_-]?key|credential`
  - `VALUE` = `(?:'[^']*(?:'|$)|"(?:[^"\\]|\\.)*(?:"|$)|[^;\s'"])+`. That is, one or more of:
    - a single-quoted run, closed or unterminated
    - a double-quoted run with backslash escapes, closed or unterminated
    - a single unquoted character other than `;`, whitespace or a quote

    The three alternatives start with different characters, so backtracking stays linear.

  The rules:
  - **Rule 0 (new), line continuations:** each `\` or `` ` `` immediately followed by `\r?\n` becomes one space before any other rule runs.
  - **Rule 1 (value amended), password assignments:** `(password|pwd)(\s*=\s*)VALUE` → `$1$2***`. The old value was `'[^']*'|"[^"]*"|[^;'"\s]+`. With the new value:
    - an unterminated quote is redacted to the end of the string
    - `\"` doesn't end a double-quoted value
    - a quote inside an unquoted value continues the value

    The rule redacts more rather than less (interpretation V).
  - **Rules 2, 3 and 4:** unchanged. Rule 0 now covers rule 3's line-continuation gap.
  - **Rule 5 (new), sensitive key assignments:** `([A-Za-z0-9_.:-]*(?:SENSITIVE)[A-Za-z0-9_.:-]*)(\s*[=:]\s*)VALUE` → `$1$2***`. This covers:
    - `AWS_SECRET_ACCESS_KEY=x`
    - `Jwt__SigningKey=x`
    - `Jwt:SigningKey=x`
    - `--Jwt:SigningKey=x` (the `--` stays outside the key)
  - **Rule 6 (new), JSON members:** `("[^"]*(?:SENSITIVE)[^"]*"\s*:\s*)(?:"(?:[^"\\]|\\.)*"|[^,}\s]+)` → `$1"***"`.
  - **Rule 7 (new), space-separated options:** `((?:^|\s)--?[A-Za-z0-9_-]*(?:SENSITIVE)[A-Za-z0-9_-]*)(\s+)(?:'[^']*(?:'|$)|"(?:[^"\\]|\\.)*(?:"|$)|\S+)` → `$1$2***`.
  - **Rule 8 (new), Authorization headers:** `(authorization\s*[:=]\s*(?:(?:bearer|basic|token|digest)\s+)?)[^\s'"]+` → `$1***`.
  - **Rule 9 (new), URL userinfo:** `([a-z][a-z0-9+.-]*://[^\s/@:'"]*:)[^\s/@'"]+@` → `$1***@`.

  Truncation to 300 characters still happens after redaction.

#### B1: `permissions.allow` (exact; replaces the whole list)

```
Bash(dotnet build), Bash(dotnet build *), Bash(dotnet test), Bash(dotnet test *), Bash(dotnet format *),
Bash(dotnet restore), Bash(dotnet restore *), Bash(dotnet list *), Bash(dotnet package *), Bash(dotnet run *),
Bash(dotnet sln *), Bash(dotnet add *), Bash(dotnet tool restore),
Bash(dotnet ef migrations add *), Bash(dotnet ef migrations list *),
Bash(docker compose ps), Bash(docker compose ps *), Bash(docker compose up -d *), Bash(docker compose up -d),
Bash(docker compose logs *),
Bash(git status), Bash(git status *), Bash(git log *), Bash(git diff), Bash(git diff *), Bash(git show *),
Bash(git ls-files *), Bash(git rev-parse *), Bash(git branch --show-current),
Bash(git switch -c *), Bash(git switch main), Bash(git checkout -b *), Bash(git pull), Bash(git pull *),
Bash(gh pr view *), Bash(gh pr list *), Bash(gh pr checks *), Bash(gh run list *), Bash(gh run view *),
Bash(node --test .claude/hooks/tests/guard-common.test.js), Bash(node --test .claude/hooks/tests/path-guard.test.js),
Bash(node --test .claude/hooks/tests/*.test.js)
```

Removed:
- `Bash(git switch *)` (finding 6)
- `Bash(dotnet ef database update *)` (finding 7; it now prompts)
- `Bash(dotnet new *)` (finding 8; it now prompts)
- `Bash(node --test .claude/hooks/tests/*)` (finding 9)

Every later test file gets its own exact entry (Phase 2).

#### B2: `permissions.deny` additions

The 40 existing entries stay. Each pattern below is appended twice, as `Bash(<p>)` and as `PowerShell(<p>)`, which makes 47 patterns and 94 entries in total.

```
 1 git -C *                          15 git log * --output*
 2 git -c *                          16 git diff --output*
 3 git reset * --hard*               17 git diff * --output*
 4 git switch --force*               18 git show --output*
 5 git switch -C*                    19 git show * --output*
 6 git switch * -f*                  20 docker compose * down -v*
 7 git switch * --force*             21 docker compose * down --volumes*
 8 git switch * --discard-changes*   22 docker-compose down -v*
 9 git switch * -C*                  23 docker-compose down --volumes*
10 git checkout * -- *               24 docker-compose * down -v*
11 git push +*                       25 docker-compose * down --volumes*
12 git push * +*                     26 dotnet-ef database drop*
13 git branch -D*                    27 docker system prune*
14 git log --output*
```

### Tests to add (amendments)

No test file contains `LIVEPROBE` (C1.7). Each secret is a distinct `S3cretValueN`, continuing from 13. New redaction tests assert on the parsed `target` field (`JSON.parse(line).target`), because the raw log line JSON-escapes quotes.

`.claude/hooks/tests/path-guard.test.js`:
- **modified (A1):**
  - `blocks on malformed JSON` asserts `invalid hook input`
  - `blocks on an empty file path` asserts `target path is missing or empty`
- **A3:**
  - `blocks a NotebookEdit notebook_path under obj` — stdin `{"tool_name":"NotebookEdit","tool_input":{"notebook_path":"obj/x.ipynb"}}`, asserting `protected segment "obj"`
  - `allows a NotebookEdit notebook_path` — `docs/x.ipynb`, exit 0
- **A4:** `logs an early guard error under CLAUDE_PROJECT_DIR`
  - setup: stdin `{not json`; env `CLAUDE_PROJECT_DIR=<temp dir A>` and `ENVANEX_HOOK_LOG_DIR: undefined`
  - asserts exit 2 and stderr `invalid hook input`
  - asserts that `A/TestResults/hook-log/hooks.jsonl` has one line with `"decision":"block"` and `invalid hook input`
  - removes temp dir A afterwards
- **A6:**
  - `blocks a UNC path with an obj segment` — `\\server\share\obj\x`, `protected segment "obj"`
  - `blocks a \\?\ device path to .env` — `\\?\C:\projects\envanex\.env`, `protected file name`
  - `blocks a \\.\ device path with an obj segment` — `\\.\C:\projects\envanex\obj\x`, `protected segment "obj"`

`.claude/hooks/tests/guard-common.test.js`:
- **A2** (spawned through `runHook` against the fixture, with stdin `{"tool_name":"Write","tool_input":{"file_path":"docs/x.md"}}` and `CLAUDE_PROJECT_DIR=C:\projects\envanex`):
  - `runGuard blocks when an async decide rejects` — mode `reject`; exit 2; stderr contains `guard error: async decide rejected`
  - `runGuard waits for an async decide that blocks` — mode `block`; exit 2; stderr contains `async decide blocked`
- **A4 and A5** (override set to `''`):
  - `resolveLogDir keeps the raw project dir's case in slash form` — `C:\Projects\Envanex` → `C:/Projects/Envanex/TestResults/hook-log`; `/c/Projects/Envanex` → `c:/Projects/Envanex/TestResults/hook-log`
  - `resolveLogDir falls back to CLAUDE_PROJECT_DIR when no project dir is given` — env `C:\Temp\Envanex-X`, argument `undefined` → `C:/Temp/Envanex-X/TestResults/hook-log`
  - `resolveLogDir throws on a relative project dir` — `projects/envanex`, with `CLAUDE_PROJECT_DIR` deleted
  - `logDecision builds the fallback log dir from projectDirRaw` — ctx `{projectDirRaw: <temp A>, projectDir: <temp B in slash form>}`; A has the line and B has no `TestResults`
- **A7:**
  - `joins a backslash line continuation before redacting` — `dotnet user-secrets set k \` + `\n` + `S3cretValue13`
  - ``joins a PowerShell backtick continuation before redacting`` — ``dotnet user-secrets set k ` `` + `\r\n` + `S3cretValue14`
  - `redacts an unterminated quoted Password value` — `Password='S3cretValue15 and the rest`
  - `redacts a Password value with a mid-value quote` — `Password=ab'S3cretValue16`
  - `redacts a Password value with an escaped double quote` — `Password="ab\" S3cretValue17"` (note the space after `\"`)
  - `redacts AWS_SECRET_ACCESS_KEY` — `AWS_SECRET_ACCESS_KEY=S3cretValue18 aws s3 ls`; target keeps `aws s3 ls`
  - `redacts a Jwt__SigningKey assignment` — `export Jwt__SigningKey='S3cretValue19'`
  - `redacts a --Jwt:SigningKey= argument` — `dotnet run --project src/Envanex.Web --Jwt:SigningKey=S3cretValue20`; target keeps `--project src/Envanex.Web`
  - `redacts a Jwt:SigningKey= assignment` — `Jwt:SigningKey=S3cretValue21`
  - `redacts a JSON Password member` — `{"Password": "S3cretValue22"}`; target contains `"Password": "***"`
  - `redacts a JSON SigningKey member` — `{"Jwt": {"SigningKey": "S3cretValue23"}}`
  - `redacts a space-separated --password value` — `tool --password S3cretValue24 --verbose`; target keeps `--verbose`
  - `redacts an Authorization Bearer header` — `curl -H "Authorization: Bearer S3cretValue25" https://example.test/x`; target keeps `https://example.test/x`
  - `redacts URL userinfo` — `git clone https://user:S3cretValue26@example.test/x.git`; target keeps `@example.test/x.git`
  - `leaves a URL without userinfo unchanged` — `git clone https://example.test/x.git` (exact)
  - `leaves git show HEAD:path unchanged` — `git show HEAD:src/Envanex.Web/Program.cs` (exact)

Total after the amendments: 85 hook tests (57 + 28).

### Mutation proofs (amendments; separate coder turn after `/commit`)

Same four steps as Phase 1. Each restore ends with `git diff --exit-code -- <file>` at 0.

| ID | Mutation | Expected red |
|---|---|---|
| M1.10 | drop the `\` → `/` replacement in `toSlashForm` (the M1.1 mutation) | `blocks on an empty file path`, on its own assertion (the reason is now `project dir is not absolute`) |
| M1.11 | in `readHookInput`, resolve `{}` instead of rejecting on a JSON parse error | `blocks on malformed JSON`, `logs an early guard error under CLAUDE_PROJECT_DIR` |
| M1.12 | remove `await` before `decide` in `runGuard` | `runGuard blocks when an async decide rejects`, `runGuard waits for an async decide that blocks` |
| M1.13 | remove `?? toolInput.notebook_path` in `getTargetPath` | `allows a NotebookEdit notebook_path`, `blocks a NotebookEdit notebook_path under obj` |
| M1.14 | in `resolveLogDir`, drop the `CLAUDE_PROJECT_DIR` fallback | `logs an early guard error under CLAUDE_PROJECT_DIR`, `resolveLogDir falls back to CLAUDE_PROJECT_DIR when no project dir is given` |
| M1.15 | in `logDecision`, pass `ctx.projectDir` instead of `ctx.projectDirRaw` | `logDecision builds the fallback log dir from projectDirRaw` |
| M1.16 | in `resolveLogDir`, lowercase the base | `resolveLogDir keeps the raw project dir's case in slash form` |
| M1.17 | in `path-guard.js` `decide`, return (allow) when the normalized path does not start with `ctx.projectDir + '/'` | the three A6 tests, and `blocks secrets.json` |
| M1.18 | remove rule 0 | both continuation tests |
| M1.19 | in `VALUE`, the single-quoted alternative requires its closing quote (`'[^']*'`) | `redacts an unterminated quoted Password value` |
| M1.20 | in `VALUE`, drop the `\\.` escape (`"[^"]*(?:"\|$)`) | `redacts a Password value with an escaped double quote` |
| M1.21 | replace `VALUE` with one unrepeated alternative, with an unquoted run `[^;\s'"]+` | `redacts a Password value with a mid-value quote` |
| M1.22 | remove rule 5 | `redacts AWS_SECRET_ACCESS_KEY`, `redacts a Jwt__SigningKey assignment`, `redacts a --Jwt:SigningKey= argument`, `redacts a Jwt:SigningKey= assignment` |
| M1.23 | remove rule 6 | both JSON member tests |
| M1.24 | remove rule 7 | `redacts a space-separated --password value` |
| M1.25 | remove rule 8 | `redacts an Authorization Bearer header` |
| M1.26 | remove rule 9 | `redacts URL userinfo` |
| M1.27 | in `resolveLogDir`, drop the absolute check | `resolveLogDir throws on a relative project dir` |

### Probes (amendments; fresh session after restart)

Unless noted, every item is its own tool call, run exactly as written.

- **P1.11 (deny, Bash tool, auto mode as in C1.1).**
  - Evidence per command is the same three-part evidence as P1.7:
    1. Claude Code's permission-denied text
    2. the human's note that no prompt appeared
    3. no command output
  - Each command is chosen so that, under case-sensitive matching, no other deny entry matches it, and so that it does no harm on a clean tree if the deny were missing (C1.8).

  | id | Command | Pattern |
  |---|---|---|
  | a | `git -C . stash list` | 1 |
  | b | `git -c core.pager=cat stash list` | 2 |
  | c | `git reset HEAD --hard -h` | 3 |
  | d | `git switch --force chore/agents-hardening` | 4 |
  | e | `git switch -C LIVEPROBE-1-11e -h` | 5 |
  | f | `git switch chore/agents-hardening -f` | 6 |
  | g | `git switch chore/agents-hardening --force` | 7 |
  | h | `git switch chore/agents-hardening --discard-changes` | 8 |
  | i | `git switch chore/agents-hardening -C LIVEPROBE-1-11i -h` | 9 |
  | j | `git checkout HEAD -- CLAUDE.md` | 10 |
  | k | `git push +LIVEPROBE-1-11k --dry-run` | 11 |
  | l | `git push origin +LIVEPROBE-1-11l --dry-run` | 12 |
  | m | `git branch -D LIVEPROBE-1-11m` | 13 |
  | n | `git log --output=TestResults/LIVEPROBE-1-11n.txt -1` | 14 |
  | o | `git log -1 --output=TestResults/LIVEPROBE-1-11o.txt` | 15 |
  | p | `git diff --output=TestResults/LIVEPROBE-1-11p.txt` | 16 |
  | q | `git diff HEAD --output=TestResults/LIVEPROBE-1-11q.txt` | 17 |
  | r | `git show --output=TestResults/LIVEPROBE-1-11r.txt HEAD` | 18 |
  | s | `git show HEAD --output=TestResults/LIVEPROBE-1-11s.txt` | 19 |
  | t | `docker compose -f /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty/compose.yml down -v --help` | 20 |
  | u | `docker compose -p liveprobe-1-11u -f /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty/compose.yml down --volumes --help` | 21 |
  | v | `cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && docker-compose down -v --help` | 22 |
  | w | `cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && docker-compose down --volumes --help` | 23 |
  | x | `docker-compose -f /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty/compose.yml down -v --help` | 24 |
  | y | `docker-compose -p liveprobe-1-11y -f /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty/compose.yml down --volumes --help` | 25 |
  | z | `cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && dotnet-ef database drop --help` | 26 |
  | aa | `docker system prune --filter label=liveprobe-1-11aa --help` | 27 |

  The `compose.yml` in t, u, x and y does not exist, so even if one of those ran, it would only report a missing file.

  Extra evidence:
  - `ls TestResults/LIVEPROBE-1-11*.txt` answers "No such file" (n-s)
  - `git branch --list 'LIVEPROBE*'` prints nothing (e, i, k, l, m)
  - **P1.11-ctl:** `git status --short` returns without a prompt
- **P1.12 (deny, PowerShell tool, auto mode):**
  - Run the same 27 commands through the PowerShell tool, with IDs `P1.12a`-`P1.12aa` and tags renamed to `LIVEPROBE-1-12…`.
  - In PowerShell, drop the `cd … &&` prefix from v, w and z, and write paths as `C:\Users\jesus\AppData\Local\Temp\envanex-probe-empty\compose.yml`.
  - v, w and z then run in the repo directory. They run only if C1.8 showed that the binary is absent or prints help for that form. Otherwise that item is skipped and recorded, and C1.9 covers its mirror entry.
  - Same three-part evidence as P1.11.
- **P1.13 (allow and prompt, manual mode as in P1.9):**
  - a. `git switch -c LIVEPROBE-1-13a -h` runs without a prompt and prints git's usage text (see C1.8 step 1). A permission-denied result means the matcher folds case; apply Rollback fallback R4-1. Afterwards `git branch --list 'LIVEPROBE*'` prints nothing.
  - b. `git switch chore/agents-hardening` prompts, because `git switch *` is gone. The human declines.
  - c. `dotnet ef database update --help` prompts; declined.
  - d. `dotnet new --help` prompts; declined.
  - e. `node --test .claude/hooks/tests/guard-common.test.js` runs without a prompt, all green.
  - f. `node --test .claude/hooks/tests/*.test.js` runs without a prompt, 85 green.
  - g. `node --test .claude/hooks/tests/../tests/path-guard.test.js`: record whether a prompt appears (finding 9's settling command). There is no expected outcome. Record it as `wildcard crosses ..: yes|no` under As built; Phase 4's residual list cites it. If a prompt appears, the human declines.
  - h. `git diff --stat`, `git log --oneline -1` and `git show --stat HEAD` run without a prompt. The kept entries still allow, and the `--output` denies don't catch them.
- **P1.14 (path guard after A2, A4 and A5):**
  - Write `obj\LIVEPROBE-1-14.txt`: blocked, and `grep 'LIVEPROBE-1-14' TestResults/hook-log/hooks.jsonl` shows the `block` line with `project_dir_raw`.
  - Write `TestResults/LIVEPROBE-1-14b.txt`: allowed, with an `allow` line. Then delete the file.
  - This proves that production logging still lands in `TestResults/hook-log/hooks.jsonl` after the `resolveLogDir` change.
- **P1.15 (NotebookEdit, live):** NotebookEdit on `obj/LIVEPROBE-1-15.ipynb`.
  - Evidence: the tool result `Blocked by path-guard: protected segment "obj"`, plus a log line for `LIVEPROBE-1-15` with `"tool_name":"NotebookEdit"`.
  - If the tool is unavailable, or rejects the call before the hook runs (no log line), the probe is **inconclusive** and recorded that way. The A3 unit tests carry the behaviour.
- **C1.9:** the mirror command prints `mirror-ok 47`.

### Validation (amendments)

```bash
node --version
node --test .claude/hooks/tests/*.test.js                                          # 85 pass, 0 fail
grep -c LIVEPROBE .claude/hooks/tests/*.js .claude/hooks/tests/fixtures/*.js       # 0 for every file (C1.7)
grep -c $'\r' .claude/hooks/lib/guard-common.js .claude/hooks/path-guard.js .claude/hooks/tests/*.js .claude/hooks/tests/fixtures/*.js   # 0 each
node -e "JSON.parse(require('fs').readFileSync('.claude/settings.json','utf8'))"
node -e "<the C1.9 command>"                                                       # mirror-ok 47
grep -cE 'Bash\((git switch \*|dotnet ef database update \*|dotnet new \*|node --test \.claude/hooks/tests/\*)\)' .claude/settings.json   # 0
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test            # 638 passed in total
git rev-parse --verify origin/main && test "$(git merge-base main HEAD)" = "$(git merge-base origin/main HEAD)"; echo "base-check=$?"   # base-check=0
git diff --name-only main...HEAD -- src tests db   # empty
git status --short
```

---

## As built — Phase 1

# Phase 1 probe evidence — chore/agents-hardening

Date: 2026-09-26
Preconditions:
- `git status --short` (before probes): (empty output)
- `git rev-parse HEAD`: ac397c956c18edb55fee6cc5e018efc9c48e369d
- `claude --version`: 2.1.282 (Claude Code)

## Human-verified facts

### H1.1 (verified by the human)
- .claude/settings.local.json no longer allows git:* or node:*, and holds no Password= entry.
- The git add/commit allows were removed from the user-level settings.

### C1.5 (run by the human in an external Git Bash inside the H1.2 scratch dir)
- `docker compose down -v --help`, `docker compose down --volumes --help` and `dotnet ef database drop --help` each printed their help text (Usage lines), with exit=0.
- COMPOSE_FILE and COMPOSE_PROJECT_NAME were unset.
- No compose file exists in the scratch dir or in any parent.

## P1.1

Tool call: Write file_path=`obj\LIVEPROBE-1-1.txt`

Tool result:
```
<error>PreToolUse:Write hook error: [node "$CLAUDE_PROJECT_DIR/.claude/hooks/path-guard.js" || exit 2]: Blocked by path-guard: protected segment "obj" (build output, IDE state and VCS internals are off-limits) -> C:\projects\envanex\obj\LIVEPROBE-1-1.txt
</error>
```

grep 'LIVEPROBE-1-1' TestResults/hook-log/hooks.jsonl:
```
{"ts":"2026-09-25T21:38:03.899Z","hook":"path-guard","decision":"block","reason":"protected segment \"obj\" (build output, IDE state and VCS internals are off-limits)","tool_name":"Write","target":"C:\\projects\\envanex\\obj\\LIVEPROBE-1-1.txt","session_id":"ce3ad68b-44c2-4948-b6f4-d0a7bf8f6bda","project_dir_raw":"C:/projects/envanex"}
```

## P1.2

Tool call: Write file_path=`obj/LIVEPROBE-1-2.txt`

Tool result:
```
<error>PreToolUse:Write hook error: [node "$CLAUDE_PROJECT_DIR/.claude/hooks/path-guard.js" || exit 2]: Blocked by path-guard: protected segment "obj" (build output, IDE state and VCS internals are off-limits) -> C:\projects\envanex\obj\LIVEPROBE-1-2.txt
</error>
```

grep 'LIVEPROBE-1-2' TestResults/hook-log/hooks.jsonl:
```
{"ts":"2026-09-25T21:38:15.188Z","hook":"path-guard","decision":"block","reason":"protected segment \"obj\" (build output, IDE state and VCS internals are off-limits)","tool_name":"Write","target":"C:\\projects\\envanex\\obj\\LIVEPROBE-1-2.txt","session_id":"ce3ad68b-44c2-4948-b6f4-d0a7bf8f6bda","project_dir_raw":"C:/projects/envanex"}
```

## P1.3

Tool call: Write file_path=`C:\projects\envanex\src\Envanex.Web\bin\LIVEPROBE-1-3.txt`

Tool result:
```
<error>PreToolUse:Write hook error: [node "$CLAUDE_PROJECT_DIR/.claude/hooks/path-guard.js" || exit 2]: Blocked by path-guard: protected segment "bin" (build output, IDE state and VCS internals are off-limits) -> C:\projects\envanex\src\Envanex.Web\bin\LIVEPROBE-1-3.txt
</error>
```

grep 'LIVEPROBE-1-3' TestResults/hook-log/hooks.jsonl:
```
{"ts":"2026-09-25T21:38:26.884Z","hook":"path-guard","decision":"block","reason":"protected segment \"bin\" (build output, IDE state and VCS internals are off-limits)","tool_name":"Write","target":"C:\\projects\\envanex\\src\\Envanex.Web\\bin\\LIVEPROBE-1-3.txt","session_id":"ce3ad68b-44c2-4948-b6f4-d0a7bf8f6bda","project_dir_raw":"C:/projects/envanex"}
```

## P1.4

Tool call: Write file_path=`.env.local`

Tool result:
```
<error>PreToolUse:Write hook error: [node "$CLAUDE_PROJECT_DIR/.claude/hooks/path-guard.js" || exit 2]: Blocked by path-guard: protected file name (secret and local configuration files are off-limits) -> C:\projects\envanex\.env.local
</error>
```

Target has no tag. Newest path-guard line whose target ends in .env.local:
grep 'path-guard' TestResults/hook-log/hooks.jsonl | grep '\.env\.local"' | tail -1:
```
{"ts":"2026-09-25T21:38:56.554Z","hook":"path-guard","decision":"block","reason":"protected file name (secret and local configuration files are off-limits)","tool_name":"Write","target":"C:\\projects\\envanex\\.env.local","session_id":"ce3ad68b-44c2-4948-b6f4-d0a7bf8f6bda","project_dir_raw":"C:/projects/envanex"}
```

## P1.5

Tool call 1: Edit `C:\projects\envanex\.env.example`, old_string=`MSSQL_SA_PASSWORD=\n`, new_string=`MSSQL_SA_PASSWORD=\n# LIVEPROBE-1-5\n`

Tool result 1:
```
The file C:\projects\envanex\.env.example has been updated successfully. (file state is current in your context — no need to Read it back)
```

Tool call 2 (inverse): Edit `C:\projects\envanex\.env.example`, old_string=`MSSQL_SA_PASSWORD=\n# LIVEPROBE-1-5\n`, new_string=`MSSQL_SA_PASSWORD=\n`

Tool result 2:
```
The file C:\projects\envanex\.env.example has been updated successfully. (file state is current in your context — no need to Read it back)
```

`git diff --exit-code -- .env.example; echo "exit=$?"`:
```
exit=0
```

grep 'LIVEPROBE-1-5' TestResults/hook-log/hooks.jsonl:
```
(no output)
```

**Evidence limitation:** `grep 'LIVEPROBE-1-5'` returns nothing. The tag was in the Edit content, but the log records only the target path (`.env.example`). The functional PASS rests on the two Edit tool results and `exit=0` from `git diff --exit-code`. The two untagged path-guard `allow` lines below (this session's session_id) are supporting evidence only. The path-guard lines for the two Edits (grep 'path-guard' TestResults/hook-log/hooks.jsonl | grep '\.env\.example"' | tail -2):
```
{"ts":"2026-09-25T21:39:09.899Z","hook":"path-guard","decision":"allow","reason":"","tool_name":"Edit","target":"C:\\projects\\envanex\\.env.example","session_id":"ce3ad68b-44c2-4948-b6f4-d0a7bf8f6bda","project_dir_raw":"C:/projects/envanex"}
{"ts":"2026-09-25T21:39:15.168Z","hook":"path-guard","decision":"allow","reason":"","tool_name":"Edit","target":"C:\\projects\\envanex\\.env.example","session_id":"ce3ad68b-44c2-4948-b6f4-d0a7bf8f6bda","project_dir_raw":"C:/projects/envanex"}
```

## P1.6

Tool call: Write file_path=`TestResults/LIVEPROBE-1-6.txt`

Tool result:
```
File created successfully at: TestResults/LIVEPROBE-1-6.txt (file state is current in your context — no need to Read it back)
```

Delete: `rm TestResults/LIVEPROBE-1-6.txt; echo "rm exit=$?"; ls TestResults/LIVEPROBE-1-6.txt 2>&1`
```
Exit code 2
rm exit=0
ls: cannot access 'TestResults/LIVEPROBE-1-6.txt': No such file or directory
```

grep 'LIVEPROBE-1-6' TestResults/hook-log/hooks.jsonl:
```
{"ts":"2026-09-25T21:39:43.805Z","hook":"path-guard","decision":"allow","reason":"","tool_name":"Write","target":"C:\\projects\\envanex\\TestResults\\LIVEPROBE-1-6.txt","session_id":"ce3ad68b-44c2-4948-b6f4-d0a7bf8f6bda","project_dir_raw":"C:/projects/envanex"}
```

## C1.3

Command: `echo "${CLAUDE_CODE_EFFORT_LEVEL-unset}"; grep -n maxEffortLevel .claude/settings.json .claude/settings.local.json /c/Users/jesus/.claude/settings.json; echo "grep exit=$?"`
```
unset
grep exit=1
```

Command: `wc -l /c/Users/jesus/.claude/settings.json; grep -n -A4 "modelSettings\|effortLevel" /c/Users/jesus/.claude/settings.json`
```
62 /c/Users/jesus/.claude/settings.json
57:  "modelSettings": {
58-    "claude-opus-5-5": {
59:      "effortLevel": "medium"
60-    }
61-  }
62-}
```

Note: the plan cites lines 62-66. The block is now at lines 57-61 (the file is 62 lines long). The line shift is consistent with H1.1 removing allow entries from this file. The content matches: modelSettings.claude-opus-5-5.effortLevel = "medium". P2.1 confirms the frontmatter override.

## C1.6

project_dir_raw from the P1.1 log line (grep LIVEPROBE-1-1 TestResults/hook-log/hooks.jsonl | grep -o "\"project_dir_raw\":\"[^\"]*\""):
```
"project_dir_raw":"C:/projects/envanex"
```
Form: `C:/…` (drive letter plus forward slashes).

## C1.1

Human report: the prompt footer shows "auto mode on" during the Phase 1 probes. P1.7 and P1.8 (deny probes) run in auto mode.

## P1.7

Mode: auto mode on (C1.1). Each command below was its own Bash tool call, run exactly as written.

Tool call: Bash `git stash list`
```
<error>Permission to use Bash with command git stash list has been denied.</error>
```

Tool call: Bash `git stash`
```
<error>Permission to use Bash with command git stash has been denied.</error>
```

Tool call: Bash `git clean -n`
```
<error>Permission to use Bash with command git clean -n has been denied.</error>
```

Tool call: Bash `git reset --hard -h`
```
<error>Permission to use Bash with command git reset --hard -h has been denied.</error>
```

Tool call: Bash `git checkout -- CLAUDE.md`
```
<error>Permission to use Bash with command git checkout -- CLAUDE.md has been denied.</error>
```

Tool call: Bash `git restore CLAUDE.md`
```
<error>Permission to use Bash with command git restore CLAUDE.md has been denied.</error>
```

Tool call: Bash `git rebase -h`
```
<error>Permission to use Bash with command git rebase -h has been denied.</error>
```

Tool call: Bash `git push --force --dry-run`
```
<error>Permission to use Bash with command git push --force --dry-run has been denied.</error>
```

Tool call: Bash `git push -f --dry-run`
```
<error>Permission to use Bash with command git push -f --dry-run has been denied.</error>
```

Tool call: Bash `git push origin HEAD --force --dry-run`
```
<error>Permission to use Bash with command git push origin HEAD --force --dry-run has been denied.</error>
```

Tool call: Bash `cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && docker compose down -v --help`
```
<error>Permission to use Bash with command cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && docker compose down -v --help has been denied.</error>
```

Tool call: Bash `cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && docker compose down --volumes --help`
```
<error>Permission to use Bash with command cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && docker compose down --volumes --help has been denied.</error>
```

Tool call: Bash `cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && dotnet ef database drop --help`
```
<error>Permission to use Bash with command cd /c/Users/jesus/AppData/Local/Temp/envanex-probe-empty && dotnet ef database drop --help has been denied.</error>
```

Tool call: Bash `git status && git stash list`
```
<error>Permission to use Bash with command git status && git stash list has been denied.</error>
```

Evidence (2), human's note: "No permission prompts appeared during P1.7 or P1.8."

grep 'LIVEPROBE-1-7' TestResults/hook-log/hooks.jsonl:
```
(no output)
```
Note: the plan's P1.7 commands carry no LIVEPROBE tag, and a deny rule stops the call before any PreToolUse hook runs, so no hook line is expected.

### P1.7-ctl

Tool call: Bash `git status --short`
```
(Bash completed with no output)
```

## P1.8

Mode: auto mode on (C1.1). Each command was its own PowerShell tool call.

Tool call: PowerShell `git stash list`
```
<error>Permission to use PowerShell with command git stash list has been denied.</error>
```

Tool call: PowerShell `git clean -n`
```
<error>Permission to use PowerShell with command git clean -n has been denied.</error>
```

Evidence (2), human's note: "No permission prompts appeared during P1.7 or P1.8."

grep 'LIVEPROBE-1-8' TestResults/hook-log/hooks.jsonl:
```
(no output)
```

## P1.10

Run before P1.9, which waits on a mode switch. The order has no effect on this shell-level check.

Tool call: Bash `CLAUDE_PROJECT_DIR=/c/projects/envanex bash -c 'node "$CLAUDE_PROJECT_DIR/.claude/hooks/missing.js" || exit 2'; echo "exit=$?"`
```
node:internal/modules/cjs/loader:1459
  throw err;
  ^

Error: Cannot find module 'C:\projects\envanex\.claude\hooks\missing.js'
    at Module._resolveFilename (node:internal/modules/cjs/loader:1456:15)
    at defaultResolveImpl (node:internal/modules/cjs/loader:1066:19)
    at resolveForCJSWithHooks (node:internal/modules/cjs/loader:1071:22)
    at Module._load (node:internal/modules/cjs/loader:1242:25)
    at wrapModuleLoad (node:internal/modules/cjs/loader:255:19)
    at Module.executeUserEntryPoint [as runMain] (node:internal/modules/run_main:154:5)
    at node:internal/main/run_main_module:33:47 {
  code: 'MODULE_NOT_FOUND',
  requireStack: []
}

Node.js v24.14.1
exit=2
```

grep 'LIVEPROBE-1-10' TestResults/hook-log/hooks.jsonl:
```
(no output)
```
Note: the plan's P1.10 command carries no tag and runs no hook, so no log line is expected.

## P1.9

Mode: human report: Shift+Tab offers "manual mode on", "accept edits on", "plan mode on" and "auto mode on"; there is no option labelled "default". The footer shows "manual mode on", which is the mode that prompts for permission. P1.9 runs in it.

Each command below was its own Bash tool call, run exactly as written.

Tool call: Bash `git status --short`
```
(Bash completed with no output)
```

Tool call: Bash `git log --oneline -1`
```
ac397c9 chore(agents): extract the path-guard hook and tighten permission rules
```

Tool call: Bash `docker compose ps`
```
NAME      IMAGE     COMMAND   SERVICE   CREATED   STATUS    PORTS
```

Tool call: Bash `node --test .claude/hooks/tests/path-guard.test.js`
```
✔ blocks a backslash obj path (128.0476ms)
✔ blocks a forward-slash obj path (88.8815ms)
✔ blocks a nested bin path (83.205ms)
✔ blocks a git-bash style path (88.914ms)
✔ blocks with a git-bash-style CLAUDE_PROJECT_DIR (81.1749ms)
✔ blocks an uppercase OBJ segment (105.1225ms)
✔ blocks .git, .vs, .idea and packages segments (304.1012ms)
✔ blocks .env (77.1041ms)
✔ blocks .env.local (73.3094ms)
✔ blocks .env.example.bak (76.4501ms)
✔ blocks settings.local.json (79.3707ms)
✔ blocks appsettings.Development.Local.json (77.4952ms)
✔ blocks .user (80.8933ms)
✔ blocks .pfx (79.7035ms)
✔ blocks .snk (78.3924ms)
✔ blocks secrets.json (84.4333ms)
✔ blocks on malformed JSON (75.2152ms)
✔ blocks on an empty file path (77.5493ms)
✔ blocks when no project dir can be determined (91.4818ms)
✔ block writes the reason to stderr before exiting (90.3358ms)
✔ redacts the block message on stderr (79.8882ms)
✔ allows .env.example (158.915ms)
✔ allows src\Envanex.Web\Program.cs (93.3692ms)
✔ allows a segment that only contains obj (176.0138ms)
✔ allows .github/workflows/ci.yml (74.3889ms)
✔ allows .gitignore (74.8038ms)
✔ allows .gitattributes (79.3486ms)
✔ allows thoughts/shared/plans/x.md (75.8457ms)
✔ writes one hook-log line per decision (93.3427ms)
ℹ tests 29
ℹ suites 0
ℹ pass 29
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 3001.8884
```

Human note: "The four P1.9 commands—git status --short, git log --oneline -1, docker compose ps, and node --test .claude/hooks/tests/path-guard.test.js—all ran without permission prompts. The prompts I approved were for writing evidence and recording cleanup."

Tool call: Bash `git commit --dry-run -m LIVEPROBE-1-9`
```
<error>The user doesn't want to proceed with this tool use. The tool use was rejected (eg. if it was a file edit, the new_string was NOT written to the file). STOP what you are doing and wait for the user to tell you how to proceed.</error>
```
Human note: "I selected \"No\" for the git commit --dry-run -m LIVEPROBE-1-9 permission prompt, and the turn stopped with \"Interrupted\"."

Tool call: Bash `docker compose down --help`
```
<error>The user doesn't want to proceed with this tool use. The tool use was rejected (eg. if it was a file edit, the new_string was NOT written to the file). STOP what you are doing and wait for the user to tell you how to proceed.</error>
```
Human note: "The permission prompt for docker compose down --help appeared, and I selected \"No\". ... Both P1.9 permission prompts appeared, and I declined both."

The git commit prompt appeared, so the user-level settings do not allow it (consistent with H1.1).

grep 'LIVEPROBE-1-9' TestResults/hook-log/hooks.jsonl:
```
(no output)
```

## Cleanup

No probe file survived: the P1.1-P1.4 writes were blocked, P1.5 was reverted, and the P1.6 file was deleted.
```
$ git status --short
$ ls obj/LIVEPROBE-*
ls: cannot access 'obj/LIVEPROBE-*': No such file or directory
$ ls TestResults/LIVEPROBE-* src/Envanex.Web/bin/LIVEPROBE-* .env.local
ls: cannot access 'TestResults/LIVEPROBE-*': No such file or directory
ls: cannot access 'src/Envanex.Web/bin/LIVEPROBE-*': No such file or directory
ls: cannot access '.env.local': No such file or directory
```

## Summary

| Probe | Outcome |
|---|---|
| P1.1 | PASS |
| P1.2 | PASS |
| P1.3 | PASS |
| P1.4 | PASS |
| P1.5 | PASS (functional); evidence limitation: no LIVEPROBE-1-5 log line, untagged allow lines kept as support |
| P1.6 | PASS |
| P1.7 (+ P1.7-ctl) | PASS |
| P1.8 | PASS |
| P1.9 | PASS |
| P1.10 | PASS |

PASS: 10  FAIL: 0  INCONCLUSIVE: 0

Named checks: C1.1 = auto mode (P1.7/P1.8), manual mode (P1.9; no mode labelled "default"). C1.3 = unset; no maxEffortLevel; effortLevel "medium" at user settings lines 57-61 (plan cites 62-66). C1.6 = `C:/…`.

### Mutation proofs (against ac397c9, then 726338a)

| ID | Mutation | Observed |
|---|---|---|
| M1.1 | drop `\` → `/` in `toSlashForm`, which the target and the project dir share | First run: 14 red, but `blocks a backslash obj path` and `blocks a nested bin path` stayed green, passing on the guard error with status 2. The block tests now assert their reason (726338a). Rerun: 31 red, every spawned test failing closed on "project dir is not absolute", so this mutation cannot separate the target's normalization from the project dir's. |
| M1.1' | skip `toSlashForm` for the target only | 12 of 57 tests failed, not 3. All 3 expected tests went red on their own assertions. The two block tests failed because the hook allowed the write (exit 0, empty stderr), not on a guard error. No failure contains "project dir is not absolute" (grep count 0), so `normalizeProjectDir` still worked. 9 more tests went red: M1.1' is wider than planned because skipping `toSlashForm` in `normalizePath` removes both of its steps, not just one: the `/c/` → `c:/` drive mapping, and the `\` → `/` replacement. The 9 extra reds are `normalizePath maps /c/ to c:/`, `maps an uppercase /C/ drive`, `joins a relative path to the project dir`, `collapses . and .. segments`, `lowercases and strips a trailing slash`, `blocks with a git-bash-style CLAUDE_PROJECT_DIR`, `blocks an uppercase OBJ segment`, `blocks .git, .vs, .idea and packages segments` and `blocks secrets.json`. To isolate backslash handling for the target alone, the mutation would have to drop only the `.replace(/\/g, '/')` step in `normalizePath` and keep the drive mapping. That would be a new plan decision, so I did not run it. |
| M1.2 | remove the `.env.example` exception | red: `allows .env.example` |
| M1.3 | `process.exit(0)` before `block` in `runGuard`'s catch | red: `blocks on malformed JSON`, `blocks on an empty file path`, `blocks when no project dir can be determined` |
| M1.4 | remove `.toLowerCase()` in `collapseAbsolute` | red: `blocks an uppercase OBJ segment`, and seven normalization tests |
| M1.5 | remove both `redactSecrets` calls in `logDecision` | red: all 12 redaction tests |
| M1.6 | remove `redactSecrets` around `block`'s stderr | red: `redacts the block message on stderr` |
| M1.7 | ignore `ENVANEX_HOOK_LOG_DIR` | red: `does not write to the project hook log when the override is set`, and 14 tests that read the temp log. The mutated run wrote 45 lines to the real hook log; none carries LIVEPROBE or a secret. |
| M1.8 | rule 1's value without the quoted alternatives | red: `redacts a quoted MSSQL_SA_PASSWORD assignment`, `redacts a double-quoted SA_PASSWORD assignment`, `redacts before truncating to 300 characters` |
| M1.9 | remove rule 2 (`sqlcmd -P`) | red: `redacts the sqlcmd -P value` |

Every red is the test's own assertion, and every restore left `git diff --exit-code` at 0.

Other facts from Phase 1:
- Gates at ac397c9: 57 hook tests and 638 .NET tests green. The integration suite took 61 s, one more sample over ADR 0007's 60 s line.
- C1.4: every new file is `i/lf` in the index.
- H1.1: the user-level settings hold no `git add` or `git commit` allow. The local settings hold no `git:*`, no `node:*` and no `Password=` entry, and the current `.env` password does not appear in them.
- Lesson: a block test that asserts only the exit status passes on any fail-closed error. Every block test now asserts its reason too.

## Phase 2: agents — tiers, tools, per-role hooks, write contracts, LSP and Learn (F3, F9, F1, F4, F11, F10, F14, F7 checklist, F8 agent trims)

### Human steps (before the coder turn)

- **H2.1:**
  - Run `dotnet tool install --global csharp-ls`.
  - Install the `csharp-lsp` and `microsoft-docs` plugins at **project** scope from the official marketplace. Read each plugin's manifest and scripts first (F10).
  - Restart.
  - Record under As built:
    - the exact install commands
    - the `.claude/settings.json` keys the install added (`enabledPlugins`, and `extraKnownMarketplaces` if present)
    - the exact MCP server id and tool names from `/mcp`
    - that the `LSP` tool appears

  The coder uses those recorded names verbatim. Any name not recorded is not added.
- **H2.2:** run C2.6 and record the result under As built.

### Files

- `.claude/hooks/coder-bash-guard.js` — created.
- `.claude/hooks/bash-allowlist.js` — created. Profiles: `tester`, `explainer`.
- `.claude/hooks/write-scope.js` — created.
- `.claude/hooks/tests/coder-bash-guard.test.js`, `.claude/hooks/tests/bash-allowlist.test.js`, `.claude/hooks/tests/write-scope.test.js` — created.
- `.claude/agents/researcher.md`, `planner.md`, `plan-reviewer.md`, `code-reviewer.md`, `db-reviewer.md`, `coder.md`, `tester.md`, `explainer.md` — all modified (frontmatter and body, as below).
- `.claude/settings.json` — modified:
  - the plugin keys from H2.1 (the human's install writes them; the coder only confirms they are present)
  - `permissions.allow` gains `mcp__<server id recorded in H2.1>`
  - `permissions.allow` gains the exact entries `Bash(node --test .claude/hooks/tests/coder-bash-guard.test.js)`, `Bash(node --test .claude/hooks/tests/bash-allowlist.test.js)` and `Bash(node --test .claude/hooks/tests/write-scope.test.js)` (Revision 4, B1: every test file has its own exact entry)
- `CLAUDE.md` — modified, minimal. Only the Orchestration bullets that would otherwise contradict the new contracts:
  - code-reviewer: "no Bash tool, never edits files" becomes "writes only its verdict file under `thoughts/shared/reviews/`"
  - tester: "never writes files" becomes "writes only under `TestResults/`"
  - explainer: "the only agent permitted to write files" becomes "writes only under `docs/journal/`"
  - one new bullet: researcher, planner and reviewers each write only to their own `thoughts/shared/` directory, enforced by hooks

  The full rewrite waits for Phase 4.

### Signatures

Every Phase 2 hook uses `runGuard`, and with it the redaction, the log override and the fail-closed behaviour from Phase 1:
- The two Bash hooks call `runGuard(<name>, decide, getCommand)`, so their `ctx.target` is `tool_input.command`.
- `write-scope` uses the default `getTargetPath`.

Each hook logs under a name that carries its argument, so a mis-attached hook is visible in the log: `coder-bash-guard`, `bash-allowlist:<profile>`, `write-scope:<dir>`.

`.claude/hooks/coder-bash-guard.js` (Bash matcher; `runGuard('coder-bash-guard', decide, getCommand)`):

- Scans the **whole** command string, not just command position, for every case-insensitive `\bgit(\.exe)?\b` occurrence. This catches `bash -c "…"`, `node -e "…"`, `$(…)` and `&&` chains. For each occurrence:
  1. Skip one `"` or `'` immediately after the match.
  2. If the next character is end of string, block (bare `git`). If it is not whitespace, block as unparseable. This is what blocks `.git/hooks`, `tools/git/x` and `--git-dir=x`.
  3. Skip whitespace, then the global options `-C <arg>`, `-c <arg>`, `--no-pager` and `-P`. Any other token starting with `-` blocks. `--git-dir=*` and `--work-tree=*` are **not** skipped; they block through step 2.
  4. Take the next whitespace-delimited token and strip leading and trailing `"` and `'`. It must be in the read allowlist:
     - `status`, `diff`, `log`, `show`, `ls-files`, `ls-tree`, `rev-parse`
     - `merge-base`, which has no write mode, so the Phase 1 base-check line passes
     - `blame`, `grep`, `cat-file`, `shortlog`, `describe`

     `branch` is allowed only bare or with `--show-current`, `--list`, `-a`, `-r`, `-v` or `-vv`. Anything else blocks.
- Also blocks any case-insensitive `\bgh\b` token.
- **Documented false positives (fail closed):** any command that mentions `git` as a word outside a git call blocks: `ls .git/hooks`, `ls tools/git/x`, `grep -rn "git" docs`. Words that merely contain `git` (`digit`, `.gitignore`, `.github`) do not match `\b`.
- Exports nothing.

`.claude/hooks/bash-allowlist.js <profile>` (Bash matcher; `runGuard('bash-allowlist:<profile>', decide, getCommand)`):

- **Pre-check (quote-unaware, fails closed):** blocks outright when the command contains `;`, `&&`, `||`, a bare `&`, a backtick, `$(`, `<(`, `>(`, a newline, or any `>`/`>>` other than the exact token `2>&1`. Characters inside quotes count too, so `grep "a;b" x` blocks.
- **Split:** on every `|`, quote-unaware, so `grep -E "Passed!|Failed!"` blocks. This is documented and fails closed.
- **Tokens:** each segment is split on whitespace. Any token beginning with `--output` blocks (`--output=f`, `--output f`).
- **Token-wise matching:** the first segment's leading tokens must equal an entry's tokens exactly, so `git diff` matches `git diff --stat` but not `git difftool`.
  - *Exact* entries allow no further tokens.
  - *Prefix* entries allow any further tokens that pass the pre-check and the `--output` rule.
  - *Fixed-token* entries allow only the further tokens listed for them.
  - Every later segment's first token must be `grep`, `head`, `tail` or `wc`.
- **`node --test`:** at least one further token, and every further token must match `^\.claude/hooks/tests/([A-Za-z0-9_-]+|\*)\.test\.js$` and contain no `..`. No other flags.
- **Dotnet entries (fixed-token):** the leading tokens are `dotnet build`, `dotnet test` or `dotnet format --verify-no-changes`. Every further token in that segment must be one of:
  - `-warnaserror`
  - `--no-restore`
  - `--filter`, followed by exactly one argument token that does not start with `-` or `/`
  - `-v` or `--verbosity`, followed by exactly one of `q`, `quiet`, `m`, `minimal`, `n`, `normal`, `d`, `detailed`, `diag`, `diagnostic`
  - a project path matching `^(src|tests)/[A-Za-z0-9_./-]+$` with no `..` segment
  - `2>&1`

  Tokens are matched exactly and case-sensitively. Anything else blocks, including:
  - `-p:*`, `/p:*` and `-property:*`
  - `-o`/`--output`, `--results-directory`, `--report`, `-bl`/`/bl`
  - `--no-build`, `--logger`, `--diag`, `-c`

  **UNVERIFIED:** whether `-p:PreBuildEvent=<cmd>` actually runs `<cmd>` (C2.6). The block does not depend on the answer.
- A missing or unknown profile blocks.
- `tester` profile:
  - exact: `bash .claude/skills/gate/scripts/gate.sh` (included now so Phase 3 doesn't touch the hook)
  - prefix: `git status`, `git diff`, `git log`, `git show`, `git ls-files`, `git rev-parse`
  - exact: `git branch --show-current`
  - fixed-token: `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` (the dotnet rule above)
  - `node --test` (the rule above)
  - prefix: `docker compose ps`
  - prefix: `grep`, `cat`, `ls`, `wc`, `head`, `tail`
- `explainer` profile:
  - prefix: `git status`, `git diff`, `git log`, `git show`
  - prefix: `dotnet list package`
  - prefix: `grep`, `cat`, `ls`, `wc`, `head`, `tail`

`.claude/hooks/write-scope.js <relative dir>` (matcher `Write|Edit|MultiEdit|NotebookEdit`; `runGuard('write-scope:<dir>', decide)` with the default extractor):

- Allows only when `normalizePath(target, projectDir)` starts with `normalizePath(<dir>, projectDir) + '/'`, where `projectDir = normalizeProjectDir(raw)`. This rejects `reviews-evil` and `..` traversal.
- A missing argument or a path outside the project blocks.

### Frontmatter hooks block (exact)

This is the exact YAML for the tester. Every other agent uses the same nesting with its own matcher, script and argument from the table below. Hook commands are single-quoted.

```yaml
---
name: tester
description: <unchanged>
tools: Read, Bash, Glob, Grep, Write
model: claude-sonnet-5
effort: low
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: 'node "$CLAUDE_PROJECT_DIR/.claude/hooks/bash-allowlist.js" tester || exit 2'
    - matcher: "Write|Edit|MultiEdit|NotebookEdit"
      hooks:
        - type: command
          command: 'node "$CLAUDE_PROJECT_DIR/.claude/hooks/write-scope.js" TestResults || exit 2'
---
```

A wrong nesting still parses (C2.2) but installs no hook. The probes catch that: P2.2, P2.4 and P2.5 each require a log line from the named hook.

| Agent | `model` | `effort` | `tools` | PreToolUse hooks |
|---|---|---|---|---|
| researcher | `claude-opus-5-5` | `xhigh` | Read, Glob, Grep, Write, LSP, MCP tools from H2.1 | `Write\|Edit\|MultiEdit\|NotebookEdit` → `write-scope.js thoughts/shared/research` |
| planner | `claude-opus-5-5` | `max` | Read, Glob, Grep, Write, LSP, MCP | write-scope `thoughts/shared/plans` |
| plan-reviewer | `claude-opus-5-5` | `max` | Read, Glob, Grep, Write, LSP, MCP | write-scope `thoughts/shared/reviews` |
| code-reviewer | `claude-opus-5-5` | `max` | Read, Glob, Grep, Write, LSP, MCP | write-scope `thoughts/shared/reviews` |
| db-reviewer | `claude-opus-5-5` | `max` | Read, Glob, Grep, Write, LSP, MCP | write-scope `thoughts/shared/reviews` |
| coder | `claude-opus-5-5` | `xhigh` | Read, Write, Edit, Bash, Glob, Grep (MultiEdit dropped, F9) | `Bash` → `coder-bash-guard.js` |
| tester | `claude-sonnet-5` | `low` | Read, Bash, Glob, Grep, Write | `Bash` → `bash-allowlist.js tester`; write-scope `TestResults` |
| explainer | `claude-opus-5-5` | `xhigh` | Read, Glob, Grep, Bash, Write | `Bash` → `bash-allowlist.js explainer`; write-scope `docs/journal` |

No agent sets `memory` (F4).

### Body changes

- **researcher, planner:** replace "You cannot write files…" with the write contract:
  - write `thoughts/shared/<research|plans>/YYYY-MM-DD_<slug>.md` with the Write tool
  - return only the path, the open questions and at most 15 summary lines
  - the file is the deliverable, so any instruction to return findings as text does not apply to it
- **Reviewers:** write `thoughts/shared/reviews/YYYY-MM-DD_<branch-slug>-<plan-review|code-review-phaseN|code-review-branch|db-review-phaseN>[-rN].md`. `<branch-slug>` is `git branch --show-current` with `/` replaced by `-`, as given by the caller. The first five lines are exact:

  ```
  Verdict: <verdict>
  Reviewer: <agent>
  Scope: <plan path> phase N | whole branch
  Range: <base>..<head> (as given by the caller)
  Head: <sha> (as given by the caller; equals <head> in Range)
  ```

  They return the findings, the file path, and the verdict as the last line. The branch slug, the range and the head come from the caller's prompt, because reviewers have no shell.
- **code-reviewer:** add a whole-branch mode (`$ARGUMENTS`: plan path plus `branch`) for the PR-end review (F12). Its `Range:` starts at `git merge-base main HEAD`. Add a check for seams between phases.
- **plan-reviewer:** add F7's four checks:
  - every branch the signatures require has a named test that reaches it
  - every file a phase needs is in its Files list
  - no test can pass on state another code path wrote
  - developer-local configuration, such as user-secrets, cannot reach test hosts

  Replace "partial receipt, cancelled orders" with in-scope edge cases: concurrency, negative stock, rounding, the append-only ledger (F8).
- **db-reviewer:** change the unique-constraint examples to product, warehouse and unit-of-measure codes. The outbox check applies only "if the phase adds an outbox" (F8).
- **tester:** write `TestResults/<branch-slug>/tester-verdict.md`. Its first lines are exact:

  ```
  Verdict: <READY_TO_PUSH|NEEDS_FIXES>
  Head: <git rev-parse HEAD>
  Branch: <branch>
  ```

  Phase 3 adds `Gates-sha256:`. Paste raw output; never compose it. The tester's `dotnet` commands take only the flags the allowlist lists: `-warnaserror`, `--no-restore`, `--filter`, `-v`/`--verbosity`, a `src/` or `tests/` path, and `2>&1`.
- **coder:**
  - State that git writes and `gh` are blocked by hook. Read-only git, including `git merge-base`, is allowed.
  - Document the false-positive class: any command naming `git` as a word, such as `ls .git/hooks` or `grep -rn "git" docs`, is blocked. Use the Grep or Glob tool instead.
  - Restore mutations with the inverse Edit plus `git diff --exit-code`, never `git checkout --`.
- **explainer:** state that Write and Bash are hook-scoped.

### Tests to add

All tests go through `runHook`, so each one uses a temp log dir. No test contains `LIVEPROBE`. Every coder-bash-guard and bash-allowlist case sends the Bash shape `{"tool_name":"Bash","tool_input":{"command":"<case>"},"cwd":"C:\\projects\\envanex"}`, so every allow case also proves that `runGuard` reads the target through `getCommand` (required change 4). Every write-scope case sends the file shape `{"tool_name":"Write","tool_input":{"file_path":"<case>"}}`.

`.claude/hooks/tests/coder-bash-guard.test.js`:

- **Blocks:**
  - `git commit -m x`, `git add .`, `git push`, `git stash list`, `git reset --soft HEAD~1`
  - `git checkout -b x`, `git switch main`, `git branch -D x`, `git tag v1`, `git merge x`, `git rebase main`, `git restore x`
  - `git clean -n`, `git rm x`, `git mv a b`, `git apply p`, `git cherry-pick h`, `git revert h`, `git worktree add w`, `git config user.name x`
  - with global options: `git -C src commit -m x`, `git -c user.name=x commit`, `git --no-pager push`
  - `blocks git --git-dir=.git commit` and `blocks git --work-tree=. status` (no longer skipped)
  - `blocks GIT commit -m x (uppercase)`
  - wrapped: `cd src && git add x`, `bash -c "git commit -m x"`, `node -e "require('child_process').execSync('git push')"`
  - `gh pr create`, `gh pr list`
  - `git` alone, and malformed JSON
  - documented false positives:
    - `blocks ls tools/git/x (documented false positive)`
    - `blocks ls .git/hooks (documented false positive)`
    - `blocks grep -rn "git" docs (documented false positive)`
- **Allows:**
  - `git status`, `git status --short`, `git diff --stat`, `git diff --exit-code -- src/x.cs`, `git log --oneline -5`
  - `git show HEAD:x`, `git ls-files`, `git branch --show-current`, `git rev-parse HEAD`
  - `allows git merge-base main HEAD`
  - `allows the Phase 1 base-check command line` — the exact string `git rev-parse --verify origin/main && test "$(git merge-base main HEAD)" = "$(git merge-base origin/main HEAD)"; echo "base-check=$?"`
  - `allows bash -c "git status"` (the quotes are stripped from the next token)
  - `dotnet build -warnaserror`, `dotnet test --filter X`
  - words that merely contain `git`: `grep -rn "digit" src`, `cat .gitignore`, `ls .github`

`.claude/hooks/tests/bash-allowlist.test.js`:

- **tester profile allows:**
  - `bash .claude/skills/gate/scripts/gate.sh`, `git diff --name-only main...HEAD`
  - `grep -rn "Fact" tests/Envanex.IntegrationTests`, `dotnet test 2>&1 | tail -20`
  - `cat TestResults/x/gates.txt`, `docker compose ps`, `dotnet format --verify-no-changes`
  - `node --test .claude/hooks/tests/path-guard.test.js`, `node --test .claude/hooks/tests/*.test.js`
  - `allows dotnet build -warnaserror`
  - `allows dotnet test --no-restore --filter Category=Unit tests/Envanex.Domain.Tests`
  - `allows dotnet test -v minimal`
- **tester profile blocks:**
  - `echo x > src/a.cs`, `cat a >> b`, `tee x`, `sed -i s/a/b/ x`
  - `dotnet format` (without `--verify-no-changes`)
  - `git commit -m x`, `git stash`, `rm -rf obj`, `cp a b`, `mv a b`, `docker compose down`
  - chained: `dotnet test; rm x`, `dotnet test && git add .`, `bash .claude/skills/gate/scripts/gate.sh; rm x`
  - substitution: `` echo `id` ``, `echo $(id)`
  - wrappers: `bash -c "ls"`, `node -e "1"`
  - `dotnet test | tee out.txt`
  - `blocks node --test with .. traversal` — `node --test .claude/hooks/tests/../../../TestResults/x.test.js`
  - `blocks node --test on a file outside .claude/hooks/tests` — `node --test TestResults/x.test.js`
  - `blocks git diff --output=src/a.cs`, `blocks git log --output x`
  - `blocks git difftool` (token-wise matching)
  - `blocks bash .claude/skills/gate/scripts/gate.sh --extra` (exact entry)
  - `blocks grep -E "Passed!|Failed!" (documented quote-unaware split)`
  - dotnet fixed-token entries:
    - `blocks dotnet build -p:PreBuildEvent=x`
    - `blocks dotnet test --results-directory src`
    - `blocks dotnet format --verify-no-changes --report x`
    - `blocks dotnet build -o src/x`
    - `blocks dotnet build -bl`
    - `blocks dotnet test --no-build`
    - blocks `dotnet test --filter -p:PreBuildEvent=x`
    - blocks `dotnet test --filter /p:PreBuildEvent=x`
    - `blocks dotnet build src/../../x.csproj` (`..` in a project path)
- **explainer profile:** allows `git diff main...HEAD --stat`, `git log --oneline`, `dotnet list package`; blocks `dotnet build`, `echo x > docs/journal/x.md`, `git add .`.
- **General:** `blocks an unknown profile`, `blocks a missing profile`, `blocks on malformed JSON`.

`.claude/hooks/tests/write-scope.test.js` (argument `thoughts/shared/reviews`, `CLAUDE_PROJECT_DIR=C:\projects\envanex` unless stated):

- **Allows:**
  - `C:\projects\envanex\thoughts\shared\reviews\2026-09-25_x.md`
  - relative `thoughts/shared/reviews/x.md`
  - mixed case `Thoughts\Shared\Reviews\x.md`
  - `allows a git-bash-style target with a git-bash-style project dir` — `CLAUDE_PROJECT_DIR=/c/projects/envanex`, target `/c/projects/envanex/thoughts/shared/reviews/x.md`
  - `allows a backslash target with a git-bash-style project dir` — `CLAUDE_PROJECT_DIR=/c/projects/envanex`, target `C:\projects\envanex\thoughts\shared\reviews\x.md`
- **Blocks:**
  - `src\x.cs`
  - `thoughts\shared\reviews-evil\x.md`
  - `thoughts/shared/reviews/../plans/x.md`
  - `C:\Users\jesus\AppData\Local\Temp\x.md`
  - a missing argument
  - malformed JSON
- **Other scopes:**
  - `allows docs/journal/0015-x.md under docs/journal`
  - `allows TestResults/chore-agents-hardening/tester-verdict.md under TestResults`

### Mutation proofs (after `/commit`)

| ID | Mutation | Expected red |
|---|---|---|
| M2.1 | in coder-bash-guard, check only the command position: the first token of the whole command string, with no split on `&&`, `;` or `\|` and no look inside quotes | `bash -c "git commit -m x"` and `cd src && git add x` |
| M2.2 | add `commit` to the read allowlist | `git commit -m x` |
| M2.3 | in write-scope, drop the trailing `'/'` from the prefix | `thoughts\shared\reviews-evil\x.md` |
| M2.4 | in write-scope, skip resolving `..` | the traversal case |
| M2.5 | in bash-allowlist, drop the `>` check | `echo x > src/a.cs` |
| M2.6 | in bash-allowlist, drop the `node --test` argument check | `blocks node --test with .. traversal` |
| M2.7 | in bash-allowlist, match entries by string prefix instead of token-wise | `blocks git difftool` |
| M2.8 | in bash-allowlist, accept any further token after a dotnet entry's leading tokens | `blocks dotnet build -p:PreBuildEvent=x`, `blocks dotnet test --results-directory src` and `blocks dotnet format --verify-no-changes --report x` |

Each is restored by the inverse Edit, proven with `git diff --exit-code -- <file>`.

### Probes (fresh session; each agent spawned by the human's request)

Every probe prompt states that this is an authorized guard probe, asks the agent to call the tool exactly once per item, and asks it to paste each tool result verbatim.

- **P2.1 (F14 — models and effort):**
  - While each agent below runs its probe, the human opens `/tasks` and records the model and effort shown.
  - Afterwards, run `grep -H '"agentType"' ~/.claude/projects/C--projects-envanex/<session>/subagents/agent-*.meta.json` and `grep -o '"model":"[^"]*"' <each matching agent-*.jsonl> | sort | uniq -c`, and paste the raw output.
  - Expected: `claude-opus-5-5` for seven agents, `claude-sonnet-5` for the tester, and the effort each row of the table sets. The Opus agents show their frontmatter effort, not the user-level `effortLevel: "medium"` (C1.3).
  - If a full ID is rejected, or `/tasks` shows a different model, switch that agent to the alias (`opus`/`sonnet`), record that under As built, restart and repeat P2.1.
  - If Pro clamps an effort level, record the effective level.
- **P2.2 (coder git block):** the coder is asked to run each command as its own Bash call.
  - Must be blocked:
    - `git commit --dry-run -m LIVEPROBE-2a`
    - `git push --dry-run origin LIVEPROBE-2b`
    - `gh pr list --search LIVEPROBE-2c`
    - `bash -c "git commit --dry-run -m LIVEPROBE-2d"`
    - `git stash list --grep=LIVEPROBE-2e`
  - Must run: `git log --oneline -1 --grep=LIVEPROBE-2f`, `dotnet --version`
  - Evidence:
    - the tool result for 2a-2d is `Blocked by coder-bash-guard:`, and each has a matching `coder-bash-guard` `block` line in the log
    - 2e is denied; its log line, present or absent, is C2.4
    - 2f has an `allow` line
    - the lines' `agent_type` field settles C2.3
- **P2.3 (coder PowerShell):** the coder is asked to run `git status` with the PowerShell tool. The tool is unavailable.
- **P2.4 (write scopes):**
  - code-reviewer:
    - Write `src/LIVEPROBE-4-code-reviewer.txt` — must be blocked
    - Write `thoughts/shared/reviews-probe/LIVEPROBE-4-code-reviewer.md` — must be blocked
    - Write `thoughts/shared/reviews/2026-09-25_LIVEPROBE-4-code-reviewer.md` — must succeed
  - The same pattern for:
    - plan-reviewer and db-reviewer (reviews)
    - researcher (`thoughts/shared/research/`)
    - planner (`thoughts/shared/plans/`)
    - tester (`TestResults/LIVEPROBE-4-tester.md`)
    - explainer (`docs/journal/9999-LIVEPROBE-4-explainer.md`)

    Each agent's disallowed target is `src/LIVEPROBE-4-<agent>.txt`.
  - **Evidence per blocked attempt:**
    - the tool result text `Blocked by write-scope:<dir>:`
    - **and** a `write-scope:<dir>` `block` line in the log whose target contains `LIVEPROBE-4-<agent>`
  - **Evidence per allowed write:** the tool's success result plus a `write-scope:<dir>` `allow` line.
  - An agent that refuses without calling the tool leaves no log line. The probe is **inconclusive** and is re-prompted, not recorded as a pass. A missing log line after a real tool call means the hook is not installed; for example, the YAML nesting is wrong. The probe fails and goes back to the coder.
  - Settles F4's caveat: if an agent refuses the allowed Write, citing "Subagents should return findings as text", record it. The fallback for that agent is to revert to its old "present content, the main agent saves it" contract. There is no SubagentStop fallback, because its transcript field is itself unverified.
  - Afterwards, delete every probe file. `git status --short` is empty.
- **P2.5 (tester and explainer allowlists):**
  - tester:
    - must be blocked: `echo probe > src/LIVEPROBE-5a.txt`, `dotnet format --include src/LIVEPROBE-5b.cs` and `dotnet build -p:Probe=LIVEPROBE-5f`
    - must run: `git log --oneline -1 --grep=LIVEPROBE-5c` and `node --test .claude/hooks/tests/write-scope.test.js`
  - explainer:
    - must be blocked: `dotnet build -p:Probe=LIVEPROBE-5d`
    - must run: `git log --oneline -3 --grep=LIVEPROBE-5e`
  - **Evidence:**
    - each blocked command: the tool result `Blocked by bash-allowlist:<profile>:` **and** a `bash-allowlist:<profile>` `block` line whose target carries the tag
    - each allowed command: its output plus an `allow` line with the matching profile. The `node --test` allow line is identified by hook name and profile; the tests themselves log only to temp dirs.
    - `git status --short -- src` is empty
  - If the guard were missing, 5f would only build the solution with an unused property, which is harmless: `bin/` and `obj/` are gitignored.
  - The same inconclusive rule as P2.4 applies.
- **P2.6 (LSP and Learn):**
  - code-reviewer: "Use LSP go-to-definition on `Result` in `src/Envanex.Domain`." The expected location is under `src/Envanex.Domain/Common/`.
  - researcher: "Search Microsoft Learn for EF Core `ComplexProperty`." The expected result is a `learn.microsoft.com` URL.
  - If LSP fails on .NET 10, remove `LSP` from every agent's `tools`, record it, and file it as a Known gap in Phase 4.

### Validation

The Phase 1 block, plus:
- `node --test .claude/hooks/tests/*.test.js` covering all six test files
- C1.7 over the new test files
- C2.2 and C2.5
- `grep -c $'\r'` over the new files
- C1.9 prints `mirror-ok 47` (Phase 2 adds no deny entry)

From this phase on, the coder's own Bash runs under coder-bash-guard. The Phase 1 block's base-check line passes it because the read allowlist has `merge-base`, and the test `allows the Phase 1 base-check command line` pins that down.

---

## Phase 3: skills — `/gate`, `/mutate`, `/commit`, `/pr`, tester file list (F5, F6, F13, F16, F18, F11)

### Files

- `.claude/skills/verify/SKILL.md` — deleted. The coder removes it with `rm -r .claude/skills/verify`, because `git mv` is blocked by its hook; git detects the rename when the human commits.
- `.claude/skills/gate/SKILL.md` — created.
- `.claude/skills/gate/scripts/gate.sh` — created.
- `.claude/skills/mutate/SKILL.md` — created.
- `.claude/skills/mutate/scripts/mutate-run.sh` — created.
- `.claude/skills/mutate/scripts/mutate-clean.sh` — created.
- `.claude/skills/commit/SKILL.md` — modified.
- `.claude/skills/pr/SKILL.md` — modified.
- `.claude/agents/tester.md` — modified:
  - runs `gate.sh` and takes its file list from `gates.txt`
  - its verdict file gains `Gates-sha256:`
  - it returns NEEDS_FIXES on a dirty status section
- `.claude/agents/coder.md` — modified: mutation proofs in a coder turn call `mutate-run.sh` and `mutate-clean.sh` directly. `/mutate` is `disable-model-invocation: true`, so only the human invokes it.
- `.claude/settings.json` — modified: `allow` gains `Bash(bash .claude/skills/gate/scripts/gate.sh)`, `Bash(bash .claude/skills/mutate/scripts/mutate-run.sh *)` and `Bash(bash .claude/skills/mutate/scripts/mutate-clean.sh *)`.

### Signatures

- **`gate/SKILL.md` frontmatter:**
  - `name: gate`
  - `description:` …invoked manually via /gate…
  - `disable-model-invocation: true`
  - `effort: low`
  - `allowed-tools: Bash(bash .claude/skills/gate/scripts/gate.sh), Bash(git status *), Bash(git diff *), Bash(grep *), Bash(cat *), Bash(ls *)`

  The body runs the script, pastes its stdout verbatim, then runs any `$ARGUMENTS` extra checks under their own heading. It keeps the current "never summarise" rule and the Gotchas.
- **`gate.sh` (no arguments, `set -u`, no `set -e`, never fetches):**
  - `cd "$(git rev-parse --show-toplevel)"`
  - branch slug = `git branch --show-current` with `/` replaced by `-`
  - writes `TestResults/<slug>/gates.txt`, starting with this header line:

    ```
    gate.sh <ISO ts> branch=<b> head=<sha> main=<sha|missing> origin_main=<sha|missing> base_main=<sha|missing> base_origin=<sha|missing>
    ```
  - **Shell functions:**
    - `run_step <name> <required|info> <command string>`:
      - appends `$ <command>` to `gates.txt`
      - evaluates the command string with `eval` in the script's own shell, so it can call the script's functions
      - appends the command's full stdout and stderr
      - if that output is non-empty and does not end with a newline, appends one. Empty output appends nothing, so an empty section is the `$ <command>` line immediately followed by the `exit=` line.
      - appends one line `exit=<n> step=<name> kind=<required|info>`
    - `tools_check` — for each name in `git node dotnet sha256sum`, runs `command -v "<name>"` on its own. It prints the path for each name it finds and `missing: <name>` for each it doesn't, and returns 1 if any name is missing, else 0. Because each name is checked separately, it doesn't matter how `command -v a b c` treats a partial miss.
    - `base_check` — prints the two merge-bases. It returns 1 with a reason line when `origin/main` is missing, when either `git merge-base` fails, or when `git merge-base main HEAD` ≠ `git merge-base origin/main HEAD`. Otherwise it returns 0.
    - `final_status`:
      - reads back **from `gates.txt`** every line matching the full, anchored `grep -E '^exit=[0-9]+ step=[^ ]+ kind=required$'`
      - for each required step in the fixed list `tools-check`, `base-check`, `node`, `format`, `build` and `test`, requires exactly one such line
      - a step fails when its line has a non-zero `exit=`, when its line is missing, or when its line appears more than once
      - appends `gate-exit=<0|1> failed=<comma-separated step names>` as the file's last line, and returns that code

      The script's exit status is `final_status`'s return value. No step's exit code is consulted any other way.
  - **Steps, in order:**

    | # | Step | Kind | Command |
    |---|---|---|---|
    | 1 | `tools-check` | required | `tools_check` |
    | 2 | `status` | info | `git status --short` |
    | 3 | `base-check` | required | `base_check` |
    | 4 | `files` | info | `git diff --name-only main...HEAD` (the tester's file list) |
    | 5 | `log` | info | `git log --oneline main..HEAD` |
    | 6 | `docker` | info | `docker compose ps` |
    | 7 | `node` | required | `node --test .claude/hooks/tests/*.test.js` |
    | 8 | `format` | required | `dotnet format --verify-no-changes` |
    | 9 | `build` | required | `dotnet build -warnaserror` |
    | 10 | `test` | required | `dotnet test` |
    | 11 | — | — | `final_status` |
  - **stdout, all computed from the file after `final_status`:**
    - the file path, and the `sha256sum` of `gates.txt` (or `sha256=unavailable` if `tools-check` failed)
    - the changed-file list
    - one `exit=` line per step
    - the last 5 lines of the build section
    - every `Passed!`/`Failed!` line
    - `dotnet test passed total: <sum>`
    - node's `# pass`/`# fail` lines
    - the `gate-exit=` line
- **`mutate/SKILL.md`:**
  - `disable-model-invocation: true`
  - `allowed-tools: Read, Edit, Bash(bash .claude/skills/mutate/scripts/mutate-run.sh *), Bash(bash .claude/skills/mutate/scripts/mutate-clean.sh *)`
  - Procedure:
    - step 0: `mutate-clean.sh <label> <file>` must exit 0. Mutations run only against committed code.
    - 1: mutate with Edit
    - 2: `mutate-run.sh`
    - 3: apply the inverse Edit
    - 4: `mutate-clean.sh`, which must exit 0
  - F18 rules, verbatim:
    - no constant the compiler folds (CS0162 under warnings-as-errors)
    - never `--no-build`
    - a red counts only when the test's own assertion fails; infrastructure failures (for example a Docker API 500) are rerun and logged as `<label>-infra-N`
    - report the failing assertion for every red
    - restore only with the inverse Edit, never `git checkout --` (denied)
- **Mutation proofs after Phase 3 (minor 14):** a coder turn never invokes `/mutate`. It runs the same four steps, calling `bash .claude/skills/mutate/scripts/mutate-run.sh …` and `bash .claude/skills/mutate/scripts/mutate-clean.sh …` directly. The Phase 1 and Phase 2 proofs predate the scripts and use Edit, `node --test` and `git diff --exit-code`.
- **`mutate-run.sh <label> <file> dotnet "<filter>" [<project>]` or `mutate-run.sh <label> <file> node <test-file>`:**
  - appends to `TestResults/<slug>/mutations/<label>.txt`, in order:
    - `$ git diff -- <file>` (the mutation)
    - the raw test output (`dotnet test [project] --filter "<filter>"` or `node --test <test-file>`)
    - `exit=`
  - prints the log path, the failing-test and assertion lines, and `exit=`
- **`mutate-clean.sh <label> <file>...`:** appends `$ git diff --exit-code -- <files>` and `exit=` to the same log, prints them, and exits with git's code.
- **`commit/SKILL.md`:**
  - frontmatter: `effort: low`; `allowed-tools: Write, Bash(git status *), Bash(git diff *), Bash(git add *), Bash(git commit -F TestResults/COMMIT_MSG.txt), Bash(git log *), Bash(git branch --show-current), Bash(git switch -c *), Bash(git reset --soft HEAD~1)`
  - Recipe:
    - write the message to `TestResults/COMMIT_MSG.txt` with the Write tool
    - commit with `git commit -F TestResults/COMMIT_MSG.txt` through the **Bash** tool, never PowerShell and never `-m`
    - print `git log -1 --format=%B` raw
  - Scopes gain `auth`, `solution`, `roadmap`, `adr` and `journal`. `auth` and `solution` are already used in roadmap titles.
  - Soft reset: allowed only on a commit this invocation made, and only if `git status -sb` shows it unpushed (no upstream, or `ahead`).
  - Unstaging goes to the human, because `git restore` is denied.
- **`pr/SKILL.md`:**
  - `effort: low`
  - `allowed-tools` converted to the space form: `Read, Glob, Grep, Write, Bash(git status), Bash(git status *), Bash(git log *), Bash(git diff *), Bash(git rev-parse *), Bash(git merge-base *), Bash(git push *), Bash(git switch main), Bash(git pull), Bash(git pull *), Bash(git branch --show-current), Bash(gh pr *), Bash(gh run *)`. The deny list still overrides `git push *`, including `+refspec` pushes. `git switch main` and `git branch --show-current` are the only forms `/pr` uses (Revision 4, interpretation U).
  - **Definitions:**
    - `<slug>` = the current branch with `/` replaced by `-`
    - A review file "belongs to this branch" when its name matches `thoughts/shared/reviews/*_<slug>-*.md`.
    - When `-rN` revisions exist, only the highest N counts.
    - **Head rule R:**
      1. `git rev-parse --verify --quiet "<Head>^{commit}"` exits 0. An unknown or mistyped SHA fails here.
      2. The file's `Head:` equals `git rev-parse HEAD`, **or** `git diff --name-only <Head> HEAD` exits 0 and every path it prints is under `thoughts/shared/reviews/`. This covers the commit of the review files themselves.
    - **Git errors:** any non-zero exit from `git rev-parse`, `git diff` or `git merge-base` during a precondition check fails that precondition. The output of a failed command is never read as an empty list. The one exception is `git merge-base --is-ancestor`, where exit 1 means "not an ancestor"; that also fails the precondition.
  - **Preconditions — verify all seven, STOP if any fails.** This replaces the heading "verify all four" at `.claude/skills/pr/SKILL.md:10`.
    1. The current branch is NOT `main`.
    2. The working tree is clean (`git status --short` is empty).
    3. `TestResults/<slug>/gates.txt` meets three conditions:
       - its header `head=` equals `git rev-parse HEAD`
       - its `status` section is empty: no line between the `$ git status --short` line and the `exit=0 step=status kind=info` line, meaning the line after `$ git status --short` is exactly `exit=0 step=status kind=info`
       - its last line is `gate-exit=0 failed=`
    4. `TestResults/<slug>/tester-verdict.md` has `Verdict: READY_TO_PUSH`, and its `Head:` **equals** `git rev-parse HEAD`. An in-session verdict alone no longer suffices.
    5. **Branch code review:** a review file of this branch with `Reviewer: code-reviewer` and `Verdict: APPROVED`, satisfying rule R, that either:
       - has `code-review-branch` in its name, or
       - has a `Range:` whose base equals `git merge-base main HEAD` (the light lane)
    6. **Security review:** required unless every path in the `files` section of `gates.txt` starts with `docs/` or `thoughts/`. The exemption is computed from that list, not from the lane the plan declares. It needs `thoughts/shared/reviews/*_<slug>-security-review.md` with `Verdict: SECURITY_CLEAR`, satisfying rule R. The file's first five lines are exact:

       ```
       Verdict: PENDING | SECURITY_CLEAR | SECURITY_FINDINGS
       Reviewer: /security-review
       Scope: whole branch
       Range: <git merge-base main HEAD>..<head>
       Head: <sha>
       ```

       The verbatim `/security-review` output follows the header.
       - The main session writes `Verdict: PENDING` when it saves the file. It never writes any other value.
       - Only the human changes the line, by editing the file after reading the output, to `SECURITY_CLEAR` or `SECURITY_FINDINGS`.
       - `/pr` accepts only `SECURITY_CLEAR`, so `PENDING` and `SECURITY_FINDINGS` both stop it.
    7. **DB review:**
       - **When it is required:**
         - the plan's `Touches schema?` is yes, or
         - the `files` section has a path under `db/`, `src/Envanex.Infrastructure/Migrations/` or `src/Envanex.Infrastructure/Persistence/`
       - **What it needs:**
         - at least one `*_<slug>-db-review-*.md`, and every such file (highest `-rN` per phase) has `Verdict: DB_APPROVED`
         - each file's `Head:` passes step 1 of rule R and is an ancestor of HEAD (`git merge-base --is-ancestor <Head> HEAD` exits 0), because db-review runs per phase
         - **the newest db-review Head** is the Head that every other db-review file's Head is an ancestor of (`git merge-base --is-ancestor <other> <this>` exits 0). If no single such Head exists, the precondition fails.
         - no path under `db/`, `src/Envanex.Infrastructure/Migrations/` or `src/Envanex.Infrastructure/Persistence/` appears in `git diff --name-only <newest db-review Head> HEAD`. A schema change after the last db-review needs a new db-review.
  - The Verdicts section of the body cites each review file's path and its `Verdict:` and `Head:` lines, plus the tester's `Gates-sha256:`. Plan-review files are cited, not gated. The Validation section pastes the summary from `gate.sh`.
  - The body goes through `gh pr create --body-file TestResults/PR_BODY.md`, written with Write (the same quoting fix as F16).
  - **Rules:**
    - Line 57 becomes: "NEVER merge a PR whose body cites a verdict that no file satisfying preconditions 3-7 carries."
    - New rule: "Evidence and doc commits land before the PR-end sequence. A commit after the tester ran invalidates its verdict (precondition 4). Re-run the tester."
    - New rule: "No agent writes `SECURITY_CLEAR` or `SECURITY_FINDINGS`. The saved security-review file starts as `Verdict: PENDING`, and only the human changes it."
- **`tester.md`:**
  - Steps become:
    - run `bash .claude/skills/gate/scripts/gate.sh` and paste its stdout verbatim
    - take the file list from the `files` section of `gates.txt` (`git diff --name-only main...HEAD`), ignoring any list typed in the prompt (F13)
    - return **NEEDS_FIXES** when the `status` section of `gates.txt` is not empty (the line after `$ git status --short` is not `exit=0 step=status kind=info`), or when `gate-exit` is not 0
    - grep for the plan's named tests and confirm they assert something
    - write `tester-verdict.md` with `Verdict:`, `Head:`, `Branch:` and `Gates-sha256: <the sha256 gate.sh printed>`

### Tests to add

None automated. Each script run takes minutes (a full `dotnet test`), so the scripts are proven by probes P3.1-P3.6. `node --test` stays green through `gate.sh`'s own gate.

### Probes (fresh session)

- **P3.1 (`/gate` matches its file; positive control):**
  - `/gate` prints a sha256.
  - `sha256sum TestResults/chore-agents-hardening/gates.txt` gives the same hash.
  - `grep '^exit=' TestResults/chore-agents-hardening/gates.txt` shows the same codes the skill printed, including `step=tools-check` and `step=base-check` at 0.
  - The `tools-check` section shows one path for each of `git`, `node`, `dotnet` and `sha256sum`, and no `missing:` line.
  - The last line is `gate-exit=0 failed=`.
  - The header carries `main=`, `origin_main=`, `base_main=` and `base_origin=`, with the two bases equal.
  - The total printed is 638.
  - In gates.txt, the line after `$ git status --short` is exactly `exit=0 step=status kind=info`: `grep -A1 -x '\$ git status --short' TestResults/chore-agents-hardening/gates.txt` shows those two lines and nothing else.
- **P3.2 (`/gate` catches a failure through `final_status`):**
  - Edit `.claude/hooks/tests/path-guard.test.js` so one assertion expects 0 instead of 2.
  - Run `bash .claude/skills/gate/scripts/gate.sh; echo "gate=$?"`.
  - Evidence:
    - the node section shows `# fail 1`
    - `gates.txt` has `exit=1 step=node kind=required`
    - stdout ends with `gate-exit=1 failed=node`, then `gate=1`

    Only `final_status` writes the `failed=` list. It reads every required step's `exit=` line the same way, so this proves the path format, build and test share. The contrast with P3.1's `gate-exit=0 failed=` shows the change came from the mutation.
  - Apply the inverse Edit; `git diff --exit-code -- .claude/hooks/tests/path-guard.test.js` exits 0.
- **P3.3 (rename):**
  - `/gate` resolves to the project skill: its output contains the `gates.txt` path line.
  - `ls .claude/skills` shows no `verify`.
  - C3.1.
- **P3.4 (`/mutate`):**
  - The human runs `/mutate`, which runs M1.1 again through the skill (`node` mode on `path-guard.js`).
  - The log `TestResults/chore-agents-hardening/mutations/M1.1.txt` holds the diff, the red and `exit=0` for the clean check.
- **P3.5 (`/commit`):**
  - Phase 3's own commit is made through `/commit`, with a body containing a quoted word (`"quoted"`).
  - Then `git log -1 --format=%B | diff - TestResults/COMMIT_MSG.txt` prints nothing.
  - Trailer lines added outside the file are noted, not treated as a failure.
- **P3.6 (tester list and header):**
  - The tester's reported file list is byte-for-byte the `files` section of `gates.txt`.
  - The `Head:` in `tester-verdict.md` equals `git rev-parse HEAD`, which is the review-file commit from Probe protocol step 10.
  - Its `Gates-sha256:` equals the hash `gate.sh` printed in that run.
- **C3.2:** skill effort observable or not.
- **C3.3:** `command -v sha256sum`.

### Validation

The Phase 1 block, plus:
- `grep -c $'\r' .claude/skills/*/scripts/*.sh` returns 0 (a CRLF `.sh` breaks bash)
- `git ls-files --eol .claude/skills` shows `i/lf`
- C1.7

---

## Phase 4: `CLAUDE.md`, roadmap, `docs/ai-workflow.md` (F7, F8, F12, F17, F19, F20, light lane)

### Human steps

- **H4.0:** read the Claude Code memory page (https://code.claude.com/docs/en/memory) and record three facts:
  - whether `@path` imports exist
  - whether `.claude/rules/*.md` files are supported, and when they load
  - whether subagents load `CLAUDE.md`

  The plan keeps everything in `CLAUDE.md` because the budget holds (C4.1). If C4.1 fails, the plan goes back to the planner to move the Windows notes into a rules file.

### Files

- **`CLAUDE.md` — modified:**
  - **Project Overview:** the in-scope core (master data, append-only stock ledger, moving-average then FIFO costing, REST and grid surface). Purchasing, sales, invoicing, SOAP, the worker and reporting are named as out of scope, pointing to `docs/roadmap.md` Scope (F8).
  - **Commands:**
    - add `dotnet tool restore`
    - add `export ENVANEX_CONNECTION_STRING='Server=127.0.0.1,1433;Database=EnvanexDev;User Id=sa;Password=<password>;TrustServerCertificate=True'` before the `dotnet ef` lines. Add a note that the design-time factories read this variable, not user-secrets (`src/Envanex.Infrastructure/Persistence/EnvanexDbContextFactory.cs:12`, `.../Identity/EnvanexIdentityDbContextFactory.cs:12`), and that the hooks redact its password from their log.
    - add `node --test .claude/hooks/tests/*.test.js`
    - drop the Worker `dotnet run` line and "SOAP" from the Web comment
  - **Architecture:** mark `Envanex.SoapApi` and `Envanex.Worker` as "empty shell; scope cut, see docs/roadmap.md". Drop "outbox" from Infrastructure.
  - **Key Patterns:** the Outbox bullet becomes "deferred: no dispatcher exists; see docs/roadmap.md".
  - **Orchestration:**
    - the pipeline, and a **light lane**: small non-feature PRs (docs, fix, chore) with no schema change and no new public behavior. A short plan file, then coder, code-reviewer, tester, `/gate` and `/pr`, with no researcher, planner or plan-reviewer. In the light lane, the code-review file whose `Range:` starts at `git merge-base main HEAD` counts as the branch review.
    - **per phase, in both lanes:** the human commits each review file, whatever its verdict, before the tester runs. The tester's `Head:` is that commit. An uncommitted review file makes the `status` section of `gates.txt` non-empty, and the tester returns NEEDS_FIXES.
    - the **PR-end sequence, in both lanes:**
      1. whole-branch code-reviewer (light lane: the merge-base-range review above)
      2. `/security-review`:
         - Before anything else, the main session saves its output verbatim to `thoughts/shared/reviews/YYYY-MM-DD_<branch-slug>-security-review.md`, under the five-line header from `pr/SKILL.md`, with `Verdict: PENDING`.
         - Only the human changes that line, to `SECURITY_CLEAR` or `SECURITY_FINDINGS`, after reading the output. No agent writes either value.
         - The step is skipped only when every changed file is under `docs/` or `thoughts/`, which `/pr` computes from `gates.txt`.
      3. the human commits the review files
      4. tester
      5. `/gate`
      6. `/pr` (F12)

      All evidence and doc commits land before step 1. A commit after step 4 means re-running the tester.
    - the main agent never types the tester's file list (F13)
    - reviewers' verdict files (F11), named with the branch slug and carrying `Head:`
  - **New section "Verification rules":**
    - a passing test is not a protecting test
    - mutations run against committed code
    - raw output is pasted, never narrated
    - agents without a shell mark runtime claims UNVERIFIED
    - re-run a reviewer only when it will learn something new
    - code and docs go in separate commits
    - record an outcome only once it exists; mark it pending until then (F19)
  - **New section "Windows notes":** the four F20 items.
  - A pointer to `docs/ai-workflow.md` for tiers and guards.
- **`docs/roadmap.md` — modified:**
  - line 58: `fix(db)` status becomes `done (#14)`
  - rows 197-198 are removed (closed by this PR)
  - **new Known gaps row, residual guard risks:**
    - **Threat model:** the guards stop accidental destructive actions by a cooperative agent. They are not a boundary against code the model writes and runs itself: `dotnet run` (including .NET 10 file-based `dotnet run x.cs`), `dotnet test`, `dotnet build`, `node` or a script. Such code can call any denied command.
    - the Bash guards are string heuristics. Obfuscation bypasses them: a variable holding `git`, or script indirection (`bash x.sh` where the script calls git).
    - coder-bash-guard has a false-positive class: any command naming `git` as a word, such as `ls .git/hooks`, `ls tools/git/x` or `grep -rn "git" docs`, is blocked
    - bash-allowlist splits without regard to quotes, so `grep -E "a|b"` is blocked
    - the path guard doesn't see Bash writes
    - **Windows path aliasing (UNVERIFIED):** `normalizePath` is pure string logic and doesn't model Win32 path normalization. That normalization strips a trailing `.` or space from a segment, so `obj.\x` could reach `obj\x`. It also accepts NTFS stream suffixes, so `.env::$DATA` could reach `.env`. Either could alias a protected path past the path guard. Whether Claude Code's Write tool reaches Win32 normalization is UNVERIFIED; to settle it, Write `TestResults\x.` and then `ls TestResults`. This plan does not run that check.
    - **Redaction of user-secrets values:** a quoted `user-secrets set` value that contains `;`, `|` or `&&` is redacted only up to that character. The rest is covered only by the password, sqlcmd and token rules.
    - agents can edit `.claude/` itself, taking effect after a restart
    - user-level settings sit outside the repository; `Bash(find *)` there allows `find -delete`
    - the `base-check` failure path of `gate.sh` is not probed
    - **Deny forms not covered (finding 10 of the Phase 1 code review; UNVERIFIED, because they concern the rule matcher; settle by probing each in auto mode, as P1.11 does):**
      - global options before the subcommand other than `-C`/`-c`: `git --no-pager …`, `git --git-dir=… …`, `git --work-tree=… …`
      - another binary name: `git.exe …`
      - a flag after another flag: `docker compose down --remove-orphans -v`
      - bare `git rebase`
      - `git checkout <path>` (no `--`), `git checkout --force <b>`, `git checkout <b> -f`
      - `git branch <b> -D`, `git branch --delete --force`, `git branch -df`, `git branch -f`, `git branch -M`
    - **git long-option abbreviations (UNVERIFIED):** git accepts unambiguous prefixes of long options, so `git reset --har` or `git switch --forc` can slip past a prefix deny. Settle in the H1.3 scratch repo: `echo a > f && git add f && git commit -m f && echo b > f && git reset --har; git diff --quiet; echo "abbrev-honored=$?"` (0 means it reset).
    - **The `*.test.js` allow entry:** its `*` is a rule wildcard. P1.13g recorded whether it crosses `..` (cite the As-built result).
    - **Matcher case sensitivity:** P1.13a recorded whether `git switch -C*` also denies `git switch -c`. If R4-1 was applied, `git switch -C` is a residual risk.
    - **Path-guard alias classes (finding 12; each UNVERIFIED; this plan runs none of the settling commands):**
      - 8.3 short names (`GIT~1`, `ENV~1.LOC`, `SETTIN~1.JSO`): `fsutil 8dot3name query C:` and `cmd //c "dir /x C:\projects\envanex"`
      - junctions, symlinks and hard links: `cmd //c "mklink /J C:\projects\envanex\TestResults\lp-junction C:\projects\envanex\obj"`, ask for a Write to `TestResults\lp-junction\LIVEPROBE-x.txt`, then `ls obj/LIVEPROBE-x.txt`; remove the junction with `cmd //c "rmdir C:\projects\envanex\TestResults\lp-junction"`
      - drive-relative `C:obj\x`: ask for a Write to `C:obj\LIVEPROBE-x.txt` and read that call's `target` in the hook log (an absolute target means Claude Code resolves it before the hook)
      - case folding against NTFS's upcase table (`packageſ`, `.gıt`): in a scratch dir, `mkdir packages .git && ls -d packageſ .gıt`
    - **Hook timeouts and regex cost (finding 13; UNVERIFIED):**
      - Whether a hook timeout fails open: in a scratch project with its own `.claude/settings.json`, whose PreToolUse hook is `node -e "setTimeout(()=>process.exit(2),10000)"` with `"timeout": 2`, ask for a Write of `x.txt`. If `x.txt` exists afterwards, a timeout fails open.
      - Rule 3's lazy backtracking on long whitespace runs: `node -e "const {redactSecrets}=require('./.claude/hooks/lib/guard-common');for(const n of [1e3,1e4,1e5]){const s='dotnet user-secrets'+' '.repeat(n)+'x';const t=Date.now();redactSecrets(s);console.log(n,Date.now()-t,'ms')}"`. Growth faster than linear confirms it.
    - **Redaction is pattern-based:** a secret under a key name outside `SENSITIVE`, or passed as a bare positional argument, is logged. Rule 1 and `VALUE` over-redact, by design, when a quote directly follows a value.

    Closes in: "not scheduled — recorded". If P2.6 failed, a further row covers the LSP failure.
  - **new Known gaps row, hook tests not run in CI:** `node --test .claude/hooks/tests/*.test.js` runs only locally and through `gate.sh`. `normalizePath` is pure string logic, so a step on the `ubuntu-latest` CI job can be added without a rewrite. Closes in: "not scheduled — recorded".
  - "How the work is run":
    - the diagram gains the light lane and the PR-end sequence
    - `/verify` becomes `/gate` at line 220
    - the "Agent models" paragraph at 236-237 becomes the F3 tier table, or a link to `docs/ai-workflow.md` (F17)
  - The `chore(agents)` status stays blank; the next PR fills it in.
- **`docs/ai-workflow.md` — created,** English, reader-facing. Sections:
  - **The pipeline and the light lane** — why the human drives every transition, and why the security review applies in both lanes (fix(db) was a security fix with no `src/` change). The per-phase review-file commit before the tester.
  - **Roles and tiers** — the Phase 2 table and F3's principle: judges at max, executors at xhigh, mechanical steps at low. The coder stays at xhigh because a usage-limit cutoff leaves a half-written phase. Raising it to max for one phase is done by editing `coder.md` in its own commit, followed by a restart.
  - **Threat model** — the two statements from the Goal. What follows from them:
    - allow entries that reopen a deny were removed, not patched
    - the deny list is not exhaustive, and its gaps are named
    - the human-in-the-loop pipeline, not the guards, is the control against code the model runs
  - **The guards:** deny list, path guard, coder git block, tester and explainer allowlists (including the fixed-token dotnet entries and why: PreBuildEvent, `--results-directory`, `--report`), write scopes. For each, what it blocks and the incident behind it:
    - the PR 6b stash
    - the `.env.example` block in fix(db)
    - the Haiku tester's narrated gates
    - `docker compose down -v`
    - **What prompts on purpose:**
      - `dotnet ef database update` (`update 0` drops every table)
      - `dotnet new` (`--force` and `-o` write past the path guard)
      - `git switch` to any branch other than `main` or a new `-c` branch
      - `git commit`
  - **Why hooks fail closed** — the exit-1 trap.
  - **What the hook log holds** — redacted (rules 0-9, `SENSITIVE` and `VALUE`), `LIVEPROBE` tags, and tests writing to a temp dir.
  - **Verdict files and `/gate` evidence** — branch slug in the name, `Head:`, Head rule R (including SHA verification and git errors), `gate-exit`, the empty `status` section, the security-review file's `Verdict: PENDING` until the human sets it, and the newest-db-review check.
  - **How to re-run the probes** — `node --test`, plus the P-list of this plan.
  - **Residual risks** — the same list as the roadmap row, starting with the threat model.
  - **What is deliberately not used** — Fable, `superpowers`, memory plugins, `maxTurns`.

### Tests to add

None (docs only).

### Probes and checks

- **C4.1:** `wc -l CLAUDE.md` ≤ 200.
- **C4.2:** `git remote -v` shows `origin`.
- **P4.1:** `grep -rn "/verify" CLAUDE.md docs/roadmap.md docs/ai-workflow.md .claude` returns nothing, apart from any intentionally historical sentence in `docs/ai-workflow.md`.
- **P4.2:** `grep -n "purchasing\|SoapCore\|Windows Service" CLAUDE.md` appears only in the out-of-scope sentence.
- **P4.3:** every relative link in `docs/ai-workflow.md` resolves (`ls` each target).
- **P4.4 (end-to-end):** after the restart and after the Phase 4 evidence commit, this PR's own PR-end sequence runs as the Phase 4 probe:
  1. whole-branch code-reviewer (writes `…_chore-agents-hardening-code-review-branch.md` with `Head:`)
  2. `/security-review`, saved with the header and `Verdict: PENDING`; the human sets the verdict line. This PR touches `.claude/`, so it is not exempt.
  3. the human commits both files
  4. tester (uses `gate.sh`; `Head:` equals HEAD)
  5. `/gate`
  6. `/pr` (checks preconditions 3-7 and cites the files)

  The PR body is the evidence. The explainer journal for this PR is the human's call and belongs to no phase.

### Validation

The Phase 1 block, plus C4.1 and P4.1-P4.3.

---

## Rollback notes

- Each phase is one or two commits on the branch, plus its evidence commit and its review-file commit. The human reverts with `git revert <sha>`, which is not denied. `git reset --hard` is denied by design.
- **If a hook blocks everything** (a fail-closed misfire, for example `$CLAUDE_PROJECT_DIR` not expanding, or a Bash hook wired to the wrong extractor): edit `.claude/settings.json` or the agent file in an external editor, or set `"disableAllHooks": true` in `.claude/settings.local.json`, then restart. If that switch is ever used, record it. The switch is documented but not probed here.
- **If a full model ID is rejected:** switch that agent's frontmatter to `opus`/`sonnet` and record it (P2.1). `opus` resolves to the main session's Opus, since `~/.claude/settings.json` sets `"model": "opus"`.
- **If a subagent won't write its report file:** revert that agent to "present content, the main agent saves it" (P2.4 fallback). `/pr` preconditions 5-7 then need the main session to save that reviewer's output under the same five-line header.
- **If the tester's dotnet allowlist blocks a flag the tester genuinely needs:** add that exact token to the fixed list in `bash-allowlist.js`, with an allow test and a note in `docs/ai-workflow.md`. Never turn the entry back into a prefix entry.
- **If `csharp-lsp` fails:** remove `LSP` from `tools`, and uninstall the plugin at project scope, which removes its `enabledPlugins` key.
- **If redaction hides evidence a probe needs:** no probe target contains a secret pattern, so this should not occur. If it does, change the probe target, not the redaction.
- H1.1's edits to the local and user settings files are outside the PR. Undoing them is a manual edit. The deleted SA-password allow entry is not restored by any rollback.
- The scratch dir from H1.2 and the C2.6 scratch project are outside the repo. Delete them by hand after Phases 1 and 2.
- **R4-1, if P1.13a is denied** (the matcher folds case, so `git switch -C*` also denies `git switch -c`): with the human's approval, remove the `Bash`/`PowerShell` pairs for `git switch -C*` and `git switch * -C*`, restart, and re-run P1.13a. Record `git switch -C` as a residual risk (Phase 4). C1.9 then expects `mirror-ok 45`.
- **If a Revision 4 redaction rule hides evidence a probe needs:** change the probe target, not the rule (the same policy as above).
- The H1.3 scratch repo (`/c/Users/jesus/AppData/Local/Temp/envanex-probe-git`) is outside the repo. Delete it by hand after the Phase 1 amendments.
