using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Envanex.Application.Authentication.Commands;
using Envanex.IntegrationTests.Fixtures;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

/// <summary>
/// Exercises the named "login" rate-limit policy against a factory that permits 2 logins per 60 s
/// and has the global limiter switched off, so a 429 here can only have come from the login policy.
/// </summary>
/// <remarks>
/// The 2-permit budget belongs to the factory, so a fresh factory is built per test. One factory per
/// class would let the first test spend the budget of the rest.
/// </remarks>
[Collection(DatabaseCollection.Name)]
[SuppressMessage("IDisposable", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed via IAsyncLifetime.DisposeAsync")]
public sealed class LoginRateLimiterTests : IAsyncLifetime
{
    private const string TestEmail = "login-rate-limit@envanex.local";
    private const string TestPassword = "Envanex-Test-Parola-1";
    private const string WrongPassword = "Yanlis-Test-Parola-9";

    private readonly SqlServerFixture _fixture;
    private LoginRateLimitedWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public LoginRateLimiterTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetIdentityAsync();

        _factory = new LoginRateLimitedWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();

        await IdentitySeeder.CreateUserAsync(_factory.Services, TestEmail, TestPassword);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginCommand(TestEmail, password));

    [Fact]
    public async Task Login_ExceedingTheLoginRateLimit_ShouldReturn429ProblemJson()
    {
        await LoginAsync(_client, WrongPassword);
        await LoginAsync(_client, WrongPassword);

        var rejected = await LoginAsync(_client, WrongPassword);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        rejected.Content.Headers.ContentType.ShouldNotBeNull();
        rejected.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");

        var body = await rejected.Content.ReadAsStringAsync();
        body.ShouldNotContain(
            "<html",
            Case.Insensitive,
            "Response body must not be HTML; UseStatusCodePagesWithReExecute should not intercept a 429 with a body.");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        root.GetProperty("status").GetInt32().ShouldBe(429);
        root.GetProperty("type").GetString().ShouldBe("https://httpstatuses.io/429");
        root.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_ExceedingTheLoginRateLimit_ShouldIncludeARetryAfterHeader()
    {
        await LoginAsync(_client, WrongPassword);
        await LoginAsync(_client, WrongPassword);

        var rejected = await LoginAsync(_client, WrongPassword);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        rejected.Headers.RetryAfter.Delta.ShouldNotBeNull();
        rejected.Headers.RetryAfter.Delta.Value.TotalSeconds.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Login_UnderTheLoginRateLimit_ShouldReturn401ForAWrongPassword()
    {
        // Proves the policy is not simply rejecting everything.
        var response = await LoginAsync(_client, WrongPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_FromTwoClientsOfTheSameFactory_ShouldShareTheSameRateLimitPartition()
    {
        // The observable consequence of the "unknown" partition fallback, and of the PR 7 blocker:
        // under WebApplicationFactory no connection carries a remote address, so every caller lands
        // in one bucket. Behind a reverse proxy the same thing happens to real users.
        using var secondClient = _factory.CreateClient();

        (await LoginAsync(_client, WrongPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginAsync(secondClient, WrongPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var third = await LoginAsync(secondClient, WrongPassword);

        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task BlazorLoginForm_ExceedingTheLoginRateLimit_ShouldReturn429()
    {
        // Decision 9: the form posts to its own component endpoint, not to /api/auth/login, and
        // must spend the same named policy's permits or it is a way round the REST door's limit.
        var token = await CookieAuthHelper.ReadAntiforgeryTokenAsync(_client, CookieAuthHelper.LoginPath);

        (await CookieAuthHelper.PostLoginFormAsync(_client, token, TestEmail, WrongPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CookieAuthHelper.PostLoginFormAsync(_client, token, TestEmail, WrongPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var rejected = await CookieAuthHelper.PostLoginFormAsync(_client, token, TestEmail, WrongPassword);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task BlazorLoginPage_RepeatedGets_ShouldNotSpendLoginPermits()
    {
        // The page and the form are one component endpoint, so the policy sees every GET too. Only
        // the POST-only guard in the policy delegate keeps a reload from costing a login attempt.
        //
        // The GETs come first on purpose, and the case does not end until a POST is refused. The
        // guard as first written returned a no-limiter under the same per-address partition key
        // the POSTs use; a limiter is built once per key and reused, so the first GET fixed that
        // address's limiter as "no limit" for the host's lifetime. Observed: after one GET of
        // /login, form POSTs and POST /api/auth/login were never answered 429. A version of this
        // case that stopped after the GETs, or after two POSTs, stayed green with that bug in.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var response = await _client.GetAsync(CookieAuthHelper.LoginPath);

            response.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"GET #{attempt + 1} of the login page was rate limited; rendering the form spent a login permit.");
        }

        // Both permits are still there to be spent by the form itself...
        var token = await CookieAuthHelper.ReadAntiforgeryTokenAsync(_client, CookieAuthHelper.LoginPath);

        (await CookieAuthHelper.PostLoginFormAsync(_client, token, TestEmail, WrongPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CookieAuthHelper.PostLoginFormAsync(_client, token, TestEmail, WrongPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // ...and the POSTs are still limited after the GETs: the third is refused.
        var rejected = await CookieAuthHelper.PostLoginFormAsync(_client, token, TestEmail, WrongPassword);

        rejected.StatusCode.ShouldBe(
            HttpStatusCode.TooManyRequests,
            "After GETs of the login page the form's POSTs were no longer limited; the GETs decided the address's limiter.");
    }

    [Fact]
    public async Task ProductsEndpoint_ShouldNotBeAffectedByTheLoginPolicy()
    {
        // The policy is attached to the login action only; the global limiter is off in this
        // factory, so one request more than the factory's 2 login permits must all get through.
        //
        // The probe is a POST on purpose. The policy hands every non-POST request a no-limiter, so
        // GETs here would pass even if the policy were attached to this endpoint, and the case could
        // not fail. UseRateLimiter runs before UseAuthentication, so an anonymous POST still passes
        // through the limiter: a 401 means it got past the limiter, a 429 means it was spent.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var response = await _client.PostAsJsonAsync("/api/products", new { });

            response.StatusCode.ShouldBe(
                HttpStatusCode.Unauthorized,
                $"POST #{attempt + 1} of /api/products was not answered 401 by the authorization challenge.");
        }
    }
}
