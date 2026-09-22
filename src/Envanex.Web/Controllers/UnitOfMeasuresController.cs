using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.Application.UnitOfMeasures.DTOs;
using Envanex.Application.UnitOfMeasures.Queries;
using Envanex.Web.Authorization;
using Envanex.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Envanex.Web.Controllers;

[ApiController]
[Route("api/unit-of-measures")]
public sealed class UnitOfMeasuresController : ControllerBase
{
    private readonly ICommandHandler<CreateUnitOfMeasureCommand, Guid> _createHandler;
    private readonly IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureDetailDto> _getByIdHandler;
    private readonly IUnitOfMeasureReadRepository _readRepository;

    public UnitOfMeasuresController(
        ICommandHandler<CreateUnitOfMeasureCommand, Guid> createHandler,
        IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureDetailDto> getByIdHandler,
        IUnitOfMeasureReadRepository readRepository)
    {
        _createHandler = createHandler;
        _getByIdHandler = getByIdHandler;
        _readRepository = readRepository;
    }

    [HttpPost]
    [Authorize(Policy = EnvanexPolicies.CanWrite)]
    public async Task<IActionResult> Create([FromBody] CreateUnitOfMeasureCommand command, CancellationToken ct)
    {
        var result = await _createHandler.HandleAsync(command, ct);
        return result.ToCreatedActionResult("GetUnitOfMeasureById", id => new { id });
    }

    [HttpGet("{id:guid}", Name = "GetUnitOfMeasureById")]
    [Authorize(Policy = EnvanexPolicies.CanRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.HandleAsync(new GetUnitOfMeasureByIdQuery(id), ct);
        return result.ToActionResult();
    }

    [HttpGet]
    [Authorize(Policy = EnvanexPolicies.CanRead)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var units = await _readRepository.GetAllAsync(ct);
        return Ok(units);
    }
}
