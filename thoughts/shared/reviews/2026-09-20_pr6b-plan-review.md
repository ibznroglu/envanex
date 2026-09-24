# Plan review — PR 6b authorization and Blazor auth

Plan reviewed:
`thoughts/shared/plans/2026-09-20_pr6b-authorization-and-blazor-auth.md` (1050 lines)
Research: `thoughts/shared/research/2026-09-20_authorization-and-blazor-auth.md`

I have no shell. Every claim below that rests on framework runtime behaviour is marked UNVERIFIED
with the command that settles it. Claims with a file:line into the repository are read directly
from the file.

## What checks out

- Test-count arithmetic is internally consistent. I added every phase: Phase 1 +10 (2+2+2+2+1+1)
  → 326/572; Phase 2 +4 → 330/576; Phase 3 +15 (5+2+2+2+2+2) → 345/591; Phase 4 +24
  (1+3+10+3+6+1) → 369/615; Phase 5 +7 → 376/622. All match the summary table at plan:909-917.
  Baseline 120/126/316 matches research:36-39.
- The three `AuthenticatedUser` construction sites the plan names are the only ones.
  `JwtAccessTokenIssuerTests.cs:25-26`, `LoginCommandHandlerTests.cs:18`,
  `RefreshTokenCommandHandlerTests.cs:17`. `FakeIdentityService`, `FakeAccessTokenIssuer` and
  `FakeRefreshTokenService` only mention the type; none constructs it. `JwtAccessTokenIssuerTests`
  does carry exactly 8 `[Fact]`s. The "eight cases, one-line break" claim at plan:274-277 is
  correct.
- `RefreshTokenService.cs:176` is exactly the line the plan cites. `IdentityService.cs:90-92` is
  exactly where `GetRolesAsync` belongs.
- `Envanex.Web.csproj:4` has `InternalsVisibleTo Envanex.IntegrationTests`, so making
  `GetReasonPhrase` internal (plan:669-673) is testable. `ResultExtensions.cs:189-197` confirms
  there is no 403 arm today.
- `AddRoles<IdentityRole<Guid>>()` is already present (`IdentityInfrastructureExtensions.cs:52-53`)
  with `AddEntityFrameworkStores`, so `RoleManager` resolves. `EnvanexUser` is public
  (`EnvanexUser.cs:11`), so `CookieSignInService`'s constructor (plan:520-523) compiles from
  `Envanex.Web`.
- `ArchitectureTests.cs:54-68` forbids auth packages in `Envanex.Application`;
  `EnvanexRoles`/`EnvanexClaimTypes` as plain strings do not trip it.
- No existing test constrains the authentication scheme registration (no match for
  `DefaultScheme`/`JwtBearerOptions`/`AuthenticationOptions` anywhere under `tests/`), so Phase 3's
  move from `"Bearer"` to the selector breaks nothing unnamed. No existing test GETs `/`,
  `/not-found`, `/Error` or a static asset, so Phase 3's `[Authorize]` on `Home.razor` breaks
  nothing unnamed.
- Phase 2's three cross-minting tests (plan:443-448) genuinely fail if the behaviour they guard is
  removed: minting through the rate-limited host spends a permit and turns the "two succeed"
  assertion red; deleting the cache-clear makes the two `jti` values equal. Those are honest tests.
- The bearer 403 test (plan:752-754) is honest: `AuthPipelineTests.cs:126-130` shows the hand-built
  token carries only `sub` and `jti`, so it authenticates and fails `CanRead`; remove `CanRead`
  from `UnitOfMeasuresController.List` and it goes 200 and red.

---

## Blocking findings

### 1. The cookie-side 403 is unreachable, so `Cookie_AuthenticatedUserWithNoRole_ShouldReturn403WithABodyAndNotTheNotFoundPage` cannot pass

plan:761-764 — the test signs in as `SqlServerFixture.RoleLessEmail`, requests `/`, and asserts
403. But plan:503 puts `@attribute [Authorize]` on `Home.razor`, and plan:709-710 states explicitly
that `DefaultPolicy` is deliberately left alone so that `[Authorize]` means "signed in", not "has a
role". The fallback policy (plan:700-703) is `RequireAuthenticatedUser()` and nothing more. No page
component anywhere in the plan carries `[Authorize(Policy = CanRead)]` or `CanWrite`, and `/api/*`
never reads the cookie by construction (plan:565-568).

Therefore an authenticated-but-role-less cookie user satisfies every non-`/api/*` requirement in
the application. `GET /` answers 200, not 403. Consequences:

