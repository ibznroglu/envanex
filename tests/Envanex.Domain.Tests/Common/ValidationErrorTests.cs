using Envanex.Domain.Common;
using Shouldly;

namespace Envanex.Domain.Tests.Common;

public class ValidationErrorTests
{
    [Fact]
    public void ValidationError_ShouldBeAnError()
    {
        var failures = new List<ValidationFailure>
        {
            new("Name", "Product.NameRequired")
        };

        var validationError = new ValidationError(failures);
        var result = Result.Failure<int>(validationError);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
        (result.Error is ValidationError).ShouldBeTrue();
    }

    [Fact]
    public void ValidationError_ShouldCarryAllFailures()
    {
        var failures = new List<ValidationFailure>
        {
            new("Code", "Product.CodeRequired"),
            new("Name", "Product.NameRequired"),
            new("UnitOfMeasureId", "Product.UnitOfMeasureRequired")
        };

        var validationError = new ValidationError(failures);

        validationError.Failures.Count.ShouldBe(3);
        validationError.Failures[0].PropertyName.ShouldBe("Code");
        validationError.Failures[0].ErrorCode.ShouldBe("Product.CodeRequired");
        validationError.Failures[1].PropertyName.ShouldBe("Name");
        validationError.Failures[1].ErrorCode.ShouldBe("Product.NameRequired");
        validationError.Failures[2].PropertyName.ShouldBe("UnitOfMeasureId");
        validationError.Failures[2].ErrorCode.ShouldBe("Product.UnitOfMeasureRequired");
        validationError.Code.ShouldBe("Validation.Failed");
        validationError.Message.ShouldBe("One or more validation errors occurred.");
    }
}
