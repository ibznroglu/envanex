Verdict: DB_APPROVED
Reviewer: db-reviewer
Scope: thoughts/shared/plans/2026-09-25_chore-agents-hardening.md phase 2
Range: 84ebd9072f3a06783d71f4af4a7d95f4644c9361..a42624d1b1a526b9feaa985d24dc1b27ed4bc856
Head: a42624d1b1a526b9feaa985d24dc1b27ed4bc856

## Findings

### Stage, inputs and method

- **Stage.** This is the R9.8 Phase 2 db-review under decision D2 (plan:359, plan:493). The plan's "Touches schema?" answer stays No (plan:444-446). D2 schedules this review anyway and prescribes no verdict. It is not the final review (R9.12) and not Validation.
- **R9.8 preconditions.**
  - The latest R9.7 review is `thoughts/shared/reviews/2026-10-11_chore-agents-hardening-code-review-phase2-r2.md`. Its lines 1-5 read Verdict APPROVED, Head 02b7923. A Glob of `thoughts/shared/reviews/` shows no later `code-review-phase2` file.
  - `git diff --name-only 02b7923… HEAD` prints only that review file. This is the caller's preflight record. ev:17558-17563 records the same, with a42624d's sole parent 02b7923.
- **Inputs.**
  - the caller's changed-path list for the Range (32 paths) and its src/tests/db-restricted diff (empty, exit 0)
  - D2 and R9.8, the Phase 2 section (plan:2966-3187) and the approved code review
  - Cross-check: the caller's 32 paths equal the 31 evidence-recorded paths of 84ebd90..02b7923 (ev:17268-17299) plus the one review file of 02b7923..a42624d (ev:17562-17563). The src/tests/db diff for 84ebd90..02b7923 is also recorded empty (ev:17302-17304).
- **Method.**
  - I read files and ran nothing.
  - Git results, hashes and the plan SHA256 are values recorded by the caller or in the evidence file. I cannot compute or re-run them.
  - Every runtime claim is marked UNVERIFIED and names the command that settles it.
- **Abbreviations.** plan = `thoughts/shared/plans/2026-09-25_chore-agents-hardening.md`; ev = `TestResults/chore-agents-hardening/evidence/phase-2.md`.
- **File name.** This file is at the caller's concrete path, which carries the preflight date 2026-10-11 (recorded 2026-10-11T20:56:30Z). The session's date changed to 2026-10-12 during this review.

### What the Range changes

- **Code and configuration (17 paths):**
  - the eight `.claude/agents/*.md` files
  - the three hooks and their three test files
  - `.claude/settings.json`, `.mcp.json` and `CLAUDE.md`
- **Records (15 paths):** plans, research and reviews under `thoughts/shared/`.
- **What the Range does not contain.**
  - No `.cs`, `.csproj`, `.sql`, migration, EF configuration, `appsettings*` or connection-string file.
  - Every EF migration file is under `src/Envanex.Infrastructure/Migrations/` (Glob): InitialSchema, `EnvanexDbContextModelSnapshot.cs` and the `Identity/` migrations. The empty src/tests/db diff therefore covers every migration.
  - None of the database-relevant files outside src/tests/db: `docker-compose.yml`, `.env.example`, `Directory.Packages.props`, `Directory.Build.props`, `global.json`, `Envanex.slnx` and `.github/workflows/ci.yml`.
  - This agrees with Non-goals (plan:433) and "Touches schema? No" (plan:446).

### Checklist categories: not applicable to this Range

- **Schema and types.** The Range contains no entity, EF configuration or migration. No column type, `RowVersion` token, nullability, unique index or FK delete behaviour can have changed.
- **Immutability.** The Range has no C# or SQL. It adds no update path to `StockMovement` or to any other table.
- **Migrations.** No migration was added, edited or removed. There is no drift, reversibility or data-destroying operation to assess.
- **Queries and indexes.** The Range adds no query, repository method, `IQueryable` surface, `DataSourceLoader` endpoint or tracking change.
- **Transactions.** The Range adds no outbox, transaction or external call in product code. A case-insensitive Grep of `src/` for "outbox" finds no file.

