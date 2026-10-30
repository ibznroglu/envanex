using Envanex.Domain.Common;
using Envanex.Domain.ValueObjects;

namespace Envanex.Domain.Aggregates.Products;

public sealed class Product : AggregateRoot<Guid>
{
    public const int CodeMaxLength = 50;
    public const int NameMaxLength = 200;
    public const decimal ListPriceAmountMaxValue = 99999999999999.9999m;
    public const decimal ReorderPointMaxValue = 999999999999.999999m;

    public string Code { get; private set; }
    public string Name { get; private set; }
    public Guid UnitOfMeasureId { get; private set; }
    public Money ListPrice { get; private set; }
    public Quantity ReorderPoint { get; private set; }
    public bool IsActive { get; private set; }

    private Product() : base()
    {
        // EF Core
        Code = default!;
        Name = default!;
        ListPrice = default!;
    }

    private Product(Guid id, string code, string name, Guid unitOfMeasureId, Money listPrice, Quantity reorderPoint)
        : base(id)
    {
        Code = code;
        Name = name;
        UnitOfMeasureId = unitOfMeasureId;
        ListPrice = listPrice;
        ReorderPoint = reorderPoint;
        IsActive = true;
    }

    public static Result<Product> Create(string code, string name, Guid unitOfMeasureId, Money listPrice, Quantity reorderPoint)
    {
        ArgumentNullException.ThrowIfNull(listPrice);

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<Product>(ProductErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Product>(ProductErrors.NameRequired);
        }

        if (code.Trim().Length > CodeMaxLength)
        {
            return Result.Failure<Product>(ProductErrors.CodeTooLong);
        }

        if (name.Trim().Length > NameMaxLength)
        {
            return Result.Failure<Product>(ProductErrors.NameTooLong);
        }

        if (unitOfMeasureId == Guid.Empty)
        {
            return Result.Failure<Product>(ProductErrors.UnitOfMeasureRequired);
        }

        if (listPrice.Amount < 0m)
        {
            return Result.Failure<Product>(ProductErrors.ListPriceAmountNegative);
        }

        if (listPrice.Amount > ListPriceAmountMaxValue)
        {
            return Result.Failure<Product>(ProductErrors.ListPriceAmountTooLarge);
        }

        if (reorderPoint.Value > ReorderPointMaxValue)
        {
            return Result.Failure<Product>(ProductErrors.ReorderPointTooLarge);
        }

        string normalizedCode = code.Trim().ToUpperInvariant();
        string normalizedName = name.Trim();

        var product = new Product(Guid.CreateVersion7(), normalizedCode, normalizedName, unitOfMeasureId, listPrice, reorderPoint);
        return Result.Success(product);
    }

    public Result UpdatePrice(Money newPrice)
    {
        ArgumentNullException.ThrowIfNull(newPrice);

        if (newPrice.Amount < 0m)
        {
            return Result.Failure(ProductErrors.ListPriceAmountNegative);
        }

        if (newPrice.Amount > ListPriceAmountMaxValue)
        {
            return Result.Failure(ProductErrors.ListPriceAmountTooLarge);
        }

        ListPrice = newPrice;

        return Result.Success();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public Result Update(string name, Guid unitOfMeasureId, Money listPrice, Quantity reorderPoint)
    {
        ArgumentNullException.ThrowIfNull(listPrice);

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(ProductErrors.NameRequired);
        }

        if (name.Trim().Length > NameMaxLength)
        {
            return Result.Failure(ProductErrors.NameTooLong);
        }

        if (unitOfMeasureId == Guid.Empty)
        {
            return Result.Failure(ProductErrors.UnitOfMeasureRequired);
        }

        if (listPrice.Amount < 0m)
        {
            return Result.Failure(ProductErrors.ListPriceAmountNegative);
        }

        if (listPrice.Amount > ListPriceAmountMaxValue)
        {
            return Result.Failure(ProductErrors.ListPriceAmountTooLarge);
        }

        if (reorderPoint.Value > ReorderPointMaxValue)
        {
            return Result.Failure(ProductErrors.ReorderPointTooLarge);
        }

        Name = name.Trim();
        UnitOfMeasureId = unitOfMeasureId;
        ListPrice = listPrice;
        ReorderPoint = reorderPoint;

        return Result.Success();
    }
}
