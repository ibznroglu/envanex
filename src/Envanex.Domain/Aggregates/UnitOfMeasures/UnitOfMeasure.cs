using Envanex.Domain.Common;

namespace Envanex.Domain.Aggregates.UnitOfMeasures;

public sealed class UnitOfMeasure : AggregateRoot<Guid>
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public Guid? BaseUnitId { get; private set; }
    public decimal ConversionFactor { get; private set; }
    public bool IsActive { get; private set; }

    private UnitOfMeasure() : base()
    {
        // EF Core
        Code = default!;
        Name = default!;
    }

    private UnitOfMeasure(Guid id, string code, string name, Guid? baseUnitId, decimal conversionFactor)
        : base(id)
    {
        Code = code;
        Name = name;
        BaseUnitId = baseUnitId;
        ConversionFactor = conversionFactor;
        IsActive = true;
    }

    public static Result<UnitOfMeasure> Create(string code, string name, Guid? baseUnitId, decimal conversionFactor)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<UnitOfMeasure>(UnitOfMeasureErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<UnitOfMeasure>(UnitOfMeasureErrors.NameRequired);
        }

        string normalizedCode = code.Trim().ToUpperInvariant();
        string normalizedName = name.Trim();

        if (baseUnitId is null)
        {
            if (conversionFactor != 1m)
            {
                return Result.Failure<UnitOfMeasure>(UnitOfMeasureErrors.BaseUnitFactorMustBeOne);
            }
        }
        else
        {
            if (baseUnitId.Value == Guid.Empty)
            {
                return Result.Failure<UnitOfMeasure>(UnitOfMeasureErrors.InvalidBaseUnitId);
            }

            if (conversionFactor <= 0m)
            {
                return Result.Failure<UnitOfMeasure>(UnitOfMeasureErrors.ConversionFactorMustBePositive);
            }
        }

        var unit = new UnitOfMeasure(Guid.CreateVersion7(), normalizedCode, normalizedName, baseUnitId, conversionFactor);
        return Result.Success(unit);
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
