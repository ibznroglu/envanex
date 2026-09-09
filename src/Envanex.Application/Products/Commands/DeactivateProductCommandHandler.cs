using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Domain.Aggregates.Products;
using Envanex.Domain.Common;

namespace Envanex.Application.Products.Commands;

public sealed class DeactivateProductCommandHandler : ICommandHandler<DeactivateProductCommand, Guid>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateProductCommandHandler(
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> HandleAsync(DeactivateProductCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Product? product = await _productRepository.GetByIdAsync(command.Id, ct);
        if (product is null)
        {
            return Result.Failure<Guid>(ProductErrors.NotFound);
        }

        product.Deactivate();

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
