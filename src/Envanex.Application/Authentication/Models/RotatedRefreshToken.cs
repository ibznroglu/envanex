namespace Envanex.Application.Authentication.Models;

/// <summary>
/// The result of a successful rotation: the new token plus the user the rotated family belongs to,
/// so that refresh can issue an access token without a second lookup.
/// </summary>
public sealed record RotatedRefreshToken(AuthenticatedUser User, string Token, DateTimeOffset ExpiresAt);
