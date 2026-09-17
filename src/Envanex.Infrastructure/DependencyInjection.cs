using Envanex.Application.Abstractions.Persistence;
using Envanex.Infrastructure.Identity;
using Envanex.Infrastructure.Persistence;
using Envanex.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Envanex.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("EnvanexDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:EnvanexDb is not configured. " +
                "Set it via user-secrets: " +
                "dotnet user-secrets set \"ConnectionStrings:EnvanexDb\" \"<connection string>\" " +
                "--project src/Envanex.Web");
        }

        services.AddDbContext<EnvanexDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddDbContext<EnvanexIdentityDbContext>(options =>
            options.UseEnvanexIdentitySqlServer(connectionString));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductReadRepository, ProductReadRepository>();
        services.AddScoped<IUnitOfMeasureRepository, UnitOfMeasureRepository>();
        services.AddScoped<IUnitOfMeasureReadRepository, UnitOfMeasureReadRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
