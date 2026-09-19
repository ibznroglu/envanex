using Envanex.Application.Authentication;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Envanex.IntegrationTests.Configuration;

/// <summary>
/// No database. Binds the <c>Jwt</c> section out of the shipped
/// <c>src/Envanex.Web/appsettings.json</c>, so the lifetimes that actually deploy are asserted
/// once. Everything else in the suite asserts <c>now + argument</c> or reads its own in-memory
/// configuration, which a typo such as <c>AccessTokenMinutes: 150</c> would survive.
/// </summary>
public sealed class JwtAppSettingsTests
{
    private static JwtOptions ShippedOptions
    {
        get
        {
            var appSettingsPath = Path.Combine(
                FindSolutionDirectory(), "src", "Envanex.Web", "appsettings.json");

            File.Exists(appSettingsPath).ShouldBeTrue(
                $"appsettings.json not found at {appSettingsPath}; the walk-up may be broken.");

            var configuration = new ConfigurationBuilder()
                .AddJsonFile(appSettingsPath, optional: false)
                .Build();

            var section = configuration.GetSection(JwtOptions.SectionName);

            section.Exists().ShouldBeTrue(
                $"The '{JwtOptions.SectionName}' section is missing from {appSettingsPath}.");

            return section.Get<JwtOptions>()!;
        }
    }

    [Fact]
    public void AppSettings_AccessTokenMinutes_ShouldBeFifteen()
    {
        ShippedOptions.AccessTokenMinutes.ShouldBe(15);
    }

    [Fact]
    public void AppSettings_RefreshTokenIdleDays_ShouldBeSeven()
    {
        ShippedOptions.RefreshTokenIdleDays.ShouldBe(7);
    }

    [Fact]
    public void AppSettings_RefreshTokenAbsoluteDays_ShouldBeThirty()
    {
        ShippedOptions.RefreshTokenAbsoluteDays.ShouldBe(30);
    }

    [Fact]
    public void AppSettings_IssuerAndAudience_ShouldBeNonEmpty()
    {
        var options = ShippedOptions;

        options.Issuer.ShouldNotBeNullOrWhiteSpace();
        options.Audience.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AppSettings_SigningKey_ShouldBeEmptySoThatNoSecretIsCommitted()
    {
        // The signing key is a user-secret. A committed value fails here.
        ShippedOptions.SigningKey.ShouldBeEmpty();
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
