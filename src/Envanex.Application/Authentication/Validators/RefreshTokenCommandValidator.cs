using Envanex.Application.Authentication.Commands;
using FluentValidation;

namespace Envanex.Application.Authentication.Validators;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithErrorCode("Auth.RefreshTokenRequired");
    }
}
