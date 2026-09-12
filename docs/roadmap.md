# Roadmap

Envanex is built in a fixed sequence of pull requests, each one small enough to review in a sitting
and each one leaving `main` green. This file records where the project is going, what has been
decided along the way, and how the work is run. Architectural reasoning lives in
[`docs/adr/`](adr/); this file is the map.

## Scope

An ERP core covering inventory, purchasing and sales/invoicing — the parts of an ERP where
correctness is not negotiable. Not a whole ERP: no HR, no accounting ledger, no manufacturing.

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
| 5b | `feat(api)` — REST endpoints, hardened grid datasource, integration tests | |
| 6 | `feat(auth)` — authentication and authorization: cookie for Blazor, token for REST and SOAP; read-only demo account | |
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

### Purchasing

| #  | Title | Status |
|----|---|---|
| 11 | `feat(purchasing)` — supplier, purchase order, state machine | |
| 12 | `feat(purchasing)` — amount-threshold approval rules | |
| 13 | `feat(purchasing)` — goods receipt to stock movement, partial receipt | |

### Sales and invoicing

| #  | Title | Status |
|----|---|---|
| 14 | `feat(sales)` — customer, sales order, stock reservation | |
| 15 | `feat(sales)` — shipment, stock issue, cost of goods sold | |
| 16 | `feat(invoicing)` — invoice, VAT rounding, numbering | |

### Integration and operations

| #  | Title | Status |
|----|---|---|
| 17 | `feat(soap)` — SOAP endpoint, supplier price feed import | |
| 18 | `feat(worker)` — Windows Service, outbox dispatcher, nightly jobs | |
| 19 | `feat(reporting)` — T-SQL views, inventory reports, README and diagrams | |

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
- **Auth precedes the public demo.** Authentication and authorization land in PR 6, before the
  first deploy in PR 7. The scheme mix — cookie for Blazor, token for the REST and SOAP surfaces —
  is decided in that PR's ADR.
- **Warehouse waits for its consumer.** Warehouse has no application layer or endpoints until
  PR 8, where the stock ledger first needs it. Endpoints with no consumer are surface to secure
  and test for no gain.
- **Concurrency tokens travel through the repository.** `RowVersion` is an EF shadow property
  (ADR 0003), so no aggregate exposes it. Update methods take it as a separate argument and
  Infrastructure sets it as the original value; read projections use `EF.Property` to surface it.
  An aggregate that grows a `RowVersion` field is a defect.

## Known gaps

Tracked deliberately rather than hidden. Each one has a PR where it closes.

| Gap | Closes in                              |
|---|----------------------------------------|
| `MoneyComplexTypeConvention` only inspects complex properties one level deep | when a nested case appears             |
| `ResetAsync` in the test fixture deletes tables in a hand-maintained order | PR 8, when the ledger makes it fragile |
| Warehouse has no application layer or endpoints | PR 8 |
| No authentication or authorization on any endpoint | PR 6 |

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

Agent models: `planner`, `plan-reviewer`, `coder`, `db-reviewer` and `explainer` run on Opus;
`code-reviewer` on Sonnet; `researcher` and `tester` on Haiku.
