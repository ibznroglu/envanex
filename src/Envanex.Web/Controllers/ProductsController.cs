using DevExtreme.AspNet.Data;
using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Products.Commands;
using Envanex.Application.Products.DTOs;
using Envanex.Application.Products.Queries;
using Envanex.Web.DataSource;
using Envanex.Web.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Envanex.Web.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private static readonly DataSourceGuard Guard = new(
        allowedFields: new HashSet<string>
        {
            "Code", "Name", "UnitOfMeasureName", "ListPriceAmount",
            "ListPriceCurrency", "ReorderPoint", "IsActive",
        },
        allowedGroupFields: new HashSet<string> { "UnitOfMeasureName", "IsActive" },
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
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getProductByIdHandler.HandleAsync(new GetProductByIdQuery(id), ct);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct)
    {
        var result = await _createProductHandler.HandleAsync(command, ct);
        return result.ToCreatedActionResult("GetProductById", id => new { id });
    }

    [HttpPut("{id:guid}")]
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
