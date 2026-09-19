using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Authentication.DTOs;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;

namespace Envanex.Application.Authentication.Commands;

/// <summary>
/// Rotates a refresh token into a fresh pair. Rotation, reuse detection and expiry all belong to
/// <see cref="IRefreshTokenService"/>; this handler only forwards the failure it is given and
/// mints an access token for the user the rotation returned.
/// </summary>
public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, AuthenticationResponse>
{
    private const string BearerTokenType = "Bearer";

    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccessTokenIssuer _accessTokenIssuer;

    public RefreshTokenCommandHandler(
        IRefreshTokenService refreshTokenService,
        IAccessTokenIssuer accessTokenIssuer)
    {
        _refreshTokenService = refreshTokenService;
        _accessTokenIssuer = accessTokenIssuer;
    }

    public async Task<Result<AuthenticationResponse>> HandleAsync(
        RefreshTokenCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<RotatedRefreshToken> rotation =
            await _refreshTokenService.RotateAsync(command.RefreshToken, ct);
        if (rotation.IsFailure)
        {
            // A failed rotation mints no access token: the presented token is unknown, revoked,
            // expired or replayed, and none of those may extend a session.
            return Result.Failure<AuthenticationResponse>(rotation.Error);
        }

        RotatedRefreshToken rotated = rotation.Value;

        IssuedAccessToken accessToken = _accessTokenIssuer.Issue(rotated.User);

        return Result.Success(new AuthenticationResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            rotated.Token,
            rotated.ExpiresAt,
            BearerTokenType));
    }
}
