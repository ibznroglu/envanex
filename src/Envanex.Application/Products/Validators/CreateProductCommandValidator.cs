using Envanex.Application.Products.Commands;
using Envanex.Domain.Aggregates.Products;
using FluentValidation;

namespace Envanex.Application.Products.Validators;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithErrorCode("Product.CodeRequired");

        RuleFor(x => x.Code)
            .MaximumLength(Product.CodeMaxLength)
            .WithErrorCode("Product.CodeTooLong");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("Product.NameRequired");

        RuleFor(x => x.Name)
            .MaximumLength(Product.NameMaxLength)
            .WithErrorCode("Product.NameTooLong");

        RuleFor(x => x.UnitOfMeasureId)
            .NotEqual(Guid.Empty)
            .WithErrorCode("Product.UnitOfMeasureRequired");

        RuleFor(x => x.ListPriceAmount)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode("Product.ListPriceAmountNegative");

        RuleFor(x => x.ListPriceCurrency)
            .NotEmpty()
            .WithErrorCode("Product.ListPriceCurrencyRequired");

        RuleFor(x => x.ListPriceCurrency)
            .Length(3)
            .WithErrorCode("Product.ListPriceCurrencyRequired");

        RuleFor(x => x.ReorderPoint)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode("Product.ReorderPointNegative");
    }
}
