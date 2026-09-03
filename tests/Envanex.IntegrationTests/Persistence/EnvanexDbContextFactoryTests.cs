using Envanex.Infrastructure.Persistence;
using Shouldly;

namespace Envanex.IntegrationTests.Persistence;

public class EnvanexDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WhenConnectionStringMissing_ThrowsInvalidOperationException()
    {
        // Ensure the env var is not set for this test
        Environment.SetEnvironmentVariable("ENVANEX_CONNECTION_STRING", null);

        var factory = new EnvanexDbContextFactory();

        var exception = Should.Throw<InvalidOperationException>(
            () => factory.CreateDbContext([]));

        exception.Message.ShouldContain("ENVANEX_CONNECTION_STRING");
    }

    [Fact]
    public void CreateDbContext_WhenConnectionStringSet_ReturnsContext()
    {
        const string connectionString = "Server=localhost,1433;Database=TestDb;User Id=sa;Password=Test123!;TrustServerCertificate=True";

        Environment.SetEnvironmentVariable("ENVANEX_CONNECTION_STRING", connectionString);

        try
        {
            var factory = new EnvanexDbContextFactory();

            using var context = factory.CreateDbContext([]);

            context.ShouldNotBeNull();
            context.ShouldBeOfType<EnvanexDbContext>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ENVANEX_CONNECTION_STRING", null);
        }
    }

    [Fact]
    public void CreateDbContext_WhenArgsNull_ThrowsArgumentNullException()
    {
        var factory = new EnvanexDbContextFactory();

        Should.Throw<ArgumentNullException>(
            () => factory.CreateDbContext(null!));
    }
}
