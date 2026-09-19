using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Envanex.Infrastructure.Identity;

public sealed class EnvanexIdentityDbContextFactory : IDesignTimeDbContextFactory<EnvanexIdentityDbContext>
{
    public EnvanexIdentityDbContext CreateDbContext(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var connectionString = Environment.GetEnvironmentVariable("ENVANEX_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ENVANEX_CONNECTION_STRING environment variable is not set. " +
                "Example: set ENVANEX_CONNECTION_STRING=Server=localhost,1433;Database=EnvanexDev;User Id=sa;Password=<password>;TrustServerCertificate=True");
        }

        var optionsBuilder = new DbContextOptionsBuilder<EnvanexIdentityDbContext>();
        optionsBuilder.UseEnvanexIdentitySqlServer(connectionString);

        return new EnvanexIdentityDbContext(optionsBuilder.Options);
    }
}
