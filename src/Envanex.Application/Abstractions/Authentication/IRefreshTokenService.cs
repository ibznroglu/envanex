using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;

namespace Envanex.Application.Abstractions.Authentication;

public interface IRefreshTokenService
{
    Task<Result<IssuedRefreshToken>> IssueAsync(Guid userId, CancellationToken ct = default);

    Task<Result<RotatedRefreshToken>> RotateAsync(string presentedToken, CancellationToken ct = default);

    /// <summary>
    /// Revokes the family of the presented token only. Ending every session for a user is a
    /// separate feature and is not part of this surface.
    /// </summary>
    Task<Result> RevokeFamilyAsync(string presentedToken, CancellationToken ct = default);
}
