using Envanex.Domain.ValueObjects;
using Shouldly;

namespace Envanex.Domain.Tests.ValueObjects;

public class QuantityTests
{
    [Fact]
    public void Of_WithPositiveValue_ShouldSucceed()
    {
        var result = Quantity.Of(10.5m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(10.5m);
    }

    [Fact]
    public void Of_WithZero_ShouldSucceed()
    {
        var result = Quantity.Of(0m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(0m);
    }

    [Fact]
    public void Of_WithNegative_ShouldFail()
    {
        var result = Quantity.Of(-1m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(QuantityErrors.Negative);
    }

    [Fact]
    public void Of_ShouldRoundToSixDecimalPlaces()
    {
        var result = Quantity.Of(1.1234567m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(1.123457m); // banker's rounding: 7 rounds 6 up
    }

    [Fact]
    public void Add_ShouldSumValues()
    {
        Quantity a = Quantity.Of(10m).Value;
        Quantity b = Quantity.Of(5m).Value;

        Quantity result = a.Add(b);

        result.Value.ShouldBe(15m);
    }

    [Fact]
    public void Subtract_WithSufficientAmount_ShouldSucceed()
    {
        Quantity a = Quantity.Of(10m).Value;
        Quantity b = Quantity.Of(3m).Value;

        var result = a.Subtract(b);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(7m);
    }

    [Fact]
    public void Subtract_WouldGoNegative_ShouldFail()
    {
        Quantity a = Quantity.Of(3m).Value;
        Quantity b = Quantity.Of(5m).Value;

        var result = a.Subtract(b);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(QuantityErrors.NegativeResult);
    }

    [Fact]
    public void Multiply_WithPositiveFactor_ShouldSucceed()
    {
        Quantity a = Quantity.Of(10m).Value;

        var result = a.Multiply(2.5m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(25m);
    }

    [Fact]
    public void Multiply_WithNegativeFactor_ShouldFail()
    {
        Quantity a = Quantity.Of(10m).Value;

        var result = a.Multiply(-1m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(QuantityErrors.NegativeResult);
    }

    [Fact]
    public void Multiply_ShouldRoundToSixDecimalPlaces()
    {
        Quantity a = Quantity.Of(1.1234567m).Value; // rounded to 1.123457

        var result = a.Multiply(3m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(Math.Round(1.123457m * 3m, 6, MidpointRounding.ToEven));
    }

    [Fact]
    public void Zero_ShouldHaveZeroValue()
    {
        Quantity.Zero.Value.ShouldBe(0m);
    }

    [Fact]
    public void Quantities_WithSameValue_ShouldBeEqual()
    {
        Quantity a = Quantity.Of(10.5m).Value;
        Quantity b = Quantity.Of(10.5m).Value;

        a.ShouldBe(b);
    }
}
