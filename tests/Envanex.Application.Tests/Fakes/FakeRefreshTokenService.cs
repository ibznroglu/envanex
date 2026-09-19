using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;

namespace Envanex.Application.Tests.Fakes;

public sealed class FakeRefreshTokenService : IRefreshTokenService
{
    private Result<IssuedRefreshToken> _issueResult =
        Result.Failure<IssuedRefreshToken>(AuthErrors.InvalidRefreshToken);

    private Result<RotatedRefreshToken> _rotateResult =
        Result.Failure<RotatedRefreshToken>(AuthErrors.InvalidRefreshToken);

    private Result _revokeResult = Result.Success();

    public int IssueCallCount { get; private set; }

    public int RotateCallCount { get; private set; }

    public int RevokeFamilyCallCount { get; private set; }

    public Guid? LastIssuedUserId { get; private set; }

    public string? LastRotatedToken { get; private set; }

    public string? LastRevokedToken { get; private set; }

    public void IssueSucceedsWith(IssuedRefreshToken token)
    {
        _issueResult = Result.Success(token);
    }

    public void IssueFailsWith(Error error)
    {
        _issueResult = Result.Failure<IssuedRefreshToken>(error);
    }

    public void RotateSucceedsWith(RotatedRefreshToken token)
    {
        _rotateResult = Result.Success(token);
    }

    public void RotateFailsWith(Error error)
    {
        _rotateResult = Result.Failure<RotatedRefreshToken>(error);
    }

    public void RevokeFamilySucceeds()
    {
        _revokeResult = Result.Success();
    }

    /// <summary>
    /// Reproduces the exhausted-retry branch of the real service: the revocation could not be
    /// guaranteed, so it reports a failure rather than a success.
    /// </summary>
    public void RevokeFamilyFailsWith(Error error)
    {
        _revokeResult = Result.Failure(error);
    }

    public Task<Result<IssuedRefreshToken>> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        IssueCallCount++;
        LastIssuedUserId = userId;

        return Task.FromResult(_issueResult);
    }

    public Task<Result<RotatedRefreshToken>> RotateAsync(string presentedToken, CancellationToken ct = default)
    {
        RotateCallCount++;
        LastRotatedToken = presentedToken;

        return Task.FromResult(_rotateResult);
    }

    public Task<Result> RevokeFamilyAsync(string presentedToken, CancellationToken ct = default)
    {
        RevokeFamilyCallCount++;
        LastRevokedToken = presentedToken;

        return Task.FromResult(_revokeResult);
    }
}
