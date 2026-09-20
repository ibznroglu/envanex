# PR 6b Phase 0 — spike observations

Run date: 2026-09-21. Branch: `feat/authz`, working tree clean at `8f9fc7c`.

Plan: `thoughts/shared/plans/2026-09-20_pr6b-authorization-and-blazor-auth.md`, Phase 0.

Every observation below was produced against a real host started with

```powershell
dotnet run --project C:\projects\envanex\src\Envanex.Web --launch-profile http
```

which binds `http://localhost:5216` only, so `app.UseHttpsRedirection()` logs
`Failed to determine the https port for redirect` and no-ops. All probes are Windows `curl.exe`
run from a scratch directory outside the repository.

Throwaway code used and then removed: `src/Envanex.Web/Components/Pages/SpikeForm.razor` (deleted),
`src/Envanex.Web/Program.cs` (edited, then `git checkout --`), and
`tests/Envanex.IntegrationTests/Spikes/StartupSeedingProbeTests.cs` (deleted).
`git status --porcelain -- src tests` is empty at the end of the phase.

## Verdict table

| Question | Branch selected |
|---|---|
| A — antiforgery in a statically rendered `EditForm` | **A1**, field name `__RequestVerificationToken` |
| B — `[EnableRateLimiting]` on a page component | **B1 with the GET guard** |
| C1 — the framework script | **C-a** (covered by exemption row 8, `MapStaticAssets().AllowAnonymous()`) |
| C2 — the three `/_blazor` endpoints | **C-b** (each needs its own exemption; `MapRazorComponents<App>().AllowAnonymous()` is the wrong mechanism) |
| C3 — the unmatched non-`/api/*` path | **NEITHER C3-a NOR C3-b — see "C3 does not match a planned branch" below. STOPPED, not adjudicated.** |
| C3, probe 7 — the unmatched `/api/*` path | **a bodiless 401** (one of the three answers the plan anticipated) |
| D — an authorization attribute on a `.razor` page | **D1** |
| E — code between `app.Build()` and `app.Run()` under `WebApplicationFactory` | **E1** |

---

## Spike A — antiforgery in a statically rendered `EditForm`

`SpikeForm.razor` at `@page "/spike-form"`, an
`<EditForm Model="Model" FormName="spike" method="post" OnValidSubmit="Submit">` with one
`InputText` bound through `[SupplyParameterFromForm]` and a `[CascadingParameter] HttpContext?`.
No `<AntiforgeryToken />` child was written.

### Step 3 — `curl -i -c cookies.txt http://localhost:5216/spike-form`

```
HTTP/1.1 200 OK
Content-Type: text/html; charset=utf-8
Date: Sun, 20 Sep 2026 22:47:47 GMT
Server: Kestrel
Cache-Control: no-cache, no-store
Pragma: no-cache
Set-Cookie: .AspNetCore.Antiforgery.U4m2qrp4QYE=CfDJ8IbIphT39VBJpzKT7i2v1Qv0XNJSDazPpDdCTqe3bAKnyIFZq0kk1vTud6zakI0eRBBaSxYhJE17j3faENM9TN3sXwW8BPT2xTGrWKmdAlY-mUVetN2qisOOZjW5mokLyaso0VMnoTCBA1lwGB2s-E4; path=/; samesite=strict; httponly
Transfer-Encoding: chunked
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
Content-Security-Policy: frame-ancestors 'self'
blazor-enhanced-nav: allow
```

Every `<input>` the page rendered:

```html
<form method="post" action="/spike-form">
<input type="hidden" name="_handler" value="spike" />
<input type="hidden" name="__RequestVerificationToken" value="CfDJ8IbIphT39VBJpzKT7i2v1QsAaHfvv7q50t_jQfVQvLnImSqQaTj6CiYe1Xi38aaT31owGm6ufuDAqMqVxq97eam8CWUr0a4nn-YziCdimLdgA1bmA2uvdZIklA7_gC2d3uwjqjD58sr2Wi7nP3Sgjdc" />
<input name="Model.Text" class="valid" />
```

Both hidden fields are emitted by `EditForm` itself. Nothing had to ask for them.

### Step 4 — POST with the cookie and the token field

```
curl_status=200
HTTP/1.1 200 OK
Content-Type: text/html; charset=utf-8
Date: Sun, 20 Sep 2026 22:47:56 GMT
Server: Kestrel
Cache-Control: no-cache, no-store
Pragma: no-cache
Transfer-Encoding: chunked
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
Content-Security-Policy: frame-ancestors 'self'
blazor-enhanced-nav: allow
```

