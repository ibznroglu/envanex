using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Commands;
using Envanex.Application.Authentication.DTOs;
using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.Infrastructure;
using Envanex.Infrastructure.Identity;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// The demo account seeder against the real <c>auth</c> tables, and the demo account end to end
/// through a host that seeded it at startup.
/// </summary>
/// <remarks>
/// Every case starts from an empty identity store, and every case that calls
/// <see cref="DemoAccountSeeder.SeedAsync"/> directly builds its own provider after that reset, so
/// the call under test is the only writer the assertions can see.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class DemoAccountSeederTests : IAsyncLifetime
{
    private const string DemoEmail = "demo-seeder@envanex.test";

    /// <summary>
    /// The address the shipped <c>appsettings.json</c> carries. The end-to-end case overrides only
    /// what the plan names — the switch and the password — so it logs in as the shipped address.
    /// </summary>
    private const string ShippedDemoEmail = "demo@envanex.local";

    private const string UnitOfMeasuresPath = "/api/unit-of-measures";

    private readonly SqlServerFixture _fixture;

    public DemoAccountSeederTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetIdentityAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedAsync_WhenDemoIsDisabled_ShouldSeedTheRolesButNotTheAccount()
    {
        await using var provider = BuildProvider(enabled: false, DemoEmail, SqlServerFixture.SeededPassword);

        var result = await DemoAccountSeeder.SeedAsync(provider);

        result.IsSuccess.ShouldBeTrue();
        (await ReadRoleNamesAsync()).ShouldBe([EnvanexRoles.Administrator, EnvanexRoles.Viewer]);
        (await CountUsersAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SeedAsync_WhenDemoIsEnabled_ShouldCreateTheAccountInTheViewerRole()
    {
        await using var provider = BuildProvider(enabled: true, DemoEmail, SqlServerFixture.SeededPassword);

        var result = await DemoAccountSeeder.SeedAsync(provider);

        result.IsSuccess.ShouldBeTrue();
        (await ReadRolesOfAsync(provider, DemoEmail)).ShouldBe([EnvanexRoles.Viewer]);
    }

    [Fact]
    public async Task SeedAsync_CalledTwice_ShouldSucceedAndShouldNotDuplicateTheAccount()
    {
        await using var provider = BuildProvider(enabled: true, DemoEmail, SqlServerFixture.SeededPassword);

        var first = await DemoAccountSeeder.SeedAsync(provider);
        var second = await DemoAccountSeeder.SeedAsync(provider);

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        (await CountUsersAsync()).ShouldBe(1);
        (await ReadRolesOfAsync(provider, DemoEmail)).ShouldBe([EnvanexRoles.Viewer]);
    }

    [Fact]
    public async Task SeedAsync_WhenDemoIsEnabledWithABlankPassword_ShouldThrow()
    {
        await using var provider = BuildProvider(enabled: true, DemoEmail, password: string.Empty);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => DemoAccountSeeder.SeedAsync(provider));

        exception.Message.ShouldContain("Demo:Password");

        // Checked before any write, so a misconfigured host leaves nothing behind — not even roles.
        (await ReadRoleNamesAsync()).ShouldBeEmpty();
        (await CountUsersAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SeedAsync_WhenDemoIsEnabledWithAPolicyViolatingPassword_ShouldThrow()
    {
        // Too short and no upper-case letter: two of the policy AddEnvanexIdentity declares.
        const string policyViolatingPassword = "tooshort1";

        await using var provider = BuildProvider(enabled: true, DemoEmail, policyViolatingPassword);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => DemoAccountSeeder.SeedAsync(provider));

        exception.Message.ShouldContain("PasswordTooShort");
        exception.Message.ShouldContain("PasswordRequiresUpper");
        exception.Message.ShouldNotContain(policyViolatingPassword);

        (await ReadRoleNamesAsync()).ShouldBeEmpty();
        (await CountUsersAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SeedAsync_WhenDemoIsEnabledWithAnInvalidEmail_ShouldReturnFailure()
    {
        // RequireUniqueEmail = true is what makes Identity validate the address at all.
        const string invalidEmail = "not-an-email";

        await using var provider = BuildProvider(enabled: true, invalidEmail, SqlServerFixture.SeededPassword);

        var result = await DemoAccountSeeder.SeedAsync(provider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DemoAccount.CreateFailed");
        result.Error.Message.ShouldContain("InvalidEmail");
        result.Error.Message.ShouldNotContain(SqlServerFixture.SeededPassword);

        (await CountUsersAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DemoAccount_ShouldBeAbleToReadButNotWrite()
    {
        // Constructed here, after InitializeAsync's reset, so the reset cannot delete what this
        // host seeds at startup.
        await using var factory = new EnvanexWebApplicationFactory(
            _fixture.ConnectionString,
            "Testing",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Demo:Enabled"] = "true",
                ["Demo:Password"] = SqlServerFixture.SeededPassword,
            });

        using var client = factory.CreateClient();

        using var login = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginCommand(ShippedDemoEmail, SqlServerFixture.SeededPassword));

        login.StatusCode.ShouldBe(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());

        var body = (await login.Content.ReadFromJsonAsync<AuthenticationResponse>()).ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);

        using var read = await client.GetAsync(UnitOfMeasuresPath);
        using var write = await client.PostAsJsonAsync(
            UnitOfMeasuresPath, new CreateUnitOfMeasureCommand("DEMO-W", "Demo Write", null, 1m));

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<IReadOnlyList<string>> ReadRoleNamesAsync()
    {
        await using var context = _fixture.CreateIdentityDbContext();

        return await context.Roles
            .Select(role => role.Name!)
            .OrderBy(name => name)
            .ToListAsync();
    }

    private async Task<int> CountUsersAsync()
    {
        await using var context = _fixture.CreateIdentityDbContext();

        return await context.Users.CountAsync();
    }

    private static async Task<IReadOnlyList<string>> ReadRolesOfAsync(IServiceProvider provider, string email)
    {
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<EnvanexUser>>();

        var user = (await userManager.FindByEmailAsync(email))
            .ShouldNotBeNull($"No user with the email '{email}' was seeded.");

        return [.. await userManager.GetRolesAsync(user)];
    }

    private ServiceProvider BuildProvider(bool enabled, string email, string password)
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
                ["Demo:Enabled"] = enabled ? "true" : "false",
                ["Demo:Email"] = email,
                ["Demo:Password"] = password,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
