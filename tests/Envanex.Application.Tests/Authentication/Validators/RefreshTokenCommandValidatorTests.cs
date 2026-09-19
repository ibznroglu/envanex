using Envanex.Application.Authentication.Commands;
using Envanex.Application.Authentication.Validators;
using Shouldly;

namespace Envanex.Application.Tests.Authentication.Validators;

public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    private static RefreshTokenCommand ValidCommand => new(RefreshToken: "presented-refresh-token");

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EmptyRefreshToken_ShouldFailWithAuthRefreshTokenRequired()
    {
        var command = ValidCommand with { RefreshToken = string.Empty };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Auth.RefreshTokenRequired");
    }

    [Fact]
    public void Validate_WhitespaceRefreshToken_ShouldFailWithAuthRefreshTokenRequired()
    {
        var command = ValidCommand with { RefreshToken = "   " };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Auth.RefreshTokenRequired");
    }
}
