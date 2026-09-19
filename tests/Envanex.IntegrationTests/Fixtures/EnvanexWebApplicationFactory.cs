using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Envanex.IntegrationTests.Fixtures;

public sealed class EnvanexWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public EnvanexWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:EnvanexDb", _connectionString);

        // Disable rate limiting so that the combined test count across all test classes
        // does not exceed the limit and cause random 429 responses.
        builder.UseSetting("RateLimiting:Enabled", "false");

        // appsettings.json ships an empty Jwt:SigningKey on purpose, and AddEnvanexIdentity
        // refuses to start without one. The host under test therefore has to be given the whole
        // section here; the values mirror the shipped numbers so the factory cannot quietly
        // change the lifetimes the production settings declare.
        builder.UseSetting("Jwt:Issuer", "https://envanex.local");
        builder.UseSetting("Jwt:Audience", "envanex-api");
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenIdleDays", "7");
        builder.UseSetting("Jwt:RefreshTokenAbsoluteDays", "30");
    }

    /// <summary>
    /// A test-only signing key, comfortably over the 32-byte minimum. It signs nothing that
    /// outlives a test run.
    /// </summary>
    internal const string TestSigningKey = "envanex-integration-test-signing-key-0123456789";
}
