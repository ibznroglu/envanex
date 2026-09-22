using System.Net;
using System.Text.RegularExpressions;
using Envanex.IntegrationTests.Fixtures;
using Envanex.Web.Extensions;
using Shouldly;

namespace Envanex.IntegrationTests.Blazor;

/// <summary>
/// The statically rendered <c>/login</c> page, driven the way a browser drives it: GET the form,
/// POST it back with the antiforgery token it carried.
/// </summary>
/// <remarks>
/// The identity tables are not reset here. Every user this class touches is its own, created
/// idempotently, so leaving the collection's other users and their cached tokens alone costs
/// nothing and saves a re-mint in every class that runs after this one.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed partial class LoginPageTests : IAsyncLifetime
{
    private const string TestEmail = "login-page@envanex.test";
    private const string LockedOutEmail = "login-page-locked-out@envanex.test";
    private const string TestPassword = "Envanex-Test-Parola-1";
    private const string WrongPassword = "Yanlis-Test-Parola-9";

    private readonly SqlServerFixture _fixture;
    private HttpClient _client = null!;

    public LoginPageTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    private static string InvalidCredentialsMessage =>
        TurkishErrorMessages.GetMessage("Auth.InvalidCredentials", "untranslated");

    public async Task InitializeAsync()
    {
        await IdentitySeeder.EnsureUserAsync(_fixture.WebApplicationFactory.Services, TestEmail, TestPassword);

        _client = CookieAuthHelper.CreateNonRedirectingClient(_fixture.WebApplicationFactory);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetLoginPage_ShouldReturn200CarryingAnAntiforgeryTokenField()
    {
        using var response = await _client.GetAsync(CookieAuthHelper.LoginPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();

        body.ShouldContain(
            $"name=\"{CookieAuthHelper.AntiforgeryFieldName}\"",
            Case.Sensitive,
            "The login form rendered no antiforgery field, so no POST of it could ever pass UseAntiforgery.");

        (await CookieAuthHelper.ReadAntiforgeryTokenAsync(_client, CookieAuthHelper.LoginPath))
            .ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PostLoginForm_WithValidCredentials_ShouldSetTheAuthCookieAndRedirectToHome()
    {
        var token = await CookieAuthHelper.ReadAntiforgeryTokenAsync(_client, CookieAuthHelper.LoginPath);

        using var response = await CookieAuthHelper.PostLoginFormAsync(_client, token, TestEmail, TestPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        CookieAuthHelper.GetRedirectPath(response).ShouldBe("/");

        var authCookie = CookieAuthHelper.GetAuthCookieHeaders(response).ShouldHaveSingleItem();

        authCookie.ShouldContain("httponly", Case.Insensitive);
        authCookie.ShouldContain("samesite=lax", Case.Insensitive);
    }

    [Fact]
    public async Task PostLoginForm_WithAnInvalidPassword_ShouldNotSetACookieAndShouldRenderTheTurkishCredentialMessage()
    {
        var token = await CookieAuthHelper.ReadAntiforgeryTokenAsync(_client, CookieAuthHelper.LoginPath);

        using var response = await CookieAuthHelper.PostLoginFormAsync(_client, token, TestEmail, WrongPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CookieAuthHelper.GetAuthCookieHeaders(response).ShouldBeEmpty();

        var body = await response.Content.ReadAsStringAsync();

        ReadErrorMessage(body).ShouldBe(InvalidCredentialsMessage);
        body.ShouldNotContain(
            WrongPassword,
            Case.Sensitive,
            "The re-rendered form echoed the submitted password back into the page.");
    }

    [Fact]
    public async Task PostLoginForm_WithNoAntiforgeryToken_ShouldReturn400()
    {
        // The GET is still made, so the antiforgery cookie is present: only the form field is
        // missing, which is what isolates the token as the cause of the 400.
        await CookieAuthHelper.ReadAntiforgeryTokenAsync(_client, CookieAuthHelper.LoginPath);

        using var response = await CookieAuthHelper.PostLoginFormAsync(_client, null, TestEmail, TestPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        CookieAuthHelper.GetAuthCookieHeaders(response).ShouldBeEmpty();
    }

    [Fact]
    public async Task PostLoginForm_ForALockedOutAccount_ShouldRenderTheSameTurkishMessageAsAWrongPassword()
    {
        var services = _fixture.WebApplicationFactory.Services;
        await IdentitySeeder.EnsureUserAsync(services, LockedOutEmail, TestPassword);
        await IdentitySeeder.LockOutAsync(services, LockedOutEmail);

        var token = await CookieAuthHelper.ReadAntiforgeryTokenAsync(_client, CookieAuthHelper.LoginPath);

        using var wrongPassword = await CookieAuthHelper.PostLoginFormAsync(_client, token, TestEmail, WrongPassword);

        // The correct password, on purpose: the only thing standing between this attempt and a
        // cookie is the lockout, and the page must not say so.
        using var lockedOut = await CookieAuthHelper.PostLoginFormAsync(_client, token, LockedOutEmail, TestPassword);

        lockedOut.StatusCode.ShouldBe(HttpStatusCode.OK);
        CookieAuthHelper.GetAuthCookieHeaders(lockedOut).ShouldBeEmpty();

        var lockedOutMessage = ReadErrorMessage(await lockedOut.Content.ReadAsStringAsync());
        var wrongPasswordMessage = ReadErrorMessage(await wrongPassword.Content.ReadAsStringAsync());

        lockedOutMessage.ShouldBe(
            wrongPasswordMessage,
            "A locked-out account must be indistinguishable from a wrong password (ADR 0007).");
        lockedOutMessage.ShouldBe(InvalidCredentialsMessage);
    }

    /// <summary>
    /// The decoded text of the page's error element. Decoded because the renderer writes the Turkish
    /// letters as character references.
    /// </summary>
    private static string ReadErrorMessage(string body)
    {
        var match = LoginError().Match(body);

        match.Success.ShouldBeTrue("The page rendered no login error element.");

        return WebUtility.HtmlDecode(match.Groups["message"].Value);
    }

    [GeneratedRegex("<div id=\"login-error\"[^>]*>(?<message>[^<]*)</div>")]
    private static partial Regex LoginError();
}
