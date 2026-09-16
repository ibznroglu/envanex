## Verdict

NEEDS_REVISION

---

## Findings — proven by reading files in this repository

### 1. [Blocker] Phase 6 turns the login rate limiter on for the *shared* test factory, which will leave `AuthApiTests` red

Plan `thoughts/shared/plans/2026-09-16_auth-infrastructure.md:987` adds `"Login": { "Enabled": true, "PermitLimit": 5, "WindowSeconds": 300 }` to `src/Envanex.Web/appsettings.json`.

- `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs:25` sets only `RateLimiting:Enabled=false`. It does **not** and cannot affect `RateLimiting:Login:Enabled`.
- The plan's Phase 4 file list (plan:593) touches that factory only for the six `Jwt:*` keys; the Phase 6 file list (plan:981–998) does not touch it at all. So after Phase 6 the shared factory runs the `"login"` policy as a **real** 5-per-300s limiter.
- `LoginRateLimitPartition.GetKey` deliberately returns the constant `"unknown"` under `WebApplicationFactory` (plan:87, plan:1048), so *every* login request in the run lands in one partition.
- `AuthApiTests` (plan:1076) uses `fixture.WebApplicationFactory`, which is constructed once for the whole collection (`tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs:43`) and disposed only at the end (`SqlServerFixture.cs:48-51`).
- `Login_AfterFiveWrongPasswords_ShouldReturn401` (plan:1083) is 5 requests; `Login_AfterFiveWrongPasswords_ShouldAlsoReturn401ForTheCorrectPassword` (plan:1084) is 6 more; `Login_UnknownEmailWrongPasswordAndLockedOutAccount_...` (plan:1085) needs at least 7. The class issues well over 20 login requests against a 5-request budget.

This is precisely the failure Decision 4 describes at plan:85 ("the 5-request budget would be consumed across classes and produce flaky 429s") and claims to have designed around — the design is right, the file list omits the edit that implements it. `tests/Envanex.IntegrationTests/Fixtures/RateLimitedWebApplicationFactory.cs:20-32` has the same omission (less damaging today, since `RateLimiterTests` never calls login).

### 2. [Blocker] The three rotation-vs-rotation concurrency tests can go red on a legitimate serialized interleaving

Plan:711 specifies that `RotatedAt is not null` is **reuse**: revoke every live row in the family and return `AuthErrors.RefreshTokenReused`.

`RefreshTokenConcurrencyTests` (plan:797–800) runs two `RotateAsync` calls on the same token via `Task.WhenAll`, each in its own scope. If on any iteration the two happen to serialize — task A commits before task B executes its step-2 lookup — task B does not hit a loser path at all; it hits step 6 and detects reuse. Then:

- plan:798 `...ShouldReturnInvalidRefreshTokenForTheLoser` — fails: the second result carries `Auth.RefreshTokenReused`.
- plan:799 `...ShouldLeaveExactlyOneLiveRowInTheFamily` — fails: reuse detection revoked the whole family, so the count is 0, not 1.
- plan:800 `...ShouldNotLeaveTheParentUnstamped` — fails: `ReplacedByTokenId` points at a row that is now revoked, not "the single live row".

plan:804 mandates ≥20 iterations, which raises the probability of hitting a serialized interleaving rather than lowering it. Note that `DatabaseCollection` being a single collection fixture is irrelevant here — the parallelism is *inside* one test method, so xUnit's class-level serialization neither causes nor prevents this. The two revocation-race tests (plan:801–802) are sound under both interleavings; only the rotation-vs-rotation trio is broken.

### 3. [High] Phase 2's schema tests need an `auth.AspNetUsers` row, and the only sanctioned way to create one arrives in Phase 4

`RefreshTokenConfiguration` declares a required cascading FK to `EnvanexUser` (plan:343). These Phase 2 tests all insert `RefreshTokens` rows and therefore need a real user row:

- plan:384 `RefreshTokens_InsertingASecondLiveTokenForTheSameFamily_ShouldThrowUniqueViolation`
- plan:385 `RefreshTokens_InsertingASecondTokenWithTheSameHash_ShouldThrowUniqueViolation`
- plan:386 `RefreshTokens_RotatedRowAndItsChild_ShouldCoexistInTheSameFamily`

`IdentitySeeder` is created in Phase 4 (plan:595), and research decision 6 (`thoughts/shared/research/2026-09-16_auth-infrastructure.md:346-348`) forbids writing users through a raw `DbContext`. Phase 2 never says how it obtains a user, so the coder must either guess or violate a settled decision.

