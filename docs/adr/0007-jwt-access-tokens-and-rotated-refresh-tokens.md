# 0007 — JWT access tokens with rotated, reuse-detected refresh tokens

> Amended by [ADR 0008](0008-policy-based-authorization-two-authentication-schemes-and-the-demo-account.md): access tokens now also carry `role` claims, read from the database at every issue, refresh included.

## Context

PR 6a introduces the first notion of a user. Until now nothing in the system knew who was calling:
there was no user table, no credential, no session, and no code path that needed to answer the
question. From here the system has an identity, and identity brings a class of failure the earlier
PRs never had — a wrong answer is not a bad response but a compromised account.

This PR answers who you are. It deliberately does not answer what you may do: no endpoint is
protected, `[Authorize]` appears nowhere, and there are no authorization policies. That split is
recorded in the roadmap and PR 6b closes it. Shipping authentication without authorization is a
strange intermediate state to leave in `main`, and it is a choice: the alternative was one PR
carrying Identity, token rotation, policies, the Blazor cookie scheme and a demo account, which
would have been too large to review with the care any of those individually deserve.

Four questions had to be answered before any of it could be written. Where does user storage live,
given that the business model has three aggregates whose mapping is governed by two
model-finalizing conventions that must not touch Identity's entity types? What kind of token does a
client carry, given that a signed token cannot be revoked and an opaque token costs a database read
on every use? What does the login endpoint say when it refuses, given that the helpful answer and
the safe answer are different sentences? And how does a refresh survive two requests arriving at
once with the same token, given that the honest reading of that situation is that either the user
double-clicked or someone stole the token and there is no way to tell from the request?

A fifth question arrived from the codebase rather than the design. Nothing under `src/` had a
notion of *now*: a search for `TimeProvider`, `ISystemClock`, `IClock`, `DateTime.` and
`DateTimeOffset.` returned nothing at all. Access token expiry, the refresh idle window, the
absolute family cap and reuse detection all depend on it, and all four need to be testable without
sleeping.

## Decision

**ASP.NET Core Identity lives in its own `DbContext`, in its own `auth` schema, with its own
migrations history table.** `EnvanexIdentityDbContext` is separate from `EnvanexDbContext` and
registers neither `AggregateRootConvention` nor `MoneyComplexTypeConvention`, both of which are
model-finalizing and walk every entity type in the model they are registered on. The decisive
reason is narrower than tidiness, and it is a silent failure rather than an aesthetic one: two
contexts pointing at one database both default to the same `__EFMigrationsHistory` table, so each
reads the other's applied migrations as its own history. Nothing throws. A migration is skipped or
re-applied and the damage surfaces weeks later. The Identity context therefore declares
`MigrationsHistoryTable("__EFMigrationsHistory", "auth")`, and all three construction sites — the
DI registration, the design-time factory and the test fixture — route through one extension method,
because a setting that must be repeated in three places is a setting that will be forgotten in one.

**Access tokens are short-lived JWTs; refresh tokens are opaque, stored hashed, and rotated on
every use.** The access token carries `sub`, `email` and `jti`, is signed with HS256, and lives 15
minutes. The refresh token is 32 bytes of `RandomNumberGenerator` output written as Base64Url — it
carries no information at all, which is what opaque means. Its meaning is the row in
`auth.RefreshTokens`, which is why revoking it is real. The split puts the frequently-used secret
on a short clock and the rarely-used secret under the database's control.

**A consumed refresh token is stamped, never deleted.** Rotation sets `RotatedAt` and
`ReplacedByTokenId` on the row it consumes and inserts a child carrying the same `FamilyId`. RFC
9700 §4.14.2 requires a replayed refresh token to revoke the entire grant rather than the token
presented, and deleting the consumed row makes that impossible after one generation: in a chain
r1 → r2 → r3, a deleted r1 matches nothing when replayed, so the system answers "invalid token"
while r3 stays live and the attacker keeps working. Keeping the row is what lets a replay of any
generation resolve to its family and close the whole family at once.

