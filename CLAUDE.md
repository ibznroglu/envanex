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
