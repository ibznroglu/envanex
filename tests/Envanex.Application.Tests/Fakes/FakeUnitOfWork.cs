using Envanex.Application.Abstractions.Persistence;

namespace Envanex.Application.Tests.Fakes;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCalled { get; private set; }

    private Exception? _exceptionToThrow;

    public void ThrowOnSaveChanges(Exception exception)
    {
        _exceptionToThrow = exception;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveChangesCalled++;

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        return Task.FromResult(1);
    }
}
