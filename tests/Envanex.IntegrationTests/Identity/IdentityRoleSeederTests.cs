using Envanex.Application.Authentication;
using Envanex.Infrastructure;
using Envanex.Infrastructure.Identity;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// The role seeder against the real <c>auth.AspNetRoles</c> table. It runs at every host start, so
/// "a second run is a no-op" is a contract rather than a nicety.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class IdentityRoleSeederTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private ServiceProvider _provider = null!;

    public IdentityRoleSeederTests(SqlServerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetIdentityAsync();

        _provider = BuildProvider();
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task EnsureRolesAsync_OnAnEmptyDatabase_ShouldCreateAdministratorAndViewer()
    {
        await IdentityRoleSeeder.EnsureRolesAsync(_provider);

        (await ReadRoleNamesAsync()).ShouldBe([EnvanexRoles.Administrator, EnvanexRoles.Viewer]);
    }

    [Fact]
    public async Task EnsureRolesAsync_CalledTwice_ShouldNotFailAndShouldNotDuplicate()
    {
        await IdentityRoleSeeder.EnsureRolesAsync(_provider);
        await IdentityRoleSeeder.EnsureRolesAsync(_provider);

        (await ReadRoleNamesAsync()).ShouldBe([EnvanexRoles.Administrator, EnvanexRoles.Viewer]);
    }

    private async Task<IReadOnlyList<string>> ReadRoleNamesAsync()
    {
        await using var context = _fixture.CreateIdentityDbContext();

        return await context.Roles
            .Select(role => role.Name!)
            .OrderBy(name => name)
            .ToListAsync();
    }

    private ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EnvanexDb"] = _fixture.ConnectionString,
                ["Jwt:Issuer"] = "https://envanex.local",
                ["Jwt:Audience"] = "envanex-api",
                ["Jwt:SigningKey"] = EnvanexWebApplicationFactory.TestSigningKey,
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenIdleDays"] = "7",
                ["Jwt:RefreshTokenAbsoluteDays"] = "30",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
