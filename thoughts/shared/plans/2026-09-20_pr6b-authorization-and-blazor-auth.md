# Plan: PR 6b — authorization policies, the Blazor cookie scheme, and the read-only demo account

Save as `C:\projects\envanex\thoughts\shared\plans\2026-09-20_pr6b-authorization-and-blazor-auth.md`.

---

## Goal

Close every endpoint behind a default-deny fallback policy; express permission as the two policies
`CanRead` and `CanWrite` fed by the roles `Administrator` and `Viewer`; give the Blazor UI a cookie
scheme with a statically-rendered login page, a sign-out control and the current user in `NavMenu`;
make every rejection path in both schemes carry a body; and ship a configuration-gated read-only
demo account.

## Non-goals

Password change, password reset, self-registration, email confirmation, two-factor, passkeys, any
change to refresh-token rotation, the product grid (PR 7), `UseForwardedHeaders` (PR 7, blocker),
and the `.gitattributes`/CRLF renormalisation chore.

## Touches schema? Per phase

No phase adds a table, a column or a migration. `auth.AspNetRoles`, `auth.AspNetUserRoles` and
`auth.AspNetRoleClaims` were all created by PR 6a's migration and are empty. **No `dotnet ef
migrations add` runs in this PR, so the LF/BOM normalisation step in `CLAUDE.md` does not apply.**

| Phase | Schema | Migration | New production queries | db-reviewer |
|---|---|---|---|---|
| 0 Spikes | no | no | no | not required |
| 1 Role claim | no | no | **yes** — `UserManager.GetRolesAsync`, a `UserRoles`×`Roles` join in `RefreshTokenService`, `RoleManager` inserts | **required** |
| 2 Authenticated test clients | no | no | no (test-project seeding only) | not required |
| 3 Cookie scheme + Blazor | no | no | **yes** — one `UserManager.FindByIdAsync` per cookie sign-in | **required (light)** |
| 4 Close the gate | no | no | no | not required |
| 5 Demo account | no | no | **yes** — idempotent role/user seeding writes at startup | **required** |

## ADR needed?

Yes — one file, title only, written by the human:

**`docs/adr/0008-policy-based-authorization-two-authentication-schemes-and-the-demo-account.md`**
titled **"0008 — Policy-based authorization, two authentication schemes, and the read-only demo
account"**.

It must cover, at minimum: the fallback-policy-plus-`[AllowAnonymous]` choice and the full exemption
list; why `/api/auth/logout` is exempt (Decision 14); the role claim added to the access token,
recorded against ADR 0007's token-content decision (Decision 5); path-prefix scheme selection and
its consequence that `/api/*` is bearer-only; and the `Demo:Enabled` gate.

---

# Phase 0: Spikes — throwaway, nothing committed

Two questions must be answered by running something before Phase 3 can be implemented as written.
This phase produces **no committed source**. Its deliverable is a debug note.

### Files

- `thoughts/shared/debug/2026-09-20_blazor-antiforgery-and-rate-limit-spike.md` — created — the two
  observations, verbatim output pasted, and which branch of Phase 3 they select.
- `src/Envanex.Web/Components/Pages/SpikeForm.razor` — created **and deleted before the phase ends**.

### Spike A — how a statically-rendered Blazor form obtains and submits its antiforgery token

Action:

1. Create `src/Envanex.Web/Components/Pages/SpikeForm.razor` at `@page "/spike-form"` containing an
   `<EditForm Model="Model" FormName="spike" method="post" OnValidSubmit="Submit">` with one text
   input bound through `[SupplyParameterFromForm]`, and a `[CascadingParameter] HttpContext?`.
2. `dotnet run --project C:\projects\envanex\src\Envanex.Web`
3. `curl -i -c C:\Users\jesus\AppData\Local\Temp\claude\C--projects-envanex\9140ec6e-37f7-492e-b9c5-92e59b57163e\scratchpad\cookies.txt http://localhost:5000/spike-form`
4. `curl -i -b <cookiejar> -X POST -d "_handler=spike&__RequestVerificationToken=<value>&Model.Text=x" -H "Content-Type: application/x-www-form-urlencoded" http://localhost:5000/spike-form`

Expected observation: step 3's HTML carries `<input type="hidden" name="_handler" value="spike">`
and `<input type="hidden" name="__RequestVerificationToken" value="CfDJ8…">`, and a
`Set-Cookie: .AspNetCore.Antiforgery.*` response header; step 4 answers 200 or 302, and the same
POST with the token field removed answers 400.

- **A1 — token field rendered automatically.** Phase 3 proceeds as written: `Login.razor` is an
  `<EditForm FormName="login">`, and `LoginPageTests` parses the hidden field out of the GET body.
- **A2 — token field absent until asked for.** Phase 3 adds an explicit `<AntiforgeryToken />` child
  inside the `EditForm`. Nothing else in Phase 3 changes.
- **A3 — POST rejected even with cookie and field.** Phase 3 collapses into Spike B's fallback
  branch (see B2): a plain `<form method="post" action="/login">` plus a minimal endpoint that calls
  `IAntiforgery.ValidateRequestAsync` explicitly.

Also record the exact hidden-field **name** observed; every test in Phase 3 and Phase 4 that posts
the login form reads it.

### Spike B — does `[EnableRateLimiting]` on a page component reach endpoint metadata?

Action: add `@attribute [EnableRateLimiting(AuthController.LoginRateLimitPolicy)]` to the same spike
page, run the host with `RateLimiting:Login:Enabled=true`, `RateLimiting:Login:PermitLimit=2`,
`RateLimiting:Login:WindowSeconds=60`, and issue three `GET /spike-form` and, separately, three
`POST /spike-form`.

Expected observation: the third request in each sequence answers 429 with
`application/problem+json`.

- **B1 — the attribute reaches metadata (expected; `UseRouting` is inserted at the head of the
  pipeline by `WebApplication`, which is why `UseRateLimiter` at `Program.cs:125` already sees
  `AuthController.Login`'s attribute).** Phase 3 puts the attribute on `Login.razor`.
  **Sub-observation that matters:** the component endpoint serves GET and POST, so a GET of the
  login page will also spend a login permit — five page reloads would lock login out for five
  minutes. If the three GETs produce a 429, Phase 3 **must** add a method guard inside the `"login"`
  policy delegate in `Program.cs` (return `RateLimitPartition.GetNoLimiter(partitionKey)` when
  `context.Request.Method` is not `POST`) and must keep the test
  `BlazorLoginPage_RepeatedGets_ShouldNotSpendLoginPermits`. If the three GETs do **not** 429, drop
  the guard and drop that test; Phase 3's count falls by one.
- **B2 — the attribute does not reach metadata.** Decision 9's mechanism falls back and **Phase 3 is
  reshaped**, spelled out here so implementation does not discover it:
  - `Login.razor` renders the form only and performs no sign-in.
  - `Program.cs` maps `app.MapPost("/login", …)`, chained with
    `.RequireRateLimiting(AuthController.LoginRateLimitPolicy)` and `.AllowAnonymous()`.
  - That endpoint validates antiforgery through `IAntiforgery.ValidateRequestAsync`, calls
    `CookieSignInService.SignInAsync`, and answers `302 /` on success or `302 /login?error=credentials`
    on failure.
  - `Login.razor` reads `error` from the query string and renders the Turkish message.
  - Cost: one extra endpoint, one extra round trip on failure, loss of `EditForm` validation, and
    `LoginPageTests` case names change from `PostLoginForm_*` to `PostLoginEndpoint_*`. The test
    **count** is unchanged, so the Phase 3 total below holds either way.

### Tests to add

None. This phase writes no test.

### Validation

```powershell
cd C:\projects\envanex
dotnet run --project src\Envanex.Web    # spike observations, then Ctrl+C
git status --porcelain                  # MUST be empty before the phase is called done
dotnet build -warnaserror
dotnet test
```

Expected test count at end: **562** (120 + 126 + 316), unchanged.

---

# Phase 1: The role claim, `RoleClaimType`, and the two seeded roles

Decisions 2, 5, 12 and 13. This is the one phase that changes PR 6a code. No policy exists yet, so
nothing is enforced and nothing closes.

### Files

- `src/Envanex.Application/Authentication/EnvanexRoles.cs` — **created** — the two role names, in
  the layer both Web and Infrastructure can see.
- `src/Envanex.Application/Authentication/EnvanexClaimTypes.cs` — **created** — the single role claim
  type string, read by the signing half and the validating half. Two copies of this string is the
  failure ADR 0007 already names for the migrations-history table.
- `src/Envanex.Application/Authentication/Models/AuthenticatedUser.cs` — **modified** — a fourth
  positional member.
- `src/Envanex.Application/Abstractions/Authentication/IIdentityService.cs` — **modified** — doc
  comment only: the returned user now carries its roles.
- `src/Envanex.Infrastructure/Identity/IdentityService.cs` — **modified** — `GetRolesAsync` on the
  success path only, after `ResetAccessFailedCountAsync`. It must not run on any failure branch, or
  it becomes a fourth timing channel.
- `src/Envanex.Infrastructure/Identity/RefreshTokenService.cs` — **modified** — line 176: the
  rotated result must carry the user's roles, otherwise a refreshed access token silently loses its
  role and the user starts getting 403 fifteen minutes after login.
- `src/Envanex.Infrastructure/Identity/JwtAccessTokenIssuer.cs` — **modified** — one `role` claim
  per role, written only when the list is non-empty.
- `src/Envanex.Infrastructure/Identity/IdentityRoleSeeder.cs` — **created** — idempotent role
  creation through `RoleManager<IdentityRole<Guid>>`.
- `src/Envanex.Web/Extensions/JwtAuthenticationExtensions.cs` — **modified** — `RoleClaimType`,
  `NameClaimType`, and `MapInboundClaims = false`. The comment at lines 28–29 that says events ship
  in PR 6b stays until Phase 4.

### Signatures

```csharp
namespace Envanex.Application.Authentication;

public static class EnvanexRoles
{
    public const string Administrator = "Administrator";
    public const string Viewer = "Viewer";
    public static IReadOnlyList<string> All { get; }
}

public static class EnvanexClaimTypes
{
    // Short form on purpose. MapInboundClaims is switched off in the bearer handler, so the claim
    // type that is written is the claim type that is read; nothing remaps it to a URI.
    public const string Role = "role";
}

public sealed record AuthenticatedUser(Guid Id, string Email, string UserName, IReadOnlyList<string> Roles);
```

```csharp
namespace Envanex.Infrastructure.Identity;

public static class IdentityRoleSeeder
{
    public static Task EnsureRolesAsync(IServiceProvider services, CancellationToken ct = default);
}
```

`JwtAccessTokenIssuer.Issue` adds, inside the existing `Claims` dictionary construction:
`[EnvanexClaimTypes.Role] = user.Roles.ToArray()` when `user.Roles.Count > 0`. A `string[]` value
serialises as a JSON array, which the handler reads back as one claim per element.

`JwtAuthenticationExtensions.AddEnvanexJwtBearer` adds, inside the `AddJwtBearer` callback:

```csharp
bearer.MapInboundClaims = false;
bearer.TokenValidationParameters.RoleClaimType = EnvanexClaimTypes.Role;
bearer.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.Sub;
```

`IdentityOptions.ClaimsIdentity.RoleClaimType` is deliberately **not** changed.
`UserClaimsPrincipalFactory<EnvanexUser, IdentityRole<Guid>>` already writes role claims under
`ClaimTypes.Role`, and `ClaimsPrincipal.IsInRole` resolves per-identity, so a cookie identity and a
bearer identity satisfy the same `RequireRole` without sharing a claim type. Phase 4 proves this
with a test rather than leaving it as an assumption.

### What this does to `JwtAccessTokenIssuerTests`

`tests/Envanex.IntegrationTests/Identity/JwtAccessTokenIssuerTests.cs`.

- **All eight existing cases break to compile, none change behaviour.** The single cause is line
  25–26, the shared `private static readonly AuthenticatedUser User = new(…)`, which needs a fourth
  argument. Change it to `new(…, [])` and add a second field
  `private static readonly AuthenticatedUser UserInRoles = User with { Roles = [EnvanexRoles.Administrator, EnvanexRoles.Viewer] };`.
- No existing assertion is edited. `Issue_ShouldProduceATokenCarryingSubEmailAndJti` stays exactly
  as written and remains true — the role claim is additive.
- Same single-line compile break, same non-change to assertions, in
  `tests/Envanex.Application.Tests/Authentication/Commands/LoginCommandHandlerTests.cs:18` and
  `tests/Envanex.Application.Tests/Authentication/Commands/RefreshTokenCommandHandlerTests.cs:17`.
  Application test count stays at 126.

### Tests to add

`tests/Envanex.IntegrationTests/Identity/JwtAccessTokenIssuerTests.cs` (**+2**)
- `Issue_ForAUserInTwoRoles_ShouldWriteOneRoleClaimPerRole`
- `Issue_ForAUserWithNoRoles_ShouldWriteNoRoleClaim`

`tests/Envanex.IntegrationTests/Identity/IdentityServiceRoleTests.cs` (**created, +2**)
- `ValidateCredentialsAsync_UserInTheAdministratorRole_ShouldReturnThatRole`
- `ValidateCredentialsAsync_UserWithNoRole_ShouldReturnAnEmptyRoleList`

`tests/Envanex.IntegrationTests/Identity/BearerRoleClaimTests.cs` (**created, +2**)
- `HostBearerOptions_ShouldSetRoleClaimTypeToTheEnvanexRoleClaim` — resolves
  `IOptionsMonitor<JwtBearerOptions>` from `_fixture.WebApplicationFactory.Services` and reads
  `Get(JwtBearerDefaults.AuthenticationScheme)`.
- `TokenCarryingTheAdministratorRole_ValidatedWithHostOptions_ShouldProduceAPrincipalInThatRole` —
  validates a real issued token through the host's own `TokenValidationParameters` and asserts
  `ClaimsPrincipal.IsInRole(EnvanexRoles.Administrator)`. This is the test that would have caught a
  `MapInboundClaims` surprise.

`tests/Envanex.IntegrationTests/Identity/IdentityRoleSeederTests.cs` (**created, +2**)
- `EnsureRolesAsync_OnAnEmptyDatabase_ShouldCreateAdministratorAndViewer`
- `EnsureRolesAsync_CalledTwice_ShouldNotFailAndShouldNotDuplicate`

`tests/Envanex.IntegrationTests/Identity/RefreshTokenServiceTests.cs` (**+1**)
- `RotateAsync_ForAUserInTheAdministratorRole_ShouldCarryThatRoleIntoTheRotatedResult`

`tests/Envanex.IntegrationTests/DependencyInjectionTests.cs` (**+1**)
- `RoleManager_ShouldResolveFromTheInfrastructureContainer`

### Validation

```powershell
cd C:\projects\envanex
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~Identity"
```

Expected test count at end: **572** (120 + 126 + **326**).

**Run db-reviewer on this phase** — three new query shapes against the `auth` schema.

---

# Phase 2: Authenticated test clients, and the 72 tests switched over

Decision 6. The gate is still open at the end of this phase — an authenticated request to an open
endpoint answers exactly as an anonymous one does, so all 72 stay green either way. This phase
exists so that Phase 4 does not have to touch 72 tests and a policy in one commit.

### How seeding and token acquisition align with `ResetIdentityAsync`

Stated concretely, because this is the trap:

1. `SqlServerFixture` owns a token cache keyed by email.
2. **`ResetIdentityAsync` clears that cache in the same method body that issues the seven `DELETE`
   statements.** The only code that can delete a seeded user is the only code that can orphan a
   cached token, so it cannot orphan one.
3. Tokens are acquired **lazily, on a cache miss, inside `GetTokenAsync`** — never in a class
   constructor, never once per class. On a miss it ensures the role, ensures the user, then performs
   a real `POST /api/auth/login` through `WebApplicationFactory`, exactly as Decision 6 requires.
4. Ordering inside any single test is therefore always reset → ensure → login → use. The five
   affected classes call `_fixture.ResetAsync()` (business tables only, untouched by auth) and then
   `await _fixture.CreateAdministratorClientAsync()` in `InitializeAsync`.
5. Cost: because xUnit serialises the `DatabaseCollection`, a run of 37 consecutive
   `ProductsDatasourceTests` performs **one** user creation and **one** login, not 37. A login is
   re-performed only after a class that calls `ResetIdentityAsync` has run. Expected added wall
   clock: a handful of PBKDF2 pairs, not seventy-two. Per-test login was rejected for that reason —
   it would add roughly 14 s to a 44 s suite and push it past the 60 s line ADR 0007 set.

### How each of the five classes obtains an authenticated client

| Class | Cases | Factory | How |
|---|---|---|---|
| `ProductsApiTests` | 20 | shared | `_client = await _fixture.CreateAdministratorClientAsync();` in `InitializeAsync`, after `ResetAsync()` |
| `UnitOfMeasuresApiTests` | 8 | shared | same |
| `ProductsDatasourceTests` | 37 | shared | same |
| `ConcurrencyApiTests` | 2 | shared | same |
| `RateLimiterTests` | 5 | **`RateLimitedWebApplicationFactory`** | token minted **through the shared factory**, header attached to the rate-limited factory's client: `_client = await _fixture.CreateAdministratorClientAsync(_rateLimitedFactory);`. Logging in through the rate-limited host itself would spend one of its two global permits and break `MutationEndpoint_ExceedingRateLimit_ShouldReturn429`. All three factories share `TestSigningKey`, issuer, audience and database, so a token minted on one validates on any of them. `SharedFactory_…ShouldNotReject150Consecutive…` uses the shared factory's authenticated client. |

`LoginRateLimitedWebApplicationFactory` gets its authenticated path through the same overload and is
proved by a new case (below) rather than by an existing one: minting through the shared factory is
what keeps the 2-permit login budget intact.

### Files

- `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs` — **modified**.
- `tests/Envanex.IntegrationTests/Fixtures/IdentitySeeder.cs` — **modified** — idempotent helpers
  added. `CreateUserAsync` keeps its throw-on-duplicate contract; other classes rely on it.
- `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs` — **modified** — a second
  constructor taking an environment name and setting overrides, so Phases 4 and 5 configure this one
  type instead of adding factory types.
- `tests/Envanex.IntegrationTests/Api/ProductsApiTests.cs`,
  `UnitOfMeasuresApiTests.cs`, `ProductsDatasourceTests.cs`, `ConcurrencyApiTests.cs`,
  `RateLimiterTests.cs` — **modified** — `_client` moves from the constructor to `InitializeAsync`
  and becomes `private HttpClient _client = null!;`.
- `tests/Envanex.IntegrationTests/Api/AuthenticatedClientTests.cs` — **created**.

### Signatures

```csharp
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string AdministratorEmail = "fixture-administrator@envanex.test";
    public const string ViewerEmail        = "fixture-viewer@envanex.test";
    public const string RoleLessEmail      = "fixture-no-role@envanex.test";
    public const string SeededPassword     = "Envanex-Fixture-Parola-1";

    public Task<string> GetAccessTokenAsync(string email, string? role);
    public Task<string> GetAdministratorTokenAsync();
    public Task<string> GetViewerTokenAsync();
    public Task<string> GetRoleLessTokenAsync();

    public Task<HttpClient> CreateAdministratorClientAsync();
    public Task<HttpClient> CreateAdministratorClientAsync(WebApplicationFactory<Program> factory);
    public Task<HttpClient> CreateViewerClientAsync();
    public Task<HttpClient> CreateViewerClientAsync(WebApplicationFactory<Program> factory);
    public Task<HttpClient> CreateRoleLessClientAsync();
}
```

```csharp
internal static class IdentitySeeder
{
    public static Task EnsureRoleAsync(IServiceProvider services, string role);
    public static Task<Guid> EnsureUserAsync(IServiceProvider services, string email, string password);
    public static Task EnsureUserInRoleAsync(IServiceProvider services, string email, string role);
}
```

```csharp
public sealed class EnvanexWebApplicationFactory : WebApplicationFactory<Program>
{
    public EnvanexWebApplicationFactory(string connectionString);
    public EnvanexWebApplicationFactory(
        string connectionString,
        string environment,
        IReadOnlyDictionary<string, string?> settingOverrides);
}
```

The password satisfies `AddEnvanexIdentity`'s policy (12 characters, digit, lower, upper).
`EnsureUserAsync` goes through `UserManager`, so `IdentitySeedingScopeTests` stays green —
`IdentityRowSeeder` is not named anywhere new.

### Tests to add

`tests/Envanex.IntegrationTests/Api/AuthenticatedClientTests.cs` (**created, +4**)
- `SharedFactory_AdministratorClient_ShouldCarryABearerTokenInTheAdministratorRole`
- `RateLimitedFactory_AdministratorClient_ShouldLeaveBothGlobalPermitsUnspent` — attaches the
  cross-minted token, then proves two writes still succeed and the third 429s.
- `LoginRateLimitedFactory_AdministratorClient_ShouldLeaveBothLoginPermitsUnspent` — attaches the
  cross-minted token, then proves two logins still succeed against that host and the third 429s.
  This is the third factory's authenticated path.
- `AccessToken_AfterResetIdentityAsync_ShouldBeReissuedRatherThanReused` — takes a token, calls
  `ResetIdentityAsync`, takes a token again, asserts the two `jti` values differ. This is the test
  that protects the cache/reset alignment; delete the cache-clear line and it goes red.

### Validation

```powershell
cd C:\projects\envanex
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~Envanex.IntegrationTests.Api"
```

Expected test count at end: **576** (120 + 126 + **330**). **No schema, no migration, no production
query — db-reviewer not required.**

---

# Phase 3: The cookie scheme, the login page, sign-out, and revalidation

Decisions 4, 8, 9, 10, 11, 15. Assumes Spike outcomes **A1 or A2** and **B1**. Under **A3/B2** the
files below change as spelled out in Phase 0; the test count does not.

The gate is still open at the end of this phase, with one exception: `Home.razor` gets
`@attribute [Authorize]` (Decision 15), which works through `AuthorizeRouteView` without any
fallback policy.

### Files

- `src/Envanex.Web/Authentication/EnvanexAuthenticationSchemes.cs` — **created**.
- `src/Envanex.Web/Authentication/CookieSignInService.cs` — **created**.
- `src/Envanex.Web/Authentication/RevalidatingIdentityAuthenticationStateProvider.cs` — **created**.
- `src/Envanex.Web/Extensions/JwtAuthenticationExtensions.cs` — **modified** — renamed in intent,
  not in file name: it now registers the selector policy scheme, the cookie scheme and the bearer
  scheme together. `AddAuthentication` moves from naming `"Bearer"` as the default to naming the
  selector.
- `src/Envanex.Web/Program.cs` — **modified** — `AddCascadingAuthenticationState()`, the
  `AuthenticationStateProvider` registration, `CookieSignInService` registration, and (if Spike B
  showed GETs spending permits) the POST-only guard in the `"login"` policy delegate.
- `src/Envanex.Web/Components/Routes.razor` — **modified** — `RouteView` → `AuthorizeRouteView` with
  a `NotAuthorized` template carrying the Turkish "Bu sayfayı görüntüleme yetkiniz yok." and, for an
  anonymous user, a link to `/login`.
- `src/Envanex.Web/Components/_Imports.razor` — **modified** — `@using Microsoft.AspNetCore.Authorization`
  and `@using Microsoft.AspNetCore.Components.Authorization`.
- `src/Envanex.Web/Components/Pages/Login.razor` — **created** — `@page "/login"`,
  `@attribute [AllowAnonymous]`, `@attribute [EnableRateLimiting(AuthController.LoginRateLimitPolicy)]`.
  Plain markup and an `EditForm`; **no Radzen component appears on this page** — Radzen requires
  interactive rendering and this page must be statically server-rendered so `SignInAsync` can write
  `Set-Cookie`.
- `src/Envanex.Web/Components/Pages/SignOut.razor` — **created** — `@page "/sign-out"`, a statically
  rendered confirm form. Deliberately a page rather than a form embedded in `NavMenu`: a named form
  handler inside a layout component under static SSR is an unverified mechanism and this PR has
  spent its experiment budget on the two questions that were named.
- `src/Envanex.Web/Components/Layout/NavMenu.razor` — **modified** — an `<AuthorizeView>` showing the
  signed-in user's email and a link to `/sign-out`, and for an anonymous visitor a link to `/login`.
- `src/Envanex.Web/Components/Pages/Home.razor` — **modified** — `@attribute [Authorize]`.

### Signatures

```csharp
namespace Envanex.Web.Authentication;

public static class EnvanexAuthenticationSchemes
{
    public const string Selector = "Envanex";
    public const string Cookie   = "Envanex.Cookie";
}
```

```csharp
public sealed class CookieSignInService
{
    public CookieSignInService(
        IIdentityService identityService,
        UserManager<EnvanexUser> userManager,
        IUserClaimsPrincipalFactory<EnvanexUser> claimsPrincipalFactory);

    public Task<Result<AuthenticatedUser>> SignInAsync(
        HttpContext httpContext, string email, string password, CancellationToken ct = default);

    public Task SignOutAsync(HttpContext httpContext);
}
```

`Login.razor` injects `CookieSignInService` only. It never injects `EnvanexDbContext`,
`UserManager` or `IUserClaimsPrincipalFactory`. Credential checking goes through
`IIdentityService.ValidateCredentialsAsync`, so the Blazor door and the REST door share one
credential path: the same lockout, the same single `AuthErrors.InvalidCredentials`, the same
password-before-lockout ordering ADR 0007 fixed. A failed sign-in renders
`TurkishErrorMessages.GetMessage("Auth.InvalidCredentials", …)` — the same sentence, not a second
one.

`CookieSignInService.SignInAsync` resolves the `EnvanexUser` by id after
`ValidateCredentialsAsync` succeeds (one `FindByIdAsync`), builds the principal through the claims
factory — which supplies role claims and the security-stamp claim — and calls
`httpContext.SignInAsync(EnvanexAuthenticationSchemes.Cookie, principal)`.

Scheme registration, in `AddEnvanexJwtBearer`:

```csharp
services.AddAuthentication(options =>
    {
        options.DefaultScheme            = EnvanexAuthenticationSchemes.Selector;
        options.DefaultAuthenticateScheme = EnvanexAuthenticationSchemes.Selector;
        options.DefaultChallengeScheme    = EnvanexAuthenticationSchemes.Selector;
        options.DefaultForbidScheme       = EnvanexAuthenticationSchemes.Selector;
        options.DefaultSignInScheme       = EnvanexAuthenticationSchemes.Cookie;
        options.DefaultSignOutScheme      = EnvanexAuthenticationSchemes.Cookie;
    })
    .AddPolicyScheme(EnvanexAuthenticationSchemes.Selector, displayName: null, options =>
    {
        // Path prefix, never header presence. A browser holding a cookie and sending no
        // Authorization header to /api/* must get 401, not a 302 to the login page.
        options.ForwardDefaultSelector = context =>
            context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                ? JwtBearerDefaults.AuthenticationScheme
                : EnvanexAuthenticationSchemes.Cookie;
    })
    .AddCookie(EnvanexAuthenticationSchemes.Cookie, cookie => { /* LoginPath, ReturnUrlParameter, HttpOnly, SameSite=Lax, SecurePolicy=Always, SlidingExpiration */ })
    .AddJwtBearer(/* unchanged from Phase 1 */);