- The cookie half of Decision 3 has no reachable code path at all.
- `CookieAuthenticationEvents.OnRedirectToAccessDenied` (plan:663) is dead code — precisely the
  situation the PR 6a comment at `Program.cs:134-138` complains about for `OnChallenge`.
- The known-gap entry at plan:946-952 ("the browser access-denied experience is a bare 403 body")
  describes an experience no user can reach.

This is read straight from the plan; no framework knowledge is needed.

**Required:** name a non-`/api/*` endpoint that carries a role-requiring policy (the natural
candidate is a page, since PR 7 adds the product grid), point the test at it, and say which policy
it carries. Or, if the answer is that the cookie 403 is genuinely unreachable in this PR, say so
and move Decision 3's cookie half to the phase that first creates a role-gated page — but then
`OnRedirectToAccessDenied` and its test must move with it.

### 2. plan:267-268 claims Phase 4 proves the two-claim-type equivalence. No Phase 4 test does.

plan:264-268 states that a cookie identity (`ClaimTypes.Role`) and a bearer identity (`"role"`)
satisfy the same `RequireRole`, and says "Phase 4 proves this with a test rather than leaving it as
an assumption."

Every case in `AuthorizationPolicyTests` (plan:784-794) is named `…WithAnAdministratorToken` /
`…WithAViewerToken` — bearer only. Combined with finding 1, nothing in the plan ever evaluates
`RequireRole` against a cookie identity.

UNVERIFIED (two premises): that `IdentityBuilder.AddRoles<TRole>()` at
`IdentityInfrastructureExtensions.cs:52` replaces the registered
`IUserClaimsPrincipalFactory<EnvanexUser>` with the role-aware
`UserClaimsPrincipalFactory<EnvanexUser, IdentityRole<Guid>>`; and that
`ClaimsIdentity.RoleClaimType` survives the cookie ticket serialise/deserialise round trip. Both
are plausible, neither is provable from this repository.

Settling command: once a role-gated page exists,
`dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~CookieAuthPipelineTests"`.
A cheaper interim probe: a test that signs in through the form and asserts the resulting
principal's `IsInRole(EnvanexRoles.Administrator)`.

**Required:** either add that test to Phase 3/4 and name it, or delete the claim at plan:267-268.

### 3. `AuthApiTests.Register_ShouldReturn404` is an unnamed casualty of Phase 4

`tests/Envanex.IntegrationTests/Api/AuthApiTests.cs:365-378`:

```
// Probed with GET rather than POST on purpose. ... A POST could not prove it: an unmatched
// POST falls through to the Blazor catch-all and is rejected by UseAntiforgery with 400
// before routing ever reports the miss.
var response = await _client.GetAsync("/api/auth/register");
response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
```

This is an anonymous request to an unmatched path. `[AllowAnonymous]` on `AuthController`
(plan:674) does not reach it — no action matches. The class's own comment (line 373) records that
unmatched `/api/*` requests reach a Blazor catch-all endpoint, which under Phase 4 carries no
exemption; and if instead no endpoint matches, the fallback policy still applies. Either way the
answer becomes 401, not 404.

Phase 4's "Tests to add and change" (plan:739-801) does not list `AuthApiTests` at all. The blast
radius is therefore at least 77, not the 76 that plan:1027-1034 corrected it to.

UNVERIFIED (which of the two paths applies). Settling command: with the fallback policy temporarily
in `Program.cs`, `curl -i http://localhost:5216/api/auth/register` signed out — see finding 5 on
the port.

**Required:** add `AuthApiTests.Register_ShouldReturn404` to Phase 4's change list with its new
expected status and a rewritten comment, and correct the blast-radius number.

### 4. Exemption row 5 has no mechanism, and its proving test asserts the outcome finding 3 says is wrong

plan:720 — row 5, "the 404 re-execution path | consequence of #4 |
`UnknownPath_WithoutAuthentication_ShouldReturn404AndNotRedirectToLogin`".

Row 4 exempts `/not-found`, which `NotFound.razor:1` declares as `@page "/not-found"` — a different
endpoint from whatever serves the unknown path. Making `/not-found` anonymous does nothing for the
request that produced the 404 in the first place; that request must pass authorization before it
can be answered 404, and `UseStatusCodePagesWithReExecute` (`Program.cs:127`) only re-executes
4xx/5xx, not the 302 a cookie challenge produces.

This is the same unresolved question as rows 11 and 12, and it carries the same trap Spike C2 names
at plan:158-160: whatever exempts the catch-all must not exempt every page component.

**Required:** fold this into Spike C as a third question (C3), with its own raw response code
recorded, and make row 5's mechanism column name the mechanism the spike selects rather than
"consequence of #4".

