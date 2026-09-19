using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Envanex.Application.Authentication.Commands;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

/// <summary>
/// Guards the position of <c>UseAuthentication</c>/<c>UseAuthorization</c> in the pipeline. They sit
/// inside the <c>UseStatusCodePagesWithReExecute("/not-found")</c> wrapper, which re-executes any
/// bodiless error response as the not-found page. No endpoint is <c>[Authorize]</c> in PR 6a, so the
/// only 401s that exist carry a ProblemDetails body; these tests prove that and prove that adding
/// the two middlewares did not close a single open endpoint.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuthPipelineTests : IAsyncLifetime
{
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
    public async Task OpenEndpoint_WithNoAuthorizationHeader_ShouldStillReturn200()
    {
        // Every endpoint stays open in PR 6a. [Authorize] arrives in PR 6b.
        var response = await _client.GetAsync("/api/unit-of-measures");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenEndpoint_WithAMalformedBearerToken_ShouldStillReturn200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/unit-of-measures");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        var response = await _client.SendAsync(request);

        // A failed bearer authentication does not challenge on an endpoint with no authorization
        // requirement; it leaves HttpContext.User unauthenticated and the request proceeds.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenEndpoint_WithAnExpiredBearerToken_ShouldStillReturn200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/unit-of-measures");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(expired: true));

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenEndpoint_WithAValidBearerToken_ShouldStillReturn200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/unit-of-measures");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(expired: false));

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string CreateToken(bool expired)
    {
        var now = DateTimeOffset.UtcNow;
        var (notBefore, expires) = expired
            ? (now.AddMinutes(-30).UtcDateTime, now.AddMinutes(-15).UtcDateTime)
            : (now.UtcDateTime, now.AddMinutes(15).UtcDateTime);

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
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = Guid.NewGuid().ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
