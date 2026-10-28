using Envanex.Application.Products.Commands;
using Envanex.Application.Products.Validators;
using Envanex.Domain.Aggregates.Products;
using Shouldly;

namespace Envanex.Application.Tests.Products.Validators;

public class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _validator = new();

    private static CreateProductCommand ValidCommand => new(
        Code: "SKU001",
        Name: "Widget",
        UnitOfMeasureId: Guid.CreateVersion7(),
        ListPriceAmount: 100.50m,
        ListPriceCurrency: "TRY",
        ReorderPoint: 10m);

    public static TheoryData<decimal> ListPriceAmountsAtOrJustBelowMaximum => new()
    {
        99999999999999.9998m,
        99999999999999.9999m,
    };

    public static TheoryData<decimal> ListPriceAmountsAboveMaximum => new()
    {
        100000000000000.0000m,
        99999999999999.99994m,
        99999999999999.99995m,
    };

    public static TheoryData<decimal> ReorderPointsAtOrJustBelowMaximum => new()
    {
        999999999999.999998m,
        999999999999.999999m,
    };

    public static TheoryData<decimal> ReorderPointsAboveMaximum => new()
    {
        1000000000000.000000m,
        999999999999.9999994m,
        999999999999.9999995m,
    };

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyCode_ShouldFail()
    {
        var command = ValidCommand with { Code = "" };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Product.CodeRequired");
    }

    [Fact]
    public void Validate_WithCodeExceedingMaxLength_ShouldFail()
    {
        string longCode = new('A', Product.CodeMaxLength + 1);
        var command = ValidCommand with { Code = longCode };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Product.CodeTooLong");
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldFail()
    {
        var command = ValidCommand with { Name = "" };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Product.NameRequired");
    }

    [Fact]
    public void Validate_WithNameExceedingMaxLength_ShouldFail()
    {
        string longName = new('A', Product.NameMaxLength + 1);
        var command = ValidCommand with { Name = longName };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Product.NameTooLong");
    }

    [Fact]
    public void Validate_WithEmptyGuidUnitOfMeasureId_ShouldFail()
    {
        var command = ValidCommand with { UnitOfMeasureId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Product.UnitOfMeasureRequired");
    }

    [Fact]
    public void Validate_WithNegativeListPriceAmount_ShouldFail()
    {
        var command = ValidCommand with { ListPriceAmount = -1m };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Product.ListPriceAmountNegative");
    }

    [Fact]
    public void Validate_WithEmptyListPriceCurrency_ShouldFail()
    {
        var command = ValidCommand with { ListPriceCurrency = "" };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Product.ListPriceCurrencyRequired");
    }

    [Fact]
    public void Validate_WithNegativeReorderPoint_ShouldFail()
    {
        var command = ValidCommand with { ReorderPoint = -5m };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Product.ReorderPointNegative");
    }

    [Fact]
    public void Validate_WithCodeAtExactMaxLength_ShouldPass()
    {
        string code = new('A', Product.CodeMaxLength);
        var command = ValidCommand with { Code = code };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNameAtExactMaxLength_ShouldPass()
    {
        string name = new('A', Product.NameMaxLength);
        var command = ValidCommand with { Name = name };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(ListPriceAmountsAtOrJustBelowMaximum))]
    public void Validate_WithListPriceAmountAtOrJustBelowMaximum_ShouldPass(decimal amount)
    {
        var command = ValidCommand with { ListPriceAmount = amount };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(ListPriceAmountsAboveMaximum))]
    public void Validate_WithListPriceAmountAboveMaximum_ShouldFailWithListPriceAmountTooLarge(decimal amount)
    {
        var command = ValidCommand with { ListPriceAmount = amount };

        var result = _validator.Validate(command);

        result.Errors
            .Where(e => e.PropertyName == "ListPriceAmount")
            .Select(e => e.ErrorCode)
            .ToArray()
            .ShouldBe(["Product.ListPriceAmountTooLarge"]);
    }

    [Fact]
    public void Validate_WithNegativeListPriceAmountThatRoundsToZero_ShouldFailWithListPriceAmountNegative()
    {
        var command = ValidCommand with { ListPriceAmount = -0.00004m };

        var result = _validator.Validate(command);

        result.Errors
            .Where(e => e.PropertyName == "ListPriceAmount")
            .Select(e => e.ErrorCode)
            .ToArray()
            .ShouldBe(["Product.ListPriceAmountNegative"]);
    }

    [Theory]
    [MemberData(nameof(ReorderPointsAtOrJustBelowMaximum))]
    public void Validate_WithReorderPointAtOrJustBelowMaximum_ShouldPass(decimal value)
    {
        var command = ValidCommand with { ReorderPoint = value };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(ReorderPointsAboveMaximum))]
    public void Validate_WithReorderPointAboveMaximum_ShouldFailWithReorderPointTooLarge(decimal value)
    {
        var command = ValidCommand with { ReorderPoint = value };

        var result = _validator.Validate(command);

        result.Errors
            .Where(e => e.PropertyName == "ReorderPoint")
            .Select(e => e.ErrorCode)
            .ToArray()
            .ShouldBe(["Product.ReorderPointTooLarge"]);
    }
}
