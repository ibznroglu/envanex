using Envanex.Domain.Aggregates.UnitOfMeasures;
using Shouldly;

namespace Envanex.Domain.Tests.Aggregates.UnitOfMeasures;

public class UnitOfMeasureTests
{
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
}
