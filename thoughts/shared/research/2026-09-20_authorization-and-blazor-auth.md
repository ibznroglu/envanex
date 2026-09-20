# Research: Authorization, the Blazor cookie scheme, and the demo account (PR 6b)

## Provenance

The `researcher` subagent mapped areas 1 to 10 below. Four of its findings were incomplete or
unverified and were corrected against the working tree and the dev database: the factory
enumeration was missing one of three factories, the count of affected tests was stated by attribute
rather than by executed case, the emptiness of `auth.AspNetRoles` was assumed rather than queried,
and one antiforgery claim named an API that does not exist and was discarded. The external findings
and the decisions section were assembled with the human in a separate session.

The decisions section records choices the human made and approved. It is a constraint on the
planner, not a proposal.

## Question

PR 6a established who a caller is. PR 6b has to establish what a caller may do, across two
different front doors — a REST surface authenticated by bearer token and a Blazor Server UI
authenticated by cookie — without either door being more permissive than the other, and without
the 562-test suite silently losing its coverage the moment endpoints close.

## Scope

In scope: authorization policies, closing every endpoint, the Blazor cookie scheme and the minimum
UI that makes it usable, the read-only demo account, and the `OnChallenge` and `OnForbidden`
response bodies.

Out of scope: password change, password reset, self-registration, email confirmation, two-factor,
passkeys, and any change to how refresh token rotation works.

## Baseline

Taken on `feat/authz` at `83f6cef`, before any code exists.

| Suite | Tests | Duration |
|---|---|---|
| `Envanex.Domain.Tests` | 120 | ~0.2 s |
| `Envanex.Application.Tests` | 126 | ~0.2 s |
| `Envanex.IntegrationTests` | 316 | ~44 s |

## Relevant files

**Web host and auth wiring**
- `src/Envanex.Web/Program.cs` — the pipeline; `AddAuthorization()` at line 28 takes no arguments
- `src/Envanex.Web/Extensions/JwtAuthenticationExtensions.cs` — the bearer scheme, lines 15–50
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — the status map and `GetReasonPhrase`
- `src/Envanex.Web/RateLimiting/LoginRateLimitPartition.cs` — the login policy's partition key

**Controllers**
- `src/Envanex.Web/Controllers/AuthController.cs`
- `src/Envanex.Web/Controllers/ProductsController.cs`
- `src/Envanex.Web/Controllers/UnitOfMeasuresController.cs`

**Blazor**
- `src/Envanex.Web/Components/App.razor`, `Routes.razor`, `_Imports.razor`
- `src/Envanex.Web/Components/Layout/MainLayout.razor`, `NavMenu.razor`
- `src/Envanex.Web/Components/Pages/Home.razor`, `Error.razor`, `NotFound.razor`

**Identity**
- `src/Envanex.Infrastructure/Identity/EnvanexUser.cs`
- `src/Envanex.Infrastructure/Identity/EnvanexIdentityDbContext.cs`
- `src/Envanex.Infrastructure/Identity/IdentityInfrastructureExtensions.cs`
- `src/Envanex.Infrastructure/Identity/JwtAccessTokenIssuer.cs`

**Tests that constrain this PR**
- `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs`
- `tests/Envanex.IntegrationTests/Fixtures/RateLimitedWebApplicationFactory.cs`
- `tests/Envanex.IntegrationTests/Fixtures/LoginRateLimitedWebApplicationFactory.cs`
- `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs`
- `tests/Envanex.IntegrationTests/Fixtures/IdentitySeeder.cs`, `IdentityRowSeeder.cs`

## Current behavior

### 1. The pipeline

Service registrations: `AddRazorComponents().AddInteractiveServerComponents()` (17–18),
`AddControllers` with the datasource model binder (20–23), `AddApplication()` (25),
`AddInfrastructure(configuration)` (26), `AddEnvanexJwtBearer(configuration)` (27),
**`AddAuthorization()` with no arguments** (28), and the rate limiter (30–98).

