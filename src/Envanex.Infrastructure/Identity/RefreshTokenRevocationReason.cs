namespace Envanex.Infrastructure.Identity;

/// <summary>
/// Why a refresh token stopped being usable. Stored as its name so that a row read directly in the
/// database explains itself without a lookup table, and so that adding a reason is not a migration.
/// </summary>
internal enum RefreshTokenRevocationReason
{
    Logout,
    Reuse,
    Expired,
}
