using System.Security.Cryptography;
using System.Text;

namespace Envanex.Infrastructure.Identity;

/// <summary>
/// Hashes refresh token material for storage.
/// </summary>
/// <remarks>
/// SHA-256, not a password hash. The token is 256 bits of CSPRNG output, so no dictionary exists
/// to slow down and a work factor would only add ~100 ms to every refresh; a salt would also
/// destroy the indexed seek on <c>IX_RefreshTokens_TokenHash</c>. Not an HMAC either: that is a
/// second secret to deploy and rotate for no gain against the only realistic attacker, someone who
/// has read the table.
/// </remarks>
internal static class RefreshTokenHasher
{
    public static byte[] Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    /// <summary>
    /// Compares a presented token against a stored hash in constant time. The comparison is done
    /// here rather than left to a <c>WHERE TokenHash = @hash</c> so that the constant-time property
    /// lives in code that can be read, not in a query plan.
    /// </summary>
    public static bool Matches(string token, byte[] storedHash)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(storedHash);

        // FixedTimeEquals returns false for a length mismatch without leaking where it differed.
        return CryptographicOperations.FixedTimeEquals(Hash(token), storedHash);
    }
}
