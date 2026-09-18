using Envanex.Application.Authentication.Models;

namespace Envanex.Application.Abstractions.Authentication;

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(AuthenticatedUser user);
}
