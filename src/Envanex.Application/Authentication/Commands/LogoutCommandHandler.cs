using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Abstractions.Messaging;
using Envanex.Domain.Common;

namespace Envanex.Application.Authentication.Commands;

/// <summary>
/// Revokes the family of the presented refresh token, and only that family. A logout that cannot
/// guarantee revocation reports the failure rather than a success: the revocation failure is
/// propagated unchanged.
/// </summary>
public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand, bool>
{
    private readonly IRefreshTokenService _refreshTokenService;

    public LogoutCommandHandler(IRefreshTokenService refreshTokenService)
    {
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<bool>> HandleAsync(LogoutCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result revocation = await _refreshTokenService.RevokeFamilyAsync(command.RefreshToken, ct);
        if (revocation.IsFailure)
        {
            return Result.Failure<bool>(revocation.Error);
        }

        return Result.Success(true);
    }
}