Body proved the handler actually ran, not merely that the page re-rendered:

```html
<p id="result">submitted:x ctx:True</p>
```

`ctx:True` — the `[CascadingParameter] HttpContext?` is non-null during a static-SSR form post,
which is what `CookieSignInService.SignInAsync` will need in Phase 3 to write `Set-Cookie`.

### Step 5 — the same POST with the token field removed

```
curl_status=400
HTTP/1.1 400 Bad Request
Content-Type: text/html; charset=utf-8
Date: Sun, 20 Sep 2026 22:48:00 GMT
Server: Kestrel
Transfer-Encoding: chunked
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
Content-Security-Policy: frame-ancestors 'self'
blazor-enhanced-nav: allow

A valid antiforgery token was not provided with the request. Add an …
```

### Branch

**A1 — the token field is rendered automatically.** Phase 3 proceeds as written: `Login.razor` is an
`<EditForm FormName="login">` and no `<AntiforgeryToken />` child is needed.

`CookieAuthHelper.AntiforgeryFieldName` = **`__RequestVerificationToken`**.
The form-handler field is `_handler`, whose value is the `FormName` (`spike` here, `login` in
Phase 3). A form-posting test must send both.

Side observation worth keeping: the 400 carried a body and was **not** rewritten by
`UseStatusCodePagesWithReExecute`, because the antiforgery middleware writes the body itself.

---

## Spike B — `[EnableRateLimiting]` on a page component

`@attribute [EnableRateLimiting(AuthController.LoginRateLimitPolicy)]` added to `SpikeForm.razor`.
Host started with

```powershell
dotnet run --project src\Envanex.Web --launch-profile http -- `
  --RateLimiting:Login:Enabled=true --RateLimiting:Login:PermitLimit=2 --RateLimiting:Login:WindowSeconds=60
```

The global limiter is also on in Development at 100 per 60 s (`Program.cs:31-33`), so three
requests cannot trip it; every 429 below is the `"login"` policy.

### Three GETs of `/spike-form`

```
=== GET #1 ===
curl_status=200
HTTP/1.1 200 OK
=== GET #2 ===
curl_status=200
HTTP/1.1 200 OK
=== GET #3 ===
curl_status=429
HTTP/1.1 429 Too Many Requests
```

GET #3, raw headers:

```
HTTP/1.1 429 Too Many Requests
Content-Type: application/problem+json
Date: Sun, 20 Sep 2026 22:48:48 GMT
Server: Kestrel
Retry-After: 60
Transfer-Encoding: chunked
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
```

### Three POSTs of `/spike-form`

The host was restarted between the two sequences so that the GET sequence could not spend the
POST sequence's permits. The limiter is in-memory and resets; the antiforgery cookie/token pair
survives, because the Development data-protection keys are persisted to disk.

```
=== POST #1 ===
curl_status=200
HTTP/1.1 200 OK
=== POST #2 ===
curl_status=200
HTTP/1.1 200 OK
=== POST #3 ===
curl_status=429
HTTP/1.1 429 Too Many Requests
```

POST #3, raw headers and body:

```
HTTP/1.1 429 Too Many Requests
Content-Type: application/problem+json
Date: Sun, 20 Sep 2026 22:49:01 GMT
Server: Kestrel
Retry-After: 60
Transfer-Encoding: chunked
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin

