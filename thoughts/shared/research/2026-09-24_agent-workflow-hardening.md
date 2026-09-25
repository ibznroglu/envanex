# Research: agent workflow hardening, model and effort per agent, and the local SQL Server compose file

Date: 2026-09-24 · Repo state: `feat/authz` @ 7283870 · Checked against the Claude Code docs under Sources.

## Question

Do `.claude/agents/*`, `.claude/skills/*`, `.claude/settings.json` and `CLAUDE.md` follow current Claude Code
guidance? What would make the pipeline enforce its own rules and cost less main-session context? Which model
and effort should each agent run? Is `docker-compose.yml` safe?

## Relevant files

- `.claude/agents/*.md` (8), `.claude/skills/{commit,verify,pr}/SKILL.md`, `.claude/settings.json`
- `CLAUDE.md` (164 lines), `docs/roadmap.md` (Scope section)
- `docker-compose.yml`, `.gitignore`
- `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs:42` (Testcontainers image)
- `thoughts/shared/plans/2026-09-02_ef-infrastructure.md:93-97, 224-225` (password literal)

## Current behavior

| Agent | model | effort | tools | writes |
|---|---|---|---|---|
| researcher | haiku | medium | Read, Glob, Grep | no; content returns to the main agent |
| planner | opus | high | Read, Glob, Grep | no; same |
| plan-reviewer | opus | high | Read, Glob, Grep | no |
| coder | opus | high | Read, Write, Edit, MultiEdit, Bash, Glob, Grep | yes |
| code-reviewer | sonnet | high | Read, Glob, Grep | no |
| db-reviewer | opus | high | Read, Glob, Grep | no |
| tester | haiku | low | Read, Bash, Glob | forbidden by prompt only |
| explainer | opus | high | Read, Glob, Grep, Bash, Write | `docs/journal/` by prompt only |

`settings.json` allow list includes `git add*`, `git commit*`, `git push*`, `docker compose*`, `dotnet ef*` and
`dotnet run*`, with no deny list. A PreToolUse path guard and a PostToolUse `dotnet format` hook both match
`Edit|Write|MultiEdit`. All three skills set `disable-model-invocation: true` and `allowed-tools`.

## Findings

**F1: The pipeline's hard rules are prompts, not enforcement.**

- Settings allow rules apply to the whole session, subagents included [sub-agents].
- `git commit*`, `git push*` and `git add*` are therefore pre-approved for the coder. "The coder never commits
  or pushes" holds only because its prompt says so.
- Nothing blocks `git stash`. The PR 6b stash survives on discipline alone.
- `docker compose*` pre-approves `docker compose down -v`, which deletes the local database volume.
- `dotnet ef*` pre-approves `dotnet ef database drop`.
- The tester's and explainer's "read-only Bash" and the explainer's "only `docs/journal/`" are prompt rules.
- The main session starts in auto mode on Pro, Max and Team plans unless settings change it; there a
  classifier, not the human, approves commands [sub-agents]. Removing allow rules alone is therefore not a
  guarantee.

Candidate changes:

- Per-agent `PreToolUse` hooks in frontmatter for what differs per role:
  - coder: block every git write
  - tester: Bash allowlist
  - explainer: Write only under `docs/journal/`
- A `permissions.deny` list for commands no one should run: `git stash`, `git clean`, `git reset --hard`,
  `git checkout --`, `git restore`, `git rebase`, `git push --force`/`-f`, `docker compose down -v`,
  `dotnet ef database drop`. Deny rules also apply to subagents [sub-agents].
- Drop `git add/commit/push` from the global allow list. `/commit` and `/pr` already grant them through
  `allowed-tools` while the skill runs [skills].
- Narrow `docker compose*` to `ps`, `up -d` and `logs`.
- Project agents' frontmatter hooks run only after the workspace-trust dialog is accepted [sub-agents].
- Write hooks as Node scripts under `.claude/hooks/`. The current inline `node -e` pattern already runs on this
  Windows setup; files are easier to read and to probe.

