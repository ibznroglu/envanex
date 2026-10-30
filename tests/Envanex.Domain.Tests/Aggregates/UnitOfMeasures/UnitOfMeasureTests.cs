using Envanex.Domain.Aggregates.UnitOfMeasures;
using Shouldly;

namespace Envanex.Domain.Tests.Aggregates.UnitOfMeasures;

public class UnitOfMeasureTests
{
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
    public void Create_BaseUnit_WithValidInputs_ShouldSucceed()
    {
        var result = UnitOfMeasure.Create("ADET", "Adet", null, 1m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("ADET");
        result.Value.Name.ShouldBe("Adet");
        result.Value.BaseUnitId.ShouldBeNull();
        result.Value.ConversionFactor.ShouldBe(1m);
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_ShouldNormalizeCode_TrimAndUpperInvariant()
    {
        var result = UnitOfMeasure.Create(" adet ", "Adet", null, 1m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("ADET");
    }

    [Fact]
    public void Create_ShouldTrimName()
    {
        var result = UnitOfMeasure.Create("KG", " Kilogram ", null, 1m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Kilogram");
    }

    [Fact]
    public void Create_WithEmptyCode_ShouldFail()
    {
        var result = UnitOfMeasure.Create("", "Adet", null, 1m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.CodeRequired);
    }

    [Fact]
    public void Create_WithWhitespaceCode_ShouldFail()
    {
        var result = UnitOfMeasure.Create("   ", "Adet", null, 1m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.CodeRequired);
    }

    [Fact]
    public void Create_WithEmptyName_ShouldFail()
    {
        var result = UnitOfMeasure.Create("ADET", "", null, 1m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.NameRequired);
    }

    [Fact]
    public void Create_BaseUnit_WithNonOneConversionFactor_ShouldFail()
    {
        var result = UnitOfMeasure.Create("ADET", "Adet", null, 2m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.BaseUnitFactorMustBeOne);
    }

    [Fact]
    public void Create_DerivedUnit_WithValidInputs_ShouldSucceed()
    {
        var baseUnitId = Guid.CreateVersion7();

        var result = UnitOfMeasure.Create("DUZINE", "Duzine", baseUnitId, 12m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("DUZINE");
        result.Value.Name.ShouldBe("Duzine");
        result.Value.BaseUnitId.ShouldBe(baseUnitId);
        result.Value.ConversionFactor.ShouldBe(12m);
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_DerivedUnit_WithZeroFactor_ShouldFail()
    {
        var baseUnitId = Guid.CreateVersion7();

        var result = UnitOfMeasure.Create("DUZINE", "Duzine", baseUnitId, 0m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.ConversionFactorMustBePositive);
    }

    [Fact]
    public void Create_DerivedUnit_WithNegativeFactor_ShouldFail()
    {
        var baseUnitId = Guid.CreateVersion7();

        var result = UnitOfMeasure.Create("DUZINE", "Duzine", baseUnitId, -1m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.ConversionFactorMustBePositive);
    }

    [Fact]
    public void Create_DerivedUnit_WithEmptyGuidBaseUnitId_ShouldFail()
    {
        var result = UnitOfMeasure.Create("DUZINE", "Duzine", Guid.Empty, 12m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.InvalidBaseUnitId);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var unit = UnitOfMeasure.Create("ADET", "Adet", null, 1m).Value;

        unit.Deactivate();

        unit.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Activate_ShouldSetIsActiveTrue()
    {
        var unit = UnitOfMeasure.Create("ADET", "Adet", null, 1m).Value;
        unit.Deactivate();

        unit.Activate();

        unit.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_ShouldGenerateVersion7Id()
    {
        var result = UnitOfMeasure.Create("ADET", "Adet", null, 1m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithCodeExceedingMaxLength_ShouldFail()
    {
        string longCode = new('A', UnitOfMeasure.CodeMaxLength + 1);

        var result = UnitOfMeasure.Create(longCode, "Adet", null, 1m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.CodeTooLong);
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ShouldFail()
    {
        string longName = new('A', UnitOfMeasure.NameMaxLength + 1);

        var result = UnitOfMeasure.Create("ADET", longName, null, 1m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.NameTooLong);
    }

    [Fact]
    public void Create_WithCodeAtExactMaxLength_ShouldSucceed()
    {
        string code = new('A', UnitOfMeasure.CodeMaxLength);

        var result = UnitOfMeasure.Create(code, "Adet", null, 1m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe(code);
    }

    [Fact]
    public void Create_WithNameAtExactMaxLength_ShouldSucceed()
    {
        string name = new('A', UnitOfMeasure.NameMaxLength);

        var result = UnitOfMeasure.Create("ADET", name, null, 1m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe(name);
    }

    [Theory]
    [MemberData(nameof(RepresentableFactorsWithinMaximum))]
    public void Create_DerivedUnit_WithRepresentableFactorWithinMaximum_ShouldSucceed(decimal factor)
    {
        var baseUnitId = Guid.CreateVersion7();

        var result = UnitOfMeasure.Create("GR", "Gram", baseUnitId, factor);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ConversionFactor.ShouldBe(factor);
    }

    [Theory]
    [MemberData(nameof(FactorsBeyondSixDecimalPlaces))]
    public void Create_DerivedUnit_WithFactorBeyondSixDecimalPlaces_ShouldFailWithTooPrecise(decimal factor)
    {
        var baseUnitId = Guid.CreateVersion7();

        var result = UnitOfMeasure.Create("GR", "Gram", baseUnitId, factor);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.ConversionFactorTooPrecise);
    }

    [Fact]
    public void Create_DerivedUnit_WithFactorAboveMaximum_ShouldFailWithTooLarge()
    {
        var baseUnitId = Guid.CreateVersion7();

        var result = UnitOfMeasure.Create("GR", "Gram", baseUnitId, 1000000000000.000000m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.ConversionFactorTooLarge);
    }

    [Theory]
    [MemberData(nameof(FactorsTooPreciseAndAboveMaximum))]
    public void Create_DerivedUnit_WithFactorTooPreciseAndAboveMaximum_ShouldFailWithTooPrecise(decimal factor)
    {
        var baseUnitId = Guid.CreateVersion7();

        var result = UnitOfMeasure.Create("GR", "Gram", baseUnitId, factor);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.ConversionFactorTooPrecise);
    }

    [Fact]
    public void Create_DerivedUnit_WithNegativeFactorBeyondSixDecimalPlaces_ShouldFailWithMustBePositive()
    {
        var baseUnitId = Guid.CreateVersion7();

        var result = UnitOfMeasure.Create("GR", "Gram", baseUnitId, -0.0000001m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.ConversionFactorMustBePositive);
    }

    [Fact]
    public void Create_BaseUnit_WithOneAndTrailingZeros_ShouldSucceed()
    {
        var result = UnitOfMeasure.Create("KG", "Kilogram", null, 1.000000000m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ConversionFactor.ShouldBe(1m);
    }

    [Fact]
    public void Create_BaseUnit_WithFactorBeyondSixDecimalPlaces_ShouldFailWithBaseUnitFactorMustBeOne()
    {
        var result = UnitOfMeasure.Create("KG", "Kilogram", null, 1.0000001m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.BaseUnitFactorMustBeOne);
    }
}
