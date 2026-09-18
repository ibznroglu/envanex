# Plan: PR 6a — Authentication infrastructure (revision 4)

**Revision 4 replaces revision 3 of this same file (`thoughts/shared/plans/2026-09-16_auth-infrastructure.md`). Save over it.** All thirteen findings of `thoughts/shared/reviews/2026-09-17_pr6a-plan-review.md` are accepted and applied; the appendix at the end maps finding → change. Two directed fixes were executed differently than the review proposed, for reasons stated in the appendix (findings 2 and 4). No settled decision is reopened.

Source research: `thoughts/shared/research/2026-09-16_auth-infrastructure.md` (decisions 1–11 are constraints).

## Goal

ASP.NET Core Identity in its own `DbContext` under an `auth` schema, 15-minute JWT access tokens, opaque refresh tokens stored hashed with rotation and reuse detection, and `POST /api/auth/login`, `/refresh`, `/logout`. Every existing endpoint stays open.

## Non-goals

Authorization policies. `[Authorize]`. The Blazor cookie scheme. The demo account. Passkeys. Any user-creating endpoint. A `JwtBearerEvents.OnChallenge` body (no challenge is reachable in 6a — it ships with the first `[Authorize]` in 6b). A refresh-token pruning job. A dummy password verification on the unknown-email path (Decision 6 leaves that half of the timing channel open deliberately). `UseForwardedHeaders` (Decision 4: PR 7).

## Touches schema?

**Yes — Phases 1, 2 and 4. db-reviewer required after each of Phase 1, Phase 2 and Phase 4.** Phases 3, 5, 6 touch no schema, migrations or queries.

## ADR needed?

Yes: `docs/adr/0007-jwt-access-tokens-and-rotated-refresh-tokens.md` — **"JWT access tokens with rotated, reuse-detected refresh tokens in a separate Identity context"**. The human writes the body after `READY_TO_PUSH`. Points it must cover:

- The separate Identity `DbContext`, `auth` schema and its own `__EFMigrationsHistory`.
- `IIdentityService` / `IRefreshTokenService` / `IAccessTokenIssuer` as Application abstractions (ADR 0005 pipeline).
- SHA-256 rather than a password hash for the stored refresh token; why a keyed hash was rejected.
- Zero reuse grace period (research decision 7).
- `min(idle, absolute cap)` refresh expiry and the 15 min / 7 d / 30 d numbers.
- Why login answers wrong password, unknown user and locked-out account with one byte-identical 401, and why `Auth.UserLockedOut` does not exist.
- Why the one message names lockout as a general possibility without confirming it.
- Why the password is verified before the lockout check, and the two residual gaps.
- The family lock: filtered unique index `IX_RefreshTokens_FamilyId_Live` **plus** the `RowVersion` token, and why both failures are collapsed into one loser exit returning `InvalidRefreshToken`. The unique-violation path is unreachable through `RotateAsync` by construction; the index is kept as a database-level invariant and the path is handled in the same branch so that a future change to save ordering cannot turn it into an unhandled exception.
- `ClockSkew = TimeSpan.Zero`.
- Identity's built-in `Microsoft.AspNetCore.Identity` meter as an argument for Identity over hand-rolled user storage.
- Deferrals: `OnChallenge` body, pruning job, and — as a named PR 7 blocker — the rate-limit partition key behind a reverse proxy.

An agent may create the empty file with that title and must not write its body.

---

## Settled decisions

Ruled by the human. Constraints on the coder, not open questions.

### Decision 1 — Lifetimes
Access token **15 minutes** (`Jwt:AccessTokenMinutes`), refresh idle window **7 days** (`Jwt:RefreshTokenIdleDays`), absolute family cap **30 days** (`Jwt:RefreshTokenAbsoluteDays`). The system deploys publicly in PR 7, carries stock-write authority and ships no revocation surface; a short window is the only defence against a leaked token. Phases 2, 3, 4, 6 depend on this.

### Decision 2 — Refresh expiry shape
Absolute cap combined with a shorter idle window. Each row carries `ExpiresAt` (`now + idle`, recomputed each rotation) and `FamilyExpiresAt` (`familyCreatedAt + absolute`, copied forward unchanged). Usable only when `now < ExpiresAt` **and** `now < FamilyExpiresAt`. Copying the cap forward keeps rotation a single-row read. Phases 2 and 4.

### Decision 3 — Refresh token hash
SHA-256 over the UTF-8 bytes, stored `binary(32)`, compared with `CryptographicOperations.FixedTimeEquals`. Token material is 32 bytes from `RandomNumberGenerator.GetBytes`, Base64Url-encoded. No dictionary exists against 256 bits of CSPRNG output, so a work factor buys nothing and costs ~100 ms per refresh; a salted hash would also destroy the indexed seek. Not HMAC: a second secret to rotate, no gain against the only realistic attacker. `FixedTimeEquals` is applied to the fetched row so the constant-time property is in the code, not in the query plan. Phases 2 and 4.

### Decision 4 — Login rate limit: own switch, own factory, named partition key
- Named policy `"login"` via `AddPolicy`, driven by `RateLimiting:Login:{Enabled,PermitLimit,WindowSeconds}` (production 5 / 300 s).
- `LoginRateLimitedWebApplicationFactory` (PermitLimit 2, WindowSeconds 60) for tests, following the `RateLimitedWebApplicationFactory` precedent.
- `Program.cs`: `AddRateLimiter` and `app.UseRateLimiter()` become unconditional; only the **assignment of `GlobalLimiter`** stays inside `if (rateLimitingEnabled)`.
- The partition key is `LoginRateLimitPartition.GetKey(HttpContext)` — a named internal static method with its own tests, so PR 7 has one edit site. It returns `context.Connection.RemoteIpAddress?.ToString()` falling back to the constant `"unknown"`; an unhandled null would throw inside the partition factory on every request.
- **Both shared test factories must set `RateLimiting:Login:Enabled=false`** (finding 1). Under `WebApplicationFactory` there is no remote address, so every login in a run lands in the single `"unknown"` partition; the collection's factory lives for the whole run, so a real 5/300 s budget would be consumed across classes and produce flaky 429s.

#### The same fallback is a PR 7 blocker
Azure App Service terminates TLS at a front end and puts the client address in `X-Forwarded-For`, so `RemoteIpAddress` is the proxy's address for every request and every user collapses into one partition: 5 per 5 minutes becomes global and login stops working. Recorded in `docs/roadmap.md` as a **blocker on PR 7**. `UseForwardedHeaders` does not belong in 6a: configuring it correctly needs deployment knowledge 6a does not have, and calling it with defaults either changes nothing (an untested no-op that looks like a fix) or, once someone clears `KnownProxies`, makes the header attacker-controlled and the limiter worthless — strictly worse than one shared bucket, because a global limit fails closed and loudly. Phase 6.

### Decision 5 — Login returns one indistinguishable 401 for every credential failure
Wrong password, unknown email and locked-out account get the same status, `Content-Type` and body. **`Auth.UserLockedOut` does not exist** — not in `AuthErrors`, `StatusCodeMap` or `TurkishErrorMessages`. Identical codes at the source beat two codes mapped to one status, because two codes still carry different `detail` strings and invite a future "helpful" message. The lockout mechanism is unchanged (`AccessFailedAsync`, `MaxFailedAccessAttempts` 5, `DefaultLockoutTimeSpan` 15 min, `AllowedForNewUsers` true) and is observable through `ILogger<IdentityService>` at `Warning` and Identity's meter, never through the response. No 403 exists in 6a, so `GetReasonPhrase` gains only `401 => "Unauthorized"`. Phases 3, 4, 5, 6.

### Decision 6 — The one message names lockout as a possibility; the password is verified first
**The message.** `Auth.InvalidCredentials` carries: *"E-posta veya parola hatalı. Arka arkaya birkaç başarısız denemeden sonra hesap bir süreliğine kilitlenir."* — a statement about the policy, not about this account. The aorist **"kilitlenir"** is load-bearing; **"kilitlendi"** would confirm the state and is what the tests forbid.

**The ordering.** For a user that exists, `CheckPasswordAsync` runs **before** the lockout check, unconditionally, so a locked-out attempt pays the same PBKDF2 cost as a wrong password. This hides the signal that matters most: a sudden fast "no" tells an attacker the lockout tripped, which confirms the account is real and the policy is 5 attempts.

Residual gaps, both in `docs/roadmap.md`: unknown email is still fast (`FindByEmailAsync` null → no hash verified), and a wrong password performs one `AccessFailedAsync` `UPDATE` that a locked-out attempt does not (~1 ms against ~100 ms of shared PBKDF2 — smaller by orders of magnitude, not zero). Accepted trade: lockout no longer sheds load; the login rate limiter bounds that.

**`AccessFailedAsync` is called at most once per attempt, on exactly one branch.** With one error code there is nothing to choose after incrementing, so there is no post-increment re-check and no path that can double-increment. Phases 3, 4, 6.

---

## Phase 1: Identity context, `auth` schema, its own migrations history, package pins

**Touches schema and migrations — db-reviewer required.**

### Files

- `Directory.Packages.props` — modified — new `<!-- Identity / Auth -->` group. Add in this order, and **run the ordered steps in "Package version determination" below rather than guessing any version**:
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore` `10.0.11`
  - `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.11`
  - `Microsoft.Extensions.Options.ConfigurationExtensions` `10.0.11` and `Microsoft.Extensions.Configuration.Binder` `10.0.11` — pre-empting risk (A); see Phase 4.
  - `Microsoft.IdentityModel.JsonWebTokens` — version read from the resolved graph, not guessed.
  - `Microsoft.Extensions.TimeProvider.Testing` — ships from `dotnet/extensions` on its own version line, so no NU1605 risk against the ASP.NET Core family. Resolve with `dotnet package search Microsoft.Extensions.TimeProvider.Testing --exact-match` and pin the latest stable.
- `src/Envanex.Web/Envanex.Web.csproj` — modified — add `<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />` **now**, in Phase 1, even though nothing uses it until Phase 6. This is what makes the version-determination command below executable at the phase that needs the answer (finding 5). An unused `PackageReference` produces no warning and therefore no `TreatWarningsAsErrors` failure.
- `CLAUDE.md` — modified — replace the two `dotnet ef` lines in `## Commands` with four:
  ```
  dotnet ef migrations add <Name> -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexDbContext
  dotnet ef migrations add <Name> -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext --output-dir Migrations/Identity
  dotnet ef database update -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexDbContext
  dotnet ef database update -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext
  ```
  Change nothing else in `CLAUDE.md`.
- `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj` — modified — add `<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />`. Do **not** add a `FrameworkReference` to `Microsoft.AspNetCore.App`: the design uses `AddIdentityCore` and `UserManager<T>` only, never `SignInManager`.
- `src/Envanex.Infrastructure/Identity/EnvanexUser.cs` — created — `public sealed class EnvanexUser : IdentityUser<Guid>` with no added members. Public because `EnvanexIdentityDbContext` is public and CS0060 forbids a public class deriving from a base constructed with a less accessible type argument.
- `src/Envanex.Infrastructure/Identity/AuthSchema.cs` — created.
- `src/Envanex.Infrastructure/Identity/EnvanexIdentityDbContext.cs` — created. `OnModelCreating` calls `ArgumentNullException.ThrowIfNull(modelBuilder)`, `modelBuilder.HasDefaultSchema(AuthSchema.Name)`, then `base.OnModelCreating(modelBuilder)`. It does **not** call `ApplyConfigurationsFromAssembly` and does **not** override `ConfigureConventions`, so `AggregateRootConvention` and `MoneyComplexTypeConvention` never reach it.
  - For db-reviewer: full `IdentityDbContext` (with roles), so `AspNetRoles`, `AspNetRoleClaims`, `AspNetUserRoles` are created now and stay empty until 6b — three empty tables today against a second Identity migration in 6b.
  - For db-reviewer: `AspNetUsers.Id` is a `uniqueidentifier` with a clustered PK; acceptable for a low-cardinality table, and called out because Phase 2's high-volume table takes the opposite decision.
- `src/Envanex.Infrastructure/Identity/IdentityDbContextOptionsExtensions.cs` — created — one place that knows how to point a builder at the Identity database, so the history table cannot be set in one of three call sites and forgotten in the others.
- `src/Envanex.Infrastructure/Identity/EnvanexIdentityDbContextFactory.cs` — created — reads `ENVANEX_CONNECTION_STRING`, throws the same-shaped `InvalidOperationException` as `EnvanexDbContextFactory`, builds through `UseEnvanexIdentitySqlServer`.
- `src/Envanex.Infrastructure/DependencyInjection.cs` — modified — add `services.AddDbContext<EnvanexIdentityDbContext>(options => options.UseEnvanexIdentitySqlServer(connectionString));` after the existing `AddDbContext<EnvanexDbContext>`. Nothing else this phase.
- `src/Envanex.Infrastructure/Persistence/EnvanexDbContext.cs` — modified — change `ApplyConfigurationsFromAssembly(typeof(EnvanexDbContext).Assembly)` to the predicate overload restricting configurations to namespace `Envanex.Infrastructure.Persistence.Configurations`. Not cosmetic: Phase 2 adds `RefreshTokenConfiguration` to the same assembly and without the predicate `EnvanexDbContext` would create a second `dbo.RefreshTokens` on the next business migration. **Verified premise:** `ProductConfiguration` is `internal sealed` (`src/Envanex.Infrastructure/Persistence/Configurations/ProductConfiguration.cs:8`) and `Products` is mapped today, so `ApplyConfigurationsFromAssembly` does discover internal configurations. Review item (C) is therefore settled by reading the repository and needs no probe migration.
- `src/Envanex.Infrastructure/Migrations/Identity/` — created by tooling — `InitialIdentitySchema`, its designer, and `EnvanexIdentityDbContextModelSnapshot`.
- `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs` — modified — add `CreateIdentityDbContext()`; in `InitializeAsync`, after the existing `MigrateAsync`, open an identity context and `MigrateAsync` it; add `ResetIdentityAsync()` deleting in FK order `AspNetUserTokens`, `AspNetUserLogins`, `AspNetUserClaims`, `AspNetUserRoles`, `AspNetRoleClaims`, `AspNetRoles`, `AspNetUsers`, all `auth.`-qualified. Per research decision 5 this is a **separate** method; `ResetAsync` is untouched. From Phase 2 on it needs no explicit `RefreshTokens` delete — the cascading FK removes them when `auth.AspNetUsers` is deleted last. Do not "fix" that.
- `tests/Envanex.IntegrationTests/Envanex.IntegrationTests.csproj` — modified — add `<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />` and `<PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" />` (both used from Phase 4).
- `tests/Envanex.Domain.Tests/ArchitectureTests.cs` — modified — see "Tests to add".

