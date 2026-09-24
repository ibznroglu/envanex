namespace Envanex.Infrastructure.Identity;

/// <summary>
/// The bound shape of the <c>Demo</c> configuration section: the read-only demo account that
/// <see cref="DemoAccountSeeder"/> creates when <see cref="Enabled"/> is true.
/// </summary>
/// <remarks>
/// The password is never committed. <c>appsettings.json</c> ships it empty and it follows the JWT
/// signing key's path: user-secrets locally, App Service configuration in production.
/// </remarks>
public sealed class DemoAccountOptions
{
    public const string SectionName = "Demo";

    public bool Enabled { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
