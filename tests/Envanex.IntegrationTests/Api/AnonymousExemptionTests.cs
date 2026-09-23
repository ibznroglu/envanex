using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Commands;
using Envanex.IntegrationTests.Fixtures;
using Envanex.Web.Extensions;
using Microsoft.AspNetCore.Hosting;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

/// <summary>
/// Rows 1–8, 11 and 12 of the PR 6b exemption table: every door the fallback policy leaves open
/// outside Development, and row 5's unmatched page path, a behaviour it changes without opening
/// anything. A missing exemption is a closed door that breaks something; a wrong one is an open
/// door. Each case below fails for one of the two.
/// </summary>
/// <remarks>
/// Rows 9 and 10 need a Development host and live in <c>DevelopmentEndpointExemptionTests</c>.
/// Row 13 is proved by <c>AuthApiTests.Register_ShouldReturn401</c>; this class carried a
/// byte-for-byte duplicate of it until the Phase 4 code review.
///
/// Every non-<c>/api/*</c> probe uses the non-redirecting client. A denied page is answered with a
/// 302 to <c>/login</c>, and a client that followed it would land on the anonymous login page and
/// report 200 whether or not the exemption under test exists.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class AnonymousExemptionTests : IAsyncLifetime
{
    private const string NotFoundPageMarker = "Sorry, the content you are looking for does not exist.";
    private const string UnknownPath = "/gibberish-unmatched-path";

    private const string TestEmail = "anonymous-exemption@envanex.local";

    private readonly SqlServerFixture _fixture;
    private readonly HttpClient _apiClient;
    private readonly HttpClient _pageClient;

    public AnonymousExemptionTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _apiClient = fixture.WebApplicationFactory.CreateClient();
        _pageClient = CookieAuthHelper.CreateNonRedirectingClient(fixture.WebApplicationFactory);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _apiClient.Dispose();
        _pageClient.Dispose();
        return Task.CompletedTask;
    }

    // Row 1.
    [Fact]
    public async Task Login_WithoutAuthentication_ShouldReturn200()
    {
        await IdentitySeeder.EnsureUserAsync(
            _fixture.WebApplicationFactory.Services, TestEmail, SqlServerFixture.SeededPassword);

        var response = await _apiClient.PostAsJsonAsync(
            "/api/auth/login", new LoginCommand(TestEmail, SqlServerFixture.SeededPassword));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Row 2. The 401 alone would prove nothing: the challenge is a 401 too. The detail is what
    // tells them apart — the refresh use case's Turkish message, not the challenge's.
    [Fact]
    public async Task Refresh_WithoutAuthentication_ShouldReturn401CarryingTheInvalidRefreshTokenDetail()
    {
        var response = await _apiClient.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshTokenCommand("this-token-was-never-issued"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("detail").GetString().ShouldBe(
            TurkishErrorMessages.GetMessage(AuthErrors.InvalidRefreshToken.Code, "fallback"),
            "The refresh endpoint was never reached; the fallback policy challenged it instead.");
    }

    // Row 3. Decision 14: logout must work for a client whose access token has already expired.
    [Fact]
    public async Task Logout_WithoutAuthentication_ShouldReturn204()
    {
        var response = await _apiClient.PostAsJsonAsync(
            "/api/auth/logout", new LogoutCommand("this-token-was-never-issued"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // Row 4. Load-bearing: this page is the re-execution target of every bodiless 4xx.
    [Fact]
    public async Task NotFoundPage_WithoutAuthentication_ShouldReturn200()
    {
        using var response = await _pageClient.GetAsync("/not-found");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain(NotFoundPageMarker);
    }

    // Row 5, anonymous half. The cookie scheme challenges before routing can report the miss, and
    // a 302 is not a 4xx, so the 404 re-execution never fires.
    [Fact]
    public async Task UnknownPath_WithoutAuthentication_ShouldRedirectToTheLoginPage()
    {
        using var response = await _pageClient.GetAsync(UnknownPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        CookieAuthHelper.GetRedirectPath(response).ShouldBe(CookieAuthHelper.LoginPath);

        var location = response.Headers.Location.ShouldNotBeNull();
        var query = new Uri(new Uri("http://localhost"), location).Query;

        query.ShouldBe("?ReturnUrl=" + Uri.EscapeDataString(UnknownPath));
    }

    // Row 5, signed-in half. The only caller for whom the 404 re-execution path is reachable, and
    // the case that makes row 4 matter.
    [Fact]
    public async Task UnknownPath_WhileSignedIn_ShouldReturn404AndRenderTheNotFoundPage()
    {
        var services = _fixture.WebApplicationFactory.Services;
        await IdentitySeeder.EnsureRoleAsync(services, EnvanexRoles.Administrator);
        await IdentitySeeder.EnsureUserAsync(services, SqlServerFixture.AdministratorEmail, SqlServerFixture.SeededPassword);
        await IdentitySeeder.EnsureUserInRoleAsync(services, SqlServerFixture.AdministratorEmail, EnvanexRoles.Administrator);

        using var client = await CookieAuthHelper.SignInAsync(
            _fixture.WebApplicationFactory, SqlServerFixture.AdministratorEmail, SqlServerFixture.SeededPassword);

        using var response = await client.GetAsync(UnknownPath);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain(NotFoundPageMarker);
    }

    // Row 6.
    [Fact]
    public async Task ErrorPage_WithoutAuthentication_ShouldReturn200()
    {
        using var response = await _pageClient.GetAsync("/Error");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Row 7.
    [Fact]
    public async Task LoginPage_WithoutAuthentication_ShouldReturn200()
    {
        using var response = await _pageClient.GetAsync(CookieAuthHelper.LoginPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Row 8. favicon.png is referenced unfingerprinted in App.razor.
    [Fact]
    public async Task StaticAsset_WithoutAuthentication_ShouldReturn200()
    {
        using var response = await _pageClient.GetAsync("/favicon.png");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Row 11. No production line of its own: the script is served out of the asset manifest, so
    // row 8's exemption covers it. The regression this guards is the login page losing enhanced
    // navigation.
    //
    // The derived host exists because the shared Testing host does not serve static web assets:
    // the framework wires them up automatically only under Development, so under Testing
    // blazor.web.js has no file behind it. Against that host the request passes authorization and then fails on
    // the missing file, which would read as an exemption failure when it is not. The exemption
    // itself was observed working against the shared host — the request reached the static-asset
    // endpoint rather than being denied — and the real host started with dotnet run serves the
    // script anonymously with 200. UseStaticWebAssets is scoped to this one test on purpose:
    // putting it on the shared factory would change the host for every other test to fix one.
    [Fact]
    public async Task BlazorFrameworkScript_WithoutAuthentication_ShouldReturn200()
    {
        using var factory = _fixture.WebApplicationFactory.WithWebHostBuilder(
            builder => builder.UseStaticWebAssets());
        using var client = CookieAuthHelper.CreateNonRedirectingClient(factory);

        using var response = await client.GetAsync("/_framework/blazor.web.js");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Row 12. All three, because an exemption that opened negotiate alone would still break every
    // circuit while a negotiate-only test stayed green. The success codes are framework-owned (the
    // spike recorded 200, 404 for no such circuit, and 400 for no circuit id), so the assertion is
    // that the request was not denied.
    //
    // A denial does not look the same on the three endpoints. .NET 10 puts
    // DisableCookieRedirectMetadata on negotiate and on the transport endpoint but not on
    // disconnect, so the cookie challenge answers those two with a bodiless 401 carrying Location
    // rather than a 302, and that 401 is then re-executed as /not-found. PR 6b's Phase 4 mutation
    // run removed the /_blazor convention and observed:
    //   negotiate  — 400 with Location /login?ReturnUrl=…   caught only by the Location assertion
    //   transport  — 401 with Location and an HTML body     caught by the 401 assertion
    //   disconnect — 302 to /login?ReturnUrl=…              caught by the 302 assertion
    // Each assertion is the one that catches one endpoint, so none of the three may be dropped.
    [Theory]
    [InlineData("POST", "/_blazor/negotiate?negotiateVersion=1")]
    [InlineData("GET", "/_blazor?id=00000000000000000000000000000000")]
    [InlineData("POST", "/_blazor/disconnect")]
    public async Task BlazorHubEndpoints_WithoutAuthentication_ShouldReturnNeither401NorARedirect(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await _pageClient.SendAsync(request);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(
            HttpStatusCode.Found,
            $"{method} {path} was denied and redirected to the login page.");
        response.Headers.Location.ShouldBeNull();
    }
}
