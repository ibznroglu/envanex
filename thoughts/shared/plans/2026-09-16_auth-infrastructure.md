# Plan: PR 6a — Authentication infrastructure (revision 3)

Supersedes `thoughts/shared/plans/2026-09-16_auth-infrastructure.md`. Save over that file.

Source research: `C:\projects\envanex\thoughts\shared\research\2026-09-16_auth-infrastructure.md` (decisions 1–11 treated as settled constraints).

Revision note. Revision 2 settled the four proposals and made login answer one indistinguishable 401. This revision closes the two costs that created — a legitimate locked-out user was told nothing, and the locked-out path was still measurably faster — and promotes the rate-limit partition key from a note to a named PR 7 blocker with a single edit site. Changes F, G and H are incorporated, plus a measured-cost validation step for the concurrency loops.

## Goal

Add ASP.NET Core Identity in its own `DbContext` under an `auth` schema, short-lived JWT access tokens, opaque refresh tokens stored hashed with rotation and reuse detection, and the `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout` endpoints. Every existing endpoint stays open.

## Non-goals

Authorization policies. `[Authorize]` on any endpoint. The Blazor cookie scheme. The read-only demo account. Passkeys. Any endpoint that creates a user. A `JwtBearerEvents.OnChallenge` ProblemDetails body — no challenge is reachable in 6a, so writing it here would ship dead, untested code; it belongs to 6b together with the first `[Authorize]`. A refresh-token pruning job — rows are never deleted and growth is unbounded; this is a recorded gap for the Worker PR. **A dummy password verification on the unknown-email path** — Decision 6 closes the locked-out half of the timing side channel and deliberately leaves the unknown-email half open; see the known gaps. **`UseForwardedHeaders`** — Decision 4 argues it belongs to PR 7, not here.

## Touches schema?

**Yes — Phases 1, 2 and 4. db-reviewer is required after each of Phase 1, Phase 2 and Phase 4.**
Phases 3, 5 and 6 touch no schema, no migrations and no queries; db-reviewer is not required for them.

## ADR needed?

Yes, one. The human writes it after `tester` returns `READY_TO_PUSH`:

`docs/adr/0007-jwt-access-tokens-and-rotated-refresh-tokens.md`
Title: **"JWT access tokens with rotated, reuse-detected refresh tokens in a separate Identity context"**

Points the ADR must cover, because they are decisions made in this plan rather than derived from an existing ADR:

- The separate Identity `DbContext` and `auth` schema with its own `__EFMigrationsHistory`.
- `IIdentityService`/`IRefreshTokenService`/`IAccessTokenIssuer` as Application abstractions, so auth uses the same `ICommandHandler` pipeline ADR 0005 describes.
- SHA-256 rather than a password hash for the stored refresh token, and why a keyed hash was rejected.
- The zero grace period on reuse detection (research decision 7) and why.
- The `min(idle, absolute cap)` refresh expiry shape and the 15 min / 7 days / 30 days numbers, tied to the fact that PR 7 deploys publicly, the system carries stock-write authority, and 6a ships no revocation surface.
- **Why login answers wrong password, unknown user and locked-out account with one byte-identical 401**, and why `Auth.UserLockedOut` therefore does not exist as an error code at all.
- **Why the one message that 401 carries names lockout as a general possibility without ever confirming it**, and why that is not a leak.
- **Why the password is verified before the lockout check** — the ordering that makes the locked-out path cost the same PBKDF2 work as a wrong password — and the two residual gaps it leaves: the unknown-email path, and the one `UPDATE` a wrong password performs that a locked-out attempt does not.
- The filtered unique index `IX_RefreshTokens_FamilyId_Live` as the family lock, and the two loser paths (unique violation and `RowVersion` concurrency) that it and the concurrency token produce.
- `ClockSkew = TimeSpan.Zero` on validation.
- Identity's built-in `Microsoft.AspNetCore.Identity` meter as an argument for Identity over hand-rolled user storage.
- The deferral of the `OnChallenge` body, the refresh-token pruning job, and — as a named PR 7 blocker — the rate-limit partition key behind a reverse proxy.

An agent may create the empty file with that title. The agent must not write its body.

---

## Settled decisions

Ruled by the human. Phases that depend on one say so. These are constraints on the coder, not open questions.

### Decision 1 — Lifetimes

- Access token: **15 minutes** (`Jwt:AccessTokenMinutes` = 15)
- Refresh token idle window: **7 days** (`Jwt:RefreshTokenIdleDays` = 7)
- Refresh token absolute family cap: **30 days** (`Jwt:RefreshTokenAbsoluteDays` = 30)

The system deploys publicly in PR 7, carries stock-write authority, and ships no revocation surface. Until something can revoke an issued grant, the only defence against a leaked token is a short window. A 7-day idle window still keeps a weekday-regular operator signed in without re-entering a password; a 30-day cap forces a real re-authentication monthly.

Phases 2, 3, 4 and 6 depend on these numbers.

### Decision 2 — Refresh expiry shape

**Absolute cap combined with a shorter idle window.** Each `RefreshTokens` row carries both `ExpiresAt` (the idle window: `now + RefreshTokenIdleDays`, recomputed on every rotation) and `FamilyExpiresAt` (the absolute cap: `familyCreatedAt + RefreshTokenAbsoluteDays`, copied forward unchanged to every child). A token is usable only when `now < ExpiresAt` **and** `now < FamilyExpiresAt`.

Pure sliding never ends a session: a stolen token that is rotated forever is permanent access. Pure absolute logs an actively-working user out mid-week. Copying `FamilyExpiresAt` onto every generation rather than joining back to the family root keeps the rotation hot path a single-row read.

Phases 2 and 4 depend on this.

### Decision 3 — Refresh token hash

**SHA-256 over the UTF-8 bytes of the token, stored as `binary(32)`, compared with `CryptographicOperations.FixedTimeEquals`.** Token material is 32 bytes from `RandomNumberGenerator.GetBytes`, Base64Url-encoded.

There is no dictionary to attack against 256 bits of CSPRNG output, so a password hash's work factor buys nothing while costing ~100 ms on every refresh. A salted password hash is also per-row and therefore not searchable: lookup would degrade from one indexed equality seek to a scan with a verify per row. A deterministic hash keeps the unique index on `TokenHash` meaningful and gives collision detection for free. Not HMAC-SHA256: a keyed hash adds a second secret to rotate and buys nothing against the only realistic attacker here, one who has read the table but still cannot invert a 256-bit random preimage. `FixedTimeEquals` is applied to the fetched row even though the index seek already matched, so the constant-time property does not silently depend on the index.

Phases 2 and 4 depend on this.

### Decision 4 — Login rate limit: own switch, own factory, and a named partition key

- A named policy `"login"` registered via `AddPolicy`, driven by `RateLimiting:Login:{Enabled,PermitLimit,WindowSeconds}` (production defaults 5 / 300 s).
- A new `LoginRateLimitedWebApplicationFactory` (PermitLimit 2, WindowSeconds 60) for the tests, following the `RateLimitedWebApplicationFactory` precedent.
- One refactor in `Program.cs`: `AddRateLimiter` and `app.UseRateLimiter()` become unconditional; only the **assignment of `GlobalLimiter`** stays inside `if (rateLimitingEnabled)`. The login policy is a real limiter when `RateLimiting:Login:Enabled` is true and `RateLimitPartition.GetNoLimiter` otherwise.
- The partition key is **not** an inline lambda. It is `LoginRateLimitPartition.GetKey(HttpContext)`, a named internal static method with its own tests, so PR 7 has exactly one edit site.

The switch alone is insufficient: `[EnableRateLimiting("login")]` names a policy that only exists if `AddRateLimiter` ran, and today both `AddRateLimiter` and `UseRateLimiter()` sit behind `RateLimiting:Enabled`, which the shared test factory sets to `false`. Referencing a policy the middleware never sees is exactly the silently-inert guard this research already found once in the architecture test. The factory alone is insufficient in the other direction: without an independent switch the shared factory would have to enable the login limiter for every test class in the collection, and since all integration tests share one container and one factory, the 5-request budget would be consumed across classes and produce flaky 429s.

**`LoginRateLimitPartition.GetKey` returns `context.Connection.RemoteIpAddress?.ToString()`, falling back to the constant `"unknown"`.** Under `WebApplicationFactory` the connection has no remote address, so every request in a test run lands in the `"unknown"` partition — which is what makes `LoginRateLimiterTests` deterministic. An unhandled null would throw `ArgumentNullException` inside the partition factory on every request and take login down entirely, so the fallback is load-bearing, not defensive noise.

#### The same fallback is a PR 7 blocker

Azure App Service terminates TLS at a front-end reverse proxy and puts the client address in `X-Forwarded-For`. On the first public deploy `RemoteIpAddress` is therefore the proxy's address, identical for every request, and **every user in the world collapses into one login partition**. Five requests per five minutes stops being a per-client limit and becomes a global one: two people signing in within the same window lock out the third. Login does not degrade, it stops working.

This is recorded in `docs/roadmap.md` as a **blocker on PR 7**, not a note, and it closes in PR 7.

**`UseForwardedHeaders` does not belong in 6a.** Two reasons, and the second is the one that matters:

1. Configuring it correctly requires `KnownProxies` or `KnownNetworks` set to the actual front end, which is deployment knowledge PR 6a does not have and cannot invent.
2. Calling `UseForwardedHeaders()` with defaults and no known proxy means ASP.NET Core refuses to honour the header — safe, but it changes nothing, so it would ship as an untested no-op that *looks* like the problem is solved. The usual next step when someone notices it did nothing is to clear `KnownNetworks` and `KnownProxies`, at which point `X-Forwarded-For` becomes attacker-controlled and the login limiter becomes worthless: an attacker simply sends a new value per request and gets an unlimited partition each time. **That is strictly worse than one shared bucket.** A global limit fails closed and is loud; a spoofable limit fails open and is silent.

So 6a's contribution is to make the failure visible and cheap to fix: one named method, four tests, one blocker line in the roadmap.

Phase 6 depends on this.

### Decision 5 — Login returns one indistinguishable 401 for every credential failure

Wrong password, unknown email and locked-out account must be answered with the same HTTP status, the same `Content-Type` and the same response body. Three distinguishable responses let an attacker enumerate accounts and, worse, watch the response change the moment a brute-force attempt starts working.

Implementation consequence: **`Auth.UserLockedOut` does not exist.** It is not in `AuthErrors`, not in `StatusCodeMap`, not in `TurkishErrorMessages`. `IdentityService.ValidateCredentialsAsync` returns `AuthErrors.InvalidCredentials` on all three paths. Making the codes identical at the source is stronger than mapping two codes to the same status: two codes with the same status still carry different `detail` strings today and invite a future maintainer to "helpfully" give the lockout its own message, silently reopening the hole. There is no error code to give a message to.

The lockout **mechanism** is unchanged: `AccessFailedAsync` on a wrong password against an unlocked account, `MaxFailedAccessAttempts` 5, `DefaultLockoutTimeSpan` 15 minutes, `AllowedForNewUsers` true. It is observable through `ILogger<IdentityService>` at `Warning` and through Identity's built-in `Microsoft.AspNetCore.Identity` meter, never through the response.

No 403 is produced anywhere in 6a, so `GetReasonPhrase` gains only a `401 => "Unauthorized"` arm.

Phases 3, 4, 5 and 6 depend on this.

### Decision 6 — The one message names lockout as a possibility, and the password is verified before the lockout check

Two changes that between them pay for Decision 5's costs without giving any of its guarantee back.

**The message.** `Auth.InvalidCredentials` carries, in Turkish:

> "E-posta veya parola hatalı. Arka arkaya birkaç başarısız denemeden sonra hesap bir süreliğine kilitlenir."

This is a statement about how the system behaves, not about the account in front of it. A legitimate user who has locked themselves out and is now staring at "wrong password" while typing the right one learns what probably happened; an attacker learns a policy they could have read in any changelog. The response is byte-identical in all three cases, so nothing is measurable. The distinction the tests enforce is grammatical and precise: the message uses the aorist **"kilitlenir"** ("gets locked"), never the past **"kilitlendi"** ("has been locked"), which would confirm the state of this specific account.

Vocabulary alignment: the codebase currently uses "Şifre" for password in `Auth.PasswordRequired`. This message uses "parola". Pick one — this plan standardises on **"parola"** and changes `Auth.PasswordRequired` to "Parola zorunludur." so the login screen does not use two words for one field.

**The ordering.** For a user that exists, `CheckPasswordAsync` runs **before** the lockout check, unconditionally. The locked-out path therefore performs the same PBKDF2 work as a wrong password and answers in comparable time.

This is the signal most worth hiding. An attacker running a password list against a known account does not care much whether the account exists — they already believe it does. They care enormously whether their attempts are being counted, because a sudden fast "no" tells them the lockout tripped, which tells them the target is real, the policy is 5 attempts, and exactly when to back off and resume. Under the old ordering that transition was a ~100 ms step change, visible over any network. Under the new ordering it is not there.

