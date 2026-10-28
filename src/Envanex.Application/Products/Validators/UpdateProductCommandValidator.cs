using Envanex.Application.Products.Commands;
using Envanex.Domain.Aggregates.Products;
using FluentValidation;

namespace Envanex.Application.Products.Validators;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithErrorCode("Product.IdRequired");

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

        RuleFor(x => x.ListPriceAmount)
            .LessThanOrEqualTo(Product.ListPriceAmountMaxValue)
            .WithErrorCode(ProductErrors.ListPriceAmountTooLarge.Code);

        RuleFor(x => x.ListPriceCurrency)
            .NotEmpty()
            .WithErrorCode("Product.ListPriceCurrencyRequired");

        RuleFor(x => x.ListPriceCurrency)
            .Length(3)
            .WithErrorCode("Product.ListPriceCurrencyRequired");

        RuleFor(x => x.ReorderPoint)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode("Product.ReorderPointNegative");

        RuleFor(x => x.ReorderPoint)
            .LessThanOrEqualTo(Product.ReorderPointMaxValue)
            .WithErrorCode(ProductErrors.ReorderPointTooLarge.Code);

        RuleFor(x => x.RowVersion)
            .NotNull()
            .WithErrorCode("Product.RowVersionRequired");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithErrorCode("Product.RowVersionRequired");
    }
}
