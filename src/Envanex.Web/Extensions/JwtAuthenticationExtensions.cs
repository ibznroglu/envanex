using System.Text;
using Envanex.Application.Authentication;
using Envanex.Web.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Envanex.Web.Extensions;

/// <summary>
/// Registers the host's three authentication schemes together: the selector every default points
/// at, the Blazor UI's cookie scheme, and the JWT bearer scheme that validates the access tokens
/// <c>JwtAccessTokenIssuer</c> signs. Web validates, Infrastructure signs, and both read the same
/// <see cref="JwtOptions"/> declaration.
/// </summary>
/// <remarks>
/// The file and the method keep their names because the call site already knows them; what they
/// register is no longer only the bearer scheme.
/// </remarks>
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

        // Resolved here rather than inside the AddCookie callback, which only runs when the options
        // are first built: a typo must stop the host coming up, not surface on the first sign-in.
        var cookieSecurePolicy = CookieSecurePolicyResolver.Resolve(
            configuration[CookieSecurePolicyResolver.SettingKey]);

        services.AddAuthentication(authentication =>
            {
                // Every default names the selector, so nothing authenticates, challenges or forbids
                // without first being routed to the right scheme. Signing in and out only ever
                // means the cookie: a bearer token is issued by the login endpoint, not signed in.
                authentication.DefaultScheme = EnvanexAuthenticationSchemes.Selector;
                authentication.DefaultAuthenticateScheme = EnvanexAuthenticationSchemes.Selector;
                authentication.DefaultChallengeScheme = EnvanexAuthenticationSchemes.Selector;
                authentication.DefaultForbidScheme = EnvanexAuthenticationSchemes.Selector;
                authentication.DefaultSignInScheme = EnvanexAuthenticationSchemes.Cookie;
                authentication.DefaultSignOutScheme = EnvanexAuthenticationSchemes.Cookie;
            })
            .AddPolicyScheme(EnvanexAuthenticationSchemes.Selector, displayName: null, selector =>
            {
                // Path prefix, never header presence. A browser holding a cookie and sending no
                // Authorization header to /api/* must get 401, not a 302 to the login page.
                //
                // The cookie is therefore never read on /api/*, which makes CSRF on the REST surface
                // structurally impossible rather than merely mitigated: the controllers carry no
                // antiforgery, and SameSite=Lax reduces that exposure without removing it.
                selector.ForwardDefaultSelector = context =>
                    context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : EnvanexAuthenticationSchemes.Cookie;
            })
            .AddCookie(EnvanexAuthenticationSchemes.Cookie, cookie =>
            {
                cookie.LoginPath = "/login";
                cookie.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
                cookie.SlidingExpiration = true;

                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;

                // Always unless configuration says SameAsRequest; see CookieSecurePolicyResolver.
                cookie.Cookie.SecurePolicy = cookieSecurePolicy;

                // AccessDeniedPath is deliberately not set: it would turn the forbid into a 302, and
                // the forbid has to be a 403 carrying a body. The events write that body.
                cookie.Events = EnvanexAuthenticationEvents.Cookie;
            })
            // No JwtBearerEvents.OnChallenge handler yet: no /api/* endpoint is [Authorize], so a
            // handler would be dead code. It ships with the first [Authorize] on a controller.
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
