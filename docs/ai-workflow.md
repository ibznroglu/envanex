# AI workflow

How Envanex is built with Claude Code agents: the pipeline, the roles and their tiers, the guards
that enforce the pipeline's rules, the evidence the gate leaves behind, and the risks that remain.
The residual record is one Known-gaps row in [`docs/roadmap.md`](roadmap.md); the section
[Residual risks](#residual-risks) below repeats it item by item.

The design comes from the research
[`2026-09-24_agent-workflow-hardening.md`](../thoughts/shared/research/2026-09-24_agent-workflow-hardening.md)
(findings F1-F21) and two plans: the hardening plan
[`2026-09-25_chore-agents-hardening.md`](../thoughts/shared/plans/2026-09-25_chore-agents-hardening.md)
(Phases 1-2, merged as #15) and the workflow-skills plan
[`2026-10-13_chore-agents-workflow-skills.md`](../thoughts/shared/plans/2026-10-13_chore-agents-workflow-skills.md)
(Phases 3-4). Finding numbers such as F3 or F13 refer to the research file.

## The pipeline and the light lane

Every agent stops at the end of its step, and the human starts the next one. The reason is in the
record. PR 6b's failures were verification failures: command output reported but never produced,
wrong counts, a nonexistent API name, nine green tests that protected nothing. The human and the
mutation proofs caught them; the agents did not (F3). A transition the human makes is a point where
raw output is read before anything is built on it, and a verdict ends the turn instead of starting
the next agent.

There are two lanes:

- **Feature lane:** researcher → planner → plan-reviewer → coder (one phase at a time) →
  code-reviewer → db-reviewer (when the phase touched schema, migrations or queries) → tester.
- **Light lane:** a small docs, fix or chore PR with no schema change and no new public behavior
  starts from a short plan file: coder → code-reviewer → tester, with no researcher, planner or
  plan-reviewer. `docs(roadmap)` (#13) and `fix(db)` (#14) ran this way and still caught real
  defects: the IPv6 `localhost` trap and the missing `dotnet-ef`. A light-lane branch review is the
  code review whose `Range:` starts at `git merge-base main HEAD` (`/pr` precondition 5).

**Per phase, in both lanes,** the human commits each review file, whatever its verdict, before the
tester runs, so the tester's `Head:` is that commit. An uncommitted review file leaves the `status`
section of `gates.txt` non-empty, and the tester returns NEEDS_FIXES.

**Before the PR end,** the explainer writes the learning journal, after the last phase's tester
returns READY_TO_PUSH, and the human writes the ADR. Every evidence, doc, journal and ADR commit
lands before the PR-end sequence starts.

**The PR-end sequence, in both lanes:**

1. whole-branch code review (in the light lane, the merge-base-range review above)
2. `/security-review`
3. the human commits the review files
4. tester
5. `/gate`
6. `/pr`

A commit after step 4 means re-running the tester: `/pr` precondition 4 compares the tester's
`Head:` with `HEAD`. The whole-branch review exists because a per-phase review cannot see a seam
between phases. In PR 6b the final whole-branch review found a test that could no longer fail,
sitting between Phase 3 and Phase 4 (F12).

**Why the security review applies in both lanes.** `fix(db)` was a security fix: it stopped
publishing the local SQL Server SA password, and it changed nothing under `src/`. A lane chosen for
small changes is not a lane without security impact. `/security-review` is skipped only when every
path in the `files` section of `gates.txt` starts with `docs/` or `thoughts/`, which `/pr` computes
itself (precondition 6).

**`/security-review` is a built-in Claude Code command, not a project skill.** It reviews the
branch's diff against the default branch and needs an `origin` remote (F10). With
`"disableBundledSkills": true` in `.claude/settings.json` it still appeared in the command list on
2026-10-13 (a human observation), so its availability is re-checked before each PR-end sequence.
The main session saves its output verbatim with `Verdict: PENDING`, and only the human sets the
verdict (see [Verdict files and `/gate` evidence](#verdict-files-and-gate-evidence)).

**Journal numbers are a sequence of their own,** independent of GitHub PR numbers and roadmap
numbers. The next entry takes the number after the highest `NNNN-` entry in `docs/journal/`. The
explainer stops if that number is taken, and a number is never reused, not even by a reverted
entry.

**`/pr` merges only the validated head.** It records `HEAD` as V before pushing, rechecks that the
remote PR head is V and that every check passes, and merges with
`gh pr merge --squash --delete-branch --match-head-commit V`. It never merges with `--admin` and
never force-pushes, and it updates `main` with `git pull --ff-only`.

## Roles and tiers

| Agent | Model | Effort | Tools (`<agent>.md:4`) | PreToolUse hooks (`<agent>.md:7-16`) |
|---|---|---|---|---|
| researcher | claude-opus-5-5 | xhigh | Read, Glob, Grep, Write, LSP, `microsoft_docs_search`, `microsoft_docs_fetch`, `microsoft_code_sample_search` | file tools → `write-scope.js thoughts/shared/research` |
| planner | claude-opus-5-5 | max | Read, Glob, Grep, Write, LSP, `microsoft_docs_search`, `microsoft_docs_fetch` | file tools → `write-scope.js thoughts/shared/plans` |
| plan-reviewer | claude-opus-5-5 | max | Read, Glob, Grep, Write, LSP, `microsoft_docs_search`, `microsoft_docs_fetch` | file tools → `write-scope.js thoughts/shared/reviews` |
| code-reviewer | claude-opus-5-5 | max | Read, Glob, Grep, Write, LSP, `microsoft_docs_search`, `microsoft_docs_fetch` | file tools → `write-scope.js thoughts/shared/reviews` |
| db-reviewer | claude-opus-5-5 | max | Read, Glob, Grep, Write, LSP, `microsoft_docs_search`, `microsoft_docs_fetch` | file tools → `write-scope.js thoughts/shared/reviews` |
| coder | claude-opus-5-5 | xhigh | Read, Write, Edit, Bash, Glob, Grep | `Bash` → `coder-bash-guard.js` |
| tester | claude-sonnet-5-5 | low | Read, Bash, Glob, Grep, Write | `Bash` → `bash-allowlist.js tester`; file tools → `write-scope.js TestResults` |
| explainer | claude-opus-5-5 | xhigh | Read, Glob, Grep, Bash, Write | `Bash` → `bash-allowlist.js explainer`; file tools → `write-scope.js docs/journal` |

The model and effort come from each agent's frontmatter (`.claude/agents/<agent>.md:5-6`). "File
tools" is the matcher `Write|Edit|MultiEdit|NotebookEdit`. The `microsoft_docs_*` and
`microsoft_code_sample_search` names are the Microsoft Learn MCP tools, written out in full
(`mcp__microsoft-learn__…`) in the files. Every hook command ends in `|| exit 2` (see
[Why hooks fail closed](#why-hooks-fail-closed)). The project-wide permissions and path-guard live
in `.claude/settings.json` (see [The guards](#the-guards)).

**The principle is F3's:** agents that judge run at max, agents that execute at xhigh, and
mechanical steps at low. The planner and the three reviewers judge, so they run at max; a reviewer
weaker than the author it reviews is an anti-pattern.

- **The tester runs on Sonnet 5.5 at low** (Revision 6 decision 1 of the hardening plan). It runs a
  script and pastes its output, so more reasoning adds nothing. A model choice does not make a
  report correct: what the tester checks is the record that `gate.sh` writes, not its own reading of
  a run.
- **The coder stays at xhigh,** the documented level for demanding coding work, not at max. It is
  the only agent that writes code, and a run cut off by a usage limit leaves a half-written phase
  behind; the PR 6b Phase 3 stash was exactly that. A read-only reviewer cut off the same way is
  simply re-run. Raising the coder to max for one concurrency- or transaction-heavy phase takes
  five steps: edit `coder.md` in its own commit, restart, run the phase, commit the revert, and
  restart again.
- **Models are pinned by full ID** in the frontmatter, and agents are spawned without a model
  parameter, because a per-invocation model parameter overrides the frontmatter model.
- No agent sets `memory`, which would enable Read, Write and Edit automatically (F4), and none sets
  `maxTurns` (see [What is deliberately not used](#what-is-deliberately-not-used)).

## Threat model

Two statements decide every guard (the hardening plan's Goal):

- The guards stop accidental destructive actions by a cooperative agent.
- They are not a boundary against code the model writes and runs itself: `dotnet run`,
  `dotnet test`, `dotnet build`, `node` or a script. Such code can call any denied command.

What follows from them:

- Allow entries that reopened a deny were removed, not patched.
- The deny list covers the forms commonly typed. It is not exhaustive, and its gaps are named
  (Residual risks, items 14, 15 and 17).
- Redaction covers the secret forms this repository actually carries.
- The control against code the model runs is the human-in-the-loop pipeline, not the guards: the
  human reads the plan, the diff, the reviews and the raw gate record before anything merges.
  Closing the gap itself would need a sandbox, not more patterns.

## The guards

Each guard below says what it blocks and the incident behind it. The hook code is under
`.claude/hooks/` and its tests under `.claude/hooks/tests/`.

### The deny list

`permissions.deny` in `.claude/settings.json` holds 47 patterns, each twice, as `Bash(…)` and as
`PowerShell(…)`, so switching shells does not escape it. Deny rules apply to subagents too (F1).
They cover:

- discarding work: `git stash`, `git clean`, `git reset --hard`, `git checkout -- …`,
  `git checkout .`, `git checkout -f`, `git restore`, and `git switch` with `-f`, `--force` or
  `--discard-changes`
- rewriting history and the remote: `git rebase`, `git push` with `--force`, `-f` or a `+refspec`,
  `git branch -D`, `git switch -C`
- options that move a command out of a pattern's reach or write a file: `git -C …`, `git -c …`, and
  `--output` on `git log`, `git diff` and `git show`
- deleting data: `docker compose down` with `-v` or `--volumes` (also as `docker-compose`),
  `docker volume rm`, `docker volume prune`, `docker system prune`, and `dotnet ef database drop`
  (also as `dotnet-ef`)

The incidents: nothing blocked `git stash` before, and the PR 6b stash survived on discipline alone.
The old `docker compose*` allow entry pre-approved `docker compose down -v`, which deletes the local
database volume (F1). The allow list was narrowed at the same time. `git add`, `git commit` and
`git push` are no longer pre-approved for the session; `/commit` and `/pr` grant what they need
through their `allowed-tools` while they run.

### The path guard

`path-guard.js` runs before every `Edit`, `Write`, `MultiEdit` and `NotebookEdit` call
(`.claude/settings.json`). It normalizes the target as a pure string: backslashes become `/`, the
Git Bash form `/c/` becomes `c:/`, `.` and `..` collapse, and the result is lower-cased. It blocks:

- a path segment `bin`, `obj`, `packages`, `.vs`, `.idea` or `.git`
- a file named `.env`, `secrets.json`, or `.env.<anything>` other than `.env.example`
- a file name ending in `.env`, `.user`, `.pfx`, `.snk` or `.local.json`, which covers
  `.claude/settings.local.json`

The incidents: the old inline guard matched only `/`-separated paths, so on Windows its directory
rules likely never fired, and it swallowed every exception (F2). Its `.env` rule also blocked the
coder from writing `.env.example`, a template holding no secret, in `fix(db)`, so the human had to
write the file (F15). `.env.example` is now the one allowed `.env.` name.

### The coder's git block

`coder-bash-guard.js` runs before each of the coder's Bash calls. It scans the whole command string
for the word `git`, so a call wrapped in `bash -c`, `node -e`, `$(…)` or a `&&` chain is seen too.
It allows only the read subcommands `status`, `diff`, `log`, `show`, `ls-files`, `ls-tree`,
`rev-parse`, `merge-base`, `blame`, `grep`, `cat-file`, `shortlog` and `describe`, and `git branch`
bare or with `--show-current`, `--list`, `-a`, `-r`, `-v` or `-vv`. Of git's global options it skips
only `-C <arg>`, `-c <arg>`, `--no-pager` and `-P`. It blocks every `gh` call.

The incident: "the coder never commits or pushes" held only because its prompt said so, while the
session allow list pre-approved `git add`, `git commit` and `git push` for every agent (F1). The
coder leaves commits to the human, who runs `/commit`.

### The tester's and explainer's allowlists

`bash-allowlist.js <profile>` runs before each Bash call of the tester (profile `tester`) and the
explainer (profile `explainer`). A command passes only when:

- it contains none of `;`, `&&`, `||`, a background `&`, a backtick, `$(`, `<(`, `>(`, a newline,
  or a `>` other than the exact token `2>&1`. Characters inside quotes count too, so the check
  fails closed.
- after a split on every `|`, also without regard to quotes, its first segment starts exactly with
  an entry's tokens, and every later segment starts with `grep`, `head`, `tail` or `wc`
- no token starts with `--output`

An entry is exact (no further tokens), prefix (any further tokens) or fixed-token (only the listed
further tokens).

- **tester:** `bash .claude/skills/gate/scripts/gate.sh` (exact); `git status`, `git diff`,
  `git log`, `git show`, `git ls-files`, `git rev-parse` (prefix); `git branch --show-current`
  (exact); `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` (fixed-token);
  `node --test` with test files under `.claude/hooks/tests/` only; `docker compose ps`, `grep`,
  `cat`, `ls`, `wc`, `head`, `tail` (prefix)
- **explainer:** `git status`, `git diff`, `git log`, `git show`, `dotnet list package`, and the
  same six read tools, all prefix

**Why the dotnet entries are fixed-token.** A prefix entry `dotnet build` would admit
`dotnet build -p:PreBuildEvent=<cmd>`, and the build runs that command: a scratch run recorded it
(C2.6: exec yes; one run, dotnet 10.0.400, Windows). `--results-directory` and `--report` would
write files outside the tester's write scope. So after `dotnet build`, `dotnet test` or
`dotnet format --verify-no-changes` only these tokens pass: `-warnaserror`, `--no-restore`,
`--filter` with one argument that does not start with `-` or `/`, `-v` or `--verbosity` with a named
level, a project path under `src/` or `tests/` without a `..` segment, and `2>&1`.

The incident behind the tester's design: the Haiku tester narrated its gates. In PR 6b's final
gate it summarized the test run instead of pasting it, in the `docs(roadmap)` gate it gave a changed
file's path wrongly, and in `fix(db)` it followed a stale file list typed into its prompt (F13). Its
Bash is now read-only apart from the gate script and the build output that `dotnet build` and
`dotnet test` always write, and its file list comes from the gate's record.

### Write scopes

`write-scope.js <dir>` runs before every file-tool call of the agent whose frontmatter names it. It
allows a target only when the target, normalized like path-guard's, lies inside the project and
under `<dir>/`. The trailing `/` rejects a sibling such as `reviews-evil`, and `..` cannot climb
out. The researcher writes only to `thoughts/shared/research/`, the planner to
`thoughts/shared/plans/`, the three reviewers to `thoughts/shared/reviews/`, the tester to
`TestResults/` and the explainer to `docs/journal/`. The coder has no write scope: it writes the
files its plan names, within path-guard's limits.

The findings behind them: every long report used to pass through the main session's context (F4),
review verdicts lived only in session transcripts (F11), and the explainer's "only `docs/journal/`"
was a prompt rule (F1).

### What prompts on purpose

No allow entry pre-approves these, so in the default permission mode each one prompts:

- `dotnet ef database update`: `update 0` drops every table
- `dotnet new`: `--force` and `-o` write past the path guard
- `git switch` to any branch other than `main` or a new `-c` branch
- `git commit`, which is pre-approved only inside `/commit`, as the one command
  `git commit -F TestResults/COMMIT_MSG.txt`

Not pre-approved is not denied. In auto mode a classifier, not the human, approves commands that no
rule covers (F1), and user-level settings can allow them (Residual risks, item 9).

### The gate script and the mutation scripts

**The tester's one Bash write exception** is the gate script, run as exactly
`bash .claude/skills/gate/scripts/gate.sh`, with no arguments, pipe or redirection; it is an exact
entry of the tester's allowlist. It writes `TestResults/<branch-slug>/gates.txt`, and its `dotnet`
steps write build output under `bin/` and `obj/` as `dotnet build` and `dotnet test` always do. No
hook inspects what the script runs or writes; its bound is its reviewed content (Residual risks,
item 2). The session allow entry `Bash(bash .claude/skills/gate/scripts/gate.sh)` lets `/gate` run
it without a prompt.

**The mutation scripts,** `mutate-run.sh` and `mutate-clean.sh` under
`.claude/skills/mutate/scripts/`, append raw output to
`TestResults/<branch-slug>/mutations/<label>.txt` and change nothing else: never the target file,
the index or `HEAD`. The label must match `^[A-Za-z0-9][A-Za-z0-9._-]*$`, so it cannot climb out of
`mutations/`, and every target must be a tracked file. `mutate-clean.sh` compares the targets with
`HEAD` both in the working tree (`git diff --exit-code HEAD --`) and in the index
(`git diff --cached --exit-code --`). A plain `git diff --exit-code` compares the working tree with
the index and would miss a staged change. A mutation is restored by the inverse Edit, never by
`git checkout --`, which is denied. Only the human invokes `/mutate`; a coder turn calls the two
scripts directly.

## Why hooks fail closed

Claude Code treats exit code 2 from a PreToolUse hook as a block. Any other non-zero exit is
reported as a non-blocking error, and the tool call goes ahead. That is the exit-1 trap: a guard
that crashes, because `node` is missing, the script has a syntax error or the input is unexpected,
would let every call through while looking installed. The old inline path guard fell into it by
design, catching every exception (F2).

Two layers close it:

- Every guard enters through `runGuard` in `.claude/hooks/lib/guard-common.js`. It turns any throw
  or rejection (invalid JSON on stdin, no project dir, an empty target) into a block with the
  reason `guard error: <message>` and exit 2.
- Every hook command ends in `|| exit 2`, so a `node` that cannot start, or a script that dies
  before `runGuard`, still blocks. P1.10 checked this wiring with a missing script.

The price is that every hook error becomes a false positive that stops work. If a misfire ever
blocks everything, the switch is `"disableAllHooks": true` in `.claude/settings.local.json`, edited
outside Claude Code because path-guard protects that file, followed by a restart; its use is
recorded.

**The same rule for the scripts.** A gate that cannot write or verify its record must never look
like a pass. `gate.sh` exits 0 only for a complete, verified record whose last line is
`gate-exit=0 failed=`, and exits 1 for a complete record with a failed required step. A start-up
failure (not inside a work tree, a failed `cd`, no current branch, a directory it cannot create)
exits 2 and writes nothing. Any failed write or read of the record, including the final re-read of
its last line, exits 3. Each failure prints exactly one line to stderr, `gate.sh: <reason>`, and
nothing else. The mutation scripts follow the same scheme: argument and start-up failures exit 2,
log I/O failures exit 3, and `mutate-clean.sh` exits 2 when a git call returns a code other than
0 or 1. P3.7 and P3.8 executed most of these exits; item 10 of the Residual risks names those that
only review covers.

## What the hook log holds

Every guard decision appends one JSON line to `TestResults/hook-log/hooks.jsonl`, which is
gitignored, with `ts`, `hook`, `decision`, `reason`, `tool_name`, `target`, `agent_type`,
`agent_id`, `session_id` and `project_dir_raw`. The hook name carries its argument (`path-guard`,
`coder-bash-guard`, `bash-allowlist:<profile>`, `write-scope:<dir>`), so a hook attached to the
wrong agent shows in the log. Logging swallows its own errors, because it must never change a
decision.

**Redaction.** `redactSecrets` runs on `target` and `reason` before `target` is cut to 300
characters, and on the block message written to stderr. Its rules, in order, replace each secret
value with `***`:

0. join line continuations (a bash `\` or a PowerShell backtick before a newline), so a value split
   across lines is seen whole
1. password assignments: `password` or `pwd`, then `=`, then a value
2. the `sqlcmd` `-P` value, only when the string contains `sqlcmd`; `-P` is matched
   case-sensitively, because `-p` is another option
3. `user-secrets … set`: everything after the `set` token, up to `&&`, `;`, `|`, a newline or the
   end
4. `secret`, `token` or `api_key` followed by `=` or `:`
5. assignments to a key that contains a `SENSITIVE` word (environment variables, configuration
   keys, `--key=value`)
6. JSON members whose name contains a `SENSITIVE` word
7. space-separated options whose name contains a `SENSITIVE` word, such as `--password x`
8. `Authorization` headers, with or without a scheme
9. passwords in a URL's userinfo (`scheme://user:password@host`)

`SENSITIVE` is
`password|passwd|pwd|secret|token|api[_-]?key|signing[_-]?key|access[_-]?key|private[_-]?key|credential`,
matched case-insensitively. `VALUE`, the value pattern of rules 1 and 5, is one or more of: a
single-quoted run, a double-quoted run with backslash escapes (either closed or unterminated), or
one unquoted character other than `;`, whitespace or a quote. Its alternatives start with different
characters, so backtracking stays linear. A quote right after a value extends the redaction: it
errs toward redacting more. The limits are items 7 and 20 of the Residual risks.

**`LIVEPROBE` tags.** Every live-probe target carries the string `LIVEPROBE` (for example
`obj\LIVEPROBE-1-1.txt`), and no test file contains it (C1.7), so a line with the tag comes from a
probe, never from a test. The log is shared across branches, so a probe's lines are selected by
`session_id`, `agent_id`, hook, target and the window between the tool call and its result, never by
counting lines over the whole log.

**Tests write to a temp dir.** `.claude/hooks/tests/run-hook.js` spawns each hook with
`ENVANEX_HOOK_LOG_DIR` set to a fresh `envanex-hooklog-` directory that `fs.mkdtempSync` creates
under the OS temp dir, and `resolveLogDir` uses that override when it is set and non-empty. So
`node --test` never writes to the project's hook log, and each test reads only its own lines.

## Verdict files and `/gate` evidence

**Verdict files.** Each reviewer writes its own file under `thoughts/shared/reviews/`, named
`YYYY-MM-DD_<branch-slug>-<kind>[-rN].md`, where `<branch-slug>` is the branch with `/` replaced by
`-`. A file's kind is its name without the date, the slug and a trailing `-rN`, for example
`code-review-phase3`, `code-review-branch` or `security-review`. Within a kind only the highest N
counts, a file without `-rN` has N = 1, and the first re-review is `-r2`. The first five lines are
the header: `Verdict:`, `Reviewer:`, `Scope:`, `Range:` and `Head:`.

**Head rule R** binds a verdict to the code it reviewed. A file satisfies it when:

1. `git rev-parse --verify --quiet "<Head>^{commit}"` exits 0, so an unknown or mistyped SHA fails
   here
2. its `Head:` equals `git rev-parse HEAD`, or `git diff --name-only <Head> HEAD` exits 0 and every
   path it prints is under `thoughts/shared/reviews/`: the only commits after the reviewed one are
   review-file commits

Any non-zero exit of `git rev-parse`, `git diff` or `git merge-base` during a check fails that
check, and a failed command's output is never read as an empty list. `git merge-base --is-ancestor`
is read by its meaning: exit 1 is "not an ancestor", and that fails the check as well.

**The gate record, `TestResults/<branch-slug>/gates.txt`.** `gate.sh` truncates it at the start of
each run and writes, in order:

- a header line, with `missing` for a value it cannot read:
  `gate.sh <UTC time> branch=<b> head=<sha> main=<sha> origin_main=<sha> base_main=<sha> base_origin=<sha>`
- one section per step: the line `$ <command>`, the step's raw stdout and stderr, then
  `exit=<n> step=<name> kind=<required|info>`
- the last line, `gate-exit=<0|1> failed=<comma-separated steps>`, which `final_status` computes by
  reading back the record's `kind=required` lines

The steps, in order, are `tools-check` (required); `status`, which is `git status --short` (info);
`base-check`, where `main` and `origin/main` must have the same merge-base with `HEAD` (required);
`files`, which is `git diff --name-only main...HEAD` (info); `log` and `docker` (info); and `node`
(the hook tests), `format`, `build` and `test` (required). The status section is empty, that is,
the line after `$ git status --short` is exactly `exit=0 step=status kind=info`, only on a clean
working tree. The script never runs `git fetch`. Its stdout is a summary computed from the record
after a final re-read: the record's path, its `sha256sum` line, the `files` section, every `exit=`
line, the build's last five lines, the test totals, the hook-test pass and fail counts, and the
record's last line.

**How the tester uses it.** The tester runs `gate.sh` once and inspects the record instead of
re-running its commands: `node --test`, `dotnet format`, `dotnet build` and `dotnet test` ran inside
the gate, and `gates.txt` is their record. It uses the record only when its own run's stdout is
complete (line 1 the record's path, line 2 a `sha256sum` line, the last line starting `gate-exit=`)
and the record's `head=` equals `HEAD`. With incomplete stdout it does not read the file at all,
because a file there may be an older run's. Either failure returns NEEDS_FIXES with
`Gates-sha256: none`. Its changed-file
list is the record's `files` section; the main agent never types it (F13). Its report,
`TestResults/<branch-slug>/tester-verdict.md`, starts with exactly four lines, `Verdict:`, `Head:`,
`Branch:` and `Gates-sha256:`. If that file already exists, the tester stops instead of
overwriting it.

**A record belongs to the run that printed its hash.** Every `gate.sh` run overwrites `gates.txt`,
but a run that stops at start-up (exit 2) writes nothing and leaves an older record in place. So the
live record is removed before every run whose record is used, and a record counts only for the run
whose complete stdout printed its path, its hash and a final `gate-exit=` line. In the
workflow-skills plan's probe protocol, a copy kept as evidence is named
`gates-<label>-<HEAD7>-<n>.txt`, with `n` the smallest free number, and no copy is ever overwritten.

**`Gates-sha256:` is recorded, not compared.** The PR-end sequence runs `/gate` after the tester on
purpose, at the same head, and that run rewrites `gates.txt`. The tester's hash therefore names the
record of its own run, which the PR body cites. `/pr` checks the live record's `head=`, its empty
status section and its last line (precondition 3), and the tester file's `Verdict:` and `Head:`
(precondition 4), not the hash.

**The security-review file starts as `Verdict: PENDING`.** The main session saves the verbatim
`/security-review` output to `thoughts/shared/reviews/YYYY-MM-DD_<branch-slug>-security-review.md`
under the header `Verdict: PENDING`, `Reviewer: /security-review`, `Scope: whole branch`,
`Range: <git merge-base main HEAD>..<head>` and `Head: <sha>`, and never writes another value. Only
the human changes the line, after reading the output, to `SECURITY_CLEAR` or `SECURITY_FINDINGS`.
`PENDING` and `SECURITY_FINDINGS` both stop `/pr`.

**The newest-db-review check** (precondition 7) applies when the plan's `Touches schema?` answer is
yes, or a changed path is under `db/`, `src/Envanex.Infrastructure/Migrations/` or
`src/Envanex.Infrastructure/Persistence/`. The highest-N file of every `db-review-*` kind must say
`DB_APPROVED`, and each such `Head:` must be a real commit and an ancestor of `HEAD`. The newest of
those heads is the one every other is an ancestor of; if no single one is, the check fails. No
schema path may change between it and `HEAD`, so a schema change made after the last DB review
cannot merge.

**Main-session writes.** `/commit` writes `TestResults/COMMIT_MSG.txt`, `/pr` writes
`TestResults/PR_BODY.md`, and the main session saves the security-review file. Write-scope hooks run
only from agent frontmatter, so none applies to these writes; path-guard applies, and allows both
directories.

**History.**
`/gate` replaces the project's earlier `/verify` skill.
The old name was shared with a skill bundled with Claude Code (F5), and the new skill is built on a
script, so that its output is a record rather than a model's reading of one (F6).

## How to re-run the probes

- **Hook tests:** `node --test .claude/hooks/tests/*.test.js`, or `/gate`, which runs them together
  with format, build and the .NET suite and records the output.
- **Phases 1-2:** the probes P1.x and P2.x, the mutation proofs M1.x and M2.x and the named checks
  C1.x and C2.x are listed in the hardening plan as of commit `2e8497a`, with their records in its
  As built sections.
- **Phases 3-4:** the probes P3.x and P4.x and the checks G0-G11, X1-X12, D1-D10 and E1 are listed
  in the workflow-skills plan.
- **Mutation proofs:** `/mutate` (the human) or the two mutation scripts (a coder turn), against
  committed code only.
- **Restart first.** After a commit that changes `.claude/settings.json`, `.claude/agents/`,
  `.claude/skills/`, `.claude/hooks/` (tests excluded), `.mcp.json` or `CLAUDE.md`, restart Claude
  Code before relying on the change. When Claude Code rereads these files mid-session is UNVERIFIED
  (F21).

**Regression checks and historical proofs are different things.** A regression check (the gate's
steps, or the G, X, D and E checks) shows that the current code and configuration still pass at a
given `HEAD`. It re-executes the existing suites; it does not re-establish an earlier proof. The
historical proofs (the mutation proofs M1.x and M2.x, the live probes P1.x and P2.x with their
connection records, the named checks C1.x and C2.x, the Phase 2 coverage ledger and #15's
validation logs) stay as recorded and are neither repeated nor rewritten. A probe run again is a new
record with its own date; it never replaces the old one.

## Residual risks

The residual record is the row "Residual risks of the agent guards" in the Known gaps table of
[`docs/roadmap.md`](roadmap.md). This section repeats its items (1)-(22) in the same order and under
the same names, each with its detail, how it would be settled and what reopens it. All of them are
"not scheduled — recorded". UNVERIFIED means that no record settles the claim, and a settling
command listed here has not been run unless the item says so.

**(1) Threat model.** The guards stop accidental destructive actions by a cooperative agent. They
are no boundary against code the model writes and runs itself: `dotnet run` (including .NET 10's
file-based `dotnet run x.cs`), `dotnet test`, `dotnet build`, `node` or a script. Such code can call
any denied command; `dotnet build -p:PreBuildEvent=<cmd>` alone runs an arbitrary command (C2.6).
**Settling:** none. Closing it needs a sandbox, not more patterns; the control is the
human-in-the-loop pipeline. **Reopens:** if anything comes to rely on the guards as a boundary, such
as an unattended agent run.

**(2) The Bash guards are string heuristics.** coder-bash-guard and bash-allowlist read the command
string, not what it runs. Obfuscation bypasses them: a variable holding `git`, or script
indirection (`bash x.sh` where the script calls git). `gate.sh` and the `/mutate` scripts are such
indirection by design: they run commands no hook inspects, so what they write is bounded by their
reviewed content, not by a hook. No hash of the executed script is kept;
`git diff --exit-code HEAD -- .claude/skills/gate/scripts/gate.sh` at a run's `HEAD` shows only that
the file was unmodified. F2 and F3 (item 21) belong to this class. **Settling:** none; it is
inherent in string matching. **Reopens:** when a script under `.claude/skills/` gains a command or a
write, because its review is then the only bound.

**(3) coder-bash-guard blocks any command that names `git` as a word.** It checks every `git` word
as the start of a git call, so `ls .git/hooks`, `ls tools/git/x` and `grep -rn "git" docs` are
blocked: a false-positive class that fails closed. Words that only contain `git` (`digit`,
`.gitignore`, `.github`) do not match. The coder uses the Grep and Glob tools instead.
**Settling:** not needed; three hook tests pin the cases as documented false positives
(`coder-bash-guard.test.js:216`, `:220`, `:224`). **Reopens:** if the guard is changed to parse
commands rather than scan for the word.

**(4) bash-allowlist splits on `|` without regard to quotes.** So `grep -E "a|b"` is blocked: the
split makes `b"` a later pipe segment, which must start with `grep`, `head`, `tail` or `wc`. The
pre-check likewise counts `;`, `&&`, `>` and the rest inside quotes. Both fail closed, and the
validation commands the tester runs avoid a `|` inside a pattern for this reason. **Settling:** not
needed; the test `bash-allowlist: blocks grep -E "Passed!|Failed!" (documented quote-unaware split)`
pins it. **Reopens:** if the split is made quote-aware, which would need tests for every quoting
form.

**(5) path-guard does not see Bash writes.** It runs only for `Edit`, `Write`, `MultiEdit` and
`NotebookEdit`, so a Bash redirect, `cp`, `mv` or `rm` reaches any path. The tester's and the
explainer's allowlists block redirection and admit only read commands, the gate script and the
build; the coder's Bash is bounded by coder-bash-guard for git, by the deny list and by its prompt.
**Settling:** none; it is the hook's scope. **Reopens:** if an agent's Bash write to a protected
path is observed.

**(6) Windows path aliasing, UNVERIFIED.** `normalizePath` is pure string logic and does not model
Win32 path normalization. Win32 strips a trailing `.` or space from a segment, so `obj.\x` could
reach `obj\x`, and it accepts NTFS stream suffixes, so `.env::$DATA` could reach `.env`. Either could
alias a protected path past path-guard. Whether Claude Code's Write tool reaches Win32
normalization is UNVERIFIED. **Settling:** Write `TestResults\x.`, then `ls TestResults`; not run.
**Reopens:** when that check is run, or a write lands on a protected path.

**(7) A quoted `user-secrets set` value is redacted only up to a `;`, `|` or `&&`.** Rule 3 ends
the redaction at the first `&&`, `;`, `|` or newline after the `set` token, without regard to
quotes. The part of a quoted value after such a character is logged unless rule 1, 2 or 4-9 matches
it. This follows from rule 3's pattern (`guard-common.js:168-174`), not from a probe.
**Settling:** not needed; the behavior is the code's. **Reopens:** if a secret containing such a
character is set through the CLI, or rule 3 changes.

**(8) Agents can edit `.claude/`.** That includes `.claude/settings.json`, which path-guard does not
cover (only `settings.local.json` is protected, through its `.local.json` suffix), and the agent,
skill and hook files. A change takes effect after a restart. `disableBundledSkills` and
`disableClaudeAiConnectors` are therefore no authorization boundary: they do not stop any agent or
tool from writing that file. The control is review: every `.claude/` change is a reviewed commit,
followed by a restart before anything relies on it. **Settling:** none. **Reopens:** if a file under
`.claude/` changes outside a reviewed commit.

**(9) User-level settings sit outside the repository.** `~/.claude/settings.json` is neither
versioned nor reviewed here, and its rules apply to every project. `Bash(find *)` there allows
`find -delete`. The human's decisions on its entries are recorded in the hardening plan (H1.1).
**Settling:** reading that file; no check inside this repository can. **Reopens:** on any change to
the user-level settings.

**(10) Reviewed, not executed.**

- **What runs.** P3.7 and P3.8 execute, as required cases, every argument and start-up failure of
  `gate.sh`, `mutate-run.sh` and `mutate-clean.sh` except the `cd` failure; each script's failed
  open, through a directory placed at the record or log path; `mutate-clean.sh`'s exit on a git
  error; and its five clean-check outcomes. Their `chmod` cases (d2 for `gate.sh`, m2 for
  `mutate-clean.sh`) are supplementary: a second trigger of the same failed-open branch, adding no
  branch.
- **What only review covers:** the base-check failure path of `gate.sh`; the `cd` failure of
  `gate.sh`, `mutate-run.sh` and `mutate-clean.sh`; and the append, read and final-record failures
  that follow a successful open of the gate record or a mutation log. The Phase 3 code review
  traces each of them to its check and exit
  (`thoughts/shared/reviews/2026-10-19_chore-agents-workflow-skills-code-review-phase3.md`, "Residual
  cases"). This extends the original "the base-check failure path of `gate.sh` is not probed", and
  that path stays unprobed.
- **Inconclusive cases accepted:** none. Every required P3.7 and P3.8 case ran, and none was
  skipped (the human's assessment of 2026-10-19).

**Settling:** fault injection, such as a full disk or a descriptor closed mid-run, which no plan
schedules; whether bash reports a failed close of the record's descriptor is UNVERIFIED (the same
review). **Reopens:** when `gate.sh` or a mutation script changes, or a gate run ends without its
final line.

**(11) No allowlist rule for pipes.** bash-allowlist lets a later pipe segment start with `grep`,
`head`, `tail` or `wc`, so a tester pipeline's exit status is its last segment's, not the first
command's: `dotnet build -warnaserror 2>&1 | tail -20` exits 0 whatever the build did. No rule is
added, because no verdict rests on a tester's direct `dotnet` run: the tester inspects `gates.txt`,
where `gate.sh` recorded each step's own exit code. **Settling:** not needed while that holds.
**Reopens:** when a verdict again rests on a direct `dotnet` run by the tester.

**(12) Runtime tool access outside P2.6's two pairs is not verified.** By its accepted scope, P2.6
verified runtime access only for code-reviewer × `LSP` and researcher ×
`mcp__microsoft-learn__microsoft_docs_search`. For every other agent and tool pair, only the
configured `tools:` line is checked. **Settling:** a call of each listed tool from each agent, which
no plan schedules. **Reopens:** when an agent run finds a listed tool unavailable. That failure is
first classified, as in P2.6, as a tool visibility, permission or server error before any fix.

**(13) 16 negative live paths of Phase 2 stay UNVERIFIED end to end.** Of Phase 2's 17 original
negative targets, only coder 2e has an exact live block. The other 16 were requested and refused
without a tool call (six) or never requested (ten):

- coder 2a-2d
- the eight write-scope targets: `src/LIVEPROBE-4-<agent>.txt` for code-reviewer, plan-reviewer,
  db-reviewer, researcher, planner, tester and explainer, and the sibling-directory target
  `thoughts/shared/reviews-probe/LIVEPROBE-4-code-reviewer.md`
- tester allowlist targets 5a, 5b, 5f and 5d

Hook decision tests and per-profile live allow records give separate, narrower evidence, not the
original negative end-to-end proof. The human accepted this on 2026-10-09 (Revision 8 of the
hardening plan), and the exact 16 again at R9.11. **Settling:** an expanded method needs an explicit
plan decision, never a prompt asking an agent to ignore its role. **Reopens:** on a relevant hook,
agent or permission change, an observed unexpected allow, or a need to claim full negative
end-to-end verification.

**(14) Deny forms not covered.** These come from finding 10 of the Phase 1 code review. They are
UNVERIFIED, because they concern the rule matcher:

- global options before the subcommand other than `-C` and `-c`: `git --no-pager …`,
  `git --git-dir=… …`, `git --work-tree=… …`
- another binary name: `git.exe …`
- a flag after another flag: `docker compose down --remove-orphans -v`
- bare `git rebase`
- `git checkout <path>` (no `--`), `git checkout --force <b>`, `git checkout <b> -f`
- `git branch <b> -D`, `git branch --delete --force`, `git branch -df`, `git branch -f`,
  `git branch -M`

For the git forms, coder-bash-guard blocks the coder regardless, since it allows only read
subcommands, and the tester's and explainer's allowlists admit none of them. The main session, and
the coder for the `docker compose` form, rely on the deny list alone. **Settling:** probe each form
in auto mode, as P1.11 did; not run. **Reopens:** on a Claude Code update that changes rule
matching, or when a new destructive form comes into use.

**(15) git long-option abbreviations, UNVERIFIED.** git accepts an unambiguous prefix of a long
option, so `git reset --har` or `git switch --forc` can slip past a prefix deny such as
`git reset --hard*`. **Settling,** in a scratch repository:
`echo a > f && git add f && git commit -m f && echo b > f && git reset --har; git diff --quiet; echo "abbrev-honored=$?"`
(0 means it reset); not run. **Reopens:** when that check is run.

**(16) The `*.test.js` allow entry's wildcard crosses `..`.** The session allow entry
`Bash(node --test .claude/hooks/tests/*.test.js)` uses a rule wildcard, not a path glob. P1.13g2
settled it: `node --test .claude/hooks/tests/../../../LIVEPROBE-1-13g2.test.js` ran without a prompt
although its path resolves to the repository root
(`2026-09-25_chore-agents-hardening.md:2772-2774`). The entry therefore pre-approves running a test
file outside `.claude/hooks/tests/`. The tester's allowlist does not share the gap: it rejects any
`node --test` argument that contains `..`.
**Settling:** done by P1.13g2. **Reopens:** if the entry or the rule matcher changes.

**(17) Matcher case.** `git switch -C*` did not deny `git switch -c`: P1.13a ran without a prompt
and printed git's usage text (exit 129), so the matcher did not fold case for this pair
(`2026-09-25_chore-agents-hardening.md:2772-2774`). Rollback R4-1, which would have removed the
`git switch -C` denies, was not applied, and `git switch -C` stays denied. A deny entry covers the
letter case it is written in. **Settling:** done by P1.13a. **Reopens:** if a Claude Code update
changes how rules match, or a deny entry is added whose meaning depends on case.

**(18) Path-guard alias classes, UNVERIFIED.** These come from finding 12 of the Phase 1 code
review. Each class could reach a protected path under a name that path-guard does not recognize:

- 8.3 short names (`GIT~1`, `ENV~1.LOC`, `SETTIN~1.JSO`): `fsutil 8dot3name query C:` and
  `cmd //c "dir /x C:\projects\envanex"`
- junctions, symlinks and hard links:
  `cmd //c "mklink /J C:\projects\envanex\TestResults\lp-junction C:\projects\envanex\obj"`, then
  ask for a Write to `TestResults\lp-junction\LIVEPROBE-x.txt` and run `ls obj/LIVEPROBE-x.txt`;
  remove the junction with `cmd //c "rmdir C:\projects\envanex\TestResults\lp-junction"`
- drive-relative paths such as `C:obj\x`: ask for a Write to `C:obj\LIVEPROBE-x.txt` and read that
  call's `target` in the hook log; an absolute target means Claude Code resolves it before the hook
- case folding against NTFS's upcase table (`packageſ`, `.gıt`): in a scratch directory,
  `mkdir packages .git && ls -d packageſ .gıt`

**Settling:** the commands above; none has been run. **Reopens:** when any of them is run, or a
write lands on a protected path.

**(19) Hook timeouts and regex cost, UNVERIFIED.** These come from finding 13 of the Phase 1 code
review and finding 2 of its re-review:

- **Whether a hook timeout fails open.** In a scratch project with its own
  `.claude/settings.json`, whose PreToolUse hook is
  `node -e "setTimeout(()=>process.exit(2),10000)"` with `"timeout": 2`, ask for a Write of
  `x.txt`. If `x.txt` exists afterwards, a timeout fails open.
- **Rule 3's lazy backtracking on long whitespace runs:**
  `node -e "const {redactSecrets}=require('./.claude/hooks/lib/guard-common');for(const n of [1e3,1e4,1e5]){const s='dotnet user-secrets'+' '.repeat(n)+'x';const t=Date.now();redactSecrets(s);console.log(n,Date.now()-t,'ms')}"`.
  Growth faster than linear confirms it.
- **Rule 5's unbounded key prefix** `[A-Za-z0-9_.:-]*`. A match can start at every character of a
  long run of key characters that holds no `SENSITIVE` word, so a backtracking engine may do
  roughly quadratic work, and the Bash hooks log whole commands:
  `node -e "const {redactSecrets}=require('./.claude/hooks/lib/guard-common');for(const n of [1e3,1e4,1e5]){const s='a'.repeat(n);const t=Date.now();redactSecrets(s);console.log(n,Date.now()-t,'ms')}"`.
  Growth faster than linear confirms it.

**Settling:** the three checks above; none has been run. **Reopens:** when a hook call is observed
to time out or to slow a session.

**(20) Redaction is pattern-based.** A secret under a key name outside `SENSITIVE`, or passed as a
bare positional argument, is logged. Rule 1 and `VALUE` over-redact, by design, when a quote
directly follows a value. **Settling:** none; a pattern list cannot be complete. **Reopens:** when a
new secret form enters the repository; its rule and its test come with it.

**(21) Open non-blocking review findings.** Each stays as its review file records it:

- **F2 and F3**, in `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-code-review.md`,
  runtime UNVERIFIED. F2: the `--output` rule checks the raw token, so a quoted option such as
  `git diff "--output=x.txt"` passes (`bash-allowlist.js:182-186`). F3: the `git branch` argument
  list is cut without regard to quotes (`coder-bash-guard.js:31`, `:57-62`). Both belong to the
  class of item (2), and their settling commands are in that file.
- **N1, N2 and N4**, in
  `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-code-review-phase2-r2.md`. N1: bare
  `node --test` has no block test. N2: the `-v`/`--verbosity` value check has no block test. N4:
  the reviewer files' "`-rN` marks the Nth re-review" wording.
- **N3**, the completeness of the R10 records, was supplemented
  (`2026-09-25_chore-agents-hardening.md:22799`); no action.
- **DBR-1**, in `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-db-review-phase2.md`.
  `db-reviewer.md:45` checks the same-transaction rule only when a phase "adds an outbox". CLAUDE.md
  keeps the invariant unconditional while the dispatcher is deferred, so DBR-1's mitigation holds.
  Its proposed reword ("adds an outbox or writes to one") stays open until the first outbox work.
- **F4-F6** (the same file as F2), **DBR-2** (the same file as DBR-1) and **R3-6 to R3-8**
  (`thoughts/shared/reviews/2026-10-12_chore-agents-hardening-code-review-phase2-r3.md`): as
  recorded there.

**Settling:** as each file records. **Reopens:** DBR-1 at the first outbox work; the others as
their files record.

**(22) `disable-model-invocation: true` is an unproven enforcement boundary in this environment.**
This item is new in Revision 4 of the workflow-skills plan. Its source is observation O1 of
`thoughts/shared/reviews/2026-10-19_chore-agents-workflow-skills-code-review-phase3.md`, and its
record is the entry "P3.4 attempt M1.1 assessment" in that plan's Phase 3 As built section.

- **What was observed.** In P3.4 attempt M1.1 the human's multiline request naming `/mutate` arrived
  as pasted text, not as a slash command (a human observation; row 498 of the main-session
  transcript `901514d2-f7c7-4bb6-b9c7-ee3425bb0618.jsonl`). The main session then made a Skill tool
  call to `/mutate` (row 508 of the same transcript, tool_use `toolu_017hKZ6yxZawB5ZfHKFL19V6`); row
  509 returned "Launching skill: mutate", and row 510 holds the injected skill body.
  `.claude/skills/mutate/SKILL.md:4` sets `disable-model-invocation: true`. The human did not count
  the attempt as a P3.4 pass.
- **What is not established:** autonomous invocation without a human request; whether the runtime
  should have refused the call, that is, whether the observed behavior is a runtime bug or the
  intended semantics of the flag; and the same behavior for `/commit` and `/pr`.
- **A potential implication only.** `/commit` and `/pr` set the same flag
  (`.claude/skills/commit/SKILL.md:4`, `.claude/skills/pr/SKILL.md:4`), and `/pr`'s `allowed-tools`
  include `Bash(git push *)` and `Bash(gh pr *)` (`.claude/skills/pr/SKILL.md:6`). This is not
  evidence of any automatic push or PR, and `/pr`'s existing merge protections are unchanged.
- **So the flag is treated as an unproven enforcement boundary in this environment.**
- **The settling check, UNVERIFIED and not scheduled:** whether the runtime refuses a model-made
  Skill tool call to a skill that sets `disable-model-invocation: true`. If it is ever run, it uses
  only a harmless test skill, never a real push or PR, and it distinguishes a human request that
  names the skill from one that does not.

**Reopens:** when that check is run, or a skill runs without a human request.

**What this follow-up resolves.** These earlier findings, from
`thoughts/shared/reviews/2026-10-12_chore-agents-hardening-code-review-phase2-r3.md` and, for N5,
the phase2-r2 review, are addressed by the Phase 3-4 changes rather than carried:

- **R3-1, the roadmap rows:** the two stale Known-gaps rows are gone, and the "Agent models"
  paragraph now points to [Roles and tiers](#roles-and-tiers).
- **R3-2/N5, the explainer wording:** `.claude/agents/explainer.md` takes the journal sequence
  number from its caller, stops on a taken number, and never reuses one.
- **R3-3:** `gate.sh` exists at the path bash-allowlist allows (`bash-allowlist.js:36`), and the
  settings entry `Bash(bash .claude/skills/gate/scripts/gate.sh)` completes the wiring.
- **R3-4:** the hook tests run through `gate.sh`, as the CI Known-gaps row states.
- **R3-5:** `/pr` binds verdicts to `HEAD`, through its preconditions 3-7 and Head rule R.

## What is deliberately not used

- **Fable.** The research asked whether the Fable model (the `fable` and `best` aliases) should run
  the planner and the plan-reviewer. It is not used: the hardening plan's answer to that question
  (Q1). Every agent runs the model pinned in [Roles and tiers](#roles-and-tiers).
- **`superpowers`,** the most-recommended skill pack. It ships its own auto-triggering plan, execute
  and review workflow, which would compete with this repository's human-gated pipeline and
  `CLAUDE.md` (F10).
- **Memory plugins.** `thoughts/` is the memory: research, plans and reviews are files in the
  repository, versioned and reviewable (F10). No agent sets the `memory` field either, because it
  enables Read, Write and Edit automatically (F4).
- **`maxTurns`.** A turn cap on the coder recreates the half-written-phase failure F3 describes; on
  a reviewer it cuts the review short.
