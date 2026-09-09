using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Products.Commands;
using Envanex.Application.Products.DTOs;
using Envanex.Application.Products.Queries;
using Envanex.Web.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Envanex.Web.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IQueryHandler<GetProductByIdQuery, ProductDetailDto> _getProductByIdHandler;
    private readonly ICommandHandler<CreateProductCommand, Guid> _createProductHandler;

    public ProductsController(
        IQueryHandler<GetProductByIdQuery, ProductDetailDto> getProductByIdHandler,
        ICommandHandler<CreateProductCommand, Guid> createProductHandler)
    {
        _getProductByIdHandler = getProductByIdHandler;
        _createProductHandler = createProductHandler;
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
}
