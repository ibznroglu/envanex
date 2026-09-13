namespace Envanex.Application.UnitOfMeasures.DTOs;

/// <summary>
/// ListDto types are kept as object initializers because they may be used in datasource
/// projections. EF Core cannot translate DataSourceLoader's OrderBy over positional records.
/// </summary>
public sealed class UnitOfMeasureListDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}