Residual gaps, both recorded in `docs/roadmap.md`:
- **Unknown email is still fast.** `FindByEmailAsync` returns null and no hash is verified, so a non-existent account answers in a millisecond and a real one in ~100 ms. Closing it needs a dummy verification against a fixed throwaway hash on the miss path, which is an explicit non-goal for 6a.
- **A wrong password performs one `UPDATE` that a locked-out attempt does not** (`AccessFailedAsync`). That delta is one indexed single-row write against a warm connection — roughly a millisecond against the ~100 ms of PBKDF2 it now shares. Orders of magnitude smaller than the gap it replaces, and not claimed to be zero.

Accepted trade: a locked-out account now burns PBKDF2 CPU on every attempt, so lockout no longer sheds load under a sustained attack. The login rate limiter (Decision 4) is what bounds that, at 5 attempts per window per partition.

**`AccessFailedAsync` is called at most once per attempt, on exactly one branch.** Decision 5 removed the only reason the old design re-checked lockout after incrementing — it needed to choose between two error codes. There is now one error code, so there is no re-check, and therefore no path on which the counter can be incremented twice. Phase 4 names the tests that hold that.

Phases 3, 4 and 6 depend on this.

---

## Phase 1: Identity context, `auth` schema, its own migrations history

**Touches schema and migrations — db-reviewer required.**

### Files

- `Directory.Packages.props` — modified — add, in a new `<!-- Identity / Auth -->` group:
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore` version `10.0.11`
  - `Microsoft.AspNetCore.Authentication.JwtBearer` version `10.0.11`
  - `Microsoft.IdentityModel.JsonWebTokens` — version **must be read, not guessed**: run `dotnet list src/Envanex.Web/Envanex.Web.csproj package --include-transitive` after adding JwtBearer and pin the exact version JwtBearer 10.0.11 resolves. Pinning anything lower produces NU1605, which `TreatWarningsAsErrors` turns into a build failure.
  - `Microsoft.Extensions.TimeProvider.Testing` — ships from `dotnet/extensions` on its own version line, not the ASP.NET Core 10.0.x line, so it carries no NU1605 risk against the family. Resolve the exact version with `dotnet package search Microsoft.Extensions.TimeProvider.Testing --exact-match` and pin the latest stable.
- `CLAUDE.md` — modified — the `## Commands` block currently reads
  ```
  dotnet ef migrations add <Name> -p src/Envanex.Infrastructure -s src/Envanex.Web
  dotnet ef database update -p src/Envanex.Infrastructure -s src/Envanex.Web
  ```
  Both break the moment a second `IDesignTimeDbContextFactory` exists in the assembly, and every validation step in this plan passes `--context`. Replace with four lines:
  ```
  dotnet ef migrations add <Name> -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexDbContext
  dotnet ef migrations add <Name> -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext --output-dir Migrations/Identity
  dotnet ef database update -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexDbContext
  dotnet ef database update -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext
  ```
  Change nothing else in `CLAUDE.md`. It is instructions, not source, so it is outside the `src/`, `tests/`, `db/` write ban, but it is also not a dumping ground — one block, four lines.
- `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj` — modified — add `<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />`. Do **not** add a `FrameworkReference` to `Microsoft.AspNetCore.App`: the design below deliberately uses `AddIdentityCore` and `UserManager<T>` only (both from `Microsoft.Extensions.Identity.Core`/`.Stores`) and never `SignInManager`, which lives in the shared framework.
- `src/Envanex.Infrastructure/Identity/EnvanexUser.cs` — created — `public sealed class EnvanexUser : IdentityUser<Guid>` with no added members. Public, not internal, because `EnvanexIdentityDbContext` is public and a public class cannot derive from a base constructed with a less accessible type argument (CS0060).
- `src/Envanex.Infrastructure/Identity/AuthSchema.cs` — created.
- `src/Envanex.Infrastructure/Identity/EnvanexIdentityDbContext.cs` — created — `public class EnvanexIdentityDbContext : IdentityDbContext<EnvanexUser, IdentityRole<Guid>, Guid>`. `OnModelCreating` calls `ArgumentNullException.ThrowIfNull(modelBuilder)`, then `modelBuilder.HasDefaultSchema(AuthSchema.Name)`, then `base.OnModelCreating(modelBuilder)`. It does **not** call `ApplyConfigurationsFromAssembly` — Phase 2 adds one explicit `ApplyConfiguration` call. It does **not** override `ConfigureConventions`, so `AggregateRootConvention` and `MoneyComplexTypeConvention` do not reach it.
  - Note for db-reviewer: the full `IdentityDbContext` (with roles) is used rather than `IdentityUserContext`, so `AspNetRoles`, `AspNetRoleClaims` and `AspNetUserRoles` are created now and stay empty until PR 6b. The trade is three empty tables today against a second Identity migration in 6b.
  - Note for db-reviewer: `AspNetUsers.Id` is a `uniqueidentifier` with a clustered PK. Acceptable for a low-cardinality table; called out because Phase 2's high-volume table takes the opposite decision.
- `src/Envanex.Infrastructure/Identity/IdentityDbContextOptionsExtensions.cs` — created — one place that knows how to point a `DbContextOptionsBuilder` at the Identity database, so the migrations-history table cannot be set in one of the three call sites and forgotten in the other two.
- `src/Envanex.Infrastructure/Identity/EnvanexIdentityDbContextFactory.cs` — created — reads `ENVANEX_CONNECTION_STRING` and throws the same-shaped `InvalidOperationException` as `EnvanexDbContextFactory`, building options through `UseEnvanexIdentitySqlServer`.
- `src/Envanex.Infrastructure/DependencyInjection.cs` — modified — add `services.AddDbContext<EnvanexIdentityDbContext>(options => options.UseEnvanexIdentitySqlServer(connectionString));` after the existing `AddDbContext<EnvanexDbContext>` call. Nothing else in this phase.
- `src/Envanex.Infrastructure/Persistence/EnvanexDbContext.cs` — modified — change `ApplyConfigurationsFromAssembly(typeof(EnvanexDbContext).Assembly)` to the predicate overload restricting configurations to the namespace `Envanex.Infrastructure.Persistence.Configurations`. **This is not cosmetic.** Phase 2 adds `RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>` to the same assembly, and without this predicate `EnvanexDbContext` would pick it up and create a second, unwanted `dbo.RefreshTokens` table on the next business migration.
- `src/Envanex.Infrastructure/Migrations/Identity/` — created by the tooling — migration `InitialIdentitySchema` plus its designer and `EnvanexIdentityDbContextModelSnapshot`.
- `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs` — modified — add `CreateIdentityDbContext()`; in `InitializeAsync`, after the existing `MigrateAsync`, open an identity context and `MigrateAsync` it; add `ResetIdentityAsync()` deleting in FK order `AspNetUserTokens`, `AspNetUserLogins`, `AspNetUserClaims`, `AspNetUserRoles`, `AspNetRoleClaims`, `AspNetRoles`, `AspNetUsers`, all schema-qualified with `auth.`. Per research decision 5 this is a **separate** method; `ResetAsync` is not touched, so no business test starts paying for auth cleanup.
- `tests/Envanex.IntegrationTests/Envanex.IntegrationTests.csproj` — modified — add `<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />` and `<PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" />` (both used from Phase 4).
- `tests/Envanex.Domain.Tests/ArchitectureTests.cs` — modified — see "Tests to add".

### Signatures

```csharp
// src/Envanex.Infrastructure/Identity/AuthSchema.cs
internal static class AuthSchema
{
    public const string Name = "auth";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";
}

// src/Envanex.Infrastructure/Identity/EnvanexUser.cs
public sealed class EnvanexUser : IdentityUser<Guid>;

// src/Envanex.Infrastructure/Identity/EnvanexIdentityDbContext.cs
public class EnvanexIdentityDbContext : IdentityDbContext<EnvanexUser, IdentityRole<Guid>, Guid>
{
    public EnvanexIdentityDbContext(DbContextOptions<EnvanexIdentityDbContext> options);
    protected override void OnModelCreating(ModelBuilder modelBuilder);
}

// src/Envanex.Infrastructure/Identity/IdentityDbContextOptionsExtensions.cs
internal static class IdentityDbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UseEnvanexIdentitySqlServer(
        this DbContextOptionsBuilder builder,
        string connectionString);
}

// src/Envanex.Infrastructure/Identity/EnvanexIdentityDbContextFactory.cs
public sealed class EnvanexIdentityDbContextFactory : IDesignTimeDbContextFactory<EnvanexIdentityDbContext>
{
    public EnvanexIdentityDbContext CreateDbContext(string[] args);
}

// tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs
public EnvanexIdentityDbContext CreateIdentityDbContext();
public Task ResetIdentityAsync();
```

`UseEnvanexIdentitySqlServer` must call `UseSqlServer(connectionString, b => b.MigrationsHistoryTable(AuthSchema.MigrationsHistoryTable, AuthSchema.Name))`. Without that argument both contexts read `dbo.__EFMigrationsHistory` as their own and the failure is silent until a migration is skipped or re-applied.

`InternalsVisibleTo` already exists from `Envanex.Infrastructure` to `Envanex.IntegrationTests`, so the test project can name `AuthSchema` and `UseEnvanexIdentitySqlServer`. No new `InternalsVisibleTo` entry is needed.

### Tests to add

`tests/Envanex.IntegrationTests/Persistence/IdentitySchemaTests.cs` — new, `[Collection(DatabaseCollection.Name)]`:
- `IdentityMigrations_ShouldApplyToCleanDatabase`
- `IdentityTables_ShouldLiveInAuthSchema` — `[Theory]` with `[InlineData]` for `AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserRoles`, `AspNetUserTokens`, `AspNetRoleClaims`.
- `IdentityMigrationsHistory_ShouldLiveInAuthSchema`
- `BusinessAppliedMigrations_ShouldNotContainIdentityMigrations`
- `IdentityAppliedMigrations_ShouldNotContainBusinessMigrations`
- `BusinessMigrationsHistory_ShouldNotContainIdentityMigrations` — direct `SELECT MigrationId FROM dbo.__EFMigrationsHistory`. Catches the silent shared-history failure at the table level rather than through EF's own bookkeeping.

`tests/Envanex.IntegrationTests/Persistence/DbContextIsolationTests.cs` — new, `[Collection(DatabaseCollection.Name)]`:
- `EnvanexDbContext_ShouldNotMapEnvanexUser`
- `EnvanexDbContext_ShouldStillMapProductUnitOfMeasureAndWarehouse` — negative control on the new configuration predicate, so an over-narrow namespace filter cannot pass silently.
- `EnvanexIdentityDbContext_ShouldNotMapAnyBusinessEntityType`
- `EnvanexIdentityDbContext_ShouldNotDeclareRowVersionShadowProperty` — proves `AggregateRootConvention` did not follow the Identity context.

`tests/Envanex.Domain.Tests/ArchitectureTests.cs` — modified:
- `Application_ShouldNotReference_AuthenticationPackages` — `[Theory]` with `[InlineData("Microsoft.AspNetCore.Identity")]`, `[InlineData("Microsoft.AspNetCore.Authentication")]`, `[InlineData("Microsoft.IdentityModel")]`, `[InlineData("System.IdentityModel")]`. Closes the gap the research proved: the existing `Microsoft\.EntityFrameworkCore` regex matches neither new package name.
- `ForbiddenPackageMatcher_ShouldMatch_RealAuthPackageNames` — the negative control. Runs the same matcher against the literal strings `"Microsoft.AspNetCore.Identity.EntityFrameworkCore"` and `"Microsoft.AspNetCore.Authentication.JwtBearer"` and asserts both match. Without it a broken matcher passes the theory above by matching nothing at all, which is precisely the failure mode the research documented.
- `Infrastructure_ShouldReference_IdentityEntityFrameworkCore` — positive control.

`tests/Envanex.IntegrationTests/Persistence/MigrationTests.cs` — unchanged; it still covers the business context.

### Validation

Run in order from `C:\projects\envanex`. **Paste the raw output of steps 4 to 7 into the phase report, including the expected failure in step 5.**