### 4. [High] No index covers the family queries the plan introduces

Three code paths query `WHERE FamilyId = @f AND RevokedAt IS NULL`:
- reuse detection (plan:711), expiry revocation (plan:712), `RevokeFamilyAsync` (plan:725, and the re-query at plan:727).

The index list at plan:336–346 contains only: PK on `Id` (nonclustered), clustered on `(CreatedAt, Id)`, unique on `TokenHash`, unique **filtered** on `FamilyId` where `RotatedAt IS NULL AND RevokedAt IS NULL`, and `UserId`. The filtered index cannot serve a predicate that must also return *rotated* rows — which is exactly what reuse detection and logout must return. There is no unfiltered `FamilyId` index, so every reuse detection and every logout is a clustered-index scan on an append-only, never-pruned table. The Phase 4 report requirement (plan:842) asks db-reviewer to confirm only the `TokenHash` seek, so this would not be caught there either.

### 5. [Medium] Phase 1's package-version instruction is unexecutable in Phase 1

plan:153 tells the coder to pin `Microsoft.IdentityModel.JsonWebTokens` to "the exact version JwtBearer 10.0.11 resolves", determined by `dotnet list src/Envanex.Web/Envanex.Web.csproj package --include-transitive` "after adding JwtBearer". But the `PackageReference` to `Microsoft.AspNetCore.Authentication.JwtBearer` is not added to `src/Envanex.Web/Envanex.Web.csproj` until Phase 6 (plan:981), and `Directory.Packages.props` only carries `PackageVersion` entries (verified: `Directory.Packages.props:1-40` has no `PackageReference`). At Phase 1 no project references JwtBearer, so the command reports nothing for it and the coder must guess — which is the NU1605 build failure the same bullet warns about.

### 6. [Medium] The plan asserts a fact about existing code that is false: `Auth.PasswordRequired` / "Şifre"

plan:126: *"the codebase currently uses 'Şifre' for password in `Auth.PasswordRequired`"*, and plan:518 presents `"Parola zorunludur."` as a change *"(was 'Şifre zorunludur.')"*.

- `rg "Auth\."` over the whole repository matches only the plan file itself — no `Auth.*` error code exists in `src/`.
- `src/Envanex.Web/Extensions/TurkishErrorMessages.cs:7-53` contains exactly 33 entries and none of them contains the substring "ifre" (verified by a case-insensitive grep over `src/`, no matches).

`Auth.PasswordRequired` is one of the **eight new** codes, not an existing one. Consequences inside the plan:
- The rollback note at plan:1175 describes reverting an alignment that never existed.
- `TurkishErrorMessagesTests.PasswordMessages_ShouldUseParolaConsistentlyAndNeverSifre` (plan:557) is green before and after the change and protects nothing — which `docs/roadmap.md:190-192` explicitly calls out as a defect pattern ("A test that passes is not a test that protects").

### 7. [Medium] `LoginRateLimiterTests` is under-specified in the two ways that decide whether it is deterministic

plan:1114 does not state:
- (a) that a **fresh** `LoginRateLimitedWebApplicationFactory` must be built per test. The precedent it cites builds one per test in `InitializeAsync` (`tests/Envanex.IntegrationTests/Api/RateLimiterTests.cs:25-30`). With one factory per class, the 2-permit/60 s budget is shared by all five cases and at least four of them break.
- (b) that the class needs `[Collection(DatabaseCollection.Name)]` to reach `_fixture.ConnectionString` — every other factory-based class does (`RateLimiterTests.cs:11,19-23`).

### 8. [Medium-Low] The `EnvanexDbContext` configuration predicate — the change the plan calls "not cosmetic" — has no direct regression test

plan:177 justifies restricting `ApplyConfigurationsFromAssembly` to `Envanex.Infrastructure.Persistence.Configurations` because `RefreshTokenConfiguration` would otherwise create a second `dbo.RefreshTokens`. Verified: all three existing configurations are in that namespace (`src/Envanex.Infrastructure/Persistence/Configurations/ProductConfiguration.cs:6`, plus `UnitOfMeasureConfiguration.cs`, `WarehouseConfiguration.cs`), so the negative control at plan:238 is sound.

