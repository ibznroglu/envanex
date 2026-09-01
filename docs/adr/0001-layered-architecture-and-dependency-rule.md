# 0001 — Layered architecture and dependency rule

## Context

Envanex is an ERP core: stock ledger, purchasing, sales and invoicing. The business rules in
this domain — how a stock movement is recorded, how moving-average cost is recalculated, when a
purchase order may be approved — change slowly and are the part of the system that has to stay
correct. The technical surface around them changes much faster: the database, the UI framework,
the API shape, the integration endpoints.

Putting both in one project would have meant tying things that change at very different rates
to the same file tree, with nothing preventing one from reaching into the other.

## Decision

The solution is split into six projects, and dependencies point in one direction only: inward,
toward the domain.

- `Envanex.Domain` holds the business rules and references nothing at all.
- `Envanex.Application` holds use cases and references only Domain.
- `Envanex.Infrastructure` owns everything that talks to the outside world — EF Core, SQL Server,
  the outbox — and references Domain and Application.
- `Envanex.SoapApi`, `Envanex.Web` and `Envanex.Worker` sit at the edge.

The rule that matters most is that Domain knows nothing about how data is stored or delivered.
Because it depends on nothing, nothing can force it to change. That is also what makes the
business rules testable in isolation: verifying a costing calculation needs no SQL Server, no
web host and no fixtures, so those tests run in milliseconds. A secondary benefit is that the
storage technology can be replaced without the business rules following it.

This rule is enforced by four tests in `ArchitectureTests`, which read the `.csproj` files
directly and assert which project references each layer is allowed to declare.

## Alternatives

**A single project.** This would have been faster in the short term. Adding a screen or an
endpoint would mean touching one place instead of three or four, with no DTO mapping between
layers and less code overall. For a small application with simple rules, that is the better
trade — layering a CRUD app is overhead with no return. It was rejected because an ERP's rules
get more tangled as the system grows, and by the time the cost of a single project becomes
visible, untangling it is expensive.

**Writing the rule in `CLAUDE.md` and relying on discipline.** A written rule is a wish, not a
constraint. Nothing stops someone from adding a reference to Domain, and nobody notices until
the damage is done. Encoding the rule as a test turns it from a human obligation into a
mechanism: a violating reference makes `dotnet test` fail, and CI blocks the merge. The rule
was deliberately not left as documentation alone.

Note that the enforcement happens at test time, not compile time — the compiler is perfectly
happy with a reference from Domain to Infrastructure. `ArchitectureTests` is what catches it.

## Consequences

**What it costs.** Six projects mean longer build times and a larger file tree. A simple feature
requires opening files in three or four projects and mapping data between them. Development is
measurably slower per feature than it would be in a single project.

**What it buys.** As the system grows, the code stays testable, the boundaries stay legible, and
a change in one layer does not silently break another. The dependency rule is checked
automatically rather than remembered.

**Where this may hurt later.** If the requirements turn out to be simpler than expected, or keep
shifting before they settle, this structure becomes friction. Basic CRUD work still pays the
full cost of crossing layers, and refactoring means touching six projects instead of one. The
honest rule of thumb: layered architecture for a large or complex system, a single project for
a small one. This decision assumes Envanex is the former — if that assumption turns out to be
wrong, the structure is the first thing to reconsider.
