namespace Envanex.Application.Products.DTOs;

public sealed record ProductDetailDto(
    Guid Id,
    string Code,
    string Name,
    Guid UnitOfMeasureId,
    string UnitOfMeasureName,
    decimal ListPriceAmount,
    string ListPriceCurrency,
    decimal ReorderPoint,
    bool IsActive,
    byte[] RowVersion);