But the *positive* guard is missing. plan:237 adds `EnvanexDbContext_ShouldNotMapEnvanexUser`, which is vacuous in every phase — `EnvanexUser` has no `IEntityTypeConfiguration` in the assembly at any point, so that test would stay green with the predicate deleted. Phase 2's test list (plan:348–388) contains no `EnvanexDbContext_ShouldNotMapRefreshToken`.

### 9. [Low] "Decision 1" tags on the Phase 2 `RefreshToken` tests overstate what they prove

`CreateRoot` and `CreateChild` take `idleWindow` / `absoluteWindow` as parameters (plan:302–306), so `CreateRoot_ShouldSetExpiresAtToSevenDaysFromNow` (plan:363) and its siblings prove `now + argument`, not the 7/30-day numbers. Phase 4's equivalents (plan:770–771) read `JwtOptions` from the test's own in-memory configuration. Nothing in any phase asserts the values the plan writes into `src/Envanex.Web/appsettings.json` (plan:425), so a typo there (e.g. `AccessTokenMinutes: 150`) ships green.

### 10. [Low] `Program.cs` restructure omits a hoist

plan:1059 moves `builder.Services.AddRateLimiter(...)` out of `if (rateLimitingEnabled)`, but `permitLimit` and `windowSeconds` are declared **inside** that block today (`src/Envanex.Web/Program.cs:30-31`). The plan does not say to hoist them.

### 11. [Low] Two under-specified test details

- plan:1123 `SharedFactory_WithGlobalLimiterDisabled_ShouldNotReject150ConsecutiveRequests` does not name the endpoint it hits 150 times. Against a write endpoint it also collides with the collection's reset semantics; against the shared factory it adds measurable time to a 24 s suite.
- plan:244 `ForbiddenPackageMatcher_ShouldMatch_RealAuthPackageNames` says it "runs the same matcher", but the regex lives inline inside the test body (`tests/Envanex.Domain.Tests/ArchitectureTests.cs:51-54`). "The same matcher" requires extracting it into a shared private helper, which the plan does not specify.

### 12. [Cosmetic] plan:3

"Supersedes `thoughts/shared/plans/2026-09-16_auth-infrastructure.md`. Save over that file." — that is this file's own path.

---

## Findings I could **not** verify (hypotheses only — I have no shell)

- **UNVERIFIED (A)** — `services.Configure<JwtOptions>(section)` / `section.Bind(...)` inside `Envanex.Infrastructure` (plan:671) may not compile. `src/Envanex.Infrastructure/Envanex.Infrastructure.csproj:9-15` references only `Microsoft.EntityFrameworkCore.Design` and `.SqlServer`; `Microsoft.Extensions.Options.ConfigurationExtensions` and `Microsoft.Extensions.Configuration.Binder` are not obviously in the transitive closure. **Settled by:** `dotnet list src/Envanex.Infrastructure/Envanex.Infrastructure.csproj package --include-transitive`, then `dotnet build -warnaserror`.
- **UNVERIFIED (B)** — `ILogger<IdentityService>` / `ILogger<RefreshTokenService>` (plan:613, 626) must resolve from the hand-built container in `tests/Envanex.IntegrationTests/DependencyInjectionTests.cs:35-39`, which calls no `AddLogging()`. I believe `AddIdentityCore` calls `services.AddOptions().AddLogging()` internally and this is fine, but the plan nowhere states the dependency. **Settled by:** `dotnet test tests/Envanex.IntegrationTests --filter "FullyQualifiedName~AllRegisteredServices_ShouldResolve"` after Phase 4.
- **UNVERIFIED (C)** — that `ApplyConfigurationsFromAssembly` picks up `internal` `IEntityTypeConfiguration<>` types at all (the entire premise of plan:177 and of finding 8). **Settled by:** after Phase 2, `dotnet ef migrations add <Probe> --context EnvanexDbContext` with the predicate temporarily removed and reading the generated `Up`.
- **UNVERIFIED (D)** — `RateLimiterOptions.GlobalLimiter` being readable from `IOptions<RateLimiterOptions>` (plan:1122). **Settled by:** `dotnet build -warnaserror`.
- **UNVERIFIED (E)** — that `CheckPasswordAsync`, `IsLockedOutAsync`, `AccessFailedAsync` and `ResetAccessFailedCountAsync` are all `public virtual` on `UserManager<TUser>` (plan:659-662). **Settled by:** `dotnet build -warnaserror` after Phase 4.
- **UNVERIFIED (F)** — that `UserManager.AccessFailedAsync` updates the tracked entity's `LockoutEnd` in place, which plan:693 relies on to avoid a second round trip. **Settled by:** the Phase 4 test run.
- plan:267 and plan:1189 already mark the `dotnet ef ... --context` premise as unverified and require pasting raw output. That handling is correct; no change needed.