```
docker compose up -d
dotnet build -warnaserror
$env:ENVANEX_CONNECTION_STRING="Server=localhost,1433;Database=EnvanexDev;User Id=sa;Password=<password>;TrustServerCertificate=True"
dotnet ef dbcontext list -p src/Envanex.Infrastructure -s src/Envanex.Web
dotnet ef migrations list -p src/Envanex.Infrastructure -s src/Envanex.Web
dotnet ef migrations list -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexDbContext
dotnet ef migrations list -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext
dotnet ef migrations add InitialIdentitySchema -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext --output-dir Migrations/Identity
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

Expected: step 5 (`migrations list` with no `--context`) **fails** with "More than one DbContext was found"; steps 6 and 7 succeed. If step 5 unexpectedly succeeds, stop and report it — the plan's premise about `--context` being mandatory is wrong, and the `CLAUDE.md` edit above would then be unnecessary.

---

## Phase 2: RefreshTokens table, hashing and token generation

**Touches schema and migrations — db-reviewer required.**
Depends on Decisions 1, 2 and 3.

### Files

- `src/Envanex.Infrastructure/Identity/RefreshToken.cs` — created — `internal sealed class RefreshToken`. All setters `private set`; state changes go through the methods below, so the rotation rules cannot be bypassed by a stray property assignment in a service.
- `src/Envanex.Infrastructure/Identity/RefreshTokenHasher.cs` — created.
- `src/Envanex.Infrastructure/Identity/RefreshTokenGenerator.cs` — created.
- `src/Envanex.Infrastructure/Identity/Configurations/RefreshTokenConfiguration.cs` — created — deliberately **not** under `Envanex.Infrastructure.Persistence.Configurations`, so the Phase 1 predicate keeps it out of `EnvanexDbContext`.
- `src/Envanex.Infrastructure/Identity/EnvanexIdentityDbContext.cs` — modified — add `internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();` and, in `OnModelCreating` after `base.OnModelCreating`, `modelBuilder.ApplyConfiguration(new Configurations.RefreshTokenConfiguration());`.
- `src/Envanex.Infrastructure/Migrations/Identity/` — migration `AddRefreshTokens`.

### Signatures

```csharp
internal sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public byte[] TokenHash { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset FamilyExpiresAt { get; private set; }
    public DateTimeOffset? RotatedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }

    public static RefreshToken CreateRoot(
        Guid userId, byte[] tokenHash, DateTimeOffset now,
        TimeSpan idleWindow, TimeSpan absoluteWindow);

    public RefreshToken CreateChild(byte[] tokenHash, DateTimeOffset now, TimeSpan idleWindow);

    public void MarkRotated(DateTimeOffset now, Guid replacedByTokenId);
    public void Revoke(DateTimeOffset now, RefreshTokenRevocationReason reason);
    public bool IsUsableAt(DateTimeOffset now);
}

internal enum RefreshTokenRevocationReason { Logout, Reuse, Expired }

internal static class RefreshTokenHasher
{
    public static byte[] Hash(string token);
    public static bool Matches(string token, byte[] storedHash);
}

internal static class RefreshTokenGenerator
{
    public const int TokenByteLength = 32;
    public static string CreateToken();
}
```

`CreateChild` copies `FamilyId` and `FamilyExpiresAt` from the parent unchanged and sets `ExpiresAt = now + idleWindow` (Decision 2). `IsUsableAt` returns true only when `RotatedAt is null && RevokedAt is null && now < ExpiresAt && now < FamilyExpiresAt`.

`Hash` is SHA-256 over `Encoding.UTF8.GetBytes(token)`. `Matches` calls `CryptographicOperations.FixedTimeEquals`. `CreateToken` is `Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenByteLength))`.

Per the project's null-vs-invalid rule: `Hash(null)` and `Matches(null, _)` throw `ArgumentNullException`; a token string that simply does not match is a `false` return, not an exception.

### EF configuration — exact

- `builder.ToTable("RefreshTokens", AuthSchema.Name)`
- `builder.HasKey(t => t.Id)` **and** `.IsClustered(false)`
- `builder.HasIndex(t => new { t.CreatedAt, t.Id }).IsClustered().HasDatabaseName("IX_RefreshTokens_CreatedAt_Id")` — this table is append-only and never pruned in 6a; a random-GUID clustered PK would fragment every insert. **db-reviewer should confirm this trade explicitly.**
- `builder.Property(t => t.TokenHash).IsRequired().HasColumnType("binary(32)")`
- `builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("IX_RefreshTokens_TokenHash")`
- `builder.HasIndex(t => t.FamilyId).IsUnique().HasFilter("[RotatedAt] IS NULL AND [RevokedAt] IS NULL").HasDatabaseName("IX_RefreshTokens_FamilyId_Live")` — at most one live token per family. This is the database-level answer to the "rotation racing revocation" trap in the research: a concurrent second rotation hits a unique violation instead of silently writing a second live child. Phase 4 names the tests that exercise it.
- `builder.HasIndex(t => t.UserId).HasDatabaseName("IX_RefreshTokens_UserId")`
- `builder.HasOne<EnvanexUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade).IsRequired()`
- `builder.Property(t => t.RevokedReason).HasMaxLength(32)`
- `builder.Property<byte[]>(ColumnNames.RowVersion).IsRowVersion()` — configured explicitly, because `AggregateRootConvention` is not registered on this context. It is also the second half of the family lock: it is what makes a rotation racing a revocation fail rather than overwrite.
- All six `DateTimeOffset` properties map to `datetimeoffset` by convention; no explicit precision is set.

### Tests to add

`tests/Envanex.IntegrationTests/Identity/RefreshTokenHasherTests.cs` — new. Pure unit tests; they sit in `Envanex.IntegrationTests` because that is the only project with `InternalsVisibleTo` access to `Envanex.Infrastructure`. No `[Collection]`, no database.
- `Hash_SameToken_ShouldProduceTheSameHash`
- `Hash_DifferentTokens_ShouldProduceDifferentHashes`
- `Hash_ShouldProduceExactly32Bytes`
- `Hash_NullToken_ShouldThrowArgumentNullException`
- `Matches_CorrectToken_ShouldReturnTrue`
- `Matches_WrongToken_ShouldReturnFalse`
- `Matches_StoredHashOfWrongLength_ShouldReturnFalse`
- `CreateToken_ShouldDecodeTo32Bytes`
- `CreateToken_ShouldBeUrlSafe` — contains no `+`, `/` or `=`.
- `CreateToken_CalledOneThousandTimes_ShouldProduceOneThousandDistinctValues`

`tests/Envanex.IntegrationTests/Identity/RefreshTokenTests.cs` — new, no database:
- `CreateRoot_ShouldSetExpiresAtToSevenDaysFromNow` *(Decision 1)*
- `CreateRoot_ShouldSetFamilyExpiresAtToThirtyDaysFromNow` *(Decision 1)*
- `CreateRoot_ShouldSetFamilyIdToANonEmptyGuid`
- `CreateChild_ShouldKeepTheParentFamilyId`
- `CreateChild_ShouldCopyFamilyExpiresAtUnchanged`
- `CreateChild_ShouldResetExpiresAtToSevenDaysFromNow`
- `IsUsableAt_FreshToken_ShouldBeTrue`
- `IsUsableAt_RotatedToken_ShouldBeFalse`
- `IsUsableAt_RevokedToken_ShouldBeFalse`
- `IsUsableAt_PastTheIdleWindow_ShouldBeFalse`
- `IsUsableAt_WithinTheIdleWindowButPastFamilyExpiry_ShouldBeFalse`
- `MarkRotated_ShouldSetRotatedAtAndReplacedByTokenId`

`tests/Envanex.IntegrationTests/Persistence/RefreshTokenSchemaTests.cs` — new, `[Collection(DatabaseCollection.Name)]`, calls `ResetIdentityAsync` in `InitializeAsync`:
- `RefreshTokens_ShouldLiveInAuthSchema`
- `RefreshTokens_TokenHash_ShouldBeBinary32AndNotNullable`
- `RefreshTokens_TokenHashIndex_ShouldBeUnique`
- `RefreshTokens_FamilyLiveIndex_ShouldBeUniqueAndFiltered` — asserts `sys.indexes.has_filter = 1` and the filter definition mentions both `RotatedAt` and `RevokedAt`.
- `RefreshTokens_PrimaryKey_ShouldBeNonClustered`
- `RefreshTokens_ClusteredIndex_ShouldBeOnCreatedAtAndId`
- `RefreshTokens_UserId_ShouldHaveCascadingForeignKeyToAspNetUsers`
- `RefreshTokens_InsertingASecondLiveTokenForTheSameFamily_ShouldThrowUniqueViolation`
- `RefreshTokens_InsertingASecondTokenWithTheSameHash_ShouldThrowUniqueViolation`
- `RefreshTokens_RotatedRowAndItsChild_ShouldCoexistInTheSameFamily` — the direct regression for the research's "deleting the predecessor destroys detection" trap.
- `RefreshTokens_ExpiryColumns_ShouldBeDateTimeOffset`
- `RefreshTokens_ShouldHaveARowVersionColumn`

### Validation

```
dotnet ef migrations add AddRefreshTokens -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext --output-dir Migrations/Identity
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

Paste the generated `Up` method of `AddRefreshTokens` into the phase report so db-reviewer can read the filtered index and the clustered/non-clustered split without opening the file.

---

## Phase 3: Application auth contracts, `AuthErrors`, and the widened reflection guard

**No schema, no migrations, no queries. db-reviewer not required.**
Depends on Decisions 1, 5 and 6.

This phase deliberately ships the error codes and the two mapping-table entries together. `ResultMappingTests.ResultMapping_NoMappingEntry_ShouldBeOrphaned` fails if a table entry exists without a matching `Error` field, so splitting them would leave the suite red.

### Files

- `src/Envanex.Application/Authentication/AuthErrors.cs` — created.
- `src/Envanex.Application/Authentication/JwtOptions.cs` — created. A plain POCO in Application so that Infrastructure (which signs) and Web (which validates) read the same key names from one declaration rather than two independent string literals. It pulls in no package: Application keeps only FluentValidation and `DependencyInjection.Abstractions`.
- `src/Envanex.Application/Authentication/JwtOptionsGuard.cs` — created.
- `src/Envanex.Application/Abstractions/Authentication/IIdentityService.cs` — created.
- `src/Envanex.Application/Abstractions/Authentication/IRefreshTokenService.cs` — created.
- `src/Envanex.Application/Abstractions/Authentication/IAccessTokenIssuer.cs` — created.
- `src/Envanex.Application/Authentication/Models/AuthenticatedUser.cs` — created.
- `src/Envanex.Application/Authentication/Models/IssuedAccessToken.cs` — created.
- `src/Envanex.Application/Authentication/Models/IssuedRefreshToken.cs` — created.
- `src/Envanex.Application/Authentication/Models/RotatedRefreshToken.cs` — created.
- `src/Envanex.Application/Authentication/DTOs/AuthenticationResponse.cs` — created. Named `*Response`, not `*ListDto`, so the `DataSourceListDtos_ShouldNotBePositionalRecords` architecture rule does not apply and a positional record is fine.
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — modified — **eight** new `StatusCodeMap` entries (33 → 41) and **one** new arm in `GetReasonPhrase`: `401 => "Unauthorized"`. No 403 arm: Decision 5 removes the only candidate.
- `src/Envanex.Web/Extensions/TurkishErrorMessages.cs` — modified — the same eight codes (33 → 41), with the `Auth.InvalidCredentials` wording from Decision 6 and the "Şifre" → "Parola" alignment.
- `src/Envanex.Web/appsettings.json` — modified — add a `Jwt` section: `"Issuer": "https://envanex.local"`, `"Audience": "envanex-api"`, `"SigningKey": ""`, `"AccessTokenMinutes": 15`, `"RefreshTokenIdleDays": 7`, `"RefreshTokenAbsoluteDays": 30`. `SigningKey` stays empty; it is a user-secret. No secret is ever written to this file.
- `tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs` — modified — per research decision 1.

### Signatures

```csharp
public static class AuthErrors
{
    public static readonly Error InvalidCredentials    = new("Auth.InvalidCredentials", "Email or password is incorrect.");
    public static readonly Error InvalidRefreshToken   = new("Auth.InvalidRefreshToken", "The refresh token is not valid.");
    public static readonly Error RefreshTokenExpired   = new("Auth.RefreshTokenExpired", "The refresh token has expired.");
    public static readonly Error RefreshTokenReused    = new("Auth.RefreshTokenReused", "The refresh token has already been used; the session was terminated.");
    public static readonly Error EmailRequired         = new("Auth.EmailRequired", "Email is required.");
    public static readonly Error EmailInvalid          = new("Auth.EmailInvalid", "Email is not a valid address.");
    public static readonly Error PasswordRequired      = new("Auth.PasswordRequired", "Password is required.");
    public static readonly Error RefreshTokenRequired  = new("Auth.RefreshTokenRequired", "Refresh token is required.");
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; }
    public int RefreshTokenIdleDays { get; set; }
    public int RefreshTokenAbsoluteDays { get; set; }
}

public static class JwtOptionsGuard
{
    public static void ThrowIfInvalid(JwtOptions options);
}

public interface IIdentityService
{
    Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);
}

public interface IRefreshTokenService
{
    Task<Result<IssuedRefreshToken>> IssueAsync(Guid userId, CancellationToken ct = default);
    Task<Result<RotatedRefreshToken>> RotateAsync(string presentedToken, CancellationToken ct = default);
    Task<Result> RevokeFamilyAsync(string presentedToken, CancellationToken ct = default);
}

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(AuthenticatedUser user);
}

public sealed record AuthenticatedUser(Guid Id, string Email, string UserName);
public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);
public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAt);
public sealed record RotatedRefreshToken(AuthenticatedUser User, string Token, DateTimeOffset ExpiresAt);

public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    string TokenType);
```

**There is deliberately no `AuthErrors.UserLockedOut`.** Decision 5. A locked-out account is answered with `AuthErrors.InvalidCredentials`, the same instance a wrong password and an unknown email produce, so the three responses cannot drift apart later.