Middleware, in order: exception handler and HSTS outside Development (110–112), OpenApi and Scalar
in Development only (115–119), `SecurityHeadersMiddleware` (121), `UseRateLimiter()` (125),
`UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true)` (127),
`UseHttpsRedirection()` (128), `UseAuthentication()` (139), `UseAuthorization()` (140),
`UseAntiforgery()` (142), `MapStaticAssets()` (144), `MapControllers()` (145),
`MapRazorComponents<App>().AddInteractiveServerRenderMode()` (146–147).

Authentication and authorization sit **inside** the `UseStatusCodePagesWithReExecute` wrapper. PR
6a argued this was safe because every reachable 401 carried a ProblemDetails body. That argument
expires in this PR: the first `[Authorize]` makes a bodiless challenge reachable, and a bodiless
4xx is exactly what the wrapper re-executes as the not-found page.

### 2. The bearer scheme

`AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` (line 30) sets both
`DefaultAuthenticateScheme` and `DefaultChallengeScheme` to `"Bearer"`. Validation is fully on:
issuer, audience, lifetime and signing key, HMAC-SHA256, with `ClockSkew = TimeSpan.Zero` (45).
`RequireHttpsMetadata` is not set and takes the framework default.

**No `JwtBearerEvents` handler of any kind exists.** Lines 28–29 carry a comment saying so, and
saying it ships with the first `[Authorize]` in PR 6b. `OnChallenge`, `OnForbidden`,
`OnAuthenticationFailed`, `OnMessageReceived` and `OnTokenValidated` all do not exist.

Today an unauthenticated request authenticates to an anonymous principal and authorization has no
policy to deny anything, so no 401 is ever produced by the pipeline. The only 401s come from
`ResultExtensions` when a use case returns an `Auth.*` error.

### 3. Every endpoint

Twelve, across three controllers.

| Controller | Action | Verb | Route | Kind |
|---|---|---|---|---|
| Auth | Login | POST | `/api/auth/login` | anonymous, rate-limited |
| Auth | Refresh | POST | `/api/auth/refresh` | anonymous |
| Auth | Logout | POST | `/api/auth/logout` | anonymous |
| Products | GetById | GET | `/api/products/{id}` | read |
| Products | GetDataSource | GET | `/api/products/datasource` | read |
| Products | Create | POST | `/api/products` | write |
| Products | Update | PUT | `/api/products/{id}` | write |
| Products | Activate | POST | `/api/products/{id}/activate` | write |
| Products | Deactivate | POST | `/api/products/{id}/deactivate` | write |
| UnitOfMeasures | GetById | GET | `/api/unit-of-measures/{id}` | read |
| UnitOfMeasures | List | GET | `/api/unit-of-measures` | read |
| UnitOfMeasures | Create | POST | `/api/unit-of-measures` | write |

Five reads, four non-auth writes, three auth endpoints that must stay anonymous. OpenApi and
Scalar are mapped in Development only. There is no SOAP mount and no health endpoint.

### 4. How the tests get a client

**Three factories, not two.**

- `EnvanexWebApplicationFactory` — the shared one, held by `SqlServerFixture` for the whole
  collection. Sets `Testing`, the connection string, `RateLimiting:Enabled=false`,
  `RateLimiting:Login:Enabled=false`, and the full `Jwt` section with `TestSigningKey`.
- `RateLimitedWebApplicationFactory` — global limiter **on** at 2 per 60 s, login policy off.
- `LoginRateLimitedWebApplicationFactory` — the inverse: global limiter off, login policy **on** at
  2 per 60 s. Constructed per test in `LoginRateLimiterTests.InitializeAsync` (line 41), not per
  class, because the 2-permit budget is per factory.

Eight test classes call `CreateClient`. Only `AuthPipelineTests` ever attaches credentials, and it
does so by hand — three `AuthenticationHeaderValue("Bearer", token)` assignments. There is no
`TestAuthHandler`, no shared authenticated-client helper and no cookie container anywhere.

**Seventy-two tests start failing the moment endpoints close.** Counted by execution, not by
attribute:

| Class | Tests |
|---|---|
| `ProductsDatasourceTests` | 37 |
| `ProductsApiTests` | 20 |
| `UnitOfMeasuresApiTests` | 8 |
| `RateLimiterTests` | 5 |
| `ConcurrencyApiTests` | 2 |
| **Total** | **72** |

