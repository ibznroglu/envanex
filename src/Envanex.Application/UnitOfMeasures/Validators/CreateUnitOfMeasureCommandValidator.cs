using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using FluentValidation;

namespace Envanex.Application.UnitOfMeasures.Validators;

public sealed class CreateUnitOfMeasureCommandValidator : AbstractValidator<CreateUnitOfMeasureCommand>
{
    public CreateUnitOfMeasureCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithErrorCode("UnitOfMeasure.CodeRequired");

        RuleFor(x => x.Code)
            .MaximumLength(UnitOfMeasure.CodeMaxLength)
            .WithErrorCode("UnitOfMeasure.CodeTooLong");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("UnitOfMeasure.NameRequired");

        RuleFor(x => x.Name)
            .MaximumLength(UnitOfMeasure.NameMaxLength)
            .WithErrorCode("UnitOfMeasure.NameTooLong");

        When(x => x.BaseUnitId is null, () =>
        {
            RuleFor(x => x.ConversionFactor)
                .Equal(1m)
                .WithErrorCode("UnitOfMeasure.BaseUnitFactorMustBeOne");
        });

        When(x => x.BaseUnitId is not null, () =>
        {
            RuleFor(x => x.ConversionFactor)
                .GreaterThan(0m)
                .WithErrorCode("UnitOfMeasure.ConversionFactorMustBePositive");
        });
    }
}
