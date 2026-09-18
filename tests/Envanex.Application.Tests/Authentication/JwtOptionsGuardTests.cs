using Envanex.Application.Authentication;
using Shouldly;

namespace Envanex.Application.Tests.Authentication;

public class JwtOptionsGuardTests
{
    private const string ThirtyTwoByteKey = "0123456789abcdef0123456789abcdef";

    private static JwtOptions ValidOptions => new()
    {
        Issuer = "https://envanex.local",
        Audience = "envanex-api",
        SigningKey = ThirtyTwoByteKey,
        AccessTokenMinutes = 15,
        RefreshTokenIdleDays = 7,
        RefreshTokenAbsoluteDays = 30,
    };

    [Fact]
    public void ThrowIfInvalid_NullOptions_ShouldThrowArgumentNullException()
    {
        // A null options object is a caller bug, not a configuration mistake.
        Should.Throw<ArgumentNullException>(() => JwtOptionsGuard.ThrowIfInvalid(null!));
    }

    [Fact]
    public void ThrowIfInvalid_BlankSigningKey_ShouldThrowNamingTheUserSecretsCommand()
    {
        var options = ValidOptions;
        options.SigningKey = "   ";

        var exception = Should.Throw<InvalidOperationException>(() => JwtOptionsGuard.ThrowIfInvalid(options));

        exception.Message.ShouldContain("Jwt:SigningKey");
        exception.Message.ShouldContain("dotnet user-secrets set");
        exception.Message.ShouldContain("--project src/Envanex.Web");
    }

    [Fact]
    public void ThrowIfInvalid_SigningKeyShorterThan32Bytes_ShouldThrow()
    {
        var options = ValidOptions;
        options.SigningKey = ThirtyTwoByteKey[..31];

        var exception = Should.Throw<InvalidOperationException>(() => JwtOptionsGuard.ThrowIfInvalid(options));

        exception.Message.ShouldContain("Jwt:SigningKey");
        exception.Message.ShouldContain("32");
    }

    [Fact]
    public void ThrowIfInvalid_SigningKeyOfExactly32Bytes_ShouldNotThrow()
    {
        var options = ValidOptions;
        options.SigningKey = ThirtyTwoByteKey;

        Should.NotThrow(() => JwtOptionsGuard.ThrowIfInvalid(options));
    }

    [Fact]
    public void ThrowIfInvalid_BlankIssuer_ShouldThrow()
    {
        var options = ValidOptions;
        options.Issuer = "";

        var exception = Should.Throw<InvalidOperationException>(() => JwtOptionsGuard.ThrowIfInvalid(options));

        exception.Message.ShouldContain("Jwt:Issuer");
    }

    [Fact]
    public void ThrowIfInvalid_BlankAudience_ShouldThrow()
    {
        var options = ValidOptions;
        options.Audience = "";

        var exception = Should.Throw<InvalidOperationException>(() => JwtOptionsGuard.ThrowIfInvalid(options));

        exception.Message.ShouldContain("Jwt:Audience");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ThrowIfInvalid_AccessTokenMinutesZeroOrNegative_ShouldThrow(int minutes)
    {
        var options = ValidOptions;
        options.AccessTokenMinutes = minutes;

        var exception = Should.Throw<InvalidOperationException>(() => JwtOptionsGuard.ThrowIfInvalid(options));

        exception.Message.ShouldContain("Jwt:AccessTokenMinutes");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ThrowIfInvalid_RefreshTokenIdleDaysZeroOrNegative_ShouldThrow(int days)
    {
        var options = ValidOptions;
        options.RefreshTokenIdleDays = days;

        var exception = Should.Throw<InvalidOperationException>(() => JwtOptionsGuard.ThrowIfInvalid(options));

        exception.Message.ShouldContain("Jwt:RefreshTokenIdleDays");
        // "must be greater than zero" is what separates this from the ordering guard, whose
        // message names the same key.
        exception.Message.ShouldContain("must be greater than zero");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ThrowIfInvalid_RefreshTokenAbsoluteDaysZeroOrNegative_ShouldThrow(int days)
    {
        var options = ValidOptions;
        options.RefreshTokenAbsoluteDays = days;

        var exception = Should.Throw<InvalidOperationException>(() => JwtOptionsGuard.ThrowIfInvalid(options));

        exception.Message.ShouldContain("Jwt:RefreshTokenAbsoluteDays");
        // Without this line the ordering guard (idle 7 > absolute 0) satisfies the assertion above,
        // so the test would stay green with the positivity check deleted.
        exception.Message.ShouldContain("must be greater than zero");
    }

    [Fact]
    public void ThrowIfInvalid_RefreshTokenIdleDaysGreaterThanAbsoluteDays_ShouldThrow()
    {
        var options = ValidOptions;
        options.RefreshTokenIdleDays = 31;
        options.RefreshTokenAbsoluteDays = 30;

        var exception = Should.Throw<InvalidOperationException>(() => JwtOptionsGuard.ThrowIfInvalid(options));

        // An idle window longer than the absolute cap can never be reached.
        exception.Message.ShouldContain("Jwt:RefreshTokenIdleDays");
        exception.Message.ShouldContain("Jwt:RefreshTokenAbsoluteDays");
    }

    [Fact]
    public void ThrowIfInvalid_ValidOptions_ShouldNotThrow()
    {
        Should.NotThrow(() => JwtOptionsGuard.ThrowIfInvalid(ValidOptions));
    }
}
