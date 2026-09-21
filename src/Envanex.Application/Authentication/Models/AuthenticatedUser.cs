namespace Envanex.Application.Authentication.Models;

/// <summary>
/// The subset of an authenticated identity the Application layer is allowed to know about.
/// No ASP.NET Core Identity type crosses this boundary.
/// </summary>
/// <remarks>
/// <c>Roles</c> is the role names the user holds, empty when it holds none. It is carried here
/// because every producer of an <see cref="AuthenticatedUser"/> feeds the access token issuer, and
/// a token issued without the user's roles authenticates a caller who then fails every policy.
/// </remarks>
public sealed record AuthenticatedUser(Guid Id, string Email, string UserName, IReadOnlyList<string> Roles);
