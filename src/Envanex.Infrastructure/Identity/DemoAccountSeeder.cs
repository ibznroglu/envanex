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
/// Idempotent by contract, like <see cref="IdentityRoleSeeder"/>: it runs at every host start, so
/// an account that is already there is an outcome, not a failure. An existing account's password
/// is deliberately left alone.
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
    /// Identity error codes when Identity refused to create the account or to add it to the role.
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

        if (options.Enabled)
        {
            await ThrowIfPasswordIsMisconfiguredAsync(userManager, options);
        }

        // Unconditional: roles are structural, and a host with no roles could never grant anyone a
        // permission, demo or not.
        await IdentityRoleSeeder.EnsureRolesAsync(services, ct);

        if (!options.Enabled)
        {
            return Result.Success();
        }

        // UserManager's API takes no CancellationToken, so the token is honoured between calls.
        ct.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(options.Email);

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

        ct.ThrowIfCancellationRequested();

        if (await userManager.IsInRoleAsync(user, EnvanexRoles.Viewer))
        {
            return Result.Success();
        }

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
