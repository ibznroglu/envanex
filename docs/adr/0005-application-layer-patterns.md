# 0005 — Application layer patterns

## Context

Until this PR the domain could construct a `Product` but nothing could act on one. There was no
way to express "create this product" as a request, validate it, decide what happens when the code
is already taken, and commit the result. PR 5a builds that layer, and the shape it takes will be
copied by every aggregate that follows — purchasing, sales, invoicing.

Three questions had to be settled before writing any of it. How a request reaches the code that
handles it, and where cross-cutting concerns like validation attach. Where the boundary between the
application and the database sits, given that the domain must stay ignorant of storage while the
application still needs transactions and query composition. And how a failure travels from the
database back to the caller without dragging Entity Framework into a layer that must not reference
it.

A fourth question surfaced during implementation rather than planning. `Error` was declared
`sealed` in PR 2 and carries a single code and message. A form submission can fail on several
fields at once, and telling a user "validation failed" without saying which field is not an answer.

## Decision

**Commands and queries are handled by hand-written interfaces, not a mediator library.**
`ICommandHandler<TCommand, TResponse>` and `IQueryHandler<TQuery, TResponse>` both return
`Task<Result<TResponse>>`. Validation attaches through the decorator pattern:
`ValidationDecorator<TCommand, TResponse>` is itself an `ICommandHandler`, wraps the real one, runs
the validators and delegates only if they pass. Every registration is written out by hand in
`AddApplication()`, so the decorator chain is readable in one file rather than inferred from
convention.

Hand registration trades automation for visibility, and the trade is only safe because two tests
make the missing-registration failure loud. `AllRegisteredServices_ShouldResolve` resolves every
handler and repository from a real container; `CommandHandlers_ShouldBeWrappedWithValidationDecorator`
asserts that what comes back is the decorator and not the bare handler. An unwrapped handler would
skip validation silently and every other test would still pass.

**Query handlers are not decorated.** Queries produce no side effects, so a validation pass buys
nothing. With a mediator this exclusion would be a type check inside a pipeline behaviour; here it
is the absence of a line in the DI file.

**Repository interfaces live in the application layer; `IUnitOfWork` owns the transaction
boundary.** No repository calls `SaveChanges`. A use case may touch several repositories and all of
it must commit or roll back together — the boundary belongs to the use case, not to the storage
detail underneath it. This also leaves room for the outbox in PR 18, which needs to write its rows
inside the same transaction as the aggregate that produced the event.

**The read side is separate and deliberately thin.** `IProductRepository` returns entities because
callers invoke aggregate methods on them; `IProductReadRepository` returns DTOs because callers only
display them. `GetAll()` returns `IQueryable<ProductListDto>` so the grid endpoint in PR 5b can
compose filtering, sorting and paging into SQL rather than loading every row into memory.
`IUnitOfMeasureReadRepository` returns a materialised list instead, because a lookup table has no
query surface worth composing.

**EF Core exceptions are translated in `UnitOfWork`, not in handlers.** `DbUpdateConcurrencyException`
becomes `ConcurrencyConflictException` and a unique-index violation — `SqlException` number 2601 or
2627 — becomes `DuplicateKeyException`. Both target types are defined in the application layer, so
handlers catch types they own and never name an EF type. A foreign-key violation is deliberately
not translated. The handler already checks that the referenced unit of measure exists and is active
before saving, so a 547 reaching `SaveChanges` means that check was bypassed. That is a defect, and
a defect should surface as one rather than be dressed up as a business error.

**`Error` is no longer sealed, and `ValidationError` derives from it.** It carries a list of
`ValidationFailure(PropertyName, ErrorCode)` so a single `Result.Failure` can report every failed
field. Working around this — parsing a composite code, or hanging a dictionary off `Result` — would
have kept the shared kernel untouched at the cost of a weaker type and a convention nobody can
enforce. Widening the kernel early, while it has one consumer, is cheaper than retrofitting it once
purchasing and sales depend on the current shape.

**Validators carry error codes, not messages.** Rules use `.WithErrorCode("Product.CodeRequired")`
and never `.WithMessage(...)`. Domain messages stay in English and are diagnostic; the Turkish text
a user reads is produced in the web layer from the code. The domain does not know which language it
is being read in, just as it does not know which HTTP status code it maps to.

