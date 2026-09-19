namespace Envanex.Application.Authentication.Models;

/// <summary>
/// The subset of an authenticated identity the Application layer is allowed to know about.
/// No ASP.NET Core Identity type crosses this boundary.
/// </summary>
public sealed record AuthenticatedUser(Guid Id, string Email, string UserName);
