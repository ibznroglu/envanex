namespace Envanex.Application.Products.Commands;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    Guid UnitOfMeasureId,
    decimal ListPriceAmount,
    string ListPriceCurrency,
    decimal ReorderPoint,
    byte[] RowVersion);
