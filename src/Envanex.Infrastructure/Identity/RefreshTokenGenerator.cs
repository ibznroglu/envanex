using System.Buffers.Text;
using System.Security.Cryptography;

namespace Envanex.Infrastructure.Identity;

/// <summary>
/// Produces the opaque refresh token material. The token carries no claims and means nothing to
/// anyone who has not seen the row it hashes to.
/// </summary>
internal static class RefreshTokenGenerator
{
    /// <summary>
    /// 32 bytes — 256 bits of entropy, which is what makes a plain SHA-256 of the token safe to
    /// store.
    /// </summary>
    public const int TokenByteLength = 32;

    /// <summary>
    /// Base64Url so the value survives a JSON body, a URL and a header untouched: no <c>+</c>,
    /// no <c>/</c> and no <c>=</c> padding to be re-encoded by a client.
    /// </summary>
    public static string CreateToken()
        => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenByteLength));
}
