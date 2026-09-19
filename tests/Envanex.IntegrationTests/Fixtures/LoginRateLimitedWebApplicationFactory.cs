using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Envanex.IntegrationTests.Fixtures;

/// <summary>
/// A dedicated factory with a deliberately low login rate limit (2 requests per window), used only
/// by <c>LoginRateLimiterTests</c>. The global limiter is switched off here so that it cannot mask
/// the result: a 429 produced by this factory can only have come from the login policy.
/// </summary>
/// <remarks>
/// The 2-permit budget is per factory, so callers construct one per test rather than one per class.
/// </remarks>
public sealed class LoginRateLimitedWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public LoginRateLimitedWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:EnvanexDb", _connectionString);

        builder.UseSetting("RateLimiting:Enabled", "false");

        builder.UseSetting("RateLimiting:Login:Enabled", "true");
        builder.UseSetting("RateLimiting:Login:PermitLimit", "2");
        builder.UseSetting("RateLimiting:Login:WindowSeconds", "60");

        // AddEnvanexIdentity and AddEnvanexJwtBearer both validate the Jwt section at startup and
        // appsettings.json ships an empty signing key, so this host needs the section too.
        builder.UseSetting("Jwt:Issuer", "https://envanex.local");
        builder.UseSetting("Jwt:Audience", "envanex-api");
        builder.UseSetting("Jwt:SigningKey", EnvanexWebApplicationFactory.TestSigningKey);
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenIdleDays", "7");
        builder.UseSetting("Jwt:RefreshTokenAbsoluteDays", "30");
    }
}
