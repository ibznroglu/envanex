# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview

**Envanta** — a production-quality ERP core: inventory, purchasing, and sales/invoicing.
Append-only stock ledger, moving-average costing, order state machines, a SOAP integration
surface, and a Windows Service worker.

Stack: .NET 10 (LTS) · Blazor Web App (Server interactive) · Radzen Blazor Components (MIT) ·
EF Core · SQL Server 2022 · `DevExtreme.AspNet.Data` (MIT) for grid data endpoints.

## Commands

```bash
docker compose up -d                          # SQL Server 2022 on localhost:1433
dotnet build -warnaserror                     # build (warnings are errors)
dotnet test                                   # all tests
dotnet format --verify-no-changes             # style check
dotnet ef migrations add <Name> -p src/Envanta.Infrastructure -s src/Envanta.Web
dotnet ef database update -p src/Envanta.Infrastructure -s src/Envanta.Web
dotnet run --project src/Envanta.Web          # Blazor UI + REST API + Scalar + SOAP
dotnet run --project src/Envanta.Worker       # background worker (console mode)
```

## Architecture

```
src/
  Envanta.Domain          entities, value objects, domain events. ZERO project references.
  Envanta.Application     use cases, DTOs, validators, Result<T>. References Domain only.
  Envanta.Infrastructure  EF Core, SQL Server, repositories, outbox. References Domain + Application.
  Envanta.SoapApi         class library: SOAP contracts + implementations. Mounted by Web.
  Envanta.Web             THE deployable host — Blazor components, REST controllers,
                          OpenAPI/Scalar, and the SoapCore middleware mount.
  Envanta.Worker          BackgroundService, hosted as a Windows Service via UseWindowsService().
tests/
  Envanta.Domain.Tests  Envanta.Application.Tests  Envanta.IntegrationTests (Testcontainers)
db/
  scripts/  views/  procs/          raw T-SQL not expressible in EF
docs/adr/                           one file per architectural decision
```

**Dependency rule:** dependencies point inward only. `Envanta.Domain` never references anything.
A build error caused by adding a reference to Domain is the correct outcome, not a problem to fix.

**One host on purpose.** Blazor UI, REST API and SOAP all live in `Envanta.Web` so the whole app
is a single deployment on the Azure free tier. Separation is enforced by project boundaries and
folders, not by extra processes.

## Key Patterns

- **Result<T>, not exceptions**, for expected failures (validation, business rule violations).
  Exceptions are for bugs and infrastructure faults only.
- **Aggregates own their invariants.** State changes go through methods on the aggregate root.
  All setters are `private set`.
- **Stock ledger is append-only.** `StockMovement` rows are never updated or deleted.
  `StockBalance` is a projection. Any change to stock is a new movement.
- **Costing is a strategy** (`ICostingStrategy`): moving average is the default, FIFO is the
  second implementation. Never inline a costing formula into a use case.
- **EF Core conventions:** money is `decimal(18,4)`, quantity is `decimal(18,6)`, every aggregate
  root has a `RowVersion` concurrency token, every FK is explicitly configured, no lazy loading.
- **Blazor components never touch EF or `DbContext`.** They call Application use cases. A
  component that injects `EnvantaDbContext` is a defect.
- **Radzen `RadzenDataGrid` is the standard grid.** Server-side paging via `LoadData`. Never load
  a full table and page client-side.
- **Grid data endpoints** under `/api/*/datasource` use `DataSourceLoader.Load` from
  `DevExtreme.AspNet.Data`. They exist so the same data protocol works for a DevExpress client.
- **Outbox pattern** for anything leaving the system. Use cases write outbox rows in the same
  transaction as the state change; `Envanta.Worker` dispatches them.
- **All code is English** — identifiers, comments, ADRs, commit messages, test names. User-facing
  strings (UI labels, validation messages) are Turkish with correct characters (ş, ğ, ı, ö, ü, ç).
  One exception: `docs/journal/` is the human's study material and is written in Turkish.

## Orchestration (human-in-the-loop)

- Pipeline: researcher → HUMAN → planner → HUMAN → plan-reviewer → HUMAN → coder → HUMAN →
  code-reviewer → HUMAN → db-reviewer (only if the phase touched schema, migrations or queries)
  → HUMAN → tester → HUMAN. The human drives every transition. Agents STOP and wait.
- **coder**: ONE phase per turn, ends with `PHASE_COMPLETE`, never commits, never pushes, and
  NEVER runs another agent. Every coder prompt must contain an explicit stop instruction
  ("do NOT run any other agent, do NOT commit, wait for me").
- **code-reviewer**: verdict only — `APPROVED` / `NEEDS_REVISION` with file:line. No Bash tool,
  never edits files.
- **db-reviewer**: verdict only — `DB_APPROVED` / `DB_NEEDS_REVISION` with file:line.
  Never edits files. Skipping it on a schema-touching phase is a defect.
- **tester**: verdict only — `READY_TO_PUSH` / `NEEDS_FIXES` with file:line. Runs format, build
  and tests. NEVER edits or writes files — not via Edit/Write, not via shell redirection.
- **explainer**: runs after tester returns `READY_TO_PUSH`, before the ADR and `/pr`. Writes
  `docs/journal/NNNN-<slug>.md` — the only agent permitted to write files, and only there.
  It never writes ADRs and never writes answers to its own questions.
- **Verdicts are terminal for the turn.** When any reviewer returns a verdict, the main agent
  MUST NOT edit or create any file (including plans and docs) and MUST NOT spawn any agent.
  `NEEDS_REVISION` -> human routes to planner. `NEEDS_FIXES` / `DB_NEEDS_REVISION` -> human
  routes to coder. The main agent applying "Required Changes" itself is a defect.
- When a subagent returns, the main agent's turn ENDS: output the verdict verbatim and stop.
  A verdict line is always the LAST content of the turn. "The next pipeline step" is never a
  reason to spawn an agent — the human advances the pipeline.
- Execute only what the current human message asks for. One delegated step per turn.

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

## Constraints

- Do not fix the same error more than twice — `/clear` and write a better prompt.
- Never edit `bin/`, `obj/`, `.vs/`, `.idea/`, `packages/`, `*.user`, `*.pfx`, or `*.Local.json`.
- Never put secrets in `appsettings.json`. Local secrets go in user-secrets.
- Never hand-edit a generated migration to change its intent — create a new migration.
- Never reference `Envanta.Infrastructure` from `Envanta.Domain` or `Envanta.Application`.
- No raw SQL in components, controllers or use cases. Raw T-SQL lives in `db/` and is called
  through a repository method.
- Every new endpoint, use case and component behavior gets a test. "It builds" is not validation.
- Files are LF-only (`.gitattributes` enforces this). Never commit CRLF.

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
