using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

        // Verify Content-Type is application/problem+json
        response3.Content.Headers.ContentType.ShouldNotBeNull();
        response3.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");

        // Verify the body is a ProblemDetails JSON with status 429 and Turkish message
        var body = await response3.Content.ReadAsStringAsync();
        body.ShouldNotContain("<html", Case.Insensitive, "Response body must not be HTML; UseStatusCodePagesWithReExecute should not intercept a 429 with a body.");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        root.GetProperty("status").GetInt32().ShouldBe(429);
        root.GetProperty("type").GetString().ShouldBe("https://httpstatuses.io/429");
        root.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MutationEndpoint_ExceedingRateLimit_ShouldIncludeRetryAfterHeader()
    {
        // The RateLimitedWebApplicationFactory has a global limit of 2 requests per window.
        // Send 3 POST requests; the third is rejected and must tell the client how long to wait.
        var command1 = new CreateUnitOfMeasureCommand("RA-001", "Retry After 1", null, 1m);
        var command2 = new CreateUnitOfMeasureCommand("RA-002", "Retry After 2", null, 1m);
        var command3 = new CreateUnitOfMeasureCommand("RA-003", "Retry After 3", null, 1m);

        await _client.PostAsJsonAsync("/api/unit-of-measures", command1);
        await _client.PostAsJsonAsync("/api/unit-of-measures", command2);
        var rejectedResponse = await _client.PostAsJsonAsync("/api/unit-of-measures", command3);

        rejectedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // The Retry-After header must be present and expressed as a positive delta-seconds value.
        rejectedResponse.Headers.RetryAfter.ShouldNotBeNull();
        rejectedResponse.Headers.RetryAfter.Delta.ShouldNotBeNull();
        rejectedResponse.Headers.RetryAfter.Delta.Value.TotalSeconds.ShouldBeGreaterThan(0);
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

    [Fact]
    public void SharedFactory_WithGlobalLimiterDisabled_ShouldNotRegisterAGlobalLimiter()
    {
        // AddRateLimiter is unconditional so that the named "login" policy always exists; only the
        // GlobalLimiter assignment stays behind RateLimiting:Enabled. This proves that guard
        // survived the move out of the if block.
        var options = _fixture.WebApplicationFactory.Services
            .GetRequiredService<IOptions<RateLimiterOptions>>();

        options.Value.GlobalLimiter.ShouldBeNull();
    }

    [Fact]
    public async Task SharedFactory_WithGlobalLimiterDisabled_ShouldNotReject150ConsecutiveRequestsToTheUnitOfMeasuresList()
    {
        // 150 exceeds the production PermitLimit of 100, so an accidentally active global limiter
        // fails this test. A read endpoint on purpose: it writes no rows and does not collide with
        // the collection's reset semantics.
        using var client = _fixture.WebApplicationFactory.CreateClient();

        for (int request = 0; request < 150; request++)
        {
            var response = await client.GetAsync("/api/unit-of-measures");

            response.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"Request {request + 1} was not answered with 200; the global limiter is active on the shared factory.");
        }
    }
}
