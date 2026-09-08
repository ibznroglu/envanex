using Envanex.Application;
using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Abstractions.Persistence;
using Envanex.Application.Behaviors;
using Envanex.Application.Products.Commands;
using Envanex.Application.Products.DTOs;
using Envanex.Application.Products.Queries;
using Envanex.Application.UnitOfMeasures.Commands;
using Envanex.Application.UnitOfMeasures.DTOs;
using Envanex.Application.UnitOfMeasures.Queries;
using Envanex.Infrastructure;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Envanex.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public sealed class DependencyInjectionTests
{
    private readonly SqlServerFixture _fixture;

    public DependencyInjectionTests(SqlServerFixture fixture) => _fixture = fixture;

    private ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EnvanexDb"] = _fixture.ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void AllRegisteredServices_ShouldResolve()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<IProductRepository>().ShouldNotBeNull();
        sp.GetRequiredService<IProductReadRepository>().ShouldNotBeNull();
        sp.GetRequiredService<IUnitOfMeasureRepository>().ShouldNotBeNull();
        sp.GetRequiredService<IUnitOfMeasureReadRepository>().ShouldNotBeNull();
        sp.GetRequiredService<IUnitOfWork>().ShouldNotBeNull();
        sp.GetRequiredService<ICommandHandler<CreateProductCommand, Guid>>().ShouldNotBeNull();
        sp.GetRequiredService<ICommandHandler<UpdateProductCommand, Guid>>().ShouldNotBeNull();
        sp.GetRequiredService<ICommandHandler<ActivateProductCommand, Guid>>().ShouldNotBeNull();
        sp.GetRequiredService<ICommandHandler<DeactivateProductCommand, Guid>>().ShouldNotBeNull();
        sp.GetRequiredService<ICommandHandler<CreateUnitOfMeasureCommand, Guid>>().ShouldNotBeNull();
        sp.GetRequiredService<IQueryHandler<GetProductByIdQuery, ProductDetailDto>>().ShouldNotBeNull();
        sp.GetRequiredService<IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureDetailDto>>().ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(CommandHandlerTypes))]
    public void CommandHandlers_ShouldBeWrappedWithValidationDecorator(
        Type serviceType,
        Type expectedDecoratorType)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var handler = sp.GetRequiredService(serviceType);

        handler.ShouldBeOfType(expectedDecoratorType);
    }

    public static TheoryData<Type, Type> CommandHandlerTypes() => new()
    {
        {
            typeof(ICommandHandler<CreateProductCommand, Guid>),
            typeof(ValidationDecorator<CreateProductCommand, Guid>)
        },
        {
            typeof(ICommandHandler<UpdateProductCommand, Guid>),
            typeof(ValidationDecorator<UpdateProductCommand, Guid>)
        },
        {
            typeof(ICommandHandler<ActivateProductCommand, Guid>),
            typeof(ValidationDecorator<ActivateProductCommand, Guid>)
        },
        {
            typeof(ICommandHandler<DeactivateProductCommand, Guid>),
            typeof(ValidationDecorator<DeactivateProductCommand, Guid>)
        },
        {
            typeof(ICommandHandler<CreateUnitOfMeasureCommand, Guid>),
            typeof(ValidationDecorator<CreateUnitOfMeasureCommand, Guid>)
        },
    };
}
