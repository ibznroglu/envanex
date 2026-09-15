# Plan: PR 6a — Authentication infrastructure

Save to `thoughts/shared/plans/2026-09-16_auth-infrastructure.md`.

Source research: `C:\projects\envanex\thoughts\shared\research\2026-09-16_auth-infrastructure.md` (decisions 1–11 treated as settled constraints).

## Goal

Add ASP.NET Core Identity in its own `DbContext` under an `auth` schema, short-lived JWT access tokens, opaque refresh tokens stored hashed with rotation and reuse detection, and the `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout` endpoints. Every existing endpoint stays open.

## Non-goals

Authorization policies. `[Authorize]` on any endpoint. The Blazor cookie scheme. The read-only demo account. Passkeys. Any endpoint that creates a user. A `JwtBearerEvents.OnChallenge` ProblemDetails body — no challenge is reachable in 6a, so writing it here would ship dead, untested code; it belongs to 6b together with the first `[Authorize]`. A refresh-token pruning job — rows are never deleted and growth is unbounded; this is a recorded gap for the Worker PR.

## Touches schema?

**Yes — Phases 1, 2 and 4. db-reviewer is required after each of Phase 1, Phase 2 and Phase 4.**
Phases 3, 5 and 6 touch no schema, no migrations and no queries; db-reviewer is not required for them.

## ADR needed?

Yes, one. The human writes it after `tester` returns `READY_TO_PUSH`:

`docs/adr/0007-jwt-access-tokens-and-rotated-refresh-tokens.md`
Title: **"JWT access tokens with rotated, reuse-detected refresh tokens in a separate Identity context"**

Points the ADR must cover, because they are decisions made in this plan rather than derived from an existing ADR: the separate Identity `DbContext` and `auth` schema with its own `__EFMigrationsHistory`; `IIdentityService`/`IRefreshTokenService`/`IAccessTokenIssuer` as Application abstractions so auth uses the same `ICommandHandler` pipeline ADR 0005 describes; SHA-256 rather than a password hash for the stored refresh token; the zero grace period on reuse detection (research decision 7) and why; the `min(idle, absolute cap)` refresh expiry shape; `Auth.UserLockedOut` mapping to 403 rather than 401 and the account-enumeration trade-off that implies; `ClockSkew = TimeSpan.Zero` on validation; Identity's built-in `Microsoft.AspNetCore.Identity` meter as an argument for Identity over hand-rolled user storage; and the deferral of the `OnChallenge` body plus the refresh-token pruning job.

An agent may create the empty file with that title. The agent must not write its body.

---

## PROPOSALS — for the human to approve or reject before the coder starts

These four are not settled. Phases that depend on one say so.

### Proposal 1 — Lifetimes

- Access token: **15 minutes** (`Jwt:AccessTokenMinutes` = 15)
- Refresh token idle window: **14 days** (`Jwt:RefreshTokenIdleDays` = 14)
- Refresh token absolute cap: **60 days** (`Jwt:RefreshTokenAbsoluteDays` = 60)

Reasoning. 6a ships no revocation list, so a leaked access token is valid until it expires and nothing can stop it; 15 minutes is the smallest window that does not multiply refresh traffic into SQL Server on the free-tier target. Five minutes would quadruple refresh writes for a marginal gain; sixty minutes makes the missing revocation list a genuine exposure. Fourteen days of idle means a user who comes back within two weeks stays signed in, which is the behaviour a normal ERP operator expects. Sixty days of absolute cap forces a real re-authentication roughly every two months.

Depends on nothing else. Phases 2, 4 and 6 depend on these numbers.

### Proposal 2 — Refresh expiry shape

**Absolute cap combined with a shorter idle window.** Each `RefreshTokens` row carries both `ExpiresAt` (the idle window: `now + RefreshTokenIdleDays`, recomputed on every rotation) and `FamilyExpiresAt` (the absolute cap: `familyCreatedAt + RefreshTokenAbsoluteDays`, copied forward unchanged to every child). A token is usable only when `now < ExpiresAt` **and** `now < FamilyExpiresAt`.

Reasoning. Pure sliding never ends a session: a stolen token that is rotated forever is permanent access, which defeats the point of having an expiry. Pure absolute logs an actively-working user out mid-week. Copying `FamilyExpiresAt` onto every generation rather than joining back to the family root keeps the rotation hot path a single-row read.

This costs one extra `datetimeoffset` column. Phase 2 (schema) and Phase 4 (rotation logic) depend on it. If rejected in favour of pure sliding, drop `FamilyExpiresAt` from Phase 2 and drop the two absolute-cap test cases from Phase 4.

### Proposal 3 — Refresh token hash

**SHA-256 over the UTF-8 bytes of the token, stored as `binary(32)`, compared with `CryptographicOperations.FixedTimeEquals`.** Token material is 32 bytes from `RandomNumberGenerator.GetBytes`, Base64Url-encoded.

Reasoning. There is no dictionary to attack against 256 bits of CSPRNG output, so the work factor a password hash buys is worth nothing while costing ~100 ms on every refresh. More importantly, a salted password hash is per-row and therefore not searchable: lookup would degrade from one indexed equality seek to a scan with a verify per row. A deterministic hash keeps the unique index on `TokenHash` meaningful, which also gives collision detection for free. Not HMAC-SHA256: a keyed hash adds a second secret to rotate and manage, and buys nothing against the only realistic attacker here — one who has read the table but still cannot invert a 256-bit random preimage. `FixedTimeEquals` is applied to the fetched row even though the index seek already matched, so the constant-time property does not silently depend on the index.

Phases 2 and 4 depend on this.

### Proposal 4 — Login rate limit: own switch **and** own factory

Both, because neither alone works.

