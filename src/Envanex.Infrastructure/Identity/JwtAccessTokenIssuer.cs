using System.Text;
using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Envanex.Infrastructure.Identity;

/// <summary>
/// Signs the short-lived access token. Registered as a singleton so the
/// <see cref="SigningCredentials"/> — and the key derivation inside them — are built once rather
/// than on every login.
/// </summary>
internal sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SigningCredentials _signingCredentials;

    public JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _timeProvider = timeProvider;
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public IssuedAccessToken Issue(AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now + TimeSpan.FromMinutes(_options.AccessTokenMinutes);

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            // A fresh jti per token, so two tokens issued in the same second are still
            // distinguishable — which is what a future deny-list would be keyed on.
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Email] = user.Email,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
        };

        if (user.Roles.Count > 0)
        {
            // A string[] value serialises as a JSON array, which the handler reads back as one
            // claim per element. Written only when there is something to write: an empty array
            // would put a meaningless "role": [] in every token a role-less user holds.
            claims[EnvanexClaimTypes.Role] = user.Roles.ToArray();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials,
            Claims = claims,
        };

        return new IssuedAccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
