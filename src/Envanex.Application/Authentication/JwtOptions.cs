namespace Envanex.Application.Authentication;

/// <summary>
/// The bound shape of the <c>Jwt</c> configuration section. A plain POCO in Application so that
/// Infrastructure (which signs tokens) and Web (which validates them) read one declaration
/// instead of two sets of string literals. It pulls in no package.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// A shorter key than this cannot carry 256 bits of entropy, which is what HMAC-SHA256
    /// signing assumes.
    /// </summary>
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; }
    public int RefreshTokenIdleDays { get; set; }
    public int RefreshTokenAbsoluteDays { get; set; }
}