`JwtOptionsGuard.ThrowIfInvalid` throws `ArgumentNullException` on null, otherwise `InvalidOperationException` naming the offending key and the user-secrets command — the same shape as the existing connection-string guard in `Infrastructure/DependencyInjection.cs`, e.g. `dotnet user-secrets set "Jwt:SigningKey" "<value>" --project src/Envanex.Web`. It rejects: blank `Issuer`, blank `Audience`, blank `SigningKey`, a `SigningKey` shorter than 32 UTF-8 bytes (HS256 needs 256 bits), `AccessTokenMinutes <= 0`, `RefreshTokenIdleDays <= 0`, `RefreshTokenAbsoluteDays <= 0`, and `RefreshTokenIdleDays > RefreshTokenAbsoluteDays`.

### Status code mapping — exact

| Code | Status |
|---|---|
| `Auth.InvalidCredentials` | 401 |
| `Auth.InvalidRefreshToken` | 401 |
| `Auth.RefreshTokenExpired` | 401 |
| `Auth.RefreshTokenReused` | 401 |
| `Auth.EmailRequired` | 400 |
| `Auth.EmailInvalid` | 400 |
| `Auth.PasswordRequired` | 400 |
| `Auth.RefreshTokenRequired` | 400 |

No auth code maps to 403. Phase 3 names a test that locks that in.

### Turkish messages — exact strings

- `Auth.InvalidCredentials` → **"E-posta veya parola hatalı. Arka arkaya birkaç başarısız denemeden sonra hesap bir süreliğine kilitlenir."** *(Decision 6)*
- `Auth.InvalidRefreshToken` → "Oturum bilgisi geçersiz. Lütfen tekrar giriş yapın."
- `Auth.RefreshTokenExpired` → "Oturum süresi doldu. Lütfen tekrar giriş yapın."
- `Auth.RefreshTokenReused` → "Oturum güvenlik nedeniyle sonlandırıldı. Lütfen tekrar giriş yapın."
- `Auth.EmailRequired` → "E-posta adresi zorunludur."
- `Auth.EmailInvalid` → "Geçerli bir e-posta adresi giriniz."
- `Auth.PasswordRequired` → **"Parola zorunludur."** *(was "Şifre zorunludur." — Decision 6 vocabulary alignment)*
- `Auth.RefreshTokenRequired` → "Yenileme anahtarı zorunludur."

The `Auth.InvalidCredentials` message is seen by a wrong password, an unknown email and a locked-out account alike. That is the point: it describes the policy, never this account. The aorist "kilitlenir" is deliberate and load-bearing; "kilitlendi" would confirm the state and is what the tests forbid.

### `ResultMappingTests` change — exact

Rename the private helper `GetAllDomainErrorCodes()` to `GetAllErrorCodes()` and have it scan two assemblies — `typeof(Error).Assembly` and `typeof(AuthErrors).Assembly` — with the same field filter. Rename the four existing facts to drop "Domain" so they do not lie:
- `ResultMapping_EveryErrorCode_ShouldHaveAnExplicitMapping`
- `ResultMapping_EveryErrorCode_ShouldHaveATurkishMessage`
- `ResultMapping_NoErrorCode_ShouldBeEmpty`
- `ResultMapping_NoMappingEntry_ShouldBeOrphaned`

And add one new fact:
- `ResultMapping_Scan_ShouldReachTheApplicationAssembly` — asserts the scanned set contains `"Auth.InvalidCredentials"`. Without it, a broken two-assembly scan silently degrades back to Domain-only and the auth codes lose their guard again.

### Tests to add

`tests/Envanex.Application.Tests/Authentication/AuthErrorsTests.cs` — new:
- `AuthErrors_EveryCode_ShouldStartWithTheAuthPrefix`
- `AuthErrors_Codes_ShouldBeUnique`
- `AuthErrors_EveryMessage_ShouldBeNonEmpty`
- `AuthErrors_ShouldNotDeclareALockoutSpecificCode` — reflects over `AuthErrors` and asserts no field whose code or message names lockout. Decision 5 is a security property; a future maintainer re-adding `UserLockedOut` should have to delete a test that says why.

`tests/Envanex.Application.Tests/Authentication/JwtOptionsGuardTests.cs` — new:
- `ThrowIfInvalid_NullOptions_ShouldThrowArgumentNullException`
- `ThrowIfInvalid_BlankSigningKey_ShouldThrowNamingTheUserSecretsCommand`
- `ThrowIfInvalid_SigningKeyShorterThan32Bytes_ShouldThrow`
- `ThrowIfInvalid_SigningKeyOfExactly32Bytes_ShouldNotThrow`
- `ThrowIfInvalid_BlankIssuer_ShouldThrow`
- `ThrowIfInvalid_BlankAudience_ShouldThrow`
- `ThrowIfInvalid_AccessTokenMinutesZeroOrNegative_ShouldThrow`
- `ThrowIfInvalid_RefreshTokenIdleDaysGreaterThanAbsoluteDays_ShouldThrow`
- `ThrowIfInvalid_ValidOptions_ShouldNotThrow`

`tests/Envanex.IntegrationTests/Api/TurkishErrorMessagesTests.cs` — new, no database, the Decision 6 message guards:
- `InvalidCredentialsMessage_ShouldMentionThatRepeatedFailuresCauseALockout` — contains "kilitlenir".
- `InvalidCredentialsMessage_ShouldNotConfirmThatThisAccountIsLockedOut` — does **not** contain "kilitlendi", "kilitli" or "hesabınız". The aorist/past distinction is the whole safety argument, so it is asserted, not left to reviewer memory.
- `InvalidCredentialsMessage_ShouldNotNameWhichFactorWasWrong` — does not contain "parola hatalı." as a standalone claim distinct from the e-posta alternative; asserts the message offers both possibilities with "veya".
- `PasswordMessages_ShouldUseParolaConsistentlyAndNeverSifre` — scans every value in `TurkishErrorMessages` for "ifre" (matching "şifre" case- and diacritic-tolerantly) and asserts none remain. The vocabulary alignment is one-way and should stay that way.

`tests/Envanex.IntegrationTests/Api/ResultExtensionsTests.cs` — modified:
- `ToActionResult_AuthInvalidCredentials_ShouldReturn401WithTurkishDetail`
- `ToActionResult_AuthRefreshTokenReused_ShouldReturn401WithTurkishDetail`
- `ToActionResult_Status401_ShouldHaveTitleUnauthorized`
- `ToActionResult_EveryAuthErrorCode_ShouldMapTo400Or401` — `[Theory]` over the codes reflected from `AuthErrors`; asserts none maps to 403 or any other status. This is Decision 5 enforced at the mapping table, one layer below the HTTP test in Phase 6.

`tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs` — modified as above.

### Validation

```
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

The phase report must state the new `StatusCodeMap` and `TurkishErrorMessages` entry counts (both 41) so the two tables staying in step is visible without reading the diff, and must paste the `Auth.InvalidCredentials` Turkish string verbatim so the human can check the characters (ş, ğ, ı, ö, ü, ç) rendered correctly through the file encoding.

---

## Phase 4: Infrastructure implementations — Identity, rotation, JWT issuance

**Touches queries (refresh-token lookup, family revocation, concurrent rotation) but adds no migration — db-reviewer required.**
Depends on Decisions 1, 2, 3, 5 and 6.

### Files

- `src/Envanex.Infrastructure/Identity/IdentityService.cs` — created — `internal sealed class IdentityService : IIdentityService`.
- `src/Envanex.Infrastructure/Identity/RefreshTokenService.cs` — created — `internal sealed class RefreshTokenService : IRefreshTokenService`.
- `src/Envanex.Infrastructure/Identity/JwtAccessTokenIssuer.cs` — created — `internal sealed class JwtAccessTokenIssuer : IAccessTokenIssuer`.
- `src/Envanex.Infrastructure/Identity/IdentityInfrastructureExtensions.cs` — created — `public static class IdentityInfrastructureExtensions` with `AddEnvanexIdentity`.
- `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj` — modified — add `<PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" />`.
- `src/Envanex.Infrastructure/DependencyInjection.cs` — modified — call `services.AddEnvanexIdentity(configuration);` as the last statement before `return services;`.
- `tests/Envanex.IntegrationTests/DependencyInjectionTests.cs` — modified — `BuildProvider()` must add `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey`, `Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenIdleDays`, `Jwt:RefreshTokenAbsoluteDays` to its in-memory configuration, otherwise `AddEnvanexIdentity`'s guard throws and every existing DI test goes red.
- `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs` — modified — `UseSetting` for the same six `Jwt:*` keys.
- `tests/Envanex.IntegrationTests/Fixtures/RateLimitedWebApplicationFactory.cs` — modified — the same six `UseSetting` calls.
- `tests/Envanex.IntegrationTests/Fixtures/IdentitySeeder.cs` — created — per research decision 6, test users are created through `UserManager` resolved from a service scope, never written through a raw `DbContext`.
- `tests/Envanex.IntegrationTests/Fixtures/CountingUserManager.cs` — created — the instrument that makes Decision 6's ordering and call-count claims assertable instead of aspirational.

### Signatures

```csharp
public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddEnvanexIdentity(
        this IServiceCollection services,
        IConfiguration configuration);
}

internal sealed class IdentityService : IIdentityService
{
    public IdentityService(
        UserManager<EnvanexUser> userManager,
        TimeProvider timeProvider,
        ILogger<IdentityService> logger);

    public Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);
}

internal sealed class RefreshTokenService : IRefreshTokenService
{
    public const int RevocationRetryLimit = 3;

    public RefreshTokenService(
        EnvanexIdentityDbContext context,
        TimeProvider timeProvider,
        IOptions<JwtOptions> options,
        ILogger<RefreshTokenService> logger);

    public Task<Result<IssuedRefreshToken>> IssueAsync(Guid userId, CancellationToken ct = default);
    public Task<Result<RotatedRefreshToken>> RotateAsync(string presentedToken, CancellationToken ct = default);
    public Task<Result> RevokeFamilyAsync(string presentedToken, CancellationToken ct = default);
}

internal sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    public JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider);
    public IssuedAccessToken Issue(AuthenticatedUser user);
}

// tests/Envanex.IntegrationTests/Fixtures/IdentitySeeder.cs
internal static class IdentitySeeder
{
    public static Task<Guid> CreateUserAsync(IServiceProvider services, string email, string password);
    public static Task<int> GetAccessFailedCountAsync(IServiceProvider services, string email);
    public static Task<DateTimeOffset?> GetLockoutEndAsync(IServiceProvider services, string email);
    public static Task LockOutAsync(IServiceProvider services, string email);
}

// tests/Envanex.IntegrationTests/Fixtures/CountingUserManager.cs
internal sealed class CountingUserManager : UserManager<EnvanexUser>
{
    public int CheckPasswordCallCount { get; }
    public int IsLockedOutCallCount { get; }
    public int AccessFailedCallCount { get; }
    public int ResetAccessFailedCountCallCount { get; }

    /// <summary>Ordered names of the intercepted calls, for ordering assertions.</summary>
    public IReadOnlyList<string> CallLog { get; }

    public override Task<bool> CheckPasswordAsync(EnvanexUser user, string password);
    public override Task<bool> IsLockedOutAsync(EnvanexUser user);
    public override Task<IdentityResult> AccessFailedAsync(EnvanexUser user);
    public override Task<IdentityResult> ResetAccessFailedCountAsync(EnvanexUser user);
}
```

`CountingUserManager` forwards the full `UserManager<TUser>` constructor parameter list (`IUserStore<EnvanexUser>`, `IOptions<IdentityOptions>`, `IPasswordHasher<EnvanexUser>`, `IEnumerable<IUserValidator<EnvanexUser>>`, `IEnumerable<IPasswordValidator<EnvanexUser>>`, `ILookupNormalizer`, `IdentityErrorDescriber`, `IServiceProvider`, `ILogger<UserManager<EnvanexUser>>`) to `base`. All four overridden members are `public virtual` on `UserManager<TUser>`; each records its name in `CallLog`, increments its counter, and delegates to `base`. It is registered in the test `ServiceCollection` after `AddEnvanexIdentity` with `services.AddScoped<UserManager<EnvanexUser>, CountingUserManager>()`, replacing Identity's own registration.

### `AddEnvanexIdentity` — exact behaviour

1. `ArgumentNullException.ThrowIfNull` on both parameters.
2. Bind `configuration.GetSection(JwtOptions.SectionName)` into a `JwtOptions` instance, call `JwtOptionsGuard.ThrowIfInvalid(...)` on it immediately (fail fast at startup, not at first login), then `services.Configure<JwtOptions>(section)`.
3. `services.TryAddSingleton(TimeProvider.System)` — `TryAdd` so a test can register `FakeTimeProvider` first and win.
4. `services.AddIdentityCore<EnvanexUser>(options => ...)` with:
   - `options.User.RequireUniqueEmail = true`
   - `options.Password.RequiredLength = 12`, `RequireDigit = true`, `RequireLowercase = true`, `RequireUppercase = true`, `RequireNonAlphanumeric = false`
   - `options.Lockout.MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15)`, `AllowedForNewUsers = true`
   then `.AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<EnvanexIdentityDbContext>()`.
   **`AddIdentityCore`, not `AddIdentity`, and no `SignInManager`.** `SignInManager` lives in the `Microsoft.AspNetCore.Identity` shared-framework assembly and would force a `FrameworkReference` to `Microsoft.AspNetCore.App` into a `Microsoft.NET.Sdk` class library. `UserManager` alone covers credential check, lockout and failure counting.
