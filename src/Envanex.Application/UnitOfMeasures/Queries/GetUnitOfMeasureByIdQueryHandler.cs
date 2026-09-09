using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.UnitOfMeasures.DTOs;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Domain.Common;

namespace Envanex.Application.UnitOfMeasures.Queries;

public sealed class GetUnitOfMeasureByIdQueryHandler : IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureDetailDto>
{
    private readonly IUnitOfMeasureReadRepository _unitOfMeasureReadRepository;

    public GetUnitOfMeasureByIdQueryHandler(IUnitOfMeasureReadRepository unitOfMeasureReadRepository)
    {
        _unitOfMeasureReadRepository = unitOfMeasureReadRepository;
    }

    public async Task<Result<UnitOfMeasureDetailDto>> HandleAsync(GetUnitOfMeasureByIdQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        UnitOfMeasureDetailDto? dto = await _unitOfMeasureReadRepository.GetByIdAsync(query.Id, ct);

        if (dto is null)
        {
            return Result.Failure<UnitOfMeasureDetailDto>(UnitOfMeasureErrors.NotFound);
        }

        return Result.Success(dto);
    }
}
