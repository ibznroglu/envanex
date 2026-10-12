Verdict: NEEDS_REVISION
Reviewer: code-reviewer
Scope: thoughts/shared/plans/2026-09-25_chore-agents-hardening.md phase 2
Range: 84ebd9072f3a06783d71f4af4a7d95f4644c9361..4994826ca784c2cfc66268aefbde16ef77591748
Head: 4994826ca784c2cfc66268aefbde16ef77591748

## Findings

### Scope, inputs and abbreviations

- **Stage.** This is R9.7, the preliminary Phase 2 code review (plan:476). It is not Phase 2's final review or its Validation. R9.8-R9.12, the coverage ledger, As built and READY_TO_PUSH come later by design. None of them is a condition of this review.
- **Runtime claims.** I ran nothing. Every runtime claim below is UNVERIFIED and names the command that settles it.
- **Abbreviations.**
  - plan = `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`
  - ev = `TestResults/chore-agents-hardening/evidence/phase-2.md`
  - Paths are relative to `C:\projects\envanex`. Hook files are under `.claude/hooks/`.
- **Changed paths.** The prompt did not include the human-recorded output of `git diff --name-only 84ebd90… 4994826…`, which R9.7 lists as a caller value. I took the set of code files from the evidence instead:
  - ev:2576-2599: 84ebd90..ce5e7cf changes 17 paths (10 M, 7 A). They are the eight agent files, `.claude/settings.json`, `CLAUDE.md`, the three hooks, the three new test files and `.mcp.json`.
  - ev:15054-15056: nothing under `.claude`, `.mcp.json` or `CLAUDE.md` changes from ce5e7cf to c0e0b65.
  - ev:16073-16086: from c0e0b65 to 4994826, the only change under those paths is `.claude/hooks/tests/write-scope.test.js` (+32/-0).
  - The rest of the range is plan, research and review records. I read them as plan inputs, not as code.
  - UNVERIFIED: that nothing under `src/`, `tests/` or `db/` changed in the range. Settle with `git diff --name-only 84ebd90 4994826 -- src tests db` (expect no output).
- **Agent descriptions.** UNVERIFIED: that each agent's `description:` line is unchanged (plan:3081, `<unchanged>`). Settle with `git diff 84ebd90 ce5e7cf -- .claude/agents | grep -E '^[-+]description:'` (expect no output).
- **Line endings.** LF-only endings are left to Validation commands 4 and 16.

### What conforms (read and checked)

- **Hooks (plan:3006-3072).**
  - `coder-bash-guard.js` and `bash-allowlist.js` call `runGuard` with `getCommand`. `write-scope.js` uses the default extractor.
  - Each hook logs under a name that carries its argument. A missing or unknown argument blocks.
  - bash-allowlist matches the plan entry by entry: the pre-check, the quote-unaware pipe split, the `--output` rule, token-wise matching, the `node --test` rule, the fixed-token dotnet list and both profiles.
  - coder-bash-guard matches steps 1-4, the read allowlist, the `branch` arguments and the `gh` block.
- **Frontmatter (plan:3078-3109).** All eight files match on model, effort, the `tools` order, the matchers and the single-quoted hook commands. The `tools:` lines equal P2.6's eight expected lines (also recorded at ev:14304-14313). No agent sets `memory`.
- **Agent bodies (plan:3131-3171).** All present:
  - the researcher and planner write contracts
  - the reviewer file names and the five-line header
  - code-reviewer's whole-branch mode and seams check
  - plan-reviewer's four F7 checks and the new edge cases
  - db-reviewer's examples and its outbox condition
  - the tester's verdict file, header and dotnet flags
  - the coder's hook, false-positive, restore and open-decision rules
  - the explainer's hook-scoped statement
- **Settings and CLAUDE.md.**
  - `.claude/settings.json:45-51` and `:178-182` carry:
    - the three exact `node --test` entries
    - the three exact Learn tool entries
    - `enabledPlugins`
    - `disableClaudeAiConnectors` and `disableBundledSkills`
  - The deny list still has 94 entries.
  - `.mcp.json` matches H2.1.
  - `CLAUDE.md:93-108` carries the five Orchestration edits of plan:2997-3002.
- **Tests (plan:3173-3264).**
  - Every listed case is present, as a flat `test()` call with the hook prefix.
  - There are 53 + 56 + 17 = 126 new tests and 211 in total, which matches R9.6 (ev:16341-16347).
  - Block tests assert the status, the hook and the exact reason.
  - Allow tests assert the status and exactly one allow line whose hook equals the hook under test. For the Bash hooks, the line's target must also equal the input, which shows that `getCommand` was used.
  - The four Revision 9 tests (`write-scope.test.js:131-161`) assert exit 2, `Blocked by write-scope:<dir>:` and the reason, as plan:3261-3264 requires. The test commit adds nothing else.
