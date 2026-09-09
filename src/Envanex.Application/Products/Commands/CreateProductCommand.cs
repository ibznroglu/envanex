namespace Envanex.Application.Products.Commands;

public sealed record CreateProductCommand(
    string Code,
    string Name,
    Guid UnitOfMeasureId,
    decimal ListPriceAmount,
    string ListPriceCurrency,
    decimal ReorderPoint);
