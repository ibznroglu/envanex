using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Domain.Aggregates.UnitOfMeasures;
using Envanex.Domain.Common;

namespace Envanex.Application.UnitOfMeasures.Commands;

public sealed class CreateUnitOfMeasureCommandHandler : ICommandHandler<CreateUnitOfMeasureCommand, Guid>
{
    private readonly IUnitOfMeasureRepository _unitOfMeasureRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUnitOfMeasureCommandHandler(
        IUnitOfMeasureRepository unitOfMeasureRepository,
        IUnitOfWork unitOfWork)
    {
        _unitOfMeasureRepository = unitOfMeasureRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> HandleAsync(CreateUnitOfMeasureCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool exists = await _unitOfMeasureRepository.ExistsByCodeAsync(command.Code, ct);
        if (exists)
        {
            return Result.Failure<Guid>(UnitOfMeasureErrors.DuplicateCode);
        }

        if (command.BaseUnitId is not null)
        {
            bool? activeStatus = await _unitOfMeasureRepository.GetActiveStatusAsync(command.BaseUnitId.Value, ct);
            if (activeStatus is null)
            {
                return Result.Failure<Guid>(UnitOfMeasureErrors.BaseUnitNotFound);
            }

            if (activeStatus == false)
            {
                return Result.Failure<Guid>(UnitOfMeasureErrors.BaseUnitInactive);
            }
        }

        Result<UnitOfMeasure> unitResult = UnitOfMeasure.Create(
            command.Code, command.Name, command.BaseUnitId, command.ConversionFactor);

        if (unitResult.IsFailure)
        {
            return Result.Failure<Guid>(unitResult.Error);
        }

        UnitOfMeasure unit = unitResult.Value;
        await _unitOfMeasureRepository.AddAsync(unit, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DuplicateKeyException)
        {
            return Result.Failure<Guid>(UnitOfMeasureErrors.DuplicateCode);
        }

        return Result.Success(unit.Id);
    }
}
