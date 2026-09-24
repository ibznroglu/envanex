using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Envanex.Application.Authentication.Commands;
using Envanex.Application.Authentication.DTOs;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

/// <summary>
/// End-to-end tests for <c>/api/auth/login</c>, <c>/refresh</c> and <c>/logout</c> against the
/// shared factory, whose login rate-limit policy is switched off.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuthApiTests : IAsyncLifetime
{
    private const string TestEmail = "auth-api@envanex.local";
    private const string TestPassword = "Envanex-Test-Parola-1";
    private const string WrongPassword = "Yanlis-Test-Parola-9";
    private const string UnknownEmail = "boyle-biri-yok@envanex.local";

    private readonly SqlServerFixture _fixture;
    private readonly HttpClient _client;

    public AuthApiTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _client = fixture.WebApplicationFactory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetIdentityAsync();
        await IdentitySeeder.CreateUserAsync(_fixture.WebApplicationFactory.Services, TestEmail, TestPassword);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, password));

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenCommand(refreshToken));

    private Task<HttpResponseMessage> LogoutAsync(string refreshToken) =>
        _client.PostAsJsonAsync("/api/auth/logout", new LogoutCommand(refreshToken));

    private async Task<AuthenticationResponse> LoginSuccessfullyAsync()
    {
        var response = await LoginAsync(TestEmail, TestPassword);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>()).ShouldNotBeNull();
    }

    private static async Task<string> ReadDetailAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("detail").GetString().ShouldNotBeNull();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturn200()
    {
        var response = await LoginAsync(TestEmail, TestPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnAccessTokenRefreshTokenAndBothExpiries()
    {
        var now = DateTimeOffset.UtcNow;

        var body = await LoginSuccessfullyAsync();

        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        body.AccessTokenExpiresAt.ShouldBeGreaterThan(now);
        body.RefreshTokenExpiresAt.ShouldBeGreaterThan(body.AccessTokenExpiresAt);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnBearerAsTokenType()
    {
        var body = await LoginSuccessfullyAsync();

        body.TokenType.ShouldBe("Bearer");
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnAnAccessTokenThatValidatesAgainstTheConfiguredKey()
    {
        var body = await LoginSuccessfullyAsync();

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(
            body.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "https://envanex.local",
                ValidateAudience = true,
                ValidAudience = "envanex-api",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(EnvanexWebApplicationFactory.TestSigningKey)),
                ClockSkew = TimeSpan.Zero,
            });

        validation.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldReturn401ProblemJson()
    {
        var response = await LoginAsync(TestEmail, WrongPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ShouldReturn401ProblemJson()
    {
        var response = await LoginAsync(UnknownEmail, TestPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_ShouldReturn401()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            (await LoginAsync(TestEmail, WrongPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // MaxFailedAccessAttempts is 5, so the account is locked by now. The answer is the same
        // 401 it was before: lockout is never visible in a response.
        var response = await LoginAsync(TestEmail, WrongPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_ShouldAlsoReturn401ForTheCorrectPassword()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(TestEmail, WrongPassword);
        }

        var response = await LoginAsync(TestEmail, TestPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmailWrongPasswordAndLockedOutAccount_ShouldReturnIdenticalStatusContentTypeAndBody()
    {
        // DECISION 5. The three credential failures must be indistinguishable to a caller. A change
        // that trips this test is a design question, not a broken test.
        var unknownEmail = await LoginAsync(UnknownEmail, TestPassword);
        var wrongPassword = await LoginAsync(TestEmail, WrongPassword);

        await IdentitySeeder.LockOutAsync(_fixture.WebApplicationFactory.Services, TestEmail);
        var lockedOut = await LoginAsync(TestEmail, TestPassword);

        wrongPassword.StatusCode.ShouldBe(unknownEmail.StatusCode);
        lockedOut.StatusCode.ShouldBe(unknownEmail.StatusCode);

        var expectedContentType = unknownEmail.Content.Headers.ContentType?.ToString();
        wrongPassword.Content.Headers.ContentType?.ToString().ShouldBe(expectedContentType);
        lockedOut.Content.Headers.ContentType?.ToString().ShouldBe(expectedContentType);

        var unknownEmailBody = await unknownEmail.Content.ReadAsStringAsync();
        var wrongPasswordBody = await wrongPassword.Content.ReadAsStringAsync();
        var lockedOutBody = await lockedOut.Content.ReadAsStringAsync();

        wrongPasswordBody.ShouldBe(unknownEmailBody);
        lockedOutBody.ShouldBe(unknownEmailBody);
    }

    [Fact]
    public async Task Login_FailureDetail_ShouldMentionTheLockoutPolicyWithoutConfirmingIt()
    {
        // DECISION 6. The aorist "kilitlenir" states the policy; "kilitlendi" would confirm the
        // state of this account and is what this test forbids.
        var response = await LoginAsync(TestEmail, WrongPassword);

        var detail = await ReadDetailAsync(response);

        detail.ShouldContain("kilitlenir");
        detail.ShouldNotContain("kilitlendi");
    }

    [Fact]
    public async Task Login_EightConsecutiveAttempts_ShouldNeverReturn429()
    {
        // The production login budget is 5 per 300 s and every request under WebApplicationFactory
        // lands in the single "unknown" partition. This goes red the moment
        // RateLimiting:Login:Enabled=false is dropped from EnvanexWebApplicationFactory, which
        // would make every auth test in the collection flaky.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var response = await LoginAsync(TestEmail, WrongPassword);

            response.StatusCode.ShouldBe(
                HttpStatusCode.Unauthorized,
                $"Attempt {attempt + 1} was rate limited; the shared factory must not run the login policy.");
        }
    }

    [Fact]
    public async Task Login_WithEmptyPassword_ShouldReturn400WithTheTurkishParolaRequiredMessage()
    {
        var response = await LoginAsync(TestEmail, string.Empty);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Parola zorunludur.");
    }

    [Fact]
    public async Task Login_WithMalformedEmail_ShouldReturn400WithTheTurkishEmailInvalidMessage()
    {
        var response = await LoginAsync("bu-bir-e-posta-degil", TestPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Geçerli bir e-posta adresi giriniz.");
    }

    [Fact]
    public async Task Login_ShouldNotSetAnySetCookieHeader()
    {
        // RESEARCH DECISION 9. The refresh token travels in the body; the cookie scheme is PR 6b.
        var response = await LoginAsync(TestEmail, TestPassword);

        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_WithTheTokenFromLogin_ShouldReturn200()
    {
        var login = await LoginSuccessfullyAsync();

        var response = await RefreshAsync(login.RefreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithTheTokenFromLogin_ShouldReturnADifferentRefreshToken()
    {
        var login = await LoginSuccessfullyAsync();

        var response = await RefreshAsync(login.RefreshToken);
        var refreshed = (await response.Content.ReadFromJsonAsync<AuthenticationResponse>()).ShouldNotBeNull();

        refreshed.RefreshToken.ShouldNotBe(login.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithTheTokenFromLogin_ShouldReturnADifferentAccessToken()
    {
        var login = await LoginSuccessfullyAsync();

        var response = await RefreshAsync(login.RefreshToken);
        var refreshed = (await response.Content.ReadFromJsonAsync<AuthenticationResponse>()).ShouldNotBeNull();

        refreshed.AccessToken.ShouldNotBe(login.AccessToken);
    }

    [Fact]
    public async Task Refresh_ReplayingAConsumedToken_ShouldReturn401()
    {
        var login = await LoginSuccessfullyAsync();
        (await RefreshAsync(login.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var replay = await RefreshAsync(login.RefreshToken);

        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ReplayingAConsumedToken_ShouldAlsoInvalidateTheCurrentToken()
    {
        // RFC 9700 section 4.14.2, end to end: detecting reuse kills the whole family, not only
        // the replayed row.
        var login = await LoginSuccessfullyAsync();

        var rotation = await RefreshAsync(login.RefreshToken);
        var rotated = (await rotation.Content.ReadFromJsonAsync<AuthenticationResponse>()).ShouldNotBeNull();

        (await RefreshAsync(login.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var afterReuse = await RefreshAsync(rotated.RefreshToken);

        afterReuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithAValidToken_ShouldReturn204()
    {
        var login = await LoginSuccessfullyAsync();

        var response = await LogoutAsync(login.RefreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_WithAValidToken_ThenRefresh_ShouldReturn401()
    {
        var login = await LoginSuccessfullyAsync();
        (await LogoutAsync(login.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await RefreshAsync(login.RefreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithAnUnknownToken_ShouldReturn204()
    {
        // Logout is idempotent on purpose: a 404 here would turn the endpoint into an oracle for
        // token existence.
        var response = await LogoutAsync("this-token-was-never-issued");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_ShouldNotAffectASecondSessionOfTheSameUser()
    {
        // RESEARCH DECISION 8. Logout revokes the family of the presented token and nothing else.
        var firstSession = await LoginSuccessfullyAsync();
        var secondSession = await LoginSuccessfullyAsync();

        (await LogoutAsync(firstSession.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await RefreshAsync(secondSession.RefreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Both cases below are probed with GET rather than POST on purpose. MVC attribute routing
    // answers a matched path with an unsupported verb with 405, so a 404 proves the path is not
    // routable at all. A POST could not prove it: an unmatched POST falls through to the Blazor
    // catch-all and is rejected by UseAntiforgery with 400 before routing ever reports the miss.
    //
    // The same URL, two callers, two properties, and neither case is a duplicate of the other.
    // Register_ShouldReturn401 guards the gate: this path is covered by the fallback policy and
    // answers a body-carrying 401, never a leaked 404 or a redirect. Register_WhileAuthenticated_
    // ShouldReturn404 guards RESEARCH DECISION 11 of PR 6a — no endpoint creates a user — and is the
    // only test that does: an anonymous caller can no longer tell an unmatched path from a real
    // endpoint that refuses it, so only an authenticated caller still sees the miss.
    // Register_ShouldReturn401 is also the proving test for row 13 of the PR 6b exemption table.
    //
    // One other anonymous probe of this path exists:
    // AuthPipelineTests.UnknownApiPath_WithoutAuthentication_ShouldReturn401ProblemJsonAndCarryNoLocationHeader.
    // It sends the same request and additionally asserts the absence of a Location header, which
    // Register_ShouldReturn401 does not — the redirect a bodiless challenge and a closed /not-found
    // produce together.
    [Fact]
    public async Task Register_ShouldReturn401()
    {
        var response = await _client.GetAsync("/api/auth/register");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Register_WhileAuthenticated_ShouldReturn404()
    {
        using var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.GetAsync("/api/auth/register");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
