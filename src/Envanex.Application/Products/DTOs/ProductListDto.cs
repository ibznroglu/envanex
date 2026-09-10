namespace Envanex.Application.Products.DTOs;

/// <summary>
/// Sealed class with required-init properties, not a positional record. EF Core's query pipeline
/// cannot translate DataSourceLoader's OrderBy member-access expressions composed on top of a
/// NewExpression (constructor-based projection). Using a positional record causes the datasource
/// query to fall back to client-side evaluation, materializing the entire table.
/// </summary>
public sealed class ProductListDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string UnitOfMeasureName { get; init; }
    public required decimal ListPriceAmount { get; init; }
    public required string ListPriceCurrency { get; init; }
    public required decimal ReorderPoint { get; init; }
    public required bool IsActive { get; init; }
}
