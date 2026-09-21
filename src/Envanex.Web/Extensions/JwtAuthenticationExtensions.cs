using System.Text;
using Envanex.Application.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
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

                    // Folded into this initializer rather than assigned after it: the line above
                    // replaces the whole TokenValidationParameters object, so anything set on the
                    // previous one is silently discarded.
                    //
                    // The claim types the issuer actually writes. IdentityOptions.ClaimsIdentity
                    // .RoleClaimType is deliberately left alone: a cookie identity carries its
                    // roles under ClaimTypes.Role, IsInRole resolves per identity, and the two
                    // identities therefore satisfy the same RequireRole without sharing a type.
                    RoleClaimType = EnvanexClaimTypes.Role,
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };

                // Not part of TokenValidationParameters, so its position here does not matter.
                // Without it the handler rewrites "role" to the WS-Federation URI on the way in,
                // RoleClaimType above then matches nothing, and every role check fails.
                bearer.MapInboundClaims = false;
            });

        return services;
    }
}
