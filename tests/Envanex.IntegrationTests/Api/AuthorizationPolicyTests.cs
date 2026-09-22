using System.Net;
using System.Net.Http.Json;
using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

/// <summary>
/// The two named policies over HTTP, against real tokens from a real login, and the fallback policy
/// read out of the host's own options.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuthorizationPolicyTests : IAsyncLifetime
{
    private const string UnitOfMeasuresPath = "/api/unit-of-measures";

    private readonly SqlServerFixture _fixture;

    public AuthorizationPolicyTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// Business tables only: one case below writes a unit of measure, and a code left behind by an
    /// earlier class would turn its 201 into a 409. The identity tables are left alone so the
    /// collection's cached tokens survive.
    /// </summary>
    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CanRead_WithAnAdministratorToken_ShouldReturn200()
    {
        using var client = await _fixture.CreateAdministratorClientAsync();

        using var response = await client.GetAsync(UnitOfMeasuresPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CanRead_WithAViewerToken_ShouldReturn200()
    {
        using var client = await _fixture.CreateViewerClientAsync();

        using var response = await client.GetAsync(UnitOfMeasuresPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CanWrite_WithAnAdministratorToken_ShouldReturn201()
    {
        using var client = await _fixture.CreateAdministratorClientAsync();

        using var response = await client.PostAsJsonAsync(
            UnitOfMeasuresPath, new CreateUnitOfMeasureCommand("AP-ADM", "Policy Administrator", null, 1m));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CanWrite_WithAViewerToken_ShouldReturn403()
    {
        // Read-only is a role, and this is what makes it true: the Viewer is authenticated, is
        // admitted by CanRead, and is refused by CanWrite.
        using var client = await _fixture.CreateViewerClientAsync();

        using var response = await client.PostAsJsonAsync(
            UnitOfMeasuresPath, new CreateUnitOfMeasureCommand("AP-VWR", "Policy Viewer", null, 1m));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public void FallbackPolicy_ShouldRequireAnAuthenticatedUser()
    {
        var options = _fixture.WebApplicationFactory.Services
            .GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value;

        var fallback = options.FallbackPolicy.ShouldNotBeNull(
            "No fallback policy is registered, so every endpoint without an attribute is open.");

        fallback.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Datasource_WithoutAuthentication_ShouldReturn401()
    {
        // The widest read surface in the application, so it gets a closure test by name.
        using var client = _fixture.WebApplicationFactory.CreateClient();

        using var response = await client.GetAsync("/api/products/datasource");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
