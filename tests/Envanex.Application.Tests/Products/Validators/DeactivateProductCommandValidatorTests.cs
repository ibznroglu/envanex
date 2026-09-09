using Envanex.Application.Products.Commands;
using Envanex.Application.Products.Validators;
using Shouldly;

namespace Envanex.Application.Tests.Products.Validators;

public class DeactivateProductCommandValidatorTests
{
    private readonly DeactivateProductCommandValidator _validator = new();

    private static DeactivateProductCommand ValidCommand => new(
        Id: Guid.CreateVersion7(),
        RowVersion: [1, 2, 3, 4, 5, 6, 7, 8]);

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyGuidId_ShouldFail()
    {
        var command = ValidCommand with { Id = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Product.IdRequired");
    }

    [Fact]
    public void Validate_WithNullRowVersion_ShouldFail()
    {
        var command = ValidCommand with { RowVersion = null! };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Product.RowVersionRequired");
    }

    [Fact]
    public void Validate_WithEmptyRowVersion_ShouldFail()
    {
        var command = ValidCommand with { RowVersion = [] };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Product.RowVersionRequired");
    }
}