### Package version determination (run in this order)

1. Add the `PackageVersion` entries for `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Configuration.Binder` (all `10.0.11`).
2. Add the JwtBearer `PackageReference` to `src/Envanex.Web/Envanex.Web.csproj` and the Identity.EntityFrameworkCore `PackageReference` to `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj`.
3. `dotnet restore`
4. `dotnet list src/Envanex.Web/Envanex.Web.csproj package --include-transitive` — read the **resolved** `Microsoft.IdentityModel.JsonWebTokens` version. **Paste the raw output into the phase report.**
5. Add that exact version as a `PackageVersion`. Pinning lower produces NU1605, which `TreatWarningsAsErrors` turns into a build failure; pinning the resolved version cannot.
6. Add the two test-project `PackageReference` entries.

### Signatures

```csharp
// src/Envanex.Infrastructure/Identity/AuthSchema.cs
internal static class AuthSchema
{
    public const string Name = "auth";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";
}

public sealed class EnvanexUser : IdentityUser<Guid>;

public class EnvanexIdentityDbContext : IdentityDbContext<EnvanexUser, IdentityRole<Guid>, Guid>
{
    public EnvanexIdentityDbContext(DbContextOptions<EnvanexIdentityDbContext> options);
    protected override void OnModelCreating(ModelBuilder modelBuilder);
}

internal static class IdentityDbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UseEnvanexIdentitySqlServer(
        this DbContextOptionsBuilder builder,
        string connectionString);
}

public sealed class EnvanexIdentityDbContextFactory : IDesignTimeDbContextFactory<EnvanexIdentityDbContext>
{
    public EnvanexIdentityDbContext CreateDbContext(string[] args);
}

// SqlServerFixture
public EnvanexIdentityDbContext CreateIdentityDbContext();
public Task ResetIdentityAsync();
```

`UseEnvanexIdentitySqlServer` must call `UseSqlServer(connectionString, b => b.MigrationsHistoryTable(AuthSchema.MigrationsHistoryTable, AuthSchema.Name))`. Without it both contexts read `dbo.__EFMigrationsHistory` as their own and the failure is silent until a migration is skipped or re-applied.

`InternalsVisibleTo` from `Envanex.Infrastructure` to `Envanex.IntegrationTests` already exists (`Envanex.Infrastructure.csproj:3`); no new entry is needed.

### Tests to add

`tests/Envanex.IntegrationTests/Persistence/IdentitySchemaTests.cs` — new, `[Collection(DatabaseCollection.Name)]`:
- `IdentityMigrations_ShouldApplyToCleanDatabase`
- `IdentityTables_ShouldLiveInAuthSchema` — `[Theory]`, `[InlineData]` for `AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserRoles`, `AspNetUserTokens`, `AspNetRoleClaims`.
- `IdentityMigrationsHistory_ShouldLiveInAuthSchema`
- `BusinessAppliedMigrations_ShouldNotContainIdentityMigrations`
- `IdentityAppliedMigrations_ShouldNotContainBusinessMigrations`
- `BusinessMigrationsHistory_ShouldNotContainIdentityMigrations` — direct `SELECT MigrationId FROM dbo.__EFMigrationsHistory`, catching the shared-history failure at the table level rather than through EF's bookkeeping.

`tests/Envanex.IntegrationTests/Persistence/DbContextIsolationTests.cs` — new, `[Collection(DatabaseCollection.Name)]`:
- `EnvanexDbContext_ShouldStillMapProductUnitOfMeasureAndWarehouse` — negative control on the new predicate, so an over-narrow namespace filter cannot pass silently.
- `EnvanexIdentityDbContext_ShouldNotMapAnyBusinessEntityType`
- `EnvanexIdentityDbContext_ShouldNotDeclareRowVersionShadowProperty` — proves `AggregateRootConvention` did not follow the Identity context.

There is deliberately **no** `EnvanexDbContext_ShouldNotMapEnvanexUser` (finding 8): `EnvanexUser` has no `IEntityTypeConfiguration` in the assembly in any phase, so that test is green with the predicate deleted. The guard the predicate actually exists for is `EnvanexDbContext_ShouldNotMapRefreshToken`, added in Phase 2 where `RefreshTokenConfiguration` exists.

`tests/Envanex.Domain.Tests/ArchitectureTests.cs` — modified. First **extract the package-name matcher** so the negative control can run the real thing (finding 11b):
```csharp
private static string[] MatchPackageReferences(string csprojContent, string packageNamePattern);
private static string ReadCsproj(string projectName);
```
`MatchPackageReferences` holds the single regex `<PackageReference\s+Include="([^"]*{pattern}[^"]*)"`; `Application_ShouldNotReference_EntityFrameworkPackages` is rewritten to call it and must keep passing unchanged. Then add:
- `Application_ShouldNotReference_AuthenticationPackages` — `[Theory]` with `[InlineData("Microsoft\\.AspNetCore\\.Identity")]`, `[InlineData("Microsoft\\.AspNetCore\\.Authentication")]`, `[InlineData("Microsoft\\.IdentityModel")]`, `[InlineData("System\\.IdentityModel")]`. Closes the proven gap: the existing `Microsoft\.EntityFrameworkCore` regex matches neither new package name.
- `ForbiddenPackageMatcher_ShouldMatch_RealAuthPackageNames` — calls `MatchPackageReferences` against a synthetic csproj string containing `<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />` and `<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />` and asserts both are returned. Without it a broken matcher passes the theory above by matching nothing.
- `Infrastructure_ShouldReference_IdentityEntityFrameworkCore` — positive control.
- `Web_ShouldReference_JwtBearerPackage` — positive control, moved here from Phase 6 because the reference lands here. It keeps JwtBearer out of Application and Infrastructure, which is what keeps Infrastructure free of a `Microsoft.AspNetCore.App` framework reference.

`tests/Envanex.IntegrationTests/Persistence/MigrationTests.cs` — unchanged.

### Validation

From `C:\projects\envanex`. **Paste raw output of steps 4–8, including the expected failure in step 6.**

```
docker compose up -d
dotnet build -warnaserror
$env:ENVANEX_CONNECTION_STRING="Server=localhost,1433;Database=EnvanexDev;User Id=sa;Password=<password>;TrustServerCertificate=True"
dotnet list src/Envanex.Web/Envanex.Web.csproj package --include-transitive
dotnet ef dbcontext list -p src/Envanex.Infrastructure -s src/Envanex.Web
dotnet ef migrations list -p src/Envanex.Infrastructure -s src/Envanex.Web
dotnet ef migrations list -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexDbContext
dotnet ef migrations list -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext
dotnet ef migrations add InitialIdentitySchema -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext --output-dir Migrations/Identity
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

Expected: step 6 (`migrations list` without `--context`) **fails** with "More than one DbContext was found"; steps 7 and 8 succeed. If step 6 unexpectedly succeeds, stop and report — the plan's `--context` premise is then wrong and the `CLAUDE.md` edit is unnecessary.

---

## Phase 2: RefreshTokens table, hashing, token generation

**Touches schema and migrations — db-reviewer required.** Depends on Decisions 1, 2, 3.

### Files

- `src/Envanex.Infrastructure/Identity/RefreshToken.cs` — created — `internal sealed class RefreshToken`, all setters `private set`.
- `src/Envanex.Infrastructure/Identity/RefreshTokenHasher.cs` — created.
- `src/Envanex.Infrastructure/Identity/RefreshTokenGenerator.cs` — created.
- `src/Envanex.Infrastructure/Identity/Configurations/RefreshTokenConfiguration.cs` — created — deliberately **not** under `Envanex.Infrastructure.Persistence.Configurations`, so the Phase 1 predicate keeps it out of `EnvanexDbContext`.
- `src/Envanex.Infrastructure/Identity/EnvanexIdentityDbContext.cs` — modified — add `internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();` and, after `base.OnModelCreating`, `modelBuilder.ApplyConfiguration(new Configurations.RefreshTokenConfiguration());`.
- `src/Envanex.Infrastructure/Migrations/Identity/` — migration `AddRefreshTokens`.
- `tests/Envanex.IntegrationTests/Fixtures/IdentityRowSeeder.cs` — created — the **narrow, named exception** to research decision 6 (finding 3); see below.

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

// tests/Envanex.IntegrationTests/Fixtures/IdentityRowSeeder.cs
internal static class IdentityRowSeeder
{
    /// <summary>
    /// Inserts a bare auth.AspNetUsers row through EnvanexIdentityDbContext so that
    /// RefreshTokens rows have a foreign key to point at. PasswordHash is left null:
    /// the row cannot be logged in with, so it cannot mask a hashing defect.
    /// Schema tests only — see the scope guard test.
    /// </summary>
    public static Task<Guid> InsertBareUserRowAsync(SqlServerFixture fixture, string email);
}
```

`CreateChild` copies `FamilyId` and `FamilyExpiresAt` unchanged and sets `ExpiresAt = now + idleWindow` (Decision 2). `IsUsableAt` returns true only when `RotatedAt is null && RevokedAt is null && now < ExpiresAt && now < FamilyExpiresAt` — note this is exactly the filter of `IX_RefreshTokens_FamilyId_Live` plus the two time checks.

`Hash` is SHA-256 over `Encoding.UTF8.GetBytes(token)`; `Matches` calls `CryptographicOperations.FixedTimeEquals`; `CreateToken` is `Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenByteLength))`. Per the null-vs-invalid rule, `Hash(null)` and `Matches(null, _)` throw `ArgumentNullException`; a non-matching token is a `false` return.

`IdentityRowSeeder.InsertBareUserRowAsync` sets `Id`, `UserName`, `NormalizedUserName`, `Email`, `NormalizedEmail` (both normalized with `ToUpperInvariant()`), `SecurityStamp`, `ConcurrencyStamp`, and leaves `PasswordHash` null.

**Why this does not weaken research decision 6.** That decision exists so login tests exercise real password hashing and Identity's normalized columns — not to forbid a row that satisfies a foreign key. Phase 2's tests never authenticate; they assert index shape, column types and FK behaviour. The seeded row has no password hash, so it cannot be authenticated against and cannot hide a hashing defect. Everything from Phase 4 on uses `IdentitySeeder.CreateUserAsync` through `UserManager`. The exception is bounded by a test, not by a comment.

**Carried forward from the Phase 1 re-review (db-reviewer, `d71f4f9`).** `auth.AspNetUsers.EmailIndex` is now a *unique* filtered index on `NormalizedEmail`, so `InsertBareUserRowAsync` — which bypasses `UserValidator` and writes `NormalizedEmail` directly — is the one path in the suite that can hit SQL Server error 2601 on a duplicate email. As specified this is safe: per-test `ResetIdentityAsync` (see "Tests to add") clears the table between tests. It becomes a hard `SqlException` if two calls in one test pass the same email, or if a caller skips the reset. Generating a unique email per call would make it safe unconditionally; that is a choice for whoever writes this phase, not a requirement recorded here.

### EF configuration — exact

- `builder.ToTable("RefreshTokens", AuthSchema.Name)`
- `builder.HasKey(t => t.Id)` **and** `.IsClustered(false)`
- `builder.HasIndex(t => new { t.CreatedAt, t.Id }).IsClustered().HasDatabaseName("IX_RefreshTokens_CreatedAt_Id")` — append-only, never pruned in 6a; a random-GUID clustered PK would fragment every insert. **db-reviewer should confirm this trade.**
- `builder.Property(t => t.TokenHash).IsRequired().HasColumnType("binary(32)")`
- `builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("IX_RefreshTokens_TokenHash")`
- `builder.HasIndex(t => t.FamilyId).IsUnique().HasFilter("[RotatedAt] IS NULL AND [RevokedAt] IS NULL").HasDatabaseName("IX_RefreshTokens_FamilyId_Live")` — at most one live token per family; the database-level answer to "rotation racing revocation".
- **`builder.HasIndex(t => t.FamilyId).HasDatabaseName("IX_RefreshTokens_FamilyId")` — new in revision 4 (finding 4).** Non-unique, **unfiltered**, one key column. It serves the three family queries (`WHERE FamilyId = @f AND RevokedAt IS NULL`: reuse detection, expiry revocation, `RevokeFamilyAsync` and its re-query), which without it are clustered-index scans of an append-only table.
  **Why unfiltered and single-column, stated for db-reviewer to rule on:** (a) all three queries are equality seeks on `FamilyId`; (b) a family is bounded by ~30 days of rotations, so the residual `RevokedAt IS NULL` filters a handful of rows after the seek; (c) a filter on `RevokedAt IS NULL` would exclude the *minority* of rows — in an append-only table revocation is the exception, rotated-but-unrevoked is the rule — so it would be barely smaller while becoming unusable for any query that must see revoked rows, including the pruning job the Worker PR will need; (d) adding `RevokedAt` as a second key column buys a few avoided key lookups per family at a write cost on an insert-heavy table. If db-reviewer prefers `(FamilyId, RevokedAt)` or the filtered shape, that is a schema call to make here, not in Phase 4.
