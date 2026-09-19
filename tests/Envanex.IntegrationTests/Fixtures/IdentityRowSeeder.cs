using Envanex.Infrastructure.Identity;

namespace Envanex.IntegrationTests.Fixtures;

/// <summary>
/// The narrow, named exception to "test users are created through <c>UserManager</c>".
/// </summary>
/// <remarks>
/// That rule exists so login tests exercise real password hashing and Identity's normalized
/// columns — not to forbid a row that merely satisfies a foreign key. The schema tests never
/// authenticate; they assert index shape, column types and foreign key behaviour, and they need a
/// user row for <c>auth.RefreshTokens.UserId</c> to point at. The row written here has no password
/// hash, so it cannot be logged in with and cannot hide a hashing defect. The exception is kept
/// narrow by <c>IdentitySeedingScopeTests</c>, not by this comment.
/// </remarks>
internal static class IdentityRowSeeder
{
    /// <summary>
    /// Inserts a bare <c>auth.AspNetUsers</c> row through <see cref="EnvanexIdentityDbContext"/>
    /// and returns its id.
    /// </summary>
    /// <remarks>
    /// This bypasses <c>UserValidator</c> and writes <c>NormalizedEmail</c> directly, and
    /// <c>auth.AspNetUsers.EmailIndex</c> is a unique filtered index. Two calls with the same
    /// <paramref name="email"/> and no intervening reset therefore fail with SQL Server error
    /// 2601; callers pass an email unique to the call.
    /// </remarks>
    public static async Task<Guid> InsertBareUserRowAsync(SqlServerFixture fixture, string email)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(email);

        var user = new EnvanexUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        };

        await using var context = fixture.CreateIdentityDbContext();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }
}
