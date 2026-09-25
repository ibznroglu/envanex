# Plan: chore(agents) — enforce the pipeline's rules and tier models by risk

Date: 2026-09-25. Branch: `chore/agents-hardening`. Research: `thoughts/shared/research/2026-09-24_agent-workflow-hardening.md` (F1-F21). Roadmap: the `chore(agents)` row and the two Known gaps rows at `docs/roadmap.md:197-198`. Carried-forward notes: `thoughts/shared/plans/2026-09-24_fix-db-compose-sa-password.md:47,74-76,85`.

## Goal

Turn the pipeline's hard rules into things that are enforced, not just written in prompts:
- a permission deny list, mirrored for Bash and PowerShell
- a narrowed allow list
- a Windows-safe path guard that fails closed
- per-agent PreToolUse hooks: coder git-write block, tester and explainer Bash allowlists, one write scope per agent
- the model and effort tiers from F3's final table, pinned by full model ID
- write-to-file contracts and persisted verdict files (F4, F11)
- LSP and Microsoft Learn tools for the read-only agents (F10)

On top of that:
- a scripted `/gate` that replaces `/verify`
- a `/mutate` skill
- a fixed `/commit` recipe
- a generated file list for the tester
- `CLAUDE.md`, the roadmap and a new `docs/ai-workflow.md` brought up to date

Every guard is proven by a probe with raw output. A probe is a mutation proof: it shows the guard blocks what it should and allows what it should.

## Non-goals

- No product change. `src/`, `tests/`, `db/`, the migrations and `.github/workflows/ci.yml` are untouched. `dotnet test` stays at **638**.
- Fable is not used (Q1). The bundled `/verify` is not evaluated (Q4).
- The PostToolUse `dotnet format` hook stays as it is. Probing a rewrite would mean editing a `.cs` file under `src/`.
- No `maxTurns` on any agent. A turn cap on the coder recreates the half-written-phase failure F3 describes; on a reviewer it cuts the review short.
- No `permissions.defaultMode` change. The guarantees come from deny rules and hooks, and the probes run in whatever mode the human normally uses (C1.1).
- Hook tests are not added to CI. They run locally and through `gate.sh`.
- The coder's per-phase max override (F3) is documented in `docs/ai-workflow.md`, not automated.

## Touches schema?

No. db-reviewer is not required.

## ADR needed?

No (Q5): this is process, not product architecture. `docs/ai-workflow.md` takes its place (Phase 4).

## Probe protocol (applies to every phase)

1. **Order within a phase:**
   - coder implements, ending with PHASE_COMPLETE
   - the human runs `/commit`
   - a separate coder turn runs the mutation proofs against that commit
   - the human restarts Claude Code
   - the probes run in the fresh session
   - code-reviewer, then tester

   A failing probe sends the phase back to the coder before any review.
2. **Preconditions:** `git status --short` is empty and `git rev-parse HEAD` is recorded. Every probe command is chosen so that, if the guard fails, it does no harm on a clean tree: `-h`/`--help`, `--dry-run`, `-n`, and `git stash` on a clean tree.
3. **Evidence is raw:**
   - the tool result text exactly as returned
   - `tail -n 30 TestResults/hook-log/hooks.jsonl`, written by the hooks themselves (Phase 1)
   - `git status --short`

   The main session pastes these into this plan under `## As built — Phase N`. An agent's description of the result is not evidence. An outcome is recorded only once it exists, and stays marked `pending` until then (F19).
4. **Cleanup:** probe files are deleted. `git status --short` must be empty again, and `ls obj/guard-probe.txt` must answer "No such file".
5. **Restart:** quit and start again with `claude` in `C:\projects\envanex`, and record `claude --version`. When Claude Code rereads configuration mid-session is not tested (F21). The restart avoids the question.

## Named checks (UNVERIFIED items that aren't behavioural probes)

