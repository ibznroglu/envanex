# Research: Authentication infrastructure (PR 6a)

## Question

What does the existing codebase require of an authentication layer built on ASP.NET Core Identity
with short-lived JWT access tokens and opaque, rotated, reuse-detected refresh tokens? Which
existing patterns must it follow, which existing guards will or will not protect it, and what does
the current external tooling actually offer?

## Scope

In scope for PR 6a: ASP.NET Core Identity in its own `DbContext`, JWT access token issuance,
opaque refresh tokens with rotation and reuse detection, and the `login` / `refresh` / `logout`
endpoints.

Out of scope, deferred to PR 6b: authorization policies, `[Authorize]` on endpoints, the Blazor
cookie scheme, and the read-only demo account. Endpoints remain open after 6a. This is a
deliberate split, not an omission.

## Baseline measurement

Taken on `feat/auth` at `06fbba9`, before any auth code exists. Recorded so that the cost of this
PR and of PR 8 can be compared against something real rather than estimated.

| Suite | Tests | Duration |
|---|---|---|
| `Envanex.Domain.Tests` | 113 | 166 ms |
| `Envanex.Application.Tests` | 79 | 135 ms |
| `Envanex.IntegrationTests` | 107 | 24 s |

The 24 s includes Testcontainers pulling up and migrating a single SQL Server container that every
integration test class shares. There is currently no test-duration problem to solve.

## Relevant files

**Web host**
- `src/Envanex.Web/Program.cs` — the whole pipeline; no authentication or authorization today
- `src/Envanex.Web/Middleware/SecurityHeadersMiddleware.cs` — first middleware in the pipeline
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — `Result` to `IActionResult`, the status map
- `src/Envanex.Web/Extensions/TurkishErrorMessages.cs` — error code to Turkish message
- `src/Envanex.Web/Controllers/ProductsController.cs` — the controller shape to copy

**Infrastructure**
- `src/Envanex.Infrastructure/DependencyInjection.cs` — `AddInfrastructure`, connection string guard
- `src/Envanex.Infrastructure/Persistence/EnvanexDbContext.cs` — DbSets, conventions, converters
- `src/Envanex.Infrastructure/Persistence/EnvanexDbContextFactory.cs` — design-time factory
- `src/Envanex.Infrastructure/Persistence/UnitOfWork.cs` — EF exception translation
- `src/Envanex.Infrastructure/Migrations/` — one migration, `20260904115921_InitialSchema`

**Application**
- `src/Envanex.Application/Abstractions/Messaging/ICommandHandler.cs`, `IQueryHandler.cs`
- `src/Envanex.Application/Behaviors/ValidationDecorator.cs`
- `src/Envanex.Application/DependencyInjection.cs` — every registration written by hand

**Tests that will constrain this PR**
- `tests/Envanex.Domain.Tests/ArchitectureTests.cs` — project reference and EF package rules
- `tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs` — four reflection facts over the tables
- `tests/Envanex.IntegrationTests/DependencyInjectionTests.cs` — resolution and decorator wrapping
- `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs` — migrations and reset
- `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs` — settings override
- `tests/Envanex.IntegrationTests/Api/ProductsApiTests.cs` — the seeding pattern

## Current behavior

### 1. Web host pipeline

`Program.cs` builds the pipeline in this order:

1. `UseExceptionHandler("/Error")` and `UseHsts()` — non-Development only
2. `MapOpenApi()`, `MapScalarApiReference()` — Development only
3. `UseMiddleware<SecurityHeadersMiddleware>()`
4. `UseRateLimiter()` — only when `RateLimiting:Enabled`
5. `UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true)`
6. `UseHttpsRedirection()`
7. `UseAntiforgery()`
8. `MapStaticAssets()`, `MapControllers()`, `MapRazorComponents<App>()`

`UseAuthentication()` and `UseAuthorization()` are absent. Where they are inserted is a decision
with consequences, not a formality: placing them before `UseStatusCodePagesWithReExecute` means a
401 gets re-executed as the not-found page, which is the same failure the rate limiter's
`OnRejected` body exists to avoid (ADR 0006).

