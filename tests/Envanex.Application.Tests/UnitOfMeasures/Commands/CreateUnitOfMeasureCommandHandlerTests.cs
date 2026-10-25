using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Tests.Fakes;
using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Domain.Common;
using Shouldly;

namespace Envanex.Application.Tests.UnitOfMeasures.Commands;

public class CreateUnitOfMeasureCommandHandlerTests
{
    private readonly FakeUnitOfMeasureRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreateUnitOfMeasureCommandHandler _handler;

    public CreateUnitOfMeasureCommandHandlerTests()
    {
        _handler = new CreateUnitOfMeasureCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WithValidBaseUnitCommand_ShouldReturnUnitId()
    {
        var command = new CreateUnitOfMeasureCommand("KG", "Kilogram", null, 1m);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
        _repository.Units.Count.ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateCode_ShouldReturnDuplicateCodeError()
    {
        UnitOfMeasure unit = UnitOfMeasure.Create("KG", "Kilogram", null, 1m).Value;
        _repository.Seed(unit);

        var command = new CreateUnitOfMeasureCommand("KG", "Kilogram Again", null, 1m);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.DuplicateCode);
    }

    [Fact]
    public async Task HandleAsync_WhenUnitOfWorkThrowsDuplicateKey_ShouldReturnDuplicateCodeError()
    {
        _unitOfWork.ThrowOnSaveChanges(new DuplicateKeyException("IX_UnitOfMeasures_Code"));

        var command = new CreateUnitOfMeasureCommand("KG", "Kilogram", null, 1m);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.DuplicateCode);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentBaseUnit_ShouldReturnBaseUnitNotFoundError()
    {
        Guid nonExistentId = Guid.CreateVersion7();
        var command = new CreateUnitOfMeasureCommand("G", "Gram", nonExistentId, 0.001m);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.BaseUnitNotFound);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveBaseUnit_ShouldReturnBaseUnitInactiveError()
    {
        Guid inactiveId = Guid.CreateVersion7();
        _repository.SeedActiveStatus(inactiveId, false);
        var command = new CreateUnitOfMeasureCommand("G", "Gram", inactiveId, 0.001m);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.BaseUnitInactive);
    }

    [Fact]
    public async Task HandleAsync_WithNullBaseUnit_ShouldNotCheckBaseUnit()
    {
        var command = new CreateUnitOfMeasureCommand("KG", "Kilogram", null, 1m);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsSuccess.ShouldBeTrue();
        _unitOfWork.SaveChangesCalled.ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_WithDerivedFactorBeyondSixDecimalPlaces_ShouldReturnTooPreciseWithoutSaving()
    {
        Guid baseId = Guid.CreateVersion7();
        _repository.SeedActiveStatus(baseId, true);
        var command = new CreateUnitOfMeasureCommand("GR", "Gram", baseId, 0.0000001m);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.ConversionFactorTooPrecise);
        _unitOfWork.SaveChangesCalled.ShouldBe(0);
        _repository.Units.Count.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_WithDerivedFactorAboveMaximum_ShouldReturnTooLargeWithoutSaving()
    {
        Guid baseId = Guid.CreateVersion7();
        _repository.SeedActiveStatus(baseId, true);
        var command = new CreateUnitOfMeasureCommand("GR", "Gram", baseId, 1000000000000m);

        Result<Guid> result = await _handler.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.ConversionFactorTooLarge);
        _unitOfWork.SaveChangesCalled.ShouldBe(0);
        _repository.Units.Count.ShouldBe(0);
    }
}
