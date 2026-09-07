using Envanex.Domain.Common;

namespace Envanex.Domain.Aggregates.UnitOfMeasures;

public static class UnitOfMeasureErrors
{
    public static readonly Error CodeRequired = new("UnitOfMeasure.CodeRequired", "Unit of measure code is required.");
    public static readonly Error NameRequired = new("UnitOfMeasure.NameRequired", "Unit of measure name is required.");
    public static readonly Error BaseUnitFactorMustBeOne = new("UnitOfMeasure.BaseUnitFactorMustBeOne", "A base unit must have a conversion factor of 1.");
    public static readonly Error ConversionFactorMustBePositive = new("UnitOfMeasure.ConversionFactorMustBePositive", "A derived unit must have a positive conversion factor.");
    public static readonly Error InvalidBaseUnitId = new("UnitOfMeasure.InvalidBaseUnitId", "Base unit identifier must not be an empty GUID.");
    public static readonly Error CodeTooLong = new("UnitOfMeasure.CodeTooLong", "Unit of measure code must not exceed 20 characters.");
    public static readonly Error NameTooLong = new("UnitOfMeasure.NameTooLong", "Unit of measure name must not exceed 200 characters.");
    public static readonly Error DuplicateCode = new("UnitOfMeasure.DuplicateCode", "A unit of measure with this code already exists.");
    public static readonly Error NotFound = new("UnitOfMeasure.NotFound", "Unit of measure was not found.");
}
