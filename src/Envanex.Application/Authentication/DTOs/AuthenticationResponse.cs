namespace Envanex.Application.Authentication.DTOs;

/// <summary>
/// The body returned by login and refresh. The refresh token travels in the body, not in a
/// cookie: the consumer of PR 6a is the REST surface, and the Blazor cookie scheme arrives with
/// PR 6b and makes its own decision there.
/// </summary>
public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    string TokenType);
