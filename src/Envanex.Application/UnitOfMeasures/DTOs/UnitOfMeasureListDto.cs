namespace Envanex.Application.UnitOfMeasures.DTOs;

public sealed record UnitOfMeasureListDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive);