### 5. Every Phase 0 spike command targets a port the host does not listen on

plan:70, plan:71, plan:142, plan:143, plan:144 all use `http://localhost:5000`.

`src/Envanex.Web/Properties/launchSettings.json:8` — the first profile ("http", the one
`dotnet run --project src\Envanex.Web` selects with no `--launch-profile`) binds
`http://localhost:5216`. The https profile binds `https://localhost:7012;http://localhost:5216`.
Port 5000 appears nowhere. Every spike curl would fail to connect. A spike that cannot run is worse
than no spike, because Phases 3 and 4 are written to branch on its outcome.

Two follow-ons:

- plan:142 uses `-o /dev/null` inside what plan:179-185 frames as a PowerShell block. On Windows
  `curl.exe` that should be `NUL`.
- UNVERIFIED: if the https profile is used instead, `app.UseHttpsRedirection()` (`Program.cs:128`)
  sits before `UseAuthentication`/`UseAuthorization` (lines 139-140) and would answer 307 to every
  probe, making Spike C's 200-vs-401 observation impossible. Under the http profile no HTTPS port
  is discoverable, so the middleware should no-op. Settling command:
  `curl -i http://localhost:5216/login` while the host runs under each profile in turn.

**Required:** correct the port to 5216, state the profile explicitly, and say which null device.

### 6. Phase 0 contradicts itself about how many spikes it has and what it leaves behind

- plan:53 — "Two questions must be answered"; plan:59 — "the two observations". There are three
  spikes (plan:62, plan:89, plan:122).
- plan:58 creates `thoughts/shared/debug/2026-09-20_blazor-antiforgery-and-rate-limit-spike.md`.
  plan:183 requires `git status --porcelain` to be empty "before the phase is called done". A newly
  created note makes it non-empty. One of the two is wrong.
- The note's slug names only antiforgery and rate limiting; Spike C's observations have no home in
  the filename.

**Required:** say "three questions", rename the note, and restate the git check as "no change under
`src/` or `tests/`" or equivalent.

### 7. `SecurePolicy = Always` versus the http TestServer — every cookie test in Phases 3-5 rests on it

plan:570 lists `SecurePolicy=Always` among the cookie options. `WebApplicationFactory`'s default
client uses `BaseAddress = http://localhost`, and `Program.cs:128`'s `UseHttpsRedirection` has no
HTTPS port under TestServer, so requests stay http.

UNVERIFIED: whether `HttpClient`'s `CookieContainer` replays a cookie marked `Secure` on an http
URI. The standard behaviour is that it does not — which would make every one of `LoginPageTests`
(plan:593-599), `SignOutPageTests` (plan:601-603), `AuthenticatedShellTests` (plan:605-607),
`CookieAuthPipelineTests` (plan:760-767) and `DemoAccount_ShouldBeAbleToReadButNotWrite` fail for a
reason that has nothing to do with the code under test.

Settling command: after Phase 3,
`dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~LoginPageTests"`; or a
one-line probe that asserts the `Set-Cookie` from a form POST is replayed on the next GET.

**Required:** state how the test host gets a replayable cookie — `SecurePolicy` bound to
configuration with `SameAsRequest` under Testing, or an explicit factory-level override — and put
it in Phase 3's Files list, not left for the coder to discover.

### 8. Phase 3's stated mechanism for `[Authorize]` on `Home.razor` contradicts Phase 3's own test

plan:472-473: "`Home.razor` gets `@attribute [Authorize]` (Decision 15), which works through
`AuthorizeRouteView` without any fallback policy."

plan:607: `GetHome_WhileSignedOut_ShouldRedirectToTheLoginPage`.

`AuthorizeRouteView` renders the `NotAuthorized` template and answers 200; a redirect can only come
from endpoint authorization metadata plus `cookie.LoginPath` (plan:574). Both statements cannot be
true. Which one holds also decides whether the `NotAuthorized` template the plan adds at
plan:487-489 is reachable at all for statically-rendered pages — and, downstream, whether
`[AllowAnonymous]` on `.razor` files is a valid exemption mechanism for rows 4, 6 and 7
(plan:719-722), which the whole Phase 4 exemption table assumes it is.

UNVERIFIED. Settling command: after Phase 3, `curl -i http://localhost:5216/` signed out, or
`dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~AuthenticatedShellTests"`.

**Required:** pick one mechanism, state it, and make the test and the `NotAuthorized` template
consistent with it. If `[AllowAnonymous]` on a `.razor` page is the exemption mechanism for rows
4/6/7, then endpoint metadata is the mechanism and plan:472-473 is simply wrong.

