namespace Envanex.Domain.Common;

public sealed record ValidationError : Error
{
    public IReadOnlyList<ValidationFailure> Failures { get; }

    public ValidationError(IReadOnlyList<ValidationFailure> failures)
        : base("Validation.Failed", "One or more validation errors occurred.")
    {
        Failures = failures;
    }
}

public sealed record ValidationFailure(string PropertyName, string ErrorCode);
