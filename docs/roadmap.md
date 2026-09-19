# Roadmap

Envanex is built in a fixed sequence of pull requests, each one small enough to review in a sitting
and each one leaving `main` green. This file records where the project is going, what has been
decided along the way, and how the work is run. Architectural reasoning lives in
[`docs/adr/`](adr/); this file is the map.

## Scope

An inventory ERP core: master data, an append-only stock ledger, inventory costing, and the
REST and grid surface that serves them. Purchasing, sales, invoicing, SOAP integration, the
background worker and reporting are deliberately out of scope — they were in the original plan
and were cut so the project could be finished rather than left half-built. The deferred table
below keeps them recorded.

Envanex is a portfolio project, not a commercial product. It is built to be read and judged by
engineers, which is why every architectural decision is recorded in docs/adr/ and every known
gap is tracked rather than hidden.

## Stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 (LTS) |
| UI | Blazor Web App (Server interactive) + Radzen Blazor Components (MIT) |
| API | ASP.NET Core REST, OpenAPI + Scalar, `DevExtreme.AspNet.Data` grid protocol (MIT) |
| Integration | SoapCore XML web service |
| Background | .NET Worker Service hosted as a Windows Service |
| Data | EF Core 10 + SQL Server 2022 |
| Tests | xUnit, Shouldly, Testcontainers |

**On DevExpress.** The job market this project targets asks for DevExpress experience, but the
component suite is commercially licensed and cannot ship in a public repository or a live demo. The
split is deliberate: the server side of the DevExtreme data protocol (`DevExtreme.AspNet.Data`, MIT)
is permanently part of the API, the shipped UI uses Radzen, and a `showcase/devexpress` branch will
carry the same screens rebuilt on DevExpress Blazor under a trial licence, recorded as screenshots
in the README. That branch is never merged.

## Pull requests

### Foundation

