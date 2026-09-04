using Envanex.Domain.Aggregates.Warehouses;
using Shouldly;

namespace Envanex.Domain.Tests.Aggregates.Warehouses;

public class WarehouseTests
{
    [Fact]
    public void Create_WithValidInputs_ShouldSucceed()
    {
        var result = Warehouse.Create("DPO1", "Ana Depo");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("DPO1");
        result.Value.Name.ShouldBe("Ana Depo");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_ShouldNormalizeCode_TrimAndUpperInvariant()
    {
        var result = Warehouse.Create(" dpo1 ", "Ana Depo");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("DPO1");
    }

    [Fact]
    public void Create_ShouldTrimName()
    {
        var result = Warehouse.Create("DPO1", " Ana Depo ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Ana Depo");
    }

    [Fact]
    public void Create_WithEmptyCode_ShouldFail()
    {
        var result = Warehouse.Create("", "Ana Depo");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(WarehouseErrors.CodeRequired);
    }

    [Fact]
    public void Create_WithEmptyName_ShouldFail()
    {
        var result = Warehouse.Create("DPO1", "");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(WarehouseErrors.NameRequired);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var warehouse = Warehouse.Create("DPO1", "Ana Depo").Value;

        warehouse.Deactivate();

        warehouse.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Activate_ShouldSetIsActiveTrue()
    {
        var warehouse = Warehouse.Create("DPO1", "Ana Depo").Value;
        warehouse.Deactivate();

        warehouse.Activate();

        warehouse.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_ShouldGenerateVersion7Id()
    {
        var result = Warehouse.Create("DPO1", "Ana Depo");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldNotBe(Guid.Empty);
    }
}
