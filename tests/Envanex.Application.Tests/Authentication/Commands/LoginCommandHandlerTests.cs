using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Commands;
using Envanex.Application.Authentication.DTOs;
using Envanex.Application.Authentication.Models;
using Envanex.Application.Tests.Fakes;
using Envanex.Domain.Common;
using Shouldly;

namespace Envanex.Application.Tests.Authentication.Commands;

public class LoginCommandHandlerTests
{
    private readonly FakeIdentityService _identityService = new();
    private readonly FakeRefreshTokenService _refreshTokenService = new();
    private readonly FakeAccessTokenIssuer _accessTokenIssuer = new();
    private readonly LoginCommandHandler _handler;

    private static readonly AuthenticatedUser User =
        new(Guid.CreateVersion7(), "user@envanex.local", "user@envanex.local");

    private static readonly DateTimeOffset AccessTokenExpiresAt =
        new(2026, 9, 19, 12, 15, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset RefreshTokenExpiresAt =
        new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly LoginCommand ValidCommand = new("user@envanex.local", "Correct-Horse-1");

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(_identityService, _refreshTokenService, _accessTokenIssuer);
    }

    private void ArrangeSuccessfulLogin()
    {
        _identityService.SucceedWith(User);
        _refreshTokenService.IssueSucceedsWith(new IssuedRefreshToken("refresh-token", RefreshTokenExpiresAt));
        _accessTokenIssuer.IssueReturns(new IssuedAccessToken("access-token", AccessTokenExpiresAt));
    }

    [Fact]
    public async Task HandleAsync_ValidCredentials_ShouldReturnAnAccessTokenAndARefreshToken()
    {
        ArrangeSuccessfulLogin();

        Result<AuthenticationResponse> result = await _handler.HandleAsync(ValidCommand);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("access-token");
        result.Value.RefreshToken.ShouldBe("refresh-token");
        _refreshTokenService.LastIssuedUserId.ShouldBe(User.Id);
        _accessTokenIssuer.LastUser.ShouldBe(User);
    }

    [Fact]
    public async Task HandleAsync_ValidCredentials_ShouldReturnBearerAsTheTokenType()
    {
        ArrangeSuccessfulLogin();

        Result<AuthenticationResponse> result = await _handler.HandleAsync(ValidCommand);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TokenType.ShouldBe("Bearer");
    }

    [Fact]
    public async Task HandleAsync_ValidCredentials_ShouldReturnBothExpiryTimestamps()
    {
        ArrangeSuccessfulLogin();

        Result<AuthenticationResponse> result = await _handler.HandleAsync(ValidCommand);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessTokenExpiresAt.ShouldBe(AccessTokenExpiresAt);
        result.Value.RefreshTokenExpiresAt.ShouldBe(RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task HandleAsync_InvalidCredentials_ShouldReturnAuthInvalidCredentials()
    {
        _identityService.FailWithInvalidCredentials();

        Result<AuthenticationResponse> result = await _handler.HandleAsync(ValidCommand);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task HandleAsync_InvalidCredentials_ShouldNotIssueARefreshToken()
    {
        _identityService.FailWithInvalidCredentials();

        await _handler.HandleAsync(ValidCommand);

        _refreshTokenService.IssueCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_InvalidCredentials_ShouldNotIssueAnAccessToken()
    {
        _identityService.FailWithInvalidCredentials();

        await _handler.HandleAsync(ValidCommand);

        _accessTokenIssuer.IssueCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_LockedOutUser_ShouldReturnAuthInvalidCredentials()
    {
        // A locked-out account is indistinguishable from a wrong password and an unknown email
        // address: the identity service answers all three with the same error instance, so the
        // fake has no separate lockout failure to return.
        _identityService.FailWithInvalidCredentials();

        Result<AuthenticationResponse> result = await _handler.HandleAsync(ValidCommand);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.InvalidCredentials);
        result.Error.Code.ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task HandleAsync_NullCommand_ShouldThrowArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => _handler.HandleAsync(null!));
    }
}
