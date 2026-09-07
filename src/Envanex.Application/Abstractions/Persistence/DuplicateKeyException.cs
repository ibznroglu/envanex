namespace Envanex.Application.Abstractions.Persistence;

public sealed class DuplicateKeyException : Exception
{
    public string? ConstraintName { get; }

    public DuplicateKeyException(string? constraintName = null, Exception? innerException = null)
        : base(constraintName, innerException)
    {
        ConstraintName = constraintName;
    }
}