5. `services.AddScoped<IIdentityService, IdentityService>();`
   `services.AddScoped<IRefreshTokenService, RefreshTokenService>();`
   `services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();`

### `IdentityService.ValidateCredentialsAsync` — exact behaviour *(Decisions 5 and 6)*

`ArgumentNullException.ThrowIfNull` on `email` and `password` (a null here is a caller bug). An empty or unknown value is a business-rule failure and returns `Result.Failure`.

**Every failure path returns the same `AuthErrors.InvalidCredentials` instance.** There is no branch that returns a different code, so there is no branch that can leak which failure occurred.

1. `FindByEmailAsync(email)`. Null → return `AuthErrors.InvalidCredentials` immediately. No password verification happens; this is the known-gap path described in Decision 6, and a test asserts the absence rather than leaving it to be discovered.
2. **`CheckPasswordAsync(user, password)` — always, unconditionally, before any lockout check.** This is the ordering change. Capture the boolean; do not act on it yet.
3. `IsLockedOutAsync(user)`.
4. Locked out → log `Warning` "Login attempt against locked-out account {UserId}" and return `AuthErrors.InvalidCredentials`. **Do not call `AccessFailedAsync`** and **do not call `ResetAccessFailedCountAsync`**, whether the password was right or wrong. Incrementing while locked out would let an attacker extend a legitimate user's lockout indefinitely by continuing to guess — a denial of service against the victim, not the attacker. Resetting would hand a locked-out account a way back in.
5. Not locked out, password false → `AccessFailedAsync(user)` **exactly once**. Then inspect the tracked `user.LockoutEnd` — `UserManager.AccessFailedAsync` updates the loaded entity in place, so no second round trip is needed — and if it is now in the future relative to `timeProvider.GetUtcNow()`, log `Warning` "Account {UserId} locked out after {MaxFailedAccessAttempts} failed attempts". Return `AuthErrors.InvalidCredentials`.
6. Not locked out, password true → `ResetAccessFailedCountAsync(user)` → `Result.Success(new AuthenticatedUser(user.Id, user.Email!, user.UserName!))`.

There is deliberately **no** post-increment `IsLockedOutAsync` re-check. Revision 2's design needed one to choose between two error codes; with one code there is nothing to choose, and removing it is what structurally guarantees `AccessFailedAsync` cannot fire twice on one attempt. `TimeProvider` is injected solely for the step-5 log condition.

Log messages carry the user id, never the email and never the password. Identity's own `Microsoft.AspNetCore.Identity` meter records the password-check attempt without any code here.

**Testing note the coder needs before starting.** `UserManager.IsLockedOutAsync` reads the system clock internally, not the injected `TimeProvider`. Advancing a `FakeTimeProvider` by 16 minutes therefore does **not** clear an Identity lockout, and no test in this plan attempts to assert lockout expiry over time. `ValidateCredentialsAsync_FifthWrongPassword_ShouldSetLockoutEndAboutFifteenMinutesAhead` asserts the stored `LockoutEnd` value instead, with a tolerance. Do not spend a cycle trying to make the fake clock drive Identity's lockout window; it cannot, and that is a framework property, not a defect to fix in 6a.

### `RefreshTokenService.RotateAsync` — exact behaviour

All reads go through `EnvanexIdentityDbContext`; no raw SQL.

1. `ArgumentNullException.ThrowIfNull(presentedToken)`; blank → `AuthErrors.InvalidRefreshToken`.
2. `RefreshTokenHasher.Hash(presentedToken)`, then a single-row query on `TokenHash` equality (the unique index seek).
3. Not found → `AuthErrors.InvalidRefreshToken`.
4. `RefreshTokenHasher.Matches(presentedToken, row.TokenHash)` false → `AuthErrors.InvalidRefreshToken`. Redundant against the index, but it makes the constant-time comparison an explicit property of the code rather than an accident of the query plan.
5. `RevokedAt is not null` → `AuthErrors.InvalidRefreshToken`.
6. `RotatedAt is not null` → **reuse**. Load every row with the same `FamilyId` and `RevokedAt IS NULL`, `Revoke(now, RefreshTokenRevocationReason.Reuse)` on each, `SaveChangesAsync`, return `AuthErrors.RefreshTokenReused`. The grace period is zero (research decision 7). **The consumed row is never deleted** — it stays resolvable to its family, which is what makes generation-1 replay after three rotations detectable.
7. `now >= ExpiresAt || now >= FamilyExpiresAt` → revoke the live rows of the family with reason `Expired`, save, return `AuthErrors.RefreshTokenExpired`.
8. Otherwise: generate a new token, build the child via `parent.CreateChild(...)`, `parent.MarkRotated(now, child.Id)`, add the child, one `SaveChangesAsync` covering both — a single transaction, so a failure leaves neither the stamp nor the child.
9. **Two loser paths, both returning `AuthErrors.InvalidRefreshToken`:**
   - `DbUpdateConcurrencyException` — another rotation or a revocation already changed the parent's `RowVersion`. This is the likelier of the two, because both racers read the same parent and both try to stamp it.
   - `DbUpdateException` whose inner is `SqlException { Number: 2601 or 2627 }` — the child insert hit `IX_RefreshTokens_FamilyId_Live` because a live sibling already exists.
   Detect the second with the same pattern `UnitOfWork` already uses. Do **not** route either through `IUnitOfWork`, which is bound to `EnvanexDbContext`.
10. Load the user for the `RotatedRefreshToken.User` field from `EnvanexIdentityDbContext` in the same round trip as step 2 where practical.

### `RefreshTokenService.RevokeFamilyAsync` — exact behaviour

Logout, research decision 8. It touches one family only; it never ends a user's other sessions.

1. Look up by hash. **Not found returns `Result.Success()`** — deliberately idempotent, so logout does not become an oracle for token existence.
2. Found → a bounded retry loop, at most `RevocationRetryLimit` (3) attempts. Each attempt loads every row of that `FamilyId` where `RevokedAt IS NULL`, calls `Revoke(now, RefreshTokenRevocationReason.Logout)` on each, and saves.
3. On `DbUpdateConcurrencyException`, discard the tracked entities and retry. This is not optional: a rotation that commits between this method's read and its write produces a **new live child that the first pass never saw**. That is exactly the "rotation racing revocation" trap in the research — the revocation succeeds and the child outlives it. The retry terminates because each pass observes a strictly later generation and a rotation cannot outrun a bounded loop indefinitely.
4. After a successful save, re-query for any remaining live row in the family. If one exists and attempts remain, loop. If one exists and attempts are exhausted, log `Error` "Failed to revoke refresh token family {FamilyId} after {Attempts} attempts" and return `Result.Failure(AuthErrors.InvalidRefreshToken)`. **A logout that cannot guarantee revocation must not report success**; the client sees a 401 and can retry, which is honest.
5. Otherwise `Result.Success()`.

The exhaustion branch in step 4 cannot be forced deterministically and therefore has no dedicated integration test; it is covered at the handler level by `LogoutCommandHandlerTests.HandleAsync_RevocationFailure_ShouldPropagateTheFailure`, and its intended outcome is asserted by `RefreshTokenConcurrencyTests.RevokeFamilyAsync_RacingARotation_ShouldLeaveNoLiveRowInTheFamily`.

### `JwtAccessTokenIssuer.Issue` — exact behaviour

`ArgumentNullException.ThrowIfNull(user)`. Claims: `sub` = `user.Id`, `email` = `user.Email`, `jti` = a fresh `Guid.NewGuid()`, plus `iss`, `aud`, `iat`, `nbf`, `exp`. `exp` = `timeProvider.GetUtcNow() + TimeSpan.FromMinutes(options.AccessTokenMinutes)`. Signed HS256 with a `SymmetricSecurityKey` over `Encoding.UTF8.GetBytes(options.SigningKey)`, produced by `JsonWebTokenHandler.CreateToken`. Registered as a singleton, so the `SigningCredentials` are built once.

### Tests to add

All in `tests/Envanex.IntegrationTests/Identity/`, `[Collection(DatabaseCollection.Name)]`, each class calling `_fixture.ResetIdentityAsync()` in `InitializeAsync`. Services are resolved from a `ServiceProvider` built the same way `DependencyInjectionTests.BuildProvider()` does, with `FakeTimeProvider` registered before `AddInfrastructure` so `TryAddSingleton` yields to it, and `CountingUserManager` registered after so it replaces Identity's `UserManager`.

`IdentityServiceTests`:
- `ValidateCredentialsAsync_CorrectPassword_ShouldReturnAuthenticatedUserWithMatchingId`
- `ValidateCredentialsAsync_UnknownEmail_ShouldReturnInvalidCredentials`
- `ValidateCredentialsAsync_WrongPassword_ShouldReturnInvalidCredentials`
- `ValidateCredentialsAsync_WrongPassword_ShouldIncrementAccessFailedCount`
- `ValidateCredentialsAsync_FifthWrongPassword_ShouldReturnInvalidCredentials` *(Decision 5)*
- `ValidateCredentialsAsync_FifthWrongPassword_ShouldSetLockoutEndAboutFifteenMinutesAhead` — the mechanism is still real even though the response never says so. Asserts the stored value with a tolerance, not elapsed time; see the testing note above.
- `ValidateCredentialsAsync_CorrectPasswordWhileLockedOut_ShouldReturnInvalidCredentials` *(Decision 5)*
- `ValidateCredentialsAsync_CorrectPasswordWhileLockedOut_ShouldLeaveAccessFailedCountUnchanged`
- `ValidateCredentialsAsync_UnknownEmailWrongPasswordAndLockedOut_ShouldAllReturnTheSameErrorCode` — the service-level half of Decision 5; Phase 6 asserts the same property over the wire.
- `ValidateCredentialsAsync_LockedOut_ShouldLogAWarningCarryingTheUserId`
- `ValidateCredentialsAsync_LockedOut_ShouldNotLogTheEmailOrPassword`
- `ValidateCredentialsAsync_CorrectPasswordAfterTwoFailures_ShouldResetAccessFailedCountToZero`
- `ValidateCredentialsAsync_NullEmail_ShouldThrowArgumentNullException`
- `ValidateCredentialsAsync_NullPassword_ShouldThrowArgumentNullException`

`IdentityServiceTimingOrderTests` — **new class** (Decision 6), all assertions driven by `CountingUserManager` rather than by a stopwatch, because a timing assertion on CI is a flake generator and an ordering assertion is not:
- `ValidateCredentialsAsync_LockedOutUser_ShouldStillCallCheckPasswordAsyncExactlyOnce` — the core of Decision 6. If this fails, the locked-out path has gone back to short-circuiting and the ~100 ms step change is back.
- `ValidateCredentialsAsync_LockedOutUser_ShouldCallCheckPasswordAsyncBeforeIsLockedOutAsync` — asserts `CallLog` begins `["CheckPasswordAsync", "IsLockedOutAsync"]`. The ordering, not merely the fact that both ran.
- `ValidateCredentialsAsync_LockedOutUserWithTheCorrectPassword_ShouldStillCallCheckPasswordAsyncExactlyOnce`
- `ValidateCredentialsAsync_LockedOutUser_ShouldNotCallAccessFailedAsync` — a locked-out account must not have its lockout extended by further guessing.
- `ValidateCredentialsAsync_LockedOutUserWithTheCorrectPassword_ShouldNotCallResetAccessFailedCountAsync`
- `ValidateCredentialsAsync_WrongPassword_ShouldCallAccessFailedAsyncExactlyOnce` — the double-increment guard.
- `ValidateCredentialsAsync_FifthWrongPassword_ShouldCallAccessFailedAsyncExactlyOnce` — the attempt that trips the lockout is the one the removed post-increment re-check used to put at risk; asserted separately because it is the only attempt whose behaviour differs.
- `ValidateCredentialsAsync_CorrectPassword_ShouldNotCallAccessFailedAsync`
- `ValidateCredentialsAsync_CorrectPassword_ShouldCallResetAccessFailedCountAsyncExactlyOnce`
- `ValidateCredentialsAsync_UnknownEmail_ShouldNotCallCheckPasswordAsync` — documents the remaining unknown-email gap as a deliberate, asserted behaviour. When PR 6b or later closes it with a dummy verification, this is the test that must be inverted, and inverting a named test is a decision; silently changing an unasserted behaviour is not.

