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
| 3 Cookie scheme + Blazor | no | no | **yes** — one `UserManager.FindByIdAsync` per cookie sign-in, and a recurring security-stamp read from `RevalidatingIdentityAuthenticationStateProvider`, once every 30 minutes per connected circuit | **required** |
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
its consequence that `/api/*` is bearer-only; why the landing page requires `CanRead` rather than
mere authentication; and the `Demo:Enabled` gate.

Two further items, produced by Phase 1's reviews and recorded here so they do not get lost between
sessions:

- **Roles are read at refresh time, not carried from login.** Each access token is stamped with the
  roles read as it is issued, so a revoked role takes effect at the next refresh — within the
  fifteen-minute access-token lifetime. Carrying the roles forward from login would have let a
  revocation survive for the life of the refresh family, up to thirty days. This is a security
  property the system now has, and nothing else in the repository writes it down.
- **The role seeder's unique-violation clause is reached, not merely defensive.** Removing the
  clause turned `EnsureRolesAsync_RunByTwoHostsAtOnce_ShouldNotThrowAndShouldLeaveExactlyTwoRoles`
  red on every run. That is the inverse of ADR 0007's kept-but-unreachable 2601/2627 clause in
  `RotateAsync`, and the two should be recorded as a pair: the same shape of catch, opposite
  evidence.

---

# Phase 0: Spikes — throwaway code, one committed note

**DONE.** Run on 2026-09-21; the note is
`thoughts/shared/debug/2026-09-20_pr6b-blazor-auth-spikes.md`. All seven questions are answered and
every branch below is settled:

| Question | Branch | Consequence for the later phases |
|---|---|---|
| A — antiforgery in a statically rendered `EditForm` | **A1** | Phase 3 as written; `EditForm` emits both hidden fields, `__RequestVerificationToken` and `_handler` |
| B — `[EnableRateLimiting]` on a page component | **B1, and a GET spends a permit** | Phase 3 keeps the POST-only guard **and** `BlazorLoginPage_RepeatedGets_ShouldNotSpendLoginPermits`; no count falls |
| C1 — the framework script | **C-a** | Exemption row 11 is a consequence of row 8; no production line |
| C2 — the three `/_blazor` endpoints | **C-b** | Exemption row 12 gets its own scoped mechanism, named in the note |
| C3 — the unmatched non-`/api/*` path | **C3-a** | Row 5's mechanism column and its two case names, below |
| D — an authorization attribute on a `.razor` page | **D1** | `@attribute` on `.razor` files; `BlazorPageAuthorizationConvention` is **not** created; no count rises |
| E — code between `app.Build()` and `app.Run()` | **E1** | Phase 5 keeps `await app.SeedIdentityAsync();` in that position |

C3 took two runs. The first measured the configuration Spike C specifies — the fallback policy and
nothing else, which leaves bearer as the default scheme — where neither C3-a's 302 nor C3-b's 404 is
reachable, because the cookie scheme that owns `OnRedirectToLogin` does not land until Phase 3. The
re-run carried Phase 3's scheme registration as well, which is how Phase 4 actually ships, and
answered C3-a. Both runs are in the note; the first is kept because it is what settles C2's
second-order finding about `/not-found`.

The rest of this phase is left as it was written. It is the record of the questions as they were
asked, and the note is only readable against it — but nothing below is still open, and no later
phase branches on any of it.

**Seven questions, carried by five spikes (A, B, C with C1–C3, D, E), must be answered by running
something before Phases 3, 4 and 5 can be implemented as written.** This phase produces **no
committed source**. Its single deliverable is a committed debug note.

### Files

- `thoughts/shared/debug/2026-09-20_pr6b-blazor-auth-spikes.md` — **created and committed** — all
  seven observations, verbatim output pasted, and which branch of Phases 3, 4 and 5 each one
  selects. This is the phase's deliverable, so it is the one file the phase is allowed to leave
  behind.
- `src/Envanex.Web/Components/Pages/SpikeForm.razor` — created **and deleted before the phase ends**.
- `src/Envanex.Web/Program.cs` — temporarily edited for Spikes C, D and E, **reverted before the
  phase ends** (`git checkout -- src/Envanex.Web/Program.cs`).
- `tests/Envanex.IntegrationTests/Spikes/StartupSeedingProbeTests.cs` — created for Spike E **and
  deleted before the phase ends**.

### How the host is run for every spike

`src/Envanex.Web/Properties/launchSettings.json` defines two profiles: `http` binds
`http://localhost:5216`, `https` binds `https://localhost:7012;http://localhost:5216`. Every spike
below probes plain HTTP, so the host is always started with the **`http` profile**, where no HTTPS
port is discoverable and `app.UseHttpsRedirection()` (`Program.cs:128`) no-ops instead of answering
307 to every probe:

```powershell
dotnet run --project C:\projects\envanex\src\Envanex.Web --launch-profile http
```

All `curl` invocations below are Windows `curl.exe`. The null device is **`NUL`**, not `/dev/null`.
Run them from a scratch directory (`cd $env:TEMP`) so that relative output files land outside the
repository.

### Spike A — how a statically-rendered Blazor form obtains and submits its antiforgery token

Action:

1. Create `src/Envanex.Web/Components/Pages/SpikeForm.razor` at `@page "/spike-form"` containing an
   `<EditForm Model="Model" FormName="spike" method="post" OnValidSubmit="Submit">` with one text
   input bound through `[SupplyParameterFromForm]`, and a `[CascadingParameter] HttpContext?`.
2. Run the host under the `http` profile as above.
3. `curl -i -c cookies.txt http://localhost:5216/spike-form`
4. `curl -i -b cookies.txt -X POST -d "_handler=spike&__RequestVerificationToken=<value>&Model.Text=x" -H "Content-Type: application/x-www-form-urlencoded" http://localhost:5216/spike-form`
5. Repeat step 4 with the token field removed.

Expected observation: step 3's HTML carries `<input type="hidden" name="_handler" value="spike">`
and `<input type="hidden" name="__RequestVerificationToken" value="CfDJ8…">`, and a
`Set-Cookie: .AspNetCore.Antiforgery.*` response header; step 4 answers 200 or 302, and step 5
answers 400.

- **A1 — token field rendered automatically.** Phase 3 proceeds as written: `Login.razor` is an
  `<EditForm FormName="login">`, and `CookieAuthHelper` parses the hidden field out of the GET body.
- **A2 — token field absent until asked for.** Phase 3 adds an explicit `<AntiforgeryToken />` child
  inside the `EditForm`. Nothing else in Phase 3 changes.
- **A3 — POST rejected even with cookie and field.** Phase 3 collapses into Spike B's fallback
  branch (see B2): a plain `<form method="post" action="/login">` plus a minimal endpoint that calls
  `IAntiforgery.ValidateRequestAsync` explicitly.

Also record the exact hidden-field **name** observed; it becomes
`CookieAuthHelper.AntiforgeryFieldName` in Phase 3, which every form-posting test reads.

### Spike B — does `[EnableRateLimiting]` on a page component reach endpoint metadata?

Action: add `@attribute [EnableRateLimiting(AuthController.LoginRateLimitPolicy)]` to the same spike
page and run the host with the login policy narrowed, passing the settings on the command line
(the host reads command-line configuration through `WebApplication.CreateBuilder(args)`):

```powershell
dotnet run --project C:\projects\envanex\src\Envanex.Web --launch-profile http -- `
  --RateLimiting:Login:Enabled=true --RateLimiting:Login:PermitLimit=2 --RateLimiting:Login:WindowSeconds=60
```

Note while reading the output that the **global** limiter is also on under Development —
`Program.cs:31-33` defaults `RateLimiting:Enabled` to true at 100 per 60 s. Three requests cannot
trip it, so a 429 in this spike is always the login policy, but the fact should be read from here
rather than discovered.

Then issue three `GET /spike-form` and, separately, three `POST /spike-form`.

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
  the guard and drop that test; **every phase count from Phase 3 onward falls by one** (see the
  count table's outcome deltas).
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

### Spike C — what does the fallback policy do to the framework script, the `/_blazor` endpoints, and an unmatched path?

`App.razor` loads `_framework/blazor.web.js` on every page, `MapRazorComponents<App>()
.AddInteractiveServerRenderMode()` maps the `/_blazor` endpoints, and an unmatched path is answered
today by `UseStatusCodePagesWithReExecute("/not-found")`. None of the three carries authorization
metadata of its own, so the fallback policy added in Phase 4 reaches all three unless something
exempts them.

**Why this is a spike and not a note.** If the script answers 401 under the fallback policy, the
login page — the one page an anonymous visitor must be able to use — loses enhanced navigation
immediately, and any interactive component added to it later cannot establish a circuit at all.
That is a broken front door discovered in production rather than in a phase.

**Three separate questions.** They are mapped by different calls and may carry different metadata,
so one observation does not settle another. Answer each on its own and do not generalise.

Action: temporarily add the Phase 4 fallback policy to `Program.cs` and nothing else — no
`AllowAnonymous()` anywhere — then, signed out:

1. `curl -s -o NUL -w "%{http_code}`n" http://localhost:5216/_framework/blazor.web.js`
2. `curl -s -o NUL -w "%{http_code}`n" http://localhost:5216/login`
3. `curl -i -X POST "http://localhost:5216/_blazor/negotiate?negotiateVersion=1"`
4. `curl -i "http://localhost:5216/_blazor?id=00000000000000000000000000000000"` — the long-poll /
   transport endpoint, which is a **different** endpoint from negotiate.
5. `curl -i -X POST "http://localhost:5216/_blazor/disconnect"`
6. `curl -i http://localhost:5216/gibberish-unmatched-path` — unmatched, non-`/api/*`.
7. `curl -i http://localhost:5216/api/auth/register` — unmatched, under `/api/*`.
8. Repeat 1, 3, 4 and 5 with `app.MapStaticAssets().AllowAnonymous();` in place, to find out whether
   the asset manifest already covers the script.

**Paste the raw status line and, for 3–7, the raw response headers for every one of these into the
spike note** — not a summary of them.

Record, for each question separately, **which mechanism grants it**:

- **C1 — the script.** Does `MapStaticAssets().AllowAnonymous()` already cover
  `_framework/blazor.web.js` through the asset manifest, or is the script served by a different
  endpoint that needs its own exemption? The answer decides whether exemption row 11 is a
  consequence of row 8 or a line of its own.
- **C2 — the `/_blazor` endpoints.** Does the interactive-server render mode's endpoint carry
  metadata that `.AllowAnonymous()` on the `MapRazorComponents` builder reaches, or does it need
  `app.MapBlazorHub()`-style handling of its own? Record **all three** endpoints (negotiate,
  transport, disconnect) separately: an exemption scoped to negotiate alone still breaks a circuit,
  and row 12's test probes all three because of it. If
  `MapRazorComponents<App>().AllowAnonymous()` would exempt **every page** as a side effect, that is
  the wrong mechanism and the note must say so — Decision 1's whole point is that pages are closed
  by default.
- **C3 — the unmatched path and the 404 re-execution.** Probe 6 asks what an anonymous request to an
  unmatched non-`/api/*` path answers under the fallback policy: a 302 to `/login` (the challenge
  wins, and the 404 never happens), or a 404 carrying the not-found page (the re-execution wins).
  `UseStatusCodePagesWithReExecute` (`Program.cs:127`) re-executes 4xx and 5xx only, never a 302,
  so these two are mutually exclusive. Probe 7 asks the same question under `/api/*`, where the
  selector forwards to the bearer scheme; record whether the answer is a bodiless 401, a
  problem+json 401, or the not-found page. **Probe 7 is what decides the new expected status of
  `AuthApiTests.Register_ShouldReturn404`** (Phase 4).

Outcomes:

- **C-a — script and hub already anonymous** (whether by the manifest or by framework-supplied
  metadata). Rows 11 and 12 in Phase 4's exemption table are documentation plus a regression test
  each; no production line is added.
