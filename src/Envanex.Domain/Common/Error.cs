namespace Envanex.Domain.Common;

#pragma warning disable CA1716 // Type name conflicts with reserved keyword — 'Error' is the standard DDD name
public sealed record Error(string Code, string Message)
#pragma warning restore CA1716
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
