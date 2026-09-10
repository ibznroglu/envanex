using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.IntegrationTests.Fixtures;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
[SuppressMessage("IDisposable", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed via IAsyncLifetime.DisposeAsync")]
public sealed class RateLimiterTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private RateLimitedWebApplicationFactory _rateLimitedFactory = null!;
    private HttpClient _client = null!;

    public RateLimiterTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _rateLimitedFactory = new RateLimitedWebApplicationFactory(_fixture.ConnectionString);
        _client = _rateLimitedFactory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _rateLimitedFactory.DisposeAsync();
    }

    [Fact]
    public async Task MutationEndpoint_ExceedingRateLimit_ShouldReturn429()
    {
        // The RateLimitedWebApplicationFactory has a global limit of 2 requests per window.
        // Send 3 POST requests; the third should be rate-limited.
        var command1 = new CreateUnitOfMeasureCommand("RL-001", "Rate Limit 1", null, 1m);
        var command2 = new CreateUnitOfMeasureCommand("RL-002", "Rate Limit 2", null, 1m);
        var command3 = new CreateUnitOfMeasureCommand("RL-003", "Rate Limit 3", null, 1m);

        var response1 = await _client.PostAsJsonAsync("/api/unit-of-measures", command1);
        var response2 = await _client.PostAsJsonAsync("/api/unit-of-measures", command2);
        var response3 = await _client.PostAsJsonAsync("/api/unit-of-measures", command3);

        // First two should succeed (201 Created)
        response1.StatusCode.ShouldBe(HttpStatusCode.Created);
        response2.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Third should be rate-limited (429 Too Many Requests)
        response3.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task MutationEndpoint_UnderRateLimit_ShouldReturn201()
    {
        // A single request under the limit should succeed.
        // This test ensures the rate limiter doesn't reject everything.
        var command = new CreateUnitOfMeasureCommand("RL-OK", "Under Limit", null, 1m);

        var response = await _client.PostAsJsonAsync("/api/unit-of-measures", command);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