- **C-b — one or more answer 401.** Phase 4 adds the specific exemption the note names, scoped so
  that it does not exempt page components. Rows 11 and 12 name that mechanism.
- **C3-a — the anonymous unmatched path answers 302 to `/login`.** Row 5's mechanism column reads
  "none is possible — the challenge precedes the 404, so the 404 re-execution path is only reachable
  for an authenticated caller", and row 5's two cases assert
  `UnknownPath_WithoutAuthentication_ShouldRedirectToTheLoginPage` and
  `UnknownPath_WhileSignedIn_ShouldReturn404AndRenderTheNotFoundPage`.
- **C3-b — the anonymous unmatched path answers 404 with the not-found page.** Row 5's mechanism
  column reads "row 4's exemption on `/not-found` covers the re-executed request; the original
  request's denial is a bodiless 4xx that the wrapper re-executes", and row 5's two cases assert
  `UnknownPath_WithoutAuthentication_ShouldReturn404AndNotRedirectToLogin` and
  `UnknownPath_WhileSignedIn_ShouldReturn404AndRenderTheNotFoundPage`.

Under either outcome Phase 4 keeps both script/hub proving tests and both row 5 cases: they are what
stops a later change to the static-asset, render-mode or status-code-pages wiring from silently
closing the login page's script or turning an unmatched path into a redirect loop.

### Spike D — does an authorization attribute on a `.razor` page reach endpoint metadata?

This is the question finding 8 refused to let the plan assume. It decides three things at once: the
mechanism by which an anonymous request to `Home` is denied; whether `AuthorizeRouteView`'s
`NotAuthorized` template is reachable at all under static server rendering; and whether
`@attribute [AllowAnonymous]` on a `.razor` file is a valid exemption mechanism for exemption rows
4, 6 and 7.

Action, in two parts, with the spike page still mapped:

1. **Authorize.** Add `@attribute [Authorize]` to `SpikeForm.razor` with **no** fallback policy in
   `Program.cs` and **`RouteView` still in `Routes.razor`** (so no component-level authorization can
   act), then `curl -i http://localhost:5216/spike-form` signed out.
2. **AllowAnonymous.** Remove `[Authorize]`, add `@attribute [AllowAnonymous]`, put the Phase 4
   fallback policy back in `Program.cs`, then `curl -i http://localhost:5216/spike-form` signed out.

Expected observation for part 1: if the attribute reaches endpoint metadata, the authorization
middleware denies and the bearer handler (still the default scheme at Phase 0) challenges — a 401,
or a 404 carrying the not-found page if the bodiless 401 is re-executed. If it does not reach
metadata, the page renders with 200. Part 2: 200 means `[AllowAnonymous]` in a `.razor` file is a
working exemption; anything else means it is not.

- **D1 — `.razor` attributes reach endpoint metadata (expected).** The mechanism for every page in
  this PR is **endpoint metadata**: `@attribute [Authorize(Policy = EnvanexPolicies.CanRead)]` on
  `Home.razor`, `@attribute [AllowAnonymous]` on `Login.razor`, `NotFound.razor` and `Error.razor`.
  An anonymous `GET /` is denied by `UseAuthorization`, the cookie handler's
  `OnRedirectToLogin` answers 302 to `cookie.LoginPath`, and
  `AuthenticatedShellTests.GetHome_WhileSignedOut_ShouldRedirectToTheLoginPage` is the proof.
  `AuthorizeRouteView`'s `NotAuthorized` template is **not reachable for a statically-rendered page
  under this branch** — the endpoint layer answers first — and it is added anyway because Decision 8
  mandates it and PR 7's interactive components make it live. `Routes.razor` carries a comment
  saying exactly that, so nobody later reads the template as dead code and deletes it.
- **D2 — `.razor` attributes do not reach endpoint metadata.** The mechanism is **still endpoint
  metadata**; only its source moves out of the `.razor` files.
  `src/Envanex.Web/Authorization/BlazorPageAuthorizationConvention.cs` is created in Phase 3 and
  attaches `AuthorizeAttribute`/`AllowAnonymousAttribute` metadata by route pattern through the
  `RazorComponentsEndpointConventionBuilder` that `MapRazorComponents<App>()` returns. The inert
  `@attribute` lines are **removed** from the `.razor` files rather than left as decoration, and the
  convention's route→requirement list becomes the single place page permissions are written. Every
  test name in Phases 3 and 4 is unchanged, because every one of them asserts over HTTP; Phase 3
  adds **one** case,
  `BlazorPageAuthorizationConventionTests.EveryRoutablePageEndpoint_ShouldCarryEitherAPolicyOrAllowAnonymous`,
  so all counts from Phase 3 onward rise by one.

Under both branches the answers to "what denies an anonymous `GET /`" and "is `NotAuthorized`
reachable under static SSR" are the same: the endpoint layer, and no. Only the D2 source of metadata
differs, which is why no test changes shape.

### Spike E — does code between `app.Build()` and `app.Run()` execute under `WebApplicationFactory`?

Phase 5 puts `await app.SeedIdentityAsync();` in exactly that position and then asserts, end to end,
that a factory-created client can log in as the seeded demo account. `HostFactoryResolver` captures
the host at the `HostBuilt` diagnostic event, and the entry point's continuation after that point is
not guaranteed to run; the research asserts it does (research:236-237) without evidence.

Action:

1. Add, temporarily, between `app.Build()` and `app.Run()` in `Program.cs`, a line that writes one
   marker row through a scope (`auth.AspNetRoles` with the name `"Spike-E-Marker"` is enough, since
   `ResetIdentityAsync` cleans it).
2. Add `tests/Envanex.IntegrationTests/Spikes/StartupSeedingProbeTests.cs` with one case that
   creates a client from `_fixture.WebApplicationFactory` (forcing the host to start) and then
   queries `auth.AspNetRoles` through `_fixture.CreateIdentityDbContext()` for that name.
3. `dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~StartupSeedingProbeTests"`
4. Paste the pass/fail output into the note, then delete both edits.

- **E1 — the marker row is there.** Phase 5 is as written: `SeedIdentityAsync` is called between
  `Build()` and `Run()`.
- **E2 — the marker row is absent.** Phase 5 changes shape, spelled out here: seeding moves into a
  hosted service, `src/Envanex.Web/Identity/IdentitySeedingHostedService.cs`
  (`IHostedService.StartAsync` calls `DemoAccountSeeder.SeedAsync`), registered in
  `IdentitySeedingExtensions.AddEnvanexIdentitySeeding(this IServiceCollection, IConfiguration)`;
  `Program.cs` loses the `await app.SeedIdentityAsync();` line and gains the registration beside the
  other service registrations. `WebApplicationFactory` starts the host, so hosted services run, and
  `DemoAccount_ShouldBeAbleToReadButNotWrite` is unchanged. The test **count** is unchanged under
  both branches.

### Tests to add

None. Spike E's probe is written, run, and deleted inside the phase; it is an observation, not a
committed test.

### Validation

```powershell
cd C:\projects\envanex
dotnet run --project src\Envanex.Web --launch-profile http   # spikes A-D, then Ctrl+C
dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~StartupSeedingProbeTests"  # spike E
git checkout -- src/Envanex.Web/Program.cs
git status --porcelain -- src tests      # MUST be empty: every spike edit under src/ and tests/ is reverted
git status --porcelain -- thoughts       # expected: exactly the new debug note
dotnet build -warnaserror
dotnet test
```

The `src`/`tests` scoping is the point: the phase's whole output is the note under `thoughts/`, and
nothing it touched under `src/` or `tests/` survives it.

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

`JwtAuthenticationExtensions.AddEnvanexJwtBearer`: `JwtAuthenticationExtensions.cs:33-46` assigns a
**whole new** `TokenValidationParameters` object, so the two claim-type lines must not precede it —
placed before the assignment they are silently discarded and `BearerRoleClaimTests` fails with a
misleading message. Fold them into that existing object initializer:

```csharp
bearer.TokenValidationParameters = new TokenValidationParameters
{
    /* the six existing properties and ClockSkew, unchanged */
    RoleClaimType = EnvanexClaimTypes.Role,
    NameClaimType = JwtRegisteredClaimNames.Sub,
};

// Not part of TokenValidationParameters, so position within the callback does not matter.
bearer.MapInboundClaims = false;
```

`IdentityOptions.ClaimsIdentity.RoleClaimType` is deliberately **not** changed.
`UserClaimsPrincipalFactory<EnvanexUser, IdentityRole<Guid>>` already writes role claims under
`ClaimTypes.Role`, and `ClaimsPrincipal.IsInRole` resolves per-identity, so a cookie identity and a
bearer identity satisfy the same `RequireRole` without sharing a claim type. **Both halves of that
sentence are proved by a test rather than assumed:** the bearer half by
`BearerRoleClaimTests.TokenCarryingTheAdministratorRole_ValidatedWithHostOptions_ShouldProduceAPrincipalInThatRole`
in this phase, and the cookie half by
`CookieAuthPipelineTests.Cookie_AuthenticatedViewer_ShouldReadTheHomePage` and
`Cookie_AuthenticatedUserWithNoRole_ShouldReturn403WithABodyAndNotTheNotFoundPage` in Phase 3, which
evaluate `RequireRole(Administrator, Viewer)` against a cookie identity over HTTP and get opposite
answers from the two identities.

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
  `MapInboundClaims` surprise, and the bearer half of the two-claim-type equivalence.

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

Expected test count at end: **573** (120 + 126 + **327**).

**Run db-reviewer on this phase** — three new query shapes against the `auth` schema.

---

# Phase 2: Authenticated test clients, and the five classes switched over

Decision 6. The gate is still open at the end of this phase — an authenticated request to an open
endpoint answers exactly as an anonymous one does, so every affected test stays green either way.
This phase exists so that Phase 4 does not have to touch seventy-odd tests and a policy in one
commit.

### The blast radius is 77, counted by executed case

| Group | Cases | What happens when the gate closes |
|---|---|---|
| `ProductsDatasourceTests`, `ProductsApiTests`, `UnitOfMeasuresApiTests`, `ConcurrencyApiTests`, `RateLimiterTests` | 72 | 71 would go red. The exception is `RateLimiterTests.SharedFactory_WithGlobalLimiterDisabled_ShouldNotRegisterAGlobalLimiter` (`RateLimiterTests.cs:110-120`), which reads `IOptions<RateLimiterOptions>` and makes no HTTP call; it is untouched by the gate but lives in a class whose client changes, so it is counted here. |
| `AuthPipelineTests` | 4 | They do not break; they **change meaning**, and inverting them is how the gate is proved. Handled in Phase 4. |
| `AuthApiTests.Register_ShouldReturn404` (`AuthApiTests.cs:365-378`) | 1 | An anonymous GET of an unmatched `/api/*` path stops being a 404. Handled in Phase 4. |
| **Total touched** | **77** | |

The research's "72" and the previous plan's "76" are both superseded by this table.

### How seeding and token acquisition align with `ResetIdentityAsync`

Stated concretely, because this is the trap:

1. `SqlServerFixture` owns a token cache keyed by email, holding the token **and its expiry**.
2. **`ResetIdentityAsync` clears that cache in the same method body that issues the seven `DELETE`
   statements.** The only code that can delete a seeded user is the only code that can orphan a
   cached token, so it cannot orphan one.
3. Tokens are acquired **lazily, on a cache miss, inside `GetAccessTokenAsync`** — never in a class
   constructor, never once per class. On a miss it ensures the role, ensures the user, then performs
   a real `POST /api/auth/login` through `WebApplicationFactory`, exactly as Decision 6 requires.
4. **A cache hit within sixty seconds of the token's `exp` counts as a miss.** The factory sets
   `Jwt:AccessTokenMinutes=15` (`EnvanexWebApplicationFactory.cs:39`) and the handler sets
   `ClockSkew = TimeSpan.Zero`, so a run where more than fifteen minutes pass between a cache fill
   and its last use — a cold CI agent pulling the SQL Server image, a debugger paused on a
   breakpoint — would otherwise hand out an expired token and fail an arbitrary subset of the 72
   with 401. The expiry is read off the returned token's `exp` claim with
   `JwtSecurityTokenHandler.ReadJwtToken`, not computed from configuration, so the cache cannot
   disagree with the issuer.
