using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Envanex.Infrastructure.Identity;

/// <summary>
/// The Identity model, kept in its own context and its own <c>auth</c> schema so that the
/// business context's model-finalizing conventions never run over Identity's entity types and
/// so that neither context can read the other's applied migrations as its own.
/// </summary>
public class EnvanexIdentityDbContext : IdentityDbContext<EnvanexUser, IdentityRole<Guid>, Guid>
{
    public EnvanexIdentityDbContext(DbContextOptions<EnvanexIdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Every Identity table lands in "auth". Deliberately no ApplyConfigurationsFromAssembly
        // and no ConfigureConventions override: AggregateRootConvention and
        // MoneyComplexTypeConvention must not reach this model.
        builder.HasDefaultSchema(AuthSchema.Name);

        base.OnModelCreating(builder);

        // Identity declares EmailIndex non-unique, but the options set RequireUniqueEmail, which
        // is only a read-then-insert check in the application. Email is the credential the login
        // endpoint looks the user up by, so the uniqueness of that lookup is made a database
        // constraint here, in the same shape Identity already uses for UserNameIndex.
        builder.Entity<EnvanexUser>()
            .HasIndex(user => user.NormalizedEmail)
            .IsUnique();
    }
}
