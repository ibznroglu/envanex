# 0008 — Policy-based authorization, two authentication schemes, and the read-only demo account

## Context

PR 6a, recorded in ADR 0007, introduced Identity, JWT access tokens, and
rotating refresh tokens. It established identity but did not restrict access:
endpoints remained open, and the Blazor UI had no sign-in flow.

PR 6b adds authorization before PR 8 and PR 10 expand the endpoint and screen
surface. The default must remain safe when an authorization attribute is
forgotten. Permissions must be expressed independently of role names so that
adding or renaming a role does not require changes throughout the controllers.

The REST API and the server-rendered UI need different authentication
contracts. API clients need bearer authentication and HTTP error responses;
Blazor needs browser sessions without introducing client-side token storage.
A public portfolio also needs a visitor account that can read but cannot write.

## Decision

### Require authentication by default; grant permissions through policies

Both `FallbackPolicy` and `DefaultPolicy` require an authenticated user.
Anonymous access must be explicit through `[AllowAnonymous]` or
`.AllowAnonymous()`. Forgetting an exemption therefore causes a visible access
failure instead of silently exposing an endpoint.

Endpoints name policies, not roles:

| Policy | Permitted roles |
| --- | --- |
| `CanRead` | `Administrator`, `Viewer` |
| `CanWrite` | `Administrator` |

Read-only access is represented by the `Viewer` role, without a separate
read-only claim duplicating the same fact. The authentication floor remains
distinct from these permissions: a signed-in user without a role must still
be able to sign out.

The landing page requires `CanRead`, not just authentication. Although the
current page contains no inventory data, its intended role is an ERP entry
point that will display that data. Neither anonymous access nor access for a
roleless account has a justification.

The complete exemption list, including related routing cases that are *not*
exemptions, is:

| Surface | Anonymous-access decision |
| --- | --- |
| `POST /api/auth/login` | Exempt through `AuthController`. |
| `POST /api/auth/refresh` | Exempt through `AuthController`. |
| `POST /api/auth/logout` | Exempt through `AuthController`; authorized by possession of the refresh token. |
| `/not-found` | Explicitly exempt so error re-execution does not introduce an authentication challenge. |
| Unmatched non-API paths / 404 re-execution | No exemption: anonymous callers are challenged and redirected to login; signed-in callers can reach the 404. |
| `/Error` | Explicitly exempt. |
| `/login` | Explicitly exempt. |
| Static assets | Exempt through `MapStaticAssets().AllowAnonymous()`. |
| OpenAPI | Exempt in Development only. |
| Scalar | Exempt in Development only. |
| `/_framework/blazor.web.js` | Covered by the static-asset exemption. |
| `/_blazor` negotiate, transport, disconnect, and initializers endpoints | Exempt through a route-pattern convention that matches the `/_blazor` path segment. |
| Unmatched `/api/*` paths | Not exempt: anonymous requests receive a body-bearing 401 rather than a redirect. |

Each row has a corresponding behavioral test. The hub exemption must not
be applied to the entire Razor component mapping: Spike C2 showed that
`MapRazorComponents<App>().AllowAnonymous()` also opens every page.

Logout is deliberately anonymous with respect to access-token authentication
(research Decision 14). It accepts a refresh token. An expired access token
must not prevent a client from ending its refresh session; requiring one
would leave that session alive when logout is still needed.

### Select one authentication scheme from the request path

A policy scheme selects bearer authentication for the `/api` path segment
and its descendants, and cookie authentication for other paths. Selection is
independent of the presence of an `Authorization` header. Sign-in and sign-out
target the cookie scheme explicitly.

Consequently, `/api/*` is bearer-only: a browser session cookie cannot
authenticate an API request. This keeps the API contract consistent and
removes cookie-authenticated CSRF from that surface without requiring
antiforgery protection on every REST write endpoint. Blazor form operations
retain their antiforgery protection.

Bearer challenges and forbids return 401 and 403 with ProblemDetails bodies.
Cookie challenges redirect to `/login`; cookie forbids return 403 with an
HTML body, rather than redirecting an already authenticated user. Giving
rejections bodies prevents status-code re-execution from replacing them with
a not-found page or a challenge under the other scheme.

### Extend ADR 0007's access-token contents with roles

This decision amends ADR 0007's token-content decision by adding `role` claims
to access tokens. Bearer validation recognizes that claim type; the cookie
principal retains Identity's role claim type. Both identities satisfy the
same authorization policies.

Roles are read from the identity database whenever an access token is issued,
including refresh. They are not copied from the roles observed at login.
Otherwise a revoked role could survive for the refresh family's lifetime of
up to 30 days. With the current 15-minute access-token lifetime and zero clock
skew, an already issued token can retain the old role for at most 15 minutes;
the next refresh uses current roles. This bound applies to bearer tokens,
not cookie sessions.

### Use a cookie session for Blazor

Login uses static server rendering and plain form markup because issuing a
cookie requires an HTTP response; an interactive circuit cannot set it.
UI login uses the same login rate-limit policy as REST login. Page GETs must
neither consume login-attempt permits nor disable the POST limiter.

Blazor components call the Application layer directly rather than calling the
REST API over HTTP. This avoids an unnecessary network hop and a bearer-token
storage problem in a UI that already has a server-side session. UI visibility
alone is not an authorization boundary.

### Seed the demo explicitly and fail startup on unsafe configuration

`Demo:Enabled` is an explicit, default-off seeding gate, independent of the
environment name. Roles are seeded on every boot. The demo password is never
committed: local development uses user-secrets, with App Service configuration
planned for PR 7.