- A named policy `"login"` registered via `AddPolicy`, driven by `RateLimiting:Login:{Enabled,PermitLimit,WindowSeconds}` (production defaults 5 / 300 s), partitioned by `HttpContext.Connection.RemoteIpAddress`.
- A new `LoginRateLimitedWebApplicationFactory` (PermitLimit 2, WindowSeconds 60) for the tests, following the `RateLimitedWebApplicationFactory` precedent.
- One small refactor in `Program.cs`: `AddRateLimiter` and `app.UseRateLimiter()` become unconditional; only the **assignment of `GlobalLimiter`** stays inside `if (rateLimitingEnabled)`. The login policy is registered as a real limiter when `RateLimiting:Login:Enabled` is true and as `NoLimiter` otherwise.

Reasoning. The switch alone is insufficient: `[EnableRateLimiting("login")]` names a policy that only exists if `AddRateLimiter` ran, and today `AddRateLimiter` and `UseRateLimiter()` are both behind `RateLimiting:Enabled`, which the shared test factory sets to `false`. Referencing a policy that the middleware never sees is exactly the kind of silently-inert guard this research already found once in the architecture test. The factory alone is insufficient in the other direction: without an independent switch, the shared factory would have to enable the login limiter for every test class in the collection, and since all integration tests share one container and one factory, the 5-request budget would be consumed across classes and produce flaky 429s.

Behaviour is unchanged for existing tests: with `RateLimiting:Enabled=false` there is no `GlobalLimiter`, and with `RateLimiting:Login:Enabled=false` the login policy is a `NoLimiter`. Phase 6 names a regression test for this.

Known gap to record in the roadmap: partitioning by `RemoteIpAddress` is wrong behind a reverse proxy without forwarded-headers configuration. That is a PR 7 (deploy) concern.

---

## Phase 1: Identity context, `auth` schema, its own migrations history

**Touches schema and migrations — db-reviewer required.**

### Files

- `Directory.Packages.props` — modified — add, in a new `<!-- Identity / Auth -->` group:
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore` version `10.0.11`
  - `Microsoft.AspNetCore.Authentication.JwtBearer` version `10.0.11`
  - `Microsoft.IdentityModel.JsonWebTokens` — version **must be read, not guessed**: run `dotnet list src/Envanex.Web/Envanex.Web.csproj package --include-transitive` after adding JwtBearer and pin the exact version JwtBearer 10.0.11 resolves. Pinning anything lower produces NU1605, which `TreatWarningsAsErrors` turns into a build failure.
  - `Microsoft.Extensions.TimeProvider.Testing` — ships from `dotnet/extensions` on its own version line, not the ASP.NET Core 10.0.x line, so it carries no NU1605 risk against the family. Resolve the exact version with `dotnet package search Microsoft.Extensions.TimeProvider.Testing --exact-match` and pin the latest stable.
- `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj` — modified — add `<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />`. Do **not** add a `FrameworkReference` to `Microsoft.AspNetCore.App`: the design below deliberately uses `AddIdentityCore` and `UserManager<T>` only (both from `Microsoft.Extensions.Identity.Core`/`.Stores`) and never `SignInManager`, which lives in the shared framework.
- `src/Envanex.Infrastructure/Identity/EnvanexUser.cs` — created — `public sealed class EnvanexUser : IdentityUser<Guid>` with no added members. It is public, not internal, because `EnvanexIdentityDbContext` is public and a public class cannot derive from a base constructed with a less accessible type argument (CS0060).
- `src/Envanex.Infrastructure/Identity/AuthSchema.cs` — created — `internal static class AuthSchema` with `public const string Name = "auth";` and `public const string MigrationsHistoryTable = "__EFMigrationsHistory";`.
- `src/Envanex.Infrastructure/Identity/EnvanexIdentityDbContext.cs` — created — `public class EnvanexIdentityDbContext : IdentityDbContext<EnvanexUser, IdentityRole<Guid>, Guid>`. `OnModelCreating` calls `ArgumentNullException.ThrowIfNull(modelBuilder)`, then `modelBuilder.HasDefaultSchema(AuthSchema.Name)`, then `base.OnModelCreating(modelBuilder)`. It does **not** call `ApplyConfigurationsFromAssembly` — Phase 2 adds one explicit `ApplyConfiguration` call. It does **not** override `ConfigureConventions`, so `AggregateRootConvention` and `MoneyComplexTypeConvention` do not reach it.
  - Note for db-reviewer: the full `IdentityDbContext` (with roles) is used rather than `IdentityUserContext`, so `AspNetRoles`, `AspNetRoleClaims` and `AspNetUserRoles` are created now and stay empty until PR 6b. The trade is three empty tables today against a second Identity migration in 6b.
  - Note for db-reviewer: `AspNetUsers.Id` is a `uniqueidentifier` with a clustered PK. Acceptable for a low-cardinality table; called out because Phase 2's high-volume table takes the opposite decision.
- `src/Envanex.Infrastructure/Identity/IdentityDbContextOptionsExtensions.cs` — created — one place that knows how to point a `DbContextOptionsBuilder` at the Identity database, so the migrations-history table cannot be set in one of the three call sites and forgotten in the other two.
- `src/Envanex.Infrastructure/Identity/EnvanexIdentityDbContextFactory.cs` — created — `public sealed class EnvanexIdentityDbContextFactory : IDesignTimeDbContextFactory<EnvanexIdentityDbContext>`, reading `ENVANEX_CONNECTION_STRING` and throwing the same-shaped `InvalidOperationException` as `EnvanexDbContextFactory`, and building options through `UseEnvanexIdentitySqlServer`.
- `src/Envanex.Infrastructure/DependencyInjection.cs` — modified — add `services.AddDbContext<EnvanexIdentityDbContext>(options => options.UseEnvanexIdentitySqlServer(connectionString));` after the existing `AddDbContext<EnvanexDbContext>` call. Nothing else in this phase.
- `src/Envanex.Infrastructure/Persistence/EnvanexDbContext.cs` — modified — change `ApplyConfigurationsFromAssembly(typeof(EnvanexDbContext).Assembly)` to the predicate overload restricting configurations to the namespace `Envanex.Infrastructure.Persistence.Configurations`. **This is not cosmetic.** Phase 2 adds `RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>` to the same assembly, and without this predicate `EnvanexDbContext` would pick it up and create a second, unwanted `dbo.RefreshTokens` table on the next business migration. Fixing it here keeps Phase 2 safe.
- `src/Envanex.Infrastructure/Migrations/Identity/` — created by the tooling — migration `InitialIdentitySchema` plus its designer and `EnvanexIdentityDbContextModelSnapshot`.
- `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs` — modified — add `public EnvanexIdentityDbContext CreateIdentityDbContext()`; in `InitializeAsync`, after the existing `MigrateAsync`, open an identity context and `MigrateAsync` it; add `public async Task ResetIdentityAsync()` deleting in FK order `AspNetUserTokens`, `AspNetUserLogins`, `AspNetUserClaims`, `AspNetUserRoles`, `AspNetRoleClaims`, `AspNetRoles`, `AspNetUsers`, all schema-qualified with `auth.`. Per research decision 5 this is a **separate** method; `ResetAsync` is not touched, so no business test starts paying for auth cleanup.
- `tests/Envanex.IntegrationTests/Envanex.IntegrationTests.csproj` — modified — add `<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />` (used from Phase 4) and `<PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" />` (used from Phase 4).
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
- `IdentityMigrations_ShouldApplyToCleanDatabase` — `GetPendingMigrationsAsync()` on the identity context is empty.
- `IdentityTables_ShouldLiveInAuthSchema` — `[Theory]` with `[InlineData]` for `AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserRoles`, `AspNetUserTokens`, `AspNetRoleClaims`; queries `INFORMATION_SCHEMA.TABLES` and asserts `TABLE_SCHEMA` is `auth`.
- `IdentityMigrationsHistory_ShouldLiveInAuthSchema` — `auth.__EFMigrationsHistory` exists.
- `BusinessAppliedMigrations_ShouldNotContainIdentityMigrations` — `EnvanexDbContext.Database.GetAppliedMigrationsAsync()` contains no id ending in `_InitialIdentitySchema`.
- `IdentityAppliedMigrations_ShouldNotContainBusinessMigrations` — the identity context's applied migrations contain no id ending in `_InitialSchema`.
- `BusinessMigrationsHistory_ShouldNotContainIdentityMigrations` — direct `SELECT MigrationId FROM dbo.__EFMigrationsHistory`; the identity migration id is absent. This is the assertion that catches the silent shared-history failure at the table level rather than through EF's own bookkeeping.

