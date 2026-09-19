using Envanex.Application.Authentication.Commands;
using Envanex.Application.Authentication.Validators;
using Shouldly;

namespace Envanex.Application.Tests.Authentication.Validators;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    private static LoginCommand ValidCommand => new(
        Email: "user@envanex.local",
        Password: "Correct-Horse-1");

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EmptyEmail_ShouldFailWithAuthEmailRequired()
    {
        var command = ValidCommand with { Email = string.Empty };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Auth.EmailRequired");
    }

    [Fact]
    public void Validate_MalformedEmail_ShouldFailWithAuthEmailInvalid()
    {
        var command = ValidCommand with { Email = "not-an-email" };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Auth.EmailInvalid");
    }

    [Fact]
    public void Validate_EmptyPassword_ShouldFailWithAuthPasswordRequired()
    {
        var command = ValidCommand with { Password = string.Empty };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Auth.PasswordRequired");
    }
}