---

## Non-blocking findings

**N1 — `TokenValidationParameters` assignment order.** plan:256-262 says the three lines go "inside
the `AddJwtBearer` callback". `JwtAuthenticationExtensions.cs:33-46` assigns a whole new
`TokenValidationParameters` object. Placed before that assignment, `RoleClaimType` and
`NameClaimType` are silently discarded and `BearerRoleClaimTests` (plan:296-302) goes red with a
misleading message. Say "after the existing assignment", or fold both into the object initializer.

**N2 — exemption row 2's test may not prove the exemption.** plan:717:
`Refresh_WithoutAuthentication_ShouldReturn401InvalidRefreshTokenAndNoWwwAuthenticateHeader`.
UNVERIFIED: if Phase 4's `OnChallenge` (plan:661-663) calls `context.HandleResponse()`, the handler
does not append `WWW-Authenticate`, so removing `[AllowAnonymous]` from `AuthController` would
leave the header assertion green; only the `InvalidRefreshToken` detail would distinguish the two
401s, and the plan never says the test asserts it. Settling command: run the test with
`[AllowAnonymous]` removed as a deliberate mutation —
`dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~AnonymousExemptionTests"`.
State whether `OnChallenge` emits `WWW-Authenticate` and what row 2's test asserts on the body.

**N3 — the Spike B alternative is not propagated past Phase 3.** plan:915 records "591 (590 under
the Spike B sub-outcome)" but plan:916-917, plan:814 and plan:900 give 615 and 622 with no
alternative. Under the sub-outcome they are 614 and 621.

**N4 — the 72 is over-counted by one.** research:154 and plan:371 count `RateLimiterTests` as 5.
`RateLimiterTests.cs:110-120`
(`SharedFactory_WithGlobalLimiterDisabled_ShouldNotRegisterAGlobalLimiter`) makes no HTTP call and
does not break when the gate closes. 71 switch over, 1 is untouched. Harmless to the totals.

**N5 — plan:385-388 asks for a change that is already done.** `RateLimiterTests.cs:20` already
declares `private HttpClient _client = null!;` and line 32 already assigns it in `InitializeAsync`.

**N6 — the token cache has no expiry.** plan:337-361 keys by email and clears only in
`ResetIdentityAsync`. `EnvanexWebApplicationFactory.cs:39` sets `Jwt:AccessTokenMinutes=15` and
`JwtAuthenticationExtensions.cs:45` sets `ClockSkew = TimeSpan.Zero`. A run where more than 15
minutes elapse between a cache fill and its last use — a cold CI agent pulling the SQL Server
image, a debugger paused at a breakpoint — hands out an expired token and fails an arbitrary subset
of the 72 with 401. Cache the `ExpiresAt` alongside the token and re-mint inside a minute of
expiry. (The cache/reset alignment itself is sound: `ResetIdentityAsync` is the only deleter, every
caller is in `DatabaseCollection` — `DatabaseCollection.cs:5-7` — so the clear and the deletes
cannot be interleaved by another test.)

**N7 — no shared cookie-sign-in helper is named.** "Signs in through the login form" is needed by
at least plan:595, plan:602, plan:606, plan:762 and plan:880: GET the page, parse the hidden field
whose name Spike A records (plan:86-87), POST it, keep the cookie. `SqlServerFixture` is listed as
modified only in Phase 2 (plan:379). Name the helper and its home, or it will be written four
different ways.

**N8 — `AllowAutoRedirect` is never mentioned.** `CreateClient()` follows redirects by default.
Every case asserting a 302 (plan:595, plan:603, plan:607, plan:720, plan:765, plan:766) needs
`new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }`. UNVERIFIED as stated but
standard; settled by
`dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~SignOutPageTests"`.

**N9 — row 12's proving test probes one of several `/_blazor` endpoints.** plan:727 and plan:773
assert on `POST /_blazor/negotiate`. The long-poll/WebSocket endpoint itself and
`/_blazor/disconnect` are separate; an exemption scoped to negotiate alone still breaks a circuit
and the test stays green. Name the endpoints the spike actually records.

**N10 — Phase 5 depends on startup seeding executing under `WebApplicationFactory`.** plan:830-831
puts `await app.SeedIdentityAsync();` between `app.Build()` and `app.Run()`; plan:879-881 is an
end-to-end test against a `Demo:Enabled=true` host. UNVERIFIED that code between `Build()` and
`Run()` runs under `WebApplicationFactory<Program>` — `HostFactoryResolver`/`HostingListener`
capture the host at the `HostBuilt` diagnostic event and the entry point's continuation is not
guaranteed. research:236-237 asserts it does, without evidence, and the research's own "Not
verified" section does not list it. Settling command:
`dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~DemoAccountSeederTests"`,
or, cheaper and earlier, a throwaway probe in Phase 0 that writes a row from that position and
asserts a factory-created client can read it. If it does not run, Phase 5's call site and test list
both change.

