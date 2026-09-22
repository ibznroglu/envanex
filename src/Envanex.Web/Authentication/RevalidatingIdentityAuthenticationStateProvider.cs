using System.Security.Claims;
using Envanex.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Envanex.Web.Authentication;

/// <summary>
/// Re-checks a connected circuit's user against the database every
/// <see cref="RevalidationInterval"/>, and signs the circuit out when the user is gone or its
/// security stamp has moved on.
/// </summary>
/// <remarks>
/// Without this a circuit keeps the principal it opened with for as long as it stays connected, so
/// locking out or deleting an account would have no effect on a UI session already open. The cost
/// is one security-stamp read per connected circuit per interval.
/// </remarks>
public sealed class RevalidatingIdentityAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<IdentityOptions> _identityOptions;

    public RevalidatingIdentityAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> identityOptions)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _identityOptions = identityOptions;
    }

    protected override TimeSpan RevalidationInterval { get; } = TimeSpan.FromMinutes(30);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authenticationState);

        return ValidateSecurityStampAsync(authenticationState.User, cancellationToken);
    }

    /// <summary>
    /// <see langword="true"/> while the principal's user still exists and still carries the
    /// security stamp the principal was issued with.
    /// </summary>
    /// <remarks>
    /// Extracted from <see cref="ValidateAuthenticationStateAsync"/> so the comparison can be proved
    /// without a live circuit. Its own scope, because the provider outlives any single request and
    /// a <see cref="UserManager{TUser}"/> held across revalidations would serve a stale entity.
    /// </remarks>
    internal async Task<bool> ValidateSecurityStampAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // UserManager's API takes no CancellationToken, so this is the one place it can be honoured.
        ct.ThrowIfCancellationRequested();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<EnvanexUser>>();

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return false;
        }

        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        var principalStamp = principal.FindFirstValue(_identityOptions.Value.ClaimsIdentity.SecurityStampClaimType);
        var userStamp = await userManager.GetSecurityStampAsync(user);

        return string.Equals(principalStamp, userStamp, StringComparison.Ordinal);
    }
}
