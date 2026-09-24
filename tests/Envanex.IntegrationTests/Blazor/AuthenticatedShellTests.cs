using System.Net;
using System.Text.RegularExpressions;
using Envanex.Application.Authentication;
using Envanex.IntegrationTests.Fixtures;
using Shouldly;

namespace Envanex.IntegrationTests.Blazor;

/// <summary>
/// The layout around every page: who is signed in, and what an anonymous visitor gets instead.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class AuthenticatedShellTests
{
    private readonly SqlServerFixture _fixture;

    public AuthenticatedShellTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact]
    public async Task GetHome_WhileSignedInAsAnAdministrator_ShouldRenderTheSignedInUsersEmailInTheNavMenu()
    {
        var services = _fixture.WebApplicationFactory.Services;
        await IdentitySeeder.EnsureRoleAsync(services, EnvanexRoles.Administrator);
        await IdentitySeeder.EnsureUserAsync(services, SqlServerFixture.AdministratorEmail, SqlServerFixture.SeededPassword);
        await IdentitySeeder.EnsureUserInRoleAsync(services, SqlServerFixture.AdministratorEmail, EnvanexRoles.Administrator);

        using var client = await CookieAuthHelper.SignInAsync(
            _fixture.WebApplicationFactory, SqlServerFixture.AdministratorEmail, SqlServerFixture.SeededPassword);

        using var response = await client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var match = NavUserEmail().Match(await response.Content.ReadAsStringAsync());

        match.Success.ShouldBeTrue("The NavMenu rendered no signed-in user element.");
        WebUtility.HtmlDecode(match.Groups["email"].Value).ShouldBe(SqlServerFixture.AdministratorEmail);
    }

    [Fact]
    public async Task GetHome_WhileSignedOut_ShouldRedirectToTheLoginPage()
    {
        // Denied at the endpoint layer, and answered by the cookie scheme's LoginPath: under static
        // server rendering AuthorizeRouteView never gets as far as rendering (PR 6b Spike D).
        using var client = CookieAuthHelper.CreateNonRedirectingClient(_fixture.WebApplicationFactory);

        using var response = await client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        CookieAuthHelper.GetRedirectPath(response).ShouldBe(CookieAuthHelper.LoginPath);
    }

    [GeneratedRegex("<span id=\"nav-user-email\"[^>]*>(?<email>[^<]*)</span>")]
    private static partial Regex NavUserEmail();
}