The rate limiter is a single global fixed-window partition, `PermitLimit` 100 over 60 seconds,
configured from `RateLimiting:*`. `OnRejected` writes a ProblemDetails body in Turkish and sets
`Retry-After` from `MetadataName.RetryAfter`.

`public partial class Program { }` at the end of the file exists so `WebApplicationFactory<Program>`
can reach it.

### 2. Infrastructure persistence wiring

`AddInfrastructure(services, configuration)`:
- reads `ConnectionStrings:EnvanexDb`
- throws `InvalidOperationException` with a user-secrets hint when it is missing or blank
- `AddDbContext<EnvanexDbContext>` with `UseSqlServer`
- registers four repositories and `IUnitOfWork`, all scoped

`EnvanexDbContext` exposes `UnitOfMeasures`, `Warehouses`, `Products`; applies configurations from
its own assembly; and in `ConfigureConventions` registers the `Quantity` converter (18,6), the
`Currency` converter (max length 3) and both custom conventions.

`EnvanexDbContextFactory` implements `IDesignTimeDbContextFactory<EnvanexDbContext>` and reads
`ENVANEX_CONNECTION_STRING` from the environment, throwing with an example when unset.

A second `DbContext` therefore needs: its own context type, its own `AddDbContext` call, its own
design-time factory, its own migrations output directory, and its own convention registration. It
inherits nothing from the first.

### 3. Model-finalizing conventions

Both are registered per context inside `EnvanexDbContext.ConfigureConventions`, so a second context
does not inherit them. This is what makes the roadmap's "Identity lives in its own DbContext"
decision work mechanically rather than only in principle.

- `AggregateRootConvention` walks every entity type, detects CLR inheritance from `AggregateRoot<>`
  and adds a shadow `byte[] RowVersion` concurrency token generated `OnAddOrUpdate`.
- `MoneyComplexTypeConvention` walks complex properties whose CLR type is `Money` and sets the
  amount precision to (18,4). It inspects one level only.

Identity's entities inherit from neither `AggregateRoot<>` nor carry `Money`, so the practical risk
of sharing a context is lower than it looks — but the conventions are model-finalizing and run over
every type in the model, so the separation removes a class of interaction rather than a specific
known bug.

### 4. Error surface and its reflection guards

`Error` is a record of `(Code, Message)` with an `Error.None` sentinel. `ValidationError` derives
from it, hardcodes the code `Validation.Failed` in its constructor, and carries
`IReadOnlyList<ValidationFailure>`, where `ValidationFailure(PropertyName, ErrorCode)` deliberately
has no message.

`ResultExtensions` holds `StatusCodeMap`, a `FrozenDictionary<string,int>` with **33 entries**, and
exposes `MappedErrorCodes` (line 98). An unmapped code falls back to 400, never 500 (line 143).
`GenericValidationFallback` is `"Bu alan geçersiz."` (line 14).

`TurkishErrorMessages` holds a `FrozenDictionary<string,string>` with **33 entries**, exposes
`TranslatedErrorCodes` (line 62) and `GetMessage(errorCode, fallbackMessage)` (line 55).

Counted directly: `Envanex.Domain` declares 33 `public static readonly Error` fields. `Error.None`
is excluded by declaring type, leaving 32 discoverable codes, plus `Validation.Failed` — exactly
the 33 entries in each table.

`ResultMappingTests` contains **four** `[Fact]`s. Its scan reaches
`typeof(Error).Assembly` only — that is, `Envanex.Domain`. An error code declared in any other
assembly is invisible to all four facts. **This is the single most important constraint on where
`AuthErrors` may live.**

### 5. Application layer patterns

Both handler interfaces return `Task<Result<TResponse>> HandleAsync(T request, CancellationToken ct = default)`.

`ValidationDecorator<TCommand,TResponse>` takes the inner handler and `IEnumerable<IValidator<TCommand>>`,
collects failures into `ValidationFailure(PropertyName, ErrorCode)` and returns
`Result.Failure<TResponse>(new ValidationError([.. failures]))`, otherwise delegating.

