Verdict: NEEDS_REVISION
Reviewer: plan-reviewer
Scope: thoughts/shared/plans/2026-09-25_chore-agents-hardening.md
Range: f6df221d70faab6dc629b761b30b7479489d5b3c..931cdbbccf4e1f1261276f2d0e5ff678caaf79bc

## Verdict
NEEDS_REVISION

The plan maps the research closely. Every research item marked UNVERIFIED has a probe or a named check, and the phase order holds. It fails on four things:
- **Probe evidence can be faked by a test run.** The hook tests write to the same log the probes use as evidence.
- **Two Phase 2 probes can pass with the guard missing.**
- **`/pr` can accept verdict files from an earlier commit or an earlier PR.**
- **Real secrets can reach a log that gets committed to a public repo.**

The EF, dependency-rule and domain edge-case checks don't apply: this PR changes no product code.

## Rulings on the five pre-review points

**1. `/security-review` precondition vs the light lane.** The claim is right, and it is wider than stated. Neither proposed fix works.
- Plan:571-572 requires a `*-code-review-branch*` file and a security-review record.
- The light lane (Plan:643) has neither step: no whole-branch review and no `/security-review`. Every light-lane PR would stop at `/pr`.
- "Only diffs touching src/ or tests/" is the wrong test:
  - fix(db) (#14), the only security fix so far, touched only `docker-compose.yml`, `.gitignore`, `.env.example`, README and CLAUDE.md.
  - This PR touches only `.claude/`, which is permissions and hooks.
  - P4.4 (Plan:696) itself runs `/security-review` on a diff with no src/ or tests/ files.
- "Feature lane only" would also have skipped fix(db).
- Ruling:
  - Require `/security-review` in both lanes.
  - Exempt a PR only when every changed file is under `docs/` or `thoughts/`. Compute that from the gates.txt file list, not from a lane the plan declares.
  - For the light lane, accept a code-review file whose `Range:` starts at the merge-base as the whole-branch review.
  - Also define what `/pr` checks inside the security-review file. Today that file has no verdict line (Plan:572, 644).

**2. gate.sh diffing against `main`.** Partly right.
- `main...HEAD` diffs from `merge-base(main, HEAD)`. A local `main` that is merely behind still gives the correct list, as long as the branch was forked from it.
- The list goes wrong when HEAD contains origin/main commits that local main lacks. That happens with a branch created from `origin/main`, or with a pull that merges into the branch. The PR diff on GitHub would also differ.
- Ruling: fail, don't fetch.
  - A fetch needs network and credentials. It changes refs mid-gate and makes the gate non-deterministic.
  - Instead, gate.sh records the SHAs of `main` and `origin/main` and both merge-bases in its header.
  - It fails a named check, `base-check` with its own `exit=` line, when `git merge-base main HEAD` differs from `git merge-base origin/main HEAD`, or when `origin/main` is missing.
  - Plan:267 and Plan:579 use the same diff, so the same check applies there.

**3. Secrets in `hooks.jsonl`.** Right, and more likely than the `user-secrets` example suggests.
- `.claude/settings.local.json:14` shows a command with an SA password literal has already been run in this repo.
- Phase 4 (Plan:637) adds `export ENVANEX_CONNECTION_STRING='...Password=...'` to CLAUDE.md. The coder runs that before `dotnet ef`, and the coder's Bash is hooked.
- The public leak path:
  - Plan:57 has the evidence step paste `tail -n 30 hooks.jsonl` into the plan.
  - The plan is committed to a public repo.
- The `block()` stderr text (Plan:124) repeats the command, so it goes into the transcript as well.
- Ruling: redact, with tests and a mutation proof (details under Required changes).

**4. `\bgit\b` false positives in coder-bash-guard.** Acceptable as fail-closed, but it is not documented, not tested, and partly contradicts itself:
- `ls tools/git/x`, `ls .git/hooks` and `grep -rn "git" docs` all match.
- Plan:312 skips `--git-dir=*` and `--work-tree=*` as global options. But under a whole-string scan, the `git` inside `--git-dir` is itself a match (a `-` sits on each side). The skip rule can never help.
- Plan:311 doesn't say how the "next token" is found inside quotes. For `bash -c "git status"` the next token is `status"`, so the command would be blocked.
- Fix: stated under Required changes.

**5. A CI step for the hook tests.** Not needed for approval. Keep the non-goal (Plan:32).
- Once verdicts are bound to HEAD (required change 3), gate.sh runs the node tests before every `/pr`.
- A step on ubuntu (`ci.yml` runs `ubuntu-latest`) would test Windows path handling on another OS. That only works if `normalizePath` is pure string logic, which Plan:113-118 doesn't say.
- Record it as a Known-gaps row. Specify `normalizePath` as pure string logic (required change 6) so the step can be added later without a rewrite.

## Cross-plan checks

**Research UNVERIFIED items: every one is covered.**

| Research item | Covered by |
|---|---|
| F2 | P1.1-P1.3 |
| F3 "Haiku ignores effort" | C2.5 |
| F4 caveat | P2.4 (the SubagentStop fallback is dropped explicitly) |
| F5 name clash | avoided by the rename; P3.3 |
| F5 user-level skill precedence | C3.1 |
| F14 | P2.1 |
| F21 | the restart in every phase |
| auto mode / workspace trust | C1.1 / C2.1 |
| LSP on .NET 10 | P2.6 |
| memory docs | H4.0 |

Assumptions the plan itself introduces but doesn't mark:
- That `--help` is honored after `docker compose down -v` and `dotnet ef database drop`. Probe safety depends on it.
- That `sha256sum` exists in Git Bash. `command -v sha256sum` settles it.
- That Node flushes stderr before `process.exit(2)`.
- Which form `CLAUDE_PROJECT_DIR` takes on Windows: `C:\...` or `/c/...`.

**Probes that would not fail if the guard were missing:**
- P2.4 (Plan:474-475) takes an empty `git status --short -- src` as its evidence. A reviewer or tester whose prompt says "never write" may refuse without calling the tool. The probe then passes with no hook, or with a mis-nested hook.
- P2.5 (Plan:476-478) names no evidence at all.
- P1.7 (Plan:247-249): if the deny rule is missing and `--help` is not honored, the probe destroys data. The local database volume goes with `down -v`, and the database with `ef database drop`. This contradicts Plan:54.
- P3.2 (Plan:594-597) proves only the node branch of gate.sh's exit logic (Plan:538). If the format, build or test exit codes were ignored, it would still pass.

## Findings

1. **Plan:179, 419 with Plan:57, 459 — tests write to the evidence log.**
   - The hook tests set `CLAUDE_PROJECT_DIR=C:\projects\envanex`, so every `node --test` run appends to the real `TestResults/hook-log/hooks.jsonl`.
   - The coder-bash-guard tests produce the same `coder-bash-guard block` lines that P2.2 cites as evidence.
   - If C2.3 finds `agent_type` absent, the two kinds of line are indistinguishable.
   - This is the F7 check "no test can pass on state another code path wrote", applied to the probes.
2. **Plan:474-478 — P2.4 and P2.5 can pass on the agent's refusal alone** (above).
3. **Plan:569-572, `.claude/skills/pr/SKILL.md:14,16,57` — `/pr` can accept stale or foreign verdicts.**
   - `thoughts/shared/reviews/*-code-review-branch*.md` matches files from every past PR. The directory already holds `2026-09-17_pr6a-plan-review.md` and `2026-09-20_pr6b-plan-review.md`.
   - `tester-verdict.md` is not tied to HEAD. The plan's own evidence commits (Plan:60) move HEAD after the tester has run.
   - Precondition 4 (db-reviewer) and Rule line 57 ("did not actually happen in this session") are not updated, so the skill contradicts itself.
4. **Plan:120-124 — no redaction** (point 3).
5. **Plan:319-332 — bash-allowlist is underspecified, and has one real bypass:**
   - **Bypass:** the tester can Write `TestResults/x.test.js`, then run `node --test .claude/hooks/tests/../../../TestResults/x.test.js`. That is arbitrary code under the "read-only" profile.
   - **Matching unspecified:** string-prefix or token-wise? With string-prefix, `git diff` also matches `git difftool`.
   - **File writes:** `git diff --output=<f>` and `git log --output=<f>` write files.
   - **Quote-unaware split:** splitting on `|` ignores quotes, so `grep -E "Passed!|Failed!"` is blocked. Fail-closed, but undocumented.
6. **Plan:113-118 — `normalizePath` is ambiguous:**
   - It doesn't say how an absolute path is detected (`^[a-z]:/` or `^/`).
   - It doesn't say whether `/c/...` maps to `c:/...`.
   - It doesn't say whether `path.resolve` or `path.win32` is used.
   - write-scope compares normalized target and project dir, so a mismatched `CLAUDE_PROJECT_DIR` form would block every write. The coder would have to guess.
7. **Plan:339 — no exact YAML block for frontmatter `hooks`.** A wrong nesting still parses (C2.2 passes) but silently installs no hook. Combined with finding 2, nothing would catch it.
8. **Plan:311-313 — coder-bash-guard issues** (point 4).
9. **Plan:643-644 and Plan:571-572 — lane conflict** (point 1).
10. **Plan:523 — base drift** (point 2).
11. **Plan:247-249 — destructive probe commands are not safe if the deny rule is missing** (above).
12. **Plan:538, 594-597 — gate exit aggregation is only partly proven** (above).
13. **Minor, `.claude/skills/pr/SKILL.md` Rules:** it still says "in this session".
14. **Minor, Plan:204, 436 vs Plan:539-541:** mutation proofs run in a coder turn, but `/mutate` is `disable-model-invocation: true`, so the coder can't invoke it. The plan should say the coder calls `mutate-run.sh` and `mutate-clean.sh` directly.
15. **Minor, Plan:440:** M2.1's "command position" isn't defined. If it includes the position after `&&`, then `cd src && git add x` doesn't go red.
16. **Minor, Plan:60 vs Plan:54:** pasting evidence into the plan mid-phase dirties the tree, which breaks the clean-tree precondition for later probes.
17. **Minor, Plan:120-126:** it doesn't say `logDecision` must write synchronously (`appendFileSync`, `mkdirSync` recursive). Stderr before `process.exit(2)` may not flush. UNVERIFIED: settle it with a block test that asserts the stderr text.
18. **Minor:**
    - `C:\Users\jesus\.claude\settings.json:62-66` sets `modelSettings.claude-opus-5-5.effortLevel: "medium"`. C1.3 should record it, and P2.1 confirms that frontmatter overrides it.
    - The same file (line 13) allows `Bash(find *)`, which includes `find -delete`. Name it among the residual risks.
    - Also name script indirection (`bash x.sh` holding a `git` call) next to the "variable holding git" bypass (Plan:661).

## Required changes

1. **Separate the test log from the probe log.**
   - `logDecision` honours a log-dir override, for example `ENVANEX_HOOK_LOG_DIR`.
   - Every test file sets it to an `fs.mkdtempSync` directory.
   - Add the test `does not write to the project hook log when the override is set`.
   - Probe evidence must name a target string that no test uses, such as `--dry-run -m probe`.
2. **Make P2.4 and P2.5 prove the hook fired.** For each disallowed attempt, the evidence is the tool result text plus a matching `write-scope` or `bash-allowlist` block line in the log. If an agent refuses without calling the tool, the probe is inconclusive and is re-prompted, not recorded as a pass.
3. **Bind verdicts to HEAD and to the branch.**
   - Reviewer files add `Head: <sha>`. `tester-verdict.md` records `Head:` and the gates.txt sha256.
   - `/pr` accepts only files whose name contains the current branch slug and whose `Head:` equals `git rev-parse HEAD`.
   - Update pr/SKILL.md precondition 4 (accept `*-db-review-*` files) and Rule line 57.
   - State that evidence and doc commits land before the PR-end sequence.
4. **Redact before logging.**
   - Add `redactSecrets(s)` to guard-common. Apply it in `logDecision` and in `block`'s stderr, before truncating to 300 characters.
   - Cover `(password|pwd)\s*=\s*[^;'"\s]+` (case-insensitive), the value argument of `dotnet user-secrets set <key> <value>`, and `(secret|token|api[_-]?key)\s*[=:]\s*\S+`.
   - Add these tests:
     - `redacts Password in a connection string`
     - `redacts the user-secrets set value`
     - `leaves a command without secrets unchanged`
   - Add mutation M1.5 (remove the redaction call; expect the first test to go red).
5. **Tighten bash-allowlist.**
   - Match profiles token-wise.
   - `node --test` arguments must match `^\.claude/hooks/tests/([A-Za-z0-9_-]+|\*)\.test\.js$`, with no `..`.
   - Block any `--output` token.
   - State that splitting is quote-unaware and fails closed.
   - Add tests: the `..` traversal case, `git diff --output=src/a.cs`, `git difftool`.
6. **Specify `normalizePath` as a pure string algorithm.**
   - Replace `\` with `/`.
   - Map `^/([a-z])/` to `$1:/`.
   - Treat `^[a-z]:/` or `^/` as absolute; join anything else to the project dir.
   - Collapse `.` and `..` segments, lowercase, and strip a trailing `/`.
   - No `path.resolve` or `path.win32`.
   - Add a write-scope allow test with a git-bash-style target and project dir.
7. **Give one exact frontmatter YAML example** of the `hooks:` block, with PreToolUse, matcher, `type: command` and the command.
8. **Fix coder-bash-guard.**
   - Drop `--git-dir=*` and `--work-tree=*` from the skip list; they now block.
   - Specify that surrounding `"` and `'` are stripped from the next token.
   - Add the tests `blocks ls tools/git/x (documented false positive)` and `allows bash -c "git status"`, or `blocks` if you decide it should.
   - Document the false-positive class in `coder.md` and in `docs/ai-workflow.md` under Residual risks.
9. **Apply the point 1 ruling** in Phase 3 (`/pr`) and Phase 4 (the light lane): security review in both lanes, a computed docs-or-thoughts-only exemption, a merge-base Range accepted as the branch review, and a defined security-review file header.
10. **Apply the point 2 ruling** in gate.sh: the `base-check` step, with both SHAs in the header.
11. **Make the P1.7 destructive probes safe.** Run the `docker compose down -v*`, `--volumes*` and `dotnet ef database drop*` probes as `cd <empty scratch dir> && <cmd>`, and list their `--help` behaviour as UNVERIFIED.
12. **gate.sh:**
    - Build its exit status from the recorded `exit=` values through one shared function, so P3.2 exercises the common path.
    - Make the tester return NEEDS_FIXES when the `git status --short` section is not empty.
    - Add a check that `sha256sum` exists.
13. **Add a Known-gaps row:** "hook tests not run in CI" (point 5).
14. **Clarify the minor items:** 14-17 (direct script calls in the coder's mutation turn, M2.1's definition, when evidence is pasted, synchronous logging), plus the residual-risk additions in 18.

Verdict: NEEDS_REVISION
