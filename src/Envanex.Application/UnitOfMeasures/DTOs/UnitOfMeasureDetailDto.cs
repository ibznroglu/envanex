namespace Envanex.Application.UnitOfMeasures.DTOs;

public sealed record UnitOfMeasureDetailDto(
    Guid Id,
    string Code,
    string Name,
    Guid? BaseUnitId,
    string? BaseUnitName,
    decimal ConversionFactor,
    bool IsActive,
    byte[] RowVersion);
