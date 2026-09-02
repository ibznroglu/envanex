using Envanex.Domain.ValueObjects;
using Shouldly;

namespace Envanex.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Of_WithValidAmountAndCurrency_ShouldSucceed()
    {
        var result = Money.Of(100.50m, Currency.TRY);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(100.50m);
        result.Value.Currency.ShouldBe(Currency.TRY);
    }

    [Fact]
    public void Of_ShouldRoundToFourDecimalPlaces()
    {
        var result = Money.Of(10.12345m, Currency.TRY);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(10.1234m); // banker's rounding: 5 rounds to even → 4
    }

    [Fact]
    public void Of_WithNegativeAmount_ShouldSucceed()
    {
        var result = Money.Of(-5m, Currency.TRY);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(-5m);
    }

    [Fact]
    public void Of_WithNullCurrency_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() => Money.Of(10m, null!));
    }

    [Fact]
    public void Add_SameCurrency_ShouldSucceed()
    {
        Money a = Money.Of(10m, Currency.TRY).Value;
        Money b = Money.Of(5m, Currency.TRY).Value;

        var result = a.Add(b);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(15m);
    }

    [Fact]
    public void Add_DifferentCurrency_ShouldFail()
    {
        Money a = Money.Of(10m, Currency.TRY).Value;
        Money b = Money.Of(5m, Currency.USD).Value;

        var result = a.Add(b);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MoneyErrors.CurrencyMismatch);
    }

    [Fact]
    public void Subtract_SameCurrency_ShouldSucceed()
    {
        Money a = Money.Of(10m, Currency.TRY).Value;
        Money b = Money.Of(3m, Currency.TRY).Value;

        var result = a.Subtract(b);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(7m);
    }

    [Fact]
    public void Subtract_SameCurrency_NegativeResult_ShouldSucceed()
    {
        Money a = Money.Of(3m, Currency.TRY).Value;
        Money b = Money.Of(10m, Currency.TRY).Value;

        var result = a.Subtract(b);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(-7m);
    }

    [Fact]
    public void Subtract_DifferentCurrency_ShouldFail()
    {
        Money a = Money.Of(10m, Currency.TRY).Value;
        Money b = Money.Of(3m, Currency.USD).Value;

        var result = a.Subtract(b);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MoneyErrors.CurrencyMismatch);
    }

    [Fact]
    public void Multiply_ShouldScale_AndRound()
    {
        Money a = Money.Of(10.1234m, Currency.TRY).Value;

        Money result = a.Multiply(3m);

        result.Amount.ShouldBe(30.3702m);
    }

    [Fact]
    public void Zero_ShouldHaveZeroAmount()
    {
        Money zero = Money.Zero(Currency.TRY);

        zero.Amount.ShouldBe(0m);
        zero.Currency.ShouldBe(Currency.TRY);
    }

    [Fact]
    public void Money_WithSameAmountAndCurrency_ShouldBeEqual()
    {
        Money a = Money.Of(10m, Currency.TRY).Value;
        Money b = Money.Of(10m, Currency.TRY).Value;

        a.ShouldBe(b);
    }

    [Fact]
    public void Money_WithDifferentAmount_ShouldNotBeEqual()
    {
        Money a = Money.Of(10m, Currency.TRY).Value;
        Money b = Money.Of(20m, Currency.TRY).Value;

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Money_WithDifferentCurrency_ShouldNotBeEqual()
    {
        Money a = Money.Of(10m, Currency.TRY).Value;
        Money b = Money.Of(10m, Currency.USD).Value;

        a.ShouldNotBe(b);
    }
}
