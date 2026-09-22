using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Authentication.Commands;
using Envanex.Application.Authentication.DTOs;
using Envanex.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Envanex.Web.Controllers;

/// <summary>
/// Login, refresh and logout. The one controller exempt from the fallback policy: a caller cannot
/// be required to hold an access token in order to obtain one.
/// </summary>
/// <remarks>
/// <c>Logout</c> is exempt too, and that is a decision rather than a side effect of the class-level
/// attribute (PR 6b Decision 14). Logout is authorized by possession of the refresh token it
/// revokes, and it has to work after the access token has expired — which is exactly when a client
/// is most likely to be signing out.
/// </remarks>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Name of the login-only rate-limit policy registered in <c>Program.cs</c>. The attribute and
    /// the registration read the same constant, because an attribute naming a policy the middleware
    /// never sees throws at endpoint build time and takes down every endpoint.
    /// </summary>
    public const string LoginRateLimitPolicy = "login";

    private readonly ICommandHandler<LoginCommand, AuthenticationResponse> _loginHandler;
    private readonly ICommandHandler<RefreshTokenCommand, AuthenticationResponse> _refreshHandler;
    private readonly ICommandHandler<LogoutCommand, bool> _logoutHandler;

    public AuthController(
        ICommandHandler<LoginCommand, AuthenticationResponse> loginHandler,
        ICommandHandler<RefreshTokenCommand, AuthenticationResponse> refreshHandler,
        ICommandHandler<LogoutCommand, bool> logoutHandler)
    {
        _loginHandler = loginHandler;
        _refreshHandler = refreshHandler;
        _logoutHandler = logoutHandler;
    }

    [HttpPost("login")]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        var result = await _loginHandler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command, CancellationToken ct)
    {
        var result = await _refreshHandler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand command, CancellationToken ct)
    {
        var result = await _logoutHandler.HandleAsync(command, ct);
        return result.ToNoContentActionResult();
    }
}
