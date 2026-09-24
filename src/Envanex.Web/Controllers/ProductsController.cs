using System.Collections.Frozen;
using DevExtreme.AspNet.Data;
using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Products.Commands;
using Envanex.Application.Products.DTOs;
using Envanex.Application.Products.Queries;
using Envanex.Web.Authorization;
using Envanex.Web.DataSource;
using Envanex.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Envanex.Web.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    /// <summary>
    /// Fields the product datasource endpoint accepts for sorting and filtering.
    /// </summary>
    /// <remarks>
    /// "ListPriceCurrency" and "ReorderPoint" are deliberately absent. They map through
    /// value converters (Quantity, Currency), so the projection reads them as member access
    /// into a converted CLR type — translatable only in a final projection. Once
    /// DataSourceLoader composes OrderBy/Where on top, EF Core throws. Keeping them out of
    /// the allowlist turns a 500 into a 400. See ADR 0006 "Alternatives".
    /// <para>
    /// Exposed as a frozen set so integration tests derive their cases from this list instead of
    /// restating it. A field added here is covered by the datasource tests automatically.
    /// </para>
    /// </remarks>
    public static readonly FrozenSet<string> AllowedDataSourceFields = FrozenSet.Create(
        "Code", "Name", "UnitOfMeasureName", "ListPriceAmount", "IsActive");

    /// <summary>
    /// Fields the product datasource endpoint accepts for grouping. A strict subset of
    /// <see cref="AllowedDataSourceFields"/>: grouping by a high-cardinality field such as
    /// "Code" produces one group per row.
    /// </summary>
    public static readonly FrozenSet<string> AllowedDataSourceGroupFields = FrozenSet.Create(
        "UnitOfMeasureName", "IsActive");

    private static readonly DataSourceGuard Guard = new(
        allowedFields: AllowedDataSourceFields,
        allowedGroupFields: AllowedDataSourceGroupFields,
        defaultSortSelector: "Code",
        defaultTake: 20,
        maxTake: 100);

    private readonly IQueryHandler<GetProductByIdQuery, ProductDetailDto> _getProductByIdHandler;
    private readonly ICommandHandler<CreateProductCommand, Guid> _createProductHandler;
    private readonly ICommandHandler<UpdateProductCommand, Guid> _updateProductHandler;
    private readonly ICommandHandler<ActivateProductCommand, Guid> _activateProductHandler;
    private readonly ICommandHandler<DeactivateProductCommand, Guid> _deactivateProductHandler;
    private readonly IProductReadRepository _productReadRepository;

    public ProductsController(
        IQueryHandler<GetProductByIdQuery, ProductDetailDto> getProductByIdHandler,
        ICommandHandler<CreateProductCommand, Guid> createProductHandler,
        ICommandHandler<UpdateProductCommand, Guid> updateProductHandler,
        ICommandHandler<ActivateProductCommand, Guid> activateProductHandler,
        ICommandHandler<DeactivateProductCommand, Guid> deactivateProductHandler,
        IProductReadRepository productReadRepository)
    {
        _getProductByIdHandler = getProductByIdHandler;
        _createProductHandler = createProductHandler;
        _updateProductHandler = updateProductHandler;
        _activateProductHandler = activateProductHandler;
        _deactivateProductHandler = deactivateProductHandler;
        _productReadRepository = productReadRepository;
    }

    [HttpGet("{id:guid}", Name = "GetProductById")]
    [Authorize(Policy = EnvanexPolicies.CanRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getProductByIdHandler.HandleAsync(new GetProductByIdQuery(id), ct);
        return result.ToActionResult();
    }

    [HttpPost]
    [Authorize(Policy = EnvanexPolicies.CanWrite)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct)
    {
        var result = await _createProductHandler.HandleAsync(command, ct);
        return result.ToCreatedActionResult("GetProductById", id => new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = EnvanexPolicies.CanWrite)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Id != id)
        {
            return Problem(
                detail: "Route id and body Id do not match.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        var result = await _updateProductHandler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = EnvanexPolicies.CanWrite)]
    public async Task<IActionResult> Activate(Guid id, [FromBody] ActivateProductCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Id != id)
        {
            return Problem(
                detail: "Route id and body Id do not match.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        var result = await _activateProductHandler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = EnvanexPolicies.CanWrite)]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] DeactivateProductCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Id != id)
        {
            return Problem(
                detail: "Route id and body Id do not match.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        var result = await _deactivateProductHandler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    [HttpGet("datasource")]
    [Authorize(Policy = EnvanexPolicies.CanRead)]
    public async Task<IActionResult> GetDataSource(DataSourceLoadOptionsBase options, CancellationToken ct)
    {
        var guardResult = Guard.ValidateAndApply(options);
        if (guardResult.IsFailure)
        {
            return Problem(
                detail: guardResult.Error.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        var validatedOptions = guardResult.Value;

        var loadResult = await DataSourceLoader.LoadAsync(_productReadRepository.GetAll(), validatedOptions, ct);
        return Ok(loadResult);
    }
}
