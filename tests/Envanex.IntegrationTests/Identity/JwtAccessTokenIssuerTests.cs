using System.Text;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// The access token, read back with the same library a resource server would use. No database:
/// this class lives in the integration test project only because that is the project
/// <c>Envanex.Infrastructure</c> makes its internals visible to.
/// </summary>
public sealed class JwtAccessTokenIssuerTests
{
    private const string Issuer = "https://envanex.local";
    private const string Audience = "envanex-api";
    private const string SigningKey = "envanex-access-token-test-signing-key-0123456789";
    private const string OtherSigningKey = "a-completely-different-key-0123456789abcdef";

    private static readonly AuthenticatedUser User =
        new(Guid.Parse("3f1b6a2c-7c2a-4f5d-9a11-9b5c1f2d3e40"), "issuer@envanex.test", "issuer@envanex.test");

    // Anchored to real time so that lifetime validation, which reads the system clock, is a real
    // check rather than one that has to be switched off.
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Issue_ShouldProduceATokenCarryingSubEmailAndJti()
    {
        var issued = CreateIssuer().Issue(User);

        var jwt = new JsonWebToken(issued.Token);

        jwt.GetPayloadValue<string>("sub").ShouldBe(User.Id.ToString());
        jwt.GetPayloadValue<string>("email").ShouldBe(User.Email);
        jwt.GetPayloadValue<string>("jti").ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Issue_ShouldSetExpToFifteenMinutesAfterFakeNow()
    {
        var issued = CreateIssuer().Issue(User);

        var jwt = new JsonWebToken(issued.Token);

        // exp is whole seconds since the epoch, so the comparison is made in that unit.
        jwt.GetPayloadValue<long>("exp").ShouldBe(_now.AddMinutes(15).ToUnixTimeSeconds());
        issued.ExpiresAt.ShouldBe(_now.AddMinutes(15));
    }

    [Fact]
    public void Issue_ShouldSetIssuerAndAudienceFromConfiguration()
    {
        var issued = CreateIssuer().Issue(User);

        var jwt = new JsonWebToken(issued.Token);

        jwt.Issuer.ShouldBe(Issuer);
        jwt.Audiences.ShouldBe([Audience]);
    }

    [Fact]
    public void Issue_CalledTwice_ShouldProduceDifferentJtiValues()
    {
        var issuer = CreateIssuer();

        var first = new JsonWebToken(issuer.Issue(User).Token);
        var second = new JsonWebToken(issuer.Issue(User).Token);

        first.GetPayloadValue<string>("jti").ShouldNotBe(second.GetPayloadValue<string>("jti"));
    }

    [Fact]
    public async Task Issue_ShouldValidateAgainstTheConfiguredSigningKey()
    {
        var issued = CreateIssuer().Issue(User);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.Token, ValidationParameters(SigningKey));

        result.IsValid.ShouldBeTrue($"Validation failed: {result.Exception?.Message}");
    }

    [Fact]
    public async Task Issue_ShouldFailValidationAgainstADifferentSigningKey()
    {
        var issued = CreateIssuer().Issue(User);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.Token, ValidationParameters(OtherSigningKey));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Issue_ShouldUseHs256()
    {
        var issued = CreateIssuer().Issue(User);

        new JsonWebToken(issued.Token).Alg.ShouldBe(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void Issue_NullUser_ShouldThrowArgumentNullException()
    {
        var issuer = CreateIssuer();

        Should.Throw<ArgumentNullException>(() => issuer.Issue(null!));
    }

    private static TokenValidationParameters ValidationParameters(string signingKey) => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,

        // No tolerance: a 15-minute access token with five minutes of default skew is a
        // 20-minute access token.
        ClockSkew = TimeSpan.Zero,
    };

    private JwtAccessTokenIssuer CreateIssuer()
    {
        var options = new JwtOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = SigningKey,
            AccessTokenMinutes = 15,
            RefreshTokenIdleDays = 7,
            RefreshTokenAbsoluteDays = 30,
        };

        return new JwtAccessTokenIssuer(Options.Create(options), new FakeTimeProvider(_now));
    }
}
