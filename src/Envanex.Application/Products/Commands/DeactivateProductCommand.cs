namespace Envanex.Application.Products.Commands;

public sealed record DeactivateProductCommand(Guid Id, byte[] RowVersion);
