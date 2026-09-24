using Envanex.Infrastructure.Identity;

namespace Envanex.Web.Extensions;

/// <summary>
/// The one call site of identity seeding, run between <c>app.Build()</c> and <c>app.Run()</c>.
/// </summary>
public static class IdentitySeedingExtensions
{
    /// <summary>
    /// Seeds the roles and, when <c>Demo:Enabled</c> is true, the read-only demo account.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Seeding failed. Thrown rather than logged so the host fails at boot: a host that comes up
    /// without what its configuration asked for is worse than one that does not come up.
    /// </exception>
    public static async Task SeedIdentityAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var result = await DemoAccountSeeder.SeedAsync(app.Services);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Identity seeding failed. {result.Error.Code}: {result.Error.Message}");
        }
    }
}