- **Mutation proofs and live records.**
  - M2.1-M2.8 are recorded and accepted (ev:3018-3026, 6292-6309, 13962-14037, 14039-14068).
  - The live records show the hooks deciding as coded: ev:14152-14154 (P2.2), ev:14285-14287 (P2.4) and R9.5's 18 correlated decisions (ev:15747-15821).

### F1 — Medium — `.claude/hooks/bash-allowlist.js:63-65`, `:81-83`, `:88-90`: the pre-check branches that hold the allowlist have no test

**What the plan and the tests cover.** The pre-check in plan:3032 blocks:
- `;`, `&&` and `||`
- a bare `&`
- a backtick and `$(`
- `<(` and `>(`
- a newline
- `>` and `>>`

"Tests to add" (plan:3217-3221) names block tests only for `>`, `>>`, `;`, `&&`, the backtick and `$(`. The test file has exactly those (`bash-allowlist.test.js:133-139`, `:177-195`). No test input contains a newline, a bare `&`, `||`, `<(` or `>(`.

**Three of these checks are the only barrier in front of a prefix entry.** Static reading:
- **Newline (`:63-65`).** Take `git status` + LF + `rm x`. Without the check, the whitespace split at `:177` puts `rm x` among the first segment's further tokens. `prefix('git status')` matches, and the hook allows. Bash then runs both lines.
- **Bare `&` (`:88-90`).** Take `git status & rm x`. It has no other forbidden character and matches `prefix('git status')`, so it is allowed.
- **`<(` (`:81-83`).** Take `cat <(rm x)`. It has no `>` and no `&`, and it matches `prefix('cat')`, so it is allowed. Bash runs the substitution.
- **Double-covered.** `||` is also caught by the empty-segment check (`:179-181`), and `>(` by the `>` check (`:91-93`).

**No current test notices if one of the three is removed.** The only inputs containing `&` are:
- `dotnet test && git add .`, which the `&&` check at `:69` catches first
- `dotnet test 2>&1 | tail -20`, whose `2>&1` token is filtered out at `:84-87`

So all 56 bash-allowlist tests stay green with any one of the three checks deleted. This is UNVERIFIED by execution. To settle it, use Revision 7's copy procedure: delete one check in a throwaway copy and run `node --test .claude/hooks/tests/bash-allowlist.test.js` (expect 56 pass, 0 fail).

**Why it matters.**
- These checks keep a cooperative tester or explainer from chaining a write after an allowed read. `tester.md:23-26` and `explainer.md:28-30` promise exactly that. A multi-line Bash command is a form agents commonly type, so this is inside the threat model (plan:410), not obfuscation.
- It is behaviour added with no test (`coder.md:25`, `CLAUDE.md:153`). It also falls short of the plan's own aim that each new branch has a test that goes red (plan:72).
- Phase 1's review treated the same class as a finding (`thoughts/shared/reviews/2026-09-26_chore-agents-code-review-phase-1.md:32`, untested NotebookEdit). Revision 9 closed a similar gap for write-scope.
- The coder implemented the approved test list exactly. The gap is in plan:3216-3241, so the fix starts with the planner.

### F2 — Low — `.claude/hooks/bash-allowlist.js:182-186`: the `--output` rule checks the raw token, so a quoted option passes