Denying `git checkout --` and `git restore` means mutation proofs restore with the inverse Edit and prove it with
`git diff --exit-code -- <file>`. That is a stronger check than a blind restore.

**F2: The Edit/Write path guard likely misses its directory rules on Windows (UNVERIFIED).**

- The regex looks for `/`-separated `bin|obj|packages|.vs|.git` segments.
- File tools on Windows receive native paths; the coder's Phase 5 report lists `C:\projects\envanex\src\...`.
  If so, only the suffix rules (`.user`, `.pfx`, `.env`, `Local.json`) can fire.
- The hook swallows every exception (`catch(e){}`), so a parse failure lets the write through.

To settle it, ask Claude Code to write `obj\guard-probe.txt` and `obj/guard-probe.txt`; both must be blocked.

Candidate change: normalise `\` to `/` first, and exit 2 on any error so the hook fails closed.

**F3: Model and effort sit opposite to where PR 6b's defects came from.**

Facts:

- Opus 5.5, Opus 5 and Sonnet 5 accept low, medium, high, xhigh and max [model-config].
- Haiku 4.5 does not appear in the effort table, so the researcher's and tester's `effort` is likely ignored
  (UNVERIFIED).
- Frontmatter `effort` overrides the session level but not `CLAUDE_CODE_EFFORT_LEVEL`. Org caps and
  `maxEffortLevel` clamp it [model-config].
- `model: opus` resolves to the main conversation's exact model when that model is an Opus. A full ID such as
  `claude-opus-5-5` pins it [sub-agents].
- `/tasks` shows each running subagent's model and, when set, its effort (v2.1.242+) [sub-agents].
- Anthropic's effort guidance for Opus 5: start at high, step up to xhigh for demanding coding and agentic
  work, and use max when a task justifies unconstrained token spend [effort].
- Opus 5.5 changes the baseline. Its default effort is medium, in Claude Code and on the API
  [model-config, opus-5.5].
- At the same effort level, Opus 5.5 thinks more per turn than Opus 5, most of all at xhigh and max
  [opus-5.5].
- A top-level `effortLevel` in user settings does not apply to Opus 5.5. `/effort` saves a level per model
  [model-config].
- This account runs Claude Pro (Claude Code v2.1.281), so usage limits are the binding constraint.

PR 6b's recorded failures were verification failures:

- command output reported but never produced
- wrong counts
- a nonexistent API name
- nine green tests that protected nothing

The human and the mutation proofs caught them. Yet the weakest configurations sit at the verification end:

- the researcher runs on Haiku,
- the code-reviewer runs on Sonnet while reviewing an Opus coder,
- the tester runs on Haiku at low, and holds the only automated "do the tests assert anything" step.

| Agent | Now | Proposed | Reason |
|---|---|---|---|
| researcher | haiku / medium | claude-opus-5-5 / xhigh | Gathers the facts everything else builds on; PR 6b's research needed a three-conflict correction. |
| planner | opus / high | claude-opus-5-5 / max | Judgment: a plan gap is the costliest defect. |
| plan-reviewer | opus / high | claude-opus-5-5 / max | Judgment: Phase 5 reached the coder with four open gaps. |
| coder | opus / high | claude-opus-5-5 / xhigh | Executes a plan that max-level agents designed and will review. Not max: see below. |
| code-reviewer | sonnet / high | claude-opus-5-5 / max | Judgment, and read-only. A reviewer weaker than the author is an anti-pattern. |
| db-reviewer | opus / high | claude-opus-5-5 / max | Judgment, and read-only; data correctness is not negotiable here. |
| tester | haiku / low | claude-sonnet-5 / low | Runs a script and pastes its output (F6); more reasoning adds nothing. |
| explainer | opus / high | claude-opus-5-5 / xhigh | Study material for the human; depth matters, but it judges nothing. |

Principle: agents that judge run at max, agents that execute at xhigh, and mechanical steps at low. The human set
quality above speed and quota, which is why this is the final version. The earlier drafts lowered effort only to
save quota.

The coder is the one exception to "max wherever quality matters". It is the only agent that writes code, and a run
cut off by a usage limit leaves a half-written phase behind. The PR 6b Phase 3 stash is exactly that. A read-only
reviewer cut off the same way is simply re-run. Keeping the coder's runs at xhigh, the documented level for
demanding coding work, shortens the window in which a limit can hit. Raise it to max for a single phase when the
phase is concurrency- or transaction-heavy (PR 8's ledger).

**F4: Every long report passes through the main session's context.**

The researcher, planner and reviewers have no Write tool. Their full output returns to the main agent, which
writes it: the PR 6b plan is 1,858 lines and its review 355.

Candidate change: give each of them Write, locked by a frontmatter hook to one directory:

- researcher: `thoughts/shared/research/`
- planner: `thoughts/shared/plans/`
- reviewers: `thoughts/shared/reviews/`
- tester: `TestResults/`

Each then returns only the verdict, the findings and the file path.

Do not use the `memory` field on these agents: it enables Read, Write and Edit automatically [sub-agents].

Caveat (UNVERIFIED): during the Phase 5 mutation run, the coder reported that the harness blocked it from
writing a report file ("Subagents should return findings as text"), yet its Phase 5 implementation run reported
writing one. Probe this first. If subagents cannot write report files, fall back to a `SubagentStop` hook that
saves the final message; which hook input field carries the transcript is also UNVERIFIED.

**F5: `/verify` shares its name with a bundled skill.**

- Claude Code ships `/verify` ("build and run your app to confirm a change, without falling back to tests")
  alongside `/run` and `/run-skill-generator` [skills].
- Which one wins a name clash is not stated in the pages read (UNVERIFIED). Candidate change: rename ours to
  `/gate`.
- The bundled `/verify`, with a recorded run recipe, might later automate the plans' manual smoke steps.
  Evaluate that separately.
- Same-named skills in `~/.claude/skills/` reportedly take precedence over project skills (UNVERIFIED in the
  official pages read; stated by third-party guides). Check that directory for `commit`, `pr` or `verify`.

**F6: Deterministic steps are prose the model follows.**

Skill-authoring guidance puts deterministic, repetitive work in `scripts/` [skill-creator]. Candidate changes:

- `/gate` runs `.claude/skills/gate/scripts/gate.sh`. The script writes raw format, build and test output plus
  exit codes to `TestResults/<branch>/gates.txt`, then prints a filtered view.
- A new `/mutate` skill standardises the mutation proof and logs to `TestResults/`:
  1. mutate with Edit
  2. run one test filter and capture its raw output
  3. apply the inverse Edit
  4. run `git diff --exit-code`
- Agents paste script output and never retype it.
- Set `effort: low` on `/commit`, `/gate` and `/pr`; skill frontmatter accepts `effort` [model-config].

**F7: The PR 6b lessons live in chat, not in the repo.**

Only the UNVERIFIED rule and the tester's "paste the real output" line reached the agent prompts.

Candidate: a short "Verification rules" section in `CLAUDE.md`:

- a passing test is not a protecting test
- mutations run against committed code
- raw output is pasted, never narrated
- agents without a shell mark runtime claims UNVERIFIED
- re-run a reviewer only when it will learn something new
- code and docs go in separate commits

Also four plan-reviewer checks drawn from Phase 5's gaps:

- every branch the signatures require has a named test that reaches it
- every file a phase needs is in its Files list
- no test can pass on state another code path wrote
- developer-local configuration, such as user-secrets, cannot reach test hosts

**F8: `CLAUDE.md` still describes the pre-cut scope.**

- Its overview lists purchasing, sales/invoicing, SOAP and a Windows Service. `docs/roadmap.md` Scope puts all
  of them out of scope.
- The plan-reviewer checklist asks about partial receipts and cancelled orders.
- Every agent loads `CLAUDE.md`, so every agent is primed for architecture the project will not build.
- Keep the file under its own 200-line limit.

**F9: Small debt.**

- `MultiEdit` appears in the coder's tools and in both hook matchers, but it is not in the tool set that
  background subagents keep. Interactive sessions run subagents in the background by default [sub-agents].
- Permission patterns mix `Bash(git log*)` and `Bash(git status:*)`. Pick the form the permissions page
  documents.
- No agent sets `maxTurns`.

## Plugins and skills worth adding (F10)

Chosen by the gap each one closes, not by popularity.

- **`/security-review`.** Built into Claude Code. It reviews the branch's diff against the default branch for
  injection, auth and data-exposure risks, and needs an `origin` remote. Run it before `/pr` on PR 6b, an
  authorization PR, and then on every PR that touches auth or input handling.
- **`csharp-lsp`.** Anthropic's official marketplace; needs `dotnet tool install --global csharp-ls`.
  - It gives agents go-to-definition, references and compiler diagnostics without a shell.
  - Add `LSP` to the read-only agents' `tools`, so reviewers can confirm an API exists instead of marking it
    UNVERIFIED. A named PR 6b failure was a nonexistent API name.
  - Probe it on .NET 10 first; early versions of the plugin needed manual fixes.
- **`microsoft-docs`.** Official marketplace: the Microsoft Learn MCP server plus three skills, free and
  keyless.
  - It serves current official docs and samples for ASP.NET Core, EF Core, Identity and Azure App Service.
  - Grant its tools explicitly to the researcher, planner and reviewers; their `tools` allowlists exclude MCP
    tools otherwise.
- **PR 7, when the UI becomes the product.** Add a Playwright-based UI testing skill or real Playwright tests to
  replace the manual smoke steps. `frontend-design` is optional.
- **CodeQL, in CI rather than in the agent loop.** Code scanning for C#, free for public repositories, is a
  visible security signal.
- **Skip `superpowers`,** the most-recommended pack. It ships its own auto-triggering plan/execute/review
  workflow, which would compete with this repository's human-gated pipeline and `CLAUDE.md`.
- **Skip persistent-memory plugins.** `thoughts/` is the memory.
- **Treat every third-party skill as code you run.** It runs with the session's permissions. Read its
  `SKILL.md` and scripts before installing, prefer the official marketplace, and install at project scope so the
  choice is versioned.

## Findings added after PR 6b and fix(db) (F11-F21)

**F11: Review verdicts are not persisted.**
- `/pr` accepts only verdicts given in its own session.
- PR 6b's per-phase code-review verdicts lived in earlier sessions' transcripts, so its PR body could cite only
  what the repository recorded.
- Candidate: each reviewer writes its verdict, with the commit range it covered, to
  `thoughts/shared/reviews/`, and `/pr` cites those files. This extends F4's write-to-file contract.

**F12: The PR-end sequence needs a whole-branch review and `/security-review`.**
- In PR 6b, the final whole-branch code review found a test that could no longer fail. It sat on a seam between
  Phase 3 and Phase 4 that no per-phase review could see.
- `/security-review` found a factual error in the plan's Known gaps.
- Candidate `CLAUDE.md` pipeline at PR end: whole-branch code-reviewer → `/security-review` → tester →
  `/gate` → `/pr`.

**F13: The tester is the weakest gate, as F3 predicted.** On Haiku at low effort:
- In PR 6b's final gate it summarized the test run instead of pasting it. It also wrote a meaningless line:
  "11 test methods (9 core + 2 config fixtures InitializeAsync/DisposeAsync)".
- In the docs(roadmap) gate it gave a changed file's path wrongly.
- In fix(db) it returned NEEDS_FIXES on a gate prompt whose file list was stale. Following the list was
  right; the list was wrong.

Candidates:
- The tester pastes the output of the `/gate` script and never composes its own.
- The main agent builds the tester's file list from `git diff --name-only main...HEAD`, so the list is
  generated, not typed.

**F14: The per-invocation model override accepts only aliases.**
- Claude Code reported that the Agent tool takes short names such as `opus`, not `claude-opus-5-5`.
- Per the docs, frontmatter accepts full model IDs. This is UNVERIFIED in this setup.
- Probe it in Phase 2: `/tasks` shows the model each agent actually ran on.

**F15: The path guard's `.env` rule is too broad.**
- In fix(db) it blocked the coder from writing `.env.example`, a template holding no secret, so the human had
  to write the file.
- Candidate: allowlist `.env.example`.
- The block does prove that the guard's suffix rules fire on Windows paths. Its directory rules (`bin`, `obj`,
  `.git`) are still unverified (F2).

**F16: `/commit` needs a fixed recipe.**
- It ran `git commit` through Windows PowerShell 5.1, which split a message at its double quotes. It recovered
  by soft-resetting its own unpushed commit and committing with `-F`.
- Candidates:
  - always commit with `-F <file>`
  - add `roadmap`, `adr` and `journal` to its scope list, since all three are in use
  - write down when a soft reset of an unpushed commit is allowed

**F17: The roadmap's "How the work is run" is stale.**
- It says the code-reviewer runs on Sonnet, and it names `/verify`.
- Update it with the new tiers and `/gate`.
- The same PR fills in `fix(db)`'s status in the PR table: done (#14).

**F18: `/mutate` rules, from PR 6b's mutation runs.** These extend F6.
- A mutation must not be a constant the compiler folds. `if (false)` before a `return` failed the build with
  CS0162 under TreatWarningsAsErrors (M2).
- Never pass `--no-build` while a mutation is applied.
- A red counts only when the test's own assertion fails. An infrastructure failure, such as MR5's Docker API
  500, is rerun and logged separately.
- Report the failing assertion for every red.
- Restore with `git checkout -- <file>` or with the inverse Edit, and prove the restore with
  `git diff --exit-code`.

**F19: Write records after the evidence exists.**
- A plan note claimed a tester rerun before the rerun had happened. It did happen before merge, but the record
  was written ahead of its evidence.
- Candidate verification rule: record an outcome only once it exists, and mark it pending until then.

**F20: Windows notes for `CLAUDE.md`.** Each of these cost time in this project:
- `localhost` resolves to `::1` first, so with an IPv4-only port binding, connect to `127.0.0.1`.
- In Git Bash, double quotes still expand `!`, backticks and `\`. Single-quote literals.
- Git Bash rewrites a native program's argument that starts with `/` into a Windows path. For generated
  secrets, prefer hex to base64.
- The EF design-time factories read `ENVANEX_CONNECTION_STRING`, not user-secrets.

**F21: Agent, skill and settings changes need a restart.**
- Every phase of `chore(agents)` changes the pipeline that runs the next phase.
- End each phase with a Claude Code restart and that phase's probes. The next phase then runs on the new
  configuration, and a broken guard shows up at once instead of three phases later.
- When exactly Claude Code rereads agents, skills and settings mid-session is UNVERIFIED. Restarting avoids the
  question.

**The light lane is already proven.**
- `docs(roadmap)` (#13) and `fix(db)` (#14) ran with no researcher and no planner: a short plan file, then the
  coder, reviews and the tester.
- They still caught real defects: the IPv6 `localhost` trap and the missing `dotnet-ef`.
- Candidate: write the lane into `CLAUDE.md` for small, non-feature PRs.

## Docker compose (done: #14)

Merged as #14 (f6df221). Beyond D1-D5, the fix also:
- moved every documented connection string to `127.0.0.1`
- pinned the EF Core CLI as a local tool
- rewrote README's "Running locally"

See `thoughts/shared/plans/2026-09-24_fix-db-compose-sa-password.md`. The findings below are kept as the
record.

- **D1: Committed default password.** A default SA password is committed to a public repository:
  `${MSSQL_SA_PASSWORD:-Erp_Local_Dev_2026!}`. The same literal is in
  `thoughts/shared/plans/2026-09-02_ef-infrastructure.md`.
- **D2: Port on every interface.** The port is published on every interface (`"1433:1433"`). While the
  container runs, anyone on the same network can try `sa` with the published password.
- **D3: Fix.**
  - `MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD:?Set MSSQL_SA_PASSWORD in .env - see .env.example}"`
  - `ports: ["127.0.0.1:1433:1433"]`
  - a `.env.example` with the key and no value
  - `!.env.example` in `.gitignore`, because the existing `.env.*` rule ignores it today