**The one-live-token-per-family rule is enforced by the database, not by application code.**
`IX_RefreshTokens_FamilyId_Live` is unique on `FamilyId` filtered on
`[RotatedAt] IS NULL AND [RevokedAt] IS NULL`. The same check could have been written in
`RotateAsync`, and it would be correct for one request at a time; two concurrent requests both pass
an application check and the database does not care that they did. Optimistic concurrency completes
the pair: every refresh token row carries a `RowVersion` token, so every write to it is conditional
on the row not having moved. The filtered index is deliberately narrower than
`RefreshToken.IsUsableAt`, which additionally requires both expiry timestamps to be in the future:
the database invariant is therefore strictly stronger than the application's notion of usable,
which is the safe direction. The reverse would make the family lock decorative.

**Rotation is one `SaveChangesAsync`, and both ways of losing the race exit through one branch.**
The parent stamp and the child insert go in a single save, with no explicit transaction and no
split. A loser can fail either with `DbUpdateConcurrencyException`, because the parent's
`RowVersion` moved, or with a 2601/2627 unique violation on the live-family index. Both report the
same fact — another rotation consumed this token first — and nothing downstream benefits from
telling them apart, so both are caught together and both return `Auth.InvalidRefreshToken`.
Collapsing them removes any dependency on EF Core's internal UPDATE/INSERT ordering. The captured
SQL shows the parent UPDATE preceding the child INSERT, which means the unique-violation path is in
practice unreachable through `RotateAsync`; it is handled anyway, in that same branch, so a future
change to save ordering cannot turn it into an unhandled exception.

**Revoking a family is a bounded retry, and a revocation that cannot be guaranteed does not report
success.** A rotation committing between the read and the write leaves a live child the first pass
never saw, so `RevokeLiveFamilyRowsWithRetryAsync` re-reads and retries up to three times, and
re-reads once more after the loop before concluding anything. The verdict is always the database's
answer to "is any row in this family still live", never this call's own write having succeeded. All
three revocation sites — reuse detection, expiry and logout — go through it.

**Login answers a wrong password, an unknown user and a locked-out account identically, and
`Auth.UserLockedOut` does not exist.** The three paths return the same `AuthErrors.InvalidCredentials`
instance and produce a byte-identical status, content type and body. Three distinguishable answers
would let an attacker enumerate which email addresses are registered and observe the moment a brute
force starts working. The error code was not merely mapped to the same status as the others but
never created, because two codes sharing a status still carry different `detail` strings and invite
a later maintainer to make one of them more helpful. The single Turkish message states the policy
without confirming this account's state, in the aorist: *"E-posta veya parola hatalı. Arka arkaya
birkaç başarısız denemeden sonra hesap bir süreliğine kilitlenir."* The past tense — *kilitlendi* —
would confirm it, and a named test forbids it.

**The password check runs before the lockout check.** Skipping password verification for a
locked-out account makes that response measurably faster, and the speed-up itself reports both that
the account exists and that the lockout threshold was crossed. `IdentityService` therefore calls
`CheckPasswordAsync` unconditionally and inspects `IsLockedOutAsync` afterwards. On the locked-out
branch neither `AccessFailedAsync` nor `ResetAccessFailedCountAsync` is called: the first would let
an attacker extend a victim's lockout indefinitely by continuing to guess, and the second would
hand a locked account a way back in. Lockout itself stays on Identity's own mechanism — five failed
attempts, fifteen minutes — observable through logs and Identity's built-in meter, never through
the response.

**Time comes from `TimeProvider`.** It is registered with `TryAddSingleton(TimeProvider.System)` so
a test can register `FakeTimeProvider` first and win, which is what lets an eight-day expiry test
run without sleeping. No hand-written `IClock` was introduced, and `TimeProvider` does not enter
`Envanex.Domain` in this PR — the stock ledger in PR 8 is where that question gets asked.

**Auth error codes live in `Envanex.Application`, and the reflection guard was widened rather than
abandoned.** There is no auth aggregate in `Envanex.Domain` and there will not be one, so the eight
`Auth.*` codes belong in the Application layer. `ResultMappingTests` previously scanned
`Envanex.Domain` alone, which would have left exactly the codes a user is most likely to see
outside every guard. The scan now covers both assemblies, so all four facts of ADR 0006 hold over
41 codes instead of 33.

**Identity is reached through an abstraction; `UserManager` never appears in `Envanex.Application`.**
`IIdentityService`, `IRefreshTokenService` and `IAccessTokenIssuer` are declared in the Application
layer and implemented in Infrastructure, and login, refresh and logout are ordinary
`ICommandHandler` implementations with validators, decorator registration and DI test entries like
every other command. ADR 0005 claims the command handler pattern will be copied by every future
aggregate; auth was its first test, and making it the one exception would have falsified the claim
on first contact.

