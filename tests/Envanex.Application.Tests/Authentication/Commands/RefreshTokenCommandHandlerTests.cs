using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Commands;
using Envanex.Application.Authentication.DTOs;
using Envanex.Application.Authentication.Models;
using Envanex.Application.Tests.Fakes;
using Envanex.Domain.Common;
using Shouldly;

namespace Envanex.Application.Tests.Authentication.Commands;

public class RefreshTokenCommandHandlerTests
{
    private readonly FakeRefreshTokenService _refreshTokenService = new();
    private readonly FakeAccessTokenIssuer _accessTokenIssuer = new();
    private readonly RefreshTokenCommandHandler _handler;

    private static readonly AuthenticatedUser User =
        new(Guid.CreateVersion7(), "user@envanex.local", "user@envanex.local");

    private static readonly DateTimeOffset AccessTokenExpiresAt =
        new(2026, 9, 19, 12, 15, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset RefreshTokenExpiresAt =
        new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly RefreshTokenCommand ValidCommand = new("presented-refresh-token");

    public RefreshTokenCommandHandlerTests()
    {
        _handler = new RefreshTokenCommandHandler(_refreshTokenService, _accessTokenIssuer);
    }

    private void ArrangeSuccessfulRotation()
    {
        _refreshTokenService.RotateSucceedsWith(
            new RotatedRefreshToken(User, "rotated-refresh-token", RefreshTokenExpiresAt));
        _accessTokenIssuer.IssueReturns(new IssuedAccessToken("access-token", AccessTokenExpiresAt));
    }

    [Fact]
    public async Task HandleAsync_ValidRefreshToken_ShouldReturnANewRefreshToken()
    {
        ArrangeSuccessfulRotation();

        Result<AuthenticationResponse> result = await _handler.HandleAsync(ValidCommand);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RefreshToken.ShouldBe("rotated-refresh-token");
        result.Value.RefreshToken.ShouldNotBe(ValidCommand.RefreshToken);
        result.Value.RefreshTokenExpiresAt.ShouldBe(RefreshTokenExpiresAt);
        _refreshTokenService.LastRotatedToken.ShouldBe(ValidCommand.RefreshToken);
    }

    [Fact]
    public async Task HandleAsync_ValidRefreshToken_ShouldReturnANewAccessTokenForTheRotatedUser()
    {
        ArrangeSuccessfulRotation();

        Result<AuthenticationResponse> result = await _handler.HandleAsync(ValidCommand);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("access-token");
        result.Value.AccessTokenExpiresAt.ShouldBe(AccessTokenExpiresAt);
        result.Value.TokenType.ShouldBe("Bearer");
        _accessTokenIssuer.LastUser.ShouldBe(User);
    }

    [Fact]
    public async Task HandleAsync_ReusedRefreshToken_ShouldReturnAuthRefreshTokenReused()
    {
        _refreshTokenService.RotateFailsWith(AuthErrors.RefreshTokenReused);

        Result<AuthenticationResponse> result = await _handler.HandleAsync(ValidCommand);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.RefreshTokenReused);
    }

    [Fact]
    public async Task HandleAsync_ExpiredRefreshToken_ShouldReturnAuthRefreshTokenExpired()
    {
        _refreshTokenService.RotateFailsWith(AuthErrors.RefreshTokenExpired);

        Result<AuthenticationResponse> result = await _handler.HandleAsync(ValidCommand);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.RefreshTokenExpired);
    }

    [Fact]
    public async Task HandleAsync_UnknownRefreshToken_ShouldReturnAuthInvalidRefreshToken()
    {
        _refreshTokenService.RotateFailsWith(AuthErrors.InvalidRefreshToken);

        Result<AuthenticationResponse> result = await _handler.HandleAsync(ValidCommand);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task HandleAsync_RotationFailure_ShouldNotIssueAnAccessToken()
    {
        _refreshTokenService.RotateFailsWith(AuthErrors.RefreshTokenReused);

        await _handler.HandleAsync(ValidCommand);

        _accessTokenIssuer.IssueCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_NullCommand_ShouldThrowArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => _handler.HandleAsync(null!));
    }
}
