using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Common;
using Envanex.Domain.ValueObjects;

namespace Envanex.Application.Products.Commands;

public sealed class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Guid>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfMeasureRepository _unitOfMeasureRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductCommandHandler(
        IProductRepository productRepository,
        IUnitOfMeasureRepository unitOfMeasureRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfMeasureRepository = unitOfMeasureRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> HandleAsync(CreateProductCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool exists = await _productRepository.ExistsByCodeAsync(command.Code, ct);
        if (exists)
        {
            return Result.Failure<Guid>(ProductErrors.DuplicateCode);
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

        Result<Product> productResult = Product.Create(
            command.Code, command.Name, command.UnitOfMeasureId,
            moneyResult.Value, quantityResult.Value);

        if (productResult.IsFailure)
        {
            return Result.Failure<Guid>(productResult.Error);
        }

        Product product = productResult.Value;
        await _productRepository.AddAsync(product, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DuplicateKeyException)
        {
            return Result.Failure<Guid>(ProductErrors.DuplicateCode);
        }

        return Result.Success(product.Id);
    }
}