| ID | Check | Where |
|---|---|---|
| C1.1 | Record the permission mode shown in the prompt footer during the Phase 1 probes. Deny probes run in that mode; allow probes (P1.9) run in `default` mode. | Phase 1 |
| C1.2 | `node --version` ≥ 20, so `node:test` is stable. No `package.json` with `"type":"module"` in the repo root or `.claude/` (none today). | Phase 1 |
| C1.3 | `echo "${CLAUDE_CODE_EFFORT_LEVEL-unset}"` prints `unset`. No `maxEffortLevel` in any of the three settings files. | Phase 1 |
| C1.4 | `grep -c $'\r'` is 0 for every new `.js`/`.sh` file. After staging, `git ls-files --eol <file>` shows `i/lf`. | Every phase |
| C2.1 | Workspace trust was accepted for `C:\projects\envanex`, which frontmatter hooks require (F1). The path guard firing in fix(db) implies it; record the `/status` or trust state. | Phase 2 |
| C2.2 | `/agents` lists all eight project agents with no load error, which proves the YAML frontmatter parses. | Phase 2 |
| C2.3 | The hook log shows whether `agent_type`/`agent_id` appear in hook input for subagent calls. Record present or absent. | Phase 2 |
| C2.4 | Order of deny rule and PreToolUse hook: on the coder's `git stash list`, record whether a `coder-bash-guard` line appears in the log. | Phase 2 |
| C2.5 | `grep -n "model: haiku" .claude/agents/*.md` returns nothing, which makes F3's "Haiku ignores effort" moot. | Phase 2 |
| C3.1 | `ls ~/.claude/skills` has no `commit`, `pr`, `gate`, `mutate` or `verify` (F5). | Phase 3 |
| C3.2 | Whether a skill's `effort: low` can be observed while it runs. Record "observed at …" or "set, not observable". | Phase 3 |
| C4.1 | `wc -l CLAUDE.md` ≤ 200. | Phase 4 |
| C4.2 | `git remote -v` shows `origin`, which `/security-review` needs. | Phase 4 |

---

## Phase 1: global guards (F1, F2, F15, F9 pattern form)

### Human steps

- **H1.1 (before the probes; outside the PR, since both files are untracked or outside the repo):**
  - Remove `Bash(git:*)` and `Bash(node:*)` from `C:\projects\envanex\.claude\settings.local.json`.
  - Decide whether to remove `Bash(git add *)`, `Bash(git commit *)` and the `PowerShell(git commit ...)` entries from `C:\Users\jesus\.claude\settings.json`. Record the decision under As built.
  - Evidence: `grep -n 'git:\*\|node:\*' .claude/settings.local.json` returns nothing.

### Files