**Length limits live on the aggregate and are referenced, not repeated.** `Product.CodeMaxLength`
is checked in the factory and passed to `MaximumLength(...)` in the validator, so the column, the
invariant and the input rule cannot drift apart. This closes the gap recorded in ADR 0004.

## Alternatives

**MediatR.** One registration line replaces every hand-written entry, and pipeline behaviours are a
well-understood way to attach validation. Rejected on three grounds. It became commercially
licensed in 2025, which turns a convenience into a dependency with terms attached. Assembly
scanning hides which behaviour applies to which handler, so the answer to "is this command
validated?" moves out of the code and into the configuration. And the scan cannot be tested the way
explicit registration can — there is no equivalent of asserting that a specific handler came back
wrapped.

**Scrutor for assembly-scanned registration.** Keeps the hand-written interfaces but removes the
repetition. Rejected for now because it reintroduces exactly the opacity that made MediatR
unattractive, in exchange for saving five registrations. The calculation changes with volume, and
the threshold is named under Consequences.

**Returning `IReadOnlyList<ProductListDto>` from the read repository.** A cleaner boundary — nothing
composable escapes the infrastructure. Rejected because the grid endpoint would then filter and
sort in memory over every product in the table.

**Translating exceptions inside each handler.** Fewer layers, and the handler decides its own
mapping. Rejected because the handler would have to catch `DbUpdateException`, which means the
application project references Entity Framework, which the architecture test forbids. The
translation has to happen where EF is already visible.

**Leaving `Error` sealed and encoding field failures in the code string.** Something like
`Validation.Name.Required`, parsed by the web layer. Rejected because a parsed string is a
convention with no compiler behind it, and because a single `Result` still could not carry more than
one failure.

## Consequences

**`AddApplication()` grows linearly with the number of use cases.** Five commands produce roughly
twenty lines today. At thirty or forty commands this file becomes a chore to read and an easy place
to forget an entry — the DI tests catch the forgetting, but not the noise. The threshold for
revisiting this is when the registration file stops being scannable in one screen, or when a new
cross-cutting decorator has to be added to every command by hand. At that point Scrutor with an
explicit convention, or a small registration helper, should be weighed again.

**`IQueryable` in the application layer is a boundary held by discipline, not by the compiler.**
`IQueryable` itself is `System.Linq` and carries no EF dependency, so nothing leaks today. The leak
appears the moment someone calls `Include`, `ThenInclude` or `EF.Functions` on it — and the only
thing preventing that is `Application_ShouldNotReference_EntityFrameworkPackages`, which fails on
the package reference such a call would require. That test is load-bearing. If it is ever weakened,
this boundary is gone and nothing will announce it.

**An open `Error` hierarchy can drift into silent logic errors.** `ValidationError` is the only
subtype today and the web layer branches on it with a pattern match. If more subtypes appear and
consumers switch on them, a missing branch falls through to a default rather than failing to
compile. Nothing currently limits the hierarchy. If a second subtype is ever added, that is the
moment to decide whether the set should be closed and exhaustively matched.

**Integration tests can see inside the infrastructure.** `InternalsVisibleTo` was added so tests can
construct the internal repositories and `UnitOfWork` directly. This is what makes the concurrency
trap testable at all — the `SetOriginalRowVersion` behaviour only exists against a real change
tracker. The cost is that those tests are coupled to implementation types, so a refactor inside the
infrastructure breaks tests that are nominally about behaviour.

**Validators are singletons while handlers are scoped.** Correct while validators are stateless. A
validator that needs a repository — an async uniqueness rule, for instance — would capture a scoped
dependency in a singleton and quietly reuse a disposed context. Any validator that grows a
dependency must be re-registered as scoped, and nothing enforces that today.

**The concurrency token travels a long path.** `RowVersion` goes from the read projection into the
DTO, back in through a command, into the repository and finally onto the EF entry as
`OriginalValue`. Every hop is a place to drop it, and dropping it disables optimistic concurrency
without failing anything — the update simply succeeds. Three unit tests assert the call happens
before `SaveChanges` and one integration test documents the silent success when it does not. Those
tests are the only thing standing between this design and a concurrency check that looks present and
does nothing.