- `builder.HasIndex(t => t.UserId).HasDatabaseName("IX_RefreshTokens_UserId")`
- `builder.HasOne<EnvanexUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade).IsRequired()`
- `builder.Property(t => t.RevokedReason).HasMaxLength(32)`
- `builder.Property<byte[]>(ColumnNames.RowVersion).IsRowVersion()` — explicit, because `AggregateRootConvention` is not registered on this context. It is the second half of the family lock and, per Phase 4's analysis, the **only** loser exit a racing rotation can actually reach.
- All six `DateTimeOffset` properties map to `datetimeoffset` by convention; no explicit precision.

### Tests to add

`tests/Envanex.IntegrationTests/Identity/RefreshTokenHasherTests.cs` — new, no database, no `[Collection]` (it lives in `Envanex.IntegrationTests` only because that is the project with `InternalsVisibleTo`):
- `Hash_SameToken_ShouldProduceTheSameHash`
- `Hash_DifferentTokens_ShouldProduceDifferentHashes`
- `Hash_ShouldProduceExactly32Bytes`
- `Hash_NullToken_ShouldThrowArgumentNullException`
- `Matches_CorrectToken_ShouldReturnTrue`
- `Matches_WrongToken_ShouldReturnFalse`
- `Matches_StoredHashOfWrongLength_ShouldReturnFalse`
- `CreateToken_ShouldDecodeTo32Bytes`
- `CreateToken_ShouldBeUrlSafe` — no `+`, `/` or `=`
- `CreateToken_CalledOneThousandTimes_ShouldProduceOneThousandDistinctValues`

`tests/Envanex.IntegrationTests/Identity/RefreshTokenTests.cs` — new, no database. Names say what they prove: these methods take `idleWindow`/`absoluteWindow` as arguments, so they prove `now + argument`, **not** the 7/30-day numbers (finding 9). The shipped numbers are pinned in Phase 3.
- `CreateRoot_ShouldSetExpiresAtToNowPlusTheIdleWindowArgument`
- `CreateRoot_ShouldSetFamilyExpiresAtToNowPlusTheAbsoluteWindowArgument`
- `CreateRoot_ShouldSetFamilyIdToANonEmptyGuid`
- `CreateChild_ShouldKeepTheParentFamilyId`
- `CreateChild_ShouldCopyFamilyExpiresAtUnchanged`
- `CreateChild_ShouldResetExpiresAtToNowPlusTheIdleWindowArgument`
- `IsUsableAt_FreshToken_ShouldBeTrue`
- `IsUsableAt_RotatedToken_ShouldBeFalse`
- `IsUsableAt_RevokedToken_ShouldBeFalse`
- `IsUsableAt_PastTheIdleWindow_ShouldBeFalse`
- `IsUsableAt_WithinTheIdleWindowButPastFamilyExpiry_ShouldBeFalse`
- `MarkRotated_ShouldSetRotatedAtAndReplacedByTokenId`

`tests/Envanex.IntegrationTests/Persistence/RefreshTokenSchemaTests.cs` — new, `[Collection(DatabaseCollection.Name)]`, `InitializeAsync` calls `ResetIdentityAsync()` then `IdentityRowSeeder.InsertBareUserRowAsync(...)` for the FK:
- `RefreshTokens_ShouldLiveInAuthSchema`
- `RefreshTokens_TokenHash_ShouldBeBinary32AndNotNullable`
- `RefreshTokens_TokenHashIndex_ShouldBeUnique`
- `RefreshTokens_FamilyLiveIndex_ShouldBeUniqueAndFiltered` — asserts `sys.indexes.has_filter = 1` and that the filter definition mentions both `RotatedAt` and `RevokedAt`.
- `RefreshTokens_FamilyIdIndex_ShouldExistAndBeUnfiltered` — asserts a non-unique, non-filtered index keyed on `FamilyId` exists. Red if the new index of finding 4 is dropped.
- `RefreshTokens_PrimaryKey_ShouldBeNonClustered`
- `RefreshTokens_ClusteredIndex_ShouldBeOnCreatedAtAndId`
- `RefreshTokens_UserId_ShouldHaveCascadingForeignKeyToAspNetUsers`
- `RefreshTokens_InsertingASecondLiveTokenForTheSameFamily_ShouldThrowUniqueViolation` — **this is the test that protects `IX_RefreshTokens_FamilyId_Live`.** It is deterministic and goes red if the index loses `.IsUnique()` or its filter. No `RotateAsync` test claims that job (see Phase 4).
- `RefreshTokens_InsertingASecondTokenWithTheSameHash_ShouldThrowUniqueViolation`
- `RefreshTokens_RotatedRowAndItsChild_ShouldCoexistInTheSameFamily` — the direct regression for "deleting the predecessor destroys detection".
- `RefreshTokens_UpdatingARowLoadedBeforeAConcurrentUpdate_ShouldThrowDbUpdateConcurrencyException` — **new in revision 4.** Deterministic, no race: load a row in context A, update and save the same row in context B, then update and save in A. Red if `.IsRowVersion()` is dropped. This is the direct guard for the concurrency half of the family lock; the Phase 4 race test is an invariant check, not this guard.
- `RefreshTokens_ExpiryColumns_ShouldBeDateTimeOffset`
- `RefreshTokens_ShouldHaveARowVersionColumn`

`tests/Envanex.IntegrationTests/Persistence/DbContextIsolationTests.cs` — modified:
- `EnvanexDbContext_ShouldNotMapRefreshToken` — **the guard the Phase 1 predicate exists for** (finding 8). Non-vacuous: `RefreshTokenConfiguration` is an `IEntityTypeConfiguration<RefreshToken>` in the same assembly, and internal configurations are demonstrably discovered (`ProductConfiguration` is `internal sealed` and `Products` is mapped), so deleting the predicate turns this red.
- `EnvanexIdentityDbContext_ShouldMapRefreshToken` — positive control, so a mis-namespaced or unregistered configuration that maps the entity nowhere cannot pass both tests.

`tests/Envanex.IntegrationTests/Identity/IdentitySeedingScopeTests.cs` — new, no database:
- `IdentityRowSeeder_ShouldBeReferencedOnlyBySchemaTests` — walks up from `AppContext.BaseDirectory` to the solution directory (same technique as `ArchitectureTests.FindSolutionDirectory`), scans every `*.cs` under `tests/` for the identifier `IdentityRowSeeder`, and asserts the matching files are exactly `IdentityRowSeeder.cs`, `RefreshTokenSchemaTests.cs` and this test file. This is what keeps the research-decision-6 exception narrow: widening it fails a named test.

### Validation

```
dotnet ef migrations add AddRefreshTokens -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext --output-dir Migrations/Identity
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

Paste the generated `Up` method of `AddRefreshTokens` into the phase report so db-reviewer can read the filtered index, the new unfiltered `FamilyId` index and the clustered/non-clustered split without opening the file.

---

## Phase 3: Application auth contracts, `AuthErrors`, widened reflection guard, `Jwt` settings

**No schema, no migrations, no queries. db-reviewer not required.** Depends on Decisions 1, 5, 6.

Error codes and both mapping-table entries ship together: `ResultMappingTests.ResultMapping_NoMappingEntry_ShouldBeOrphaned` fails if a table entry exists without a matching `Error` field, so splitting them would leave the suite red.

### Files

- `src/Envanex.Application/Authentication/AuthErrors.cs` — created.
- `src/Envanex.Application/Authentication/JwtOptions.cs` — created. A plain POCO in Application so Infrastructure (which signs) and Web (which validates) read one declaration rather than two sets of string literals. It pulls in no package.
- `src/Envanex.Application/Authentication/JwtOptionsGuard.cs` — created.
- `src/Envanex.Application/Abstractions/Authentication/IIdentityService.cs`, `IRefreshTokenService.cs`, `IAccessTokenIssuer.cs` — created.
- `src/Envanex.Application/Authentication/Models/{AuthenticatedUser,IssuedAccessToken,IssuedRefreshToken,RotatedRefreshToken}.cs` — created.
- `src/Envanex.Application/Authentication/DTOs/AuthenticationResponse.cs` — created. Named `*Response`, not `*ListDto`, so `DataSourceListDtos_ShouldNotBePositionalRecords` does not apply.
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — modified — **eight** new `StatusCodeMap` entries (33 → 41) and one new `GetReasonPhrase` arm, `401 => "Unauthorized"`. No 403 arm.
- `src/Envanex.Web/Extensions/TurkishErrorMessages.cs` — modified — the same eight codes (33 → 41). All eight are **new**; no existing message is changed (finding 6).
- `src/Envanex.Web/appsettings.json` — modified — add `Jwt`: `"Issuer": "https://envanex.local"`, `"Audience": "envanex-api"`, `"SigningKey": ""`, `"AccessTokenMinutes": 15`, `"RefreshTokenIdleDays": 7`, `"RefreshTokenAbsoluteDays": 30`. `SigningKey` stays empty; it is a user-secret.
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

**There is deliberately no `AuthErrors.UserLockedOut`** (Decision 5): a locked-out account gets the same `InvalidCredentials` instance as a wrong password and an unknown email, so the three cannot drift apart later.

`JwtOptionsGuard.ThrowIfInvalid` throws `ArgumentNullException` on null, otherwise `InvalidOperationException` naming the offending key and the user-secrets command — the same shape as the connection-string guard, e.g. `dotnet user-secrets set "Jwt:SigningKey" "<value>" --project src/Envanex.Web`. It rejects blank `Issuer`, blank `Audience`, blank `SigningKey`, a `SigningKey` shorter than 32 UTF-8 bytes, `AccessTokenMinutes <= 0`, `RefreshTokenIdleDays <= 0`, `RefreshTokenAbsoluteDays <= 0`, and `RefreshTokenIdleDays > RefreshTokenAbsoluteDays`.

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

No auth code maps to 403; a test locks that in.

### Turkish messages — exact strings

All eight are new entries. **No existing message is edited** — a case-insensitive search of `src/` for "ifre" and "parola" returns nothing today, and no `Auth.*` code exists, so there is no vocabulary to align (finding 6).

- `Auth.InvalidCredentials` → **"E-posta veya parola hatalı. Arka arkaya birkaç başarısız denemeden sonra hesap bir süreliğine kilitlenir."** *(Decision 6)*
- `Auth.InvalidRefreshToken` → "Oturum bilgisi geçersiz. Lütfen tekrar giriş yapın."
- `Auth.RefreshTokenExpired` → "Oturum süresi doldu. Lütfen tekrar giriş yapın."
- `Auth.RefreshTokenReused` → "Oturum güvenlik nedeniyle sonlandırıldı. Lütfen tekrar giriş yapın."
- `Auth.EmailRequired` → "E-posta adresi zorunludur."
- `Auth.EmailInvalid` → "Geçerli bir e-posta adresi giriniz."
- `Auth.PasswordRequired` → **"Parola zorunludur."**
- `Auth.RefreshTokenRequired` → "Yenileme anahtarı zorunludur."

The `InvalidCredentials` message is what a wrong password, an unknown email and a locked-out account all see. The aorist "kilitlenir" is load-bearing; "kilitlendi" would confirm the state.

### `ResultMappingTests` change — exact

Rename the private helper `GetAllDomainErrorCodes()` to `GetAllErrorCodes()`, scanning `typeof(Error).Assembly` **and** `typeof(AuthErrors).Assembly` with the same field filter. Rename the four facts so they do not lie: `ResultMapping_EveryErrorCode_ShouldHaveAnExplicitMapping`, `ResultMapping_EveryErrorCode_ShouldHaveATurkishMessage`, `ResultMapping_NoErrorCode_ShouldBeEmpty`, `ResultMapping_NoMappingEntry_ShouldBeOrphaned`. Add:
- `ResultMapping_Scan_ShouldReachTheApplicationAssembly` — asserts the scanned set contains `"Auth.InvalidCredentials"`. Without it a broken two-assembly scan silently degrades to Domain-only.

### Tests to add

`tests/Envanex.Application.Tests/Authentication/AuthErrorsTests.cs` — new:
- `AuthErrors_EveryCode_ShouldStartWithTheAuthPrefix`
- `AuthErrors_Codes_ShouldBeUnique`
- `AuthErrors_EveryMessage_ShouldBeNonEmpty`
- `AuthErrors_ShouldNotDeclareALockoutSpecificCode` — reflects over `AuthErrors`, asserts no field whose code or message names lockout. Re-adding `UserLockedOut` must mean deleting a test that says why.

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

`tests/Envanex.IntegrationTests/Api/TurkishErrorMessagesTests.cs` — new, no database. Every case below fails if the shipped string is wrong (finding 6):
- `InvalidCredentialsMessage_ShouldBeExactlyTheDecision6String` — asserts `GetMessage("Auth.InvalidCredentials", "fallback")` equals the exact string above, character for character. Also proves the Turkish characters survived the file encoding.
- `InvalidCredentialsMessage_ShouldNotConfirmThatThisAccountIsLockedOut` — does not contain "kilitlendi", "kilitli" or "hesabınız".
- `InvalidCredentialsMessage_ShouldOfferBothFactorsWithVeya` — contains "veya" and does not name one factor as the wrong one.
- `PasswordRequiredMessage_ShouldBeExactlyParolaZorunludur` — asserts `GetMessage("Auth.PasswordRequired", "fallback")` equals `"Parola zorunludur."`. This replaces revision 3's "scan every message for 'ifre'" test, which was green before and after the change and protected nothing. This one goes red if the message is written as "Şifre zorunludur.".

`tests/Envanex.IntegrationTests/Configuration/JwtAppSettingsTests.cs` — **new** (finding 9), no database. Loads `src/Envanex.Web/appsettings.json` from the solution directory (walk-up from `AppContext.BaseDirectory`) with `ConfigurationBuilder().AddJsonFile(...)` and binds the `Jwt` section:
- `AppSettings_AccessTokenMinutes_ShouldBeFifteen` *(Decision 1)*
- `AppSettings_RefreshTokenIdleDays_ShouldBeSeven` *(Decision 1)*
- `AppSettings_RefreshTokenAbsoluteDays_ShouldBeThirty` *(Decision 1)*
- `AppSettings_IssuerAndAudience_ShouldBeNonEmpty`
- `AppSettings_SigningKey_ShouldBeEmptySoThatNoSecretIsCommitted` — a committed signing key fails here.

These are the only tests in the plan that assert the shipped numbers; everything else asserts `now + argument` or reads its own in-memory configuration. A typo such as `AccessTokenMinutes: 150` now ships red.

`tests/Envanex.IntegrationTests/Api/ResultExtensionsTests.cs` — modified:
- `ToActionResult_AuthInvalidCredentials_ShouldReturn401WithTurkishDetail`
- `ToActionResult_AuthRefreshTokenReused_ShouldReturn401WithTurkishDetail`
- `ToActionResult_Status401_ShouldHaveTitleUnauthorized`
- `ToActionResult_EveryAuthErrorCode_ShouldMapTo400Or401` — `[Theory]` over codes reflected from `AuthErrors`; Decision 5 enforced at the mapping table, one layer below Phase 6's HTTP test.

`tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs` — modified as above.

### Validation

```
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