`RefreshTokenServiceTests`:
- `IssueAsync_ShouldPersistOnlyTheHashAndNeverThePlaintextToken` — reads the row back and asserts no column contains the returned token string.
- `IssueAsync_ShouldSetExpiresAtToSevenDaysAfterFakeNow` *(Decision 1)*
- `IssueAsync_ShouldSetFamilyExpiresAtToThirtyDaysAfterFakeNow` *(Decisions 1 and 2)*
- `IssueAsync_CalledTwiceForOneUser_ShouldProduceTwoIndependentFamilies`
- `RotateAsync_ValidToken_ShouldReturnADifferentTokenString`
- `RotateAsync_ValidToken_ShouldStampParentRotatedAtAndReplacedByTokenId`
- `RotateAsync_ValidToken_ShouldNotDeleteTheConsumedRow`
- `RotateAsync_ValidToken_ShouldKeepTheSameFamilyId`
- `RotateAsync_ValidToken_ShouldCarryFamilyExpiresAtForwardUnchanged` *(Decision 2)*
- `RotateAsync_ValidToken_ShouldResetTheIdleWindowToSevenDays` *(Decisions 1 and 2)*
- `RotateAsync_ValidToken_ShouldReturnTheOwningUser`
- `RotateAsync_UnknownToken_ShouldReturnInvalidRefreshToken`
- `RotateAsync_BlankToken_ShouldReturnInvalidRefreshToken`
- `RotateAsync_NullToken_ShouldThrowArgumentNullException`
- `RotateAsync_ReplayOfGenerationOneAfterASingleRotation_ShouldReturnRefreshTokenReused` — the shallow case.
- `RotateAsync_ReplayOfGenerationOneAfterThreeRotations_ShouldReturnRefreshTokenReusedAndRevokeTheWholeFamilyAndRejectTheLiveToken` — **self-contained.** One test, four assertions, no reliance on sibling cases: (a) three successive rotations all succeed, producing generations 2, 3 and 4; (b) replaying the generation-1 token returns `AuthErrors.RefreshTokenReused`; (c) every row carrying that `FamilyId` has a non-null `RevokedAt` with reason `Reuse`; (d) a subsequent `RotateAsync` with the generation-4 token — which was live a moment earlier — returns `AuthErrors.InvalidRefreshToken`. This is the RFC 9700 §4.14.2 requirement and the direct regression for the research's "detection dies after one generation" trap; a depth regression that depended on a neighbouring test would go green the moment that neighbour was deleted.
- `RotateAsync_AfterReuseDetection_ShouldNotTouchTheUsersOtherFamilies`
- `RotateAsync_TokenPastTheIdleWindow_ShouldReturnRefreshTokenExpired` — advance `FakeTimeProvider` by 8 days *(Decision 1)*
- `RotateAsync_TokenRotatedEveryFiveDaysButPastTheThirtyDayCap_ShouldReturnRefreshTokenExpired` — rotate on days 5, 10, 15, 20, 25 (each rotation resets the 7-day idle window, so the idle window is never the reason), then attempt on day 31 *(Decisions 1 and 2)*
- `RotateAsync_ExpiredToken_ShouldRevokeTheFamily`
- `RotateAsync_RevokedToken_ShouldReturnInvalidRefreshToken`
- `RevokeFamilyAsync_ValidToken_ShouldRevokeEveryLiveRowInThatFamily`
- `RevokeFamilyAsync_ShouldNotTouchOtherFamiliesOfTheSameUser` — research decision 8.
- `RevokeFamilyAsync_UnknownToken_ShouldReturnSuccess`
- `RevokeFamilyAsync_CalledTwice_ShouldReturnSuccessBothTimes`
- `RotateAsync_AfterRevokeFamily_ShouldReturnInvalidRefreshToken`

`RefreshTokenConcurrencyTests` — `[Collection(DatabaseCollection.Name)]`, run against the real SQL Server container. `EnvanexIdentityDbContext` is not thread-safe, so each concurrent task must resolve its **own** `IServiceScope` and therefore its own context and its own `RefreshTokenService`; a shared instance would fail with `InvalidOperationException: A second operation was started on this context` and would prove nothing about the database. `FakeTimeProvider` is shared read-only across the tasks.
- `RotateAsync_TwoConcurrentRotationsOfTheSameToken_ShouldLetExactlyOneSucceed`
- `RotateAsync_TwoConcurrentRotationsOfTheSameToken_ShouldReturnInvalidRefreshTokenForTheLoser` — the other result is a failure carrying `AuthErrors.InvalidRefreshToken`, not an escaped `DbUpdateException` or `DbUpdateConcurrencyException`. This is the test that makes path 9 a real branch rather than an untested catch that merely looks like protection.
- `RotateAsync_TwoConcurrentRotationsOfTheSameToken_ShouldLeaveExactlyOneLiveRowInTheFamily` — counts rows with that `FamilyId` where `RotatedAt IS NULL AND RevokedAt IS NULL` and asserts exactly 1.
- `RotateAsync_TwoConcurrentRotationsOfTheSameToken_ShouldNotLeaveTheParentUnstamped` — the winner's parent has `RotatedAt` and `ReplacedByTokenId` set, and `ReplacedByTokenId` points at the single live row.
- `RevokeFamilyAsync_RacingARotation_ShouldLeaveNoLiveRowInTheFamily` — `RotateAsync` and `RevokeFamilyAsync` on the same token in two scopes via `Task.WhenAll`; afterwards no row with that `FamilyId` has `RotatedAt IS NULL AND RevokedAt IS NULL`, regardless of which side won. This is the assertion the bounded retry loop exists to satisfy, and the direct regression for the research's "a child written after a concurrent revocation outlives it" trap.
- `RevokeFamilyAsync_RacingARotation_ShouldNotThrow` — neither task surfaces an exception; both return a `Result`.

Because SQL Server interleaving is not deterministic, each of these must execute a loop of at least 20 iterations, each on a freshly issued token, so a single lucky serialisation cannot make the test pass. The iteration count is a constant named `ConcurrencyIterations` on the test class, so the human can turn it down without hunting through six methods if the measured cost below comes back unacceptable.

`JwtAccessTokenIssuerTests`:
- `Issue_ShouldProduceATokenCarryingSubEmailAndJti`
- `Issue_ShouldSetExpToFifteenMinutesAfterFakeNow` *(Decision 1)*
- `Issue_ShouldSetIssuerAndAudienceFromConfiguration`
- `Issue_CalledTwice_ShouldProduceDifferentJtiValues`
- `Issue_ShouldValidateAgainstTheConfiguredSigningKey` — `JsonWebTokenHandler.ValidateTokenAsync` with `ClockSkew = TimeSpan.Zero` succeeds.
- `Issue_ShouldFailValidationAgainstADifferentSigningKey`
- `Issue_ShouldUseHs256`
- `Issue_NullUser_ShouldThrowArgumentNullException`

`AddEnvanexIdentityTests`:
- `AddEnvanexIdentity_BlankSigningKey_ShouldThrowInvalidOperationExceptionNamingTheUserSecretsCommand`
- `AddEnvanexIdentity_SigningKeyShorterThan32Bytes_ShouldThrow`
- `AddEnvanexIdentity_MissingJwtSection_ShouldThrow`
- `AddEnvanexIdentity_ShouldResolveIIdentityService`
- `AddEnvanexIdentity_ShouldResolveIRefreshTokenService`
- `AddEnvanexIdentity_ShouldResolveIAccessTokenIssuer`
- `AddEnvanexIdentity_ShouldResolveUserManagerOfEnvanexUser`
- `AddEnvanexIdentity_ShouldConfigureMaxFailedAccessAttemptsAsFive` — resolves `IOptions<IdentityOptions>`; Decision 5 hides lockout from the response, so this is the only place the setting is visible.
- `AddEnvanexIdentity_ShouldConfigureDefaultLockoutTimeSpanAsFifteenMinutes`
- `AddEnvanexIdentity_ShouldNotOverrideAPreRegisteredTimeProvider` — proves the `TryAddSingleton`.
- `AddEnvanexIdentity_WithNoTimeProviderRegistered_ShouldResolveTimeProviderSystem`

`DependencyInjectionTests` — modified: `AllRegisteredServices_ShouldResolve` gains `IIdentityService`, `IRefreshTokenService` and `IAccessTokenIssuer` assertions.

### Validation

```
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test tests/Envanex.IntegrationTests --filter "FullyQualifiedName~RefreshTokenConcurrencyTests"
dotnet test tests/Envanex.IntegrationTests
dotnet test
```

The phase report must include:
1. The SQL `RotateAsync` generates for the hash lookup and for the family revocation — capture it by raising `Microsoft.EntityFrameworkCore.Database.Command` to `Information` for one test run — so db-reviewer can confirm the lookup is an index seek on `IX_RefreshTokens_TokenHash` and not a scan.
2. Which exception fired on the concurrent-rotation loser path across the iterations: `DbUpdateConcurrencyException`, the 2601/2627 unique violation, or both. db-reviewer needs that to judge whether `IX_RefreshTokens_FamilyId_Live` is genuinely load-bearing or entirely shadowed by the `RowVersion` token. If the unique violation never fires in 20 iterations the index may still be correct as a second guarantee, but the human should be told rather than left to assume.
3. **The measured cost of the concurrency loops, as numbers, not a warning.** Report three figures from the two `dotnet test` runs above:
   - the wall-clock duration of `RefreshTokenConcurrencyTests` alone;
   - the wall-clock duration of the whole `Envanex.IntegrationTests` suite after this phase;
   - the delta against the 24 s baseline recorded in the research file.
   State the value of `ConcurrencyIterations` used. If `RefreshTokenConcurrencyTests` alone exceeds 60 s, stop and report rather than absorbing it — the iteration count is a knob the human turns, not one the coder quietly lowers to make the number look better.

---

## Phase 5: Login / refresh / logout use cases in the Application layer

**No schema, no migrations, no queries. db-reviewer not required.**
Depends on Decision 5.

Per research decision 2 these are ordinary `ICommandHandler` implementations with validators, decorator registration and DI test entries, so auth is not the one use case that skips the layer ADR 0005 describes.

### Files

- `src/Envanex.Application/Authentication/Commands/LoginCommand.cs` — created
- `src/Envanex.Application/Authentication/Commands/LoginCommandHandler.cs` — created
- `src/Envanex.Application/Authentication/Commands/RefreshTokenCommand.cs` — created
- `src/Envanex.Application/Authentication/Commands/RefreshTokenCommandHandler.cs` — created
- `src/Envanex.Application/Authentication/Commands/LogoutCommand.cs` — created
- `src/Envanex.Application/Authentication/Commands/LogoutCommandHandler.cs` — created
- `src/Envanex.Application/Authentication/Validators/LoginCommandValidator.cs` — created
- `src/Envanex.Application/Authentication/Validators/RefreshTokenCommandValidator.cs` — created
- `src/Envanex.Application/Authentication/Validators/LogoutCommandValidator.cs` — created
- `src/Envanex.Application/DependencyInjection.cs` — modified — three validators as singletons, three concrete handlers as scoped, three `ICommandHandler<,>` registrations wrapped in `ValidationDecorator`, following the existing hand-written shape exactly.
- `tests/Envanex.Application.Tests/Fakes/FakeIdentityService.cs` — created
- `tests/Envanex.Application.Tests/Fakes/FakeRefreshTokenService.cs` — created
- `tests/Envanex.Application.Tests/Fakes/FakeAccessTokenIssuer.cs` — created
- `tests/Envanex.IntegrationTests/DependencyInjectionTests.cs` — modified — three new resolve assertions and three new `CommandHandlerTypes()` entries. Per the research, a command handler missing from `CommandHandlerTypes()` is an unvalidated handler that passes silently.

### Signatures

```csharp
public sealed record LoginCommand(string Email, string Password);
public sealed record RefreshTokenCommand(string RefreshToken);
public sealed record LogoutCommand(string RefreshToken);

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, AuthenticationResponse>
{
    public LoginCommandHandler(
        IIdentityService identityService,
        IRefreshTokenService refreshTokenService,
        IAccessTokenIssuer accessTokenIssuer);

    public Task<Result<AuthenticationResponse>> HandleAsync(LoginCommand command, CancellationToken ct = default);
}

public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, AuthenticationResponse>
{
    public RefreshTokenCommandHandler(
        IRefreshTokenService refreshTokenService,
        IAccessTokenIssuer accessTokenIssuer);

    public Task<Result<AuthenticationResponse>> HandleAsync(RefreshTokenCommand command, CancellationToken ct = default);
}

public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand, bool>
{
    public LogoutCommandHandler(IRefreshTokenService refreshTokenService);
    public Task<Result<bool>> HandleAsync(LogoutCommand command, CancellationToken ct = default);
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>;
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>;
public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>;
```

`LoginCommandHandler` order: `ThrowIfNull(command)`; validate credentials; on failure return that error **without issuing a refresh token**; on success issue the refresh token, then the access token, then compose `AuthenticationResponse` with `TokenType = "Bearer"`. The handler does not inspect or reclassify the error it gets back — Decisions 5 and 6 live in `IdentityService`, and a handler that tried to add nuance here would undo them.

Validator error codes: `LoginCommandValidator` uses `.WithErrorCode("Auth.EmailRequired")` on `NotEmpty`, `.WithErrorCode("Auth.EmailInvalid")` on `EmailAddress`, `.WithErrorCode("Auth.PasswordRequired")` on password `NotEmpty`. Both token validators use `.WithErrorCode("Auth.RefreshTokenRequired")`. No maximum-length rule on the password: truncating a passphrase at validation time is a worse failure than letting `CheckPasswordAsync` reject it.