**The login endpoint has its own named rate limit policy, and its partition key is a known blocker
for the first deploy.** Production is five requests per 300 seconds, partitioned by the client
address. `AddRateLimiter` and `UseRateLimiter` became unconditional, with only the global limiter
assignment staying behind `RateLimiting:Enabled`, because `[EnableRateLimiting("login")]` naming a
policy the middleware never sees throws at endpoint build time and takes the whole application down
rather than the one endpoint. `LoginRateLimitPartition.GetKey` falls back to a constant `"unknown"`
when the connection has no remote address, and `UseForwardedHeaders` is deliberately not configured
in this PR.

**Lifetimes are 15 minutes, a 7-day idle window and a 30-day absolute cap.** The idle window is
recomputed on every rotation; the absolute cap is set at login and copied to every child unchanged,
which keeps validation a single-row read. An active client stays signed in as long as it refreshes
within seven days, and no longer than thirty days regardless.

## Alternatives

**One `DbContext` for Identity and business data.** One context, one migration chain, one set of
`dotnet ef` commands with no `--context` flag. Rejected because the two model-finalizing conventions
would run over Identity's entity types, and more decisively because the shared
`__EFMigrationsHistory` table fails silently rather than loudly. The cost of the separation is real:
a second design-time factory, `--context` on every migration command, a second `MigrateAsync` in
the test fixture and a separate reset path.

**Refresh tokens as JWTs.** No table, no lookup, and the same signing infrastructure the access
token already uses. Rejected because revoking a signed token requires a denylist, which is a table
and a lookup — the two things the JWT was chosen to avoid. A refresh token's whole purpose is that
it can be revoked.

**Server-side session identifiers instead of JWTs.** Revocation would be immediate and complete.
Rejected because every request would then carry a database read, and the access token's 15-minute
lifetime already bounds the window in which a leaked token is useful.

**Deleting the consumed row on rotation.** The obvious implementation, and it keeps the table
small. Rejected on the r1 → r2 → r3 analysis above: it destroys reuse detection after a single
generation, and it does so silently — every test that replays the immediately-previous token still
passes.

**Enforcing one live token per family in application code.** No filtered index, no unique-violation
handling, simpler error paths. Rejected because two concurrent requests both pass an application
check. This is the decision that cost the most: error handling and the concurrency tests are
markedly more complex than they would otherwise be. Leaving session security dependent on the order
in which two requests happen to execute was the heavier cost.

**An explicit transaction with two saves in rotation, in a stated order.** Considered during
implementation to make the failure mode predictable rather than dependent on EF Core's batch
ordering. Rejected because production behaviour should not be reshaped to make an exception type
predictable, and because collapsing the two failure branches into one achieves the same result
without touching how the data is written.

**A distinguishable response for a locked-out account.** Far better for a user who genuinely is
locked out. Rejected because it confirms both that the account exists and that repeated guessing
has had an effect, which is precisely the feedback a brute force needs.

**A password hash for the stored refresh token.** PBKDF2 or similar, by analogy with passwords.
Rejected on two counts: a refresh token is 256 bits of CSPRNG output, so there is no dictionary to
slow down, and a per-row salt makes the hash unsearchable — the lookup degrades from an index seek
to a scan with a verification per row. Plain SHA-256 into `binary(32)` with a unique index, compared
with `CryptographicOperations.FixedTimeEquals`, is the right shape. HMAC-SHA256 was also rejected:
it introduces a second secret to rotate and buys nothing against an attacker who can read the table
but cannot invert the hash.

**A hand-written `IClock` abstraction.** The pre-.NET 8 convention, and it would have worked.
Rejected because the BCL now ships the type and a second concept for the same idea is a cost with
no return.

**`AuthErrors` in `Envanex.Domain`.** It would have required no change to `ResultMappingTests`.
Rejected because there is no auth aggregate and inventing one to satisfy a test's scan would be the
test dictating the model.

**Configuring `UseForwardedHeaders` in this PR.** It is the correct fix for the partition key.
Rejected here because calling it with default options and no known proxy is a no-op that looks
solved, and the usual next step — clearing `KnownProxies` and `KnownNetworks` — makes
`X-Forwarded-For` attacker-controlled and lets an attacker mint a fresh partition per request. A
global limit fails closed and is loud; a spoofable limit fails open and is silent. The fix belongs
with the deployment that creates the proxy.