The phase report must state the new `StatusCodeMap` and `TurkishErrorMessages` entry counts (both 41) and paste the `Auth.InvalidCredentials` string verbatim.

---

## Phase 4: Infrastructure implementations — Identity, rotation, JWT issuance

**Touches queries (refresh-token lookup, family revocation, concurrent rotation) but adds no migration — db-reviewer required.** Depends on Decisions 1, 2, 3, 5, 6.

### Named risks for this phase (settle these before writing the tests)

- **(A) Options/configuration binding inside `Envanex.Infrastructure`.** `services.Configure<JwtOptions>(section)` lives in `Microsoft.Extensions.Options.ConfigurationExtensions` and `section.Bind`/`Get<T>` in `Microsoft.Extensions.Configuration.Binder`; neither is obviously in Infrastructure's transitive closure (`Envanex.Infrastructure.csproj` references only EF Core Design, EF Core SqlServer and — from Phase 1 — Identity.EntityFrameworkCore). Phase 1 already added both `PackageVersion` entries; this phase adds `<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />` and `<PackageReference Include="Microsoft.Extensions.Configuration.Binder" />` to `Envanex.Infrastructure.csproj`. If they turn out to be redundant that is harmless — same version, no NU1605. **Settled by:** `dotnet list src/Envanex.Infrastructure/Envanex.Infrastructure.csproj package --include-transitive` then `dotnet build -warnaserror`. Paste both outputs.
- **(B) `ILogger<T>` in the hand-built test container.** `tests/Envanex.IntegrationTests/DependencyInjectionTests.cs:26-40` builds a `ServiceCollection` with no `AddLogging()`. `IdentityService` and `RefreshTokenService` both take `ILogger<T>`. Rather than rely on `AddIdentityCore` calling `AddLogging()` internally, this phase adds an explicit `services.AddLogging();` to `BuildProvider()` (idempotent, harmless if already present). **Settled by:** `dotnet test tests/Envanex.IntegrationTests --filter "FullyQualifiedName~AllRegisteredServices_ShouldResolve"`.
- **(E) `UserManager<TUser>` virtuals.** `CountingUserManager` overrides `CheckPasswordAsync`, `IsLockedOutAsync`, `AccessFailedAsync`, `ResetAccessFailedCountAsync`. If any is not `public virtual` in .NET 10 the class will not compile. **Settled by:** `dotnet build -warnaserror`. If one is non-virtual, stop and report — the alternative (an `IUserStore` decorator) is a design change the human rules on, not a silent substitution.
- **(F) `AccessFailedAsync` updating the tracked entity in place.** Step 5 below reads `user.LockoutEnd` after the call without a second round trip. **Settled by:** `ValidateCredentialsAsync_FifthWrongPassword_ShouldSetLockoutEndAboutFifteenMinutesAhead`. If it fails, re-fetch the user before reading `LockoutEnd` — that is a one-line fix, not a design change.
- **(G) EF Core statement ordering inside `SaveChangesAsync`.** Rotation stamps the parent (`RotatedAt`) and inserts the child in one save. If the INSERT reached the server **before** the UPDATE, the parent would still be live at insert time and `IX_RefreshTokens_FamilyId_Live` would reject **every** rotation, not just a racing one. Step 9 makes this safe rather than fatal — both failures map to `InvalidRefreshToken` in one branch, so no ordering produces an unhandled exception — but an INSERT-first ordering would still break rotation functionally, so it must be observed rather than assumed. **Settled by:** the first green run of `RotateAsync_ValidToken_ShouldReturnADifferentTokenString` (red if every rotation is rejected) plus the logged SQL pasted into the report, which must show `UPDATE ... RefreshTokens ... SET RotatedAt` before `INSERT ... RefreshTokens`.

### Files