`tests/Envanex.IntegrationTests/Persistence/DbContextIsolationTests.cs` — new, `[Collection(DatabaseCollection.Name)]`:
- `EnvanexDbContext_ShouldNotMapEnvanexUser` — `Model.FindEntityType(typeof(EnvanexUser))` is null.
- `EnvanexDbContext_ShouldStillMapProductUnitOfMeasureAndWarehouse` — negative control on the new `ApplyConfigurationsFromAssembly` predicate, so an over-narrow namespace filter cannot pass silently.
- `EnvanexIdentityDbContext_ShouldNotMapAnyBusinessEntityType` — `FindEntityType` is null for `Product`, `UnitOfMeasure` and `Warehouse`.
- `EnvanexIdentityDbContext_ShouldNotDeclareRowVersionShadowProperty` — proves `AggregateRootConvention` did not follow the Identity context.

`tests/Envanex.Domain.Tests/ArchitectureTests.cs` — modified:
- `Application_ShouldNotReference_AuthenticationPackages` — `[Theory]` with `[InlineData("Microsoft.AspNetCore.Identity")]`, `[InlineData("Microsoft.AspNetCore.Authentication")]`, `[InlineData("Microsoft.IdentityModel")]`, `[InlineData("System.IdentityModel")]`, asserting `Envanex.Application.csproj` has no `PackageReference` whose `Include` contains the prefix. This closes the gap the research proved: the existing `Microsoft\.EntityFrameworkCore` regex does not match either new package name.
- `ForbiddenPackageMatcher_ShouldMatch_RealAuthPackageNames` — the negative control. Runs the same matcher against the literal strings `"Microsoft.AspNetCore.Identity.EntityFrameworkCore"` and `"Microsoft.AspNetCore.Authentication.JwtBearer"` and asserts both match. Without this, a broken matcher passes the theory above by matching nothing at all — which is precisely the failure mode the research documented.
- `Infrastructure_ShouldReference_IdentityEntityFrameworkCore` — positive control that the package landed in Infrastructure rather than drifting into Application or Web.

`tests/Envanex.IntegrationTests/Persistence/MigrationTests.cs` — unchanged; it still covers the business context.

### Validation

Run in order from `C:\projects\envanex`. Steps 2–4 are the unverified-behaviour check the research file demands; **paste the raw output of each into the phase report, including the failure in step 2.**

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

Expected: step 5 (`migrations list` with no `--context`) **fails** with "More than one DbContext was found"; steps 6 and 7 succeed. If step 5 unexpectedly succeeds, stop and report it — the plan's premise about `--context` being mandatory is wrong and the human needs to know before Phase 2.

Test count after this phase: 113 Domain + 6 new architecture cases, 79 Application unchanged, 107 Integration + 10 new.

---

## Phase 2: RefreshTokens table, hashing and token generation

**Touches schema and migrations — db-reviewer required.**
Depends on Proposal 2 (the `FamilyExpiresAt` column) and Proposal 3 (SHA-256, `binary(32)`).

### Files

- `src/Envanex.Infrastructure/Identity/RefreshToken.cs` — created — `internal sealed class RefreshToken`. All setters `private set`; state changes go through the three methods below, so the rotation rules cannot be bypassed by a stray property assignment in a service.
- `src/Envanex.Infrastructure/Identity/RefreshTokenHasher.cs` — created — `internal static class RefreshTokenHasher`.
- `src/Envanex.Infrastructure/Identity/RefreshTokenGenerator.cs` — created — `internal static class RefreshTokenGenerator`.
- `src/Envanex.Infrastructure/Identity/Configurations/RefreshTokenConfiguration.cs` — created — `internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>`. Deliberately **not** under `Envanex.Infrastructure.Persistence.Configurations`, so the Phase 1 predicate keeps it out of `EnvanexDbContext`.
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