5. Ordering inside any single test is therefore always reset → ensure → login → use. The five
   affected classes call `_fixture.ResetAsync()` (business tables only, untouched by auth) and then
   `await _fixture.CreateAdministratorClientAsync()` in `InitializeAsync`.
6. Cost: because xUnit serialises the `DatabaseCollection`, a run of 37 consecutive
   `ProductsDatasourceTests` performs **one** user creation and **one** login, not 37. A login is
   re-performed only after a class that calls `ResetIdentityAsync` has run, or after a token comes
   within a minute of expiry. Expected added wall clock: a handful of PBKDF2 pairs, not seventy-two.
7. **The cache is a ruling, not a shortcut.** Decision 6 says "log in for real", not "log in once
   per test". A cached token was minted by a real `POST /api/auth/login` against the real endpoint
   with real PBKDF2, and it passes full issuer, audience, lifetime and signature validation on
   **every** use — that is the property the decision exists to protect, and caching does not touch
   it. What a test-only authentication handler would have skipped is exactly what the cached token
   still performs. Per-test login would add roughly 14 s to a 44 s suite and push it past the 60 s
   line ADR 0007 set, for no gain in what is proved.

### How each of the five classes obtains an authenticated client

| Class | Cases | Factory | How |
|---|---|---|---|
| `ProductsApiTests` | 20 | shared | `_client = await _fixture.CreateAdministratorClientAsync();` in `InitializeAsync`, after `ResetAsync()` |
| `UnitOfMeasuresApiTests` | 8 | shared | same |
| `ProductsDatasourceTests` | 37 | shared | same |
| `ConcurrencyApiTests` | 2 | shared | same |
| `RateLimiterTests` | 5 | **`RateLimitedWebApplicationFactory`** | token minted **through the shared factory**, header attached to the rate-limited factory's client: line 32 becomes `_client = await _fixture.CreateAdministratorClientAsync(_rateLimitedFactory);`. Logging in through the rate-limited host itself would spend one of its two global permits and break `MutationEndpoint_ExceedingRateLimit_ShouldReturn429`. All three factories share `TestSigningKey`, issuer, audience and database, so a token minted on one validates on any of them. `SharedFactory_…ShouldNotReject150Consecutive…` builds its own client at line 128 and switches to `await _fixture.CreateAdministratorClientAsync()`. |

`LoginRateLimitedWebApplicationFactory` gets its authenticated path through the same overload and is
proved by a new case (below) rather than by an existing one: minting through the shared factory is
what keeps the 2-permit login budget intact.

### Files

- `tests/Envanex.IntegrationTests/Fixtures/SqlServerFixture.cs` — **modified** — the token cache with
  expiry, the client factory methods, and the cache clear inside `ResetIdentityAsync`.
- `tests/Envanex.IntegrationTests/Fixtures/IdentitySeeder.cs` — **modified** — idempotent helpers
  added. `CreateUserAsync` keeps its throw-on-duplicate contract; other classes rely on it.
- `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs` — **modified** — a second
  constructor taking an environment name and setting overrides, so Phases 3, 4 and 5 configure this
  one type instead of adding factory types. The overrides are applied **last** in
  `ConfigureWebHost`, so they can replace a value the primary constructor set.
- `tests/Envanex.IntegrationTests/Api/ProductsApiTests.cs`,
  `UnitOfMeasuresApiTests.cs`, `ProductsDatasourceTests.cs`, `ConcurrencyApiTests.cs` —
  **modified** — `_client` moves from the constructor to `InitializeAsync` and becomes
  `private HttpClient _client = null!;`.
- `tests/Envanex.IntegrationTests/Api/RateLimiterTests.cs` — **modified** — line 32 and line 128
  only. It already declares `private HttpClient _client = null!;` at line 20 and already assigns in
  `InitializeAsync` at line 32; no restructuring is needed there.
- `tests/Envanex.IntegrationTests/Api/AuthenticatedClientTests.cs` — **created**. It calls
  `await _fixture.ResetAsync()` first in `InitializeAsync` and uses unit-of-measure codes unique to
  the class: its writing cases assert 201/201/429, and a leftover code from an earlier class would
  turn a 201 into a 409 and the test into a false red.

### Signatures

```csharp
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string AdministratorEmail = "fixture-administrator@envanex.test";
    public const string ViewerEmail        = "fixture-viewer@envanex.test";
    public const string RoleLessEmail      = "fixture-no-role@envanex.test";
    public const string SeededPassword     = "Envanex-Fixture-Parola-1";

    // Private (amendment): the cache is keyed by email alone, so only the fixed wrappers below —
    // one email, one role each — may reach it.
    private Task<string> GetAccessTokenAsync(string email, string? role);
    public Task<string> GetAdministratorTokenAsync();
    public Task<string> GetViewerTokenAsync();

    public Task<HttpClient> CreateAdministratorClientAsync();
    public Task<HttpClient> CreateAdministratorClientAsync(WebApplicationFactory<Program> factory);
    public Task<HttpClient> CreateViewerClientAsync();
    public Task<HttpClient> CreateViewerClientAsync(WebApplicationFactory<Program> factory);

    // Test seam for the expiry path: moves the cached entry's expiry to thirty seconds ahead —
    // still valid, but inside the one-minute margin — without touching the database, so the
    // re-mint branch is reachable without a fifteen-minute wait. Not into the past on purpose: a
    // naive "has it expired yet" check would also re-mint an already-expired entry, so only a
    // near-expiry one proves the margin itself. Mutation D2 is what proves it.
    internal void ExpireCachedToken(string email);

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);
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

`tests/Envanex.IntegrationTests/Api/AuthenticatedClientTests.cs` (**created, +5**)
- `SharedFactory_AdministratorClient_ShouldCarryABearerTokenInTheAdministratorRole`
- `RateLimitedFactory_AdministratorClient_ShouldLeaveBothGlobalPermitsUnspent` — attaches the
  cross-minted token, then proves two writes still succeed and the third 429s.
- `LoginRateLimitedFactory_AdministratorClient_ShouldLeaveBothLoginPermitsUnspent` — attaches the
  cross-minted token, then proves two logins still succeed against that host and the third 429s.
  This is the third factory's authenticated path.
- `AccessToken_AfterResetIdentityAsync_ShouldBeReissuedRatherThanReused` — takes a token, calls
  `ResetIdentityAsync`, takes a token again, asserts the two `jti` values differ. This is the test
  that protects the cache/reset alignment; delete the cache-clear line and it goes red.
- `AccessToken_WhenTheCachedTokenIsNearExpiry_ShouldBeRemintedRatherThanReused` — takes a token,
  calls `ExpireCachedToken`, takes a token again, asserts the two `jti` values differ. Delete the
  expiry check and it goes red.

### Validation

```powershell
cd C:\projects\envanex
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~Envanex.IntegrationTests.Api"
```

Expected test count at end: **578** (120 + 126 + **332**). **No schema, no migration, no production
query — db-reviewer not required.**

---

# Phase 3: The cookie scheme, the login page, sign-out, and revalidation

Decisions 3 (cookie half), 4, 8, 9, 10, 11, 15. Phase 0 settled every question this phase depended
on: **A1**, **B1 with a GET spending a permit**, and **D1**. So `Login.razor` is an `<EditForm>`
with no `<AntiforgeryToken />` child, the `"login"` policy delegate gains a POST-only guard, page
permissions are written as `@attribute` lines in the `.razor` files, and
`BlazorPageAuthorizationConvention` is not created. Nothing here is conditional any more.

The `/api/*` gate is still open at the end of this phase. One page is not: **`Home.razor` carries
`@attribute [Authorize(Policy = EnvanexPolicies.CanRead)]`**. An ERP landing page shows inventory
data, and a user with no role has no business seeing it — so the landing page is role-gated, not
merely authenticated. Three consequences follow and are honoured below:

- `EnvanexPolicies` and the two `AddPolicy` registrations land **here**, not in Phase 4. Phase 4 adds
  only the `FallbackPolicy` and the controller attributes. A policy name that no `AddPolicy`
  registered throws at authorization time, so the two must land together with the attribute that
  names one.
- The cookie scheme's **forbid** becomes reachable in this phase, so Decision 3's cookie half —
  `CookieAuthenticationEvents.OnRedirectToAccessDenied` writing a body-carrying 403 — lands here
  too, with the failure it answers. The bearer half stays in Phase 4, where the first bearer policy
  lands. Each half ships with its first reachable failure rather than with its sibling.
- Denial of an anonymous `GET /` happens at the **endpoint layer**: `UseAuthorization` denies,
  the cookie handler's `OnRedirectToLogin` answers 302 to `cookie.LoginPath`. It does **not** happen
  through `AuthorizeRouteView`, whose `NotAuthorized` template is unreachable for a statically
  rendered page (Spike D). The template is still added, because Decision 8 requires it and PR 7's
  interactive components make it live; `Routes.razor` carries a comment saying so.

### Files

- `src/Envanex.Web/Authentication/EnvanexAuthenticationSchemes.cs` — **created**.
- `src/Envanex.Web/Authentication/CookieSignInService.cs` — **created**.
- `src/Envanex.Web/Authentication/RevalidatingIdentityAuthenticationStateProvider.cs` — **created**.
- `src/Envanex.Web/Authentication/EnvanexAuthenticationEvents.cs` — **created** — the **cookie half
  only** in this phase: `CookieAuthenticationEvents.OnRedirectToAccessDenied` writes a 403 with a
  Turkish HTML body. Writing the body starts the response, which is what stops
  `UseStatusCodePagesWithReExecute` from re-executing it as `/not-found`. `OnRedirectToLogin` keeps
  the framework's 302 — that is the correct answer on a non-API path, and Decision 4 only forbids it
  on `/api/*`. The bearer half is added in Phase 4.
- `src/Envanex.Web/Authentication/CookieSecurePolicyResolver.cs` — **created** — `internal static
  CookieSecurePolicy Resolve(string? configured)`: `null`/absent → `Always`; `"Always"` → `Always`;
  `"SameAsRequest"` → `SameAsRequest`; anything else throws `InvalidOperationException`. This is how
  the integration host gets a replayable cookie, see below.
- `src/Envanex.Web/Authorization/EnvanexPolicies.cs` — **created** — `CanRead`, `CanWrite`.
- `src/Envanex.Web/Extensions/JwtAuthenticationExtensions.cs` — **modified** — renamed in intent,
  not in file name: it now registers the selector policy scheme, the cookie scheme and the bearer
  scheme together. `AddAuthentication` moves from naming `"Bearer"` as the default to naming the
  selector. It reads `Auth:Cookie:SecurePolicy` through `CookieSecurePolicyResolver`.
- `src/Envanex.Web/Program.cs` — **modified** — `AddCascadingAuthenticationState()`, the
  `AuthenticationStateProvider` registration, `CookieSignInService` registration, the two
  `AddPolicy` calls inside the existing `AddAuthorization` at line 28, and **the POST-only guard in
  the `"login"` policy delegate** — when `context.Request.Method` is not `POST`, the request falls
  into its own fixed partition, `return RateLimitPartition.GetNoLimiter("login-non-post")`, so every
  GET shares one unlimited bucket and never touches the per-address POST bucket. The guard is
  required, not optional: Spike B observed the third consecutive **GET** of the attributed page
  answer 429, because a component endpoint serves GET and POST from one endpoint. Without it, five
  reloads of `/login` lock login out for five minutes in production.

  **The guard as first written here was a defect, and it is the one place PR 6b nearly regressed
  PR 6a.** It returned `GetNoLimiter(partitionKey)` — the same per-address key the POST limiter
  uses. A limiter is built once per partition key and reused for the host's lifetime, so whichever
  method arrived first from an address decided that address's limiter forever. The coder observed
  it on a real host (`--launch-profile http`, login limit 2 per 60 s): after one visit to `/login`,
  four consecutive `POST /api/auth/login` answered 401 and never 429 — one page load had turned off
  brute-force protection on the REST login for that address, the protection PR 6a shipped. The
  reverse order was broken too: POSTs first spent the limit, and every later GET of `/login`
  answered 429. Spike B missed it because it restarted the host between its GET and POST
  sequences, so the two methods never shared a partition. The fixed key is what separates them.
- `src/Envanex.Web/Components/Routes.razor` — **modified** — `RouteView` → `AuthorizeRouteView` with
  a `NotAuthorized` template carrying the Turkish "Bu sayfayı görüntüleme yetkiniz yok." and, for an
  anonymous user, a link to `/login`, plus the comment recording that under static SSR the endpoint
  layer answers first and this template goes live with PR 7's interactive components.
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
  spent its experiment budget on the seven questions Phase 0 names.
- `src/Envanex.Web/Components/Layout/NavMenu.razor` — **modified** — an `<AuthorizeView>` showing the
  signed-in user's email and a link to `/sign-out`, and for an anonymous visitor a link to `/login`.
- `src/Envanex.Web/Components/Pages/Home.razor` — **modified** —
  `@attribute [Authorize(Policy = EnvanexPolicies.CanRead)]`.
- `tests/Envanex.IntegrationTests/Fixtures/EnvanexWebApplicationFactory.cs` — **modified** — adds
  `builder.UseSetting("Auth:Cookie:SecurePolicy", "SameAsRequest");` with a comment: the default
  client's `BaseAddress` is `http://localhost` and `UseHttpsRedirection` no-ops under TestServer, so
  a cookie marked `Secure` would never be replayed by the handler's cookie container and every
  cookie test in Phases 3–5 would fail for a reason unrelated to the code under test. Production
  keeps `Always` because the setting is absent there.
- `src/Envanex.Web/appsettings.Development.json` — **modified** — adds
  `"Auth": { "Cookie": { "SecurePolicy": "SameAsRequest" } }` with a `//` comment above it saying
  why. The same problem the test factory has, on a developer's machine: every spike in Phase 0 and
  this phase's manual smoke step run under the `http` profile at `http://localhost:5216`, and a
  browser silently discards a `Secure` cookie delivered over plain HTTP. Without this, signing in
  at `/login` by hand appears to fail — no cookie is stored, the redirect to `/` bounces straight
  back to the login page — and the cause is configuration, not code. Production is unaffected
  (`appsettings.json` does not carry the key, and `appsettings.Development.json` does not ship to
  App Service) and `Testing` is unaffected (the factory's `UseSetting` wins over file
  configuration). The .NET JSON configuration provider skips `//` comments, so the comment is
  legal here.
- `tests/Envanex.IntegrationTests/Fixtures/CookieAuthHelper.cs` — **created** — the one place a test
  signs in through the login form. Without it, "signs in through the login form" gets written four
  different ways across `LoginPageTests`, `SignOutPageTests`, `AuthenticatedShellTests`,
  `CookieAuthPipelineTests` and Phase 5's demo test.
- `src/Envanex.Web/Authorization/BlazorPageAuthorizationConvention.cs` — **not created.** Spike D
  landed on D1: `@attribute [Authorize]` and `@attribute [AllowAnonymous]` in a `.razor` file both
  reach endpoint metadata, so the attributes in the files above are the mechanism and there is no
  convention class and no
  `BlazorPageAuthorizationConventionTests.EveryRoutablePageEndpoint_ShouldCarryEitherAPolicyOrAllowAnonymous`.
  (A route-pattern convention on the same builder is still used in Phase 4, for the `/_blazor`
  endpoints, which carry no attributes of their own. That is a different mechanism for a different
  surface; it does not bring this file back.)

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
    options.AddPolicy(EnvanexPolicies.CanRead,  policy => policy.RequireRole(EnvanexRoles.Administrator, EnvanexRoles.Viewer));
    options.AddPolicy(EnvanexPolicies.CanWrite, policy => policy.RequireRole(EnvanexRoles.Administrator));
    // FallbackPolicy lands in Phase 4.
});
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
factory — which supplies role claims under `ClaimTypes.Role` and the security-stamp claim — and
calls `httpContext.SignInAsync(EnvanexAuthenticationSchemes.Cookie, principal)`.

Scheme registration, in `AddEnvanexJwtBearer`:

```csharp
services.AddAuthentication(options =>
    {
        options.DefaultScheme             = EnvanexAuthenticationSchemes.Selector;
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
        //
        // The cookie is therefore never read on /api/*, which makes CSRF on the REST surface
        // structurally impossible rather than merely mitigated: the controllers carry no
        // antiforgery, and SameSite=Lax reduces that exposure without removing it.
        options.ForwardDefaultSelector = context =>
            context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                ? JwtBearerDefaults.AuthenticationScheme
                : EnvanexAuthenticationSchemes.Cookie;
    })
    .AddCookie(EnvanexAuthenticationSchemes.Cookie, cookie =>
    {
        // LoginPath = "/login", ReturnUrlParameter, HttpOnly, SameSite=Lax, SlidingExpiration,
        // Events = EnvanexAuthenticationEvents.Cookie
        cookie.Cookie.SecurePolicy =
            CookieSecurePolicyResolver.Resolve(configuration["Auth:Cookie:SecurePolicy"]);
    })
    .AddJwtBearer(/* unchanged from Phase 1 */);
