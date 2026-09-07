namespace Envanex.Application.Abstractions.Persistence;

public sealed class DuplicateKeyException : Exception
{
    public string? ConstraintName { get; }

    public DuplicateKeyException(string? constraintName = null, Exception? innerException = null)
        : base($"A duplicate key violation occurred on constraint '{constraintName}'.", innerException)
    {
        ConstraintName = constraintName;
    }
}