`CreateChild` copies `FamilyId` and `FamilyExpiresAt` from the parent unchanged and sets `ExpiresAt = now + idleWindow` (Proposal 2). `IsUsableAt` returns true only when `RotatedAt is null && RevokedAt is null && now < ExpiresAt && now < FamilyExpiresAt`.

`Hash` is SHA-256 over `Encoding.UTF8.GetBytes(token)`. `Matches` calls `CryptographicOperations.FixedTimeEquals`. `CreateToken` is `Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenByteLength))`.

Per the project's null-vs-invalid rule: `Hash(null)` and `Matches(null, _)` throw `ArgumentNullException`; a token string that simply does not match is a `false` return, not an exception.

### EF configuration — exact

- `builder.ToTable("RefreshTokens", AuthSchema.Name)`
- `builder.HasKey(t => t.Id)` **and** `.IsClustered(false)`
- `builder.HasIndex(t => new { t.CreatedAt, t.Id }).IsClustered().HasDatabaseName("IX_RefreshTokens_CreatedAt_Id")` — this table is append-only and never pruned in 6a; a random-GUID clustered PK would fragment every insert. **db-reviewer should confirm this trade explicitly.**
- `builder.Property(t => t.TokenHash).IsRequired().HasColumnType("binary(32)")`
- `builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("IX_RefreshTokens_TokenHash")`
- `builder.HasIndex(t => t.FamilyId).IsUnique().HasFilter("[RotatedAt] IS NULL AND [RevokedAt] IS NULL").HasDatabaseName("IX_RefreshTokens_FamilyId_Live")` — at most one live token per family. This is the database-level answer to the "rotation racing revocation" trap in the research: a concurrent second rotation hits a unique violation instead of silently writing a second live child.
- `builder.HasIndex(t => t.UserId).HasDatabaseName("IX_RefreshTokens_UserId")`
- `builder.HasOne<EnvanexUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade).IsRequired()`
- `builder.Property(t => t.RevokedReason).HasMaxLength(32)`
- `builder.Property<byte[]>(ColumnNames.RowVersion).IsRowVersion()` — configured explicitly, because `AggregateRootConvention` is not registered on this context.
- All six `DateTimeOffset` properties map to `datetimeoffset` by convention; no explicit precision is set.

### Tests to add

`tests/Envanex.IntegrationTests/Identity/RefreshTokenHasherTests.cs` — new. These are pure unit tests and would normally live in a Domain/Application test project; they sit in `Envanex.IntegrationTests` because that is the only project with `InternalsVisibleTo` access to `Envanex.Infrastructure`. No `[Collection]` attribute, no database.
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
- `CreateRoot_ShouldSetExpiresAtToIdleWindowAndFamilyExpiresAtToAbsoluteWindow`
- `CreateRoot_ShouldSetFamilyIdToANonEmptyGuid`
- `CreateChild_ShouldKeepTheParentFamilyId`
- `CreateChild_ShouldCopyFamilyExpiresAtUnchanged`
- `CreateChild_ShouldResetExpiresAtToIdleWindowFromNow`
- `IsUsableAt_FreshToken_ShouldBeTrue`
- `IsUsableAt_RotatedToken_ShouldBeFalse`
- `IsUsableAt_RevokedToken_ShouldBeFalse`
- `IsUsableAt_PastIdleWindow_ShouldBeFalse`
- `IsUsableAt_WithinIdleWindowButPastFamilyExpiry_ShouldBeFalse`
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
- `RefreshTokens_RotatedRowAndItsChild_ShouldCoexistInTheSameFamily` — the direct regression test for the research's "deleting the predecessor destroys detection" trap.
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
Depends on Proposal 1 (the three default lifetime numbers land in `appsettings.json` here).

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
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — modified — nine new `StatusCodeMap` entries (33 → 42) and two new arms in `GetReasonPhrase`: `401 => "Unauthorized"`, `403 => "Forbidden"`.
- `src/Envanex.Web/Extensions/TurkishErrorMessages.cs` — modified — the same nine codes (33 → 42).
- `src/Envanex.Web/appsettings.json` — modified — add a `Jwt` section: `"Issuer": "https://envanex.local"`, `"Audience": "envanex-api"`, `"SigningKey": ""`, `"AccessTokenMinutes": 15`, `"RefreshTokenIdleDays": 14`, `"RefreshTokenAbsoluteDays": 60`. `SigningKey` stays empty; it is a user-secret. No secret is ever written to this file.
- `tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs` — modified — per research decision 1.

### Signatures

