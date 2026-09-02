using Envanex.Domain.Common;
using Shouldly;

namespace Envanex.Domain.Tests.Common;

public class ResultTests
{
    private static readonly Error TestError = new("Test.Error", "Something went wrong.");

    [Fact]
    public void Success_ShouldHaveIsSuccessTrue()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
    }

    [Fact]
    public void Success_ShouldHaveNoneError()
    {
        var result = Result.Success();

        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_ShouldHaveIsFailureTrueAndContainError()
    {
        var result = Result.Failure(TestError);

        result.IsFailure.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(TestError);
    }

    [Fact]
    public void Failure_WithNullError_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Fact]
    public void GenericSuccess_ShouldContainValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void GenericFailure_AccessingValue_ShouldThrow()
    {
        var result = Result.Failure<int>(TestError);

        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void GenericFailure_ShouldContainError()
    {
        var result = Result.Failure<int>(TestError);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TestError);
    }
}
