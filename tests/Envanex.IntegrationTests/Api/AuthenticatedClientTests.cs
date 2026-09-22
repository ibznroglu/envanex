using System.Net;
using System.Net.Http.Json;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Commands;
using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.IdentityModel.JsonWebTokens;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

/// <summary>
/// What <see cref="SqlServerFixture"/>'s authenticated clients are worth. Every other class in the
/// collection takes its client from here and asserts something else entirely, so a defect in the
/// token cache would surface as an unrelated 401 somewhere far away. These cases are where it
/// surfaces as itself.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuthenticatedClientTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public AuthenticatedClientTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// Business tables only. Two cases below write unit of measures through POST, and a code left
    /// behind by an earlier class would turn an expected 201 into a 409 and this class into a
    /// false red. The identity tables are deliberately left alone: resetting them here would throw
    /// the collection's cached tokens away for no reason.
    /// </summary>
    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SharedFactory_AdministratorClient_ShouldCarryABearerTokenInTheAdministratorRole()
    {
        using var client = await _fixture.CreateAdministratorClientAsync();

        var authorization = client.DefaultRequestHeaders.Authorization.ShouldNotBeNull(
            "The administrator client carries no Authorization header at all.");

        authorization.Scheme.ShouldBe("Bearer");

        var token = new JsonWebToken(authorization.Parameter.ShouldNotBeNull());

        token.Claims
            .Where(claim => string.Equals(claim.Type, EnvanexClaimTypes.Role, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ShouldContain(
                EnvanexRoles.Administrator,
                "The token was minted but carries no Administrator role, so every CanWrite policy would fail for it. "
                + "Claim types present: " + string.Join(", ", token.Claims.Select(claim => claim.Type)));
    }

    [Fact]
    public async Task RateLimitedFactory_AdministratorClient_ShouldLeaveBothGlobalPermitsUnspent()
    {
        // The token is minted on the shared host, so building this client must cost the
        // rate-limited host nothing: its 2-permit budget has to be entirely available to the
        // writes below.
        await using var factory = new RateLimitedWebApplicationFactory(_fixture.ConnectionString);
        using var client = await _fixture.CreateAdministratorClientAsync(factory);

        var first = await CreateUnitOfMeasureAsync(client, "AC-GL-1");
        var second = await CreateUnitOfMeasureAsync(client, "AC-GL-2");
        var third = await CreateUnitOfMeasureAsync(client, "AC-GL-3");

        first.ShouldBe(
            HttpStatusCode.Created,
            "The first write was rejected, so obtaining the client had already spent a global permit.");
        second.ShouldBe(HttpStatusCode.Created);
        third.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task LoginRateLimitedFactory_AdministratorClient_ShouldLeaveBothLoginPermitsUnspent()
    {
        await using var factory = new LoginRateLimitedWebApplicationFactory(_fixture.ConnectionString);
        using var client = await _fixture.CreateAdministratorClientAsync(factory);

        var command = new LoginCommand(SqlServerFixture.AdministratorEmail, SqlServerFixture.SeededPassword);

        using var first = await client.PostAsJsonAsync("/api/auth/login", command);
        using var second = await client.PostAsJsonAsync("/api/auth/login", command);
        using var third = await client.PostAsJsonAsync("/api/auth/login", command);

        first.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "The first login was rejected, so obtaining the client had already spent a login permit on this host.");
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task AccessToken_AfterResetIdentityAsync_ShouldBeReissuedRatherThanReused()
    {
        var before = await _fixture.GetAdministratorTokenAsync();

        await _fixture.ResetIdentityAsync();

        var after = await _fixture.GetAdministratorTokenAsync();

        JtiOf(after).ShouldNotBe(
            JtiOf(before),
            "The same token came back after the user behind it was deleted, so ResetIdentityAsync "
            + "no longer clears the fixture's token cache and every later class would authenticate "
            + "as a user that is not there.");
    }

    [Fact]
    public async Task AccessToken_WhenTheCachedTokenIsNearExpiry_ShouldBeRemintedRatherThanReused()
    {
        var before = await _fixture.GetAdministratorTokenAsync();

        _fixture.ExpireCachedToken(SqlServerFixture.AdministratorEmail);

        var after = await _fixture.GetAdministratorTokenAsync();

        JtiOf(after).ShouldNotBe(
            JtiOf(before),
            "A token within a minute of its expiry was handed out again. The bearer handler runs "
            + "with ClockSkew = TimeSpan.Zero, so a slow run would start answering 401 from an "
            + "arbitrary point onwards.");
    }

    private static async Task<HttpStatusCode> CreateUnitOfMeasureAsync(HttpClient client, string code)
    {
        // Codes unique to this class: the collection is serial, and a code another class seeded
        // would come back 409 and read as a spent permit.
        using var response = await client.PostAsJsonAsync(
            "/api/unit-of-measures",
            new CreateUnitOfMeasureCommand(code, $"Yetkili İstemci {code}", null, 1m));

        return response.StatusCode;
    }

    private static string JtiOf(string token)
        => new JsonWebToken(token).GetPayloadValue<string>(JwtRegisteredClaimNames.Jti);
}