```csharp
public static class AuthErrors
{
    public static readonly Error InvalidCredentials    = new("Auth.InvalidCredentials", "Email or password is incorrect.");
    public static readonly Error UserLockedOut         = new("Auth.UserLockedOut", "The account is temporarily locked out.");
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

`JwtOptionsGuard.ThrowIfInvalid` throws `ArgumentNullException` on null, otherwise `InvalidOperationException` naming the offending key and the user-secrets command — the same shape as the existing connection-string guard in `Infrastructure/DependencyInjection.cs`, e.g. `dotnet user-secrets set "Jwt:SigningKey" "<value>" --project src/Envanex.Web`. It rejects: blank `Issuer`, blank `Audience`, blank `SigningKey`, a `SigningKey` shorter than 32 UTF-8 bytes (HS256 needs 256 bits), `AccessTokenMinutes <= 0`, `RefreshTokenIdleDays <= 0`, `RefreshTokenAbsoluteDays <= 0`, and `RefreshTokenIdleDays > RefreshTokenAbsoluteDays`.

### Status code mapping — exact

| Code | Status |
|---|---|
| `Auth.InvalidCredentials` | 401 |
| `Auth.UserLockedOut` | 403 |
| `Auth.InvalidRefreshToken` | 401 |
| `Auth.RefreshTokenExpired` | 401 |
| `Auth.RefreshTokenReused` | 401 |
| `Auth.EmailRequired` | 400 |
| `Auth.EmailInvalid` | 400 |
| `Auth.PasswordRequired` | 400 |
| `Auth.RefreshTokenRequired` | 400 |

Turkish messages (exact strings, correct characters):

- `Auth.InvalidCredentials` → "E-posta veya şifre hatalı."
- `Auth.UserLockedOut` → "Hesabınız çok sayıda başarısız giriş denemesi nedeniyle geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin."
- `Auth.InvalidRefreshToken` → "Oturum bilgisi geçersiz. Lütfen tekrar giriş yapın."
- `Auth.RefreshTokenExpired` → "Oturum süresi doldu. Lütfen tekrar giriş yapın."
- `Auth.RefreshTokenReused` → "Oturum güvenlik nedeniyle sonlandırıldı. Lütfen tekrar giriş yapın."
- `Auth.EmailRequired` → "E-posta adresi zorunludur."
- `Auth.EmailInvalid` → "Geçerli bir e-posta adresi giriniz."
- `Auth.PasswordRequired` → "Şifre zorunludur."
- `Auth.RefreshTokenRequired` → "Yenileme anahtarı zorunludur."

`Auth.UserLockedOut` mapping to 403 rather than 401 is a deliberate choice: it tells a locked-out operator why they cannot get in, at the cost of confirming that the account exists. Record it in the ADR.

### `ResultMappingTests` change — exact

Rename the private helper `GetAllDomainErrorCodes()` to `GetAllErrorCodes()` and have it scan two assemblies — `typeof(Error).Assembly` and `typeof(AuthErrors).Assembly` — with the same field filter. Rename the four existing facts to drop "Domain" from the names so they do not lie:
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

`tests/Envanex.IntegrationTests/Api/ResultExtensionsTests.cs` — modified:
- `ToActionResult_AuthInvalidCredentials_ShouldReturn401WithTurkishDetail`
- `ToActionResult_AuthUserLockedOut_ShouldReturn403WithTurkishDetail`
- `ToActionResult_AuthRefreshTokenReused_ShouldReturn401WithTurkishDetail`
- `ToActionResult_Status401_ShouldHaveTitleUnauthorized`
- `ToActionResult_Status403_ShouldHaveTitleForbidden`

`tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs` — modified as above.

### Validation

```
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

The phase report must state the new `StatusCodeMap` and `TurkishErrorMessages` entry counts (both 42) so the two tables staying in step is visible without reading the diff.

---

## Phase 4: Infrastructure implementations — Identity, rotation, JWT issuance

**Touches queries (refresh-token lookup and family revocation) but adds no migration — db-reviewer required.**
Depends on Proposals 1, 2 and 3.

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
    public IdentityService(UserManager<EnvanexUser> userManager);
    public Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);
}