- `src/Envanex.Infrastructure/Identity/IdentityService.cs` — created — `internal sealed class IdentityService : IIdentityService`.
- `src/Envanex.Infrastructure/Identity/RefreshTokenService.cs` — created — `internal sealed class RefreshTokenService : IRefreshTokenService`.
- `src/Envanex.Infrastructure/Identity/JwtAccessTokenIssuer.cs` — created — `internal sealed class JwtAccessTokenIssuer : IAccessTokenIssuer`.
- `src/Envanex.Infrastructure/Identity/IdentityInfrastructureExtensions.cs` — created — `public static class IdentityInfrastructureExtensions` with `AddEnvanexIdentity`.
- `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj` — modified — add `Microsoft.IdentityModel.JsonWebTokens`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Configuration.Binder` (risk A).
- `src/Envanex.Infrastructure/DependencyInjection.cs` — modified — `services.AddEnvanexIdentity(configuration);` as the last statement before `return services;`.
- `tests/Envanex.IntegrationTests/DependencyInjectionTests.cs` — modified — `BuildProvider()` adds `services.AddLogging();` (risk B) and the six `Jwt:*` keys to its in-memory configuration, otherwise `AddEnvanexIdentity`'s guard throws and every existing DI test goes red.
- `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs` — modified — `UseSetting` for the six `Jwt:*` keys.
- `tests/Envanex.IntegrationTests/Fixtures/RateLimitedWebApplicationFactory.cs` — modified — the same six.
- `tests/Envanex.IntegrationTests/Fixtures/IdentitySeeder.cs` — created — research decision 6: users created through `UserManager` from a service scope, with a real password.
- `tests/Envanex.IntegrationTests/Fixtures/CountingUserManager.cs` — created — the instrument that makes Decision 6's ordering and call-count claims assertable.

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

internal static class IdentitySeeder
{
    public static Task<Guid> CreateUserAsync(IServiceProvider services, string email, string password);
    public static Task<int> GetAccessFailedCountAsync(IServiceProvider services, string email);
    public static Task<DateTimeOffset?> GetLockoutEndAsync(IServiceProvider services, string email);
    public static Task LockOutAsync(IServiceProvider services, string email);
}

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

**Carried forward from the Phase 1 re-review (db-reviewer, `d71f4f9`).** `IdentitySeeder.CreateUserAsync` must check the `IdentityResult` that `UserManager.CreateAsync` returns and throw on failure. With `RequireUniqueEmail = true` and the now-unique `EmailIndex`, a duplicate email comes back as a failed `IdentityResult` with `DuplicateEmail` rather than as an exception. A seeder that discards the result creates no user and reports success, and the test that follows fails somewhere else with a confusing "user not found".

`CountingUserManager` forwards the full `UserManager<TUser>` constructor list (`IUserStore<EnvanexUser>`, `IOptions<IdentityOptions>`, `IPasswordHasher<EnvanexUser>`, `IEnumerable<IUserValidator<EnvanexUser>>`, `IEnumerable<IPasswordValidator<EnvanexUser>>`, `ILookupNormalizer`, `IdentityErrorDescriber`, `IServiceProvider`, `ILogger<UserManager<EnvanexUser>>`) to `base`; each override records its name in `CallLog`, increments its counter, and delegates. Registered after `AddEnvanexIdentity` with `services.AddScoped<UserManager<EnvanexUser>, CountingUserManager>()`. `CountingUserManager` is test-only and is never referenced by production code — no seam is added to `src/` for any test in this plan.

### `AddEnvanexIdentity` — exact behaviour

1. `ArgumentNullException.ThrowIfNull` on both parameters.
2. Bind `configuration.GetSection(JwtOptions.SectionName)` into a `JwtOptions`, call `JwtOptionsGuard.ThrowIfInvalid(...)` immediately (fail fast at startup, not at first login), then `services.Configure<JwtOptions>(section)`.
3. `services.TryAddSingleton(TimeProvider.System)` — `TryAdd` so a test can register `FakeTimeProvider` first and win.
4. `services.AddIdentityCore<EnvanexUser>(options => ...)` with `User.RequireUniqueEmail = true`; `Password.RequiredLength = 12`, `RequireDigit`, `RequireLowercase`, `RequireUppercase` true, `RequireNonAlphanumeric` false; `Lockout.MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15)`, `AllowedForNewUsers = true`; then `.AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<EnvanexIdentityDbContext>()`. **`AddIdentityCore`, not `AddIdentity`, and no `SignInManager`** — `SignInManager` lives in the shared framework and would force a `FrameworkReference` into a class library.
5. `services.AddScoped<IIdentityService, IdentityService>();` `services.AddScoped<IRefreshTokenService, RefreshTokenService>();` `services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();`

### `IdentityService.ValidateCredentialsAsync` — exact behaviour *(Decisions 5 and 6)*

`ArgumentNullException.ThrowIfNull` on `email` and `password`. **Every failure path returns the same `AuthErrors.InvalidCredentials` instance**, so no branch can leak which failure occurred.

1. `FindByEmailAsync(email)`. Null → return `InvalidCredentials` immediately; no password verification (the known unknown-email gap, asserted rather than assumed).
2. **`CheckPasswordAsync(user, password)` — always, unconditionally, before any lockout check.** Capture the boolean; do not act on it yet.
3. `IsLockedOutAsync(user)`.
4. Locked out → log `Warning` "Login attempt against locked-out account {UserId}" and return `InvalidCredentials`. **Do not call `AccessFailedAsync`** and **do not call `ResetAccessFailedCountAsync`**, whatever the password was. Incrementing would let an attacker extend a victim's lockout indefinitely; resetting would hand a locked-out account a way back in.
5. Not locked out, password false → `AccessFailedAsync(user)` **exactly once**; then read the tracked `user.LockoutEnd` (risk F) and if it is now in the future relative to `timeProvider.GetUtcNow()`, log `Warning` "Account {UserId} locked out after {MaxFailedAccessAttempts} failed attempts". Return `InvalidCredentials`.
6. Not locked out, password true → `ResetAccessFailedCountAsync(user)` → `Result.Success(new AuthenticatedUser(user.Id, user.Email!, user.UserName!))`.

There is deliberately **no** post-increment `IsLockedOutAsync` re-check: with one error code there is nothing to choose, and its absence is what structurally guarantees `AccessFailedAsync` cannot fire twice. `TimeProvider` is injected solely for the step-5 log condition. Logs carry the user id, never the email or password.

**Testing note the coder needs before starting.** `UserManager.IsLockedOutAsync` reads the system clock internally, not the injected `TimeProvider`. Advancing `FakeTimeProvider` by 16 minutes does **not** clear an Identity lockout, and no test here tries to. `ValidateCredentialsAsync_FifthWrongPassword_ShouldSetLockoutEndAboutFifteenMinutesAhead` asserts the stored value with a tolerance instead. Do not spend a cycle on this; it is a framework property, not a defect to fix in 6a.

### `RefreshTokenService.RotateAsync` — exact behaviour

All reads go through `EnvanexIdentityDbContext`; no raw SQL.

1. `ArgumentNullException.ThrowIfNull(presentedToken)`; blank → `InvalidRefreshToken`.
2. `RefreshTokenHasher.Hash(presentedToken)`, then a single-row query on `TokenHash` equality (the unique index seek).
3. Not found → `InvalidRefreshToken`.
4. `RefreshTokenHasher.Matches(presentedToken, row.TokenHash)` false → `InvalidRefreshToken`. Redundant against the index, but it makes constant-time comparison a property of the code rather than of the query plan.
5. `RevokedAt is not null` → `InvalidRefreshToken`.
6. `RotatedAt is not null` → **reuse**: load every row with the same `FamilyId` and `RevokedAt IS NULL`, `Revoke(now, Reuse)` each, save, return `RefreshTokenReused`. Grace period zero (research decision 7). **The consumed row is never deleted** — that is what makes generation-1 replay detectable after three rotations.
7. `now >= ExpiresAt || now >= FamilyExpiresAt` → revoke the family's live rows with reason `Expired`, save, return `RefreshTokenExpired`.
8. Otherwise, in **one `SaveChangesAsync`**: `parent.MarkRotated(now, childId)`, where `childId` is the pre-generated `Guid` of the child, and add the child built by `parent.CreateChild(...)`. The parent's `RowVersion` predicate is what decides the race. **No explicit transaction and no split into two saves:** a single `SaveChangesAsync` is already atomic, and production behaviour is not reshaped to make an exception type predictable — step 9 removes any need to tell the two failures apart.
9. **One loser exit, returning `InvalidRefreshToken`.** Catch `DbUpdateConcurrencyException` (another rotation or a revocation changed the parent's `RowVersion`) and `DbUpdateException` whose inner is `SqlException { Number: 2601 or 2627 }` (the child INSERT hit `IX_RefreshTokens_FamilyId_Live`) **in the same branch**, and map both to `InvalidRefreshToken`. They report the same fact — another rotation consumed this token first — and nothing downstream benefits from telling them apart. Handling them together also removes the dependency on EF Core's internal UPDATE/INSERT batch ordering, since either ordering produces the same result. Detect the unique violation with the pattern `UnitOfWork` already uses. Do **not** route either through `IUnitOfWork`, which is bound to `EnvanexDbContext`.
10. Load the user for `RotatedRefreshToken.User` from `EnvanexIdentityDbContext`, in the same round trip as step 2 where practical.

**The unique-violation path, stated so nobody writes a test that pretends to cover it.** For the child INSERT to violate `IX_RefreshTokens_FamilyId_Live`, a second live row must exist in the family at insert time. The parent is stamped under a `RowVersion` predicate in the same save, so a racing rotation loses there first; and no legal database state has two live rows in one family, because that is exactly what the index forbids — which means the state cannot be constructed by a test either. **The unique-violation path is expected to be unreachable from `RotateAsync` under current EF behaviour, is retained as a database-level invariant, and is handled in the same branch as the concurrency failure so that a future change to save ordering cannot turn it into an unhandled exception.** `IX_RefreshTokens_FamilyId_Live` stays regardless of whether the application ever trips it: it enforces "at most one live token per family" at the database level, which is its purpose. The index itself is protected by the deterministic Phase 2 test `RefreshTokens_InsertingASecondLiveTokenForTheSameFamily_ShouldThrowUniqueViolation`.

### `RefreshTokenService.RevokeFamilyAsync` — exact behaviour

Logout, research decision 8: one family only, never a user's other sessions.

1. Look up by hash. **Not found returns `Result.Success()`** — deliberately idempotent, so logout is not an oracle for token existence.
2. Found → a bounded retry loop, at most `RevocationRetryLimit` (3) attempts. Each attempt loads every row of that `FamilyId` where `RevokedAt IS NULL`, calls `Revoke(now, Logout)` on each, and saves.
3. On `DbUpdateConcurrencyException`, discard the tracked entities and retry. Not optional: a rotation committing between this method's read and its write produces a **new live child the first pass never saw** — the "rotation racing revocation" trap. The loop terminates because each pass observes a strictly later generation.
4. After a successful save, re-query for any remaining live row in the family. If one exists and attempts remain, loop. If one exists and attempts are exhausted, log `Error` "Failed to revoke refresh token family {FamilyId} after {Attempts} attempts" and return `Result.Failure(AuthErrors.InvalidRefreshToken)`. **A logout that cannot guarantee revocation must not report success.**
5. Otherwise `Result.Success()`.

The exhaustion branch cannot be forced deterministically and has no dedicated integration test; it is covered at the handler level by `LogoutCommandHandlerTests.HandleAsync_RevocationFailure_ShouldPropagateTheFailure`, and its intended outcome is asserted by `RefreshTokenConcurrencyTests.RevokeFamilyAsync_RacingARotation_ShouldLeaveNoLiveRowInTheFamily`.

### `JwtAccessTokenIssuer.Issue` — exact behaviour

`ArgumentNullException.ThrowIfNull(user)`. Claims: `sub` = `user.Id`, `email` = `user.Email`, `jti` = a fresh `Guid.NewGuid()`, plus `iss`, `aud`, `iat`, `nbf`, `exp`. `exp = timeProvider.GetUtcNow() + TimeSpan.FromMinutes(options.AccessTokenMinutes)`. HS256 over a `SymmetricSecurityKey` built from `Encoding.UTF8.GetBytes(options.SigningKey)`, produced by `JsonWebTokenHandler.CreateToken`. Singleton, so the `SigningCredentials` are built once.

### Tests to add

All in `tests/Envanex.IntegrationTests/Identity/`, `[Collection(DatabaseCollection.Name)]`, each class calling `_fixture.ResetIdentityAsync()` in `InitializeAsync`. Services come from a `ServiceProvider` built the way `DependencyInjectionTests.BuildProvider()` does, with `AddLogging()`, `FakeTimeProvider` registered **before** `AddInfrastructure` so `TryAddSingleton` yields to it, and `CountingUserManager` registered after.

`IdentityServiceTests`:
- `ValidateCredentialsAsync_CorrectPassword_ShouldReturnAuthenticatedUserWithMatchingId`
- `ValidateCredentialsAsync_UnknownEmail_ShouldReturnInvalidCredentials`
- `ValidateCredentialsAsync_WrongPassword_ShouldReturnInvalidCredentials`
- `ValidateCredentialsAsync_WrongPassword_ShouldIncrementAccessFailedCount`
- `ValidateCredentialsAsync_FifthWrongPassword_ShouldReturnInvalidCredentials` *(Decision 5)*
- `ValidateCredentialsAsync_FifthWrongPassword_ShouldSetLockoutEndAboutFifteenMinutesAhead` — asserts the stored value with a tolerance, not elapsed time (risk F, and the testing note above).
- `ValidateCredentialsAsync_CorrectPasswordWhileLockedOut_ShouldReturnInvalidCredentials` *(Decision 5)*
- `ValidateCredentialsAsync_CorrectPasswordWhileLockedOut_ShouldLeaveAccessFailedCountUnchanged`
- `ValidateCredentialsAsync_UnknownEmailWrongPasswordAndLockedOut_ShouldAllReturnTheSameErrorCode`
- `ValidateCredentialsAsync_LockedOut_ShouldLogAWarningCarryingTheUserId`
- `ValidateCredentialsAsync_LockedOut_ShouldNotLogTheEmailOrPassword`
- `ValidateCredentialsAsync_CorrectPasswordAfterTwoFailures_ShouldResetAccessFailedCountToZero`
- `ValidateCredentialsAsync_NullEmail_ShouldThrowArgumentNullException`
- `ValidateCredentialsAsync_NullPassword_ShouldThrowArgumentNullException`

`IdentityServiceTimingOrderTests` — new class (Decision 6), driven by `CountingUserManager`, never by a stopwatch:
- `ValidateCredentialsAsync_LockedOutUser_ShouldStillCallCheckPasswordAsyncExactlyOnce` — the core of Decision 6; red means the ~100 ms step change is back.
- `ValidateCredentialsAsync_LockedOutUser_ShouldCallCheckPasswordAsyncBeforeIsLockedOutAsync` — asserts `CallLog` begins `["CheckPasswordAsync", "IsLockedOutAsync"]`.
- `ValidateCredentialsAsync_LockedOutUserWithTheCorrectPassword_ShouldStillCallCheckPasswordAsyncExactlyOnce`
- `ValidateCredentialsAsync_LockedOutUser_ShouldNotCallAccessFailedAsync`
- `ValidateCredentialsAsync_LockedOutUserWithTheCorrectPassword_ShouldNotCallResetAccessFailedCountAsync`
- `ValidateCredentialsAsync_WrongPassword_ShouldCallAccessFailedAsyncExactlyOnce` — the double-increment guard.
- `ValidateCredentialsAsync_FifthWrongPassword_ShouldCallAccessFailedAsyncExactlyOnce` — the attempt the removed post-increment re-check used to put at risk.
- `ValidateCredentialsAsync_CorrectPassword_ShouldNotCallAccessFailedAsync`
- `ValidateCredentialsAsync_CorrectPassword_ShouldCallResetAccessFailedCountAsyncExactlyOnce`
- `ValidateCredentialsAsync_UnknownEmail_ShouldNotCallCheckPasswordAsync` — documents the remaining gap as deliberate; closing it means inverting a named test.

`RefreshTokenServiceTests`:
- `IssueAsync_ShouldPersistOnlyTheHashAndNeverThePlaintextToken`
- `IssueAsync_ShouldSetExpiresAtToSevenDaysAfterFakeNow` *(Decision 1, from the test's own configuration)*
- `IssueAsync_ShouldSetFamilyExpiresAtToThirtyDaysAfterFakeNow` *(Decisions 1 and 2)*
- `IssueAsync_CalledTwiceForOneUser_ShouldProduceTwoIndependentFamilies`
- `RotateAsync_ValidToken_ShouldReturnADifferentTokenString` — also the risk-G canary.
- `RotateAsync_ValidToken_ShouldStampParentRotatedAtAndReplacedByTokenId`
- `RotateAsync_ValidToken_ShouldNotDeleteTheConsumedRow`
- `RotateAsync_ValidToken_ShouldKeepTheSameFamilyId`
- `RotateAsync_ValidToken_ShouldCarryFamilyExpiresAtForwardUnchanged` *(Decision 2)*
- `RotateAsync_ValidToken_ShouldResetTheIdleWindowToSevenDays` *(Decisions 1 and 2)*
- `RotateAsync_ValidToken_ShouldReturnTheOwningUser`
- `RotateAsync_UnknownToken_ShouldReturnInvalidRefreshToken`
- `RotateAsync_BlankToken_ShouldReturnInvalidRefreshToken`
- `RotateAsync_NullToken_ShouldThrowArgumentNullException`
- `RotateAsync_ReplayOfGenerationOneAfterASingleRotation_ShouldReturnRefreshTokenReused` — the shallow case, and the direct guard for step 6.
- `RotateAsync_ReplayOfGenerationOneAfterThreeRotations_ShouldReturnRefreshTokenReusedAndRevokeTheWholeFamilyAndRejectTheLiveToken` — self-contained: (a) three rotations succeed; (b) replaying generation 1 returns `RefreshTokenReused`; (c) every row of the family has `RevokedAt` with reason `Reuse`; (d) the generation-4 token then returns `InvalidRefreshToken`. RFC 9700 §4.14.2, and the regression for "detection dies after one generation".
- `RotateAsync_AfterReuseDetection_ShouldNotTouchTheUsersOtherFamilies`
- `RotateAsync_TokenPastTheIdleWindow_ShouldReturnRefreshTokenExpired` — advance the fake clock 8 days.
- `RotateAsync_TokenRotatedEveryFiveDaysButPastTheThirtyDayCap_ShouldReturnRefreshTokenExpired` — rotate on days 5, 10, 15, 20, 25, attempt on day 31, so the idle window is never the reason.
- `RotateAsync_ExpiredToken_ShouldRevokeTheFamily`
- `RotateAsync_RevokedToken_ShouldReturnInvalidRefreshToken`
- `RevokeFamilyAsync_ValidToken_ShouldRevokeEveryLiveRowInThatFamily`
- `RevokeFamilyAsync_ShouldNotTouchOtherFamiliesOfTheSameUser` — research decision 8.
- `RevokeFamilyAsync_UnknownToken_ShouldReturnSuccess`
- `RevokeFamilyAsync_CalledTwice_ShouldReturnSuccessBothTimes`
- `RotateAsync_AfterRevokeFamily_ShouldReturnInvalidRefreshToken`

`RefreshTokenConcurrencyTests` — `[Collection(DatabaseCollection.Name)]`, against the real container. `EnvanexIdentityDbContext` is not thread-safe, so each concurrent task resolves its **own** `IServiceScope`, context and service; `FakeTimeProvider` is shared read-only. `ConcurrencyIterations` is a constant on the class (≥ 20) so the human can turn it down without hunting through methods. **No barrier, hook, seam or internal-visibility change is added to production code for these tests.**

- `RotateAsync_TwoConcurrentRotationsOfTheSameToken_ShouldLetExactlyOneSucceed` — holds under every interleaving: whichever racer arrives second either loses the `RowVersion` predicate or detects reuse, and both are failures.
- `RotateAsync_TwoConcurrentRotationsOfTheSameToken_ShouldNeverLeaveTwoLiveRowsInTheFamily` — after each iteration, rows with that `FamilyId` and `RotatedAt IS NULL AND RevokedAt IS NULL` number 0 or 1. The family invariant, independent of who won.
- `RotateAsync_TwoConcurrentRotationsOfTheSameToken_ShouldEndInOneOfTheTwoAcceptedStates` — **replaces revision 3's three strict loser assertions** (finding 2). Per iteration, exactly one result succeeds and the non-winner's error code decides which post-state must hold:
  - `Auth.InvalidRefreshToken` (the loser exit) → exactly **one** live row; the parent carries `RotatedAt` and a `ReplacedByTokenId` equal to that live row's `Id`; no row in the family carries `RevokedAt`.
  - `Auth.RefreshTokenReused` (the serialized interleaving, where the loser's read happened after the winner committed) → **zero** live rows; every row in the family carries `RevokedAt` with reason `Reuse`; the parent carries `RotatedAt`.
  - any other code, two successes, two failures, or an escaped exception → fail, naming what was observed.
  **This test asserts returned error codes and database post-state only. It never asserts which exception type was thrown** — that is exactly the distinction step 9 collapses, and an assertion on it would re-introduce the dependency on save ordering that step 9 removes.
  **What its failure means:** removing the catch in step 9 turns it red, because an escaped `DbUpdateConcurrencyException` is not one of the accepted states; dropping `MarkRotated`'s `ReplacedByTokenId` stamp turns it red on the `InvalidRefreshToken` post-state. It does **not** guard the reuse branch itself — `RotateAsync_ReplayOfGenerationOneAfterASingleRotation_ShouldReturnRefreshTokenReused` does that deterministically — and it does **not** guard `IX_RefreshTokens_FamilyId_Live`, which the Phase 2 schema test guards.
- `RevokeFamilyAsync_RacingARotation_ShouldLeaveNoLiveRowInTheFamily` — `RotateAsync` and `RevokeFamilyAsync` on the same token in two scopes via `Task.WhenAll`; afterwards no row with that `FamilyId` is live, whoever won. This is what the bounded retry loop exists to satisfy; removing the loop turns it red.
- `RevokeFamilyAsync_RacingARotation_ShouldNotThrow` — neither task surfaces an exception; both return a `Result`.

`JwtAccessTokenIssuerTests`:
- `Issue_ShouldProduceATokenCarryingSubEmailAndJti`
- `Issue_ShouldSetExpToFifteenMinutesAfterFakeNow`
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
- `AddEnvanexIdentity_ShouldResolveIIdentityService` / `...IRefreshTokenService` / `...IAccessTokenIssuer` / `...UserManagerOfEnvanexUser`
- `AddEnvanexIdentity_ShouldConfigureMaxFailedAccessAttemptsAsFive` — Decision 5 hides lockout from the response, so `IOptions<IdentityOptions>` is the only place the setting is visible.
- `AddEnvanexIdentity_ShouldConfigureDefaultLockoutTimeSpanAsFifteenMinutes`
- `AddEnvanexIdentity_ShouldNotOverrideAPreRegisteredTimeProvider`
- `AddEnvanexIdentity_WithNoTimeProviderRegistered_ShouldResolveTimeProviderSystem`

`DependencyInjectionTests` — modified: `AllRegisteredServices_ShouldResolve` gains `IIdentityService`, `IRefreshTokenService`, `IAccessTokenIssuer`.

### Validation

```
dotnet list src/Envanex.Infrastructure/Envanex.Infrastructure.csproj package --include-transitive
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test tests/Envanex.IntegrationTests --filter "FullyQualifiedName~AllRegisteredServices_ShouldResolve"
dotnet test tests/Envanex.IntegrationTests --filter "FullyQualifiedName~RefreshTokenConcurrencyTests"
dotnet test tests/Envanex.IntegrationTests
dotnet test
```

The phase report must include:
1. **The SQL for the `TokenHash` lookup and for the family query**, captured by raising `Microsoft.EntityFrameworkCore.Database.Command` to `Information` for one run — plus, for the family query, its **execution plan**: run the captured statement against the dev container with `SET SHOWPLAN_TEXT ON` (or `SET STATISTICS XML ON`) through `sqlcmd` and paste the physical operator. db-reviewer must be able to confirm a seek on `IX_RefreshTokens_TokenHash` **and** a seek on `IX_RefreshTokens_FamilyId`, not a clustered scan (finding 4).
2. **The statement order inside rotation** — the log must show the parent `UPDATE` before the child `INSERT` (risk G).
3. **The measured cost of the concurrency loops, as numbers**: wall-clock for `RefreshTokenConcurrencyTests` alone; wall-clock for the whole `Envanex.IntegrationTests` suite after this phase; the delta against the 24 s baseline in the research file; and the `ConcurrencyIterations` value used. If `RefreshTokenConcurrencyTests` alone exceeds 60 s, stop and report — the iteration count is a knob the human turns, not one the coder quietly lowers.
4. The `dotnet list --include-transitive` output for risk (A).

---

## Phase 5: Login / refresh / logout use cases in the Application layer

**No schema, no migrations, no queries. db-reviewer not required.** Depends on Decision 5.

Per research decision 2 these are ordinary `ICommandHandler` implementations with validators, decorator registration and DI test entries.

### Files

- `src/Envanex.Application/Authentication/Commands/{LoginCommand,LoginCommandHandler,RefreshTokenCommand,RefreshTokenCommandHandler,LogoutCommand,LogoutCommandHandler}.cs` — created
- `src/Envanex.Application/Authentication/Validators/{LoginCommandValidator,RefreshTokenCommandValidator,LogoutCommandValidator}.cs` — created
- `src/Envanex.Application/DependencyInjection.cs` — modified — three validators as singletons, three concrete handlers as scoped, three `ICommandHandler<,>` registrations wrapped in `ValidationDecorator`, following the existing hand-written shape exactly.
- `tests/Envanex.Application.Tests/Fakes/{FakeIdentityService,FakeRefreshTokenService,FakeAccessTokenIssuer}.cs` — created
- `tests/Envanex.IntegrationTests/DependencyInjectionTests.cs` — modified — three resolve assertions and three `CommandHandlerTypes()` rows. A handler missing from `CommandHandlerTypes()` is an unvalidated handler that passes silently.

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

`LoginCommandHandler` order: `ThrowIfNull(command)`; validate credentials; on failure return that error **without issuing a refresh token**; on success issue the refresh token, then the access token, then compose `AuthenticationResponse` with `TokenType = "Bearer"`. The handler never inspects or reclassifies the error — Decisions 5 and 6 live in `IdentityService`.

Validator error codes: `.WithErrorCode("Auth.EmailRequired")` on email `NotEmpty`, `"Auth.EmailInvalid"` on `EmailAddress`, `"Auth.PasswordRequired"` on password `NotEmpty`; both token validators use `"Auth.RefreshTokenRequired"`. No maximum-length rule on the password.

### Tests to add

`LoginCommandHandlerTests`: `HandleAsync_ValidCredentials_ShouldReturnAnAccessTokenAndARefreshToken`, `..._ShouldReturnBearerAsTheTokenType`, `..._ShouldReturnBothExpiryTimestamps`, `HandleAsync_InvalidCredentials_ShouldReturnAuthInvalidCredentials`, `..._ShouldNotIssueARefreshToken`, `..._ShouldNotIssueAnAccessToken`, `HandleAsync_LockedOutUser_ShouldReturnAuthInvalidCredentials` *(Decision 5 — the fake returns `InvalidCredentials` because that is what the real service returns)*, `HandleAsync_NullCommand_ShouldThrowArgumentNullException`.

`RefreshTokenCommandHandlerTests`: `HandleAsync_ValidRefreshToken_ShouldReturnANewRefreshToken`, `..._ShouldReturnANewAccessTokenForTheRotatedUser`, `HandleAsync_ReusedRefreshToken_ShouldReturnAuthRefreshTokenReused`, `HandleAsync_ExpiredRefreshToken_ShouldReturnAuthRefreshTokenExpired`, `HandleAsync_UnknownRefreshToken_ShouldReturnAuthInvalidRefreshToken`, `HandleAsync_RotationFailure_ShouldNotIssueAnAccessToken`, `HandleAsync_NullCommand_ShouldThrowArgumentNullException`.

`LogoutCommandHandlerTests`: `HandleAsync_ValidRefreshToken_ShouldCallRevokeFamilyOnce`, `..._ShouldReturnSuccess`, `HandleAsync_UnknownRefreshToken_ShouldReturnSuccess`, `HandleAsync_RevocationFailure_ShouldPropagateTheFailure` (covers Phase 4's exhausted-retry branch), `HandleAsync_NullCommand_ShouldThrowArgumentNullException`.

`LoginCommandValidatorTests`: `Validate_EmptyEmail_ShouldFailWithAuthEmailRequired`, `Validate_MalformedEmail_ShouldFailWithAuthEmailInvalid`, `Validate_EmptyPassword_ShouldFailWithAuthPasswordRequired`, `Validate_ValidCommand_ShouldPass`.
`RefreshTokenCommandValidatorTests`: `Validate_EmptyRefreshToken_ShouldFailWithAuthRefreshTokenRequired`, `Validate_WhitespaceRefreshToken_ShouldFailWithAuthRefreshTokenRequired`, `Validate_ValidCommand_ShouldPass`.
`LogoutCommandValidatorTests`: `Validate_EmptyRefreshToken_ShouldFailWithAuthRefreshTokenRequired`, `Validate_ValidCommand_ShouldPass`.

`DependencyInjectionTests` — modified: three new resolve assertions, and three `CommandHandlerTypes()` rows mapping each `ICommandHandler<,>` to its `ValidationDecorator<,>`.

### Validation

```
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

