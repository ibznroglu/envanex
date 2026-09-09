using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.ValueObjects;
using Shouldly;

namespace Envanex.Domain.Tests.Aggregates.Products;

public class ProductTests
{
    private static readonly Money DefaultPrice = Money.Of(100.50m, Currency.TRY).Value;
    private static readonly Quantity DefaultReorderPoint = Quantity.Of(10m).Value;
    private static readonly Guid ValidUnitOfMeasureId = Guid.CreateVersion7();

    [Fact]
    public void Create_WithValidInputs_ShouldSucceed()
    {
        var result = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("SKU001");
        result.Value.Name.ShouldBe("Widget");
        result.Value.UnitOfMeasureId.ShouldBe(ValidUnitOfMeasureId);
        result.Value.ListPrice.ShouldBe(DefaultPrice);
        result.Value.ReorderPoint.ShouldBe(DefaultReorderPoint);
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_ShouldNormalizeCode_TrimAndUpperInvariant()
    {
        var result = Product.Create(" sku-001 ", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("SKU-001");
    }

    [Fact]
    public void Create_ShouldTrimName()
    {
        var result = Product.Create("SKU001", " Widget ", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Widget");
    }

    [Fact]
    public void Create_WithEmptyCode_ShouldFail()
    {
        var result = Product.Create("", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.CodeRequired);
    }

    [Fact]
    public void Create_WithEmptyName_ShouldFail()
    {
        var result = Product.Create("SKU001", "", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.NameRequired);
    }

    [Fact]
    public void Create_WithEmptyGuidUnitOfMeasureId_ShouldFail()
    {
        var result = Product.Create("SKU001", "Widget", Guid.Empty, DefaultPrice, DefaultReorderPoint);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.UnitOfMeasureRequired);
    }

    [Fact]
    public void Create_ShouldGenerateVersion7Id()
    {
        var result = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void UpdatePrice_ShouldChangeListPrice()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;
        var newPrice = Money.Of(200.75m, Currency.TRY).Value;

        product.UpdatePrice(newPrice);

        product.ListPrice.ShouldBe(newPrice);
    }

    [Fact]
    public void UpdatePrice_WithNull_ShouldThrow()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;

        Should.Throw<ArgumentNullException>(() => product.UpdatePrice(null!));
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;

        product.Deactivate();

        product.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Activate_ShouldSetIsActiveTrue()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;
        product.Deactivate();

        product.Activate();

        product.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithCodeExceedingMaxLength_ShouldFail()
    {
        string longCode = new('A', Product.CodeMaxLength + 1);

        var result = Product.Create(longCode, "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.CodeTooLong);
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ShouldFail()
    {
        string longName = new('A', Product.NameMaxLength + 1);

        var result = Product.Create("SKU001", longName, ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.NameTooLong);
    }

    [Fact]
    public void Update_WithValidInputs_ShouldSucceed()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;
        var newUomId = Guid.CreateVersion7();
        var newPrice = Money.Of(200.75m, Currency.TRY).Value;
        var newReorderPoint = Quantity.Of(20m).Value;

        var result = product.Update("New Name", newUomId, newPrice, newReorderPoint);

        result.IsSuccess.ShouldBeTrue();
        product.Name.ShouldBe("New Name");
        product.UnitOfMeasureId.ShouldBe(newUomId);
        product.ListPrice.ShouldBe(newPrice);
        product.ReorderPoint.ShouldBe(newReorderPoint);
    }

    [Fact]
    public void Update_WithEmptyName_ShouldFail()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;

        var result = product.Update("", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.NameRequired);
    }

    [Fact]
    public void Update_WithNameExceedingMaxLength_ShouldFail()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;
        string longName = new('A', Product.NameMaxLength + 1);

        var result = product.Update(longName, ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.NameTooLong);
    }

    [Fact]
    public void Update_WithEmptyGuidUnitOfMeasureId_ShouldFail()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;

        var result = product.Update("Widget", Guid.Empty, DefaultPrice, DefaultReorderPoint);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.UnitOfMeasureRequired);
    }

    [Fact]
    public void Update_ShouldTrimName()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;

        var result = product.Update(" Updated Name ", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsSuccess.ShouldBeTrue();
        product.Name.ShouldBe("Updated Name");
    }

    [Fact]
    public void Update_ShouldNotChangeCode()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;
        string originalCode = product.Code;

        product.Update("New Name", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        product.Code.ShouldBe(originalCode);
    }

    [Fact]
    public void Update_WithNullListPrice_ShouldThrow()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;

        Should.Throw<ArgumentNullException>(() => product.Update("Widget", ValidUnitOfMeasureId, null!, DefaultReorderPoint));
    }

    [Fact]
    public void Create_WithCodeAtExactMaxLength_ShouldSucceed()
    {
        string code = new('A', Product.CodeMaxLength);

        var result = Product.Create(code, "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe(code);
    }

    [Fact]
    public void Create_WithNameAtExactMaxLength_ShouldSucceed()
    {
        string name = new('A', Product.NameMaxLength);

        var result = Product.Create("SKU001", name, ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe(name);
    }

    [Fact]
    public void Create_WithWhitespaceOnlyName_ShouldFail()
    {
        var result = Product.Create("SKU001", "   ", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.NameRequired);
    }

    [Fact]
    public void Update_WithNameAtExactMaxLength_ShouldSucceed()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;
        string name = new('A', Product.NameMaxLength);

        var result = product.Update(name, ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsSuccess.ShouldBeTrue();
        product.Name.ShouldBe(name);
    }

    [Fact]
    public void Update_WithWhitespaceOnlyName_ShouldFail()
    {
        var product = Product.Create("SKU001", "Widget", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint).Value;

        var result = product.Update("   ", ValidUnitOfMeasureId, DefaultPrice, DefaultReorderPoint);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProductErrors.NameRequired);
    }
}