internal sealed class RefreshTokenService : IRefreshTokenService
{
    public RefreshTokenService(
        EnvanexIdentityDbContext context,
        TimeProvider timeProvider,
        IOptions<JwtOptions> options);

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
}
```

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

### `IdentityService.ValidateCredentialsAsync` — exact behaviour

`ArgumentNullException.ThrowIfNull` on `email` and `password` (a null here is a caller bug). An empty or unknown value is a business-rule failure and returns `Result.Failure`.

1. `FindByEmailAsync(email)` → null → `AuthErrors.InvalidCredentials`.
2. `IsLockedOutAsync(user)` → true → `AuthErrors.UserLockedOut`.
3. `CheckPasswordAsync(user, password)` → false → `AccessFailedAsync(user)`, then re-check `IsLockedOutAsync`; if the failure just tripped the lockout return `AuthErrors.UserLockedOut`, otherwise `AuthErrors.InvalidCredentials`.
4. true → `ResetAccessFailedCountAsync(user)` → `Result.Success(new AuthenticatedUser(user.Id, user.Email!, user.UserName!))`.

### `RefreshTokenService.RotateAsync` — exact behaviour

All reads go through `EnvanexIdentityDbContext`; no raw SQL.

1. `ArgumentNullException.ThrowIfNull(presentedToken)`; blank → `AuthErrors.InvalidRefreshToken`.
2. `RefreshTokenHasher.Hash(presentedToken)`, then a single-row query on `TokenHash` equality (the unique index seek).
3. Not found → `AuthErrors.InvalidRefreshToken`.
4. `RefreshTokenHasher.Matches(presentedToken, row.TokenHash)` false → `AuthErrors.InvalidRefreshToken`. This is redundant against the index but makes the constant-time comparison an explicit property of the code.
5. `RevokedAt is not null` → `AuthErrors.InvalidRefreshToken`.
6. `RotatedAt is not null` → **reuse**. Load every row with the same `FamilyId` and `RevokedAt IS NULL`, `Revoke(now, RefreshTokenRevocationReason.Reuse)` on each, `SaveChangesAsync`, return `AuthErrors.RefreshTokenReused`. The grace period is zero (research decision 7). **The consumed row is never deleted** — it stays resolvable to its family, which is what makes generation-1 replay after three rotations detectable.
7. `now >= ExpiresAt || now >= FamilyExpiresAt` → revoke the live rows of the family with reason `Expired`, save, return `AuthErrors.RefreshTokenExpired`.
8. Otherwise: generate a new token, build the child via `parent.CreateChild(...)`, `parent.MarkRotated(now, child.Id)`, add the child, one `SaveChangesAsync` for both.
9. A `DbUpdateException` that is a unique violation (SQL error 2601/2627) on `IX_RefreshTokens_FamilyId_Live` means a concurrent rotation already produced the live child; return `AuthErrors.InvalidRefreshToken`. Detect it with the same `SqlException { Number: 2601 or 2627 }` pattern `UnitOfWork` already uses; do not route it through `IUnitOfWork`, which is bound to `EnvanexDbContext`.
10. Load the user for the `RotatedRefreshToken.User` field from `EnvanexIdentityDbContext` in the same round trip as step 2 where practical.

`RevokeFamilyAsync` (logout, research decision 8): look up by hash; **not found returns `Result.Success()`**, deliberately idempotent so logout does not become an oracle for token existence. Found → revoke every row in that `FamilyId` where `RevokedAt IS NULL` with reason `Logout`. It touches one family only; it never ends a user's other sessions.

### `JwtAccessTokenIssuer.Issue` — exact behaviour

`ArgumentNullException.ThrowIfNull(user)`. Claims: `sub` = `user.Id`, `email` = `user.Email`, `jti` = a fresh `Guid.NewGuid()`, plus `iss`, `aud`, `iat`, `nbf`, `exp`. `exp` = `timeProvider.GetUtcNow() + TimeSpan.FromMinutes(options.AccessTokenMinutes)`. Signed HS256 with a `SymmetricSecurityKey` over `Encoding.UTF8.GetBytes(options.SigningKey)`, produced by `JsonWebTokenHandler.CreateToken`. Registered as a singleton, so the `SigningCredentials` are built once.

### Tests to add

All in `tests/Envanex.IntegrationTests/Identity/`, `[Collection(DatabaseCollection.Name)]`, each class calling `_fixture.ResetIdentityAsync()` in `InitializeAsync`. Services are resolved from a `ServiceProvider` built the same way `DependencyInjectionTests.BuildProvider()` does, with `FakeTimeProvider` registered before `AddInfrastructure` so `TryAddSingleton` yields to it.

`IdentityServiceTests`:
- `ValidateCredentialsAsync_CorrectPassword_ShouldReturnAuthenticatedUserWithMatchingId`
- `ValidateCredentialsAsync_UnknownEmail_ShouldReturnInvalidCredentials`
- `ValidateCredentialsAsync_WrongPassword_ShouldReturnInvalidCredentials`
- `ValidateCredentialsAsync_WrongPassword_ShouldIncrementAccessFailedCount`
- `ValidateCredentialsAsync_FifthWrongPassword_ShouldReturnUserLockedOut`
- `ValidateCredentialsAsync_CorrectPasswordAfterLockout_ShouldStillReturnUserLockedOut`
- `ValidateCredentialsAsync_CorrectPasswordAfterTwoFailures_ShouldResetAccessFailedCountToZero`
- `ValidateCredentialsAsync_NullEmail_ShouldThrowArgumentNullException`
- `ValidateCredentialsAsync_NullPassword_ShouldThrowArgumentNullException`

`RefreshTokenServiceTests`:
- `IssueAsync_ShouldPersistOnlyTheHashAndNeverThePlaintextToken` — reads the row back and asserts no column contains the returned token string.
- `IssueAsync_ShouldSetExpiresAtToIdleWindowFromFakeNow`
- `IssueAsync_ShouldSetFamilyExpiresAtToAbsoluteWindowFromFakeNow` *(Proposal 2)*
- `IssueAsync_CalledTwiceForOneUser_ShouldProduceTwoIndependentFamilies`
- `RotateAsync_ValidToken_ShouldReturnADifferentTokenString`
- `RotateAsync_ValidToken_ShouldStampParentRotatedAtAndReplacedByTokenId`
- `RotateAsync_ValidToken_ShouldNotDeleteTheConsumedRow`
- `RotateAsync_ValidToken_ShouldKeepTheSameFamilyId`
- `RotateAsync_ValidToken_ShouldCarryFamilyExpiresAtForwardUnchanged` *(Proposal 2)*
- `RotateAsync_ValidToken_ShouldResetTheIdleWindow` *(Proposal 2)*
- `RotateAsync_ValidToken_ShouldReturnTheOwningUser`
- `RotateAsync_UnknownToken_ShouldReturnInvalidRefreshToken`
- `RotateAsync_BlankToken_ShouldReturnInvalidRefreshToken`
- `RotateAsync_NullToken_ShouldThrowArgumentNullException`
- `RotateAsync_ReplayOfGenerationOneAfterThreeRotations_ShouldReturnRefreshTokenReused` — the RFC 9700 §4.14.2 case, and the direct regression for the research's "one generation of detection" trap.
- `RotateAsync_ReplayOfGenerationOne_ShouldRevokeEveryLiveRowInTheFamily`
- `RotateAsync_AfterReuseDetection_TheLatestLiveTokenShouldAlsoBeRejected`
- `RotateAsync_AfterReuseDetection_ShouldNotTouchTheUsersOtherFamilies`
- `RotateAsync_TokenPastTheIdleWindow_ShouldReturnRefreshTokenExpired` — advance `FakeTimeProvider` by 15 days *(Proposal 1)*
- `RotateAsync_TokenRotatedRegularlyButPastTheAbsoluteCap_ShouldReturnRefreshTokenExpired` — rotate every 10 days, advance past day 60 *(Proposals 1 and 2)*
- `RotateAsync_ExpiredToken_ShouldRevokeTheFamily`
- `RotateAsync_RevokedToken_ShouldReturnInvalidRefreshToken`
- `RevokeFamilyAsync_ValidToken_ShouldRevokeEveryLiveRowInThatFamily`
- `RevokeFamilyAsync_ShouldNotTouchOtherFamiliesOfTheSameUser` — research decision 8.
- `RevokeFamilyAsync_UnknownToken_ShouldReturnSuccess`
- `RevokeFamilyAsync_CalledTwice_ShouldReturnSuccessBothTimes`
- `RotateAsync_AfterRevokeFamily_ShouldReturnInvalidRefreshToken`

`JwtAccessTokenIssuerTests`:
- `Issue_ShouldProduceATokenCarryingSubEmailAndJti`
- `Issue_ShouldSetExpToAccessTokenMinutesAfterFakeNow` *(Proposal 1)*
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
- `AddEnvanexIdentity_ShouldNotOverrideAPreRegisteredTimeProvider` — proves the `TryAddSingleton`.
- `AddEnvanexIdentity_WithNoTimeProviderRegistered_ShouldResolveTimeProviderSystem`

`DependencyInjectionTests` — modified: `AllRegisteredServices_ShouldResolve` gains `IIdentityService`, `IRefreshTokenService` and `IAccessTokenIssuer` assertions.

### Validation

```
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