---

## Phase 6: Web host — endpoints, JWT bearer scheme, pipeline placement, login rate limit

**No schema, no migrations, no queries. db-reviewer not required.** Depends on Decisions 4, 5, 6.

### Files

- `src/Envanex.Web/Extensions/JwtAuthenticationExtensions.cs` — created. (The JwtBearer `PackageReference` already landed in Phase 1; `Envanex.Web.csproj` is not edited here.)
- `src/Envanex.Web/RateLimiting/LoginRateLimitPartition.cs` — created — the single named edit site for PR 7 *(Decision 4)*.
- `src/Envanex.Web/Controllers/AuthController.cs` — created.
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — modified — add `ToNoContentActionResult<T>`.
- `src/Envanex.Web/Program.cs` — modified — see below.
- `src/Envanex.Web/appsettings.json` — modified — add `"Login": { "Enabled": true, "PermitLimit": 5, "WindowSeconds": 300 }` inside `RateLimiting`.
- `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs` — **modified — add `builder.UseSetting("RateLimiting:Login:Enabled", "false");`** with the comment: *the shared factory lives for the whole collection and every login lands in the single `"unknown"` partition, so a real 5/300 s budget would be consumed across classes and produce flaky 429s* (finding 1).
- `tests/Envanex.IntegrationTests/Fixtures/RateLimitedWebApplicationFactory.cs` — **modified — the same `RateLimiting:Login:Enabled=false` line**, so enabling the global limiter there never silently enables the login policy too.
- `tests/Envanex.IntegrationTests/Fixtures/LoginRateLimitedWebApplicationFactory.cs` — created.
- `docs/roadmap.md` — modified — mark PR 6a done; record the gaps, first one as **BLOCKER**:
  - **BLOCKER (PR 7) — login rate-limit partition key behind a reverse proxy.** `LoginRateLimitPartition.GetKey` reads `Connection.RemoteIpAddress`, which on Azure App Service is the front end's address for every request; all users collapse into one partition and 5 per 5 minutes becomes global — login stops working for everyone after three sign-ins. Closes in PR 7 by configuring `UseForwardedHeaders` with the real `KnownProxies`/`KnownNetworks` and deleting `LoginRateLimitPartitionTests.GetKey_ShouldIgnoreXForwardedForUntilPr7`. **Must be closed before the first public deploy.**
  - Login timing side channel, unknown-email half (Decision 6).
  - Login timing side channel, residual `AccessFailedAsync` write (Decision 6).
  - Zero reuse-detection grace period (research decision 7).
  - No `JwtBearerEvents.OnChallenge` body until 6b.
  - No refresh-token pruning job — the table is append-only and grows without bound.
  - The 2601/2627 unique-violation path in `RotateAsync` is unreachable by construction and has no test that reaches it; it shares the concurrency failure's branch so that a change to save ordering cannot turn it into an unhandled exception (Phase 4 analysis).
  - `SqlServerFixture` reset still hand-maintained (6a handled the auth half; PR 8 owns the full fix).
  - Passkeys deferred.
- `docs/adr/0007-jwt-access-tokens-and-rotated-refresh-tokens.md` — created **empty, title line only**.

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
    /// Partition key used when the connection carries no remote address, which is the case
    /// under WebApplicationFactory and — until PR 7 configures forwarded headers — would also
    /// be the effective case behind a reverse proxy.
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

public static IActionResult ToNoContentActionResult<T>(this Result<T> result);
```

`LoginRateLimitPartition.GetKey` calls `ArgumentNullException.ThrowIfNull(context)` and returns `context.Connection.RemoteIpAddress?.ToString() ?? UnknownPartitionKey`. It deliberately does **not** read `X-Forwarded-For` (Decision 4).

`AddEnvanexJwtBearer` binds `Jwt:*` into a `JwtOptions`, calls `JwtOptionsGuard.ThrowIfInvalid`, then `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = ...)` with `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all true and **`ClockSkew = TimeSpan.Zero`** — the default five-minute skew would make a 15-minute token last 20 and make every fake-clock expiry test lie.

There is deliberately **no `JwtBearerEvents.OnChallenge`**: no endpoint is `[Authorize]` in 6a, so a handler would be untestable dead code. It ships with the first `[Authorize]` in 6b.

`Logout` returns `result.ToNoContentActionResult()` — 204 on success, the mapped ProblemDetails on failure.

