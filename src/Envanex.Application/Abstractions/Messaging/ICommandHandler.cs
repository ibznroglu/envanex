using Envanex.Domain.Common;

namespace Envanex.Application.Abstractions.Messaging;

public interface ICommandHandler<in TCommand, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken ct = default);
}