`RateLimiterTests` is in the list because it drives the limiter through
`POST /api/unit-of-measures`. `ProductsDatasourceTests` alone is more than half the blast radius,
and most of its count is `InlineData` expansion, which is why an attribute count understates it.

### 5. The Blazor surface

`App.razor` renders `<Routes />` at line 18. `Routes.razor` line 1 declares the router with
`NotFoundPage="typeof(Pages.NotFound)"`, and its `<Found>` uses **`RouteView`**, a default layout
and `FocusOnNavigate`. There is no `<Authorized>`, `<Authenticating>` or `<NotAuthorized>`.

`NavMenu.razor` carries a single Home link. `Home.razor` is a heading. `Error.razor` takes
`HttpContext` as a cascading parameter. `NotFound.razor` is mapped at `/not-found`.

All of the following do not exist anywhere: `AuthenticationStateProvider`,
`CascadingAuthenticationState`, `AddCascadingAuthenticationState()`, `AuthorizeRouteView`,
`AuthorizeView`, and `@attribute [Authorize]` in any `.razor` file. `_Imports.razor` has no
authentication usings.

### 6. Antiforgery

`app.UseAntiforgery()` at line 142, after authentication and authorization and before every
endpoint mapping. There is **no explicit `AddAntiforgery` call anywhere in the repository** — a
repo-wide search returns zero matches, so nothing here configures antiforgery options. Which
registration supplies the antiforgery services, and with what defaults, is framework behaviour that
cannot be established from this repository; it is recorded under "Not verified" rather than
asserted here.

There is no `<EditForm>`, no `<form>`, no `<input>` and no `[SupplyParameterFromForm]` in the
codebase. Nothing posts a form today.

One behaviour is recorded but not asserted: a comment in `AuthApiTests` notes that an unmatched
`POST /api/*` falls through to the Blazor catch-all and is rejected by antiforgery with 400 before
routing reports a miss. The test beside it probes with GET for that reason.

### 7. The Identity surface

`EnvanexUser : IdentityUser<Guid>` declares no property of its own.
`EnvanexIdentityDbContext : IdentityDbContext<EnvanexUser, IdentityRole<Guid>, Guid>` — the role
type parameter is already there. `HasDefaultSchema("auth")`, a unique filtered index on
`NormalizedEmail`, and the refresh token configuration.

**`AddRoles<IdentityRole<Guid>>()` is already called** (`IdentityInfrastructureExtensions.cs:52`).
`EnvanexRole` does not exist; the stock `IdentityRole<Guid>` is used. The migration creates all
seven Identity tables including `auth.AspNetRoles`.

Verified by query against the dev database: `auth.AspNetRoles`, `AspNetUserRoles`,
`AspNetRoleClaims`, `AspNetUserClaims`, `AspNetUsers` and `RefreshTokens` all hold zero rows. A
repository-wide search finds no `RoleManager` injection, no `AddToRoleAsync` and no code anywhere
that creates a role or assigns one. **Role storage and role services exist; nothing has ever
written a role.**

**The access token carries no role claim.** `JwtAccessTokenIssuer` lines 49–56 write exactly three:
`sub`, `email`, `jti`. The descriptor otherwise sets only issuer, audience, issued-at, not-before,
expiry and signing credentials. There is no `RoleClaimType` configuration on the validation
parameters either.

### 8. The seeders

`IdentitySeeder` (test project) creates users through `UserManager` from a service scope, checks
the returned `IdentityResult` and throws on failure. It also exposes lockout helpers. It depends
only on public shared-framework API, so its shape is reusable; its location is not.

`IdentityRowSeeder` writes one bare `auth.AspNetUsers` row directly through the context with
`PasswordHash` left null, to satisfy foreign keys in schema tests. It depends on `SqlServerFixture`
and is bounded by a test that fails if any file outside the schema tests names it.

