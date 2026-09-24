using Envanex.Application.Authentication;
using Envanex.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Envanex.Infrastructure.Identity;

/// <summary>
/// Seeds the identity store at host start: the two roles always, and the read-only demo account
/// in the <see cref="EnvanexRoles.Viewer"/> role when <c>Demo:Enabled</c> is true.
/// </summary>
/// <remarks>
/// <para>
/// Idempotent across successive host starts: it runs at every start, so an account that is already
/// there is an outcome, not a failure. An existing account's password is deliberately left alone.
/// </para>
/// <para>
/// Unlike <see cref="IdentityRoleSeeder"/>, it does not survive a concurrent first seeding. Two
/// hosts creating the demo user at the same moment can collide on <c>UserNameIndex</c>; the loser's
/// boot fails, and a restart heals it. Production is a single App Service instance, so the limit is
/// recorded rather than handled.
/// </para>
/// </remarks>
public static class DemoAccountSeeder
{
    private const string UserSecretsProject = "src/Envanex.Web";

    /// <summary>
    /// Ensures the roles, then — only when the demo is enabled — the demo user and its
    /// <see cref="EnvanexRoles.Viewer"/> membership.
    /// </summary>
    /// <returns>
    /// Success when everything the configuration asks for is in place; a failure carrying the
    /// Identity error codes when Identity refused to create the account or to add it to the role;
    /// and a failure naming the roles, with nothing written, when the account at <c>Demo:Email</c>
    /// already exists and holds any role other than <see cref="EnvanexRoles.Viewer"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// <c>Demo:Enabled</c> is true and <c>Demo:Password</c> is blank or fails the password policy.
    /// That is a misconfiguration rather than a seeding outcome, and a public demo must fail at
    /// boot rather than hand out an account whose password nobody chose. Checked before anything
    /// is written, and the message never carries the password itself.
    /// </exception>
    public static async Task<Result> SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Its own scope: the caller is a root provider at host start, and UserManager is scoped.
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DemoAccountOptions>>().Value;
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<EnvanexUser>>();

        EnvanexUser? user = null;
        var isAlreadyViewer = false;

        if (options.Enabled)
        {
            await ThrowIfPasswordIsMisconfiguredAsync(userManager, options);

            // UserManager's API takes no CancellationToken, so the token is honoured between calls.
            ct.ThrowIfCancellationRequested();

            user = await userManager.FindByEmailAsync(options.Email);

            // Only an account that already exists can hold a role; one this call creates holds none.
            // Checked before anything is written, roles included, so a refused boot changes nothing.
            if (user is not null)
            {
                var heldRoles = await userManager.GetRolesAsync(user);

                if (heldRoles.Any(role => !string.Equals(role, EnvanexRoles.Viewer, StringComparison.Ordinal)))
                {
                    return Result.Failure(new Error(
                        "DemoAccount.HasOtherRoles",
                        $"The demo account '{options.Email}' holds the roles " +
                        $"{string.Join(", ", heldRoles.Order(StringComparer.Ordinal))}, and may hold only " +
                        $"'{EnvanexRoles.Viewer}'. Nothing was changed."));
                }

                isAlreadyViewer = heldRoles.Count > 0;
            }
        }

        // Unconditional: roles are structural, and a host with no roles could never grant anyone a
        // permission, demo or not.
        await IdentityRoleSeeder.EnsureRolesAsync(services, ct);

        if (!options.Enabled)
        {
            return Result.Success();
        }

        ct.ThrowIfCancellationRequested();

        if (user is null)
        {
            user = new EnvanexUser { UserName = options.Email, Email = options.Email };

            var created = await userManager.CreateAsync(user, options.Password);

            if (!created.Succeeded)
            {
                return Result.Failure(new Error(
                    "DemoAccount.CreateFailed",
                    $"Failed to create the demo account '{options.Email}'. Identity reported: {JoinCodes(created)}"));
            }
        }
        else if (isAlreadyViewer)
        {
            return Result.Success();
        }

        ct.ThrowIfCancellationRequested();

        var added = await userManager.AddToRoleAsync(user, EnvanexRoles.Viewer);

        return added.Succeeded
            ? Result.Success()
            : Result.Failure(new Error(
                "DemoAccount.AddToRoleFailed",
                $"Failed to add the demo account '{options.Email}' to the role '{EnvanexRoles.Viewer}'. " +
                $"Identity reported: {JoinCodes(added)}"));
    }

    /// <summary>
    /// Runs the configured password through every validator the host's
    /// <see cref="UserManager{TUser}"/> carries, so the policy is the one <c>AddEnvanexIdentity</c>
    /// declares rather than a second copy of it.
    /// </summary>
    private static async Task ThrowIfPasswordIsMisconfiguredAsync(
        UserManager<EnvanexUser> userManager,
        DemoAccountOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException(
                "Demo:Password is not configured, and Demo:Enabled is true. " +
                $"Set it via user-secrets: dotnet user-secrets set \"Demo:Password\" \"<value>\" --project {UserSecretsProject}");
        }

        var candidate = new EnvanexUser { UserName = options.Email, Email = options.Email };
        var codes = new List<string>();

        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, candidate, options.Password);

            if (!result.Succeeded)
            {
                codes.AddRange(result.Errors.Select(error => error.Code));
            }
        }

        if (codes.Count > 0)
        {
            // Codes only, never descriptions or the value: nothing here may echo the password.
            throw new InvalidOperationException(
                "Demo:Password does not satisfy the password policy, and Demo:Enabled is true. " +
                $"Identity reported: {string.Join(", ", codes)}");
        }
    }

    private static string JoinCodes(IdentityResult result)
        => string.Join(", ", result.Errors.Select(error => error.Code));
}
