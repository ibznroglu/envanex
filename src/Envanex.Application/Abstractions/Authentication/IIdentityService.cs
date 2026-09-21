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
    /// Returns the authenticated user — carrying the roles it holds — or a failure carrying
    /// <c>AuthErrors.InvalidCredentials</c>. A wrong password, an unknown email address and a
    /// locked-out account all produce that same error instance.
    /// </summary>
    /// <remarks>
    /// The roles are read on the success path only. Reading them on a failure branch would make a
    /// second round trip observable in the response time of a failed login, which is the timing
    /// channel ADR 0007 closes.
    /// </remarks>
    Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);
}
