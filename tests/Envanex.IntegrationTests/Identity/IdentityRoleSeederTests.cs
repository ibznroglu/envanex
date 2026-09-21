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
    /// <summary>
    /// How many times the two-host race is run. A single pass proves nothing about an interleaving
    /// nobody controls. Kept as a constant so the number is turned in one place.
    /// </summary>
    private const int ConcurrencyIterations = 20;

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

    [Fact]
    public async Task EnsureRolesAsync_RunByTwoHostsAtOnce_ShouldNotThrowAndShouldLeaveExactlyTwoRoles()
    {
        for (var iteration = 0; iteration < ConcurrencyIterations; iteration++)
        {
            // Every iteration starts from the state a cold host sees, which is the only state in
            // which the insert can be raced at all.
            await _fixture.ResetIdentityAsync();

            // Nothing here is orchestrated by a barrier, a hook or a seam: the two racers are the
            // production method, unmodified, so only what holds under every interleaving is
            // asserted. Whether they serialize or truly overlap is not this test's to decide.
            await Should.NotThrowAsync(async () =>
            {
                var first = SeedInOwnScopeAsync();
                var second = SeedInOwnScopeAsync();

                await Task.WhenAll(first, second);
            });

            (await ReadRoleNamesAsync()).ShouldBe(
                [EnvanexRoles.Administrator, EnvanexRoles.Viewer],
                $"Iteration {iteration} did not leave exactly the two seeded roles.");
        }
    }

    private Task SeedInOwnScopeAsync()
    {
        // Task.Run so the two racers really are on different threads rather than interleaved by
        // the await points of a single one. EnsureRolesAsync opens its own scope, and therefore its
        // own RoleManager and DbContext, per call.
        return Task.Run(() => IdentityRoleSeeder.EnsureRolesAsync(_provider));
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