**N11 — `SeedAsync_WhenDemoIsEnabledWithABlankPassword_ShouldThrowAtStartup`** (plan:878) is named
for startup but exercises `DemoAccountSeeder.SeedAsync` directly (plan:853). Separately, `SeedAsync`
returning `Result` while also throwing on a blank password is a mixed contract; `JwtOptionsGuard` is
a fair precedent, but say which of the two shapes the blank-password check uses so the coder does
not guess.

**N12 — the Development host differs in more than OpenAPI.** plan:729-730 builds a Development host
for rows 9-10. Under Development, `app.UseExceptionHandler("/Error")` and `UseHsts()` are not
registered (`Program.cs:108-113`). Worth one sentence so an unexpected 500 in
`DevelopmentEndpointExemptionTests` is not read as an exemption failure.

**N13 — Phase 2's new writing tests need a business reset.** plan:443-444 does two POSTs and expects
201/201/429. `AuthenticatedClientTests` is not said to call `_fixture.ResetAsync()`; a leftover
unit-of-measure code from an earlier class turns a 201 into a 409 and the test into a false red.

**N14 — Spike B names settings but no mechanism to set them.** plan:92-93 says "run the host with
`RateLimiting:Login:Enabled=true`, `PermitLimit=2`, `WindowSeconds=60`" without saying how
(`dotnet run -- --RateLimiting:Login:PermitLimit=2`, user-secrets, or environment variables). Also
worth noting that `RateLimiting:Enabled` defaults to true at 100/60 s in Development
(`Program.cs:31-33`), which is harmless for three requests but should be said rather than
discovered.

**N15 — "ResultMappingTests's fourth fact"** (plan:672-673) is an ordinal reference.
`tests/Envanex.IntegrationTests/Api/ResultMappingTests.cs` carries 5 `[Fact]`/`[Theory]` attributes
while research:248-250 describes four facts. Name the test method instead of its position.

---

## Required changes

1. Give the cookie scheme a reachable forbid: name a non-`/api/*` endpoint carrying a
   role-requiring policy, point `Cookie_AuthenticatedUserWithNoRole_ShouldReturn403…` at it, and
   reconcile it with plan:709-710. (Finding 1)
2. Add a test that evaluates `RequireRole` against a cookie identity, or delete the claim at
   plan:267-268. (Finding 2)
3. Add `AuthApiTests.Register_ShouldReturn404` (`AuthApiTests.cs:365-378`) to Phase 4's change list
   with its new expected status, and correct the blast radius from 76. (Finding 3)
4. Turn the unmatched-path question into Spike C3 and make exemption row 5's mechanism column name
   whatever the spike selects, instead of "consequence of #4". (Finding 4)
5. Fix every spike URL to `http://localhost:5216`, name the launch profile, and replace
   `/dev/null` with `NUL`. (Finding 5)
6. Fix Phase 0's "two questions"/"two observations" to three, rename the debug note, and restate
   the `git status --porcelain` check so it does not contradict plan:58. (Finding 6)
7. State how the integration test host obtains a replayable auth cookie given
   `SecurePolicy = Always`, and put it in Phase 3's Files list. (Finding 7)
8. Decide and state the single mechanism by which `[Authorize]` on `Home.razor` denies an anonymous
   SSR request, and make plan:472-473, plan:607 and the `NotAuthorized` template consistent with
   it. (Finding 8)
9. Specify that the `RoleClaimType`/`NameClaimType`/`MapInboundClaims` lines go after the existing
   `TokenValidationParameters` assignment. (N1)
10. State whether `OnChallenge` emits `WWW-Authenticate` and what exemption row 2's test asserts on
    the body. (N2)
11. Propagate the Spike B sub-outcome counts to Phases 4 and 5 (614/621), or state that they are
    not tracked past Phase 3. (N3)
12. Add token expiry to the fixture cache. (N6)
13. Name the shared cookie-sign-in helper and its file, and state the `AllowAutoRedirect = false`
    client option. (N7, N8)
14. Either cite evidence that startup seeding runs under `WebApplicationFactory` or add it to
    Phase 0 as a fourth observation, since Phase 5's end-to-end test depends on it. (N10)

## Verdict

NEEDS_REVISION