The phase report must include the SQL that `RotateAsync` generates for the hash lookup and for the family revocation — capture it by raising `Microsoft.EntityFrameworkCore.Database.Command` to `Information` in one test run — so db-reviewer can confirm the lookup is an index seek on `IX_RefreshTokens_TokenHash` and not a scan.

---

## Phase 5: Login / refresh / logout use cases in the Application layer

**No schema, no migrations, no queries. db-reviewer not required.**

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

`LoginCommandHandler` order: `ThrowIfNull(command)`; validate credentials; on failure return that error **without issuing a refresh token**; on success issue the refresh token, then the access token, then compose `AuthenticationResponse` with `TokenType = "Bearer"`.

Validator error codes: `LoginCommandValidator` uses `.WithErrorCode("Auth.EmailRequired")` on `NotEmpty`, `.WithErrorCode("Auth.EmailInvalid")` on `EmailAddress`, `.WithErrorCode("Auth.PasswordRequired")` on password `NotEmpty`. Both token validators use `.WithErrorCode("Auth.RefreshTokenRequired")`. No maximum-length rule on the password: truncating a passphrase at validation time is a worse failure than letting `CheckPasswordAsync` reject it.

### Tests to add

`tests/Envanex.Application.Tests/Authentication/Commands/LoginCommandHandlerTests.cs`:
- `HandleAsync_ValidCredentials_ShouldReturnAnAccessTokenAndARefreshToken`
- `HandleAsync_ValidCredentials_ShouldReturnBearerAsTheTokenType`
- `HandleAsync_ValidCredentials_ShouldReturnBothExpiryTimestamps`
- `HandleAsync_InvalidCredentials_ShouldReturnAuthInvalidCredentials`
- `HandleAsync_InvalidCredentials_ShouldNotIssueARefreshToken`
- `HandleAsync_InvalidCredentials_ShouldNotIssueAnAccessToken`
- `HandleAsync_LockedOutUser_ShouldReturnAuthUserLockedOut`
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
Depends on Proposal 4.

### Files

- `src/Envanex.Web/Envanex.Web.csproj` — modified — add `<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />`.
- `src/Envanex.Web/Extensions/JwtAuthenticationExtensions.cs` — created.
- `src/Envanex.Web/Controllers/AuthController.cs` — created.
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — modified — add `ToNoContentActionResult<T>`.
- `src/Envanex.Web/Program.cs` — modified — see below.
- `src/Envanex.Web/appsettings.json` — modified — add `"Login": { "Enabled": true, "PermitLimit": 5, "WindowSeconds": 300 }` inside the existing `RateLimiting` section.
- `tests/Envanex.IntegrationTests/Fixtures/LoginRateLimitedWebApplicationFactory.cs` — created.
- `docs/roadmap.md` — modified — mark PR 6a done; record the gaps this PR knowingly leaves: zero reuse-detection grace period (research decision 7), no `JwtBearerEvents.OnChallenge` ProblemDetails body until 6b, no refresh-token pruning job, `SqlServerFixture` reset still hand-maintained (6a handled the auth half, PR 8 owns the full fix, per research decision 5), passkeys deferred, login rate limiter partitioned by `RemoteIpAddress` and therefore proxy-unaware until PR 7.
- `docs/adr/0007-jwt-access-tokens-and-rotated-refresh-tokens.md` — created **empty, title line only**. The human writes the body.

### Signatures

