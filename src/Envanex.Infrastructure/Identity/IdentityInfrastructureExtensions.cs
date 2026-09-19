using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Envanex.Infrastructure.Identity;

/// <summary>
/// Wires ASP.NET Core Identity, the JWT options and the three authentication services.
/// </summary>
public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddEnvanexIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(JwtOptions.SectionName);

        // Bound and validated here, at startup, rather than on the first login: a misconfigured
        // signing key must stop the host coming up, not surface as a 500 hours later.
        JwtOptionsGuard.ThrowIfInvalid(section.Get<JwtOptions>() ?? new JwtOptions());
        services.Configure<JwtOptions>(section);

        // TryAdd so a test that registers a FakeTimeProvider first keeps it.
        services.TryAddSingleton(TimeProvider.System);

        // AddIdentityCore, not AddIdentity: SignInManager lives in the shared framework and
        // pulling it in would force a Microsoft.AspNetCore.App framework reference into a class
        // library. Nothing here needs it.
        services.AddIdentityCore<EnvanexUser>(options =>
        {
            options.User.RequireUniqueEmail = true;

            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;

            // Lockout is never visible in a response (every credential failure answers with the
            // same error), so these three values are only observable through IOptions and
            // Identity's meter.
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;
        })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<EnvanexIdentityDbContext>();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        return services;
    }
}
