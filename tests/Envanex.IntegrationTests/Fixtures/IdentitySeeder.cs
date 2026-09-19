using Envanex.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Envanex.IntegrationTests.Fixtures;

/// <summary>
/// Creates test users the way the application does: through <see cref="UserManager{TUser}"/>, with
/// a real password, so that every test downstream exercises the same hashing and the same
/// normalized columns the login path reads.
/// </summary>
internal static class IdentitySeeder
{
    /// <summary>
    /// Creates a user and returns its id.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <c>CreateAsync</c> reported failure. Identity returns a failed <see cref="IdentityResult"/>
    /// rather than throwing — a duplicate email under <c>RequireUniqueEmail</c> comes back as
    /// <c>DuplicateEmail</c> — so discarding the result would create no user, report success, and
    /// fail a later test somewhere else with a confusing "user not found".
    /// </exception>
    public static async Task<Guid> CreateUserAsync(IServiceProvider services, string email, string password)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(email);

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<EnvanexUser>>();

        var user = new EnvanexUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);

        ThrowIfFailed(result, $"create the user '{email}'");

        return user.Id;
    }

    public static async Task<int> GetAccessFailedCountAsync(IServiceProvider services, string email)
    {
        var (scope, userManager, user) = await ResolveUserAsync(services, email);

        using (scope)
        {
            return await userManager.GetAccessFailedCountAsync(user);
        }
    }

    public static async Task<DateTimeOffset?> GetLockoutEndAsync(IServiceProvider services, string email)
    {
        var (scope, userManager, user) = await ResolveUserAsync(services, email);

        using (scope)
        {
            return await userManager.GetLockoutEndDateAsync(user);
        }
    }

    /// <summary>
    /// Locks the account out by stamping <c>LockoutEnd</c> directly.
    /// </summary>
    /// <remarks>
    /// Deliberately not "call AccessFailedAsync five times": that path also resets
    /// <c>AccessFailedCount</c> to zero as it locks, which would make the tests that assert the
    /// count depend on a framework detail rather than on what they are named for. Identity reads
    /// the system clock when it evaluates a lockout, so the end date is stamped against
    /// <see cref="DateTimeOffset.UtcNow"/> and not against any injected TimeProvider.
    /// </remarks>
    public static async Task LockOutAsync(IServiceProvider services, string email)
    {
        var (scope, userManager, user) = await ResolveUserAsync(services, email);

        using (scope)
        {
            ThrowIfFailed(
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(15)),
                $"lock out the user '{email}'");
        }
    }

    private static async Task<(IServiceScope Scope, UserManager<EnvanexUser> UserManager, EnvanexUser User)> ResolveUserAsync(
        IServiceProvider services,
        string email)
    {
        ArgumentNullException.ThrowIfNull(services);

        var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<EnvanexUser>>();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            scope.Dispose();
            throw new InvalidOperationException($"No user with the email '{email}' exists.");
        }

        return (scope, userManager, user);
    }

    private static void ThrowIfFailed(IdentityResult result, string what)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));

            throw new InvalidOperationException($"Failed to {what}. Identity reported: {errors}");
        }
    }
}
