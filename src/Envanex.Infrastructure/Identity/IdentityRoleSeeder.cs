using Envanex.Application.Authentication;
using Microsoft.AspNetCore.Identity;
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
            ct.ThrowIfCancellationRequested();

            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));

            if (result.Succeeded)
            {
                continue;
            }

            // Two hosts starting at once can both pass the check above and race the insert. The
            // loser is told the name is taken, and the role it wanted is there either way, so that
            // is an outcome rather than a failure.
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
}
