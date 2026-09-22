using Envanex.Infrastructure.Identity;
using Envanex.IntegrationTests.Fixtures;
using Envanex.Web.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Envanex.IntegrationTests.Blazor;

/// <summary>
/// Decision 11: a connected circuit must not keep its principal after the user's security stamp
/// moves on. The comparison is exercised directly, because a live circuit would have to wait out
/// the thirty-minute interval.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SecurityStampRevalidationTests
{
    private const string TestEmail = "security-stamp@envanex.test";
    private const string TestPassword = "Envanex-Test-Parola-1";

    private readonly SqlServerFixture _fixture;

    public SecurityStampRevalidationTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact]
    public void AuthenticationStateProvider_ShouldBeTheRevalidatingIdentityProvider()
    {
        using var scope = _fixture.WebApplicationFactory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>()
            .ShouldBeOfType<RevalidatingIdentityAuthenticationStateProvider>();
    }

    [Fact]
    public async Task ValidateSecurityStampAsync_AfterTheUsersSecurityStampChanges_ShouldReturnFalse()
    {
        var services = _fixture.WebApplicationFactory.Services;
        await IdentitySeeder.EnsureUserAsync(services, TestEmail, TestPassword);

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<EnvanexUser>>();
        var claimsPrincipalFactory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<EnvanexUser>>();
        var provider = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>()
            .ShouldBeOfType<RevalidatingIdentityAuthenticationStateProvider>();

        var user = (await userManager.FindByEmailAsync(TestEmail)).ShouldNotBeNull();

        // Built by the same factory CookieSignInService uses, so it carries the same stamp claim a
        // real cookie principal does.
        var principal = await claimsPrincipalFactory.CreateAsync(user);

        (await provider.ValidateSecurityStampAsync(principal, CancellationToken.None)).ShouldBeTrue(
            "Control: a freshly issued principal must validate, or the false below proves nothing.");

        (await userManager.UpdateSecurityStampAsync(user)).Succeeded.ShouldBeTrue();

        (await provider.ValidateSecurityStampAsync(principal, CancellationToken.None)).ShouldBeFalse(
            "The principal still validates after its user's security stamp changed; an open circuit would keep it.");
    }
}
