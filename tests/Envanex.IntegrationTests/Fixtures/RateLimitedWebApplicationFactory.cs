using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Envanex.IntegrationTests.Fixtures;

/// <summary>
/// A dedicated factory with a deliberately low rate limit (2 requests per window)
/// used only by <c>RateLimiterTests</c> to verify rate limiter correctness.
/// This factory is NOT shared with SqlServerFixture or any other test class.
/// </summary>
public sealed class RateLimitedWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public RateLimitedWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:EnvanexDb", _connectionString);

        // Enable rate limiting with a deliberately low limit for testing
        builder.UseSetting("RateLimiting:Enabled", "true");
        builder.UseSetting("RateLimiting:PermitLimit", "2");
        builder.UseSetting("RateLimiting:WindowSeconds", "60");

        // AddEnvanexIdentity validates the Jwt section at startup and appsettings.json ships an
        // empty signing key, so this host needs the section too.
        builder.UseSetting("Jwt:Issuer", "https://envanex.local");
        builder.UseSetting("Jwt:Audience", "envanex-api");
        builder.UseSetting("Jwt:SigningKey", EnvanexWebApplicationFactory.TestSigningKey);
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenIdleDays", "7");
        builder.UseSetting("Jwt:RefreshTokenAbsoluteDays", "30");
    }
}
