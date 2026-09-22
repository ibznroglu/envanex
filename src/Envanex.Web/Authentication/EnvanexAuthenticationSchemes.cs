namespace Envanex.Web.Authentication;

/// <summary>
/// The names of the host's own authentication schemes. The bearer scheme keeps the framework's
/// <c>JwtBearerDefaults.AuthenticationScheme</c> and is not repeated here.
/// </summary>
public static class EnvanexAuthenticationSchemes
{
    /// <summary>
    /// The policy scheme every default points at. It authenticates nothing itself: it forwards each
    /// request to the bearer scheme or to <see cref="Cookie"/> by path prefix.
    /// </summary>
    public const string Selector = "Envanex";

    /// <summary>
    /// The Blazor UI's cookie scheme. Never read on <c>/api/*</c>.
    /// </summary>
    public const string Cookie = "Envanex.Cookie";
}
