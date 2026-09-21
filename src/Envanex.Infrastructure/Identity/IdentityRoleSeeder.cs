using Envanex.Application.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Envanex.Infrastructure.Identity;

/// <summary>
/// Creates the roles named by <see cref="EnvanexRoles.All"/> if they are not already there.
/// </summary>
/// <remarks>
/// Idempotent by contract: it runs at every host start, and a second run must be a no-op rather
/// than a failure. Roles are structural — a host whose roles were never created could never grant
/// anyone a permission — so this is deliberately separate from any seeding that is gated by
/// configuration.
/// </remarks>
public static class IdentityRoleSeeder
{
    public static async Task EnsureRolesAsync(IServiceProvider services, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Its own scope: the caller is a root provider at host start, and RoleManager is scoped.
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in EnvanexRoles.All)
        {
            // RoleManager's API takes no CancellationToken, so this is the one place the caller's
            // token can be honoured at all: between roles, never inside the work on one.
            ct.ThrowIfCancellationRequested();

            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            IdentityResult result;

            try
            {
                result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
            catch (Exception exception) when (IsLostSeedRace(exception))
            {
                // The other half of the race, and the half RoleValidator cannot see. Its duplicate
                // check reads the store before the insert, so when both hosts read before either
                // commits, both validators pass and the loser's INSERT is what
                // auth.AspNetRoles.RoleNameIndex rejects. The role the loser wanted exists, so this
                // is an outcome rather than a failure — the same terms as DuplicateRoleName below.
                //
                // Unlike the unique-violation clause in RotateAsync, this one is reached rather
                // than merely defensive: deleting it turns
                // EnsureRolesAsync_RunByTwoHostsAtOnce_ShouldNotThrowAndShouldLeaveExactlyTwoRoles
                // red on the first iteration, with the DbUpdateException escaping host startup.
                continue;
            }

            if (result.Succeeded)
            {
                continue;
            }

            // The interleaving RoleValidator does see: the winner had already committed by the time
            // this host's validator read the store, so the insert never runs and Identity reports
            // the name as taken instead. The role is there either way.
            if (result.Errors.Any(error =>
                string.Equals(error.Code, roleManager.ErrorDescriber.DuplicateRoleName(role).Code, StringComparison.Ordinal)))
            {
                continue;
            }

            // Anything else is a misconfigured or unreachable store, and a host that comes up
            // without its roles hands out an application in which nobody can do anything.
            var errors = string.Join(", ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));

            throw new InvalidOperationException($"Failed to create the role '{role}'. Identity reported: {errors}");
        }
    }

    /// <summary>
    /// A 2601/2627 on <c>auth.AspNetRoles.RoleNameIndex</c>: another host inserted this role name
    /// between this host's validator reading the store and its own INSERT reaching it.
    /// </summary>
    private static bool IsLostSeedRace(Exception exception)
        => exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } };
}
