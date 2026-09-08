using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Behaviors;
using Envanex.Application.Products.Commands;
using Envanex.Application.Products.DTOs;
using Envanex.Application.Products.Queries;
using Envanex.Application.Products.Validators;
using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.Application.UnitOfMeasures.DTOs;
using Envanex.Application.UnitOfMeasures.Queries;
using Envanex.Application.UnitOfMeasures.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Envanex.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Validators
        services.AddSingleton<IValidator<CreateProductCommand>, CreateProductCommandValidator>();
        services.AddSingleton<IValidator<UpdateProductCommand>, UpdateProductCommandValidator>();
        services.AddSingleton<IValidator<ActivateProductCommand>, ActivateProductCommandValidator>();
        services.AddSingleton<IValidator<DeactivateProductCommand>, DeactivateProductCommandValidator>();
        services.AddSingleton<IValidator<CreateUnitOfMeasureCommand>, CreateUnitOfMeasureCommandValidator>();

        // Inner handlers (concrete types)
        services.AddScoped<CreateProductCommandHandler>();
        services.AddScoped<UpdateProductCommandHandler>();
        services.AddScoped<ActivateProductCommandHandler>();
        services.AddScoped<DeactivateProductCommandHandler>();
        services.AddScoped<CreateUnitOfMeasureCommandHandler>();

        // Decorated command handlers
        services.AddScoped<ICommandHandler<CreateProductCommand, Guid>>(sp =>
            new ValidationDecorator<CreateProductCommand, Guid>(
                sp.GetRequiredService<CreateProductCommandHandler>(),
                sp.GetServices<IValidator<CreateProductCommand>>()));

        services.AddScoped<ICommandHandler<UpdateProductCommand, Guid>>(sp =>
            new ValidationDecorator<UpdateProductCommand, Guid>(
                sp.GetRequiredService<UpdateProductCommandHandler>(),
                sp.GetServices<IValidator<UpdateProductCommand>>()));

        services.AddScoped<ICommandHandler<ActivateProductCommand, Guid>>(sp =>
            new ValidationDecorator<ActivateProductCommand, Guid>(
                sp.GetRequiredService<ActivateProductCommandHandler>(),
                sp.GetServices<IValidator<ActivateProductCommand>>()));

        services.AddScoped<ICommandHandler<DeactivateProductCommand, Guid>>(sp =>
            new ValidationDecorator<DeactivateProductCommand, Guid>(
                sp.GetRequiredService<DeactivateProductCommandHandler>(),
                sp.GetServices<IValidator<DeactivateProductCommand>>()));

        services.AddScoped<ICommandHandler<CreateUnitOfMeasureCommand, Guid>>(sp =>
            new ValidationDecorator<CreateUnitOfMeasureCommand, Guid>(
                sp.GetRequiredService<CreateUnitOfMeasureCommandHandler>(),
                sp.GetServices<IValidator<CreateUnitOfMeasureCommand>>()));

        // Query handlers
        services.AddScoped<IQueryHandler<GetProductByIdQuery, ProductDetailDto>,
            GetProductByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureDetailDto>,
            GetUnitOfMeasureByIdQueryHandler>();

        return services;
    }
}
