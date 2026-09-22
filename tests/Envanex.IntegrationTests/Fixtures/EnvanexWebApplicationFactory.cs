using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Envanex.IntegrationTests.Fixtures;

public sealed class EnvanexWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _environment;
    private readonly IReadOnlyDictionary<string, string?> _settingOverrides;

    public EnvanexWebApplicationFactory(string connectionString)
        : this(connectionString, TestingEnvironment, EmptyOverrides)
    {
    }

    /// <summary>
    /// A host that differs from the collection's shared one by an environment name and a handful
    /// of settings.
    /// </summary>
    /// <remarks>
    /// This overload exists so that a later phase configures <em>this</em> type instead of adding
    /// another <see cref="WebApplicationFactory{TEntryPoint}"/> subclass for every variation. The
    /// overrides are applied last in <see cref="ConfigureWebHost"/>, so a caller can replace a
    /// value the shared configuration below already set.
    /// </remarks>
    public EnvanexWebApplicationFactory(
        string connectionString,
        string environment,
        IReadOnlyDictionary<string, string?> settingOverrides)
    {
        ArgumentNullException.ThrowIfNull(settingOverrides);

        _connectionString = connectionString;
        _environment = environment;
        _settingOverrides = settingOverrides;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(_environment);

        builder.UseSetting("ConnectionStrings:EnvanexDb", _connectionString);

        // Disable rate limiting so that the combined test count across all test classes
        // does not exceed the limit and cause random 429 responses.
        builder.UseSetting("RateLimiting:Enabled", "false");

        // The login policy has its own switch and must be turned off here too. This factory lives
        // for the whole collection and every login lands in the single "unknown" partition, so a
        // real 5/300 s budget would be consumed across classes and produce flaky 429s.
        builder.UseSetting("RateLimiting:Login:Enabled", "false");

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

        // The default client's BaseAddress is http://localhost and UseHttpsRedirection no-ops under
        // TestServer, so an auth cookie marked Secure would never be replayed by the client's
        // cookie container, and every cookie test would fail for a reason unrelated to the code
        // under test. Production keeps Always because the setting is absent there.
        builder.UseSetting("Auth:Cookie:SecurePolicy", "SameAsRequest");

        // Last on purpose: the caller's overrides win over every default above.
        foreach (var (key, value) in _settingOverrides)
        {
            builder.UseSetting(key, value);
        }
    }

    /// <summary>
    /// A test-only signing key, comfortably over the 32-byte minimum. It signs nothing that
    /// outlives a test run.
    /// </summary>
    internal const string TestSigningKey = "envanex-integration-test-signing-key-0123456789";

    private const string TestingEnvironment = "Testing";

    private static readonly IReadOnlyDictionary<string, string?> EmptyOverrides =
        new Dictionary<string, string?>(StringComparer.Ordinal);
}
