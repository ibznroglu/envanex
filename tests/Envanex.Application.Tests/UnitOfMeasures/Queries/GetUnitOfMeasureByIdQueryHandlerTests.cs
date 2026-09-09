using Envanex.Application.Tests.Fakes;
using Envanex.Application.UnitOfMeasures.DTOs;
using Envanex.Application.UnitOfMeasures.Queries;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Domain.Common;
using Shouldly;

namespace Envanex.Application.Tests.UnitOfMeasures.Queries;

public class GetUnitOfMeasureByIdQueryHandlerTests
{
    private readonly FakeUnitOfMeasureReadRepository _readRepository = new();
    private readonly GetUnitOfMeasureByIdQueryHandler _handler;

    public GetUnitOfMeasureByIdQueryHandlerTests()
    {
        _handler = new GetUnitOfMeasureByIdQueryHandler(_readRepository);
    }

    [Fact]
    public async Task HandleAsync_WithExistingUnit_ShouldReturnUnitDetail()
    {
        Guid unitId = Guid.CreateVersion7();
        var dto = new UnitOfMeasureDetailDto(
            unitId, "KG", "Kilogram", null, null, 1m, true, [1, 2, 3, 4, 5, 6, 7, 8]);
        _readRepository.SeedDetail(dto);

        var query = new GetUnitOfMeasureByIdQuery(unitId);

        Result<UnitOfMeasureDetailDto> result = await _handler.HandleAsync(query);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(dto);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentUnit_ShouldReturnNotFoundError()
    {
        var query = new GetUnitOfMeasureByIdQuery(Guid.CreateVersion7());

        Result<UnitOfMeasureDetailDto> result = await _handler.HandleAsync(query);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.NotFound);
    }
}
