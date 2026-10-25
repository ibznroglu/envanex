using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.Application.UnitOfMeasures.Validators;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Shouldly;

namespace Envanex.Application.Tests.UnitOfMeasures.Validators;

public class CreateUnitOfMeasureCommandValidatorTests
{
    private readonly CreateUnitOfMeasureCommandValidator _validator = new();

    private static CreateUnitOfMeasureCommand ValidBaseUnitCommand => new(
        Code: "KG",
        Name: "Kilogram",
        BaseUnitId: null,
        ConversionFactor: 1m);

    private static CreateUnitOfMeasureCommand ValidDerivedUnitCommand => new(
        Code: "GR",
        Name: "Gram",
        BaseUnitId: Guid.CreateVersion7(),
        ConversionFactor: 0.001m);

    public static TheoryData<decimal> RepresentableFactorsWithinMaximum => new()
    {
        0.000001m,
        0.0000010m,
        1.500000000m,
        999999999999.999998m,
        999999999999.999999m,
    };

    public static TheoryData<decimal> FactorsBeyondSixDecimalPlaces => new()
    {
        0.0000001m,
        0.0000005m,
        0.1234567m,
    };

    public static TheoryData<decimal> FactorsTooPreciseAndAboveMaximum => new()
    {
        1000000000000.0000001m,
        999999999999.9999994m,
    };

    [Fact]
    public void Validate_WithValidBaseUnitCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidBaseUnitCommand);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithValidDerivedUnitCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidDerivedUnitCommand);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyCode_ShouldFail()
    {
        var command = ValidBaseUnitCommand with { Code = "" };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "UnitOfMeasure.CodeRequired");
    }

    [Fact]
    public void Validate_WithCodeExceedingMaxLength_ShouldFail()
    {
        string longCode = new('A', UnitOfMeasure.CodeMaxLength + 1);
        var command = ValidBaseUnitCommand with { Code = longCode };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "UnitOfMeasure.CodeTooLong");
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldFail()
    {
        var command = ValidBaseUnitCommand with { Name = "" };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "UnitOfMeasure.NameRequired");
    }

    [Fact]
    public void Validate_WithNameExceedingMaxLength_ShouldFail()
    {
        string longName = new('A', UnitOfMeasure.NameMaxLength + 1);
        var command = ValidBaseUnitCommand with { Name = longName };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "UnitOfMeasure.NameTooLong");
    }

    [Fact]
    public void Validate_BaseUnitWithConversionFactorNotOne_ShouldFail()
    {
        var command = ValidBaseUnitCommand with { ConversionFactor = 2m };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "UnitOfMeasure.BaseUnitFactorMustBeOne");
    }

    [Fact]
    public void Validate_DerivedUnitWithZeroConversionFactor_ShouldFail()
    {
        var command = ValidDerivedUnitCommand with { ConversionFactor = 0m };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "UnitOfMeasure.ConversionFactorMustBePositive");
    }

    [Fact]
    public void Validate_WithCodeAtExactMaxLength_ShouldPass()
    {
        string code = new('A', UnitOfMeasure.CodeMaxLength);
        var command = ValidBaseUnitCommand with { Code = code };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNameAtExactMaxLength_ShouldPass()
    {
        string name = new('A', UnitOfMeasure.NameMaxLength);
        var command = ValidBaseUnitCommand with { Name = name };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(RepresentableFactorsWithinMaximum))]
    public void Validate_DerivedUnitWithRepresentableFactorWithinMaximum_ShouldPass(decimal factor)
    {
        var command = ValidDerivedUnitCommand with { ConversionFactor = factor };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(FactorsBeyondSixDecimalPlaces))]
    public void Validate_DerivedUnitWithFactorBeyondSixDecimalPlaces_ShouldFailWithTooPrecise(decimal factor)
    {
        var command = ValidDerivedUnitCommand with { ConversionFactor = factor };

        var result = _validator.Validate(command);

        result.Errors
            .Where(e => e.PropertyName == "ConversionFactor")
            .Select(e => e.ErrorCode)
            .ToArray()
            .ShouldBe(["UnitOfMeasure.ConversionFactorTooPrecise"]);
    }

    [Fact]
    public void Validate_DerivedUnitWithFactorAboveMaximum_ShouldFailWithTooLarge()
    {
        var command = ValidDerivedUnitCommand with { ConversionFactor = 1000000000000.000000m };

        var result = _validator.Validate(command);

        result.Errors
            .Where(e => e.PropertyName == "ConversionFactor")
            .Select(e => e.ErrorCode)
            .ToArray()
            .ShouldBe(["UnitOfMeasure.ConversionFactorTooLarge"]);
    }

    [Theory]
    [MemberData(nameof(FactorsTooPreciseAndAboveMaximum))]
    public void Validate_DerivedUnitWithFactorTooPreciseAndAboveMaximum_ShouldReportBothFailures(decimal factor)
    {
        var command = ValidDerivedUnitCommand with { ConversionFactor = factor };

        var result = _validator.Validate(command);

        result.Errors
            .Where(e => e.PropertyName == "ConversionFactor")
            .Select(e => e.ErrorCode)
            .ToArray()
            .ShouldBe(["UnitOfMeasure.ConversionFactorTooPrecise", "UnitOfMeasure.ConversionFactorTooLarge"]);
    }

    [Fact]
    public void Validate_DerivedUnitWithNegativeFactorBeyondSixDecimalPlaces_ShouldReportPositivityAndPrecision()
    {
        var command = ValidDerivedUnitCommand with { ConversionFactor = -0.0000001m };

        var result = _validator.Validate(command);

        result.Errors
            .Where(e => e.PropertyName == "ConversionFactor")
            .Select(e => e.ErrorCode)
            .ToArray()
            .ShouldBe(["UnitOfMeasure.ConversionFactorMustBePositive", "UnitOfMeasure.ConversionFactorTooPrecise"]);
    }

    [Fact]
    public void Validate_BaseUnitWithOneAndTrailingZeros_ShouldPass()
    {
        var command = ValidBaseUnitCommand with { ConversionFactor = 1.000000000m };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }
}
