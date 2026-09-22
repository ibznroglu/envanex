using System.Net;
using Envanex.Application.Authentication;
using Envanex.IntegrationTests.Fixtures;
using Shouldly;

namespace Envanex.IntegrationTests.Blazor;

/// <summary>
/// The cookie half of two claims. First, that a cookie forbid carries a body and is therefore never
/// re-executed as the not-found page. Second, that <c>RequireRole</c> evaluated against a
/// <em>cookie</em> identity — whose roles sit under <c>ClaimTypes.Role</c>, not the bearer's short
/// <c>role</c> — admits a Viewer and refuses a user with no role.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CookieAuthPipelineTests
{
    private const string NotFoundPageMarker = "Sorry, the content you are looking for does not exist.";

    private readonly SqlServerFixture _fixture;

    public CookieAuthPipelineTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact]
    public async Task Cookie_AuthenticatedUserWithNoRole_ShouldReturn403WithABodyAndNotTheNotFoundPage()
    {
        // Created here: CookieAuthHelper signs in, it never creates, and nothing else in the
        // collection creates this user.
        await IdentitySeeder.EnsureUserAsync(
            _fixture.WebApplicationFactory.Services, SqlServerFixture.RoleLessEmail, SqlServerFixture.SeededPassword);

        using var client = await CookieAuthHelper.SignInAsync(
            _fixture.WebApplicationFactory, SqlServerFixture.RoleLessEmail, SqlServerFixture.SeededPassword);

        using var response = await client.GetAsync("/");

        response.StatusCode.ShouldBe(
            HttpStatusCode.Forbidden,
            "A signed-in user holding no role must fail CanRead on the landing page.");

        var body = await response.Content.ReadAsStringAsync();

        body.ShouldNotBeNullOrWhiteSpace("The cookie forbid carried no body, so it is open to re-execution.");
        body.ShouldNotContain(
            NotFoundPageMarker,
            Case.Sensitive,
            "The 403 was re-executed as the /not-found page; OnRedirectToAccessDenied wrote no body.");
    }

    [Fact]
    public async Task Cookie_AuthenticatedViewer_ShouldReadTheHomePage()
    {
        var services = _fixture.WebApplicationFactory.Services;
        await IdentitySeeder.EnsureRoleAsync(services, EnvanexRoles.Viewer);
        await IdentitySeeder.EnsureUserAsync(services, SqlServerFixture.ViewerEmail, SqlServerFixture.SeededPassword);
        await IdentitySeeder.EnsureUserInRoleAsync(services, SqlServerFixture.ViewerEmail, EnvanexRoles.Viewer);

        using var client = await CookieAuthHelper.SignInAsync(
            _fixture.WebApplicationFactory, SqlServerFixture.ViewerEmail, SqlServerFixture.SeededPassword);

        using var response = await client.GetAsync("/");

        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "A Viewer's cookie identity must satisfy CanRead; the role claim did not reach RequireRole.");
    }
}
