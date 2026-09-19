using System.Text;
using Envanex.Application.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Envanex.Web.Extensions;

/// <summary>
/// Registers the JWT bearer scheme that validates the access tokens
/// <c>JwtAccessTokenIssuer</c> signs. Web validates, Infrastructure signs, and both read the same
/// <see cref="JwtOptions"/> declaration.
/// </summary>
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddEnvanexJwtBearer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        // Validated here as well as in AddEnvanexIdentity: the validating half of the pair must not
        // be able to come up against a key the signing half would have rejected.
        JwtOptionsGuard.ThrowIfInvalid(options);

        // No JwtBearerEvents.OnChallenge handler: no endpoint is [Authorize] in PR 6a, so a
        // handler would be dead code. It ships with the first [Authorize] in PR 6b.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),

                    // The default five-minute skew would make a 15-minute access token last 20 and
                    // would make every fake-clock expiry test lie.
                    ClockSkew = TimeSpan.Zero,
                };
            });

        return services;
    }
}
