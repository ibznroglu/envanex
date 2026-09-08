using Envanex.Application.Abstractions.Persistence;

namespace Envanex.Application.Tests.Fakes;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly FakeProductRepository? _productRepository;

    public int SaveChangesCalled { get; private set; }

    /// <summary>
    /// <see langword="true"/> if <see cref="FakeProductRepository.SetOriginalRowVersion"/>
    /// had been called at least once before the first <see cref="SaveChangesAsync"/> invocation.
    /// <see langword="null"/> when no <see cref="FakeProductRepository"/> was provided or
    /// <see cref="SaveChangesAsync"/> has not been called yet.
    /// </summary>
    public bool? RowVersionWasSetBeforeSave { get; private set; }

    public FakeUnitOfWork()
    {
    }

    public FakeUnitOfWork(FakeProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    private Exception? _exceptionToThrow;

    public void ThrowOnSaveChanges(Exception exception)
    {
        _exceptionToThrow = exception;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (_productRepository is not null && RowVersionWasSetBeforeSave is null)
        {
            RowVersionWasSetBeforeSave = _productRepository.SetOriginalRowVersionCallCount > 0;
        }

        SaveChangesCalled++;

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        return Task.FromResult(1);
    }
}