### `Program.cs` — exact changes

1. After `builder.Services.AddInfrastructure(builder.Configuration);` add `builder.Services.AddEnvanexJwtBearer(builder.Configuration);` and `builder.Services.AddAuthorization();`.
2. **Hoist `permitLimit` and `windowSeconds`** (finding 10). They are declared at `Program.cs:30-31` *inside* `if (rateLimitingEnabled)` today; move both declarations up next to `rateLimitingEnabled` (`Program.cs:26`) before moving `AddRateLimiter` out of the block, or the lambda will not compile.
3. Move `builder.Services.AddRateLimiter(options => { ... })` **out** of `if (rateLimitingEnabled)`. Inside the lambda keep `RejectionStatusCode` and `OnRejected` exactly as they are; assign `options.GlobalLimiter` **only** when `rateLimitingEnabled`; and always register the named policy `AuthController.LoginRateLimitPolicy`, reading `RateLimiting:Login:{Enabled,PermitLimit,WindowSeconds}`. When `Login:Enabled` is false the policy resolves to `RateLimitPartition.GetNoLimiter`; when true it is a fixed-window limiter whose partition key comes from `LoginRateLimitPartition.GetKey(context)` — a call, not an inline lambda.
4. Make `app.UseRateLimiter();` unconditional, in the position it occupies today (after `UseMiddleware<SecurityHeadersMiddleware>()`, before `UseStatusCodePagesWithReExecute`). The policy must be visible to the middleware or `[EnableRateLimiting("login")]` throws at endpoint build time.
5. Insert, **after `app.UseHttpsRedirection();` and before `app.UseAntiforgery();`**: `app.UseAuthentication();` then `app.UseAuthorization();`.

Why that position: **after `UseHttpsRedirection()`** because there is no point authenticating a request about to be 307'd and a bearer token should not be parsed off a plaintext request; **before `UseAntiforgery()` and the endpoint mappings** because antiforgery, routing, the Blazor circuit and MVC filters all read `HttpContext.User`; **inside the `UseStatusCodePagesWithReExecute("/not-found", ...)` wrapper**, which is the documented risk — a bodiless 401 under that wrapper is re-executed as the not-found page. In 6a no bodiless 401 is reachable, because every 401 comes from `ResultExtensions` with a ProblemDetails body, and `AuthPipelineTests.Refresh_WithUnknownToken_ShouldReturn401ProblemJsonAndNotTheNotFoundPage` locks that in. The bodiless challenge 401 becomes reachable in 6b together with the `OnChallenge` body that fixes it.

`LoginRateLimitedWebApplicationFactory` sets `ConnectionStrings:EnvanexDb`, the six `Jwt:*` keys, `RateLimiting:Enabled=false` (so the global limiter cannot mask the result), `RateLimiting:Login:Enabled=true`, `RateLimiting:Login:PermitLimit=2`, `RateLimiting:Login:WindowSeconds=60`.

### Tests to add

`tests/Envanex.IntegrationTests/Api/AuthApiTests.cs` — new, `[Collection(DatabaseCollection.Name)]`, `InitializeAsync` calls `ResetIdentityAsync()` and seeds one user via `IdentitySeeder.CreateUserAsync(fixture.WebApplicationFactory.Services, ...)`:
- `Login_WithValidCredentials_ShouldReturn200`
- `Login_WithValidCredentials_ShouldReturnAccessTokenRefreshTokenAndBothExpiries`
- `Login_WithValidCredentials_ShouldReturnBearerAsTokenType`
- `Login_WithValidCredentials_ShouldReturnAnAccessTokenThatValidatesAgainstTheConfiguredKey`
- `Login_WithWrongPassword_ShouldReturn401ProblemJson`
- `Login_WithUnknownEmail_ShouldReturn401ProblemJson`
- `Login_AfterFiveWrongPasswords_ShouldReturn401` *(Decision 5)*
- `Login_AfterFiveWrongPasswords_ShouldAlsoReturn401ForTheCorrectPassword`
- `Login_UnknownEmailWrongPasswordAndLockedOutAccount_ShouldReturnIdenticalStatusContentTypeAndBody` — **the Decision 5 test.** All three requests; the three `StatusCode`s equal, the three `Content-Type`s equal, the three raw bodies byte-for-byte equal.
- `Login_FailureDetail_ShouldMentionTheLockoutPolicyWithoutConfirmingIt` *(Decision 6)* — parses `detail` from a wrong-password response; contains "kilitlenir", does not contain "kilitlendi".
- **`Login_EightConsecutiveAttempts_ShouldNeverReturn429`** — **the finding-1 guard.** Eight wrong-password logins against the shared factory, all 401, none 429. The production `PermitLimit` is 5, so this goes red the moment `RateLimiting:Login:Enabled=false` is dropped from `EnvanexWebApplicationFactory`. It is behavioural rather than structural because `RateLimiterOptions`' policy map is not a public surface (unverified; `GlobalLimiter` is, which is why the sibling test below can be structural).
- `Login_WithEmptyPassword_ShouldReturn400WithTheTurkishParolaRequiredMessage`
- `Login_WithMalformedEmail_ShouldReturn400WithTheTurkishEmailInvalidMessage`
- `Login_ShouldNotSetAnySetCookieHeader` — research decision 9.
- `Refresh_WithTheTokenFromLogin_ShouldReturn200`
- `Refresh_WithTheTokenFromLogin_ShouldReturnADifferentRefreshToken`
- `Refresh_WithTheTokenFromLogin_ShouldReturnADifferentAccessToken`
- `Refresh_ReplayingAConsumedToken_ShouldReturn401`
- `Refresh_ReplayingAConsumedToken_ShouldAlsoInvalidateTheCurrentToken` — RFC 9700 §4.14.2 end to end.
- `Logout_WithAValidToken_ShouldReturn204`
- `Logout_WithAValidToken_ThenRefresh_ShouldReturn401`
- `Logout_WithAnUnknownToken_ShouldReturn204`
- `Logout_ShouldNotAffectASecondSessionOfTheSameUser` — research decision 8.
- `Register_ShouldReturn404` — research decision 11; asserts no user-creating endpoint snuck in.

`tests/Envanex.IntegrationTests/Api/AuthPipelineTests.cs` — new, `[Collection(DatabaseCollection.Name)]`:
- `Refresh_WithUnknownToken_ShouldReturn401ProblemJsonAndNotTheNotFoundPage` — status 401, `Content-Type` starting `application/problem+json`, body does not contain `<html` (case-insensitive), parsed `status` is 401 — the assertion shape `RateLimiterTests` already uses for 429.
- `OpenEndpoint_WithNoAuthorizationHeader_ShouldStillReturn200`
- `OpenEndpoint_WithAMalformedBearerToken_ShouldStillReturn200`
- `OpenEndpoint_WithAnExpiredBearerToken_ShouldStillReturn200`
- `OpenEndpoint_WithAValidBearerToken_ShouldStillReturn200`

`tests/Envanex.IntegrationTests/Api/LoginRateLimitPartitionTests.cs` — new, no database, no `[Collection]`:
- `GetKey_WithARemoteIpAddress_ShouldReturnThatAddress`
- `GetKey_WithNoRemoteIpAddress_ShouldReturnTheUnknownPartitionKey`
- `GetKey_ShouldIgnoreXForwardedForUntilPr7` — **the PR 7 tripwire**; PR 7 must delete it when it configures `UseForwardedHeaders`.
- `GetKey_NullContext_ShouldThrowArgumentNullException`

`tests/Envanex.IntegrationTests/Api/LoginRateLimiterTests.cs` — new (finding 7). **`[Collection(DatabaseCollection.Name)]`** (it needs `_fixture.ConnectionString`), `IAsyncLifetime`, and **a fresh `LoginRateLimitedWebApplicationFactory` constructed per test in `InitializeAsync`**, disposed in `DisposeAsync`, exactly as `RateLimiterTests.cs:25-36` does — the 2-permit/60 s budget is per factory, so one factory per class would break at least four of the five cases. `InitializeAsync` also calls `_fixture.ResetIdentityAsync()` and seeds a user through the new factory's `Services`.
- `Login_ExceedingTheLoginRateLimit_ShouldReturn429ProblemJson`
- `Login_ExceedingTheLoginRateLimit_ShouldIncludeARetryAfterHeader`
- `Login_UnderTheLoginRateLimit_ShouldReturn401ForAWrongPassword` — proves the policy is not rejecting everything.
- `Login_FromTwoClientsOfTheSameFactory_ShouldShareTheSameRateLimitPartition` — two `HttpClient`s from one factory; the third request across both is rejected. The observable consequence of the `"unknown"` fallback and of the PR 7 blocker.
- `ProductsEndpoint_ShouldNotBeAffectedByTheLoginPolicy` — `GET /api/products/{id:guid}` three times; none is 429. Proves the policy is scoped to the login action.

`tests/Envanex.IntegrationTests/Api/RateLimiterTests.cs` — modified, the regression pair for the Decision 4 restructure:
- `SharedFactory_WithGlobalLimiterDisabled_ShouldNotRegisterAGlobalLimiter` — resolves `IOptions<RateLimiterOptions>` from `fixture.WebApplicationFactory.Services` and asserts `GlobalLimiter is null`. Proves the `if (rateLimitingEnabled)` guard survived the move. *(Unverified (D): that `GlobalLimiter` is readable from `IOptions<RateLimiterOptions>`. **Settled by** `dotnet build -warnaserror`; if it does not compile, drop this test and rely on the behavioural one below — do not invent a reflection workaround.)*
- `SharedFactory_WithGlobalLimiterDisabled_ShouldNotReject150ConsecutiveRequestsToTheUnitOfMeasuresList` — the behavioural check, **against `GET /api/unit-of-measures`** (finding 11a): a read endpoint, so it neither writes rows nor collides with the collection's reset semantics. 150 exceeds the production `PermitLimit` of 100, so an accidentally-active global limiter fails the test; a smaller number would pass with the limiter on and prove nothing. Assert all 150 responses are 200 and none is 429.
- The three existing cases stay unchanged and must still pass.

`tests/Envanex.IntegrationTests/Api/ResultExtensionsTests.cs` — modified:
- `ToNoContentActionResult_Success_ShouldReturn204`
- `ToNoContentActionResult_Failure_ShouldReturnTheMappedProblemDetails`
- `ToNoContentActionResult_NullResult_ShouldThrowArgumentNullException`

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

Manual smoke against the running host, all responses pasted into the phase report:
1. `POST /api/auth/login` with a seeded user returns 200 and a token pair.
2. `POST /api/auth/refresh` with that token returns a new pair.
3. Replaying the first refresh token returns 401, and the second token then also returns 401.
4. Login with a wrong password, an unknown email and against a locked-out account produce three responses identical in status line, `Content-Type` and body. **Paste all three verbatim.** Confirm by eye that `detail` reads "E-posta veya parola hatalı. Arka arkaya birkaç başarısız denemeden sonra hesap bir süreliğine kilitlenir." with the Turkish characters intact.
5. With `Jwt:SigningKey` unset, `dotnet run` fails at startup with the `InvalidOperationException` naming `Jwt:SigningKey` and the user-secrets command — not with a silently generated key.

---

## Expected test counts

| Suite | Before | After |
|---|---|---|
| `Envanex.Domain.Tests` | 113 | ~123 |
| `Envanex.Application.Tests` | 79 | ~120 |
| `Envanex.IntegrationTests` | 107 | ~248 |

Estimates. The exact number after each phase comes from that phase's `dotnet test` output, not from this table. `RefreshTokenConcurrencyTests` contributes 5 cases and is by far the slowest addition; Phase 4's validation requires its cost as three measured numbers against the research file's 24 s baseline.

---

## Rollback notes

Every phase is a separate commit on `feat/auth`, so `git revert` of one commit is the first resort.

