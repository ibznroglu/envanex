using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Common;
using Envanex.Domain.ValueObjects;

namespace Envanex.Application.Products.Commands;

public sealed class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand, Guid>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfMeasureRepository _unitOfMeasureRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductCommandHandler(
        IProductRepository productRepository,
        IUnitOfMeasureRepository unitOfMeasureRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfMeasureRepository = unitOfMeasureRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> HandleAsync(UpdateProductCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Product? product = await _productRepository.GetByIdAsync(command.Id, ct);
        if (product is null)
        {
            return Result.Failure<Guid>(ProductErrors.NotFound);
        }

        bool? activeStatus = await _unitOfMeasureRepository.GetActiveStatusAsync(command.UnitOfMeasureId, ct);
        if (activeStatus is null)
        {
            return Result.Failure<Guid>(ProductErrors.UnitOfMeasureNotFound);
        }

        if (activeStatus == false)
        {
            return Result.Failure<Guid>(ProductErrors.UnitOfMeasureInactive);
        }

        Result<Currency> currencyResult = Currency.Of(command.ListPriceCurrency);
        if (currencyResult.IsFailure)
        {
            return Result.Failure<Guid>(currencyResult.Error);
        }

        Result<Money> moneyResult = Money.Of(command.ListPriceAmount, currencyResult.Value);
        if (moneyResult.IsFailure)
        {
            return Result.Failure<Guid>(moneyResult.Error);
        }

        Result<Quantity> quantityResult = Quantity.Of(command.ReorderPoint);
        if (quantityResult.IsFailure)
        {
            return Result.Failure<Guid>(quantityResult.Error);
        }

        Result updateResult = product.Update(command.Name, command.UnitOfMeasureId, moneyResult.Value, quantityResult.Value);
        if (updateResult.IsFailure)
        {
            return Result.Failure<Guid>(updateResult.Error);
        }

        _productRepository.SetOriginalRowVersion(product, command.RowVersion);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure<Guid>(ProductErrors.ConcurrencyConflict);
        }

        return Result.Success(product.Id);
    }
}
