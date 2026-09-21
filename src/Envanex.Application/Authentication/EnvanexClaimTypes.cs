namespace Envanex.Application.Authentication;

/// <summary>
/// Claim types this application writes and reads. One declaration, read by the signing half in
/// Infrastructure and by the validating half in Web: two copies of this string is the same failure
/// mode ADR 0007 already names for the migrations-history table.
/// </summary>
public static class EnvanexClaimTypes
{
    /// <summary>
    /// The role claim carried by the access token.
    /// </summary>
    /// <remarks>
    /// Short form on purpose. <c>MapInboundClaims</c> is switched off in the bearer handler, so the
    /// claim type that is written is the claim type that is read; nothing remaps it to a URI.
    /// </remarks>
    public const string Role = "role";
}
