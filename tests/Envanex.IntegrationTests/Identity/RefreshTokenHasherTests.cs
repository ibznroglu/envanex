using System.Buffers.Text;
using Envanex.Infrastructure.Identity;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// No database and no collection: these run in memory. They live in this project only because it
/// is the one with <c>InternalsVisibleTo</c> from <c>Envanex.Infrastructure</c>.
/// </summary>
public sealed class RefreshTokenHasherTests
{
    [Fact]
    public void Hash_SameToken_ShouldProduceTheSameHash()
    {
        var token = RefreshTokenGenerator.CreateToken();

        RefreshTokenHasher.Hash(token).ShouldBe(RefreshTokenHasher.Hash(token));
    }

    [Fact]
    public void Hash_DifferentTokens_ShouldProduceDifferentHashes()
    {
        var first = RefreshTokenHasher.Hash("first-token");
        var second = RefreshTokenHasher.Hash("second-token");

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Hash_ShouldProduceExactly32Bytes()
    {
        // The column is binary(32). A different digest size would fail at insert time instead.
        RefreshTokenHasher.Hash(RefreshTokenGenerator.CreateToken()).Length.ShouldBe(32);
    }

    [Fact]
    public void Hash_NullToken_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => RefreshTokenHasher.Hash(null!));
    }

    [Fact]
    public void Matches_CorrectToken_ShouldReturnTrue()
    {
        var token = RefreshTokenGenerator.CreateToken();

        RefreshTokenHasher.Matches(token, RefreshTokenHasher.Hash(token)).ShouldBeTrue();
    }

    [Fact]
    public void Matches_WrongToken_ShouldReturnFalse()
    {
        var stored = RefreshTokenHasher.Hash(RefreshTokenGenerator.CreateToken());

        RefreshTokenHasher.Matches(RefreshTokenGenerator.CreateToken(), stored).ShouldBeFalse();
    }

    [Fact]
    public void Matches_StoredHashOfWrongLength_ShouldReturnFalse()
    {
        // A truncated or corrupted stored value must be a plain "no", not an exception that would
        // turn a refresh attempt into a 500.
        var token = RefreshTokenGenerator.CreateToken();
        var truncated = RefreshTokenHasher.Hash(token)[..16];

        RefreshTokenHasher.Matches(token, truncated).ShouldBeFalse();
    }

    [Fact]
    public void Matches_NullToken_ShouldThrowArgumentNullException()
    {
        var stored = RefreshTokenHasher.Hash(RefreshTokenGenerator.CreateToken());

        Should.Throw<ArgumentNullException>(() => RefreshTokenHasher.Matches(null!, stored));
    }

    [Fact]
    public void CreateToken_ShouldDecodeTo32Bytes()
    {
        Base64Url.DecodeFromChars(RefreshTokenGenerator.CreateToken()).Length
            .ShouldBe(RefreshTokenGenerator.TokenByteLength);
    }

    [Fact]
    public void CreateToken_ShouldBeUrlSafe()
    {
        // The token travels in a JSON body today and may travel in a header or a URL later.
        // '+', '/' and '=' are the three characters that would be re-encoded on the way.
        var token = RefreshTokenGenerator.CreateToken();

        token.ShouldNotContain("+");
        token.ShouldNotContain("/");
        token.ShouldNotContain("=");
    }

    [Fact]
    public void CreateToken_CalledOneThousandTimes_ShouldProduceOneThousandDistinctValues()
    {
        // Catches the classic defect of a shared or seeded generator producing a repeating value.
        // Against 256 bits of entropy a genuine collision here has no realistic probability.
        var tokens = Enumerable.Range(0, 1000).Select(_ => RefreshTokenGenerator.CreateToken());

        tokens.Distinct(StringComparer.Ordinal).Count().ShouldBe(1000);
    }
}