---

## Things I checked that are **correct** (so the planner does not re-litigate them)

- **Mapping-table arithmetic.** `ResultExtensions.StatusCodeMap` has exactly 33 entries (`src/Envanex.Web/Extensions/ResultExtensions.cs:19-67`) and `TurkishErrorMessages.Messages` has exactly 33 (`src/Envanex.Web/Extensions/TurkishErrorMessages.cs:10-52`). Eight new codes → 41 / 41 in both tables (plan:423-424, 497-506, 512-519). Dropping `Auth.UserLockedOut` is applied consistently to `AuthErrors`, `StatusCodeMap` and `TurkishErrorMessages`, so `ResultMapping_NoMappingEntry_ShouldBeOrphaned` leaves no orphan.
- **The widened scan is safe against all four existing facts.** `tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs:24-41` filters `t.IsPublic && t != Error && t != ValidationError` then `IsInitOnly && FieldType == Error`. `Envanex.Application` declares **zero** `public static readonly Error` fields today (grep over `src/` returns only Domain hits), so widening to `typeof(AuthErrors).Assembly` adds exactly the 8 auth codes and each of the four renamed facts stays satisfiable. `AuthErrors` is a top-level public static class, so `Type.IsPublic` holds.
- **The reuse-at-depth test is genuinely load-bearing.** plan:784 is self-contained across three rotations, asserts every row of the family carries `RevokedAt` with reason `Reuse`, and asserts the generation-4 token then returns `InvalidRefreshToken`. Because plan:711 revokes every row with `RevokedAt IS NULL` (which includes rotated-but-unrevoked generation 1), the assertion is satisfiable and would go red if the predecessor row were deleted or if only the presented token were revoked.
- **Pipeline placement.** Inserting `UseAuthentication`/`UseAuthorization` after `app.UseHttpsRedirection()` (`src/Envanex.Web/Program.cs:100`) and before `app.UseAntiforgery()` (`Program.cs:102`) does put them inside the `UseStatusCodePagesWithReExecute` wrapper at `Program.cs:99`. The plan states this explicitly (plan:1070), correctly argues no bodiless 401 is reachable in 6a, and pins it with `AuthPipelineTests.Refresh_WithUnknownToken_ShouldReturn401ProblemJsonAndNotTheNotFoundPage` using the same assertion shape as the existing 429 case (`RateLimiterTests.cs:59-71`).
- **The revocation-race pair (plan:801-802) is sound** under both interleavings and would go red if the bounded retry loop at plan:726 were removed.
- **The Decision 6 ordering test is real.** `CallLog` beginning `["CheckPasswordAsync", "IsLockedOutAsync"]` (plan:758) does prove the reordering, not merely that both ran.
- **Two-context setup completeness:** design-time factory (plan:175, mirroring `src/Envanex.Infrastructure/Persistence/EnvanexDbContextFactory.cs:6-25`), `--output-dir Migrations/Identity` (plan:163, 261), the `auth` migrations-history table funnelled through one extension method (plan:174, 222), and the fixture applying both chains in a stated order (plan:179, against `SqlServerFixture.cs:36-44`). All four are present.
- `InternalsVisibleTo` to `Envanex.IntegrationTests` exists (`src/Envanex.Infrastructure/Envanex.Infrastructure.csproj:3`), and `ColumnNames.RowVersion` exists as the plan uses it (`src/Envanex.Infrastructure/Persistence/Constants/ColumnNames.cs:5`).
- `ResetIdentityAsync` does not need an explicit `RefreshTokens` delete: the cascading FK at plan:343 removes them when `auth.AspNetUsers` is deleted last. (Worth one sentence in the plan so a reader does not "fix" it, but not a defect.)

---

## Required changes (ordered by severity)

1. **Add `RateLimiting:Login:Enabled=false` to `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs` and to `RateLimitedWebApplicationFactory.cs`, in the Phase 6 file list**, with a one-line comment giving the reason (shared factory, single `"unknown"` partition, whole-collection lifetime). Add a Phase 6 test that locks it in — e.g. `SharedFactory_ShouldNotEnableTheLoginPolicy`, resolving `IOptions<RateLimiterOptions>` from `fixture.WebApplicationFactory.Services` — or an `AuthApiTests` case that issues more than `PermitLimit` logins and expects no 429.