{"type":"https://httpstatuses.io/429","title":"\u00C7ok fazla istek g\u00F6nderildi.","status":429,"detail":"\u0130stek s\u0131n\u0131r\u0131 a\u015F\u0131ld\u0131. L\u00FCtfen bir s\u00FCre bekleyip tekrar deneyin."}
```

POST #1 and #2 really submitted, they did not merely render:

```html
bpost1.html:<p id="result">submitted:x ctx:True</p>
bpost2.html:<p id="result">submitted:x ctx:True</p>
```

### Branch

**B1 with the GET guard.** The attribute reaches endpoint metadata, and the component endpoint
serves GET and POST from the same endpoint, so **a GET of the login page spends a login permit**.

Phase 3 therefore **must** add the method guard inside the `"login"` policy delegate in
`Program.cs` — return `RateLimitPartition.GetNoLimiter(partitionKey)` when
`context.Request.Method` is not `POST` — and **must keep** the test
`BlazorLoginPage_RepeatedGets_ShouldNotSpendLoginPermits`. No phase count falls.

Side observation: the 429 was not rewritten by `UseStatusCodePagesWithReExecute` either, because
`OnRejected` writes the ProblemDetails body.

---

## Spike C — the fallback policy against the script, `/_blazor`, and unmatched paths

`Program.cs` temporarily carried the Phase 4 fallback policy and nothing else:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

At Phase 0 the default scheme is still bearer and no `OnChallenge` body exists yet, so every
denial below is the framework's bodiless bearer challenge. Read every status line with that in
mind — it is the single reason the C3 branches do not fit (below).

### Probes 1 and 2, signed out, no `AllowAnonymous` anywhere

```
=== C probe 1: GET /_framework/blazor.web.js ===
401
=== C probe 2: GET /login ===
401
```

(`/login` does not exist yet, so probe 2 is an unmatched path; it is reported here for
completeness and its real content is probe 6's.)

### Probe 3 — `POST /_blazor/negotiate?negotiateVersion=1`

```
curl_status=401 size=0
HTTP/1.1 401 Unauthorized
Content-Length: 0
Date: Sun, 20 Sep 2026 22:49:42 GMT
Server: Kestrel
WWW-Authenticate: Bearer
WWW-Authenticate: Bearer
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
```

### Probe 4 — `GET /_blazor?id=00000000000000000000000000000000`

```
curl_status=401 size=0
HTTP/1.1 401 Unauthorized
Content-Length: 0
Date: Sun, 20 Sep 2026 22:49:42 GMT
Server: Kestrel
WWW-Authenticate: Bearer
WWW-Authenticate: Bearer
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
```

### Probe 5 — `POST /_blazor/disconnect`

```
curl_status=401 size=0
HTTP/1.1 401 Unauthorized
Content-Length: 0
Date: Sun, 20 Sep 2026 22:49:42 GMT
Server: Kestrel
WWW-Authenticate: Bearer
WWW-Authenticate: Bearer
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
```

### Probe 6 — `GET /gibberish-unmatched-path`, unmatched, non-`/api/*`

```
curl_status=401 size=0
HTTP/1.1 401 Unauthorized
Content-Length: 0
Date: Sun, 20 Sep 2026 22:49:48 GMT
Server: Kestrel
WWW-Authenticate: Bearer
WWW-Authenticate: Bearer
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
```

### Probe 7 — `GET /api/auth/register`, unmatched, under `/api/*`

```
curl_status=401 size=0
HTTP/1.1 401 Unauthorized
Content-Length: 0
Date: Sun, 20 Sep 2026 22:49:48 GMT
Server: Kestrel
WWW-Authenticate: Bearer
WWW-Authenticate: Bearer
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
```

Two supporting probes, same configuration:

```
=== GET /not-found directly (under the fallback policy) ===
curl_status=401 size=0
HTTP/1.1 401 Unauthorized
Content-Length: 0
Date: Sun, 20 Sep 2026 22:50:03 GMT
Server: Kestrel
WWW-Authenticate: Bearer
WWW-Authenticate: Bearer
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin

=== GET /app.css (a MapStaticAssets manifest asset) ===
401
```

The **duplicated `WWW-Authenticate: Bearer` header** is the evidence that
`UseStatusCodePagesWithReExecute` did fire: the original request challenged once, the re-executed
`/not-found` request — itself closed by the fallback policy — challenged again. The re-execution
produced nothing, so the client sees a bodiless 401.

### Step 8 — repeat 1, 3, 4 and 5 with `app.MapStaticAssets().AllowAnonymous();`

```
=== C step 8, probe 1: GET /_framework/blazor.web.js ===
curl_status=200 size=200645
HTTP/1.1 200 OK
Content-Length: 200645
Content-Type: text/javascript
Date: Sun, 20 Sep 2026 22:50:21 GMT
Server: Kestrel
Accept-Ranges: bytes
Cache-Control: no-cache
ETag: "iLprmSueJh5+S5e7TMSpDCIn76pkDjFM/MNuNeykNuU="
Last-Modified: Fri, 24 Jul 2026 16:40:58 GMT
Vary: Accept-Encoding
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin

=== C step 8: GET /app.css (control: a manifest asset) ===
200

=== C step 8, probe 3: POST /_blazor/negotiate?negotiateVersion=1 ===
curl_status=401 size=0
HTTP/1.1 401 Unauthorized
Content-Length: 0
…
=== C step 8, probe 4: GET /_blazor?id=000... ===
curl_status=401 size=0
HTTP/1.1 401 Unauthorized
Content-Length: 0
…
=== C step 8, probe 5: POST /_blazor/disconnect ===
curl_status=401 size=0
HTTP/1.1 401 Unauthorized
Content-Length: 0
…
```

### C1 — the script: **C-a**

`_framework/blazor.web.js` is served by the static-asset endpoint out of the asset manifest: it
flips from 401 to 200 the moment `MapStaticAssets()` carries `.AllowAnonymous()`, and it answers
with the manifest's fingerprint `ETag` and `Last-Modified`, exactly as `/app.css` does.

**Exemption row 11 is a consequence of row 8, not a line of its own.** Phase 4 adds no production
line for the script; row 11 is documentation plus its regression test.

### C2 — the three `/_blazor` endpoints: **C-b**

None of negotiate, transport or disconnect is covered by the asset manifest, and none carries
framework-supplied anonymous metadata. All three answer a bodiless 401 under the fallback policy,
both before and after row 8's exemption.

**`MapRazorComponents<App>().AddInteractiveServerRenderMode().AllowAnonymous()` is the wrong
mechanism, and the plan asked for this to be said out loud.** With it in place:

```
--- POST /_blazor/negotiate ---   200
--- GET /_blazor?id=... ---       404   (no such circuit; authorization passed)
--- POST /_blazor/disconnect ---  400   (bad request; authorization passed)
--- GET / (a PAGE) ---            200   <-- side effect: every page is now anonymous
--- GET /spike-form (a PAGE) ---  200   <-- side effect: every page is now anonymous
--- GET /gibberish-unmatched-path --- 401
```

That defeats Decision 1: pages must be closed by default.

A **scoped** mechanism was verified to work. Adding a route-pattern-filtered convention to the
`RazorComponentsEndpointConventionBuilder` that `MapRazorComponents<App>()` returns:

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

produced:

```
--- POST /_blazor/negotiate ---   200
--- GET /_blazor?id=... ---       404   (authorization passed)
--- POST /_blazor/disconnect ---  401   (see the note below — this is NOT the disconnect endpoint)
--- GET / (page, must stay closed) ---           401
--- GET /spike-form (page, must stay closed) --- 401
```

The full set of route patterns the convention saw — useful for Phase 4, because it is the exact
surface `MapRazorComponents<App>()` maps:

```
SPIKE-ENDPOINT: /_framework/opaque-redirect
SPIKE-ENDPOINT: /Error
SPIKE-ENDPOINT: /
SPIKE-ENDPOINT: /not-found
SPIKE-ENDPOINT: /spike-form
SPIKE-ENDPOINT: /_blazor/negotiate
SPIKE-ENDPOINT: /_blazor
SPIKE-ENDPOINT: /_blazor/disconnect/
SPIKE-ENDPOINT: /_blazor/initializers/
```

Note `/_framework/opaque-redirect` is a **razor-components** endpoint, not a static asset, so row
8's exemption does not reach it.

**The disconnect 401 above is a second-order effect, not a failed exemption,** and Phase 4 needs to
know why. Disconnect, once anonymous, answers `400 Bad Request` (no circuit id posted). That 400 is
a 4xx, so `UseStatusCodePagesWithReExecute` re-executes `/not-found`, which was still closed by the
fallback policy, and the re-executed challenge overwrites the 400 with a bodiless 401. Adding
`/not-found` to the same exemption restored it:

```
--- POST /_blazor/disconnect ---
st=400
HTTP/1.1 400 Bad Request
Content-Type: text/html; charset=utf-8
Date: Sun, 20 Sep 2026 22:51:37 GMT
Server: Kestrel
Transfer-Encoding: chunked
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
Content-Security-Policy: frame-ancestors 'self'

--- POST /_blazor/initializers/ --- 200
```

**Consequence for Phase 4 to absorb:** as long as the `/not-found` re-execution target is closed,
*any* 4xx produced anywhere in the application is silently rewritten into a bodiless 401. Exemption
row 4 (`/not-found` is `[AllowAnonymous]`) is therefore not cosmetic — it is what keeps every other
status code in the application honest.

### C3 — the unmatched path: **DOES NOT MATCH A PLANNED BRANCH. NOT ADJUDICATED.**

The plan's two branches are:

- **C3-a** — the anonymous unmatched path answers **302 to `/login`**.
- **C3-b** — the anonymous unmatched path answers **404 with the not-found page**.

The observation is **neither**. Under the configuration Spike C itself specifies — the Phase 4
fallback policy and *nothing else*, which leaves bearer as the default scheme — probe 6 answers

```
HTTP/1.1 401 Unauthorized
Content-Length: 0
WWW-Authenticate: Bearer
WWW-Authenticate: Bearer
```

and, once `/not-found` is exempted so the re-execution can actually render,

```
st=401 size=4485
HTTP/1.1 401 Unauthorized
Content-Type: text/html; charset=utf-8
Transfer-Encoding: chunked
WWW-Authenticate: Bearer
…
```

with the not-found page in the body:

```html
<p>Sorry, the content you are looking for does not exist.</p></article></main></div>
```

So the status code is **401 in both cases**, never 302 and never 404, and the "mutually exclusive"
framing in the plan does not hold: the challenge *and* the re-execution both happen, the
re-execution renders the not-found page, and `UseStatusCodePagesWithReExecute` restores the
original 401 rather than the 404 that routing would have produced.

Why the branches do not fit is not a mystery, and the plan half-says it itself in Spike D's
expected observation ("the bearer handler — still the default scheme at Phase 0 — challenges"):
**C3-a's 302 is unreachable in Spike C's configuration, because the cookie scheme that owns
`OnRedirectToLogin` does not land until Phase 3.** The spike as specified cannot produce the answer
one of its own branches describes.

What the observation *does* settle, without interpretation:

1. The fallback policy **does** apply to a request that matches no endpoint. Routing's 404 never
   happens; authorization denies first.
2. The 404 re-execution is not skipped — it runs, and it runs *through the full pipeline*, so the
   re-executed `/not-found` is itself subject to the fallback policy.
3. The final status code is the **original denial's** status code, not the re-executed request's
   and not 404.
4. Under `/api/*` the answer is identical, because at Phase 0 there is no scheme selector yet.

**This is deliberately left for the human / planner to rule on rather than mapped to the nearest
branch.** Row 5's mechanism column and its two case names in Phase 4 depend on the ruling, and both
of the plan's wordings assert a status code that was not observed.

### Probe 7 — `/api/auth/register`, the input to `AuthApiTests.Register_ShouldReturn404`

The plan offered three answers here and the observation is one of them: **a bodiless 401**
(`Content-Length: 0`, `WWW-Authenticate: Bearer` twice, no body), under the exact configuration
Spike C specifies.

Two caveats Phase 4 must carry, both observed rather than reasoned:

- With `/not-found` exempted, the same probe becomes **401 with the not-found HTML page**
  (`Content-Type: text/html`, 4485 bytes) — the status does not change, only the body appears.
- Phase 4 adds the bearer `OnChallenge` that writes a ProblemDetails body and calls
  `HandleResponse()`. Writing the body starts the response, which stops the re-execution, so the
  Phase 4 answer should be a **`application/problem+json` 401**. That is a prediction, not an
  observation: nothing in Phase 0 has an `OnChallenge`.

Either way the observed and predicted status is **401**, not 404, so
`AuthApiTests.Register_ShouldReturn404` must be rewritten in Phase 4. Its new expected status is
401. Whether it should also assert a problem+json body depends on the `OnChallenge` that Phase 4
itself adds, so the assertion belongs with that code.

---

## Spike D — an authorization attribute on a `.razor` page

### Part 1 — `@attribute [Authorize]`, no fallback policy, `RouteView` still in `Routes.razor`

`git checkout -- src/Envanex.Web/Program.cs` first, so the only authorization in the application
was the attribute itself. `Routes.razor:3` still reads
`<RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" />`, so no
component-level authorization could act.

```
curl_status=401 size=4485
HTTP/1.1 401 Unauthorized
Content-Type: text/html; charset=utf-8
Date: Sun, 20 Sep 2026 22:52:00 GMT
Server: Kestrel
Transfer-Encoding: chunked
WWW-Authenticate: Bearer
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
Content-Security-Policy: frame-ancestors 'self'
```

Body:

```html
Sorry, the content you are looking for does not exist.
```

The page did **not** render. The endpoint was denied by `UseAuthorization`, the bearer handler
challenged with a bodiless 401, and — `/not-found` being open in this configuration — the
re-execution rendered the not-found page under the original 401 status. This is precisely the
"a 401, or a 404 carrying the not-found page if the bodiless 401 is re-executed" the plan
anticipated, with the 401 status preserved.

### Part 2 — `@attribute [AllowAnonymous]`, fallback policy back in place

Control: `GET /` answered `401` in the same run, so the fallback policy was live.

```
curl_status=200 size=6081
HTTP/1.1 200 OK
Content-Type: text/html; charset=utf-8
Date: Sun, 20 Sep 2026 22:52:17 GMT
Server: Kestrel
Cache-Control: no-cache, no-store
Pragma: no-cache
Set-Cookie: .AspNetCore.Antiforgery.U4m2qrp4QYE=CfDJ8IbIphT39VBJpzKT7i2v1Qs7kXe3dyLdk7MMOXO8pFYeiut4M_sWxfdlEdtn7lY-ye-60tdqsSOOn9AW3wVLBW-HKqsTgbd_Ezje7OImP-jqqwN26FgwnD6tyTmymmDjfV_0NlSHifpFi2B9p0YUCEs; path=/; samesite=strict; httponly
Transfer-Encoding: chunked
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
Content-Security-Policy: frame-ancestors 'self'
blazor-enhanced-nav: allow
```

Body:

```html
<h1>Spike Form</h1>
```

### Branch

**D1 — `.razor` attributes reach endpoint metadata.** Both halves hold: `[Authorize]` closes a page
and `[AllowAnonymous]` opens one against a fallback policy.

Phase 3 proceeds as written. `@attribute [Authorize(Policy = EnvanexPolicies.CanRead)]` on
`Home.razor` and `@attribute [AllowAnonymous]` on `Login.razor`, `NotFound.razor` and `Error.razor`
are valid mechanisms. `BlazorPageAuthorizationConvention.cs` is **not** created and no test count
rises.

Also confirmed by part 1: `AuthorizeRouteView`'s `NotAuthorized` template is **not reachable for a
statically rendered page** — the endpoint layer answered before any component rendered. The
template is still added in Phase 3 per Decision 8, and `Routes.razor` carries the comment saying
why.

---

## Spike E — code between `app.Build()` and `app.Run()` under `WebApplicationFactory`

Temporary insertion in `Program.cs`, immediately before `app.Run();`:

```csharp
using (var spikeScope = app.Services.CreateScope())
{
    var spikeContext = spikeScope.ServiceProvider.GetRequiredService<EnvanexIdentityDbContext>();

    if (!await spikeContext.Roles.AnyAsync(role => role.Name == "Spike-E-Marker"))
    {
        spikeContext.Roles.Add(new IdentityRole<Guid> { /* Id, Name, NormalizedName, ConcurrencyStamp */ });
        await spikeContext.SaveChangesAsync();
    }
}
```

`tests/Envanex.IntegrationTests/Spikes/StartupSeedingProbeTests.cs` created a client from
`_fixture.WebApplicationFactory` — which is what forces the host to start — and then queried
`auth.AspNetRoles` through `_fixture.CreateIdentityDbContext()`. The container is a fresh
Testcontainers instance per run, so the row cannot be left over from an earlier run.

```
dotnet test tests\Envanex.IntegrationTests --filter "FullyQualifiedName~StartupSeedingProbeTests"
```

```
Test run for C:\projects\envanex\tests\Envanex.IntegrationTests\bin\Debug\net10.0\Envanex.IntegrationTests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 1 s - Envanex.IntegrationTests.dll (net10.0)
```

### Branch

**E1 — the marker row is there.** `HostFactoryResolver` does not cut the entry point short: the
continuation after `app.Build()` runs before the factory hands back a client.

Phase 5 is as written — `await app.SeedIdentityAsync();` sits between `Build()` and `Run()`.
`IdentitySeedingHostedService.cs` and `AddEnvanexIdentitySeeding` are **not** created.

---

## Effect on the plan's counts

| Spike | Branch | Count delta |
|---|---|---|
| A | A1 | 0 |
| B | B1 **with** the GET guard | 0 — `BlazorLoginPage_RepeatedGets_ShouldNotSpendLoginPermits` is kept |
| C1 | C-a | 0 |
| C2 | C-b | 0 — row 12 names a scoped mechanism instead of "documentation only" |
| C3 | unresolved | unknown until the human rules |
| D | D1 | 0 — no `BlazorPageAuthorizationConventionTests` case |
| E | E1 | 0 |

Every count from Phase 3 onward stands as the plan wrote it, **except** whatever follows from the
C3 ruling.
