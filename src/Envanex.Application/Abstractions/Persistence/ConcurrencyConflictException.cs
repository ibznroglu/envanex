namespace Envanex.Application.Abstractions.Persistence;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string? message = null, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