**A 14-day idle window and a 60-day absolute cap.** Proposed during planning and shortened. The
system deploys publicly in PR 7, carries stock-write authority and ships no revocation surface, so
the only defence against a leaked refresh token is the length of the window. A shorter window is
easier to defend and costs nothing measurable.

**Calling `UserManager` from the controller.** Fewer types, less mapping, and a shorter path from
request to response. Rejected because auth would then be the one use case that skips the layer ADR
0005 says every future aggregate will copy.

## Consequences

**The two error tables now hold 41 entries each and both must be edited by hand for every new
code.** ADR 0006 accepted that burden because the reflection tests turn a gap into a failing test;
widening the scan to a second assembly keeps that true and extends the burden to the Application
layer.

**`auth.RefreshTokens` grows without bound, and the pruning job currently has no owner.** Rows are
stamped and never deleted, which is the point, and nothing reclaims them. The gap was recorded
against PR 18 — but PR 18 sits in the roadmap's deferred section, cut along with purchasing, sales
and invoicing, so as written the work is assigned to a PR that will never happen. The active plan
ends at PR 10. This needs a real owner before the first deploy accumulates rows in production, and
naming it here is more honest than leaving the pointer dangling.

**A locked-out user cannot learn why, and the missing piece is recovery rather than notification.**
Someone typing the correct password into a locked account is told only that the credentials are
wrong. The response is deliberately silent about it and should stay that way; what the system owes
that person is not a more informative error but a safe path — through a channel they have already
proven they control — to find out what happened and regain access. That path does not exist yet and
is not a reason to weaken the response.

**Two timing side channels remain, one by choice and one by arithmetic.** An unknown email still
answers measurably faster than a wrong password, because no password hash is verified; closing it
needs a dummy verification and is a declared non-goal, pinned by a named test that would have to be
inverted to change it. A wrong password also performs one UPDATE that a locked-out attempt does not,
which is about a millisecond against the roughly hundred now shared by both paths.

**The filtered index and `IsUsableAt` must stay in step, and nothing forces them to.** They encode
the same two conditions in two places, one in SQL and one in C#. If one changes and the other does
not, the database invariant and the application's notion of usable diverge silently. Schema tests
pin the index's exact filter definition, read from `sys.indexes` rather than guessed, which is the
closest thing to a guard that exists.

**The unique-violation branch in rotation is unreachable and untested, on purpose.** The analysis
and the captured SQL both say a racing rotation loses on the `RowVersion` predicate before it ever
reaches the child insert, and no legal database state has two live rows in one family, so the state
that would trigger it cannot be constructed by a test either. It is kept because it costs one catch
clause and converts a future change in save ordering from an unhandled exception into the same
clean 401.

**The integration suite went from 21 seconds to about 44, and the test count from 299 to 562.**
Roughly a third of the new cost is real PBKDF2 work over HTTP, a third is the concurrency tests
looping twenty iterations each, and the rest is factory construction in the rate-limit tests. The
plan set 60 seconds as the point at which the iteration count becomes a decision rather than a
default; the suite is now close enough to that line to be worth watching in PR 8.

**The rate-limit partition key blocks the first public deploy.** On Azure App Service the
connection's remote address is the front end's for every request, so all users collapse into one
partition and five requests per five minutes becomes a global limit — login stops working for
everyone after three sign-ins. It is recorded as a blocker in the roadmap and PR 7 owns it.

**The tripwire test guarding that blocker is weaker than it looks.**
`GetKey_ShouldIgnoreXForwardedForUntilPr7` calls `GetKey` directly against a hand-built
`DefaultHttpContext`, so it only catches a fix that changes `GetKey` itself. A fix applied upstream
through `UseForwardedHeaders` leaves the test green, and PR 7 must therefore delete it deliberately
rather than wait for a red build to point at it. The limitation is written into both the test and
the roadmap row, which is the only thing standing in for a guard.

**Every migration command now needs `--context`, and generated migration files need normalising.**
`CLAUDE.md` carries the four commands. `dotnet ef` emits CRLF with a UTF-8 BOM, which violates the
repository's LF rule and fails `dotnet format`, so generated files are normalised after every
`migrations add` — an encoding fix only, never a change to the DDL.