2. **Fix the three rotation-vs-rotation concurrency assertions (plan:797–800)** so a serialized interleaving is a pass, not a failure. Either:
   - state the accepted outcome set explicitly — the non-winner's error is `Auth.InvalidRefreshToken` **or** `Auth.RefreshTokenReused`, and the post-state is "exactly one live row **or** zero live rows with every row revoked with reason `Reuse`" — and rename the tests to say so; **or**
   - force genuine overlap with a synchronisation barrier (both tasks read the parent before either saves) and keep the strict assertions, stating how the barrier is implemented.
   Whichever is chosen, say in the plan **which guard's removal turns each test red**, per `docs/roadmap.md:190-192`.

3. **Say in Phase 2 how the FK-satisfying `AspNetUsers` row is created** for plan:384–386. Either move `IdentitySeeder.CreateUserAsync` (plan:595) from Phase 4 to Phase 2, or record an explicit, narrow exception to research decision 6 for schema-only tests and name the exact insert path.

4. **Add an unfiltered index on `FamilyId`** (or `(FamilyId, RevokedAt)`) to the Phase 2 EF configuration at plan:336–346, and extend the Phase 4 report requirement at plan:842 to include the plan for the family query, not just the `TokenHash` seek. If the scan is a deliberate trade for a low-volume table, say so explicitly so db-reviewer can rule on it.

5. **Move the `Microsoft.IdentityModel.JsonWebTokens` version determination to where it is executable.** Either add the `Microsoft.AspNetCore.Authentication.JwtBearer` `PackageReference` to `src/Envanex.Web/Envanex.Web.csproj` in Phase 1 (unused but resolvable), or move the pin to Phase 6 and drop the Phase 1 `PackageReference` from `Envanex.IntegrationTests.csproj` until then. Give the coder a deterministic command that works at the phase where it is run.

6. **Correct plan:126, plan:518 and plan:1175.** `Auth.PasswordRequired` is a new code, not an existing one, and no existing Turkish message uses "Şifre". Either drop the "vocabulary alignment" framing and the rollback note, or replace `PasswordMessages_ShouldUseParolaConsistentlyAndNeverSifre` (plan:557) with a guard that would actually fail if the new message used "Şifre" — and say so.

7. **Specify `LoginRateLimiterTests` (plan:1114)**: a fresh `LoginRateLimitedWebApplicationFactory` per test in `InitializeAsync`, and `[Collection(DatabaseCollection.Name)]` for the connection string.

8. **Add `EnvanexDbContext_ShouldNotMapRefreshToken` to Phase 2's test list**, since that is the guard the plan:177 predicate change actually exists to protect. Keep or drop `EnvanexDbContext_ShouldNotMapEnvanexUser` as taste dictates, but do not present it as the guard.

9. **Add an assertion that pins the `appsettings.json` values** (15 / 7 / 30) — e.g. a Phase 6 test that binds `Jwt` from the shipped `appsettings.json` and checks the three numbers — or remove the "(Decision 1)" tags from plan:363–368, which currently claim more than those tests prove.

10. **Say to hoist `permitLimit` / `windowSeconds` out of the `if` block** in plan:1059 (`src/Envanex.Web/Program.cs:30-31`).

11. **Name the endpoint** used by `SharedFactory_WithGlobalLimiterDisabled_ShouldNotReject150ConsecutiveRequests` (plan:1123), and **say to extract the package-name regex** from `ArchitectureTests.cs:51-54` into a shared helper so plan:244 can genuinely run "the same matcher".

12. **Pre-empt the unverified items** by naming them in the plan as risks with the settling command, rather than leaving the coder to discover them mid-phase — specifically (A) the options/configuration-binder package availability in `Envanex.Infrastructure`, and (B) the absence of `AddLogging()` in `DependencyInjectionTests.BuildProvider()`.

13. **Fix plan:3** — the "supersedes" line points at this file's own path.

None of the settled decisions (1–6, research decisions 1–11, the indistinguishable 401, the password/lockout reordering, the PR 7 partition-key blocker) are contested by any finding above. Findings 2, 6 and 8 are defects *within* those decisions' test coverage, not objections to the decisions.

## Verdict

NEEDS_REVISION
