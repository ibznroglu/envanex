using Envanex.Application.Authentication.Commands;
using FluentValidation;

namespace Envanex.Application.Authentication.Validators;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode("Auth.EmailRequired");

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithErrorCode("Auth.EmailInvalid");

        // No maximum-length rule: a length limit here would reject a password the user chose
        // without telling the login surface anything it is allowed to act on.
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithErrorCode("Auth.PasswordRequired");
    }
}
