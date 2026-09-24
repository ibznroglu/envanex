using Envanex.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Envanex.IntegrationTests.Configuration;

/// <summary>
/// No database. Binds the <c>Demo</c> section out of the shipped
/// <c>src/Envanex.Web/appsettings.json</c>, so what actually deploys is asserted: the demo is off
/// unless an environment turns it on, and no password is committed.
/// </summary>
public sealed class DemoAppSettingsTests
{
    private static DemoAccountOptions ShippedOptions
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

            var section = configuration.GetSection(DemoAccountOptions.SectionName);

            section.Exists().ShouldBeTrue(
                $"The '{DemoAccountOptions.SectionName}' section is missing from {appSettingsPath}.");

            return section.Get<DemoAccountOptions>()!;
        }
    }

    [Fact]
    public void AppSettings_DemoEnabled_ShouldDefaultToFalse()
    {
        ShippedOptions.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void AppSettings_DemoPassword_ShouldBeEmptySoThatNoSecretIsCommitted()
    {
        // The demo password is a user-secret. A committed value fails here.
        ShippedOptions.Password.ShouldBeEmpty();
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
