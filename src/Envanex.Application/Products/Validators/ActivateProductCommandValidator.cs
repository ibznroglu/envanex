using Envanex.Application.Products.Commands;
using FluentValidation;

namespace Envanex.Application.Products.Validators;

public sealed class ActivateProductCommandValidator : AbstractValidator<ActivateProductCommand>
{
    public ActivateProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithErrorCode("Product.IdRequired");

        RuleFor(x => x.RowVersion)
            .NotNull()
            .WithErrorCode("Product.RowVersionRequired");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithErrorCode("Product.RowVersionRequired");
    }
}