### Phase 2 changes to the database workflow (tooling and review text)

1. **The hooks never touch a database.**
   - Each hook requires only `./lib/guard-common`: bash-allowlist.js:7, coder-bash-guard.js:7 and write-scope.js:6. guard-common.js:6 requires only `fs`.
   - No hook loads `child_process`, opens a connection or executes the command it inspects. Each parses its input and exits 0 or 2.
   - The test inputs that name container operations are data. `docker compose ps` (bash-allowlist.test.js:103-104) and `docker compose down` (:173-174) reach the hook only as JSON on stdin, with a fixed argv (run-hook.js:23-27). The tests therefore cannot stop or remove the SQL Server container.
2. **The tester and explainer allowlists admit no schema or data command.** This is a static reading.
   - Tester profile (bash-allowlist.js:35-50):
     - read-only git prefixes
     - `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes`, limited to the fixed-token flags (:113-144)
     - `node --test` on the hook tests
     - `docker compose ps`
     - read tools
   - Explainer profile (:51-58): read-only git prefixes, `dotnet list package` and read tools.
   - None of the following matches an entry, so each reaches the no-matching-entry block (:189-193): `dotnet ef …` (database update or drop, migrations add or remove), `sqlcmd`, `docker compose up` or `down`, and `docker volume …`. The pre-check (:62-95) blocks chaining, substitution and redirection before matching.
   - Bare `dotnet test` (tester.md:35) still matches with no further tokens (:113-144), so the full-suite command stays available for validation.
   - This review does not establish that the hooks fire at runtime for these profiles. That is P2.4, P2.5 and C2.2 evidence (plan:3367 onward).
3. **The coder guard leaves migration authoring alone.**
   - coder-bash-guard decides only on the words `\bgit(\.exe)?\b` and `\bgh\b` (coder-bash-guard.js:9-10, :123-134).
   - The migration commands at CLAUDE.md:21-22 contain neither word.
   - The migration rules stay in force: CLAUDE.md:29-31, :73-75, :149 and :163, and the deny list (item 5).
4. **The write scopes keep every agent except the coder away from schema files.**
   - The hook lines are researcher.md:12, planner.md:12, plan-reviewer.md:12, code-reviewer.md:12, db-reviewer.md:12, tester.md:16 and explainer.md:16.
   - They scope those agents to `thoughts/shared/research`, `thoughts/shared/plans`, `thoughts/shared/reviews` (three reviewers), `TestResults` and `docs/journal`.
   - write-scope.js:18-25 blocks every target outside the scope. With item 2 for the two Bash profiles, none of these agents can write an EF configuration or a migration under `src/` (static reading).
   - This run is the db-reviewer's own live connection; the human correlates it.
5. **`.claude/settings.json`.**
   - **Phase 2 additions** (plan:2994-3011): three exact `node --test` entries (:45-47), three Learn tools (:49-51), `enabledPlugins` (:178-180), `disableClaudeAiConnectors` (:181) and `disableBundledSkills` (:182). None of them touches a database.
   - **DB-relevant allow entries** are from Phase 1's B1 list (plan:989-1001): `dotnet ef migrations add *` and `dotnet ef migrations list *` (:17-18), and `docker compose up -d` (:21-22). `dotnet ef database update` is not in the allow list, so it prompts (plan:1006).
   - **DB-protective deny entries** are from Phase 1 (plan:1012-1031):
     - `dotnet ef database drop*` (:92-93) and `dotnet-ef database drop*` (:144-145)
     - `docker compose … down -v` and `--volumes`, with the `docker-compose` forms (:84-87, :132-143)
     - `docker volume rm` and `prune` (:88-91)
     - `docker system prune*` (:146-147)
   - All of them are present at Head. The deny list has 94 entries, the total Phase 1 states (plan:1014).
   - I have not verified that Phase 2 left these lines byte-identical; the code review records the list as conforming. Settle with `git diff 84ebd90 a42624d -- .claude/settings.json`.
