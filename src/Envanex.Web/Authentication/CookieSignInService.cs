using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;
using Envanex.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Envanex.Web.Authentication;

/// <summary>
/// Signs a user in to, and out of, the Blazor UI's cookie scheme.
/// </summary>
/// <remarks>
/// Hand-rolled rather than <c>SignInManager</c>, which <c>AddIdentityCore</c> deliberately does not
/// register. Credentials are checked through <see cref="IIdentityService"/>, the same path
/// <c>POST /api/auth/login</c> takes, so the two front doors share one lockout, one
/// <see cref="AuthErrors.InvalidCredentials"/> for every credential failure, and the
/// password-before-lockout ordering that keeps a locked-out attempt as slow as a wrong password.
/// <para>
/// Only callable from a statically rendered component: signing in writes <c>Set-Cookie</c>, which
/// is impossible once a response has started and meaningless over a circuit.
/// </para>
/// </remarks>
public sealed class CookieSignInService
{
    private readonly IIdentityService _identityService;
    private readonly UserManager<EnvanexUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<EnvanexUser> _claimsPrincipalFactory;

    public CookieSignInService(
        IIdentityService identityService,
        UserManager<EnvanexUser> userManager,
        IUserClaimsPrincipalFactory<EnvanexUser> claimsPrincipalFactory)
    {
        _identityService = identityService;
        _userManager = userManager;
        _claimsPrincipalFactory = claimsPrincipalFactory;
    }

    /// <summary>
    /// Validates the credentials and, on success, issues the auth cookie.
    /// </summary>
    /// <returns>
    /// The authenticated user, or the failure exactly as <see cref="IIdentityService"/> returned
    /// it. No cookie is written on any failure.
    /// </returns>
    public async Task<Result<AuthenticatedUser>> SignInAsync(
        HttpContext httpContext,
        string email,
        string password,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        Result<AuthenticatedUser> credentials = await _identityService.ValidateCredentialsAsync(email, password, ct);
        if (credentials.IsFailure)
        {
            // Returned as it arrived. Reclassifying it here would give the Blazor door a way to
            // tell a locked-out account from a wrong password that the REST door does not have.
            return credentials;
        }

        // The one extra read a cookie sign-in costs: the claims factory needs the entity, and
        // AuthenticatedUser deliberately carries no Identity type across the Application boundary.
        EnvanexUser? user = await _userManager.FindByIdAsync(credentials.Value.Id.ToString());
        if (user is null)
        {
            // Deleted between the credential check and this read. The answer it would have had a
            // moment earlier, rather than a new outcome of its own.
            return Result.Failure<AuthenticatedUser>(AuthErrors.InvalidCredentials);
        }

        // The factory writes the role claims under ClaimTypes.Role and the security-stamp claim
        // that RevalidatingIdentityAuthenticationStateProvider compares against.
        var principal = await _claimsPrincipalFactory.CreateAsync(user);

        await httpContext.SignInAsync(EnvanexAuthenticationSchemes.Cookie, principal);

        return credentials;
    }

    /// <summary>
    /// Clears the auth cookie.
    /// </summary>
    public Task SignOutAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.SignOutAsync(EnvanexAuthenticationSchemes.Cookie);
    }
}