- **The bypass.** Take `git diff "--output=src/a.cs"`. The token starts with `"`, not `--output`. `prefix('git diff')` matches and the hook allows. Bash removes the quotes, and git receives `--output=src/a.cs`. The forms `'--output=…'` and `--"output"=…` pass the same way.
- **The deny list.** The deny pairs `git diff --output*` and `git diff * --output*` (`.claude/settings.json:124-127`) are literal too.
- **UNVERIFIED** (git's behaviour and the rule matcher). Settle in the H1.3 scratch repo with `git diff "--output=x.txt"; test -f x.txt; echo "written=$?"`.
- **Assessment.** The code meets plan:3034 to the letter, and a cooperative agent does not quote this flag. The case falls under the Phase 4 residual "the Bash guards are string heuristics" (plan:3825). Not blocking.

### F3 — Low — `.claude/hooks/coder-bash-guard.js:31`, `:57-62`: the `git branch` argument list is cut without regard to quotes

- **The bypass.** Take `git branch -v ";" -D x`. The cut at the first character in `[;&|)` + backtick + CR/LF`]` leaves ` -v "`. After `stripQuotes`, its only argument is `-v`, so the guard allows. Bash passes `-v`, `;`, `-D` and `x` to git.
- **Against the plan.** plan:3025 says "Anything else blocks". The cut itself is not in the plan. It is what lets `$(git branch --show-current)` and `git branch -a && …` through.
- **The deny list.** The deny `git branch -D*` (`.claude/settings.json:118`) is a prefix match, so it misses this form too.
- **UNVERIFIED** (whether git's option parser for `branch` keeps reading options after a non-option argument). Settle in the H1.3 scratch repo with `git branch lpx && git branch -v ";" -D lpx; git branch --list lpx`. Empty output means the branch was deleted.
- **Assessment.** The form is contrived and falls in the same residual class as F2. Not blocking.

### F4 — Low — `.claude/hooks/write-scope.js:15-17`: the scope-outside-project branch has no test

- This is the only check that stops a scope argument of `.` or `..` from admitting every project path. With the scope `.`, lines 19-24 alone allow `src/x.cs`.
- No agent passes such a scope today, so the check only guards against misconfiguration.
- It is consistent with plan:3072 ("a path outside the project blocks"). Not blocking.

### F5 — Info — plan:3019 against `.claude/hooks/coder-bash-guard.js:109-111`

- The plan says `--git-dir=*` and `--work-tree=*` "block through step 2". The code blocks both at step 3 ("any other token starting with `-`"), and the tests at `coder-bash-guard.test.js:165-171` pin that reason.
- `--work-tree=.` contains no `git` word, so step 2 can never apply to it.
- Both forms block either way. ev:14065-14066 records the same correction. This is plan wording only; no code change.

### F6 — Info — untested branches that fail closed

- **coder-bash-guard.** These branches have no test:
  - `-P` (`:105`)
  - `git.exe` (`:9`)
  - the branch arguments other than `--show-current` and `-D` (`:28`)
  - the cut (`:31`)
  - "without a subcommand" (`:93`)
  - "-C without an argument" (`:99-101`)
- **bash-allowlist.** The tester's exact entry `git branch --show-current` (`:43`) has no test, although `tester.md:41-42` depends on it. Neither does the empty-segment check (`:179-181`).
- Each of these fails closed if broken: a regression shows up as a false positive, not a bypass. No change is required.

### F7 — Process — the path of this report

- The caller named `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-code-review.md`. R9.7 (plan:476) requires `…_chore-agents-hardening-code-review-phase2[-rN].md`, and `code-reviewer.md:46` requires `…-code-review-phaseN…`.
- I wrote the caller's path, because the human's preflight absence check and the correlation check's exact report path (plan:510) are tied to it.
- Revision 9 renames and overwrites no verdict file. The human therefore decides whether this name stands, and that decision fixes the base name for any `-rN` re-review.

### F8 — The evidence's stated limitations, assessed

- **ev:15660-15666** is text "prepared by Codex", so it is model-written. plan:522-526 requires every record to be named by its type. It should be labelled a model report before the ledger cites the R9.5 block. The mechanical records below it are unaffected.
- **ev:15764** (the `git check-attr` block) is the closed read allowlist working as planned (plan:3020-3025). It is not a defect. The same command also contained `git config --get`, which would have blocked too.
- **ev:15760** (CR count 161) came from the coder's own command. The independent counts at ev:15685 and ev:15776 (0) are the record, as the evidence says. Validation commands 4 and 16 check again at the validated HEAD.
- **ev:15812** holds only the last 15 lines of build output, with `PIPESTATUS[0]` = 0. That is enough for an implementation record. Validation command 9 is the gate.
- **ev:16109-16358 (R9.6)** shows 211 pass, 0 fail and 0 suites, which is consistent with the code. The block does not say which recorder produced its JSON-argv format, while R9.6 names `run` (plan:520, plan:3491-3499). The human may record the recorder's identity when the ledger cites R9.6.

## Required changes

1. **(F1) Plan revision (planner), then coder.** Add named block tests to `.claude/hooks/tests/bash-allowlist.test.js`.
   - Each test uses the tester profile, through `runHook` with the Bash shape.
   - Each asserts exit 2, `Blocked by bash-allowlist:tester:` and the exact pre-check reason.
   - Each input is one that a prefix entry would otherwise allow, so the test fails if its check is removed:
     - a newline, input `'git status\nrm x'` (a real LF): reason `a newline is not allowed`
     - a bare `&`, input `git status & rm x`: reason `a background "&" is not allowed`
     - `<(`, input `cat <(rm x)`: reason `process substitution is not allowed`
   - Recommended in the same edit, because both are named branches of plan:3032:
     - `||`, input `git status || rm x`: reason `chaining with "||" is not allowed`
     - `>(`, input `cat x >(rm x)`: reason `process substitution is not allowed`
   - Names take the hook prefix (plan:3179), and no input contains `LIVEPROBE` (C1.7).
   - They fall under "review only" in the Behaviour mapping, so no mutation proof is required.
   - What follows the correction commit:
     - R9.6 runs again, because a file under `.claude/hooks` changes (plan:489).
     - No restart is needed, because test files are excluded from the restart rule (plan:518).
     - An R9.7 re-review covers the full Range.

Not required; for the human's evaluation:
- F2 and F3: either name each form in the Phase 4 residual row, or fail closed.
  - For F2, strip `"` and `'` before the `--output` test.
  - For F3, block when the text before the cut contains a quote.
- F4: add one test with the scope argument `..`, reason `scope ".." is outside the project`.
- F7: decide whether this report's name stands.
- F8: label the Codex-prepared paragraph as a model report, and record which recorder produced R9.6.
