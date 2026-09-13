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
    }
}
