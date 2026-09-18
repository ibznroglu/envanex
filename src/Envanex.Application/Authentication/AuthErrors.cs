using Envanex.Domain.Common;

namespace Envanex.Application.Authentication;

/// <summary>
/// Authentication error codes. They live in Envanex.Application rather than Envanex.Domain
/// because there is no auth aggregate and there will not be one; ResultMappingTests scans both
/// assemblies so these codes stay covered by the same reflection guards as the domain codes.
/// </summary>
public static class AuthErrors
{
    /// <summary>
    /// The single answer to a wrong password, an unknown email address and a locked-out account.
    /// There is deliberately no lockout-specific code: three identical answers at the source
    /// cannot drift apart into three distinguishable responses later.
    /// </summary>
    public static readonly Error InvalidCredentials = new("Auth.InvalidCredentials", "Email or password is incorrect.");
    public static readonly Error InvalidRefreshToken = new("Auth.InvalidRefreshToken", "The refresh token is not valid.");
    public static readonly Error RefreshTokenExpired = new("Auth.RefreshTokenExpired", "The refresh token has expired.");
    public static readonly Error RefreshTokenReused = new("Auth.RefreshTokenReused", "The refresh token has already been used; the session was terminated.");
    public static readonly Error EmailRequired = new("Auth.EmailRequired", "Email is required.");
    public static readonly Error EmailInvalid = new("Auth.EmailInvalid", "Email is not a valid address.");
    public static readonly Error PasswordRequired = new("Auth.PasswordRequired", "Password is required.");
    public static readonly Error RefreshTokenRequired = new("Auth.RefreshTokenRequired", "Refresh token is required.");
}