```

`cookie.LoginPath = "/login"`. `cookie.AccessDeniedPath` and
`CookieAuthenticationEvents.OnRedirectToAccessDenied` are **not** touched here — they belong to
Decision 3 and land in Phase 4 with the policies that can fail.

```csharp
public sealed class RevalidatingIdentityAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    protected override TimeSpan RevalidationInterval { get; } = TimeSpan.FromMinutes(30);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken);

    // Extracted so the stamp comparison is reachable from a test without a live circuit.
    internal Task<bool> ValidateSecurityStampAsync(ClaimsPrincipal principal, CancellationToken ct);
}
```

### Tests to add

`tests/Envanex.IntegrationTests/Blazor/LoginPageTests.cs` (**created, +5**)
- `GetLoginPage_ShouldReturn200CarryingAnAntiforgeryTokenField`
- `PostLoginForm_WithValidCredentials_ShouldSetTheAuthCookieAndRedirectToHome`
- `PostLoginForm_WithAnInvalidPassword_ShouldNotSetACookieAndShouldRenderTheTurkishCredentialMessage`
- `PostLoginForm_WithNoAntiforgeryToken_ShouldReturn400`
- `PostLoginForm_ForALockedOutAccount_ShouldRenderTheSameTurkishMessageAsAWrongPassword` — the
  Blazor twin of the ADR 0007 rule that a locked-out account is indistinguishable.

`tests/Envanex.IntegrationTests/Blazor/SignOutPageTests.cs` (**created, +2**)
- `PostSignOutForm_WhileSignedIn_ShouldClearTheAuthCookieAndRedirectToTheLoginPage`
- `GetSignOutPage_WhileSignedOut_ShouldRedirectToTheLoginPage`

`tests/Envanex.IntegrationTests/Blazor/AuthenticatedShellTests.cs` (**created, +2**)
- `GetHome_WhileSignedIn_ShouldRenderTheSignedInUsersEmailInTheNavMenu`
- `GetHome_WhileSignedOut_ShouldRedirectToTheLoginPage`

`tests/Envanex.IntegrationTests/Blazor/SecurityStampRevalidationTests.cs` (**created, +2**)
- `AuthenticationStateProvider_ShouldBeTheRevalidatingIdentityProvider`
- `ValidateSecurityStampAsync_AfterTheUsersSecurityStampChanges_ShouldReturnFalse` — changes the
  stamp through `UserManager.UpdateSecurityStampAsync` and asserts the previously-issued principal
  stops validating. Without this, Decision 11 is an unproved claim.

`tests/Envanex.IntegrationTests/Api/SchemeSelectionTests.cs` (**created, +2**)
- `ForwardDefaultSelector_ForAnApiPath_ShouldSelectTheBearerScheme`
- `ForwardDefaultSelector_ForANonApiPath_ShouldSelectTheCookieScheme` — both invoke the selector
  from `IOptionsMonitor<PolicySchemeOptions>` against a `DefaultHttpContext`. The end-to-end proof
  that `/api/*` never redirects is in Phase 4, where a challenge is first reachable.

`tests/Envanex.IntegrationTests/Api/LoginRateLimiterTests.cs` (**+2**, Decision 9)
- `BlazorLoginForm_ExceedingTheLoginRateLimit_ShouldReturn429`
- `BlazorLoginPage_RepeatedGets_ShouldNotSpendLoginPermits` — **drop this case and the guard it
  covers if Spike B showed GETs do not consume permits**; the phase total then becomes 344/590.

### Validation

```powershell
cd C:\projects\envanex
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~Envanex.IntegrationTests.Blazor"
dotnet run --project src\Envanex.Web   # manual: sign in at /login, see the email in NavMenu, sign out
```

The manual step closes the roadmap's known gap "the manual smoke steps in the PR 6a plan that
require a signed-in user were never run".

Expected test count at end: **591** (120 + 126 + **345**), or 590 under the Spike B sub-outcome.

**Run db-reviewer on this phase** — one new production read (`FindByIdAsync` per sign-in).

---

# Phase 4: Close the gate

Decisions 1, 2, 3, 4, 13, 14. This is the phase that changes behaviour for every caller. The 72
tests already authenticate (Phase 2) and the login page already exists (Phase 3), so nothing in this
phase repairs a break it caused.

### Files

- `src/Envanex.Web/Authorization/EnvanexPolicies.cs` — **created** — `CanRead`, `CanWrite`.
- `src/Envanex.Web/Program.cs` — **modified** — `AddAuthorization(options => { … })` at line 28
  gains the fallback policy and the two named policies;
  `app.MapStaticAssets().AllowAnonymous();`, `app.MapOpenApi().AllowAnonymous();`,
  `app.MapScalarApiReference().AllowAnonymous();`. The comment block at lines 130–138 is rewritten:
  the bodiless-challenge argument it makes has now expired and the events that replace it must be
  named there.
- `src/Envanex.Web/Authentication/EnvanexAuthenticationEvents.cs` — **created** —
  `JwtBearerEvents.OnChallenge` / `OnForbidden` writing `application/problem+json`, and
  `CookieAuthenticationEvents.OnRedirectToAccessDenied` writing a 403 with a Turkish HTML body.
  Writing the body starts the response, which is what stops `UseStatusCodePagesWithReExecute` from
  re-executing it as `/not-found`. `OnRedirectToLogin` keeps the framework's 302 — that is the
  correct answer on a non-API path and Decision 4 only forbids it on `/api/*`.
- `src/Envanex.Web/Extensions/JwtAuthenticationExtensions.cs` — **modified** — wires the events;
  the PR 6a comment at lines 28–29 is deleted, its promise now kept.
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — **modified** — `GetReasonPhrase` gains
  `StatusCodes.Status403Forbidden => "Forbidden"` and changes from `private` to `internal` so the
  events class builds its ProblemDetails through the same function rather than a copy. No entry is
  added to `StatusCodeMap`, so `ResultMappingTests`'s fourth fact (no orphaned mapping entry) stays
  green — exactly as Decision 3 predicts.
- `src/Envanex.Web/Controllers/AuthController.cs` — **modified** — `[AllowAnonymous]` on the class,
  with a comment naming Decision 14 for `Logout`.
- `src/Envanex.Web/Controllers/ProductsController.cs` — **modified** — `[Authorize(Policy = EnvanexPolicies.CanRead)]`
  on `GetById` and `GetDataSource`; `[Authorize(Policy = EnvanexPolicies.CanWrite)]` on `Create`,
  `Update`, `Activate`, `Deactivate`.
- `src/Envanex.Web/Controllers/UnitOfMeasuresController.cs` — **modified** —
  `CanRead` on `GetById` and `List`; `CanWrite` on `Create`.
- `src/Envanex.Web/Components/Pages/NotFound.razor` — **modified** — `@attribute [AllowAnonymous]`.
- `src/Envanex.Web/Components/Pages/Error.razor` — **modified** — `@attribute [AllowAnonymous]`.
- `src/Envanex.Web/Components/Pages/Login.razor` — already `[AllowAnonymous]` from Phase 3.

### Signatures

```csharp
namespace Envanex.Web.Authorization;

public static class EnvanexPolicies
{
    public const string CanRead  = "CanRead";
    public const string CanWrite = "CanWrite";
}
```

```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(EnvanexPolicies.CanRead,  policy => policy.RequireRole(EnvanexRoles.Administrator, EnvanexRoles.Viewer));
    options.AddPolicy(EnvanexPolicies.CanWrite, policy => policy.RequireRole(EnvanexRoles.Administrator));
});
```

`DefaultPolicy` is deliberately left alone: `[Authorize]` with no policy (on `Home.razor`) must mean
"signed in", not "has a role", or an anonymous visitor's landing experience changes shape.

### The complete `[AllowAnonymous]` exemption list

| # | Exemption | Mechanism | Proving test |
|---|---|---|---|
| 1 | `POST /api/auth/login` | `[AllowAnonymous]` on `AuthController` | `Login_WithoutAuthentication_ShouldReturn200` |
| 2 | `POST /api/auth/refresh` | same attribute | `Refresh_WithoutAuthentication_ShouldReturn401InvalidRefreshTokenAndNoWwwAuthenticateHeader` |
| 3 | `POST /api/auth/logout` (Decision 14) | same attribute | `Logout_WithoutAuthentication_ShouldReturn204` |
| 4 | `/not-found` | `@attribute [AllowAnonymous]` | `NotFoundPage_WithoutAuthentication_ShouldReturn200` |
| 5 | the 404 re-execution path | consequence of #4 | `UnknownPath_WithoutAuthentication_ShouldReturn404AndNotRedirectToLogin` |
| 6 | `/Error` | `@attribute [AllowAnonymous]` | `ErrorPage_WithoutAuthentication_ShouldReturn200` |
| 7 | `/login` (**extends Decision 1's enumeration**; required by Decision 15) | `@attribute [AllowAnonymous]` | `LoginPage_WithoutAuthentication_ShouldReturn200` |
| 8 | static assets | `app.MapStaticAssets().AllowAnonymous()` | `StaticAsset_WithoutAuthentication_ShouldReturn200` (`GET /favicon.png`, referenced unfingerprinted in `App.razor`) |
| 9 | `/openapi/v1.json`, Development only | `app.MapOpenApi().AllowAnonymous()` | `OpenApiDocument_InDevelopment_WithoutAuthentication_ShouldReturn200` |
| 10 | Scalar reference, Development only | `app.MapScalarApiReference().AllowAnonymous()` | `ScalarReference_InDevelopment_WithoutAuthentication_ShouldReturn200` |

Rows 9 and 10 need a host running as `Development`; that is the second constructor added to
`EnvanexWebApplicationFactory` in Phase 2, not a fourth factory type.

### Tests to add and change

`tests/Envanex.IntegrationTests/Api/AuthPipelineTests.cs` — **modified, 5 cases → 6** (**+1**).
Four of the five existing cases change; this is the correction to the research's "72", which did not
count them.
- `Refresh_WithUnknownToken_ShouldReturn401ProblemJsonAndNotTheNotFoundPage` — **unchanged**.
- `OpenEndpoint_WithNoAuthorizationHeader_ShouldStillReturn200` → renamed and inverted to
  `ProtectedEndpoint_WithNoAuthorizationHeader_ShouldReturn401ProblemJsonAndNotTheNotFoundPage`.
- `OpenEndpoint_WithAMalformedBearerToken_ShouldStillReturn200` → renamed and inverted to
  `ProtectedEndpoint_WithAMalformedBearerToken_ShouldReturn401ProblemJson`.
- `OpenEndpoint_WithAnExpiredBearerToken_ShouldStillReturn200` → renamed and inverted to
  `ProtectedEndpoint_WithAnExpiredBearerToken_ShouldReturn401ProblemJson`.
- `OpenEndpoint_WithAValidBearerToken_ShouldStillReturn200` → becomes
  **`ProtectedEndpoint_WithAValidBearerTokenCarryingNoRole_ShouldReturn403ProblemJsonAndNotTheNotFoundPage`**
  — the token the class hand-builds carries no role claim, so it authenticates and then fails
  `CanRead`. **This is the bearer half of Decision 3.**
- **added:** `ProtectedEndpoint_WithAValidBearerTokenCarryingTheAdministratorRole_ShouldReturn200` —
  the positive control. The private `CreateToken(bool expired)` helper gains a
  `params string[] roles` parameter.
- The class doc comment is rewritten: it no longer says "no endpoint is `[Authorize]`".

`tests/Envanex.IntegrationTests/Api/CookieAuthPipelineTests.cs` (**created, +3**)
- **`Cookie_AuthenticatedUserWithNoRole_ShouldReturn403WithABodyAndNotTheNotFoundPage`** — signs in
  through the login form as `SqlServerFixture.RoleLessEmail`, requests `/`, and asserts 403, a
  non-empty body, and that the body is not the not-found page. **This is the cookie half of
  Decision 3.**
- `Cookie_AnonymousRequestToAProtectedPage_ShouldRedirectToTheLoginPage`
- **`ApiPath_WithASessionCookieAndNoBearerToken_ShouldReturn401AndNotARedirect`** — Decision 4's
  end-to-end proof: the exact case a header-presence selector would have got wrong.

`tests/Envanex.IntegrationTests/Api/AnonymousExemptionTests.cs` (**created, +8**) — rows 1–8 of the
table above, method names as listed there.

`tests/Envanex.IntegrationTests/Api/DevelopmentEndpointExemptionTests.cs` (**created, +3**)
- `OpenApiDocument_InDevelopment_WithoutAuthentication_ShouldReturn200`
- `ScalarReference_InDevelopment_WithoutAuthentication_ShouldReturn200`
- `ProtectedEndpoint_InDevelopment_WithoutAuthentication_ShouldReturn401` — so a Development host
  cannot quietly become an open one.

`tests/Envanex.IntegrationTests/Api/AuthorizationPolicyTests.cs` (**created, +6**)
- `CanRead_WithAnAdministratorToken_ShouldReturn200`
- `CanRead_WithAViewerToken_ShouldReturn200`
- `CanWrite_WithAnAdministratorToken_ShouldReturn201`
- **`CanWrite_WithAViewerToken_ShouldReturn403`** — Decision 13 over HTTP: read-only is a role, and
  this is what makes it true.
- `FallbackPolicy_ShouldRequireAnAuthenticatedUser` — reads `IOptions<AuthorizationOptions>` and
  asserts `FallbackPolicy` is non-null and carries `DenyAnonymousAuthorizationRequirement`. Delete
  the fallback line in `Program.cs` and this goes red before any endpoint test does.
- `Datasource_WithoutAuthentication_ShouldReturn401` — `/api/products/datasource` is the widest read
  surface in the app and deserves a named closure test.

`tests/Envanex.IntegrationTests/Api/ResultExtensionsTests.cs` (**+1**)
- `GetReasonPhrase_ForForbidden_ShouldBeForbiddenRatherThanError`

`tests/Envanex.IntegrationTests/Api/LoginRateLimiterTests.cs` —
`ProductsEndpoint_ShouldNotBeAffectedByTheLoginPolicy` asserts `ShouldNotBe(429)` and is unaffected
by the gate (its responses move from 404 to 401). **No change.**

### Validation

```powershell
cd C:\projects\envanex
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~AuthPipelineTests|FullyQualifiedName~CookieAuthPipelineTests|FullyQualifiedName~AnonymousExemptionTests|FullyQualifiedName~AuthorizationPolicyTests"
dotnet run --project src\Envanex.Web   # manual: /api/products/datasource in a browser answers 401 JSON, not the login page
```

Expected test count at end: **613** (120 + 126 + **367**).
**No schema, no migration, no query — db-reviewer not required for this phase.**

---

# Phase 5: The read-only demo account

Decision 7 and Decision 12.

### Files

- `src/Envanex.Infrastructure/Identity/DemoAccountOptions.cs` — **created**.
- `src/Envanex.Infrastructure/Identity/DemoAccountSeeder.cs` — **created** — idempotent; ensures the
  two roles through `IdentityRoleSeeder`, then the demo user, then its `Viewer` membership.
- `src/Envanex.Web/Extensions/IdentitySeedingExtensions.cs` — **created** — one call site, invoked
  from `Program.cs` between `app.Build()` and `app.Run()`.
- `src/Envanex.Web/Program.cs` — **modified** — `await app.SeedIdentityAsync();`.
- `src/Envanex.Web/appsettings.json` — **modified** — a `Demo` block with `"Enabled": false` and
  `"Email": "demo@envanex.local"` and `"Password": ""`. **The password is never committed**; it
  follows the JWT signing key's path — user-secrets locally, App Service configuration in PR 7.

### Signatures

```csharp
namespace Envanex.Infrastructure.Identity;

public sealed class DemoAccountOptions
{
    public const string SectionName = "Demo";
    public bool Enabled { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public static class DemoAccountSeeder
{
    // Returns Result: an already-existing demo account is the expected steady state, not a failure.
    // A null services reference is a caller bug and throws.
    public static Task<Result> SeedAsync(IServiceProvider services, CancellationToken ct = default);
}
```

```csharp
namespace Envanex.Web.Extensions;

public static class IdentitySeedingExtensions
{
    public static Task SeedIdentityAsync(this WebApplication app);
}
```

Roles are seeded **unconditionally** — they are structural, and a production host with
`Demo:Enabled=false` and no roles could never grant anyone a permission. The demo **user** is seeded
only when `Demo:Enabled` is true. `Demo:Enabled=true` with a blank or policy-violating password
throws at startup, in the same spirit as `JwtOptionsGuard`: a misconfigured public demo must fail at
boot, not hand out an account nobody chose the password for.

### Tests to add

`tests/Envanex.IntegrationTests/Identity/DemoAccountSeederTests.cs` (**created, +5**)
- `SeedAsync_WhenDemoIsDisabled_ShouldSeedTheRolesButNotTheAccount`
- `SeedAsync_WhenDemoIsEnabled_ShouldCreateTheAccountInTheViewerRole`
- `SeedAsync_CalledTwice_ShouldSucceedAndShouldNotDuplicateTheAccount`
- `SeedAsync_WhenDemoIsEnabledWithABlankPassword_ShouldThrowAtStartup`
- **`DemoAccount_ShouldBeAbleToReadButNotWrite`** — end to end over HTTP against a
  `Demo:Enabled=true` host: `POST /api/auth/login` with the configured password → 200,
  `GET /api/unit-of-measures` → 200, `POST /api/unit-of-measures` → 403.

`tests/Envanex.IntegrationTests/Configuration/DemoAppSettingsTests.cs` (**created, +2**) — mirrors
`JwtAppSettingsTests` against the shipped `src/Envanex.Web/appsettings.json`
- `AppSettings_DemoEnabled_ShouldDefaultToFalse`
- `AppSettings_DemoPassword_ShouldBeEmptySoThatNoSecretIsCommitted`

### Validation

```powershell
cd C:\projects\envanex
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
dotnet user-secrets set "Demo:Enabled" "true" --project src\Envanex.Web
dotnet user-secrets set "Demo:Password" "<a local value, never committed>" --project src\Envanex.Web
dotnet run --project src\Envanex.Web   # manual: sign in at /login as the demo account, confirm read-only
```

Expected test count at end: **620** (120 + 126 + **374**).

**Run db-reviewer on this phase** — the seeder writes `auth.AspNetRoles`, `auth.AspNetUsers` and
`auth.AspNetUserRoles` at host startup, in production.

---

## Expected test counts, by phase

| Phase | Domain | Application | Integration | Total |
|---|---|---|---|---|
| baseline | 120 | 126 | 316 | 562 |
| 0 spikes | 120 | 126 | 316 | 562 |
| 1 role claim | 120 | 126 | 326 | **572** |
| 2 authenticated clients | 120 | 126 | 330 | **576** |
| 3 cookie + Blazor | 120 | 126 | 345 | **591** (590 under the Spike B sub-outcome) |
| 4 close the gate | 120 | 126 | 367 | **613** |
| 5 demo account | 120 | 126 | 374 | **620** |

Watch the integration suite's wall clock. ADR 0007 set 60 seconds as the point where it becomes a
decision. The lazy per-collection token cache is what keeps this PR's addition to a handful of
PBKDF2 pairs; if the suite crosses 60 s, the cause to check first is a class calling
`ResetIdentityAsync` more often than it needs to.

## Rollback notes

- Every phase is a separate commit on `feat/authz`; `git revert` of any single one leaves the
  solution building and green, because no phase depends on a later one to repair a test it broke.
- The single riskiest revert is Phase 4: reverting it reopens every endpoint. Phases 1–3 are all
  additive and enforce nothing, which is why the gate is last but one.
- **No migration is created, so there is nothing to roll back in the database.** Reverting Phase 5
  leaves the seeded roles and demo user in place; they are harmless once no policy names them, and
  the test database is ephemeral.
- If Spike B lands on outcome B2, Phase 3 is the reshaped variant and Phase 4 is unchanged. Do not
  start Phase 3 before the spike note exists.

## Known gaps this PR opens, for the roadmap table

- `/api/*` is bearer-only by construction (Decision 4). A browser holding a session cookie cannot
  call the REST surface, including `/api/products/datasource`. Closes in PR 7 only if the grid ever
  needs a browser-side data call; the Blazor grid calls the Application layer directly, so it does
  not today.
- The roadmap row "No `JwtBearerEvents.OnChallenge` body" closes in Phase 4 and should be struck.
- The roadmap row "No authentication or authorization on any endpoint" closes in Phase 4.
- The roadmap row about PR 6a's unrun manual smoke steps closes in Phase 3/Phase 5.
- `Demo:Password` joins `Jwt:SigningKey` as a value the first deploy must set in App Service
  configuration — add it to the PR 7 deployment row.

---

# Disagreement with the fifteen decisions

Planned as written above regardless. Three items, and two corrections that are not disagreements.

## Disagreement 1 — Decision 4's path-prefix forwarding is stricter than Decision 4's own sentence

Decision 4 says "API paths answer 401 regardless of **which scheme authenticated them**", which
reads as though both schemes may authenticate an API request and only the *challenge* is pinned to
401. Path-prefix forwarding to a single scheme is stricter: on `/api/*` the cookie is not read at
all, so a signed-in browser is simply anonymous there. I would rather forward `/api/*` to a
multi-scheme policy — `new AuthorizationPolicyBuilder(Cookie, Bearer)` — and pin the *challenge and
forbid* to the bearer handler, which delivers the stated guarantee (never a 302 on `/api/*`) while
keeping a browser session able to call the API. That matters for the roadmap's stated reason for
`DevExtreme.AspNet.Data` existing at all: "so the same data protocol works for a DevExpress client".
Under the plan as written, that client must hold a bearer token, which reopens the browser-token
question the cookie scheme was chosen to avoid. The cost of my alternative is that
`HttpContext.User` no longer answers which door opened it — the external findings' stated cost, and
a cheaper one than an unreachable API surface.

## Disagreement 2 — Decision 3's cookie-side 403 puts a Turkish user-facing string in C#

Requiring a body-carrying 403 from `OnRedirectToAccessDenied` means the access-denied page for a
browser is a string literal in an events class, not a Razor page — the one place in this codebase
where user-facing markup escapes `.razor`. `AccessDeniedPath` to a Razor page is the shape the
framework offers and gives a real page, at the cost of a 302 instead of a 403. I think the honest
resolution is a 403 that *re-executes* an `/access-denied` component, which gives both, but it is
more machinery than this PR should carry. Planned as written; I would revisit it in PR 7 when the
UI gets its second screen.

## Disagreement 3 — Decision 6, as implemented, is "one real login per class", not "per test"

Decision 6 says the 72 tests authenticate by logging in for real, and the plan honours that: every
token comes from a real `POST /api/auth/login` through the real endpoint with real PBKDF2 and full
issuer/audience/lifetime/signature validation on use. But the token is cached on the fixture, so 37
`ProductsDatasourceTests` share one login rather than performing 37. If the human's intent was
literally one login per test, say so and the cache comes out — the cost is roughly 14 seconds added
to a 44-second suite, which crosses the 60-second line ADR 0007 set as a decision point. I think the
cache is right and the cache-invalidation test
(`AuthenticatedClientTests.AccessToken_AfterResetIdentityAsync_ShouldBeReissuedRatherThanReused`)
is what makes it safe, but the deviation should be a choice rather than something noticed in review.

## Correction 1 — the blast radius is 76 executed tests, not 72 (not a disagreement)

The research's table lists five classes totalling 72. It does not count
`tests/Envanex.IntegrationTests/Api/AuthPipelineTests.cs`, four of whose five cases assert
`GET /api/unit-of-measures` returns 200 with no credentials, a malformed token, an expired token and
a valid token respectively. All four change meaning when the gate closes. They are handled in Phase
4 rather than Phase 2 because they are the gate's own guard rather than collateral — but the human
should know the number is 76.

## Correction 2 — Decision 9's mechanism rate-limits GETs of the login page (not a disagreement)

A Blazor page endpoint serves GET and POST. `[EnableRateLimiting("login")]` on `Login.razor`
therefore spends a login permit every time the page is *rendered*, so five reloads would lock login
out for five minutes in production (5 per 300 s). The plan adds a method guard inside the existing
`"login"` policy delegate so the limit applies to POST only; this keeps Decision 9's "same named
policy" intact and leaves `AuthController.Login`, which is POST-only, unaffected. Spike B confirms
whether the guard is needed before Phase 3 is written.

## Correction 3 — Decision 1's exemption list needs one more entry (not a disagreement)

The enumerated list is "the three auth endpoints, `/not-found`, the error page, static assets, and
the Development-only OpenApi and Scalar endpoints". `/login` must be added, or Decision 15's "an
anonymous visitor sees the login page" cannot hold. The plan adds it and names its test.