**The integration tests do not use the dev container.** `SqlServerFixture` builds its own
Testcontainers `MsSqlContainer` per run and migrates both chains at `InitializeAsync`.
`ResetIdentityAsync` deletes every row in all seven auth tables between tests. A seeder that runs
at host startup therefore also runs inside every test host, against that ephemeral database, and
whatever it seeds is deleted between tests.

### 9. The error surface

Eight `Auth.*` codes: four session codes mapped to 401, four validation codes mapped to 400. The
whole status map produces 400, 401, 404, 409 and 422.

**Nothing maps to 403.** `GetReasonPhrase` (lines 189–197) handles 400, 401, 404, 409 and 422 and
falls through to `"Error"` for anything else, so a 403 produced today would carry the title
`"Error"`.

`ResultMappingTests` scans `Envanex.Domain` and `Envanex.Application` and holds four facts: the
scan reaches Application, every code has a status mapping, every code has a Turkish message, and no
mapping entry is orphaned.

### 10. Configuration

`appsettings.json` carries `ConnectionStrings:EnvanexDb` (empty), the `RateLimiting` block with
global 100/60 s and login 5/300 s, and the `Jwt` block with an empty `SigningKey` and 15 / 7 / 30.
`appsettings.Development.json` carries only logging.

`JwtOptionsGuard.ThrowIfInvalid` rejects a blank issuer, audience or signing key, a key under 32
UTF-8 bytes, non-positive lifetimes, and an idle window longer than the absolute cap. It is called
from both `AddEnvanexIdentity` and `AddEnvanexJwtBearer`.

All three factories override configuration through `UseSetting` under `UseEnvironment("Testing")`.

## External findings

### Two authentication schemes in one application

Two shapes are available. A multi-scheme authorization policy built with
`AuthorizationPolicyBuilder(cookieScheme, bearerScheme)` makes the framework try both and merge the
result into a single principal on `HttpContext.User`; the cost is that the request no longer
carries an easy answer to which scheme authenticated it. Alternatively a policy scheme registered
with `AddPolicyScheme` and a `ForwardDefaultSelector` chooses a scheme per request, typically by
inspecting whether an `Authorization` header is present. A third, narrower option is naming schemes
on individual `[Authorize]` attributes.

Header presence is the common key for that selector and is rejected here. A browser holding a
session cookie and sending no `Authorization` header to an `/api/*` path would be forwarded to the
cookie scheme and answered with a 302 to the login page, which is exactly what Decision 4 forbids.
Selection keys on the path prefix instead.

The current registration names `"Bearer"` as both the default authenticate and default challenge
scheme, so adding a cookie scheme forces an explicit decision about what the defaults become rather
than leaving it implicit.

### Signing in from Blazor requires static server rendering

`HttpContext` is null during interactive rendering and Blazor components run outside the ASP.NET
Core request pipeline, so `IHttpContextAccessor` carries no guarantee there. `SignInAsync` writes a
`Set-Cookie` header, and setting a header after the response has started raises an exception. The
page that signs a user in therefore has to be statically server-rendered. Microsoft's own Identity
template makes its entire account area static for this reason.

This collides with component libraries: Radzen, like Telerik, requires interactive rendering and
does not respond to user input under static SSR. **The login form cannot be built from Radzen
components.** It is plain markup and an `EditForm`.

### The Blazor authorization surface

`AddCascadingAuthenticationState()` is the service-level equivalent of placing a
`CascadingAuthenticationState` component at the root of the component hierarchy.
`AuthorizeRouteView` replaces `RouteView` in the router and is what makes `@attribute [Authorize]`
work on a page; its `NotAuthorized` template is what a denied user sees, and without it they see
nothing.

For interactive server solutions, the template ships a revalidating authentication state provider
that re-checks the signed-in user's security stamp roughly every 30 minutes while a circuit is
connected. Without it, an open circuit keeps its principal after the underlying user is locked out
or deleted — which would make PR 6a's lockout mechanism ineffective on the Blazor side.

### Not verified

How a statically-rendered Blazor form obtains and submits its antiforgery token was not settled by
experiment and is not asserted here. The first phase that builds the login form must establish it
by running the form, not by assuming it.

