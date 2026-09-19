using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;

namespace Envanex.Application.Tests.Fakes;

/// <summary>
/// The real service answers a wrong password, an unknown email address and a locked-out account
/// with the same <see cref="AuthErrors.InvalidCredentials"/> instance, so this fake offers no way
/// to express three different failures either.
/// </summary>
public sealed class FakeIdentityService : IIdentityService
{
    private Result<AuthenticatedUser> _result =
        Result.Failure<AuthenticatedUser>(AuthErrors.InvalidCredentials);

    public int ValidateCredentialsCallCount { get; private set; }

    public string? LastEmail { get; private set; }

    public string? LastPassword { get; private set; }

    public void SucceedWith(AuthenticatedUser user)
    {
        _result = Result.Success(user);
    }

    public void FailWithInvalidCredentials()
    {
        _result = Result.Failure<AuthenticatedUser>(AuthErrors.InvalidCredentials);
    }

    public Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        ValidateCredentialsCallCount++;
        LastEmail = email;
        LastPassword = password;

        return Task.FromResult(_result);
    }
}
