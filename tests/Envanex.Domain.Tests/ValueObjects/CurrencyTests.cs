using Envanex.Domain.ValueObjects;
using Shouldly;

namespace Envanex.Domain.Tests.ValueObjects;

public class CurrencyTests
{
    [Fact]
    public void Of_WithValidCode_ShouldSucceed()
    {
        var result = Currency.Of("TRY");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("TRY");
    }

    [Fact]
    public void Of_WithLowercaseCode_ShouldNormalize()
    {
        var result = Currency.Of("try");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("TRY");
    }

    [Fact]
    public void Of_WithInvalidCode_ShouldFail()
    {
        var result = Currency.Of("XYZ");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CurrencyErrors.InvalidCode);
    }

    [Fact]
    public void Of_WithEmptyString_ShouldFail()
    {
        var result = Currency.Of("");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Of_WithNull_ShouldFail()
    {
        var result = Currency.Of(null!);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Of_WithWrongLength_ShouldFail()
    {
        var result = Currency.Of("US");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void WellKnown_TRY_ShouldHaveCorrectCode()
    {
        Currency.TRY.Code.ShouldBe("TRY");
    }

    [Fact]
    public void Currencies_WithSameCode_ShouldBeEqual()
    {
        var result1 = Currency.Of("TRY");
        var result2 = Currency.Of("TRY");

        result1.Value.ShouldBe(result2.Value);
    }

    [Fact]
    public void Currencies_WithDifferentCode_ShouldNotBeEqual()
    {
        var result1 = Currency.Of("TRY");
        var result2 = Currency.Of("USD");

        result1.Value.ShouldNotBe(result2.Value);
    }
}