Which registration supplies the antiforgery services is likewise unestablished. The repository
contains no `AddAntiforgery` call; that `AddRazorComponents()` is what registers them, and with
which defaults, is framework behaviour and was not verified here.

Whether `[EnableRateLimiting]` on a statically-rendered Blazor page component reaches endpoint
metadata where `UseRateLimiter` can see it is not established in this repository. The attribute is
proven to work as an MVC attribute on `AuthController.Login` and nowhere else; a Blazor form posts
to a component endpoint from `MapRazorComponents`, not to a controller. Decision 9 depends on this
and must be settled by experiment before its mechanism is chosen.

## Decisions taken before planning

Settled by the human. The planner treats these as constraints.

1. **The gate is closed by default.** `AddAuthorization` sets a `FallbackPolicy` requiring an
   authenticated user, and exemptions are granted explicitly with `[AllowAnonymous]`. The
   alternative — decorating each endpoint with `[Authorize]` — was rejected because PR 8 and PR 10
   add endpoints and screens, and a forgotten `[Authorize]` is an open door that no test catches,
   whereas a forgotten `[AllowAnonymous]` is a closed door that fails immediately. The cost is that
   every exemption must be found and named: the three auth endpoints, `/not-found`, the error page,
   static assets, and the Development-only OpenApi and Scalar endpoints. Each exemption needs a
   test that proves it is reachable anonymously.

2. **Policies are `CanRead` and `CanWrite`, and two roles are seeded.** Roles map to policies;
   endpoints never name a role. Adding a role later must not mean editing a controller.

3. **An authenticated user who fails a policy gets 403, and every rejection path carries a body —
   in both schemes.** `OnChallenge` and `OnForbidden` are `JwtBearerEvents` and cover the bearer
   scheme only; a request rejected by the cookie scheme never passes through them. A cookie
   handler's forbid produces a bodiless 403 unless `AccessDeniedPath` is set or
   `CookieAuthenticationEvents.OnRedirectToAccessDenied` is overridden, and a bodiless 4xx is
   precisely what `UseStatusCodePagesWithReExecute` re-executes as the not-found page. Scoping this
   decision to the bearer events alone would leave that failure alive on the Blazor side only,
   which is the half least likely to be noticed. Both schemes are therefore in scope: bearer
   through its events, cookie through `AccessDeniedPath` or `OnRedirectToAccessDenied`. The test
   must prove a body-carrying 403 for each scheme separately rather than for one.

   `GetReasonPhrase` gains a 403 arm; without it a 403 would carry the title `"Error"`. This does
   not orphan `ResultMappingTests`: that test pairs status-map entries with error codes, and a
   middleware-produced 403 adds no entry to the status map, so its fourth fact stays green.

   This is the decision that retires PR 6a's argument for the middleware's position inside the
   status-code-pages wrapper, and a test must prove a 403 is not re-executed as the not-found page,
   exactly as `AuthPipelineTests` proves it today for 401.

4. **The cookie scheme never redirects an API path to a login page.** A cookie handler's default
   behaviour on an unauthenticated request is a 302 to `LoginPath`; on `/api/*` that turns a 401
   into a redirect and breaks every API client. API paths answer 401 regardless of which scheme
   authenticated them. This constrains how the scheme is selected: selection keys on the **path
   prefix**, not on the presence of an `Authorization` header. Header presence is the shape the
   external findings name as typical, and it fails this decision — a browser holding a cookie and
   sending no header to `/api/*` would be forwarded to the cookie scheme and redirected.

5. **The access token carries a role claim, and the bearer handler is told how to read it.**
   Cookie-authenticated users get their roles from Identity's principal automatically; bearer
   users get nothing today, so every policy would fail for them. `JwtAccessTokenIssuer` adds the
   claim and `TokenValidationParameters` sets `RoleClaimType`. This is the one place PR 6b changes
   PR 6a's code, and ADR 0008 records it against ADR 0007's token-content decision.

