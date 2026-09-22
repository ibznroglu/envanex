using Envanex.IntegrationTests.Fixtures;
using Envanex.Web.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Envanex.IntegrationTests.Blazor;

/// <summary>
/// The auth cookie's <c>Secure</c> flag, from the setting to the options the host actually runs
/// with. The whole suite runs under <c>Testing</c>, where the factory's override wins, so without
/// these cases a wrong default or a deleted development value would go unnoticed by every other
/// test.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CookieSchemeOptionsTests
{
    private readonly SqlServerFixture _fixture;

    public CookieSchemeOptionsTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact]
    public void Resolve_WhenTheSettingIsAbsent_ShouldBeAlways()
    {
        // Production never sets the key, so this is the value that ships.
        CookieSecurePolicyResolver.Resolve(null).ShouldBe(CookieSecurePolicy.Always);
    }

    [Fact]
    public void Resolve_WhenTheSettingIsUnrecognised_ShouldThrow()
    {
        // A typo must stop the host, never silently downgrade the cookie.
        var exception = Should.Throw<InvalidOperationException>(() => CookieSecurePolicyResolver.Resolve("SameAsRequst"));

        exception.Message.ShouldContain(CookieSecurePolicyResolver.SettingKey);
    }

    [Fact]
    public void AppSettingsDevelopment_SecurePolicy_ShouldBeSameAsRequestSoTheHttpProfileCanSignIn()
    {
        var path = Path.Combine(FindSolutionDirectory(), "src", "Envanex.Web", "appsettings.Development.json");

        File.Exists(path).ShouldBeTrue($"appsettings.Development.json not found at {path}; the walk-up may be broken.");

        // Loaded through the same JSON configuration provider the host uses, so the comment in the
        // file is parsed exactly as it is at startup.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(path, optional: false)
            .Build();

        configuration[CookieSecurePolicyResolver.SettingKey].ShouldBe(
            "SameAsRequest",
            "Without it the http launch profile cannot sign in: the browser discards a Secure cookie sent over plain HTTP.");
    }

    [Fact]
    public void TestHost_CookieOptions_ShouldUseSameAsRequestSoTheCookieIsReplayedOverHttp()
    {
        var options = _fixture.WebApplicationFactory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(EnvanexAuthenticationSchemes.Cookie);

        options.Cookie.SecurePolicy.ShouldBe(
            CookieSecurePolicy.SameAsRequest,
            "EnvanexWebApplicationFactory must set Auth:Cookie:SecurePolicy=SameAsRequest; a Secure cookie is "
            + "never replayed over the test server's http://localhost, and every cookie test would fail for that reason.");
    }

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.GetFiles("Envanex.slnx").Length > 0
                || directory.GetFiles("Envanex.sln").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Envanex.sln or Envanex.slnx not found. The test must run from within the solution directory.");
    }
}
