using System.Diagnostics.CodeAnalysis;
using System.Net;
using Envanex.IntegrationTests.Fixtures;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

/// <summary>
/// Exemption rows 9 and 10: the OpenAPI document and the Scalar reference are mapped only in
/// Development, and open there. And the guard that goes with them: a Development host is not an
/// open one.
/// </summary>
/// <remarks>
/// A Development host differs in more than OpenAPI. <c>UseExceptionHandler("/Error")</c> and
/// <c>UseHsts()</c> are registered only outside Development, so an unhandled exception here surfaces
/// as a raw 500 or the developer exception page. An unexpected 500 in this class is that, not an
/// exemption failure.
/// </remarks>
[Collection(DatabaseCollection.Name)]
[SuppressMessage("IDisposable", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed via IAsyncLifetime.DisposeAsync")]
public sealed class DevelopmentEndpointExemptionTests : IAsyncLifetime
{
    private const string DevelopmentEnvironment = "Development";

    private readonly SqlServerFixture _fixture;
    private EnvanexWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public DevelopmentEndpointExemptionTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new EnvanexWebApplicationFactory(
            _fixture.ConnectionString,
            DevelopmentEnvironment,
            new Dictionary<string, string?>(StringComparer.Ordinal));

        // Not following redirects: a denial here would be a 302 to /login, and a followed one
        // would read as the anonymous login page's 200.
        _client = CookieAuthHelper.CreateNonRedirectingClient(_factory);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task OpenApiDocument_InDevelopment_WithoutAuthentication_ShouldReturn200()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ScalarReference_InDevelopment_WithoutAuthentication_ShouldReturn200()
    {
        // The trailing slash matters: Scalar answers /scalar with its own 302 to scalar/, which
        // this non-redirecting client would report as-is.
        using var response = await _client.GetAsync("/scalar/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_InDevelopment_WithoutAuthentication_ShouldReturn401()
    {
        using var response = await _client.GetAsync("/api/unit-of-measures");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