`AddApplication` is hand-written throughout: validators as singletons (lines 23–27), concrete
handlers as scoped (30–34), the `ICommandHandler<,>` interface registered through a factory lambda
that wraps the concrete handler in the decorator (37–60), and query handlers registered directly
and undecorated (63–66).

A new command handler must therefore: implement the interface, ship at least one
`IValidator<TCommand>`, be registered twice, and be added to `CommandHandlerTypes()` in
`DependencyInjectionTests` — otherwise `CommandHandlers_ShouldBeWrappedWithValidationDecorator`
does not cover it and an unvalidated handler passes silently.

### 6. Integration test harness

`SqlServerFixture.InitializeAsync`: start the container, `CreateDbContext()`, `MigrateAsync()`,
then construct `EnvanexWebApplicationFactory(ConnectionString)`. **One context, one migrate call.**
A second context whose migrations are never applied produces `Invalid object name 'AspNetUsers'`
on the first auth test.

`ResetAsync` issues four hand-written `DELETE FROM` statements in hand-maintained FK order
(`Products`, `UnitOfMeasures` with `BaseUnitId IS NOT NULL`, then `IS NULL`, `Warehouses`). The
roadmap already tracks this as a gap closing in PR 8.

`DatabaseCollection` is a single `ICollectionFixture<SqlServerFixture>`, so every integration test
class shares one container and one factory and runs serially.

`EnvanexWebApplicationFactory` overrides configuration purely through `builder.UseSetting(...)`
under `UseEnvironment("Testing")`, and disables rate limiting.
`RateLimitedWebApplicationFactory` is a separate factory used only by `RateLimiterTests`, with
`PermitLimit` 2.

Seeding today (`ProductsApiTests`) builds an aggregate through its domain factory and writes it
with `_fixture.CreateDbContext()`. **There is no equivalent path for a user:** writing an Identity
user through a raw `DbContext` bypasses password hashing and the normalized columns, so the seeded
user would not be the thing the login endpoint reads.

### 7. Time

A search of `src/` for `TimeProvider`, `ISystemClock`, `IClock`, `DateTime.` and `DateTimeOffset.`
(excluding generated migration files) returns **nothing at all**. In `tests/`, only
`AggregateRootTests.cs` matches.

PR 6a is the first code in this system that has a notion of *now*. Access token expiry, refresh
token expiry, the rotation window and reuse detection all depend on it, and all four need to be
testable without sleeping.

### 8. Configuration and secrets

`appsettings.json` carries `ConnectionStrings:EnvanexDb` (empty), `RateLimiting:{Enabled,PermitLimit,WindowSeconds}`,
`Logging` and `AllowedHosts`. `appsettings.Development.json` carries only `Logging`.

`Envanex.Web.csproj` already declares `<UserSecretsId>envanex-web-secrets</UserSecretsId>`.

The established failure mode for missing configuration is a thrown `InvalidOperationException`
naming the key and the user-secrets command. A JWT signing key must follow the same pattern. A
silently generated or defaulted signing key would leave every test green while making tokens
unverifiable across restarts and forgeable if the default ever shipped.

### 9. Project files and package placement

`Directory.Build.props` applies to every project: `net10.0`, `Nullable enable`, `ImplicitUsings enable`,
**`TreatWarningsAsErrors true`**, `EnforceCodeStyleInBuild true`, `AnalysisLevel latest-recommended`,
`GenerateDocumentationFile true` with `CS1591` suppressed.

`InternalsVisibleTo` exists once: `Envanex.Infrastructure` to `Envanex.IntegrationTests`.

Current package placement: Application has FluentValidation and DI.Abstractions only;
Infrastructure has `Microsoft.EntityFrameworkCore.Design` (PrivateAssets all) and
`Microsoft.EntityFrameworkCore.SqlServer`; Web has DevExtreme, OpenApi, Radzen, Scalar and EF Design.