- **D4: Rotation trap.** SQL Server applies `MSSQL_SA_PASSWORD` only when it initialises a new data directory,
  and the named volume keeps the old password. Before switching, do one of these:
  - run `ALTER LOGIN sa WITH PASSWORD = '<new>'` while the old password still works, or
  - run `docker compose down -v`, then both `dotnet ef database update` commands.

  Then update the `ConnectionStrings:EnvanexDb` user-secret. Skipping this leaves the healthcheck failing,
  because it reads the new value.
- **D5: History.** The old literal stays in git history. It is a local development credential, and rotation
  makes it inert; rewriting public history is not worth it.
- **D6: Image tag (optional).** Pin the same SQL Server image tag or digest in `docker-compose.yml` and
  `SqlServerFixture.cs:42`, both on `2022-latest` today, so development and tests run the same engine. Look the
  tag up; do not guess it.

## Proposed PR split

1. `fix(db): stop publishing the local SQL Server SA password`. Done as #14.
2. `chore(agents): enforce the pipeline's rules and tier models by risk`.
   - Phase 1, global guards: the deny list, a narrowed allow list, and a fixed path guard with an
     `.env.example` allowlist (F1, F2, F15).
   - Phase 2, agents:
     - model and effort, with the F14 probe (F3)
     - tool cleanup (F9)
     - per-role hooks (F1)
     - write-to-file contracts and verdict files (F4, F11)
     - the LSP and Microsoft Learn tools for read-only agents (F10)
   - Phase 3, skills:
     - `/gate` with its script (F5)
     - `/mutate` with the F18 rules (F6)
     - `/commit`'s recipe (F16)
     - the tester's generated file list (F13)
     - low effort on mechanical skills
   - Phase 4, `CLAUDE.md` and the roadmap:
     - scope and the migration note (F8)
     - verification rules, including F19 (F7)
     - the PR-end sequence (F12)
     - the light lane
     - Windows notes (F20)
     - "How the work is run" and the `fix(db)` status (F17)

     Start with the current memory docs (imports, rules files); they are not yet researched.
   - Every phase ends with a restart and its probes (F21).

   Validation is by probe; each probe is a mutation proof of one guard:
   - the coder, asked to run `git stash list`, is blocked
   - a Write to `obj\x` is blocked
   - a reviewer's Write to `src/` is blocked, and to `thoughts/shared/reviews/` succeeds
   - `/tasks` shows the intended model and effort for each agent
   - `/gate` output matches its file

## Open questions for the human

1. Is Fable 5.1 available on your plan (the `fable` and `best` aliases [model-config])? If so, the planner and
   plan-reviewer are the candidates.
2. Pin full model IDs, for reproducibility and a visible commit per upgrade, or keep aliases for automatic
   upgrades?
3. Adopt the light lane that #13 and #14 proved, for small non-feature PRs (see "The light lane is already
   proven")?
4. Evaluate the bundled `/verify` for the manual smoke steps?
5. ADR, or a `docs/ai-workflow.md` page for readers of the repo?

## Sources

- [sub-agents] https://code.claude.com/docs/en/sub-agents
- [model-config] https://code.claude.com/docs/en/model-config
- [skills] https://code.claude.com/docs/en/skills
- [effort] https://platform.claude.com/docs/en/build-with-claude/effort
- [opus-5.5] https://platform.claude.com/docs/en/models/opus-5-5/whats-new-opus-5-5
- [csharp-lsp] https://github.com/anthropics/claude-plugins-official/tree/main/plugins/csharp-lsp
- [microsoft-docs] https://github.com/microsoftdocs/mcp
- [skill-creator] Anthropic's skill-authoring guidance: deterministic work belongs in `scripts/`
