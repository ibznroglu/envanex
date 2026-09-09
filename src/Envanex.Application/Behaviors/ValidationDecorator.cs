using Envanex.Application.Abstractions.Messaging;
using Envanex.Domain.Common;
using FluentValidation;

namespace Envanex.Application.Behaviors;

public sealed class ValidationDecorator<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
{
    private readonly ICommandHandler<TCommand, TResponse> _inner;
    private readonly IEnumerable<IValidator<TCommand>> _validators;

    public ValidationDecorator(
        ICommandHandler<TCommand, TResponse> inner,
        IEnumerable<IValidator<TCommand>> validators)
    {
        _inner = inner;
        _validators = validators;
    }

    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        var validatorList = _validators.ToList();

        if (validatorList.Count == 0)
        {
            return await _inner.HandleAsync(command, ct);
        }

        var failures = validatorList
            .SelectMany(v => v.Validate(command).Errors)
            .Select(f => new ValidationFailure(f.PropertyName, f.ErrorCode))
            .ToList();

        if (failures.Count == 0)
        {
            return await _inner.HandleAsync(command, ct);
        }

        return Result.Failure<TResponse>(new ValidationError([.. failures]));
    }
}