```

`cookie.AccessDeniedPath` is **not** set: Decision 3 requires a body-carrying 403, and a path would
produce a 302 instead. `OnRedirectToAccessDenied` writes the body.

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

```csharp
namespace Envanex.IntegrationTests.Fixtures;

internal static class CookieAuthHelper
{
    // The exact name Spike A recorded. One constant, not five string literals.
    public const string AntiforgeryFieldName = "__RequestVerificationToken";

    // AllowAutoRedirect = false is the point: CreateClient() follows redirects by default, and
    // every 302 assertion in Phases 3-5 would otherwise see the followed response instead.
    // HandleCookies stays at its default of true; the cookie container is what replays the
    // auth cookie, which is why SecurePolicy must be SameAsRequest under Testing.
    public static HttpClient CreateNonRedirectingClient(WebApplicationFactory<Program> factory);

    public static Task<string> ReadAntiforgeryTokenAsync(HttpClient client, string pagePath);

    // GET /login, parse the hidden field, POST the form, return the same client holding the cookie.
    public static Task<HttpClient> SignInAsync(
        WebApplicationFactory<Program> factory, string email, string password);
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
- `GetHome_WhileSignedInAsAnAdministrator_ShouldRenderTheSignedInUsersEmailInTheNavMenu`
- `GetHome_WhileSignedOut_ShouldRedirectToTheLoginPage` — the endpoint-layer denial plus
  `cookie.LoginPath`, per Spike D.

`tests/Envanex.IntegrationTests/Blazor/CookieAuthPipelineTests.cs` (**created, +2**) — the cookie
half of Decision 3, and the cookie half of the two-claim-type equivalence Phase 1 asserts.
- **`Cookie_AuthenticatedUserWithNoRole_ShouldReturn403WithABodyAndNotTheNotFoundPage`** — signs in
  through the login form as `SqlServerFixture.RoleLessEmail`, requests `/`, and asserts 403, a
  non-empty body, and that the body is not the not-found page. The test must create that user
  itself with `IdentitySeeder.EnsureUserAsync` before signing in — `CookieAuthHelper.SignInAsync`
  signs in, it does not create, and the Phase 2 amendment removed `GetRoleLessTokenAsync`, which
  was the only other code that would have created it. Reachable because `Home.razor`
  requires `CanRead`. Remove `OnRedirectToAccessDenied` and this goes red as a 404 carrying the
  not-found page; remove the policy from `Home.razor` and it goes red as a 200.
- **`Cookie_AuthenticatedViewer_ShouldReadTheHomePage`** — signs in as `SqlServerFixture.ViewerEmail`
  and asserts 200 on `/`. Together with the case above this is a `RequireRole` evaluated against a
  **cookie** identity carrying `ClaimTypes.Role`, which is the proof Phase 1's claim needs: change
  `IdentityOptions.ClaimsIdentity.RoleClaimType` or break the claims factory and this goes red.

`tests/Envanex.IntegrationTests/Blazor/CookieSchemeOptionsTests.cs` (**created, +4**)
- `Resolve_WhenTheSettingIsAbsent_ShouldBeAlways` — production never sets the key, so the default is
  what ships.
- `Resolve_WhenTheSettingIsUnrecognised_ShouldThrow` — a typo must not silently downgrade the cookie.
- `AppSettingsDevelopment_SecurePolicy_ShouldBeSameAsRequestSoTheHttpProfileCanSignIn` — reads
  `src/Envanex.Web/appsettings.Development.json` from disk, the way `JwtAppSettingsTests` reads the
  shipped `appsettings.json`, and asserts the key is `"SameAsRequest"`. An untested appsettings
  value is exactly the thing that drifts: delete the key and the manual smoke step starts failing
  for a reason no automated test would otherwise catch, because the whole suite runs under
  `Testing`, where the factory override wins.
- `TestHost_CookieOptions_ShouldUseSameAsRequestSoTheCookieIsReplayedOverHttp` — reads
  `IOptionsMonitor<CookieAuthenticationOptions>.Get(EnvanexAuthenticationSchemes.Cookie)` from the
  shared factory. Delete the factory's override and this goes red before the twelve cookie tests do,
  with a message that names the cause.

`tests/Envanex.IntegrationTests/Blazor/SecurityStampRevalidationTests.cs` (**created, +2**)
- `AuthenticationStateProvider_ShouldBeTheRevalidatingIdentityProvider`
- `ValidateSecurityStampAsync_AfterTheUsersSecurityStampChanges_ShouldReturnFalse` — changes the
  stamp through `UserManager.UpdateSecurityStampAsync` and asserts the previously-issued principal
  stops validating. Without this, Decision 11 is an unproved claim.

`tests/Envanex.IntegrationTests/Api/SchemeSelectionTests.cs` (**created, +2**)
- `ForwardDefaultSelector_ForAnApiPath_ShouldSelectTheBearerScheme`
- `ForwardDefaultSelector_ForANonApiPath_ShouldSelectTheCookieScheme` — both invoke the selector
  from `IOptionsMonitor<PolicySchemeOptions>` against a `DefaultHttpContext`. The end-to-end proof
  that `/api/*` never redirects is in Phase 4, where an API challenge is first reachable.

`tests/Envanex.IntegrationTests/Api/LoginRateLimiterTests.cs` (**+2**, Decision 9)
- `BlazorLoginForm_ExceedingTheLoginRateLimit_ShouldReturn429`
- `BlazorLoginPage_RepeatedGets_ShouldNotSpendLoginPermits` — **kept, and strengthened.** It issues
  the GETs of `/login` first, then two POSTs that must answer 200, then a third POST that must
  answer 429. Both halves are the guard's proof: without the guard a GET spends a permit and the
  first POSTs fail; with the guard keyed on the address, the GETs claim the address's partition as
  a no-limiter and the third POST answers 200 instead of 429. The test as first written here —
  GETs only, asserting none of them answered 429 — stayed green with that second defect present,
  which is why it was strengthened rather than kept. Its comment records the observed failure. No
  count falls.

### Validation

```powershell
cd C:\projects\envanex
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~Envanex.IntegrationTests.Blazor"
dotnet run --project src\Envanex.Web --launch-profile http   # manual: sign in at /login, see the email in NavMenu, sign out
```

The manual step closes the roadmap's known gap "the manual smoke steps in the PR 6a plan that
require a signed-in user were never run". It runs under the `http` profile like every other manual
and spike step in this PR, and it only works because `appsettings.Development.json` sets
`Auth:Cookie:SecurePolicy` to `SameAsRequest` (see this phase's Files list). If that key is missing,
the sign-in appears to fail with no error: the browser discards the `Secure` cookie and the
redirect to `/` bounces back to `/login`.

Expected test count at end: **599** (120 + 126 + **353**). Settled, not provisional: B1 keeps the
GET case and D1 adds none, so neither of the deltas the plan used to carry applies.

**Run db-reviewer on this phase** — two new production reads: a `FindByIdAsync` per cookie sign-in,
and a recurring security-stamp read from `RevalidatingIdentityAuthenticationStateProvider`, once
every 30 minutes for every connected circuit. The recurring one is the read to rule on: it scales
with concurrent users rather than with sign-ins.

---

# Phase 4: Close the gate

Decisions 1, 2, 3 (bearer half), 4, 13, 14. This is the phase that changes behaviour for every
caller. The five classes already authenticate (Phase 2) and the login page already exists (Phase 3),
so nothing in this phase repairs a break it caused.

### Files

- `src/Envanex.Web/Program.cs` — **modified** — the existing `AddAuthorization` call gains the
  fallback policy; `app.MapStaticAssets().AllowAnonymous();`, `app.MapOpenApi().AllowAnonymous();`,
  `app.MapScalarApiReference().AllowAnonymous();`, and — for row 12 — a route-pattern convention on
  the builder `MapRazorComponents<App>()` returns, exactly as Spike C2 verified it:

  ```csharp
  app.MapRazorComponents<App>()
      .AddInteractiveServerRenderMode()
      .Add(endpointBuilder =>
      {
          string? pattern = (endpointBuilder as RouteEndpointBuilder)?.RoutePattern.RawText;

          if (pattern is not null && pattern.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase))
          {
              endpointBuilder.Metadata.Add(new AllowAnonymousAttribute());
          }
      });
  ```

  Nothing is added for row 11 — Spike C1 landed on C-a. The comment block at lines 130–138 is
  rewritten: the bodiless-challenge argument it makes has now expired and the events that replace it
  must be named there.
- `src/Envanex.Web/Authentication/EnvanexAuthenticationEvents.cs` — **modified** — gains the bearer
  half: `JwtBearerEvents.OnChallenge` and `OnForbidden` writing `application/problem+json`. The
  cookie half shipped in Phase 3.
- `src/Envanex.Web/Extensions/JwtAuthenticationExtensions.cs` — **modified** — wires the bearer
  events; the PR 6a comment at lines 28–29 is deleted, its promise now kept.
- `src/Envanex.Web/Extensions/ResultExtensions.cs` — **modified** — `GetReasonPhrase` gains
  `StatusCodes.Status403Forbidden => "Forbidden"` and changes from `private` to `internal` so the
  events class builds its ProblemDetails through the same function rather than a copy. No entry is
  added to `StatusCodeMap`, so `ResultMappingTests.ResultMapping_NoMappingEntry_ShouldBeOrphaned`
  stays green — exactly as Decision 3 predicts.
- `src/Envanex.Web/Controllers/AuthController.cs` — **modified** — `[AllowAnonymous]` on the class,
  with a comment naming Decision 14 for `Logout`.
- `src/Envanex.Web/Controllers/ProductsController.cs` — **modified** — `[Authorize(Policy = EnvanexPolicies.CanRead)]`
  on `GetById` and `GetDataSource`; `[Authorize(Policy = EnvanexPolicies.CanWrite)]` on `Create`,
  `Update`, `Activate`, `Deactivate`.
- `src/Envanex.Web/Controllers/UnitOfMeasuresController.cs` — **modified** —
  `CanRead` on `GetById` and `List`; `CanWrite` on `Create`.
- `src/Envanex.Web/Components/Pages/NotFound.razor` — **modified** — `@attribute [AllowAnonymous]`
  (Spike D landed on D1, so the attribute is the mechanism).
- `src/Envanex.Web/Components/Pages/Error.razor` — **modified** — same mechanism.
- `src/Envanex.Web/Components/Pages/Login.razor` — already exempt from Phase 3.
- `src/Envanex.Web/Components/Pages/Home.razor` — already `CanRead` from Phase 3; unchanged here.

### Signatures

```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // The two named policies were registered in Phase 3, when Home.razor first needed CanRead.
    options.AddPolicy(EnvanexPolicies.CanRead,  policy => policy.RequireRole(EnvanexRoles.Administrator, EnvanexRoles.Viewer));
    options.AddPolicy(EnvanexPolicies.CanWrite, policy => policy.RequireRole(EnvanexRoles.Administrator));
});
```

**`DefaultPolicy` is deliberately left alone, and the reason is not `Home.razor`.** The codebase
carries exactly one policy-less `[Authorize]`: `SignOut.razor`, added in Phase 3, because a user
with no role must still be able to sign out. Raising `DefaultPolicy` to require a role would
therefore break sign-out today, for exactly the users most likely to need it — an authenticated
user whose roles were revoked. It would also set a trap for later: an `[Authorize]` written in PR 8
to mean "signed in" would silently mean "has a role",
and `AuthorizeView` with no policy would start hiding UI from authenticated users for a reason
nobody wrote down. Leaving it at `RequireAuthenticatedUser()` keeps `DefaultPolicy` and
`FallbackPolicy` saying the same thing — "authentication is the floor" — and keeps every role
requirement written in the place it is read, which is what Decision 2 asks for.

**`OnChallenge` calls `context.HandleResponse()`** after writing its ProblemDetails, because
otherwise the handler continues and appends its own headers and empty body. The consequence is that
**no `WWW-Authenticate` header is emitted on any 401 in this application.** No existing test asserts
that header (a repo-wide search under `tests/` returns no match), so nothing breaks — but exemption
row 2's test is rewritten because of it, see below. The challenge body is a ProblemDetails with the
Turkish title "Kimlik doğrulaması gerekli."; the forbid body's title is "Bu işlem için yetkiniz
yok."; both take their reason phrase from `GetReasonPhrase`.

**The rule these bodies exist to satisfy: once a response has started, no re-execution happens at
all.** That is the whole of it, and it is narrower than either sentence the plan previously carried.
"4xx and 5xx are re-executed, a 302 is not, so a challenge and a 404 are mutually exclusive" is
wrong, and so is the repair that was first proposed for it — "the final status is always the
original denial's". `UseStatusCodePagesWithReExecute` restores the original status *before* it runs
the re-executed request, and that request then runs through the **whole** pipeline and can set a
status of its own. Spike C's five-row table records two cases where it did: a `400` from
`/_blazor/disconnect` came back to the client as a bodiless `401` because the re-executed
`/not-found` was itself denied, and a bearer `401` on an unmatched `/api/*` path came back as a
`302` to `/login?ReturnUrl=%2Fnot-found` because `/not-found` is not under `/api/*`, so the selector
forwarded the re-executed request to the cookie scheme. The original status survives only when the
re-executed page renders normally.

Writing a body is what takes the request out of that machinery entirely, which is why **every**
rejection path in this PR carries one — `OnChallenge` and `OnForbidden` on the bearer side,
`OnRedirectToAccessDenied` on the cookie side (Phase 3) — and why **exemption row 4 on `/not-found`
is load-bearing rather than cosmetic**: for as long as the re-execution target is closed, any 4xx
produced anywhere in the application is silently rewritten into whatever denying `/not-found`
produces. Both facts were observed, not reasoned: see the spike note's "Standing observation" and
the disconnect finding under C2.

### The complete `[AllowAnonymous]` exemption list

| # | Exemption | Mechanism | Proving test |
|---|---|---|---|
| 1 | `POST /api/auth/login` | `[AllowAnonymous]` on `AuthController` | `Login_WithoutAuthentication_ShouldReturn200` |
| 2 | `POST /api/auth/refresh` | same attribute | `Refresh_WithoutAuthentication_ShouldReturn401CarryingTheInvalidRefreshTokenDetail` |
| 3 | `POST /api/auth/logout` (Decision 14) | same attribute | `Logout_WithoutAuthentication_ShouldReturn204` |
| 4 | `/not-found` | `@attribute [AllowAnonymous]` (Spike D → D1) | `NotFoundPage_WithoutAuthentication_ShouldReturn200` |
| 5 | the 404 re-execution path | **none is possible — the challenge precedes the 404, so the 404 re-execution path is only reachable for an authenticated caller** (Spike C3 → C3-a) | `UnknownPath_WithoutAuthentication_ShouldRedirectToTheLoginPage` and `UnknownPath_WhileSignedIn_ShouldReturn404AndRenderTheNotFoundPage` |
| 6 | `/Error` | `@attribute [AllowAnonymous]`, as row 4 | `ErrorPage_WithoutAuthentication_ShouldReturn200` |
| 7 | `/login` (**extends Decision 1's enumeration**; required by Decision 15) | `@attribute [AllowAnonymous]`, as row 4 | `LoginPage_WithoutAuthentication_ShouldReturn200` |
| 8 | static assets | `app.MapStaticAssets().AllowAnonymous()` | `StaticAsset_WithoutAuthentication_ShouldReturn200` (`GET /favicon.png`, referenced unfingerprinted in `App.razor`) |
| 9 | `/openapi/v1.json`, Development only | `app.MapOpenApi().AllowAnonymous()` | `OpenApiDocument_InDevelopment_WithoutAuthentication_ShouldReturn200` |
| 10 | Scalar reference, Development only | `app.MapScalarApiReference().AllowAnonymous()` | `ScalarReference_InDevelopment_WithoutAuthentication_ShouldReturn200` |
| 11 | `_framework/blazor.web.js` | **a consequence of row 8** — the script is served out of the asset manifest and flips from 401 to 200 the moment `MapStaticAssets()` carries `.AllowAnonymous()` (Spike C1 → C-a). No production line of its own | `BlazorFrameworkScript_WithoutAuthentication_ShouldReturn200` |
| 12 | the `/_blazor` endpoints — **negotiate, the transport endpoint and disconnect, all three** | **its own mechanism** (Spike C2 → C-b): the route-pattern convention in the Files list above. `MapRazorComponents<App>().AllowAnonymous()` is the wrong one — the spike observed it opening `/` and every other page as a side effect | `BlazorHubEndpoints_WithoutAuthentication_ShouldNotReturn401`, a `[Theory]` with one `[InlineData]` per endpoint |
| 13 | unmatched `/api/*` paths | none — the selector forwards to bearer, so the answer is a 401 with a body rather than a redirect. Listed because it is a behaviour change, not an exemption | `UnknownApiPath_WithoutAuthentication_ShouldReturn401ProblemJson` |

Row 2's test no longer asserts the absence of `WWW-Authenticate`, because `OnChallenge` calls
`HandleResponse()` and no 401 in this application carries that header — the assertion would stay
green with the exemption removed and would prove nothing. It asserts instead that the 401 body's
ProblemDetails detail is the Turkish `Auth.InvalidRefreshToken` message produced by
`ResultExtensions`. Remove `[AllowAnonymous]` from `AuthController` and the body becomes the
challenge's "Kimlik doğrulaması gerekli.", which is red.

Rows 9 and 10 need a host running as `Development`; that is the second constructor added to
`EnvanexWebApplicationFactory` in Phase 2, not a fourth factory type. **A Development host differs in
more than OpenAPI:** `app.UseExceptionHandler("/Error")` and `UseHsts()` are registered only outside
Development (`Program.cs:108-113`), so an unhandled exception in that host surfaces as a raw 500 or
a developer exception page rather than the `/Error` component. An unexpected 500 in
`DevelopmentEndpointExemptionTests` is that, not an exemption failure.

Rows 11 and 12 exist because `App.razor` loads the framework script on **every** page and
`MapRazorComponents<App>().AddInteractiveServerRenderMode()` maps `/_blazor`, and neither carries
authorization metadata — so the fallback policy reaches both. If the script answers 401, the login
page loses enhanced navigation immediately and no interactive component can ever establish a
circuit there. Spike C settled them separately, and they landed on different answers.

**Row 11 is free.** `_framework/blazor.web.js` came back 401 under the fallback policy and 200 the
moment row 8's `.AllowAnonymous()` was added, with the manifest's fingerprint `ETag` and
`Last-Modified` — the same response shape as `/app.css`. It is served by the static-asset endpoint,
so row 8 already covers it and row 11 adds documentation plus its regression test.

**Row 12 costs a mechanism.** None of the three hub endpoints is in the asset manifest and none
carries framework-supplied anonymous metadata; all three still answered a bodiless 401 after row 8.
`MapRazorComponents<App>().AddInteractiveServerRenderMode().AllowAnonymous()` does open them — and
opens `/` and `/spike-form` with them, which defeats Decision 1 outright. The route-pattern
convention in the Files list is what the spike verified instead: it opened negotiate (200), the
transport endpoint (404, no such circuit — authorization passed) and disconnect, and left `/` and
the spike page at 401. Row 12 probes all three, because an exemption scoped to negotiate alone still
breaks a circuit while a negotiate-only test stays green.

Two things the spike recorded that this phase has to carry:

- **Disconnect needs row 4.** Once anonymous, `POST /_blazor/disconnect` answers `400` (no circuit
  id), and while `/not-found` was still closed that 400 came back as a bodiless 401 — the
  re-execution rule above, in miniature. Exempting `/not-found` restored the 400. Row 12's test and
  row 4's exemption are not independent.
- **The convention sees nine route patterns**, and they are the exact surface `MapRazorComponents<App>()`
  maps: `/_framework/opaque-redirect`, `/Error`, `/`, `/not-found`, `/spike-form` (the throwaway
  spike page, which will be `/login` and `/sign-out` here), `/_blazor/negotiate`, `/_blazor`,
  `/_blazor/disconnect/` and `/_blazor/initializers/`. Note the trailing slashes, which is why the
  filter is a `StartsWith("/_blazor")` prefix rather than a set of literals. Note also that
  `/_framework/opaque-redirect` is a **razor-components** endpoint, not a static asset, so row 8
  does not reach it and the `/_blazor` prefix does not either: it stays closed, and nothing in this
  PR requests it.

**Do not write this phase without the spike note open beside it.**

### Tests to add and change

`tests/Envanex.IntegrationTests/Api/AuthPipelineTests.cs` — **modified, 5 cases → 7** (**+2**).
Four of the five existing cases change.
- `Refresh_WithUnknownToken_ShouldReturn401ProblemJsonAndNotTheNotFoundPage` — **unchanged**.
- `OpenEndpoint_WithNoAuthorizationHeader_ShouldStillReturn200` → renamed and inverted to
  `ProtectedEndpoint_WithNoAuthorizationHeader_ShouldReturn401ProblemJsonAndNotTheNotFoundPage`.
- `OpenEndpoint_WithAMalformedBearerToken_ShouldStillReturn200` → renamed and inverted to
  `ProtectedEndpoint_WithAMalformedBearerToken_ShouldReturn401ProblemJson`.
- `OpenEndpoint_WithAnExpiredBearerToken_ShouldStillReturn200` → renamed and inverted to
  `ProtectedEndpoint_WithAnExpiredBearerToken_ShouldReturn401ProblemJson`.
- `OpenEndpoint_WithAValidBearerToken_ShouldStillReturn200` → becomes
  **`ProtectedEndpoint_WithAValidBearerTokenCarryingNoRole_ShouldReturn403ProblemJsonAndNotTheNotFoundPage`**
  — the token the class hand-builds (`AuthPipelineTests.cs:126-130`) carries only `sub` and `jti`,
  so it authenticates and then fails `CanRead`. **This is the bearer half of Decision 3.**
- **added:** `ProtectedEndpoint_WithAValidBearerTokenCarryingTheAdministratorRole_ShouldReturn200` —
  the positive control. The private `CreateToken(bool expired)` helper gains a
  `params string[] roles` parameter.
- **added:** `UnknownApiPath_WithoutAuthentication_ShouldReturn401ProblemJsonAndCarryNoLocationHeader`
  — an anonymous `GET /api/auth/register`, asserting 401, `application/problem+json`, and that the
  response carries **no `Location` header**. See "Why the no-`Location` case exists" below; it is
  here rather than in `AnonymousExemptionTests` because it guards the events this class is about.
- The class doc comment is rewritten: it no longer says "no endpoint is `[Authorize]`".

#### Why the no-`Location` case exists

It looks like a restatement of exemption row 13 and is not one, so the reason is written here rather
than left to be re-derived.

Spike C3's probe 7, run against the configuration Phase 4 actually ships **minus** the bearer
events, observed `GET /api/auth/register` answer **`302 Found`, `Location:
/login?ReturnUrl=%2Fnot-found`** — an unmatched API path redirecting a client to an HTML login page,
which is precisely what Decision 4 forbids. It happens by accident and through a route nobody
designed: the bearer handler challenges with a bodiless 401, the response has therefore not started,
`UseStatusCodePagesWithReExecute` re-executes the request as `/not-found`, `/not-found` is **not**
under `/api/*`, so the selector forwards the re-executed request to the **cookie** scheme, and the
cookie scheme redirects. The redirect overwrites the 401.

`OnChallenge` is the only thing that stops it: it writes a body, the response starts, and a started
response is never re-executed. **So `OnChallenge` is load-bearing for `/api/*` 404 handling, not a
nicety about response bodies** — and that is a claim about a mechanism, which is what this case
pins. Row 13's case asserts the *behaviour* an operator reads off the exemption table (an unmatched
API path answers 401 with a problem+json body, not 404); this case asserts the *absence of the
redirect*, names probe 7 in its comment, and is the one that fails if a later change drops the body
from `OnChallenge` or removes its `HandleResponse()`. Row 13's assertion was narrowed to status and
content type so the two do not overlap.

`tests/Envanex.IntegrationTests/Api/AuthApiTests.cs` — **modified, +1.**
`Register_ShouldReturn404` (`AuthApiTests.cs:365-378`) is an anonymous GET of an unmatched `/api/*`
path. Once the gate closes it answers **401**, not 404 — `[AllowAnonymous]` on `AuthController`
cannot reach it because no action matches, and the fallback policy denies before routing can report
the miss. Spike C3's probe 7 observed exactly that.
**It becomes two cases, and neither is a duplicate of the other.** The gate splits one test into
two because it splits the fact the test was proving. Before PR 6b a single anonymous 404 said both
"the gate does not open this path" and "there is no registration endpoint". After PR 6b an anonymous
caller cannot tell those apart — an unmatched path and a real endpoint that rejects anonymous
callers both answer 401 — so each property needs the caller that can still see it.

- **`Register_ShouldReturn401`** — anonymous, **401 with an `application/problem+json` body**,
  renamed from `Register_ShouldReturn404` to say so. It guards the **gate**: this path is covered by
  the fallback policy and answers a body-carrying 401 rather than leaking a 404 or a redirect.
- **`Register_WhileAuthenticated_ShouldReturn404`** — **added** (`+1`), with
  `using var client = await _fixture.CreateAdministratorClientAsync();`. An authenticated caller
  satisfies the fallback policy, so routing reports the miss and the answer is 404; a registration
  endpoint that existed would answer 405, or 200/400, but not 404. It guards **PR 6a's Decision 11 —
  no endpoint creates a user** — and it is the only test in the repository that does. An
  authenticated caller is now the only caller who can see the difference, which is why this case
  cannot be folded back into the one above and must not be deleted as a duplicate of it: they
  assert the same URL for opposite reasons, one that the door is shut and one that there is no room
  behind it.
- The comment is rewritten, once, above the pair. The GET-not-POST paragraph stays (it is still
  true, and it is still why the probe is a GET: an unmatched `POST /api/*` falls through to the
  Blazor catch-all and is rejected by antiforgery with 400 before routing reports a miss). A second
  paragraph replaces the old reasoning: it says which property each of the two cases guards, and
  names the other two anonymous probes of this same path and why each of those is distinct from
  `Register_ShouldReturn401` — exemption row 13's
  `UnknownApiPath_WithoutAuthentication_ShouldReturn401ProblemJson` (the exemption table is
  complete: it is 401, not 404), and `AuthPipelineTests`'
  `UnknownApiPath_WithoutAuthentication_ShouldReturn401ProblemJsonAndCarryNoLocationHeader` (the
  mechanism: `OnChallenge`'s body is what keeps it from being a 302).

`tests/Envanex.IntegrationTests/Blazor/CookieAuthPipelineTests.cs` (**modified, +1**)
- **`ApiPath_WithASessionCookieAndNoBearerToken_ShouldReturn401AndNotARedirect`** — Decision 4's
  end-to-end proof: the exact case a header-presence selector would have got wrong. It is the
  **only** end-to-end guard on Decision 4: Phase 3's mutation proofs changed the selector to key on
  `Authorization`-header presence and observed every cookie test stay green, leaving
  `SchemeSelectionTests`' direct call to the selector as the sole guard. This test must therefore
  actually reach the selector — a signed-in cookie client with no bearer token requesting a
  protected `/api/*` endpoint, asserting 401 with no `Location` header rather than a 302 — and not
  merely assert a status that any denial would produce.

The two cookie cases this class already carries from Phase 3 (the role-less 403 and the Viewer 200)
are unchanged by the gate. `Cookie_AnonymousRequestToAProtectedPage_ShouldRedirectToTheLoginPage` is
deliberately **not** added: it is the same HTTP request as
`AuthenticatedShellTests.GetHome_WhileSignedOut_ShouldRedirectToTheLoginPage`, which already exists.

`tests/Envanex.IntegrationTests/Api/AnonymousExemptionTests.cs` (**created, +14**) — rows 1–8, 11, 12
and 13 of the table above, method names as listed there. Row 5 contributes two cases and row 12
three, one `[InlineData]` per hub endpoint. Notable bodies:
- `UnknownPath_WithoutAuthentication_ShouldRedirectToTheLoginPage` — row 5's anonymous half, settled
  by Spike C3's re-run as **302**, not 404: the cookie scheme challenges a non-`/api/*` path before
  routing can report a miss, and a 302 is not a 4xx, so `UseStatusCodePagesWithReExecute` never
  fires. The observed `Location` is `/login?ReturnUrl=%2F<original path>`, so the case may assert
  the `ReturnUrl` as well as the status — worth doing, because it is what proves the redirect
  targets the login page rather than merely being a redirect.
- `UnknownPath_WhileSignedIn_ShouldReturn404AndRenderTheNotFoundPage` — row 5's other half, and the
  case that makes row 4's exemption matter: for an authenticated caller the challenge does not
  happen, routing reports the miss, and the 404 re-execution path is reachable at last.
- `BlazorFrameworkScript_WithoutAuthentication_ShouldReturn200` — `GET /_framework/blazor.web.js`
  signed out. The regression this guards is the login page losing enhanced navigation.
- `BlazorHubEndpoints_WithoutAuthentication_ShouldNotReturn401` — `POST
  /_blazor/negotiate?negotiateVersion=1`, `GET /_blazor?id=…` and `POST /_blazor/disconnect`, signed
  out. Asserted as "not 401" rather than a specific success code: the responses are framework-owned,
  and the spike recorded 200, 404 (no such circuit) and 400 (no circuit id) respectively — a 401 is
  the failure this test exists to catch, and pinning the other three would make it a test of the
  framework.
- `UnknownApiPath_WithoutAuthentication_ShouldReturn401ProblemJson` — row 13, narrowed to status and
  content type. The absence of a `Location` header is asserted by `AuthPipelineTests` instead, for
  the reason given there.

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
dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~AuthPipelineTests|FullyQualifiedName~CookieAuthPipelineTests|FullyQualifiedName~AnonymousExemptionTests|FullyQualifiedName~AuthorizationPolicyTests|FullyQualifiedName~AuthApiTests"
dotnet run --project src\Envanex.Web --launch-profile http   # manual: /api/products/datasource in a browser answers 401 JSON, not the login page
```

Expected test count at end: **627** (120 + 126 + **381**). The three cases over the plan's earlier
624 are `AuthPipelineTests`' no-`Location` case,
`AuthApiTests.Register_WhileAuthenticated_ShouldReturn404`, and the Phase 1 role-seeder race test
carried forward; neither spike branch moved a count.
**No schema, no migration, no query — db-reviewer not required for this phase.**

---

# Phase 5: The read-only demo account

Decision 7 and Decision 12. Spike E landed on **E1**: the marker row written between `app.Build()`
and `app.Run()` was there when a `WebApplicationFactory` client queried for it, so `HostFactoryResolver`
does not cut the entry point short. `await app.SeedIdentityAsync();` stays in that position, and
`IdentitySeedingHostedService` / `AddEnvanexIdentitySeeding` are **not** created.

### Files

- `src/Envanex.Infrastructure/Identity/DemoAccountOptions.cs` — **created**.
- `src/Envanex.Infrastructure/Identity/DemoAccountSeeder.cs` — **created** — idempotent; ensures the
  two roles through `IdentityRoleSeeder`, then the demo user, then its `Viewer` membership.
- `src/Envanex.Web/Extensions/IdentitySeedingExtensions.cs` — **created** — one call site, invoked
  from `Program.cs` between `app.Build()` and `app.Run()` (Spike E → E1).
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
    // Two shapes on purpose, and which is which is not left to the coder:
    //   - Result: an already-existing demo account, or an IdentityResult failure, is an expected
    //     outcome of seeding and is returned.
    //   - throw (InvalidOperationException): Demo:Enabled=true with a blank or policy-violating
    //     password is a MISCONFIGURATION, not a business outcome, and follows JwtOptionsGuard.
    //     A misconfigured public demo must fail at boot rather than hand out an account whose
    //     password nobody chose.
    //   - A null services reference is a caller bug and throws ArgumentNullException.
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
only when `Demo:Enabled` is true.

Note for the test author: the shared fixture's `ResetIdentityAsync` deletes the roles this seeder
created at host startup, and `IdentitySeeder.EnsureRoleAsync` (Phase 2) is what puts them back. A
Demo-enabled factory must therefore be constructed **inside** `InitializeAsync`, after any reset, so
that its startup seeding is not deleted out from under the test — the same per-test construction
`LoginRateLimitedWebApplicationFactory` already uses.

### Tests to add

`tests/Envanex.IntegrationTests/Identity/DemoAccountSeederTests.cs` (**created, +5**)
- `SeedAsync_WhenDemoIsDisabled_ShouldSeedTheRolesButNotTheAccount`
- `SeedAsync_WhenDemoIsEnabled_ShouldCreateTheAccountInTheViewerRole`
- `SeedAsync_CalledTwice_ShouldSucceedAndShouldNotDuplicateTheAccount`
- `SeedAsync_WhenDemoIsEnabledWithABlankPassword_ShouldThrow` — renamed from "…ShouldThrowAtStartup",
  because it calls `DemoAccountSeeder.SeedAsync` directly and asserts the exception, not a host boot.
- **`DemoAccount_ShouldBeAbleToReadButNotWrite`** — end to end over HTTP against a host built from
  `new EnvanexWebApplicationFactory(conn, "Testing", new Dictionary<string, string?> { ["Demo:Enabled"] = "true", ["Demo:Password"] = SqlServerFixture.SeededPassword })`:
  `POST /api/auth/login` with the configured password → 200, `GET /api/unit-of-measures` → 200,
  `POST /api/unit-of-measures` → 403. This case is what Spike E exists to protect, and E1 is why it
  can be written against the startup call site at all: a factory-created client sees the seeding.

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
dotnet run --project src\Envanex.Web --launch-profile http   # manual: sign in at /login as the demo account, confirm read-only
```

Expected test count at end: **634** (120 + 126 + **388**).

**Run db-reviewer on this phase** — the seeder writes `auth.AspNetRoles`, `auth.AspNetUsers` and
`auth.AspNetUserRoles` at host startup, in production.

---

## Expected test counts, by phase

One column, because Phase 0 settled every branch: **A1**, **B1 with the GET guard**, **C-a**,
**C-b**, **C3-a**, **D1** and **E1**. The two sub-outcome columns the plan used to carry are gone —
B1's guard keeps its case rather than dropping one, and D1 adds no convention test, so neither delta
was ever taken.

| Phase | Domain | Application | Integration | Total |
|---|---|---|---|---|
| baseline | 120 | 126 | 316 | 562 |
| 0 spikes | 120 | 126 | 316 | 562 |
| 1 role claim | 120 | 126 | 327 | **573** |
| 2 authenticated clients | 120 | 126 | 332 | **578** |
| 3 cookie + Blazor | 120 | 126 | 353 | **599** |
| 4 close the gate | 120 | 126 | 381 | **627** |
| 5 demo account | 120 | 126 | 388 | **634** |

Every row from Phase 1 onward is one higher than the plan first wrote it, because Phase 1 added one
test the plan did not anticipate: `IdentityRoleSeederTests`'
`EnsureRolesAsync_RunByTwoHostsAtOnce_ShouldNotThrowAndShouldLeaveExactlyTwoRoles`, which runs two
seeders concurrently against the same empty `auth.AspNetRoles` so that the seeder's unique-violation
clause on `RoleNameIndex` is proved reachable by a real race rather than left as an unexercised
defensive branch.

Phases 4 and 5 are three higher than the plan's earlier 624 / 631: two of the three are in Phase 4 —
`AuthPipelineTests`' no-`Location` case, and the split of `Register_ShouldReturn404` into an
anonymous 401 case and an authenticated 404 case — and the third is the Phase 1 race test above. No
spike outcome moved a count.

Watch the integration suite's wall clock. ADR 0007 set 60 seconds as the point where it becomes a
decision. The lazy per-collection token cache is what keeps this PR's addition to a handful of
PBKDF2 pairs; if the suite crosses 60 s, the cause to check first is a class calling
`ResetIdentityAsync` more often than it needs to.

## Rollback notes

- Every phase is a separate commit on `feat/authz`; `git revert` of any single one leaves the
  solution building and green, because no phase depends on a later one to repair a test it broke.
- Phase 3 is the first phase that denies anybody anything: it closes the landing page behind
  `CanRead`. Reverting it reopens `/` and removes the login page with it. Phase 4 is still the
  riskiest revert — it reopens every API endpoint. Phases 1 and 2 are purely additive and enforce
  nothing.
- **No migration is created, so there is nothing to roll back in the database.** Reverting Phase 5
  leaves the seeded roles and demo user in place; they are harmless once no policy names them, and
  the test database is ephemeral.
- Phase 0 is done and none of the reshaped variants is in play: B1 keeps `Login.razor` doing its own
  sign-in (no `MapPost("/login")` endpoint), D1 keeps page permissions in the `.razor` files (no
  convention class), and E1 keeps seeding at the `app.Build()`/`app.Run()` call site (no hosted
  service). Phases 3, 4 and 5 have one shape each.

## Known gaps this PR opens, for the roadmap table

- `/api/*` is bearer-only by construction (Decision 4). A browser holding a session cookie cannot
  call the REST surface, including `/api/products/datasource`. This is the deliberate price of
  making CSRF structurally impossible there rather than mitigated. Nothing needs it today: the
  Radzen grid runs in the Blazor Server circuit and calls the Application layer directly per
  Decision 10. **It reopens on `showcase/devexpress`** — a DevExpress Blazor client rebuilt against
  the same `DevExtreme.AspNet.Data` protocol would have to hold a bearer token in the browser,
  which is the browser-token question the cookie scheme was chosen to avoid. That branch is never
  merged, so the question is recorded rather than answered.
- No 401 in this application carries a `WWW-Authenticate` header, because `OnChallenge` calls
  `HandleResponse()` in order to write a ProblemDetails body. That is a deviation from RFC 7235 for
  a machine client that looks for the header to discover the scheme. No client here does; record it
  and revisit if an external consumer appears.
- The browser access-denied experience is a bare 403 body, not a page: Decision 3 requires a
  body-carrying 403, and writing that body from `OnRedirectToAccessDenied` means the markup is a
  string literal in an events class rather than a `.razor` file — the one place in this codebase
  where user-facing markup escapes Razor. The shape that gives both a correct status and a real
  page is `UseStatusCodePagesWithReExecute("/status/{0}")`, re-executing an `/access-denied`
  component while keeping the 403. Adopt it in PR 7, when the UI gets its second screen and the
  machinery pays for itself. This gap is reachable from Phase 3 onward, since `Home.razor` requires
  `CanRead`.
- `AuthorizeRouteView`'s `NotAuthorized` template is unreachable for statically rendered pages — the
  endpoint layer answers first, as Spike D part 1 observed. It is carried because Decision 8 requires it
  and PR 7's interactive components make it live. Until then it is untested markup, and the comment
  in `Routes.razor` is what stops it being deleted as dead code.
- The roadmap row "No `JwtBearerEvents.OnChallenge` body" closes in Phase 4 and should be struck.
- The roadmap row "No authentication or authorization on any endpoint" closes in Phases 3 and 4.
- The roadmap row about PR 6a's unrun manual smoke steps closes in Phase 3/Phase 5.
- `Demo:Password` joins `Jwt:SigningKey` as a value the first deploy must set in App Service
  configuration — add it to the PR 7 deployment row.
- `ReturnUrl` is not honoured. The cookie handler writes `?ReturnUrl=<path>` on its redirect to
  `/login`, and the login page ignores it and always redirects to `/` after a successful sign-in.
  Honouring it needs open-redirect validation — a `ReturnUrl` pointing off-site must not be
  followed — and the tests that prove it, neither of which this plan carries. Its own chore, no PR
  number.

---

# Disagreement with the fifteen decisions

Planned as written above regardless. Three items, and three corrections that are not disagreements.

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

**RULING — rejected. Path-prefix forwarding stands.** The reason is one the planner did not give:
if the cookie never authenticates `/api/*`, CSRF on the REST surface is **structurally impossible**
rather than merely mitigated. A browser cannot be induced to make an authenticated cross-site call
to an endpoint that does not read its cookie, whatever the request looks like. The controllers
carry no antiforgery of their own — `UseAntiforgery` guards the Blazor endpoints, not
`MapControllers` — and `SameSite=Lax` reduces that exposure without removing it. The multi-scheme
alternative would make every write endpoint reachable with an ambient cookie and no token, which is
the classic CSRF shape, and would then need antiforgery added across the REST surface to be safe.
Removing the attack beats defending against it.

The DevExpress concern is real but has no consumer today: the Radzen grid runs inside the Blazor
Server circuit and calls the Application layer directly per Decision 10, so nothing in this
repository makes a browser-side call to `/api/*`. The consequence is recorded in the known-gaps
list above, naming `showcase/devexpress` as the branch where the question reopens.

## Disagreement 2 — Decision 3's cookie-side 403 puts a Turkish user-facing string in C#

Requiring a body-carrying 403 from `OnRedirectToAccessDenied` means the access-denied page for a
browser is a string literal in an events class, not a Razor page — the one place in this codebase
where user-facing markup escapes `.razor`. `AccessDeniedPath` to a Razor page is the shape the
framework offers and gives a real page, at the cost of a 302 instead of a 403. I think the honest
resolution is a 403 that *re-executes* an `/access-denied` component, which gives both, but it is
more machinery than this PR should carry. Planned as written; I would revisit it in PR 7 when the
UI gets its second screen.

**RULING — planned as written, gap named precisely.** The known-gaps list above now records that
the browser access-denied experience is a bare 403 body rather than a page, and names
`UseStatusCodePagesWithReExecute("/status/{0}")` as the shape that gives both a correct status and
a real Razor page, to be adopted in PR 7.

## Disagreement 3 — Decision 6, as implemented, is "one real login per class", not "per test"

Decision 6 says the affected tests authenticate by logging in for real, and the plan honours that:
every token comes from a real `POST /api/auth/login` through the real endpoint with real PBKDF2 and
full issuer/audience/lifetime/signature validation on use. But the token is cached on the fixture,
so 37 `ProductsDatasourceTests` share one login rather than performing 37. If the human's intent was
literally one login per test, say so and the cache comes out — the cost is roughly 14 seconds added
to a 44-second suite, which crosses the 60-second line ADR 0007 set as a decision point. I think the
cache is right and the two cache tests
(`AccessToken_AfterResetIdentityAsync_ShouldBeReissuedRatherThanReused` and
`AccessToken_WhenTheCachedTokenIsNearExpiry_ShouldBeRemintedRatherThanReused`) are what make it
safe, but the deviation should be a choice rather than something noticed in review.

**RULING — rejected. The cache stands.** The decision was "log in for real", not "log in once per
test". A cached real token still passes full issuer, audience, lifetime and signature validation on
every use, which is the property the decision exists to protect; fourteen seconds would cross the
60-second line. Recorded beside the cache in Phase 2 as point 7 so it reads as a ruling.

## Correction 1 — the blast radius is 77 executed tests, not 72 (not a disagreement)

The research's table lists five classes totalling 72; of those, 71 actually go red when the gate
closes — `RateLimiterTests.SharedFactory_WithGlobalLimiterDisabled_ShouldNotRegisterAGlobalLimiter`
(`RateLimiterTests.cs:110-120`) reads options and makes no HTTP call. The research does not count
`AuthPipelineTests`, four of whose five cases assert `GET /api/unit-of-measures` returns 200 with no
credentials, a malformed token, an expired token and a valid token respectively; all four change
meaning. Nor does it count `AuthApiTests.Register_ShouldReturn404` (`AuthApiTests.cs:365-378`),
whose anonymous GET of an unmatched `/api/*` path stops answering 404. Seventy-two plus four plus
one is **77 touched tests**; the earlier figures of 72 and 76 are both superseded by the table in
Phase 2.

## Correction 2 — Decision 9's mechanism rate-limits GETs of the login page (not a disagreement)

A Blazor page endpoint serves GET and POST. `[EnableRateLimiting("login")]` on `Login.razor`
therefore spends a login permit every time the page is *rendered*, so five reloads would lock login
out for five minutes in production (5 per 300 s). The plan adds a method guard inside the existing
`"login"` policy delegate so the limit applies to POST only; this keeps Decision 9's "same named
policy" intact and leaves `AuthController.Login`, which is POST-only, unaffected. **Spike B confirmed
it: the third consecutive GET of the attributed page answered 429.** The guard is required and the
correction is no longer a prediction.

## Correction 3 — Decision 1's exemption list needs one more entry (not a disagreement)

The enumerated list is "the three auth endpoints, `/not-found`, the error page, static assets, and
the Development-only OpenApi and Scalar endpoints". `/login` must be added, or Decision 15's "an
anonymous visitor sees the login page" cannot hold. The plan adds it and names its test, along with
the framework script, the three `/_blazor` endpoints and the unmatched-`/api/*` behaviour that
Decision 1 also did not enumerate.

---

# Response to the review

**1 — the cookie 403 is unreachable.** Fixed as ruled: `Home.razor` now carries
`@attribute [Authorize(Policy = EnvanexPolicies.CanRead)]`, which makes the cookie forbid reachable
and `Cookie_AuthenticatedUserWithNoRole_ShouldReturn403WithABodyAndNotTheNotFoundPage` a real test.
Because a named policy must be registered before an attribute names it, `EnvanexPolicies` and the
two `AddPolicy` calls moved from Phase 4 into Phase 3, and Decision 3's cookie half
(`OnRedirectToAccessDenied` plus its test) moved with them — each half of Decision 3 now ships with
its first reachable failure. Phase 4's `DefaultPolicy` justification was rewritten: it no longer
rests on `Home.razor`, but on the fact that the codebase now has no policy-less `[Authorize]` at
all, so raising `DefaultPolicy` would be inert today and a trap for PR 8.

**2 — no test evaluates `RequireRole` against a cookie identity.** Fixed by adding
`CookieAuthPipelineTests.Cookie_AuthenticatedViewer_ShouldReadTheHomePage` in Phase 3, which
together with the role-less 403 case evaluates `RequireRole(Administrator, Viewer)` against a cookie
identity carrying `ClaimTypes.Role` and gets opposite answers from the two identities. The claim in
Phase 1 now names those two tests (and the Phase 1 bearer test) instead of gesturing at Phase 4.

**3 — `AuthApiTests.Register_ShouldReturn404`.** Added to Phase 4's change list. It switches to an
authenticated administrator client and keeps asserting 404, which preserves the non-routability fact
the test exists for; the anonymous 401 becomes exemption row 13 with its own case. The comment
rewrite is specified. Blast radius corrected to 77 in Phase 2's new table, in Correction 1, and
everywhere 72 or 76 appeared. **(Revised when Phase 0's outcomes were folded in: the case splits in
two. `Register_ShouldReturn401` keeps the anonymous client and expects 401 with problem+json;
`Register_WhileAuthenticated_ShouldReturn404` keeps the 404 with an administrator client. The
review's answer was right about the authenticated probe and incomplete about the anonymous one —
after the gate closes, the two facts need two callers.)**

**4 — exemption row 5 has no mechanism.** The unmatched-path question is now Spike C3, with two
`curl` probes (non-API and API), its raw response recorded, and two named outcomes C3-a/C3-b that
select row 5's mechanism text and its two test names. Row 5 now carries two cases: the anonymous
probe and a signed-in probe that makes row 4's exemption matter.

**5 — every spike command targets port 5000.** All URLs are now `http://localhost:5216`, every spike
runs `dotnet run --project src\Envanex.Web --launch-profile http` (stated once, with the reason:
under the `http` profile no HTTPS port is discoverable so `UseHttpsRedirection` no-ops), `/dev/null`
became `NUL`, and Spike A's cookie jar is the relative `cookies.txt` written from a scratch
directory rather than a dead session GUID path.

**6 — Phase 0 contradicts itself.** The phase now says "seven questions, carried by five spikes",
the note is renamed `thoughts/shared/debug/2026-09-20_pr6b-blazor-auth-spikes.md`, and the git check
is scoped to `git status --porcelain -- src tests` (must be empty) with a second command showing
that the one expected new file is the note under `thoughts/`.

**7 — `SecurePolicy = Always` versus the http TestServer.** Fixed with a named mechanism rather than
a warning: `CookieSecurePolicyResolver` binds `Auth:Cookie:SecurePolicy` (absent → `Always`, so
production is unchanged), `EnvanexWebApplicationFactory` sets it to `SameAsRequest`, both are in
Phase 3's Files list, and `CookieSchemeOptionsTests` (+3) proves the default, the throw on a typo,
and the test host's value — so deleting the override fails with a message that names the cause
rather than reddening twelve cookie tests.

**8 — the stated mechanism contradicts the test.** Made a spike (Spike D) with two probes and two
named outcomes, as instructed. Both outcomes select the same *mechanism* — endpoint metadata — and
differ only in whether `.razor` attributes supply it (D1) or a `BlazorPageAuthorizationConvention`
must (D2); under both, `GetHome_WhileSignedOut_ShouldRedirectToTheLoginPage` holds and
`AuthorizeRouteView`'s `NotAuthorized` template is unreachable under static SSR. The old
"works through `AuthorizeRouteView` without any fallback policy" sentence is gone, the template is
kept with a comment recording why it is carried, and exemption rows 4/6/7 now name Spike D's choice
instead of assuming `[AllowAnonymous]` on a `.razor` file works.

**N1 — `TokenValidationParameters` assignment order.** Fixed by folding `RoleClaimType` and
`NameClaimType` into the existing object initializer at `JwtAuthenticationExtensions.cs:33-46`, with
`MapInboundClaims` called out separately as not being part of that object.

**N2 — exemption row 2 may not prove the exemption.** Stated that `OnChallenge` calls
`HandleResponse()` and that consequently no 401 in the application emits `WWW-Authenticate`; row 2's
test is renamed to `Refresh_WithoutAuthentication_ShouldReturn401CarryingTheInvalidRefreshTokenDetail`
and asserts the ProblemDetails detail, which does distinguish the two 401s. The header's absence is
recorded as a known gap.

**N3 — Spike B alternative not propagated.** The count table carried two extra columns
(B sub-outcome −1, D2 +1) through Phases 3, 4 and 5, with a sentence saying they are additive and
why they are tracked to the end. (Both columns were dropped when Phase 0's outcomes were folded in:
B1 kept its case and D1 added none, so neither delta was ever taken.)

**N4 — the 72 is over-counted by one.** Phase 2's new blast-radius table records 72 cases in five
classes of which 71 go red, names `SharedFactory_WithGlobalLimiterDisabled_ShouldNotRegisterAGlobalLimiter`
as the exception and why it is still counted (its class's client changes).

**N5 — an already-done change was asked for.** `RateLimiterTests` is removed from the "moves to
`InitializeAsync`" list; its entry now says line 32 and line 128 only, noting that line 20 already
declares `null!`.

**N6 — the token cache has no expiry.** The cache entry became `CachedToken(string Token,
DateTimeOffset ExpiresAt)` read from the token's `exp`, a hit within 60 s of expiry counts as a
miss, and `AccessToken_WhenTheCachedTokenIsNearExpiry_ShouldBeRemintedRatherThanReused` plus the
`ExpireCachedToken` test seam prove it (Phase 2 is now +5).

**N7 — no shared cookie-sign-in helper.** `tests/Envanex.IntegrationTests/Fixtures/CookieAuthHelper.cs`
is created in Phase 3 with `AntiforgeryFieldName`, `ReadAntiforgeryTokenAsync`, `SignInAsync` and
`CreateNonRedirectingClient`, and it is named as the one place a test signs in.

**N8 — `AllowAutoRedirect`.** `CookieAuthHelper.CreateNonRedirectingClient` sets
`new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }` with a comment explaining
that every 302 assertion depends on it, and that `HandleCookies` stays at its default.

**N9 — row 12 probes one of several endpoints.** Spike C2 now curls negotiate, the transport
endpoint and disconnect separately, and row 12's test is a `[Theory]` with one `[InlineData]` per
endpoint (`BlazorHubEndpoints_WithoutAuthentication_ShouldNotReturn401`), which is +3 rather than
+1 in the counts.

**N10 — startup seeding under `WebApplicationFactory`.** Added as Spike E, with a throwaway marker
row and a throwaway probe test, and an E2 branch that moves seeding into an `IHostedService` (which
does run under the factory) leaving Phase 5's tests and counts unchanged.

**N11 — `SeedAsync_…ShouldThrowAtStartup` and the mixed contract.** Renamed to
`SeedAsync_WhenDemoIsEnabledWithABlankPassword_ShouldThrow`, and the signature comment now states
which shape each case uses: `Result` for an existing account or an `IdentityResult` failure,
`InvalidOperationException` for a misconfigured password, `ArgumentNullException` for a null
services reference.

**N12 — the Development host differs in more than OpenAPI.** Added a sentence under rows 9–10 noting
that `UseExceptionHandler("/Error")` and `UseHsts()` are absent under Development
(`Program.cs:108-113`), so an unexpected 500 there is not an exemption failure.

**N13 — Phase 2's new writing tests need a business reset.** `AuthenticatedClientTests` is now
specified to call `_fixture.ResetAsync()` first in `InitializeAsync` and to use unit-of-measure
codes unique to the class, with the 409-instead-of-201 failure named.

**N14 — Spike B names settings but no mechanism.** The exact command line is given
(`dotnet run … --launch-profile http -- --RateLimiting:Login:Enabled=true …`), and the note that
`RateLimiting:Enabled` defaults to true at 100/60 s in Development is stated rather than left to be
discovered.

**N15 — "ResultMappingTests's fourth fact".** Replaced with the method name
`ResultMapping_NoMappingEntry_ShouldBeOrphaned`, read from
`tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs:109`.