Seeding completes between application construction and request serving. A
blank or policy-invalid password is rejected before any seeding write, using
the host's actual Identity password policy. Diagnostics expose Identity error
codes, never the password. Any seeding failure prevents the host from starting
instead of serving with configuration it failed to honor.

When demo seeding is enabled, an existing account with any role other than
`Viewer` is rejected before writes (`DemoAccount.HasOtherRoles`). A public
demo password must not silently acquire administrative access, including when
the configured email collides with an existing administrator. This is a
startup invariant, not continuous monitoring of later role changes.

Every test host pins `Demo:Enabled=false` unless a test explicitly overrides
it, so neither a developer's user-secrets nor an environment variable can
change the suite's behavior.

## Alternatives

| Alternative | Reason for rejection |
| --- | --- |
| Add `[Authorize]` individually to every endpoint | An omission silently permits anonymous access. Explicit exemptions make omissions fail closed at the authentication boundary. |
| Select the scheme by header presence | A headerless API request can use a browser cookie or receive a login redirect, violating the bearer-only API contract. |
| Accept cookie and bearer together in authorization policies | Makes cookies valid credentials for REST writes and requires antiforgery protection across that surface. |
| Carry login-time roles through refresh | Extends stale permissions from an access-token lifetime to a refresh-family lifetime. |
| Represent read-only access with an additional claim | Duplicates the role-to-permission model. |
| Leave the landing page anonymous | Creates an unjustified exception for a page intended to expose inventory data. |
| Open the entire Razor component mapping for hub access | Also makes protected pages anonymous, as observed in the spike. |
| Have Blazor call REST over HTTP | Adds a hop and requires bearer-token handling that the cookie session avoids. |
| Gate demo creation on the environment name | Couples deployment identity to feature availability instead of using an independently testable switch. |
| Commit the demo password | Violates the same secret-management boundary as committing the JWT signing key. |
| Seed through a hosted service | Unnecessary: the pre-serving call site works under the test host and makes startup failure explicit. |
| Use a test-only authentication handler | Bypasses real issuer, audience, signature, and lifetime validation; tests log in through the actual authentication flow instead. |

## Consequences

### Guarantees and costs

New endpoints deny anonymous access unless explicitly exempted. Policies
centralize the permission model, and role removal reaches newly issued bearer
tokens without waiting for refresh-family expiry. Enabled demo seeding refuses
unexpected privileges and fails startup on invalid configuration.

The fallback is only an authentication floor. It does **not** infer that a
new write action needs `CanWrite`: omitting that policy can still admit a
signed-in `Viewer`. Endpoint policy coverage remains a separate obligation.

A browser cookie cannot call even read-only API endpoints such as
`/api/products/datasource`. Future browser-based REST clients, including the
DevExpress showcase, must address bearer-token handling explicitly.

Real-login integration tests exercise the actual security boundary, at the
cost of broader failure propagation: a broken login fails every test that signs
in for real. The integration suite runs in 57–59 seconds, close to ADR 0007's
60-second decision threshold. No schema change or migration is introduced.

### PR 7 blockers: live sessions and the demo lifecycle

**Cookie sessions are not revalidated today. Research Decision 11 is not
fulfilled.** There is no cookie principal-validation callback or explicit
expiry setting; the default 14-day sliding lifetime applies. Deleting a user,
removing a role, changing the security stamp, or locking the account does not
invalidate an already issued cookie. Sliding renewal means 14 days is not a
guaranteed absolute limit on stale access.

The registered `RevalidatingIdentityAuthenticationStateProvider` does not run
because no component currently declares an interactive render mode. Even
when active, it would revalidate a circuit, not the browser cookie. Its
presence therefore does not establish live-session revocation or lockout.

The present exposure is limited: the cookie-protected landing page contains
no inventory data, and API endpoints ignore cookies. This must be fixed before
PR 7 places data behind cookie authentication. Cookie revalidation, explicit
session-lifetime limits, and effective lockout/deletion handling for live
sessions require behavioral tests; rotating a security stamp alone is not a
solution in the current setup.

**Demo configuration does not control the existing account's lifecycle.**
Setting `Demo:Enabled=false` stops seeding but does not disable the account or
revoke sessions. Changing `Demo:Password` does not rotate an existing account's
password. The shared account can also be locked for 15 minutes by failed login
attempts. Account disablement, credential rotation, session revocation, and
shared-account lockout behavior remain PR 7 blockers. Effective revocation
depends on closing the cookie-validation gap above.

### Startup availability and evidence limits

Every boot requires a reachable, migrated identity database, with no retry
policy. Future Identity migrations must be applied before starting a build
that depends on them. Concurrent initial demo-user creation is an accepted
limitation: the losing host fails startup and a restart recovers. A single
App Service instance does not eliminate overlap during restarts or slot swaps.

The three related defensive paths have different evidence and must be read
together:

| Retained path | Evidence boundary |
| --- | --- |
| ADR 0007's `RotateAsync` unique-violation catch (2601/2627) | Retained defensively; no test reaches it. |
| Role-seeder unique-violation catch | Reached by the two-host race test; removing it makes that test fail on every recorded run. |
| `DemoAccount.AddToRoleFailed` branch | Reached only under mutation M5; no test directly exercises it in the unmodified implementation. |

These are not equivalent claims of tested coverage. Role-seeding race
handling does not make demo-user creation concurrency-safe.

Other recorded limitations remain: bearer 401 responses omit the
`WWW-Authenticate` header, and login ignores `ReturnUrl` and returns to `/`
until safe local-redirect validation is implemented. Cookie access-denied
markup is currently emitted by the authentication handler; moving it to a
page must preserve the 403 status.
