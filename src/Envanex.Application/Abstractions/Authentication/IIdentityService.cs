using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;

namespace Envanex.Application.Abstractions.Authentication;

/// <summary>
/// The Application-side contract for ASP.NET Core Identity. UserManager and SignInManager never
/// appear in this project; the implementation lives in Envanex.Infrastructure.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Returns the authenticated user, or a failure carrying
    /// <c>AuthErrors.InvalidCredentials</c>. A wrong password, an unknown email address and a
    /// locked-out account all produce that same error instance.
    /// </summary>
    Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);
}
