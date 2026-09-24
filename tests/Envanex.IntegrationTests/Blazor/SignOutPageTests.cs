using System.Net;
using Envanex.IntegrationTests.Fixtures;
using Shouldly;

namespace Envanex.IntegrationTests.Blazor;

/// <summary>
/// The statically rendered <c>/sign-out</c> confirm page.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SignOutPageTests : IAsyncLifetime
{
    // In no role on purpose: signing out must need a session, never a permission.
    private const string TestEmail = "sign-out-page@envanex.test";
    private const string TestPassword = "Envanex-Test-Parola-1";

    private readonly SqlServerFixture _fixture;

    public SignOutPageTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    public Task InitializeAsync()
        => IdentitySeeder.EnsureUserAsync(_fixture.WebApplicationFactory.Services, TestEmail, TestPassword);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostSignOutForm_WhileSignedIn_ShouldClearTheAuthCookieAndRedirectToTheLoginPage()
    {
        using var client = await CookieAuthHelper.SignInAsync(_fixture.WebApplicationFactory, TestEmail, TestPassword);

        // Control: the session is live before the sign-out, so the redirect below can only have
        // come from the sign-out and not from a sign-in that never took.
        using (var before = await client.GetAsync(CookieAuthHelper.SignOutPath))
        {
            before.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var token = await CookieAuthHelper.ReadAntiforgeryTokenAsync(client, CookieAuthHelper.SignOutPath);

        using var response = await CookieAuthHelper.PostFormAsync(
            client, CookieAuthHelper.SignOutPath, CookieAuthHelper.SignOutFormName, token);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        CookieAuthHelper.GetRedirectPath(response).ShouldBe(CookieAuthHelper.LoginPath);

        // Clearing a cookie is a Set-Cookie with an expiry in the past.
        CookieAuthHelper.GetAuthCookieHeaders(response)
            .ShouldHaveSingleItem()
            .ShouldContain("expires=Thu, 01 Jan 1970", Case.Insensitive);

        // And the client, replaying whatever it still holds, is anonymous again.
        using var after = await client.GetAsync(CookieAuthHelper.SignOutPath);

        after.StatusCode.ShouldBe(HttpStatusCode.Found);
        CookieAuthHelper.GetRedirectPath(after).ShouldBe(CookieAuthHelper.LoginPath);
    }

    [Fact]
    public async Task GetSignOutPage_WhileSignedOut_ShouldRedirectToTheLoginPage()
    {
        using var client = CookieAuthHelper.CreateNonRedirectingClient(_fixture.WebApplicationFactory);

        using var response = await client.GetAsync(CookieAuthHelper.SignOutPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        CookieAuthHelper.GetRedirectPath(response).ShouldBe(CookieAuthHelper.LoginPath);
    }
}
