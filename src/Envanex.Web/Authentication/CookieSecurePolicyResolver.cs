namespace Envanex.Web.Authentication;

/// <summary>
/// Turns the <c>Auth:Cookie:SecurePolicy</c> setting into the auth cookie's
/// <see cref="CookieSecurePolicy"/>.
/// </summary>
/// <remarks>
/// Production never sets the key, so absent means <see cref="CookieSecurePolicy.Always"/>. Only the
/// plain-HTTP hosts — the <c>http</c> launch profile and the integration test server — set
/// <c>SameAsRequest</c>, because a <c>Secure</c> cookie delivered over HTTP is never sent back.
/// </remarks>
internal static class CookieSecurePolicyResolver
{
    public const string SettingKey = "Auth:Cookie:SecurePolicy";

    /// <exception cref="InvalidOperationException">
    /// The value is present but is neither <c>Always</c> nor <c>SameAsRequest</c>. A typo must stop
    /// the host rather than silently downgrade the cookie, and <c>None</c> is deliberately not
    /// accepted at all.
    /// </exception>
    public static CookieSecurePolicy Resolve(string? configured)
    {
        if (configured is null)
        {
            return CookieSecurePolicy.Always;
        }

        if (string.Equals(configured, nameof(CookieSecurePolicy.Always), StringComparison.Ordinal))
        {
            return CookieSecurePolicy.Always;
        }

        if (string.Equals(configured, nameof(CookieSecurePolicy.SameAsRequest), StringComparison.Ordinal))
        {
            return CookieSecurePolicy.SameAsRequest;
        }

        throw new InvalidOperationException(
            $"'{configured}' is not a valid value for {SettingKey}. "
            + $"Use '{nameof(CookieSecurePolicy.Always)}' or '{nameof(CookieSecurePolicy.SameAsRequest)}', "
            + "or leave the setting out to get Always.");
    }
}
