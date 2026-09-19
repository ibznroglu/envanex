using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Commands;
using Envanex.Application.Tests.Fakes;
using Envanex.Domain.Common;
using Shouldly;

namespace Envanex.Application.Tests.Authentication.Commands;

public class LogoutCommandHandlerTests
{
    private readonly FakeRefreshTokenService _refreshTokenService = new();
    private readonly LogoutCommandHandler _handler;

    private static readonly LogoutCommand ValidCommand = new("presented-refresh-token");

    public LogoutCommandHandlerTests()
    {
        _handler = new LogoutCommandHandler(_refreshTokenService);
    }

    [Fact]
    public async Task HandleAsync_ValidRefreshToken_ShouldCallRevokeFamilyOnce()
    {
        _refreshTokenService.RevokeFamilySucceeds();

        await _handler.HandleAsync(ValidCommand);

        _refreshTokenService.RevokeFamilyCallCount.ShouldBe(1);
        _refreshTokenService.LastRevokedToken.ShouldBe(ValidCommand.RefreshToken);
    }

    [Fact]
    public async Task HandleAsync_ValidRefreshToken_ShouldReturnSuccess()
    {
        _refreshTokenService.RevokeFamilySucceeds();

        Result<bool> result = await _handler.HandleAsync(ValidCommand);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_UnknownRefreshToken_ShouldReturnSuccess()
    {
        // The revocation is deliberately idempotent for an unknown token, so logout cannot be
        // used as an oracle for whether a token exists.
        _refreshTokenService.RevokeFamilySucceeds();

        Result<bool> result = await _handler.HandleAsync(new LogoutCommand("unknown-refresh-token"));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_RevocationFailure_ShouldPropagateTheFailure()
    {
        // The exhausted-retry branch of the real service: a logout that cannot guarantee the
        // family was revoked must not report success.
        _refreshTokenService.RevokeFamilyFailsWith(AuthErrors.InvalidRefreshToken);

        Result<bool> result = await _handler.HandleAsync(ValidCommand);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task HandleAsync_NullCommand_ShouldThrowArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => _handler.HandleAsync(null!));
    }
}