- **Phase 1** — reverting code is not enough once `dotnet ef database update` has run: the `auth` schema, seven Identity tables and `auth.__EFMigrationsHistory` survive. Undo with `dotnet ef database update 0 ... --context EnvanexIdentityDbContext`, then `DROP SCHEMA auth`. **`dbo.__EFMigrationsHistory` must not be touched.** The Testcontainers database is rebuilt each run, so tests are unaffected. The `CLAUDE.md` edit reverts with the commit — keep them together, or the `--context` lines become dead instructions. The JwtBearer `PackageReference` on `Envanex.Web.csproj` and the four new `PackageVersion` entries revert with it; `Web_ShouldReference_JwtBearerPackage` reverts in the same commit, so nothing is left asserting a reference that no longer exists.
- **Phase 2** — `dotnet ef migrations remove ... --context EnvanexIdentityDbContext` if `AddRefreshTokens` has not been applied to any shared database; otherwise `database update InitialIdentitySchema` first. **Never hand-edit a generated migration to change its intent** — create a new one. `IdentityRowSeeder` and its scope-guard test revert together.
- **Phase 3** — pure revert. One cross-cutting pair travels together: the `ResultMappingTests` scan widening and the eight table entries — revert one without the other and `ResultMapping_NoMappingEntry_ShouldBeOrphaned` turns red. The `Jwt` block in `appsettings.json` and `JwtAppSettingsTests` also revert together, or the test fails on a missing section. (Revision 3's "Şifre → Parola alignment" rollback note is deleted: no existing message was ever changed.)
- **Phase 4** — pure revert, plus removing the `Jwt:*` keys from `DependencyInjectionTests.BuildProvider()` and the two factories; leaving them in is harmless, and so is removing `AddEnvanexIdentity` without them. The `AddLogging()` line may stay. `CountingUserManager`, `IdentitySeeder` and the three new Infrastructure `PackageReference`s are all safe to leave or remove.
- **Phase 5** — pure revert. Remove the three `CommandHandlerTypes()` rows as well, or the theory fails on an unresolvable service.
- **Phase 6** — pure revert, with two caveats. (1) The `Program.cs` rate-limiter restructure is the only edit touching previously-working behaviour: if it comes out, restore `AddRateLimiter` and `UseRateLimiter()` inside `if (rateLimitingEnabled)`, push `permitLimit`/`windowSeconds` back inside, and drop `[EnableRateLimiting("login")]` from the controller **in the same commit** — an attribute naming a policy the middleware cannot see throws at endpoint build time and takes down every endpoint. (2) The `RateLimiting:Login:Enabled=false` lines in the two shared factories must come out in the same commit as the `appsettings.json` `Login` block; leaving the setting behind is harmless, but leaving the `appsettings` block while reverting the factories makes `AuthApiTests` flaky with 429s. Reverting Phase 6 also removes the PR 7 tripwire test, so the roadmap blocker entry must be kept even if the code is reverted.

Per project rules the coder never commits and never pushes. Take a `git commit` before starting each phase.

---

## Notes for the human

1. All six decisions are settled; no phase is blocked on a further ruling. The coder can start Phase 1 immediately.
2. db-reviewer runs after Phases 1, 2 and 4 only. Phase 4 needs it despite adding no migration, because it introduces the refresh-token lookup, the family revocation and the concurrent-rotation paths — and because Phase 2's new `IX_RefreshTokens_FamilyId` should be judged against Phase 4's measured plan.
3. **One thing in Phase 4 belongs in the ADR.** `RotateAsync` keeps a single `SaveChangesAsync` and collapses both failures — the `RowVersion` concurrency loss and the `IX_RefreshTokens_FamilyId_Live` unique violation — into one branch returning `InvalidRefreshToken`. They report the same fact, so the distinction buys nothing; collapsing them removes the dependency on EF's save ordering without deleting a guard, and keeps the index as a database-level invariant whether or not the application ever trips it.
4. Phase 1's validation deliberately runs `dotnet ef migrations list` **without** `--context` and expects failure. If it succeeds, the `CLAUDE.md` edit is unnecessary and the two-context premise needs rechecking before Phase 2.
5. Seven tests encode security or policy properties rather than mechanics. A change that trips one is a design question, not a broken test:
   `AuthErrorsTests.AuthErrors_ShouldNotDeclareALockoutSpecificCode`;
   `ResultExtensionsTests.ToActionResult_EveryAuthErrorCode_ShouldMapTo400Or401`;
   `TurkishErrorMessagesTests.InvalidCredentialsMessage_ShouldNotConfirmThatThisAccountIsLockedOut`;
   `AuthApiTests.Login_UnknownEmailWrongPasswordAndLockedOutAccount_ShouldReturnIdenticalStatusContentTypeAndBody`;
   `IdentityServiceTimingOrderTests.ValidateCredentialsAsync_LockedOutUser_ShouldCallCheckPasswordAsyncBeforeIsLockedOutAsync`;
   `LoginRateLimitPartitionTests.GetKey_ShouldIgnoreXForwardedForUntilPr7`;
   `IdentitySeedingScopeTests.IdentityRowSeeder_ShouldBeReferencedOnlyBySchemaTests`.
6. `IdentityServiceTimingOrderTests.ValidateCredentialsAsync_UnknownEmail_ShouldNotCallCheckPasswordAsync` asserts a *gap*, not a guarantee. Whoever closes the unknown-email timing hole must invert a named test rather than quietly change an unobserved behaviour.
7. PR 7 has one hard dependency from this PR: the rate-limit partition key. It is a roadmap blocker with a test PR 7 must delete.

---

## Appendix: revision 4 — what changed, per review finding

1. **Login limiter on the shared factory (Blocker).** Phase 6's file list now edits `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs` **and** `RateLimitedWebApplicationFactory.cs` to set `RateLimiting:Login:Enabled=false`, each with the one-line reason. Decision 4 records the requirement so it cannot be dropped again. The lock-in is the behavioural test `AuthApiTests.Login_EightConsecutiveAttempts_ShouldNeverReturn429` — eight logins against a production budget of five — rather than a structural check, because the policy map is not public surface (noted as unverified; `GlobalLimiter` is, which is why the sibling `RateLimiterTests` check stays structural).
2. **Rotation-vs-rotation concurrency (Blocker) — solved differently than directed, and here is why.** The directed deterministic construction ("insert a live child row into the family directly, leave the parent's `RotatedAt` null, then call `RotateAsync`") **cannot be built**: with the parent live, a second live row in the same family is exactly what `IX_RefreshTokens_FamilyId_Live` forbids, so the direct insert fails before `RotateAsync` is ever called. Worse, the same reasoning shows the 2601/2627 branch is unreachable through `RotateAsync` at all: the parent is stamped under its `RowVersion` predicate before the child is inserted, so a racing rotation always loses on concurrency first. The plan therefore (a) **deletes** the three loser-path tests rather than renaming them, (b) keeps a natural-race loop asserting only what holds under every interleaving — `ShouldNeverLeaveTwoLiveRowsInTheFamily` and `ShouldEndInOneOfTheTwoAcceptedStates`, with the two accepted states spelled out and any third outcome a failure, (c) states per test which guard's removal turns it red, (d) moves the deterministic proof of each guard to Phase 2, where it is genuinely deterministic — the existing unique-violation test for the index, plus a **new** `RefreshTokens_UpdatingARowLoadedBeforeAConcurrentUpdate_ShouldThrowDbUpdateConcurrencyException` for the `RowVersion` token, (e) adds **no** hook, seam, interceptor, barrier or internal-visibility change to production code.

   **Ruled by the human after revision 4 was drafted, and applied here.** The unreachability analysis is accepted; the fix it produced went further than needed and has been replaced. `RotateAsync` step 8 keeps **one `SaveChangesAsync`** — no explicit transaction, no two-save split; production behaviour is not reshaped to make an exception type predictable. Step 9 collapses the two failure branches into **one**: `DbUpdateConcurrencyException` and a 2601/2627 unique violation on `IX_RefreshTokens_FamilyId_Live` are caught together and both map to `Auth.InvalidRefreshToken`, which removes the dependency on EF's batch ordering and removes the unreachable branch without deleting a guard. `IX_RefreshTokens_FamilyId_Live` is kept as a database-level invariant. The Phase 4 report no longer states which exception fired. The natural-race test asserts the returned error code and the post-state only, never the exception type.
3. **Phase 2's FK-satisfying user row (High) — narrow exception, chosen over moving the seeder.** Moving `IdentitySeeder.CreateUserAsync` to Phase 2 would mean hand-building an Identity registration in the test project two phases before `AddEnvanexIdentity` exists, leaving two ways to make a user; so Phase 2 gets `IdentityRowSeeder.InsertBareUserRowAsync`, which writes one `auth.AspNetUsers` row through `EnvanexIdentityDbContext` with `PasswordHash` left null. Research decision 6 exists so login tests exercise real hashing, not to forbid a row that satisfies a foreign key; a row with no password hash cannot be authenticated against and cannot mask a hashing defect. The exception is bounded by `IdentitySeedingScopeTests.IdentityRowSeeder_ShouldBeReferencedOnlyBySchemaTests`, which fails if any other test file names it.
4. **Family-query index (High) — shape chosen and defended.** Added `IX_RefreshTokens_FamilyId`: non-unique, **unfiltered**, single column. The filtered shape was rejected because in an append-only table revocation is the exception, not the rule — most rows have `RevokedAt IS NULL`, so `WHERE RevokedAt IS NULL` removes little while making the index useless to the future pruning job; a `(FamilyId, RevokedAt)` composite was rejected because a family holds a handful of rows, so the residual predicate is free and the extra key column is a write cost on an insert-heavy table. The reasoning is in the configuration section for db-reviewer to overrule. Phase 4's report requirement now covers the **family query plan** (captured statement plus `SHOWPLAN` output), not only the `TokenHash` seek, and a Phase 2 schema test asserts the index exists and is unfiltered.
5. **Package-version determination (Medium).** The JwtBearer `PackageReference` moves to `src/Envanex.Web/Envanex.Web.csproj` in **Phase 1** (unused but resolvable), and Phase 1 gains an ordered six-step "Package version determination" block ending in `dotnet list src/Envanex.Web/Envanex.Web.csproj package --include-transitive`, whose raw output is pasted. `Web_ShouldReference_JwtBearerPackage` moves to Phase 1 with it; Phase 6 no longer edits `Envanex.Web.csproj`.
6. **The false "Şifre" fact (Medium) — removed entirely.** Verified in this pass: a case-insensitive search of `src/` for "ifre" returns nothing, `TurkishErrorMessages` has 33 entries and none mentions a password, and no `Auth.*` code exists. The vocabulary-alignment paragraph in Decision 6, the "(was ...)" annotation on the message table, and the Phase 3 rollback clause are all deleted. `PasswordMessages_ShouldUseParolaConsistentlyAndNeverSifre` is **replaced**, not renamed, by `PasswordRequiredMessage_ShouldBeExactlyParolaZorunludur`, which fails if the message is written as "Şifre zorunludur."; `InvalidCredentialsMessage_ShouldBeExactlyTheDecision6String` gives the same treatment to the load-bearing message.
7. **`LoginRateLimiterTests` (Medium).** Now specified as `[Collection(DatabaseCollection.Name)]` with `IAsyncLifetime` and a **fresh** `LoginRateLimitedWebApplicationFactory` per test in `InitializeAsync`, disposed in `DisposeAsync`, citing `RateLimiterTests.cs:25-36` as the precedent, plus the identity reset and seeding it needs.
8. **The predicate's real guard (Medium-Low).** `EnvanexDbContext_ShouldNotMapEnvanexUser` is **deleted** as vacuous. Phase 2 adds `EnvanexDbContext_ShouldNotMapRefreshToken` and `EnvanexIdentityDbContext_ShouldMapRefreshToken`. Unverified item (C) is settled by reading the repo rather than by a probe migration: `ProductConfiguration` is `internal sealed` and `Products` is mapped today, so `ApplyConfigurationsFromAssembly` does pick up internal configurations and the new guard is non-vacuous.
9. **Overstated "(Decision 1)" tags (Low).** The six Phase 2 `RefreshToken` tests are renamed to say what they prove (`...ToNowPlusTheIdleWindowArgument`), and the shipped numbers get a real guard: the new `tests/Envanex.IntegrationTests/Configuration/JwtAppSettingsTests.cs` binds the `Jwt` section out of the shipped `src/Envanex.Web/appsettings.json` and asserts 15 / 7 / 30, non-empty issuer and audience, and an empty `SigningKey`.
10. **The hoist (Low).** `Program.cs` step 2 now says explicitly to move the `permitLimit` and `windowSeconds` declarations (currently `Program.cs:30-31`, inside the `if`) up beside `rateLimitingEnabled` before `AddRateLimiter` leaves the block.
11. **Two under-specified details (Low).** The 150-request test names its endpoint — `GET /api/unit-of-measures`, a read endpoint that neither writes rows nor collides with reset semantics — and asserts all 150 are 200. `ArchitectureTests` gains the extracted `MatchPackageReferences(csprojContent, pattern)` / `ReadCsproj(projectName)` helpers, with the existing EF test rewritten onto them, so `ForbiddenPackageMatcher_ShouldMatch_RealAuthPackageNames` genuinely runs the same matcher against a synthetic csproj string.
12. **Unverified items pre-empted (A)–(F).** Phase 4 opens with a "Named risks" block: (A) options/configuration-binder availability in `Envanex.Infrastructure` — pre-empted by adding both `PackageVersion`s in Phase 1 and both `PackageReference`s in Phase 4, settled by `dotnet list ... --include-transitive` plus build; (B) no `AddLogging()` in `DependencyInjectionTests.BuildProvider()` — pre-empted by adding the line, settled by the filtered `AllRegisteredServices_ShouldResolve` run; (E) the four `UserManager` virtuals, settled by build with a stop-and-report instruction; (F) `AccessFailedAsync` updating the tracked entity, settled by the lockout test with a one-line fallback; (G) EF statement ordering, new in this revision. (C) is now verified from the repository and (D) stays marked unverified with `dotnet build` as its settling command and an explicit instruction not to invent a reflection workaround.
13. **The supersedes line (Cosmetic).** Replaced with "Revision 4 replaces revision 3 of this same file … Save over it."

---

### Handback note

Two of the review's directed fixes were executed differently, both flagged above and both inside the finding they answer: **finding 2** (the directed deterministic construction contradicts `IX_RefreshTokens_FamilyId_Live` and cannot be built — the branch it targets is unreachable through `RotateAsync`, so the tests were deleted, the guards were relocated to deterministic Phase 2 tests, and no barrier or seam was added) and **finding 4** (an unfiltered single-column `FamilyId` index was chosen over the filtered shape the review suggested, with the reasoning stated for db-reviewer to overrule). The analysis behind finding 2 also produced one production-spec change — an explicit transaction with two saves — which **the human has since replaced**: rotation keeps a single `SaveChangesAsync` and the two failure branches are collapsed into one loser exit returning `InvalidRefreshToken`. See the ruling in appendix item 2, step 9 of `RotateAsync`, risk G, and point 3 of "Notes for the human".