### Tests to add

`tests/Envanex.Application.Tests/Authentication/Commands/LoginCommandHandlerTests.cs`:
- `HandleAsync_ValidCredentials_ShouldReturnAnAccessTokenAndARefreshToken`
- `HandleAsync_ValidCredentials_ShouldReturnBearerAsTheTokenType`
- `HandleAsync_ValidCredentials_ShouldReturnBothExpiryTimestamps`
- `HandleAsync_InvalidCredentials_ShouldReturnAuthInvalidCredentials`
- `HandleAsync_InvalidCredentials_ShouldNotIssueARefreshToken`
- `HandleAsync_InvalidCredentials_ShouldNotIssueAnAccessToken`
- `HandleAsync_LockedOutUser_ShouldReturnAuthInvalidCredentials` *(Decision 5 — the fake returns `InvalidCredentials` for the locked-out case because that is what the real service returns)*
- `HandleAsync_NullCommand_ShouldThrowArgumentNullException`

`tests/Envanex.Application.Tests/Authentication/Commands/RefreshTokenCommandHandlerTests.cs`:
- `HandleAsync_ValidRefreshToken_ShouldReturnANewRefreshToken`
- `HandleAsync_ValidRefreshToken_ShouldReturnANewAccessTokenForTheRotatedUser`
- `HandleAsync_ReusedRefreshToken_ShouldReturnAuthRefreshTokenReused`
- `HandleAsync_ExpiredRefreshToken_ShouldReturnAuthRefreshTokenExpired`
- `HandleAsync_UnknownRefreshToken_ShouldReturnAuthInvalidRefreshToken`
- `HandleAsync_RotationFailure_ShouldNotIssueAnAccessToken`
- `HandleAsync_NullCommand_ShouldThrowArgumentNullException`

`tests/Envanex.Application.Tests/Authentication/Commands/LogoutCommandHandlerTests.cs`:
- `HandleAsync_ValidRefreshToken_ShouldCallRevokeFamilyOnce`
- `HandleAsync_ValidRefreshToken_ShouldReturnSuccess`
- `HandleAsync_UnknownRefreshToken_ShouldReturnSuccess`
- `HandleAsync_RevocationFailure_ShouldPropagateTheFailure` — covers the exhausted-retry branch specified in Phase 4 step 4; the handler must not convert it into a success.
- `HandleAsync_NullCommand_ShouldThrowArgumentNullException`

`tests/Envanex.Application.Tests/Authentication/Validators/LoginCommandValidatorTests.cs`:
- `Validate_EmptyEmail_ShouldFailWithAuthEmailRequired`
- `Validate_MalformedEmail_ShouldFailWithAuthEmailInvalid`
- `Validate_EmptyPassword_ShouldFailWithAuthPasswordRequired`
- `Validate_ValidCommand_ShouldPass`

`tests/Envanex.Application.Tests/Authentication/Validators/RefreshTokenCommandValidatorTests.cs`:
- `Validate_EmptyRefreshToken_ShouldFailWithAuthRefreshTokenRequired`
- `Validate_WhitespaceRefreshToken_ShouldFailWithAuthRefreshTokenRequired`
- `Validate_ValidCommand_ShouldPass`

`tests/Envanex.Application.Tests/Authentication/Validators/LogoutCommandValidatorTests.cs`:
- `Validate_EmptyRefreshToken_ShouldFailWithAuthRefreshTokenRequired`
- `Validate_ValidCommand_ShouldPass`

`tests/Envanex.IntegrationTests/DependencyInjectionTests.cs` — modified:
- `AllRegisteredServices_ShouldResolve` gains the three `ICommandHandler<,>` assertions.
- `CommandHandlerTypes()` gains three rows: `ICommandHandler<LoginCommand, AuthenticationResponse>` → `ValidationDecorator<LoginCommand, AuthenticationResponse>`, and the equivalents for `RefreshTokenCommand` and `LogoutCommand`.

### Validation

```
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

---

## Phase 6: Web host — endpoints, JWT bearer scheme, pipeline placement, login rate limit

**No schema, no migrations, no queries. db-reviewer not required.**
Depends on Decisions 4, 5 and 6.

### Files

- `src/Envanex.Web/Envanex.Web.csproj` — modified — add `<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />`.
- `src/Envanex.Web/Extensions/JwtAuthenticationExtensions.cs` — created.
- `src/Envanex.Web/RateLimiting/LoginRateLimitPartition.cs` — created — the single named edit site PR 7 will change *(Decision 4)*.
- `src/Envanex.Web/Controllers/AuthController.cs` — created.
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — modified — add `ToNoContentActionResult<T>`.
- `src/Envanex.Web/Program.cs` — modified — see below.
- `src/Envanex.Web/appsettings.json` — modified — add `"Login": { "Enabled": true, "PermitLimit": 5, "WindowSeconds": 300 }` inside the existing `RateLimiting` section.
- `tests/Envanex.IntegrationTests/Fixtures/LoginRateLimitedWebApplicationFactory.cs` — created.
- `docs/roadmap.md` — modified — mark PR 6a done; record the gaps this PR knowingly leaves, with the first one marked **BLOCKER**:
  - **BLOCKER (PR 7) — login rate-limit partition key behind a reverse proxy.** Azure App Service terminates TLS at a front end and puts the client address in `X-Forwarded-For`; `LoginRateLimitPartition.GetKey` reads `Connection.RemoteIpAddress`, which on first deploy is the proxy's address for every request. All users collapse into one partition and 5 requests per 5 minutes becomes a global limit — login stops working for everyone after three people sign in. Closes in PR 7 by configuring `UseForwardedHeaders` with the real `KnownProxies`/`KnownNetworks` for the deployment, and by deleting `LoginRateLimitPartitionTests.GetKey_ShouldIgnoreXForwardedForUntilPr7`. **Must be closed before the first public deploy.**
  - Login timing side channel, unknown-email half (Decision 6): `FindByEmailAsync` returning null skips password verification, so a non-existent account answers in ~1 ms where a real one takes ~100 ms. Needs a dummy verification against a fixed throwaway hash. The locked-out half is closed in 6a.
  - Login timing side channel, residual write (Decision 6): a wrong password performs one `AccessFailedAsync` `UPDATE` that a locked-out attempt does not — roughly a millisecond against ~100 ms of shared PBKDF2, but not zero.
  - Zero reuse-detection grace period (research decision 7).
  - No `JwtBearerEvents.OnChallenge` ProblemDetails body until 6b.
  - No refresh-token pruning job — the table is append-only and grows without bound.
  - `SqlServerFixture` reset still hand-maintained (6a handled the auth half, PR 8 owns the full fix, per research decision 5).
  - Passkeys deferred.
- `docs/adr/0007-jwt-access-tokens-and-rotated-refresh-tokens.md` — created **empty, title line only**. The human writes the body.

### Signatures

```csharp
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddEnvanexJwtBearer(
        this IServiceCollection services,
        IConfiguration configuration);
}

internal static class LoginRateLimitPartition
{
    /// <summary>
    /// Partition key used when the connection carries no remote address, which is the
    /// case under WebApplicationFactory and — until PR 7 configures forwarded headers —
    /// would also be the effective case behind a reverse proxy.
    /// </summary>
    public const string UnknownPartitionKey = "unknown";

    public static string GetKey(HttpContext context);
}

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    public AuthController(
        ICommandHandler<LoginCommand, AuthenticationResponse> loginHandler,
        ICommandHandler<RefreshTokenCommand, AuthenticationResponse> refreshHandler,
        ICommandHandler<LogoutCommand, bool> logoutHandler);

    [HttpPost("login")]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    public Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct);

    [HttpPost("refresh")]
    public Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command, CancellationToken ct);

    [HttpPost("logout")]
    public Task<IActionResult> Logout([FromBody] LogoutCommand command, CancellationToken ct);

    public const string LoginRateLimitPolicy = "login";
}

