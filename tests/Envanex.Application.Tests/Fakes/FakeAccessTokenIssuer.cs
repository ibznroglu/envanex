using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication.Models;

namespace Envanex.Application.Tests.Fakes;

public sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    private IssuedAccessToken _token = new("access-token", DateTimeOffset.UnixEpoch);

    public int IssueCallCount { get; private set; }

    public AuthenticatedUser? LastUser { get; private set; }

    public void IssueReturns(IssuedAccessToken token)
    {
        _token = token;
    }

    public IssuedAccessToken Issue(AuthenticatedUser user)
    {
        IssueCallCount++;
        LastUser = user;

        return _token;
    }
}
