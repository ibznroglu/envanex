using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Envanex.Infrastructure.Persistence;

public sealed class EnvanexDbContextFactory : IDesignTimeDbContextFactory<EnvanexDbContext>
{
    public EnvanexDbContext CreateDbContext(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var connectionString = Environment.GetEnvironmentVariable("ENVANEX_CONNECTION_STRING");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "ENVANEX_CONNECTION_STRING environment variable is not set. " +
                "Example: set ENVANEX_CONNECTION_STRING=Server=localhost,1433;Database=EnvanexDev;User Id=sa;Password=<password>;TrustServerCertificate=True");
        }

        var optionsBuilder = new DbContextOptionsBuilder<EnvanexDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new EnvanexDbContext(optionsBuilder.Options);
    }
}
