using Envanex.Application.Abstractions.Authentication;
using Envanex.Infrastructure.Identity;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// The registration surface: what <c>AddEnvanexIdentity</c> refuses to start with, and what it
/// leaves resolvable once it does.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AddEnvanexIdentityTests
{
    private readonly SqlServerFixture _fixture;

    public AddEnvanexIdentityTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public void AddEnvanexIdentity_BlankSigningKey_ShouldThrowInvalidOperationExceptionNamingTheUserSecretsCommand()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<InvalidOperationException>(
            () => services.AddEnvanexIdentity(BuildConfiguration(signingKey: "")));

        // Fail fast at startup, and say how to fix it: the same shape as the connection-string
        // guard, because appsettings.json ships an empty key on purpose.
        exception.Message.ShouldContain("Jwt:SigningKey");
        exception.Message.ShouldContain("dotnet user-secrets set");
    }

    [Fact]
    public void AddEnvanexIdentity_SigningKeyShorterThan32Bytes_ShouldThrow()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<InvalidOperationException>(
            () => services.AddEnvanexIdentity(BuildConfiguration(signingKey: "too-short-key")));

        exception.Message.ShouldContain("Jwt:SigningKey");
    }

    [Fact]
    public void AddEnvanexIdentity_MissingJwtSection_ShouldThrow()
    {
        var services = new ServiceCollection();
        var empty = new ConfigurationBuilder().Build();

        // A missing section binds to a default JwtOptions, which is blank, which the guard
        // rejects — the host must not come up with an unusable token configuration.
        Should.Throw<InvalidOperationException>(() => services.AddEnvanexIdentity(empty));
    }

    [Fact]
    public void AddEnvanexIdentity_ShouldResolveIIdentityService()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IIdentityService>().ShouldNotBeNull();
    }

    [Fact]
    public void AddEnvanexIdentity_ShouldResolveIRefreshTokenService()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IRefreshTokenService>().ShouldNotBeNull();
    }

    [Fact]
    public void AddEnvanexIdentity_ShouldResolveIAccessTokenIssuer()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<IAccessTokenIssuer>().ShouldNotBeNull();
    }

    [Fact]
    public void AddEnvanexIdentity_ShouldResolveUserManagerOfEnvanexUser()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<UserManager<EnvanexUser>>().ShouldNotBeNull();
    }

    [Fact]
    public void AddEnvanexIdentity_ShouldConfigureMaxFailedAccessAttemptsAsFive()
    {
        using var provider = BuildProvider();

        // Lockout is invisible in every response by design, so IOptions is the only place this
        // number can be asserted at all.
        provider.GetRequiredService<IOptions<IdentityOptions>>()
            .Value.Lockout.MaxFailedAccessAttempts.ShouldBe(5);
    }

    [Fact]
    public void AddEnvanexIdentity_ShouldConfigureDefaultLockoutTimeSpanAsFifteenMinutes()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<IOptions<IdentityOptions>>()
            .Value.Lockout.DefaultLockoutTimeSpan.ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void AddEnvanexIdentity_ShouldNotOverrideAPreRegisteredTimeProvider()
    {
        var fake = new FakeTimeProvider();
        using var provider = BuildProvider(services => services.AddSingleton<TimeProvider>(fake));

        // TryAddSingleton, not AddSingleton: a test that registers a fake clock first must keep it.
        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(fake);
    }

    [Fact]
    public void AddEnvanexIdentity_WithNoTimeProviderRegistered_ShouldResolveTimeProviderSystem()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(TimeProvider.System);
    }

    private ServiceProvider BuildProvider(Action<IServiceCollection>? before = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        before?.Invoke(services);

        services.AddDbContext<EnvanexIdentityDbContext>(
            options => options.UseEnvanexIdentitySqlServer(_fixture.ConnectionString));

        services.AddEnvanexIdentity(BuildConfiguration());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static IConfiguration BuildConfiguration(string? signingKey = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "https://envanex.local",
                ["Jwt:Audience"] = "envanex-api",
                ["Jwt:SigningKey"] = signingKey ?? EnvanexWebApplicationFactory.TestSigningKey,
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenIdleDays"] = "7",
                ["Jwt:RefreshTokenAbsoluteDays"] = "30",
            })
            .Build();
}