| # | Title | Status |
|---|---|---|
| 1 | `chore(solution)` — solution, projects, central package management, CI | done (#1) |
| 2 | `feat(domain)` — shared kernel: `Result`, `Entity`, `AggregateRoot`, `Money`, `Quantity`, `Currency` | done (#2) |
| 3 | `feat(infra)` — EF Core, mapping conventions, Testcontainers harness | done (#3) |

### Master data and first deployment

| # | Title | Status |
|---|---|---|
| 4 | `feat(domain)` — `UnitOfMeasure`, `Warehouse`, `Product`, first migration | done (#4) |
| 5a | `feat(api)` — application layer, repositories, unit tests | done (#6) |
| 5b | `feat(api)` — REST endpoints, hardened grid datasource, integration tests | done (#8) |
| 6a | `feat(auth)` — ASP.NET Core Identity, JWT with rotating refresh tokens, login/refresh/logout | done |
| 6b | `feat(auth)` — authorization policies, [Authorize] on every endpoint, Blazor cookie scheme, read-only demo account | |
| 7 | `feat(web)` — Blazor shell, product grid, **first deploy** | |

Roadmap numbers and GitHub PR numbers are not the same. The status cell of a completed row
carries the GitHub PR number it merged as.

PR 7 is the point where the project becomes publicly visible: Azure SQL free tier, App Service,
automatic deployment on merge to `main`, a read-only demo account and realistic Turkish seed data.
Everything after it ships continuously.

### Stock core

| #  | Title | Status |
|----|---|---|
| 8  | `feat(domain)` — append-only stock ledger, `StockMovement` and balance projection | |
| 9  | `feat(domain)` — costing strategies: moving average, then FIFO | |
| 10 | `feat(web)` — stock screens, manual adjustment, movement history | |

### Deferred (out of scope)

These were planned and cut. The architecture supports them: adding an aggregate means a
repository, handlers, a controller and entries in the two error tables — the patterns are
established in ADR 0005 and ADR 0006. They are listed so the boundary is visible as a choice,
not an omission. The only real seam is the outbox: `IDomainEvent` and the event collection on
`AggregateRoot` exist but no dispatcher does, and the first genuine domain event arrives with
the stock ledger in PR 8 — that is where the question gets answered, not here.

| #  | Title |
|----|---|
| 11 | `feat(purchasing)` — supplier, purchase order, state machine |
| 12 | `feat(purchasing)` — amount-threshold approval rules |
| 13 | `feat(purchasing)` — goods receipt to stock movement, partial receipt |
| 14 | `feat(sales)` — customer, sales order, stock reservation |
| 15 | `feat(sales)` — shipment, stock issue, cost of goods sold |
| 16 | `feat(invoicing)` — invoice, VAT rounding, numbering |
| 17 | `feat(soap)` — SOAP endpoint, supplier price feed import |
| 18 | `feat(worker)` — Windows Service, outbox dispatcher, nightly jobs |
| 19 | `feat(reporting)` — T-SQL views, inventory reports, README and diagrams |

## Decisions carried forward

Recorded here because they shape work that has not started yet, and none of them belong to a single
ADR.

- **Repositories:** interfaces in `Envanex.Application`, implementations in
  `Envanex.Infrastructure`. `Envanex.Domain` never mentions storage.
- **Outbox:** deferred to PR 18. `IDomainEvent` and the event collection on `AggregateRoot` exist,
  but there is no dispatcher and no outbox table. Designing one before a single event type exists
  would mean designing it twice.
- **Migrations:** every schema change gets a migration file committed to the repository. The test
  fixture applies migrations rather than creating the schema from the model, so every test run
  exercises the migration chain.
- **Money is not nullable.** Complex types cannot be null in EF Core, so an optional amount has to
  be expressed as `Money.Zero` or split into separate columns. See ADR 0003.
- **Every `Money` property needs an explicit `ComplexProperty` line** in its entity configuration.
  Forgetting it fails silently. See ADR 0003.
- **Grid datasource is a hardened surface.** Max page size, sort/group field allowlist, and a
  default `Take` ship with the endpoint in PR 5b, not later. The read DTO is flat, which makes the
  DTO itself the field allowlist.
- **Auth precedes the public demo.** Authentication and authorization land in PR 6a and 6b, before the
  first deploy in PR 7. The scheme mix — cookie for Blazor, token for the REST and SOAP surfaces —
  is decided in that PR's ADR.
- **Warehouse waits for its consumer.** Warehouse has no application layer or endpoints until
  PR 8, where the stock ledger first needs it. Endpoints with no consumer are surface to secure
  and test for no gain.
- **Concurrency tokens travel through the repository.** `RowVersion` is an EF shadow property
  (ADR 0003), so no aggregate exposes it. Update methods take it as a separate argument and
  Infrastructure sets it as the original value; read projections use `EF.Property` to surface it.
  An aggregate that grows a `RowVersion` field is a defect.
- **The UI stays Blazor Server.** A separate SPA was considered and rejected: it would prove
  frontend skill that is already proven elsewhere, add CORS, browser token storage and a second
  build pipeline, and force the JWT into the browser where a cookie-based Blazor session does
  not need it. Blazor also differentiates in the .NET market this project targets.
- **Identity lives in its own DbContext.** ASP.NET Core Identity brings seven entities. Putting
  them in `EnvanexDbContext` would run `AggregateRootConvention` and `MoneyComplexTypeConvention`
  over them, since both are model-finalizing conventions. A separate `EnvanexIdentityDbContext`
  in its own schema with its own migration chain removes the interaction; auth data never shares
  a transaction with business data.
- **Access tokens are short-lived JWTs; refresh tokens are opaque, hashed, rotated and
  reuse-detected.** A long-lived JWT cannot be revoked. Rotation without reuse detection is half
  a solution — the security value is in catching a stolen token.
- **Authorization is policy-based, not role-based at the endpoint.** Endpoints require `CanRead`
  or `CanWrite`; roles map to policies. Adding a role later must not mean touching every
  controller.

## Known gaps

Tracked deliberately rather than hidden. Each one names where it closes: a PR, or a chore of its own.

| Gap | Closes in                              |
|---|----------------------------------------|
| **BLOCKER — login rate-limit partition key behind a reverse proxy.** `LoginRateLimitPartition.GetKey` reads `Connection.RemoteIpAddress`, which on Azure App Service is the front end's address for every request; all users collapse into one partition and 5 per 5 minutes becomes global — login stops working for everyone after three sign-ins. Closes by configuring `UseForwardedHeaders` with the real `KnownProxies`/`KnownNetworks` and deleting `LoginRateLimitPartitionTests.GetKey_ShouldIgnoreXForwardedForUntilPr7`. **Must be closed before the first public deploy.** The tripwire test is weaker than it looks: it calls `GetKey` directly, so it only catches a fix that changes `GetKey` itself. A fix applied upstream through `UseForwardedHeaders` leaves it green, so PR 7 must delete it by hand rather than wait for a red build to point at it. | PR 7 — **blocker on the deploy** |
| `MoneyComplexTypeConvention` only inspects complex properties one level deep | when a nested case appears             |
| `ResetAsync` in the test fixture deletes tables in a hand-maintained order. PR 6a handled the auth half with a separate `ResetIdentityAsync`; the business half is unchanged | PR 8, when the ledger makes it fragile |
| Login timing side channel, unknown-email half: `FindByEmailAsync` returns null and no password hash is verified, so an unknown address answers measurably faster than a wrong password. Deliberate in PR 6a (Decision 6); `IdentityServiceTimingOrderTests.ValidateCredentialsAsync_UnknownEmail_ShouldNotCallCheckPasswordAsync` asserts the gap, so closing it means inverting a named test | its own chore, when a dummy-hash cost is judged worth paying |
| Login timing side channel, residual write: a wrong password performs one `AccessFailedAsync` `UPDATE` that a locked-out attempt does not — roughly 1 ms against the ~100 ms of PBKDF2 both pay | its own chore, with the entry above |
| Reuse-detection grace period is zero: a client that retries a refresh after a dropped response has its whole family revoked | when a measurement against a real client gives a number to set |
| No `JwtBearerEvents.OnChallenge` body. No endpoint is `[Authorize]` in PR 6a, so no challenge is reachable; the first one would be a bodiless 401 re-executed as the not-found page | PR 6b, with the first `[Authorize]` |
| No refresh-token pruning job. `auth.RefreshTokens` is append-only and grows without bound | **needs an owner** — recorded against PR 18, which sits in the deferred section and will not happen; the active plan ends at PR 10 |
| The 2601/2627 unique-violation path in `RotateAsync` is unreachable by construction and has no test that reaches it. It shares the concurrency failure's branch so that a change to save ordering cannot turn it into an unhandled exception; `IX_RefreshTokens_FamilyId_Live` is kept as a database-level invariant | not scheduled — deliberate |
| Passkeys | not scheduled |
| Warehouse has no application layer or endpoints | PR 8 |
| No authentication or authorization on any endpoint | PR 6b |
| UnitOfMeasure lookup list is unbounded and unordered | PR 7, when seed data makes it visible |
| No index supports datasource sorting on Name, UnitOfMeasureName, ListPriceAmount or IsActive, nor the composite ORDER BY <field>, Code the tiebreaker produces | PR 7, with realistic seed data |
| Datasource cannot sort or filter on ListPriceCurrency or ReorderPoint; value converters block translation | PR 7 |
| Datasource allowlist expects PascalCase selectors while JSON responses serialize camelCase; a real grid sending `code` would get 400 on every sort and filter | PR 7, before the grid is wired |
| `DataSourceGuard` can throw on a sort entry with no selector (`sort=[{"desc":true}]`), producing 500 where the guard intends 400 | PR 7 |
| Grouped paging stability is untested; the group theory never combines with skip/take | PR 7 |
| `DataSourceGuard` has no unit tests; its constructor invariant is unprotected | PR 7 |
| `.gitattributes` declares `* text=auto eol=lf` and CLAUDE.md forbids committing CRLF, but every file at HEAD is CRLF, `.gitattributes` itself included — nothing has ever been renormalized. `dotnet format --verify-no-changes` passes on CRLF files, so the CI check this file credits with enforcing LF does not check line endings at all. The rule is written and unenforced. Pre-existing and repo-wide; not introduced by PR 6a | its own chore — `git add --renormalize .` plus a check that actually fails on CRLF. Deliberately not folded into an auth PR, where it would touch every file and drown the diff |
| README.md still describes the project as "inventory, purchasing and sales" and lists purchase and sales order state machines, SoapCore and a Windows Service in its stack — all of them in the deferred section above. It reads as an unfinished promise where it should state the scope as a finished boundary | PR 7, when the project first becomes publicly visible |
| Dangling PR triggers in the ADRs: ADR 0006 names PR 17 as the trigger for revisiting the error-mapping table, and ADR 0005 and the **Outbox** line under "Decisions carried forward" both point at PR 18. Both PRs sit in the deferred section and will not happen. The ADRs are historical records and are not being rewritten, so the triggers are recorded here rather than left silently waiting | **needs an owner** — nothing carries either trigger once PR 17 and PR 18 are cut |
| `Envanex.SoapApi` and `Envanex.Worker` are empty shell projects. Their scope was cut, but a reader opening the repository sees two empty projects and reads "did not finish" rather than "chose not to build". They cannot simply be deleted: `ArchitectureTests` pins `Envanex.SoapApi` in the Web project's expected references, so removing them means updating that test. Either resolution is acceptable — delete both and update the architecture test, or state in README that the scaffolding is kept deliberately | PR 7, where the project first becomes publicly visible |
| A locked-out account has no recovery path. Login deliberately refuses to tell a user that their account is locked, which is the right security decision and is recorded in ADR 0007. What is missing is not a more informative error but a safe way for the account owner — through a channel they have already proven they control — to learn what happened and regain access | **needs an owner** |
| The manual smoke steps in the PR 6a plan that require a signed-in user were never run: PR 6a deliberately ships no user-creating surface outside the test project. `AuthApiTests` covers the same scenarios end to end against real SQL Server, but those steps exist because the real host started with `dotnet run` can behave differently from the test factory | PR 6b, where the read-only demo account makes them runnable |
| The JWT signing key exists only in local user-secrets. The first deploy needs it in App Service configuration. `AddEnvanexIdentity` runs `JwtOptionsGuard.ThrowIfInvalid` at startup, so a deploy without the key fails loudly at boot rather than generating one silently — which is intended, and is why this is a deployment step rather than a code gap | PR 7 |
| `TimeProvider` is injected in the auth code but does not reach `Envanex.Domain`. PR 6a deliberately kept it at the auth boundary rather than deciding how domain code reads the clock. The stock ledger is the first domain code that needs a notion of now, and that is where the question gets answered | PR 8 |

## How the work is run

Each PR follows the same loop, and the human drives every transition — agents stop and wait.

```
researcher → planner → plan-reviewer → coder (one phase at a time)
           → code-reviewer → db-reviewer (if schema changed) → tester
           → explainer → human writes the ADR → /pr
```

Rules that were learned the hard way and are not negotiable:

- **Commit immediately after each phase completes**, before review. A phase was lost once by not
  doing this.
- **Verdicts are terminal.** When a reviewer returns one, the turn ends. The human routes the next
  step.
- **Reviewers have no shell.** Any claim about whether code compiles, what an API accepts, or what
  git history contains is a guess until a command proves it. Six such claims turned out to be wrong
  during PRs 1–4. Use `/verify`, which prints raw command output and never summarises.
- **Settle disputes by experiment.** When the implementing agent and the reviewing agent disagree
  about a technical necessity, change the code and run the tests. This is how the parameterless
  constructor on `Money` was removed — it was reported as required and was not.
- **The human writes every ADR.** Agents may create the empty file. `docs/journal/` is the
  agent-written study material that feeds it; the reasoning has to be the human's own.
- **Read raw command output, not the agent's summary of it.** The main agent routinely
  summarises `git diff` and test output instead of showing it. For anything longer than a few
  lines, have it write the output to a file and read the file.
- **An unverified claim is not a finding.** Reviewers have no shell; several of their assertions
  turned out to be wrong — `FixedWindowRateLimiter` does supply `RetryAfter` metadata, and a
  closed known gap was reported as still open. Prove it before acting on it.
- **A test that passes is not a test that protects.** Two tests in PR 5b asserted things that
  stayed green when the behaviour they claimed to guard was removed. When a test exists to
  prevent a regression, delete the code it guards and watch it go red.

Agent models: `planner`, `plan-reviewer`, `coder`, `db-reviewer` and `explainer` run on Opus;
`code-reviewer` on Sonnet; `researcher` and `tester` on Haiku.
