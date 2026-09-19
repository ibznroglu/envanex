using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Authentication.DTOs;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;

namespace Envanex.Application.Authentication.Commands;

/// <summary>
/// Exchanges an email address and a password for an access token and a refresh token.
/// The handler never inspects or reclassifies the credential error it is given: a wrong password,
/// an unknown email address and a locked-out account are made indistinguishable inside
/// <see cref="IIdentityService"/>, and a branch here would undo that.
/// </summary>
public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, AuthenticationResponse>
{
    private const string BearerTokenType = "Bearer";

    private readonly IIdentityService _identityService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccessTokenIssuer _accessTokenIssuer;

    public LoginCommandHandler(
        IIdentityService identityService,
        IRefreshTokenService refreshTokenService,
        IAccessTokenIssuer accessTokenIssuer)
    {
        _identityService = identityService;
        _refreshTokenService = refreshTokenService;
        _accessTokenIssuer = accessTokenIssuer;
    }

    public async Task<Result<AuthenticationResponse>> HandleAsync(
        LoginCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<AuthenticatedUser> credentials =
            await _identityService.ValidateCredentialsAsync(command.Email, command.Password, ct);
        if (credentials.IsFailure)
        {
            // No refresh token is issued and no access token is minted for a failed credential
            // check, and the error is returned exactly as it arrived.
            return Result.Failure<AuthenticationResponse>(credentials.Error);
        }

        AuthenticatedUser user = credentials.Value;

        Result<IssuedRefreshToken> refreshToken = await _refreshTokenService.IssueAsync(user.Id, ct);
        if (refreshToken.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(refreshToken.Error);
        }

        IssuedAccessToken accessToken = _accessTokenIssuer.Issue(user);

        return Result.Success(new AuthenticationResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.Value.Token,
            refreshToken.Value.ExpiresAt,
            BearerTokenType));
    }
}