**Verified gap in the architecture test.** `Application_ShouldNotReference_EntityFrameworkPackages`
matches the regex `Microsoft\.EntityFrameworkCore` against `PackageReference` includes. Tested
against real package names:

```
MATCH     Microsoft.EntityFrameworkCore.SqlServer
MATCH     Microsoft.EntityFrameworkCore.Design
NO MATCH  Microsoft.AspNetCore.Identity.EntityFrameworkCore
NO MATCH  Microsoft.AspNetCore.Authentication.JwtBearer
```

Neither auth package would be caught if added to `Envanex.Application`. The guard ADR 0005 calls
load-bearing does not bear this particular load.

### 10. Code style in use

- File-scoped namespaces everywhere.
- Infrastructure implementation types are `internal sealed` (`UnitOfWork.cs:8`, `internal sealed partial class UnitOfWork`).
- Public entry points open with `ArgumentNullException.ThrowIfNull` (`DependencyInjection.cs:16-17`,
  `EnvanexWebApplicationFactory.cs:22`).
- Constant lookup tables are `FrozenDictionary` exposed through a read-only set.
- `[GeneratedRegex]` with a partial method (`UnitOfWork.cs:52`).
- Test naming is `Method_Condition_ShouldExpectation`.

## External findings

### Package versions

`Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 is published. The
`Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.x line is at 10.0.12, so 10.0.11 exists.

Both must be pinned at **10.0.11** in `Directory.Packages.props` to match every other ASP.NET Core
and EF Core package in the solution. Splitting the family risks NU1605 downgrade warnings, which
`TreatWarningsAsErrors` turns into build failures.

If `FakeTimeProvider` is adopted, `Microsoft.Extensions.TimeProvider.Testing` is an additional
central package version.

### ASP.NET Core Identity in .NET 10

Identity gained built-in metrics under the `Microsoft.AspNetCore.Identity` meter: user creation,
update and delete durations, password check attempts, generated and verified tokens, sign-ins and
sign-outs. These require no code and are worth surfacing in the ADR as an argument for using
Identity rather than hand-rolling user storage.

Identity also gained built-in passkey (WebAuthn/FIDO2) support, treated as a primary authentication
factor rather than a second factor, and without attestation validation by default. Out of scope for
6a; worth one line in the roadmap's deferred table so the omission reads as a choice.

### Refresh token rotation and reuse detection

RFC 9700 §4.14.2 requires that a replayed refresh token revoke the whole grant, not just the token
presented. Two implementation traps are documented from real incidents and both fail silently:

**Deleting the predecessor row on rotation destroys detection after one generation.** In a chain
r1 → r2 → r3, if rotation deletes the row it consumed, replaying r1 matches nothing while r3 stays
live. The fix is to keep the row and stamp it (`RotatedAt`), so a replayed token of any generation
still resolves to its family and the whole family can be expired at once. Lookup of a usable token
then requires `RotatedAt IS NULL`.

**Rotation racing revocation.** A rotation that has already read its successor can still write a
child after a concurrent revocation, and the child outlives the revocation. The family root row
acts as the lock. This is the same optimistic-concurrency concern ADR 0003 and ADR 0005 already
handle for aggregates, applied to a new table.

**Grace periods exist because replay and network retry look identical.** Implementations that
enable reuse protection commonly treat a replay as an attack only after a grace window, otherwise
a retried request on a flaky connection logs the legitimate user out.

### Two DbContexts and the EF tooling

With two `IDesignTimeDbContextFactory` implementations in one assembly, `dotnet ef` cannot choose
and requires `--context`; migrations for the second context need their own `--output-dir`.

**Both contexts default to the same `__EFMigrationsHistory` table in the same database.** Left
alone, each context reads the other's applied-migration rows as its own history, and the failure is
silent until a migration is skipped or re-applied. The second context must therefore declare its own
history table, for example
`UseSqlServer(cs, b => b.MigrationsHistoryTable("__EFMigrationsHistory", "auth"))`. This is the
strongest argument for giving the Identity context its own schema rather than sharing `dbo`.