- `.claude/hooks/lib/guard-common.js` — created. Shared stdin, path, log and fail-closed helpers.
- `.claude/hooks/path-guard.js` — created. Replaces the inline PreToolUse `node -e` guard.
- `.claude/hooks/tests/run-hook.js` — created. Test helper that spawns a hook script with JSON on stdin.
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
- `getProjectDir(input: object): string` — `process.env.CLAUDE_PROJECT_DIR`, else `input.cwd`, else throws.
- `normalizePath(rawPath: string, projectDir: string): string`
  - replaces `\` with `/`
  - resolves relative paths against `projectDir`
  - collapses `.` and `..` segments
  - lowercases (Windows paths are case-insensitive)
  - strips any trailing `/`
- `getTargetPath(input: object): string` — `tool_input.file_path ?? tool_input.path ?? tool_input.notebook_path`. Throws if empty.
- `logDecision(projectDir: string, entry: {hook, decision, reason, tool_name, target, agent_type?, agent_id?, session_id?}): void`
  - appends one JSON line with an ISO `ts` to `<projectDir>/TestResults/hook-log/hooks.jsonl` (the directory is created recursively)
  - truncates `target` to 300 characters
  - swallows its own errors, because logging must never change a decision
- `block(ctx, reason: string): never` — logs, writes `Blocked by <hook>: <reason> -> <target>` to stderr, exits 2.
- `allow(ctx): never` — logs, exits 0.
- `runGuard(hookName: string, decide: (input, ctx) => void): void` — the entry point every guard uses. Any throw or rejection becomes `block` with `guard error: <message>`: fail closed.

`.claude/hooks/path-guard.js` blocks when the normalized path has:

- **a segment equal to** one of `bin`, `obj`, `packages`, `.vs`, `.idea`, `.git`
- **a basename that is:**
  - exactly `.env`
  - starting with `.env.` other than exactly `.env.example`
  - ending in `.env`, `.user`, `.pfx`, `.snk` or `.local.json`
  - exactly `secrets.json`

`.idea`, `.snk` and `secrets.json` come from `CLAUDE.md:143` and the `.gitignore` secrets section. Blocking `.local.json` case-insensitively now also covers `.claude/settings.local.json`, which is deliberate. Everything else is allowed.

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

`.claude/hooks/tests/path-guard.test.js` (`node:test`). The script is spawned with `CLAUDE_PROJECT_DIR=C:\projects\envanex`, so every case also exercises the stdin wiring.

**Blocks** (exit code 2):
- `blocks a backslash obj path` — `C:\projects\envanex\obj\guard-probe.txt`
- `blocks a forward-slash obj path` — `obj/guard-probe.txt`
- `blocks a nested bin path` — `src\Envanex.Web\bin\Debug\x.dll`
- `blocks a git-bash style path` — `/c/projects/envanex/obj/x`
- `blocks an uppercase OBJ segment` — `OBJ\x`
- `blocks .git, .vs, .idea and packages segments` — four sub-cases
- `blocks .env`, `blocks .env.local`, `blocks .env.example.bak`
- `blocks settings.local.json` — `.claude\settings.local.json`
- `blocks appsettings.Development.Local.json`, `blocks .user`, `blocks .pfx`, `blocks .snk`, `blocks secrets.json`
- `blocks on malformed JSON` — stdin `{not json`
- `blocks on an empty file path`

**Allows** (exit code 0):
- `allows .env.example` — both relative and absolute backslash forms
- `allows src\Envanex.Web\Program.cs`
- `allows a segment that only contains obj` — `docs/objects.md`, `src/Binary/x.cs`
- `allows .github/workflows/ci.yml`, `allows .gitignore`, `allows .gitattributes`
- `allows thoughts/shared/plans/x.md`

**Log:**
- `writes one hook-log line per decision` — sets `CLAUDE_PROJECT_DIR` to a temp directory and asserts that `TestResults/hook-log/hooks.jsonl` has a line with `"decision":"block"`

### Mutation proofs (separate coder turn, after `/commit`)

Each proof follows the same steps:
1. mutate `.claude/hooks/path-guard.js` (or `lib/guard-common.js`) with Edit
2. run `node --test .claude/hooks/tests/path-guard.test.js` and capture the raw output
3. apply the inverse Edit
4. run `git diff --exit-code -- <file>`, which must exit 0

| ID | Mutation | Expected red |
|---|---|---|
| M1.1 | drop the `\` → `/` replacement | `blocks a backslash obj path` and `blocks a nested bin path` |
| M1.2 | remove the `.env.example` exception | `allows .env.example` |
| M1.3 | make `runGuard` exit 0 on a throw | `blocks on malformed JSON` |
| M1.4 | remove the lowercasing | `blocks an uppercase OBJ segment` |

### Probes (fresh session after restart; main session)

- **P1.1 (block):** "Use the Write tool to create `obj\guard-probe.txt`." Blocked. The log shows `path-guard` `block`.
- **P1.2 (block):** the same with `obj/guard-probe.txt`. Blocked.
- **P1.3 (block):** Write `C:\projects\envanex\src\Envanex.Web\bin\guard-probe.txt`. Blocked.
- **P1.4 (block):** Write `.env.local`. Blocked.
- **P1.5 (allow):**
  - Edit `.env.example` to append `# probe` — allowed
  - apply the inverse Edit — allowed
  - `git diff --exit-code -- .env.example` exits 0

  This closes F15.
- **P1.6 (allow):**
  - Write `TestResults/guard-probe-allowed.txt` — allowed
  - delete the file

  Together with P1.1 this proves `$CLAUDE_PROJECT_DIR` expands in Claude Code's hook shell on Windows.
- **P1.7 (deny, Bash tool):** each command must be denied without running:
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
  - `docker compose down -v --help`
  - `docker compose down --volumes --help`
  - `dotnet ef database drop --help`
  - `git status && git stash list` (tests the compound-command split)
- **P1.8 (deny, PowerShell tool):** `git stash list` and `git clean -n` through the PowerShell tool. Both denied.
- **P1.9 (allow, in `default` mode):**
  - These run without a prompt: `git status --short`, `git log --oneline -1`, `docker compose ps`, `node --test .claude/hooks/tests/path-guard.test.js`.
  - These prompt, and the human declines: `git commit --dry-run -m probe` and `docker compose down --help`. If the first one does not prompt, record that user-level settings allowed it (H1.1).
- **P1.10 (fail-closed wiring, shell level):** `CLAUDE_PROJECT_DIR=/c/projects/envanex bash -c 'node "$CLAUDE_PROJECT_DIR/.claude/hooks/missing.js" || exit 2'; echo "exit=$?"` prints `exit=2`.

### Validation

```bash
node --version
node --test .claude/hooks/tests/*.test.js
node -e "JSON.parse(require('fs').readFileSync('.claude/settings.json','utf8'))"
grep -c $'\r' .claude/hooks/lib/guard-common.js .claude/hooks/path-guard.js .claude/hooks/tests/*.js
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test            # 638 passed in total
git diff --name-only main...HEAD -- src tests db   # empty
git status --short
```

---

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

### Files

- `.claude/hooks/coder-bash-guard.js` — created.
- `.claude/hooks/bash-allowlist.js` — created. Profiles: `tester`, `explainer`.
- `.claude/hooks/write-scope.js` — created.
- `.claude/hooks/tests/coder-bash-guard.test.js`, `.claude/hooks/tests/bash-allowlist.test.js`, `.claude/hooks/tests/write-scope.test.js` — created.
- `.claude/agents/researcher.md`, `planner.md`, `plan-reviewer.md`, `code-reviewer.md`, `db-reviewer.md`, `coder.md`, `tester.md`, `explainer.md` — all modified (frontmatter and body, as below).
- `.claude/settings.json` — modified:
  - the plugin keys from H2.1 (the human's install writes them; the coder only confirms they are present)
  - `permissions.allow` gains `mcp__<server id recorded in H2.1>`
- `CLAUDE.md` — modified, minimal. Only the Orchestration bullets that would otherwise contradict the new contracts:
  - code-reviewer: "no Bash tool, never edits files" becomes "writes only its verdict file under `thoughts/shared/reviews/`"
  - tester: "never writes files" becomes "writes only under `TestResults/`"
  - explainer: "the only agent permitted to write files" becomes "writes only under `docs/journal/`"
  - one new bullet: researcher, planner and reviewers each write only to their own `thoughts/shared/` directory, enforced by hooks

  The full rewrite waits for Phase 4.

### Signatures

`.claude/hooks/coder-bash-guard.js` (Bash matcher):

- Scans the **whole** command string, not just command position, for every `\bgit(\.exe)?\b` occurrence. This catches `bash -c "…"`, `node -e "…"`, `$(…)` and `&&` chains.
- Skips global options: `-C <arg>`, `-c <arg>`, `--no-pager`, `-P`, `--git-dir=*`, `--work-tree=*`.
- The next token must be in the read allowlist: `status`, `diff`, `log`, `show`, `ls-files`, `ls-tree`, `rev-parse`, `blame`, `grep`, `cat-file`, `shortlog`, `describe`. `branch` is allowed only bare or with `--show-current`, `--list`, `-a`, `-r`, `-v` or `-vv`. Anything else blocks, including an unparseable token after `git`.
- Also blocks any `\bgh\b` token.
- Exports nothing; the entry point is `runGuard('coder-bash-guard', decide)`.

`.claude/hooks/bash-allowlist.js <profile>` (Bash matcher):

- Blocks outright when the command contains `;`, `&&`, `||`, a bare `&`, a backtick, `$(`, `<(`, `>(`, a newline, or any `>`/`>>` other than the exact token `2>&1`.
- Otherwise splits on `|`. The first segment must match the profile's allowlist; every later segment must start with `grep`, `head`, `tail` or `wc`.
- A missing or unknown profile blocks.
- `tester` profile:
  - `bash .claude/skills/gate/scripts/gate.sh` (included now so Phase 3 doesn't touch the hook)
  - `git status|diff|log|show|ls-files|rev-parse`, `git branch --show-current`
  - `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`
  - `node --test .claude/hooks/tests/`
  - `docker compose ps`
  - `grep`, `cat`, `ls`, `wc`, `head`, `tail`
- `explainer` profile:
  - `git status|diff|log|show`
  - `dotnet list package`
  - `grep`, `cat`, `ls`, `wc`, `head`, `tail`

`.claude/hooks/write-scope.js <relative dir>` (matcher `Write|Edit|MultiEdit|NotebookEdit`):

- Allows only when `normalizePath(target)` starts with `normalizePath(<projectDir>/<dir>) + '/'`. This rejects `reviews-evil` and `..` traversal.
- A missing argument or a path outside the project blocks.

Agent frontmatter. Hook commands use the same `node "$CLAUDE_PROJECT_DIR/.claude/hooks/<script>.js" <args> || exit 2` form, single-quoted in YAML.

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

Body changes:

- **researcher, planner:** replace "You cannot write files…" with the write contract:
  - write `thoughts/shared/<research|plans>/YYYY-MM-DD_<slug>.md` with the Write tool
  - return only the path, the open questions and at most 15 summary lines
  - the file is the deliverable, so any instruction to return findings as text does not apply to it
- **Reviewers:** write `thoughts/shared/reviews/YYYY-MM-DD_<slug>-<plan-review|code-review-phaseN|code-review-branch|db-review-phaseN>[-rN].md`. The first four lines are exact:

  ```
  Verdict: <verdict>
  Reviewer: <agent>
  Scope: <plan path> phase N | whole branch
  Range: <base>..<head> (as given by the caller)
  ```

  They return the findings, the file path, and the verdict as the last line. The range comes from the caller's prompt, because reviewers have no shell.
- **code-reviewer:** add a whole-branch mode (`$ARGUMENTS`: plan path plus `branch`) for the PR-end review (F12), with a check for seams between phases.
- **plan-reviewer:** add F7's four checks:
  - every branch the signatures require has a named test that reaches it
  - every file a phase needs is in its Files list
  - no test can pass on state another code path wrote
  - developer-local configuration, such as user-secrets, cannot reach test hosts

  Replace "partial receipt, cancelled orders" with in-scope edge cases: concurrency, negative stock, rounding, the append-only ledger (F8).
- **db-reviewer:** change the unique-constraint examples to product, warehouse and unit-of-measure codes. The outbox check applies only "if the phase adds an outbox" (F8).
- **tester:** write `TestResults/<branch-slug>/tester-verdict.md`, with `Verdict:` as its first line. Paste raw output; never compose it.
- **coder:** state that git writes and `gh` are blocked by hook; restore mutations with the inverse Edit plus `git diff --exit-code`, never `git checkout --`.
- **explainer:** state that Write and Bash are hook-scoped.

### Tests to add

`.claude/hooks/tests/coder-bash-guard.test.js`:

- **Blocks:**
  - `git commit -m x`, `git add .`, `git push`, `git stash list`, `git reset --soft HEAD~1`
  - `git checkout -b x`, `git switch main`, `git branch -D x`, `git tag v1`, `git merge x`, `git rebase main`, `git restore x`
  - `git clean -n`, `git rm x`, `git mv a b`, `git apply p`, `git cherry-pick h`, `git revert h`, `git worktree add w`, `git config user.name x`
  - with global options: `git -C src commit -m x`, `git -c user.name=x commit`, `git --no-pager push`
  - wrapped: `cd src && git add x`, `bash -c "git commit -m x"`, `node -e "require('child_process').execSync('git push')"`
  - `gh pr create`, `gh pr list`
  - `git` alone, and malformed JSON
- **Allows:**
  - `git status`, `git status --short`, `git diff --stat`, `git diff --exit-code -- src/x.cs`, `git log --oneline -5`
  - `git show HEAD:x`, `git ls-files`, `git branch --show-current`, `git rev-parse HEAD`
  - `dotnet build -warnaserror`, `dotnet test --filter X`
  - words that merely contain `git`: `grep -rn "digit" src`, `cat .gitignore`, `ls .github`

`.claude/hooks/tests/bash-allowlist.test.js`:

- **tester profile allows:**
  - `bash .claude/skills/gate/scripts/gate.sh`, `git diff --name-only main...HEAD`
  - `grep -rn "Fact" tests/Envanex.IntegrationTests`, `dotnet test 2>&1 | tail -20`
  - `cat TestResults/x/gates.txt`, `docker compose ps`, `dotnet format --verify-no-changes`
  - `node --test .claude/hooks/tests/path-guard.test.js`
- **tester profile blocks:**
  - `echo x > src/a.cs`, `cat a >> b`, `tee x`, `sed -i s/a/b/ x`
  - `dotnet format` (without `--verify-no-changes`)
  - `git commit -m x`, `git stash`, `rm -rf obj`, `cp a b`, `mv a b`, `docker compose down`
  - chained: `dotnet test; rm x`, `dotnet test && git add .`, `bash .claude/skills/gate/scripts/gate.sh; rm x`
  - substitution: `` echo `id` ``, `echo $(id)`
  - wrappers: `bash -c "ls"`, `node -e "1"`
  - `dotnet test | tee out.txt`
- **explainer profile:** allows `git diff main...HEAD --stat`, `git log --oneline`, `dotnet list package`; blocks `dotnet build`, `echo x > docs/journal/x.md`, `git add .`.
- **General:** `blocks an unknown profile`, `blocks a missing profile`, `blocks on malformed JSON`.

`.claude/hooks/tests/write-scope.test.js` (argument `thoughts/shared/reviews`, `CLAUDE_PROJECT_DIR=C:\projects\envanex`):

- **Allows:**
  - `C:\projects\envanex\thoughts\shared\reviews\2026-09-25_x.md`
  - relative `thoughts/shared/reviews/x.md`
  - mixed case `Thoughts\Shared\Reviews\x.md`
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
| M2.1 | in coder-bash-guard, scan command position only | `bash -c "git commit -m x"` and `cd src && git add x` |
| M2.2 | add `commit` to the read allowlist | `git commit -m x` |
| M2.3 | in write-scope, drop the trailing `'/'` from the prefix | `thoughts\shared\reviews-evil\x.md` |
| M2.4 | in write-scope, skip resolving `..` | the traversal case |
| M2.5 | in bash-allowlist, drop the `>` check | `echo x > src/a.cs` |

Each is restored by the inverse Edit, proven with `git diff --exit-code -- <file>`.

### Probes (fresh session; each agent spawned by the human's request)

- **P2.1 (F14 — models and effort):**
  - While each agent below runs its probe, the human opens `/tasks` and records the model and effort shown.
  - Afterwards, run `grep -H '"agentType"' ~/.claude/projects/C--projects-envanex/<session>/subagents/agent-*.meta.json` and `grep -o '"model":"[^"]*"' <each matching agent-*.jsonl> | sort | uniq -c`, and paste the raw output.
  - Expected: `claude-opus-5-5` for seven agents, `claude-sonnet-5` for the tester, and the effort each row of the table sets.
  - If a full ID is rejected, or `/tasks` shows a different model, switch that agent to the alias (`opus`/`sonnet`), record that under As built, restart and repeat P2.1.
  - If Pro clamps an effort level, record the effective level.
- **P2.2 (coder git block):** the coder is asked to run each command as its own Bash call and paste each tool result verbatim.
  - Must be blocked: `git commit --dry-run -m probe`, `git push --dry-run`, `gh pr list`, `bash -c "git commit --dry-run -m probe"`, `git stash list`
  - Must run: `git status --short`, `dotnet --version`
  - Evidence: `coder-bash-guard` block lines in the log. This also covers C2.3 and C2.4.
- **P2.3 (coder PowerShell):** the coder is asked to run `git status` with the PowerShell tool. The tool is unavailable.
- **P2.4 (write scopes):**
  - code-reviewer:
    - Write `src/guard-probe.txt` — blocked
    - Write `thoughts/shared/reviews-probe/x.md` — blocked
    - Write `thoughts/shared/reviews/2026-09-25_probe-code-reviewer.md` — succeeds
  - The same pattern for:
    - plan-reviewer and db-reviewer (reviews)
    - researcher (`thoughts/shared/research/`)
    - planner (`thoughts/shared/plans/`)
    - tester (`TestResults/probe-tester.md`)
    - explainer (`docs/journal/9999-probe.md`)

    Each agent's disallowed target is `src/guard-probe.txt`.
  - Settles F4's caveat: if an agent refuses the allowed Write, citing "Subagents should return findings as text", record it. The fallback for that agent is to revert to its old "present content, the main agent saves it" contract. There is no SubagentStop fallback, because its transcript field is itself unverified.
  - Afterwards, delete every probe file. Evidence that the blocked writes never happened: `git status --short -- src` is empty.
- **P2.5 (tester and explainer allowlists):**
  - tester: `echo probe > src/guard-probe.txt` and `dotnet format` are blocked; `git status --short` and `node --test .claude/hooks/tests/write-scope.test.js` run.
  - explainer: `dotnet build` is blocked; `git log --oneline -3` runs.
- **P2.6 (LSP and Learn):**
  - code-reviewer: "Use LSP go-to-definition on `Result` in `src/Envanex.Domain`." The expected location is under `src/Envanex.Domain/Common/`.
  - researcher: "Search Microsoft Learn for EF Core `ComplexProperty`." The expected result is a `learn.microsoft.com` URL.
  - If LSP fails on .NET 10, remove `LSP` from every agent's `tools`, record it, and file it as a Known gap in Phase 4.

### Validation

The Phase 1 block, plus:
- `node --test .claude/hooks/tests/*.test.js` covering all four test files
- C2.2 and C2.5
- `grep -c $'\r'` over the new files

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
- `.claude/agents/tester.md` — modified: runs `gate.sh` and takes its file list from `gates.txt`.
- `.claude/settings.json` — modified: `allow` gains `Bash(bash .claude/skills/gate/scripts/gate.sh)`, `Bash(bash .claude/skills/mutate/scripts/mutate-run.sh *)` and `Bash(bash .claude/skills/mutate/scripts/mutate-clean.sh *)`.

### Signatures

- **`gate/SKILL.md` frontmatter:**
  - `name: gate`
  - `description:` …invoked manually via /gate…
  - `disable-model-invocation: true`
  - `effort: low`
  - `allowed-tools: Bash(bash .claude/skills/gate/scripts/gate.sh), Bash(git status *), Bash(git diff *), Bash(grep *), Bash(cat *), Bash(ls *)`

  The body runs the script, pastes its stdout verbatim, then runs any `$ARGUMENTS` extra checks under their own heading. It keeps the current "never summarise" rule and the Gotchas.
- **`gate.sh` (no arguments, `set -u`, no `set -e`):**
  - `cd "$(git rev-parse --show-toplevel)"`
  - branch slug = `git branch --show-current` with `/` replaced by `-`
  - writes `TestResults/<slug>/gates.txt`: a header line `gate.sh <ISO ts> branch=<b> head=<sha>`, then for each command, `$ <cmd>`, its full stdout and stderr, and `exit=<n>`. The commands, in order:
    - `git status --short`
    - `git diff --name-only main...HEAD` (the tester's file list)
    - `git log --oneline main..HEAD`
    - `docker compose ps`
    - `node --test .claude/hooks/tests/*.test.js`
    - `dotnet format --verify-no-changes`
    - `dotnet build -warnaserror`
    - `dotnet test`
  - stdout, all computed from the file:
    - the file path and `sha256sum` of `gates.txt`
    - the changed-file list
    - one `exit=` line per command
    - the last 5 lines of the build section
    - every `Passed!`/`Failed!` line
    - `dotnet test passed total: <sum>`
    - node's `# pass`/`# fail` lines
  - exit status: 0 only if node, format, build and test all exited 0. `docker compose ps` is informational.
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
- **`mutate-run.sh <label> <file> dotnet "<filter>" [<project>]` or `mutate-run.sh <label> <file> node <test-file>`:**
  - appends to `TestResults/<slug>/mutations/<label>.txt`: `$ git diff -- <file>` (the mutation), then the raw test output (`dotnet test [project] --filter "<filter>"` or `node --test <test-file>`), then `exit=`
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
  - Precondition 3 accepts `TestResults/<slug>/tester-verdict.md` with `Verdict: READY_TO_PUSH` as well as an in-session verdict.
  - New preconditions:
    - a `thoughts/shared/reviews/*-code-review-branch*.md` with `Verdict: APPROVED`
    - a `/security-review` record (see Phase 4)
  - The Verdicts section cites each review file's path and its `Verdict:` line. Validation pastes `gate.sh`'s summary.
  - The body goes through `gh pr create --body-file TestResults/PR_BODY.md`, written with Write (the same quoting fix as F16).
  - `allowed-tools` converted to the space form, plus `Write`.
- **`tester.md`:**
  - Steps become:
    - run `bash .claude/skills/gate/scripts/gate.sh` and paste its stdout verbatim
    - take the file list from the `git diff --name-only main...HEAD` section of `gates.txt`, ignoring any list typed in the prompt (F13)
    - grep for the plan's named tests and confirm they assert something
    - write `tester-verdict.md`

### Tests to add

None automated. Each script run takes minutes (a full `dotnet test`), so the scripts are proven by probes P3.1-P3.6. `node --test` stays green through `gate.sh`'s own gate.

### Probes (fresh session)

- **P3.1 (`/gate` matches its file):**
  - `/gate` prints a sha256.
  - `sha256sum TestResults/chore-agents-hardening/gates.txt` gives the same hash.
  - `grep '^exit=' TestResults/chore-agents-hardening/gates.txt` shows the same codes the skill printed.
  - The total printed is 638.
- **P3.2 (`/gate` catches a failure):**
  - Edit `.claude/hooks/tests/path-guard.test.js` so one assertion expects 0 instead of 2.
  - Run `bash .claude/skills/gate/scripts/gate.sh; echo "gate=$?"`. The node section shows `# fail 1` and the output ends with `gate=1`.
  - Apply the inverse Edit; `git diff --exit-code -- .claude/hooks/tests/path-guard.test.js` exits 0.
- **P3.3 (rename):**
  - `/gate` resolves to the project skill: its output contains the `gates.txt` path line.
  - `ls .claude/skills` shows no `verify`.
  - C3.1.
- **P3.4 (`/mutate`):**
  - `/mutate` runs M1.1 again through the skill (`node` mode on `path-guard.js`).
  - The log `TestResults/chore-agents-hardening/mutations/M1.1.txt` holds the diff, the red and `exit=0` for the clean check.
- **P3.5 (`/commit`):**
  - Phase 3's own commit is made through `/commit`, with a body containing a quoted word (`"quoted"`).
  - Then `git log -1 --format=%B | diff - TestResults/COMMIT_MSG.txt` prints nothing.
  - Trailer lines added outside the file are noted, not treated as a failure.
- **P3.6 (tester list):** the tester's reported file list is byte-for-byte the `git diff --name-only main...HEAD` section of `gates.txt`.
- **C3.2:** skill effort observable or not.

### Validation

The Phase 1 block, plus:
- `grep -c $'\r' .claude/skills/*/scripts/*.sh` returns 0 (a CRLF `.sh` breaks bash)
- `git ls-files --eol .claude/skills` shows `i/lf`

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
    - add `export ENVANEX_CONNECTION_STRING='Server=127.0.0.1,1433;Database=EnvanexDev;User Id=sa;Password=<password>;TrustServerCertificate=True'` before the `dotnet ef` lines, with a note that the design-time factories read this variable, not user-secrets (`src/Envanex.Infrastructure/Persistence/EnvanexDbContextFactory.cs:12`, `.../Identity/EnvanexIdentityDbContextFactory.cs:12`)
    - add `node --test .claude/hooks/tests/*.test.js`
    - drop the Worker `dotnet run` line and "SOAP" from the Web comment
  - **Architecture:** mark `Envanex.SoapApi` and `Envanex.Worker` as "empty shell; scope cut, see docs/roadmap.md". Drop "outbox" from Infrastructure.
  - **Key Patterns:** Outbox bullet becomes "deferred: no dispatcher exists; see docs/roadmap.md".
  - **Orchestration:**
    - the pipeline, and a **light lane**: small non-feature PRs (docs, fix, chore) with no schema change and no new public behavior. A short plan file, then coder, code-reviewer, tester, `/gate` and `/pr`, with no researcher, planner or plan-reviewer.
    - the **PR-end sequence**: whole-branch code-reviewer → `/security-review` (its output saved verbatim to `thoughts/shared/reviews/YYYY-MM-DD_<slug>-security-review.md` before anything else) → tester → `/gate` → `/pr` (F12)
    - the main agent never types the tester's file list (F13)
    - reviewers' verdict files (F11)
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
  - one new Known gaps row: residual guard risks
    - the Bash guards are string heuristics, and obfuscation (a variable holding `git`) bypasses them
    - the path guard doesn't see Bash writes
    - agents can edit `.claude/` itself, taking effect after a restart
    - user-level settings sit outside the repository

    Closes in: "not scheduled — recorded". If P2.6 failed, a second row covers the LSP failure.
  - "How the work is run": the diagram gains the light lane and the PR-end sequence; `/verify` becomes `/gate` at line 220; the "Agent models" paragraph at 236-237 becomes the F3 tier table, or a link to `docs/ai-workflow.md` (F17).
  - The `chore(agents)` status stays blank; the next PR fills it in.
- **`docs/ai-workflow.md` — created,** English, reader-facing. Sections:
  - **The pipeline and the light lane** — why the human drives every transition.
  - **Roles and tiers** — the Phase 2 table and F3's principle: judges at max, executors at xhigh, mechanical steps at low. The coder stays at xhigh because a usage-limit cutoff leaves a half-written phase; raising it to max for one phase is done by editing `coder.md` in its own commit, followed by a restart.
  - **The guards:** deny list, path guard, coder git block, tester and explainer allowlists, write scopes. For each, what it blocks and the incident behind it:
    - the PR 6b stash
    - the `.env.example` block in fix(db)
    - the Haiku tester's narrated gates
    - `docker compose down -v`
  - **Why hooks fail closed** — the exit-1 trap.
  - **Verdict files and `/gate` evidence.**
  - **How to re-run the probes** — `node --test`, plus the P-list of this plan.
  - **Residual risks** — the same list as the roadmap row.
  - **What is deliberately not used** — Fable, `superpowers`, memory plugins, `maxTurns`.

### Tests to add

None (docs only).

### Probes and checks

- **C4.1:** `wc -l CLAUDE.md` ≤ 200.
- **C4.2:** `git remote -v` shows `origin`.
- **P4.1:** `grep -rn "/verify" CLAUDE.md docs/roadmap.md docs/ai-workflow.md .claude` returns nothing, apart from any intentionally historical sentence in `docs/ai-workflow.md`.
- **P4.2:** `grep -n "purchasing\|SoapCore\|Windows Service" CLAUDE.md` appears only in the out-of-scope sentence.
- **P4.3:** every relative link in `docs/ai-workflow.md` resolves (`ls` each target).
- **P4.4 (end-to-end):** after the restart, this PR's own PR-end sequence runs as the Phase 4 probe:
  - whole-branch code-reviewer (writes its verdict file)
  - `/security-review` (saved to a file)
  - tester (uses `gate.sh`)
  - `/gate`
  - `/pr` (cites the files)

  The PR body is the evidence. The explainer journal for this PR is the human's call and belongs to no phase.

### Validation

The Phase 1 block, plus C4.1 and P4.1-P4.3.

---

## Rollback notes

- Each phase is one or two commits on the branch. The human reverts with `git revert <sha>`, which is not denied. `git reset --hard` is denied by design.
- **If a hook blocks everything** (fail-closed misfire, for example `$CLAUDE_PROJECT_DIR` not expanding): edit `.claude/settings.json` or the agent file in an external editor, or set `"disableAllHooks": true` in `.claude/settings.local.json`, then restart. If that switch is ever used, record it. The switch is documented but not probed here.
- **If a full model ID is rejected:** switch that agent's frontmatter to `opus`/`sonnet` and record it (P2.1). `opus` resolves to the main session's Opus, since `~/.claude/settings.json` sets `"model": "opus"`.
- **If a subagent won't write its report file:** revert that agent to "present content, the main agent saves it" (P2.4 fallback).
- **If `csharp-lsp` fails:** remove `LSP` from `tools`, and uninstall the plugin at project scope, which removes its `enabledPlugins` key.
- H1.1's edits to the local and user settings files are outside the PR. Undoing them is a manual edit.
