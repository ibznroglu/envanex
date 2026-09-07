namespace Envanex.Application.Products.Commands;

public sealed record ActivateProductCommand(Guid Id, byte[] RowVersion);