```csharp
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddEnvanexJwtBearer(
        this IServiceCollection services,
        IConfiguration configuration);
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

`AddEnvanexJwtBearer` binds `Jwt:*` into a `JwtOptions`, calls `JwtOptionsGuard.ThrowIfInvalid`, then `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = ...)` with `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime` and `ValidateIssuerSigningKey` all true, and **`ClockSkew = TimeSpan.Zero`**. The default five-minute skew would make a 15-minute access token effectively last 20 and would make every `FakeTimeProvider`-driven expiry test lie.

There is deliberately **no `JwtBearerEvents.OnChallenge`** in this phase. No endpoint is `[Authorize]` in 6a, so no challenge is reachable and such a handler would be untestable dead code. It ships with the first `[Authorize]` in 6b.

`Logout` returns `result.ToNoContentActionResult()` — 204 on success, the mapped ProblemDetails on failure.

### `Program.cs` — exact changes

1. After `builder.Services.AddInfrastructure(builder.Configuration);` add `builder.Services.AddEnvanexJwtBearer(builder.Configuration);` and `builder.Services.AddAuthorization();`.
2. Rate limiter restructure (Proposal 4): move `builder.Services.AddRateLimiter(options => { ... })` **out** of `if (rateLimitingEnabled)`. Inside the lambda, keep `RejectionStatusCode` and `OnRejected` exactly as they are; assign `options.GlobalLimiter` only when `rateLimitingEnabled`; and always register the named policy `AuthController.LoginRateLimitPolicy`, reading `RateLimiting:Login:{Enabled,PermitLimit,WindowSeconds}`. When `RateLimiting:Login:Enabled` is false the policy resolves to `RateLimitPartition.GetNoLimiter`. When true it is a fixed-window limiter partitioned by `context.Connection.RemoteIpAddress?.ToString() ?? "unknown"`.
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
- `Login_WithUnknownEmailAndWrongPassword_ShouldReturnTheSameProblemDetail` — no account-existence oracle on the credential path.
- `Login_WithEmptyPassword_ShouldReturn400WithTheTurkishPasswordRequiredMessage`
- `Login_WithMalformedEmail_ShouldReturn400WithTheTurkishEmailInvalidMessage`
- `Login_AfterFiveWrongPasswords_ShouldReturn403`
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
- `Login_ShouldNotBeAbleToCreateAUser` — `POST /api/auth/register` returns 404; research decision 11 says 6a has no user-creating endpoint, and this asserts one did not sneak in.

`tests/Envanex.IntegrationTests/Api/AuthPipelineTests.cs` — new, `[Collection(DatabaseCollection.Name)]`:
- `Refresh_WithUnknownToken_ShouldReturn401ProblemJsonAndNotTheNotFoundPage` — **this is the named test the pipeline-placement question requires.** Asserts status 401, `Content-Type` starting `application/problem+json`, body does not contain `<html` (case-insensitive), and the parsed `status` field is 401 — the same assertion shape `RateLimiterTests` already uses for the 429 case.
- `OpenEndpoint_WithNoAuthorizationHeader_ShouldStillReturn200` — `UseAuthentication` must not close an endpoint that 6a leaves open.
- `OpenEndpoint_WithAMalformedBearerToken_ShouldStillReturn200` — a garbage token must be ignored, not turned into a 401, before 6b introduces `[Authorize]`.
- `OpenEndpoint_WithAnExpiredBearerToken_ShouldStillReturn200`
- `OpenEndpoint_WithAValidBearerToken_ShouldStillReturn200`

`tests/Envanex.IntegrationTests/Api/LoginRateLimiterTests.cs` — new, uses `LoginRateLimitedWebApplicationFactory` (Proposal 4):
- `Login_ExceedingTheLoginRateLimit_ShouldReturn429ProblemJson`
- `Login_ExceedingTheLoginRateLimit_ShouldIncludeARetryAfterHeader`
- `Login_UnderTheLoginRateLimit_ShouldReturn401ForAWrongPassword` — proves the policy is not rejecting everything.
- `ProductsEndpoint_ShouldNotBeAffectedByTheLoginPolicy` — proves the policy is scoped to the login action and has not leaked to the global limiter.

`tests/Envanex.IntegrationTests/Api/RateLimiterTests.cs` — modified, regression for the Proposal 4 restructure:
- `SharedFactory_WithGlobalLimiterDisabled_ShouldNotReject20ConsecutiveRequests` — run against `fixture.WebApplicationFactory`; proves that making `AddRateLimiter`/`UseRateLimiter` unconditional did not quietly start limiting every other test.
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

Manual smoke against the running host: `POST /api/auth/login` with a seeded user returns 200 and a token pair; `POST /api/auth/refresh` with that token returns a new pair; replaying the first refresh token returns 401 and the second token then also returns 401. Paste the four responses into the phase report.

Also confirm and report: with `Jwt:SigningKey` unset, `dotnet run --project src/Envanex.Web` fails at startup with the `InvalidOperationException` naming `Jwt:SigningKey` and the user-secrets command — not with a silently generated key.

---

## Expected test counts

| Suite | Before | After |
|---|---|---|
| `Envanex.Domain.Tests` | 113 | ~121 |
| `Envanex.Application.Tests` | 79 | ~119 |
| `Envanex.IntegrationTests` | 107 | ~215 |

The counts are estimates; the exact number after each phase must come from the `dotnet test` output in that phase's report, not from this table.

---

## Rollback notes

Every phase is a separate commit on `feat/auth`, so `git revert` of a single commit is the first resort.

- **Phase 1** — reverting the code is not enough once `dotnet ef database update` has run against a real database. The `auth` schema, seven Identity tables and `auth.__EFMigrationsHistory` survive. To undo: `dotnet ef database update 0 -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext`, then drop the schema with `DROP SCHEMA auth`. Local containers are disposable; the Testcontainers database is rebuilt on every run, so tests are unaffected. **`dbo.__EFMigrationsHistory` must not be touched** — the business context's history lives there and the two are separate precisely so this rollback cannot damage business data.
- **Phase 2** — `dotnet ef migrations remove -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext` if `AddRefreshTokens` has not been applied to any shared database; otherwise `database update InitialIdentitySchema` first. **Never hand-edit the generated migration to change its intent** — create a new one.
- **Phase 3** — pure revert. The only cross-cutting change is the `ResultMappingTests` scan widening; reverting it and the nine table entries together keeps the four reflection facts consistent. Reverting one without the other turns `ResultMapping_NoMappingEntry_ShouldBeOrphaned` red.
- **Phase 4** — pure revert, plus removing the `Jwt:*` keys from `DependencyInjectionTests.BuildProvider()` and the two factories. Leaving them in is harmless; removing `AddEnvanexIdentity` without them is also harmless.
- **Phase 5** — pure revert. Remember to remove the three rows from `CommandHandlerTypes()` as well, or the theory fails on an unresolvable service.
- **Phase 6** — pure revert, with one caveat: the `Program.cs` rate-limiter restructure is the only edit that touches previously-working behaviour. If it has to come out, restore `AddRateLimiter` and `UseRateLimiter()` inside `if (rateLimitingEnabled)` and drop `[EnableRateLimiting("login")]` from the controller in the same commit — an attribute naming a policy the middleware cannot see throws at endpoint build time, which would take down every endpoint, not just login.

Per project rules the coder never commits and never pushes. Take a `git commit` before starting each phase so the rollback above has something to return to.

---

## Notes for the human

1. The four proposals need a decision before the coder starts Phase 1. Phases 1 and 3 are safe to start with only Proposal 1 settled; Phase 2 needs Proposals 2 and 3; Phase 6 needs Proposal 4.
2. db-reviewer runs after Phases 1, 2 and 4 only.
3. The ADR stub goes in at Phase 6; the body is written after `tester` returns `READY_TO_PUSH`.
4. Phase 1's validation deliberately runs `dotnet ef migrations list` **without** `--context` and expects it to fail. The research marks that behaviour unverified, and a passing command there would invalidate the two-context assumption the rest of the plan rests on.