6. **`.mcp.json` and plugins.**
   - `.mcp.json:1-8` adds only the HTTP `microsoft-learn` documentation server. The Range adds no database MCP server, connection string or credential.
   - `csharp-lsp` is a code-navigation plugin. That it runs no EF design-time code and opens no database connection is UNVERIFIED (runtime behaviour). Nothing in the Range suggests otherwise.
7. **DB-review text at Head.**
   - db-reviewer.md:22-46 carries every check of the five categories.
   - CLAUDE.md keeps the database rules:
     - the append-only ledger (:69-70)
     - types, `RowVersion`, explicit FKs and no lazy loading (:73-75)
     - the outbox rule (:82-83)
   - CLAUDE.md:91, db-reviewer.md:3 and planner.md:34 all trigger the db-review on schema (data model), migrations and queries.
   - db-reviewer.md:26: the new examples match the unique indexes configured today: `ProductConfiguration.cs:16`, `WarehouseConfiguration.cs:15` and `UnitOfMeasureConfiguration.cs:15`, under `src/Envanex.Infrastructure/Persistence/Configurations/`. I checked only that the examples correspond to these indexes, not that the migrations or the database carry them.
   - plan-reviewer.md:29 now names concurrency, negative stock, rounding and the append-only ledger, which match the in-scope stock core (docs/roadmap.md:81-83). The removed partial receipt and cancelled orders belong to the deferred purchasing and sales PRs (docs/roadmap.md:96-99). Neutral to positive.
   - coder.md:37-41 now stops the coder on an open data-integrity decision. Positive.

### Non-blocking observations

**DBR-1 — Low — db-reviewer.md:45: the outbox check applies only "if the phase adds an outbox".**
- The plan specified this wording (plan:3172, F8), and the code review found it conformant.
- Today the condition is vacuous: `src/` has no outbox, and docs/roadmap.md:113-115 records that no outbox table exists.
- **Residual.** docs/roadmap.md:90-92 places the outbox question with PR 8, which is in scope (:81).
  - If an outbox arrives there, a later phase that adds new outbox inserts does not literally "add an outbox".
  - A literal reading would then skip the same-transaction check that CLAUDE.md:82-83 still requires of every use case.
- **Data risk if the check is skipped:** a state change committed without its outbox row, or an outbox row without its state change. Either way an integration message is lost or phantom.
- **Why not blocking.** Phase 2 neither adds nor uses an outbox. CLAUDE.md:82-83 stays unconditional, and the wording is the approved plan's.
- **Possible wording**, through the normal plan workflow (Phase 4, or before the first outbox phase): "If the phase adds an outbox or writes to one: the state change and the outbox insert happen in the same transaction."

**DBR-2 — Info — this review does not establish what Phase 2 removed from DB-related agent text and CLAUDE.md.**
- Item 7 confirms the present text only.
- Whether Phase 2 removed any other DB-relevant line from db-reviewer.md, plan-reviewer.md, planner.md, coder.md, tester.md or CLAUDE.md is UNVERIFIED. I cannot read git history, and ev records hashes and path lists, not content diffs.
- Settle with `git diff 84ebd90 a42624d -- .claude/agents CLAUDE.md`, and check that plan:3013-3018 or plan:3147-3187 covers every removed DB-related line.
- **Why not blocking.** No database check is missing from the present text, so nothing is lost at Head.

### Limits of this review

- **Existing database behaviour.** This review does not verify the current schema, model snapshot, migrations, queries, indexes or test results. It shows only that the Phase 2 Range changes none of them. That is an absence of change, not a verification of existing behaviour.
- **Files outside the Range.** I did not review `.claude/settings.local.json` or the user settings. Their hashes are in the human's identity records (ev:17475-17485).
- **Runtime.** This review does not re-establish hook firing, plugin behaviour or test outcomes, and makes no claim about the P2.x or C2.x records.

## Required changes

None.
- Phase 2 changes no schema, EF mapping or configuration, migration, query, transaction or database behaviour.
- It gives no agent a new way to change the database, and no agent except the coder a way to write schema files.

Not required; for the human's evaluation:
- DBR-1: reword the outbox condition in a later approved change.
- DBR-2: run the settling diff if a record of removed lines is wanted before the final review.
