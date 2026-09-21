using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;
using Envanex.Infrastructure;
using Envanex.Infrastructure.Identity;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// What the credential path answers about roles. The access token issuer writes whatever this
/// returns, so a role that is not read here is a role the bearer half of the application never
/// sees.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class IdentityServiceRoleTests : IAsyncLifetime
{
    private const string AdministratorEmail = "identity-service-role-admin@envanex.test";
    private const string RoleLessEmail = "identity-service-role-none@envanex.test";
    private const string Password = "CorrectHorse1Battery";

    private readonly SqlServerFixture _fixture;
    private ServiceProvider _provider = null!;

    public IdentityServiceRoleTests(SqlServerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetIdentityAsync();

        _provider = BuildProvider();

        await IdentityRoleSeeder.EnsureRolesAsync(_provider);
        await IdentitySeeder.CreateUserAsync(_provider, AdministratorEmail, Password);
        await IdentitySeeder.CreateUserAsync(_provider, RoleLessEmail, Password);
        await AddToRoleAsync(AdministratorEmail, EnvanexRoles.Administrator);
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task ValidateCredentialsAsync_UserInTheAdministratorRole_ShouldReturnThatRole()
    {
        var result = await ValidateAsync(AdministratorEmail, Password);

        result.IsSuccess.ShouldBeTrue($"Validation failed with: {result.Error.Code}");
        result.Value.Roles.ShouldBe([EnvanexRoles.Administrator]);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_UserWithNoRole_ShouldReturnAnEmptyRoleList()
    {
        var result = await ValidateAsync(RoleLessEmail, Password);

        result.IsSuccess.ShouldBeTrue($"Validation failed with: {result.Error.Code}");

        // Empty, never null: the issuer reads Count on it without a guard.
        result.Value.Roles.ShouldBeEmpty();
    }

    private async Task<Result<AuthenticatedUser>> ValidateAsync(string email, string password)
    {
        using var scope = _provider.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IIdentityService>()
            .ValidateCredentialsAsync(email, password);
    }

    private async Task AddToRoleAsync(string email, string role)
    {
        using var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<EnvanexUser>>();

        var user = await userManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();

        var result = await userManager.AddToRoleAsync(user, role);

        result.Succeeded.ShouldBeTrue(
            $"Failed to add '{email}' to '{role}': {string.Join(", ", result.Errors.Select(error => error.Code))}");
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