**Not verified by experiment.** The first phase's validation step must actually run the migration
command and paste its raw output, per the project's rule that an unverified claim is not a finding.

## Proposals from the researcher — NOT reviewed by the human

Written by the researcher agent. The human had not seen this file when these were written and has
approved none of them. They are input to planning, not constraints on it: every item below is an
open design decision that remains the human's to make.

1. **`AuthErrors` lives in `Envanex.Application`, and `ResultMappingTests` is widened to scan both
   `Envanex.Domain` and `Envanex.Application`.** Domain has no auth aggregate and will not get one;
   but leaving the codes outside every reflection guard would make the mapping tables unprotected
   for exactly the errors a user is most likely to see. Widening the scan preserves the guarantee
   instead of abandoning it.

2. **Identity is consumed through an `IIdentityService` abstraction declared in `Envanex.Application`
   and implemented in `Envanex.Infrastructure`.** `UserManager` and `SignInManager` never appear in
   Application. Login, refresh and logout are ordinary `ICommandHandler` implementations with
   validators, decorator registration and DI test entries like every other command. The alternative
   — a service called straight from a controller — is faster but makes auth the one use case that
   skips the layer ADR 0005 claims every future aggregate will copy.

3. **Refresh tokens live in the Identity `DbContext`.** They need a foreign key to `AspNetUsers`,
   which is not expressible across contexts. This also keeps auth data out of any transaction that
   touches business data.

4. **Time comes from `TimeProvider`**, registered as `TimeProvider.System` and faked in tests with
   `FakeTimeProvider`. No hand-written `IClock`. `TimeProvider` does not enter `Envanex.Domain` in
   this PR; the stock ledger in PR 8 is where that question gets asked.

5. **The Identity context gets its own reset path in the test fixture** rather than eight more lines
   appended to `ResetAsync`'s hand-maintained list. The roadmap's gap entry is updated to say 6a
   handled the auth half and PR 8 still owns the full fix.

6. **Test users are created through `UserManager` resolved from the factory's service scope**, not
   written through a raw `DbContext`.

7. **The reuse-detection grace period is zero in this PR**, recorded in the ADR with its reasoning
   and entered in the known gaps table. A non-zero window should be set from a measurement against
   a real client, not guessed now.

## Risks and unknowns

- Placement of `UseAuthentication`/`UseAuthorization` relative to `UseStatusCodePagesWithReExecute`
  can turn a 401 into the not-found page. Needs an explicit test asserting the status code and the
  response body of an unauthenticated call, not just that it is not 200.
- The login endpoint needs a stricter rate limit than the global 100/60s, but
  `RateLimiting:Enabled` is `false` in the shared test factory, so a login limiter attached to the
  global switch would be untested. It needs its own switch or its own factory, following the
  `RateLimitedWebApplicationFactory` precedent.
- `TreatWarningsAsErrors` plus `AnalysisLevel latest-recommended` means any obsolete Identity API
  or nullable mismatch fails the build rather than warning. Expect this in the first coder phase.
- Opaque refresh tokens must be stored hashed. The hash must be a fixed-cost one suitable for
  high-entropy secrets, not a password hash, and the comparison must be constant-time. The
  algorithm choice belongs in the ADR.
- `Envanex.SoapApi` and `Envanex.Worker` are empty shells whose references are pinned by
  `ArchitectureTests`. Unrelated to 6a, but they become visible to readers at the PR 7 deploy and
  need a decision before then.

## Open questions for the human

1. Does `logout` revoke only the presented refresh token's family, or every family for that user?
2. Is the refresh token returned in the response body or set as an `HttpOnly` cookie? PR 6b adds a
   Blazor cookie scheme, so this choice constrains that PR.
3. Does the Identity context get its own SQL schema (for example `auth`), or does it share `dbo`
   with a table prefix?
4. Does 6a add any endpoint that creates a user, or do users exist only through test seeding until
   the demo account arrives in 6b?
