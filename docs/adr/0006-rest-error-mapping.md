# 0006 — REST error mapping and concurrency protocol

## Context

PR 5b opens the HTTP surface. Until now a failure was a `Result` object that never left the
process; from here it becomes a status code, a response body and a sentence a user reads. Three
questions had to be answered before any of that could be written, and each of them decides where a
responsibility lives rather than how a line of code looks.

How does a domain error become an HTTP response — by pattern, by category, or by an explicit table?
Who decides which language the user reads, given that the domain must not know it is being read at
all? And how does a grid endpoint that accepts client-supplied filtering, sorting, grouping and
paging stay safe, when the whole point of the DevExtreme protocol is to let the client shape the
query?

A fourth question arrived during implementation. Entity Framework could not translate the
expressions `DataSourceLoader` composes over the read projection, which forced the query into
memory and quietly undid the reason `IQueryable` was chosen in ADR 0005.

## Decision

**Error codes map to status codes through an explicit table of exact strings.** No suffix or prefix
matching. `Product.NotFound` is 404 and `Product.UnitOfMeasureNotFound` is 422; a suffix rule would
collapse them. All thirty-two domain error codes appear in the table by name, including the ones
that map to 400. Three reflection tests hold it closed: every code declared in `Envanex.Domain` must
have a mapping entry and a Turkish message, and every entry in either table must correspond to a
code that still exists. `Error` and `ValidationError` are skipped by declaring type — they are
infrastructure, not catalogue entries.

**Business failures that are not the client's syntax get 422; unmapped failures get 400, never
500.** A request naming an inactive unit of measure is well-formed and parses cleanly — it is
refused by a rule, not by a parser, and 422 says exactly that. An unmapped code falling to 400 is a
statement about what `Result.Failure` means: it is a business outcome. Mapping it to 500 would
raise an alert for something the server handled correctly, and a forgotten table entry is our
omission, not a server fault.

**`ValidationError` becomes the ProblemDetails `errors` dictionary.** Each `ValidationFailure`
contributes its `PropertyName` as the key and the Turkish translation of its `ErrorCode` as the
value. This is the payoff for unsealing `Error` in ADR 0005; without it that change bought nothing.

**Message language is decided in the presentation layer.** Domain messages stay English and are
diagnostic. Validators use `.WithErrorCode(...)` and never `.WithMessage(...)`, so FluentValidation's
default English text never reaches a user. The web layer translates codes through
`TurkishErrorMessages`, falling back to the English `Error.Message` for a single failure and to a
generic Turkish sentence for a field failure — a raw error code is never shown to a user.

**The grid datasource is guarded, and the guard refuses rather than corrects.** `DataSourceGuard`
enforces a default `Take` of 20, a maximum `Take` of 100, a maximum `Skip`, non-negative `Take` and
`Skip`, and rejects `RequireGroupCount` and `GroupSummary`. Sorting, grouping and filtering share
one allowlist, and the filter check walks the nested DevExtreme filter tree rather than inspecting
only its top level. Every violation returns 400. Silently clamping an out-of-range request would
tell the client its request was honoured when it was not, and would hide the shape of the surface
from the person maintaining it.

**A malformed query string is a client error.** The model binder wraps
`DataSourceLoadOptionsParser.Parse` and fails model binding rather than letting the exception
surface, so bad input produces 400 instead of 500. The parser's message is not returned to the
caller.

**A rejected request gets a body.** The rate limiter's `OnRejected` writes a ProblemDetails
document with a Turkish message. An empty 429 would be caught by
`UseStatusCodePagesWithReExecute` and re-executed as the not-found page, and a client would be told
nothing about why it was refused.

**Projections consumed by `DataSourceLoader` are constrained.** A list DTO may not be a positional
record and may not include a shadow property read through `EF.Property`. Either one makes the
`OrderBy` expression `DataSourceLoader` composes untranslatable, and the query falls back to
LINQ-to-Objects over the entire table. `ProductListDto` is therefore an object-initializer type
without `RowVersion`, and `DataSourceListDtos_ShouldNotBePositionalRecords` enforces the first half
of the rule by scanning source files.

## Alternatives

**An `ErrorCategory` on `Error`.** Each error would declare whether it is a not-found, a conflict
or a validation failure, and each consumer would translate the category into its own protocol.
Rejected because the choice of status code is a presentation decision and this would move it into
the domain, and because two errors in the same category legitimately need different codes — the
`NotFound` suffix already proves it.

**Suffix or prefix matching on error codes.** Far less to write and it looked correct on the first
nine codes. Rejected because it is ambiguous by construction, and the ambiguity is silent: nothing
fails, a request simply gets the wrong status.

**Clamping out-of-range datasource options.** More forgiving, and the request still returns data.
Rejected for the same reason the mapping is explicit: a wrong answer that looks right is worse than
a refusal.

**Returning `IReadOnlyList` from the product read repository.** It would have avoided the
translation problem entirely, since nothing composable escapes the infrastructure. Rejected because
the grid would then page and sort in memory over every row in the table — the failure mode this
whole decision exists to prevent.

**Turkish messages in the domain.** One table fewer and no synchronisation to keep. Rejected
because it binds the domain to a language, and PR 6 and PR 7 will present the same errors through
SOAP and Blazor.

## Consequences

**Two tables must be updated by hand for every new error code.** A code needs an entry in the
status map and one in the Turkish messages. The reflection tests turn that burden into a failing
test rather than a silent gap, which is the only reason the burden is acceptable — but it is still a
burden, and it grows with the domain.

**The trigger for revisiting the table is a second consumer, not a row count.** PR 17 adds a SOAP
surface that will map the same errors into its own faults, and a table living in the web layer will
be useless to it. That is the moment to weigh an `ErrorCategory` on `Error` again, with each
consumer translating the category — not when the table merely gets long.

**The list-DTO constraint will be met again.** PR 8 adds a stock grid and will need its own list
DTO. The architecture test catches a positional record, but nothing catches a shadow property added
to a projection — that failure appears as a silent fall back to in-memory evaluation, and only the
regression test that calls `DataSourceLoader` directly on the queryable would notice.

**The guard's allowlist is per-controller and hand-written.** `ProductsController` holds its own
static instance. A second grid means a second allowlist, and nothing checks that an allowlist
matches the DTO it guards. A field renamed on the DTO and not in the allowlist becomes a 400 on a
request that should have worked.

**`required init` on list DTOs is a constraint, not a preference.** A future projection that would
be natural to express as a positional record cannot be, and the reason lives in a comment and a
test rather than in the type system.

**`Retry-After` is proven, not assumed.** The `OnRejected` callback reads
`MetadataName.RetryAfter` from the lease and sets the header when present, and an experiment now
settles what was previously only asserted: `FixedWindowRateLimiter` does supply that metadata on a
failed lease, so the block is live code and stays. The proof is
`MutationEndpoint_ExceedingRateLimit_ShouldIncludeRetryAfterHeader` in
`tests/Envanex.IntegrationTests/Api/RateLimiterTests.cs`, which asserts that a rejected request
carries `Retry-After` with a positive delta-seconds value. That test is also the guard: swapping the
global limiter for one that does not populate the metadata — a concurrency or token-bucket
partition, say — turns a silently missing header into a failing test.
