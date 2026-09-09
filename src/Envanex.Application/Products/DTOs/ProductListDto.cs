namespace Envanex.Application.Products.DTOs;

public sealed record ProductListDto(
    Guid Id,
    string Code,
    string Name,
    string UnitOfMeasureName,
    decimal ListPriceAmount,
    string ListPriceCurrency,
    decimal ReorderPoint,
    bool IsActive,
    byte[] RowVersion);
