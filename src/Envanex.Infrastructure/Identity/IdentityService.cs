using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Envanex.Infrastructure.Identity;

/// <summary>
/// The only place ASP.NET Core Identity is spoken to. Every credential failure — a wrong password,
/// an unknown email address and a locked-out account — returns the same
/// <see cref="AuthErrors.InvalidCredentials"/> instance, so no branch can leak which one occurred
/// and no later change can make two of them answer differently.
/// </summary>
/// <remarks>
/// Lockout is observable through this logger and through Identity's own meter, never through the
/// response. The password is verified before the lockout check so that a locked-out attempt pays
/// the same PBKDF2 cost as a wrong password; a sudden fast "no" is the signal that would otherwise
/// confirm both that the account exists and that the lockout threshold was reached.
/// </remarks>
internal sealed partial class IdentityService : IIdentityService
{
    private readonly UserManager<EnvanexUser> _userManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        UserManager<EnvanexUser> userManager,
        TimeProvider timeProvider,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        // UserManager's API takes no CancellationToken, so this is the one place the caller's
        // token can be honoured at all.
        ct.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            // The known and accepted half of the timing channel: no hash is verified here, so an
            // unknown address answers faster than a known one. Closing it needs a dummy
            // verification, which is out of scope for this PR and recorded in the roadmap.
            return Result.Failure<AuthenticatedUser>(AuthErrors.InvalidCredentials);
        }

        // Unconditional and before the lockout check. The result is captured, not acted on yet.
        var passwordIsCorrect = await _userManager.CheckPasswordAsync(user, password);

        if (await _userManager.IsLockedOutAsync(user))
        {
            // Neither AccessFailedAsync nor ResetAccessFailedCountAsync runs here, whatever the
            // password was: incrementing would let an attacker extend a victim's lockout forever,
            // and resetting would hand a locked-out account a way back in.
            LogLockedOutAttempt(user.Id);

            return Result.Failure<AuthenticatedUser>(AuthErrors.InvalidCredentials);
        }

        if (!passwordIsCorrect)
        {
            await _userManager.AccessFailedAsync(user);

            // There is deliberately no second IsLockedOutAsync call here. With one error code
            // there is nothing to choose after incrementing, and the absence of that re-check is
            // what structurally guarantees AccessFailedAsync cannot fire twice on one attempt.
            // AccessFailedAsync updates the tracked entity in place, so LockoutEnd is readable
            // without a second round trip.
            if (user.LockoutEnd > _timeProvider.GetUtcNow())
            {
                LogLockedOut(user.Id, _userManager.Options.Lockout.MaxFailedAccessAttempts);
            }

            return Result.Failure<AuthenticatedUser>(AuthErrors.InvalidCredentials);
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        // The success path only, and deliberately after the reset. Every failure branch above
        // returns before this line: a role lookup on any of them would add a round trip that only
        // that branch pays for, which is a fourth timing channel on top of the three the remarks
        // above account for.
        var roles = await _userManager.GetRolesAsync(user);

        return Result.Success(new AuthenticatedUser(user.Id, user.Email!, user.UserName!, [.. roles]));
    }

    // Logs carry the user id and nothing else. The email address is the credential the attacker
    // supplied and the password must never reach a sink of any kind.
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Login attempt against locked-out account {UserId}")]
    private partial void LogLockedOutAttempt(Guid userId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Account {UserId} locked out after {MaxFailedAccessAttempts} failed attempts")]
    private partial void LogLockedOut(Guid userId, int maxFailedAccessAttempts);
}
