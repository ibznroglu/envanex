namespace Envanex.Application.UnitOfMeasures.Commands;

public sealed record CreateUnitOfMeasureCommand(
    string Code,
    string Name,
    Guid? BaseUnitId,
    decimal ConversionFactor);
