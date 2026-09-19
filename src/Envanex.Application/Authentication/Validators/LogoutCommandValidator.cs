using Envanex.Application.Authentication.Commands;
using FluentValidation;

namespace Envanex.Application.Authentication.Validators;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithErrorCode("Auth.RefreshTokenRequired");
    }
}
