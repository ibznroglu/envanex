using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Commands;
using Envanex.IntegrationTests.Fixtures;
using Envanex.Web.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

/// <summary>
/// Guards the bearer side of the gate and the position of <c>UseAuthentication</c>/<c>UseAuthorization</c>
/// in the pipeline. They sit inside the <c>UseStatusCodePagesWithReExecute("/not-found")</c> wrapper,
/// which re-executes any bodiless 4xx, and the re-executed request can overwrite the original status.
/// Every <c>/api/*</c> endpoint outside <c>AuthController</c> now requires a policy, so the bearer
/// scheme challenges and forbids for real; these tests prove that each of those rejections carries
/// a ProblemDetails body, is therefore never re-executed, and never becomes a redirect.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuthPipelineTests : IAsyncLifetime
{
    private const string ProtectedPath = "/api/unit-of-measures";

    private readonly SqlServerFixture _fixture;
    private readonly HttpClient _client;

    public AuthPipelineTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _client = fixture.WebApplicationFactory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ShouldReturn401ProblemJsonAndNotTheNotFoundPage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenCommand("this-token-was-never-issued"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(
            "<html",
            Case.Insensitive,
            "The 401 was re-executed as the /not-found page; UseAuthentication sits inside the "
            + "UseStatusCodePagesWithReExecute wrapper and this response lost its body.");

        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(401);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithNoAuthorizationHeader_ShouldReturn401ProblemJsonAndNotTheNotFoundPage()
    {
        var response = await _client.GetAsync(ProtectedPath);

        await ShouldBeProblemJsonAsync(
            response, HttpStatusCode.Unauthorized, EnvanexAuthenticationEvents.ChallengeDetail);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAMalformedBearerToken_ShouldReturn401ProblemJson()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        var response = await _client.SendAsync(request);

        await ShouldBeProblemJsonAsync(
            response, HttpStatusCode.Unauthorized, EnvanexAuthenticationEvents.ChallengeDetail);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAnExpiredBearerToken_ShouldReturn401ProblemJson()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(expired: true));

        var response = await _client.SendAsync(request);

        await ShouldBeProblemJsonAsync(
            response, HttpStatusCode.Unauthorized, EnvanexAuthenticationEvents.ChallengeDetail);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAValidBearerTokenCarryingNoRole_ShouldReturn403ProblemJsonAndNotTheNotFoundPage()
    {
        // The bearer half of the body-carrying forbid. The token validates, so the caller is
        // authenticated, and it carries no role, so CanRead refuses it: a forbid, not a challenge.
        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(expired: false));

        var response = await _client.SendAsync(request);

        await ShouldBeProblemJsonAsync(
            response, HttpStatusCode.Forbidden, EnvanexAuthenticationEvents.ForbiddenDetail);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAValidBearerTokenCarryingTheAdministratorRole_ShouldReturn200()
    {
        // The positive control for the case above: the same hand-built token, with a role added,
        // is admitted. Without it a 403 above could equally mean that no token is ever accepted.
        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(expired: false, EnvanexRoles.Administrator));

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownApiPath_WithoutAuthentication_ShouldReturn401ProblemJsonAndCarryNoLocationHeader()
    {
        // The redirect this case is named for needs two defects at once. PR 6b's Phase 4 mutation
        // run reproduced it only as a double mutation — the bearer challenge left as a bodiless 401
        // AND [AllowAnonymous] removed from NotFound.razor — where this request answered
        // 302, Location /login?ReturnUrl=%2Fnot-found, no content type, empty body: the bodiless
        // 401 was re-executed as /not-found, /not-found is not under /api/*, so the selector handed
        // the re-executed request to the cookie scheme, and the denied page redirected. Spike C3's
        // probe 7 saw the same 302 for the same reason, before either exemption existed.
        //
        // Either defect alone does not redirect. With /not-found open and the body dropped, the
        // re-executed page renders and the 401 survives as text/html; the content-type assertion
        // catches it. With the body-writing call removed entirely, the status is never set and the
        // answer is an empty 200; the status assertion catches it. Under the double mutation the
        // status assertion fails first, and the Location assertion would fail with it. It is kept
        // because the combination is reachable in production: any change that closes /not-found
        // again turns a bodiless /api/* challenge into a redirect to an HTML login page.
        using var client = CookieAuthHelper.CreateNonRedirectingClient(_fixture.WebApplicationFactory);

        using var response = await client.GetAsync("/api/auth/register");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull(
            "An unmatched /api/* path was redirected; the bearer challenge was re-executed as /not-found.");

        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");
    }

    private static async Task ShouldBeProblemJsonAsync(
        HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedDetail)
    {
        response.StatusCode.ShouldBe(expectedStatus);

        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(
            "<html",
            Case.Insensitive,
            $"The {(int)expectedStatus} was re-executed as the /not-found page; the bearer event wrote no body.");

        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe((int)expectedStatus);
        document.RootElement.GetProperty("detail").GetString().ShouldBe(expectedDetail);
    }

    private static string CreateToken(bool expired, params string[] roles)
    {
        var now = DateTimeOffset.UtcNow;
        var (notBefore, expires) = expired
            ? (now.AddMinutes(-30).UtcDateTime, now.AddMinutes(-15).UtcDateTime)
            : (now.UtcDateTime, now.AddMinutes(15).UtcDateTime);

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [JwtRegisteredClaimNames.Sub] = Guid.NewGuid().ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
        };

        // The same shape JwtAccessTokenIssuer writes: a JSON array under the short claim type,
        // and no claim at all when there is no role.
        if (roles.Length > 0)
        {
            claims[EnvanexClaimTypes.Role] = roles;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://envanex.local",
            Audience = "envanex-api",
            IssuedAt = notBefore,
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(EnvanexWebApplicationFactory.TestSigningKey)),
                SecurityAlgorithms.HmacSha256),
            Claims = claims,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