6. **The 72 affected tests authenticate by logging in for real.** A test-only authentication
   handler was rejected: injecting a fabricated principal skips issuer, audience, lifetime and
   signature validation entirely, so the tests would prove a bypass works rather than that the
   surface is protected. The accepted cost is that a broken login turns 72 unrelated tests red —
   tolerable, because `AuthApiTests` fails first and the rest are redundant signal rather than
   misleading signal. The plan must align seeding with `ResetIdentityAsync`: a token obtained once
   per class dies when the reset deletes its user mid-class.

7. **The demo account is gated by configuration, not by environment name, and its password comes
   from configuration.** An environment-name check is not testable; a `Demo:Enabled` flag is. It
   defaults to off. The password is never committed — it follows the same path as the JWT signing
   key, user-secrets locally and App Service configuration in PR 7. The seeder is idempotent and
   must not fail when the account already exists.

8. **The login page is statically server-rendered and built from plain markup, and the router uses
   `AuthorizeRouteView` with a `NotAuthorized` template.** Forced by the signing-in constraint
   above, and the Radzen collision is recorded rather than worked around.

9. **The Blazor login path is rate-limited by the same named policy as the REST login endpoint;
   the mechanism is experiment-first.** `[EnableRateLimiting("login")]` currently sits on
   `AuthController.Login` only. A Blazor form posts to its own component endpoint, not to
   `/api/auth/login`, so without this the brute-force protection built in PR 6a is bypassable the
   moment a login form exists. That the path must be limited is settled; *how* is not. Whether the
   attribute on a statically-rendered page component reaches endpoint metadata where
   `UseRateLimiter` can see it is unestablished in this repository and is recorded under "Not
   verified" — it is proven only as an MVC attribute on a controller action. The phase that builds
   the form establishes it by running it. If the attribute does not reach metadata, the fallback is
   a dedicated endpoint or a middleware, which reshapes Decision 8. A test must prove the Blazor
   path is limited whichever mechanism carries it.

10. **Blazor components call the Application layer directly.** They inject `ICommandHandler` and
    `IQueryHandler` rather than calling the REST surface over HTTP. They run in the same process,
    so an HTTP hop would add a round trip and force a decision about how the UI holds a token —
    a decision the cookie scheme exists to avoid.

11. **Security-stamp revalidation is added.** Without it a connected circuit keeps its principal
    after the user is locked out or deleted, which would leave PR 6a's lockout ineffective in the
    UI.

## Risks and unknowns

- The antiforgery mechanics of a statically-rendered login form are unmapped and must be settled by
  running the form, not by reasoning.
- `AddIdentityCore` was chosen in PR 6a specifically to avoid pulling `SignInManager` and a shared
  framework reference into `Envanex.Infrastructure`. The cookie scheme needs sign-in, but PR 6a's
  decision does not have to be reopened to get it: `AddIdentityCore` already registers
  `IUserClaimsPrincipalFactory<EnvanexUser>`, and `SignInAsync` is an extension on
  `Microsoft.AspNetCore.Authentication` — both reachable from `Envanex.Web`. Sign-in can therefore
  be hand-rolled in the host, building the principal from the factory and calling
  `HttpContext.SignInAsync`, with no `SignInManager` and no framework reference entering
  `Envanex.Infrastructure`. Decision 11's revalidation runs on `UserManager.GetSecurityStampAsync`
  and needs nothing further. What remains is where in `Envanex.Web` that code sits, which is a
  placement question rather than a design one.
- Closing the gate changes what the three factories mean. Every one of them needs an authenticated
  client path, and a factory left out is a surface left untested rather than unprotected.
- The status-code-pages interaction is now live for 403 as well as 401, and the existing proof only
  covers 401.
- Seventy-two tests changing at once is the largest mechanical change in this PR and belongs in its
  own phase, not folded into the phase that adds the attributes.

## Open questions for the human

1. Which two role names are seeded, and which one the demo account receives.
2. Whether the read-only demo account is expressed as a role without `CanWrite`, or as a separate
   claim checked by the policy.
3. Whether `/api/auth/logout` stays anonymous under the fallback policy, given it takes a refresh
   token rather than an access token.
4. Whether the Blazor UI in this PR shows anything beyond a login page, a sign-out control and the
   current user, and whether the existing Home page becomes protected or stays public.