// ResultExtensions
public static IActionResult ToNoContentActionResult<T>(this Result<T> result);
```

`LoginRateLimitPartition.GetKey` calls `ArgumentNullException.ThrowIfNull(context)` and returns `context.Connection.RemoteIpAddress?.ToString() ?? UnknownPartitionKey`. It deliberately does **not** read `X-Forwarded-For`: an unvalidated forwarded header is attacker-controlled, and honouring it without `KnownProxies` would let an attacker mint a fresh partition per request and escape the limiter entirely — strictly worse than the shared-bucket failure it would appear to fix. See Decision 4.

`AddEnvanexJwtBearer` binds `Jwt:*` into a `JwtOptions`, calls `JwtOptionsGuard.ThrowIfInvalid`, then `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = ...)` with `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime` and `ValidateIssuerSigningKey` all true, and **`ClockSkew = TimeSpan.Zero`**. The default five-minute skew would make a 15-minute access token effectively last 20 and would make every `FakeTimeProvider`-driven expiry test lie.

There is deliberately **no `JwtBearerEvents.OnChallenge`** in this phase. No endpoint is `[Authorize]` in 6a, so no challenge is reachable and such a handler would be untestable dead code. It ships with the first `[Authorize]` in 6b.

`Logout` returns `result.ToNoContentActionResult()` — 204 on success, the mapped ProblemDetails on failure.

### `Program.cs` — exact changes

1. After `builder.Services.AddInfrastructure(builder.Configuration);` add `builder.Services.AddEnvanexJwtBearer(builder.Configuration);` and `builder.Services.AddAuthorization();`.
2. Rate limiter restructure (Decision 4): move `builder.Services.AddRateLimiter(options => { ... })` **out** of `if (rateLimitingEnabled)`. Inside the lambda, keep `RejectionStatusCode` and `OnRejected` exactly as they are; assign `options.GlobalLimiter` **only** when `rateLimitingEnabled`; and always register the named policy `AuthController.LoginRateLimitPolicy`, reading `RateLimiting:Login:{Enabled,PermitLimit,WindowSeconds}`. When `RateLimiting:Login:Enabled` is false the policy resolves to `RateLimitPartition.GetNoLimiter`. When true it is a fixed-window limiter whose partition key comes from `LoginRateLimitPartition.GetKey(context)` — a call, not an inline lambda, so PR 7 changes one method and the tests that pin its behaviour are already named.
3. Make `app.UseRateLimiter();` unconditional, in the same position it occupies today (after `UseMiddleware<SecurityHeadersMiddleware>()`, before `UseStatusCodePagesWithReExecute`). The named policy must be visible to the middleware or `[EnableRateLimiting("login")]` throws at endpoint build time.
4. Insert, **after `app.UseHttpsRedirection();` and before `app.UseAntiforgery();`**:
   ```
   app.UseAuthentication();
   app.UseAuthorization();
   ```

Why that position, explicitly:
- **After `UseHttpsRedirection()`** — there is no point authenticating a request that is about to be 307'd, and a bearer token should not be parsed off a plaintext request.
- **Before `UseAntiforgery()` and the endpoint mappings** — antiforgery, endpoint routing, the Blazor circuit and MVC filters all read `HttpContext.User`; if authentication runs after them the user is anonymous everywhere that matters.
- **Inside the `UseStatusCodePagesWithReExecute("/not-found", ...)` wrapper**, which is the position that carries the documented risk: a bodiless 401 produced under that wrapper is re-executed as the not-found page, exactly the failure ADR 0006 records for the rate limiter. In 6a no bodiless 401 is reachable, because every 401 comes from `ResultExtensions` and carries a ProblemDetails body. `AuthPipelineTests.Refresh_WithUnknownToken_ShouldReturn401ProblemJsonAndNotTheNotFoundPage` is the test that locks that in. The remaining case — a bodiless challenge 401 — becomes reachable in 6b along with the `OnChallenge` body that fixes it, and is recorded in `docs/roadmap.md` as a known gap.

`LoginRateLimitedWebApplicationFactory` sets: `ConnectionStrings:EnvanexDb`, the six `Jwt:*` keys, `RateLimiting:Enabled=false` (so the global limiter stays off and cannot mask the result), `RateLimiting:Login:Enabled=true`, `RateLimiting:Login:PermitLimit=2`, `RateLimiting:Login:WindowSeconds=60`.

### Tests to add

`tests/Envanex.IntegrationTests/Api/AuthApiTests.cs` — new, `[Collection(DatabaseCollection.Name)]`, `InitializeAsync` calls `ResetIdentityAsync()` and seeds one user via `IdentitySeeder.CreateUserAsync(fixture.WebApplicationFactory.Services, ...)`:
- `Login_WithValidCredentials_ShouldReturn200`
- `Login_WithValidCredentials_ShouldReturnAccessTokenRefreshTokenAndBothExpiries`
- `Login_WithValidCredentials_ShouldReturnBearerAsTokenType`
- `Login_WithValidCredentials_ShouldReturnAnAccessTokenThatValidatesAgainstTheConfiguredKey`
- `Login_WithWrongPassword_ShouldReturn401ProblemJson`
- `Login_WithUnknownEmail_ShouldReturn401ProblemJson`
- `Login_AfterFiveWrongPasswords_ShouldReturn401` *(Decision 5)*
- `Login_AfterFiveWrongPasswords_ShouldAlsoReturn401ForTheCorrectPassword` — the lockout is real; the response simply never admits it.
- `Login_UnknownEmailWrongPasswordAndLockedOutAccount_ShouldReturnIdenticalStatusContentTypeAndBody` — **the Decision 5 test.** Issues all three requests, then asserts the three `StatusCode` values are equal, the three `Content-Type` header values are equal, and the three raw response bodies are byte-for-byte equal. Any future change that gives lockout its own message, its own status or its own `detail` fails here.
- `Login_FailureDetail_ShouldMentionTheLockoutPolicyWithoutConfirmingIt` — *(Decision 6)* parses the ProblemDetails `detail` from a wrong-password response and asserts it contains "kilitlenir" and does not contain "kilitlendi". The end-to-end confirmation that the Decision 6 wording actually reaches the client and was not lost to a fallback path.
- `Login_WithEmptyPassword_ShouldReturn400WithTheTurkishParolaRequiredMessage`
- `Login_WithMalformedEmail_ShouldReturn400WithTheTurkishEmailInvalidMessage`
- `Login_ShouldNotSetAnySetCookieHeader` — research decision 9: the refresh token travels in the body, not a cookie.
- `Refresh_WithTheTokenFromLogin_ShouldReturn200`
- `Refresh_WithTheTokenFromLogin_ShouldReturnADifferentRefreshToken`
- `Refresh_WithTheTokenFromLogin_ShouldReturnADifferentAccessToken`
- `Refresh_ReplayingAConsumedToken_ShouldReturn401`
- `Refresh_ReplayingAConsumedToken_ShouldAlsoInvalidateTheCurrentToken` — the end-to-end proof of RFC 9700 §4.14.2 through HTTP.
- `Logout_WithAValidToken_ShouldReturn204`
- `Logout_WithAValidToken_ThenRefresh_ShouldReturn401`
- `Logout_WithAnUnknownToken_ShouldReturn204`
- `Logout_ShouldNotAffectASecondSessionOfTheSameUser` — research decision 8.
- `Register_ShouldReturn404` — research decision 11 says 6a has no user-creating endpoint; `POST /api/auth/register` must 404, asserting one did not sneak in.

`tests/Envanex.IntegrationTests/Api/AuthPipelineTests.cs` — new, `[Collection(DatabaseCollection.Name)]`:
- `Refresh_WithUnknownToken_ShouldReturn401ProblemJsonAndNotTheNotFoundPage` — **the pipeline-placement test.** Asserts status 401, `Content-Type` starting `application/problem+json`, body does not contain `<html` (case-insensitive), and the parsed `status` field is 401 — the same assertion shape `RateLimiterTests` already uses for the 429 case.
- `OpenEndpoint_WithNoAuthorizationHeader_ShouldStillReturn200` — `UseAuthentication` must not close an endpoint that 6a leaves open.
- `OpenEndpoint_WithAMalformedBearerToken_ShouldStillReturn200` — a garbage token must be ignored, not turned into a 401, before 6b introduces `[Authorize]`.
- `OpenEndpoint_WithAnExpiredBearerToken_ShouldStillReturn200`
- `OpenEndpoint_WithAValidBearerToken_ShouldStillReturn200`

`tests/Envanex.IntegrationTests/Api/LoginRateLimitPartitionTests.cs` — **new**, no database, no `[Collection]` *(Decision 4)*:
- `GetKey_WithARemoteIpAddress_ShouldReturnThatAddress` — `DefaultHttpContext` with `Connection.RemoteIpAddress` set.
- `GetKey_WithNoRemoteIpAddress_ShouldReturnTheUnknownPartitionKey` — the null case stated directly rather than inferred from HTTP behaviour.
- `GetKey_ShouldIgnoreXForwardedForUntilPr7` — sets `X-Forwarded-For` to an arbitrary address and asserts the key is unchanged. **This test is the PR 7 tripwire**: it documents that the header is deliberately not honoured yet, and PR 7 must delete it as part of configuring `UseForwardedHeaders`. A blocker that only lives in a markdown file is a blocker nobody trips over.
- `GetKey_NullContext_ShouldThrowArgumentNullException`

`tests/Envanex.IntegrationTests/Api/LoginRateLimiterTests.cs` — new, uses `LoginRateLimitedWebApplicationFactory` *(Decision 4)*:
- `Login_ExceedingTheLoginRateLimit_ShouldReturn429ProblemJson`
- `Login_ExceedingTheLoginRateLimit_ShouldIncludeARetryAfterHeader`
- `Login_UnderTheLoginRateLimit_ShouldReturn401ForAWrongPassword` — proves the policy is not rejecting everything.
- `Login_FromTwoClientsOfTheSameFactory_ShouldShareTheSameRateLimitPartition` — two `HttpClient` instances from one factory; the third request across both is rejected. The observable consequence of the `"unknown"` fallback, and the same behaviour that becomes the PR 7 blocker in production.
- `ProductsEndpoint_ShouldNotBeAffectedByTheLoginPolicy` — proves the policy is scoped to the login action and has not leaked to the global limiter.

`tests/Envanex.IntegrationTests/Api/RateLimiterTests.cs` — modified, the regression pair for the Decision 4 restructure:
- `SharedFactory_WithGlobalLimiterDisabled_ShouldNotRegisterAGlobalLimiter` — the structural check. Resolves `IOptions<RateLimiterOptions>` from `fixture.WebApplicationFactory.Services` and asserts `GlobalLimiter is null`. Proves the `if (rateLimitingEnabled)` guard around the assignment survived the move of `AddRateLimiter` out of the conditional.
- `SharedFactory_WithGlobalLimiterDisabled_ShouldNotReject150ConsecutiveRequests` — the behavioural check. 150 is chosen deliberately: it exceeds the production `PermitLimit` of 100, so if a global limiter were accidentally active with its default limit the test fails. A smaller number would pass even with the limiter on and would prove nothing. Together these two are what stop the existing 107 integration tests from quietly starting to see 429s.
- The three existing cases stay unchanged and must still pass.

`tests/Envanex.IntegrationTests/Api/ResultExtensionsTests.cs` — modified:
- `ToNoContentActionResult_Success_ShouldReturn204`
- `ToNoContentActionResult_Failure_ShouldReturnTheMappedProblemDetails`
- `ToNoContentActionResult_NullResult_ShouldThrowArgumentNullException`

`tests/Envanex.Domain.Tests/ArchitectureTests.cs` — modified:
- `Web_ShouldReference_JwtBearerPackage` — positive control that the JwtBearer package landed in Web and not in Application or Infrastructure, which is what keeps `Envanex.Infrastructure` free of a `Microsoft.AspNetCore.App` framework reference.

### Validation

```
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 bytes>" --project src/Envanex.Web
dotnet user-secrets set "ConnectionStrings:EnvanexDb" "<connection string>" --project src/Envanex.Web
dotnet ef database update -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexDbContext
dotnet ef database update -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext
dotnet run --project src/Envanex.Web
```

Manual smoke against the running host, with all responses pasted into the phase report:
1. `POST /api/auth/login` with a seeded user returns 200 and a token pair.
2. `POST /api/auth/refresh` with that token returns a new pair.
3. Replaying the first refresh token returns 401, and the second token then also returns 401.
4. `POST /api/auth/login` with a wrong password, with an unknown email, and against a locked-out account produce three responses that are identical in status line, `Content-Type` and body. **Paste all three verbatim** — this is Decision 5, and the test that asserts it should agree with what the human can see. Confirm by eye that the `detail` reads "E-posta veya parola hatalı. Arka arkaya birkaç başarısız denemeden sonra hesap bir süreliğine kilitlenir." with the Turkish characters intact.

Also confirm and report: with `Jwt:SigningKey` unset, `dotnet run --project src/Envanex.Web` fails at startup with the `InvalidOperationException` naming `Jwt:SigningKey` and the user-secrets command — not with a silently generated key.

---

## Expected test counts

| Suite | Before | After |
|---|---|---|
| `Envanex.Domain.Tests` | 113 | ~122 |
| `Envanex.Application.Tests` | 79 | ~120 |
| `Envanex.IntegrationTests` | 107 | ~240 |

Estimates. The exact number after each phase must come from that phase's `dotnet test` output, not from this table. `RefreshTokenConcurrencyTests` contributes only 6 cases but is by far the slowest addition; Phase 4's validation step requires its cost to be reported as three measured numbers against the research file's 24 s baseline, so the growth is a fact in the phase report rather than a warning in this plan.

---

## Rollback notes

Every phase is a separate commit on `feat/auth`, so `git revert` of a single commit is the first resort.

- **Phase 1** — reverting the code is not enough once `dotnet ef database update` has run against a real database. The `auth` schema, seven Identity tables and `auth.__EFMigrationsHistory` survive. To undo: `dotnet ef database update 0 -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext`, then drop the schema with `DROP SCHEMA auth`. Local containers are disposable; the Testcontainers database is rebuilt on every run, so tests are unaffected. **`dbo.__EFMigrationsHistory` must not be touched** — the business context's history lives there and the two are separate precisely so this rollback cannot damage business data. The `CLAUDE.md` edit reverts with the commit; if it is kept while the second context is removed, the `--context EnvanexIdentityDbContext` lines become dead instructions, so revert them together.
- **Phase 2** — `dotnet ef migrations remove -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext` if `AddRefreshTokens` has not been applied to any shared database; otherwise `database update InitialIdentitySchema` first. **Never hand-edit the generated migration to change its intent** — create a new one.
- **Phase 3** — pure revert. Two cross-cutting changes travel together: the `ResultMappingTests` scan widening with the eight table entries (revert one without the other and `ResultMapping_NoMappingEntry_ShouldBeOrphaned` turns red), and the "Şifre" → "Parola" alignment with `TurkishErrorMessagesTests.PasswordMessages_ShouldUseParolaConsistentlyAndNeverSifre`.
- **Phase 4** — pure revert, plus removing the `Jwt:*` keys from `DependencyInjectionTests.BuildProvider()` and the two factories. Leaving them in is harmless; removing `AddEnvanexIdentity` without them is also harmless. `CountingUserManager` is test-only and can stay or go without affecting production code.
- **Phase 5** — pure revert. Remember to remove the three rows from `CommandHandlerTypes()` as well, or the theory fails on an unresolvable service.
- **Phase 6** — pure revert, with one caveat: the `Program.cs` rate-limiter restructure is the only edit that touches previously-working behaviour. If it has to come out, restore `AddRateLimiter` and `UseRateLimiter()` inside `if (rateLimitingEnabled)` and drop `[EnableRateLimiting("login")]` from the controller in the same commit — an attribute naming a policy the middleware cannot see throws at endpoint build time, which would take down every endpoint, not just login. Reverting Phase 6 also removes the PR 7 blocker's tripwire test, so the roadmap entry must be kept even if the code is reverted.

Per project rules the coder never commits and never pushes. Take a `git commit` before starting each phase so the rollback above has something to return to.

---

## Notes for the human

1. All six decisions are settled; no phase is blocked on a further ruling. The coder can start Phase 1 immediately.
2. db-reviewer runs after Phases 1, 2 and 4 only. Phase 4 needs it despite adding no migration, because it introduces the refresh-token lookup, the family revocation and the concurrent-rotation paths.
3. The ADR stub goes in at Phase 6; the body is written after `tester` returns `READY_TO_PUSH`.
4. Phase 1's validation deliberately runs `dotnet ef migrations list` **without** `--context` and expects it to fail. The research marks that behaviour unverified. If it unexpectedly succeeds, the `CLAUDE.md` edit is unnecessary and the two-context premise needs rechecking before Phase 2.
5. Six tests in this plan encode security or policy properties rather than mechanics. A change that trips one of them is a design question, not a broken test:
   `AuthErrorsTests.AuthErrors_ShouldNotDeclareALockoutSpecificCode`;
   `ResultExtensionsTests.ToActionResult_EveryAuthErrorCode_ShouldMapTo400Or401`;
   `TurkishErrorMessagesTests.InvalidCredentialsMessage_ShouldNotConfirmThatThisAccountIsLockedOut`;
   `AuthApiTests.Login_UnknownEmailWrongPasswordAndLockedOutAccount_ShouldReturnIdenticalStatusContentTypeAndBody`;
   `IdentityServiceTimingOrderTests.ValidateCredentialsAsync_LockedOutUser_ShouldCallCheckPasswordAsyncBeforeIsLockedOutAsync`;
   `LoginRateLimitPartitionTests.GetKey_ShouldIgnoreXForwardedForUntilPr7`.
6. `IdentityServiceTimingOrderTests.ValidateCredentialsAsync_UnknownEmail_ShouldNotCallCheckPasswordAsync` asserts a *gap*, not a guarantee. It exists so that whoever closes the unknown-email timing hole has to invert a named test rather than quietly change an unobserved behaviour. Do not read it as "this is correct".
7. PR 7 has one hard dependency from this PR: the rate-limit partition key. It is in the roadmap as a blocker and has a test (`GetKey_ShouldIgnoreXForwardedForUntilPr7`) that PR 7 must delete. Deploying 6a/6b publicly without closing it means login stops working for everyone after a handful of sign-ins.
