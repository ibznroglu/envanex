using Envanex.Infrastructure.Identity;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// No database. The names say what these prove: the factory methods take the windows as
/// arguments, so what is pinned here is <c>now + argument</c>, not the shipped 7-day and 30-day
/// numbers. Those are configuration and are pinned where they are configured.
/// </summary>
public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan IdleWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan AbsoluteWindow = TimeSpan.FromDays(30);

    private static RefreshToken CreateRoot(DateTimeOffset? now = null)
        => RefreshToken.CreateRoot(
            Guid.NewGuid(),
            RefreshTokenHasher.Hash(RefreshTokenGenerator.CreateToken()),
            now ?? Now,
            IdleWindow,
            AbsoluteWindow);

    private static RefreshToken CreateChild(RefreshToken parent, DateTimeOffset now)
        => parent.CreateChild(
            RefreshTokenHasher.Hash(RefreshTokenGenerator.CreateToken()),
            now,
            IdleWindow);

    [Fact]
    public void CreateRoot_ShouldSetExpiresAtToNowPlusTheIdleWindowArgument()
    {
        CreateRoot().ExpiresAt.ShouldBe(Now + IdleWindow);
    }

    [Fact]
    public void CreateRoot_ShouldSetFamilyExpiresAtToNowPlusTheAbsoluteWindowArgument()
    {
        CreateRoot().FamilyExpiresAt.ShouldBe(Now + AbsoluteWindow);
    }

    [Fact]
    public void CreateRoot_ShouldSetFamilyIdToANonEmptyGuid()
    {
        var first = CreateRoot();
        var second = CreateRoot();

        first.FamilyId.ShouldNotBe(Guid.Empty);
        second.FamilyId.ShouldNotBe(first.FamilyId);
    }

    [Fact]
    public void CreateChild_ShouldKeepTheParentFamilyId()
    {
        var parent = CreateRoot();

        CreateChild(parent, Now.AddDays(1)).FamilyId.ShouldBe(parent.FamilyId);
    }

    [Fact]
    public void CreateChild_ShouldCopyFamilyExpiresAtUnchanged()
    {
        // The absolute cap is the only thing a rotation cannot extend. If this ever moves, a
        // family becomes immortal as long as it is refreshed inside the idle window.
        var parent = CreateRoot();

        CreateChild(parent, Now.AddDays(1)).FamilyExpiresAt.ShouldBe(parent.FamilyExpiresAt);
    }

    [Fact]
    public void CreateChild_ShouldResetExpiresAtToNowPlusTheIdleWindowArgument()
    {
        var parent = CreateRoot();
        var rotatedAt = Now.AddDays(1);

        CreateChild(parent, rotatedAt).ExpiresAt.ShouldBe(rotatedAt + IdleWindow);
    }

    [Fact]
    public void CreateChild_ShouldKeepTheParentUserId()
    {
        var parent = CreateRoot();

        CreateChild(parent, Now.AddDays(1)).UserId.ShouldBe(parent.UserId);
    }

    [Fact]
    public void IsUsableAt_FreshToken_ShouldBeTrue()
    {
        CreateRoot().IsUsableAt(Now).ShouldBeTrue();
    }

    [Fact]
    public void IsUsableAt_RotatedToken_ShouldBeFalse()
    {
        // The row survives rotation so a replay still resolves to its family; what it must not do
        // is keep working.
        var token = CreateRoot();
        token.MarkRotated(Now.AddMinutes(1), Guid.NewGuid());

        token.IsUsableAt(Now.AddMinutes(2)).ShouldBeFalse();
    }

    [Fact]
    public void IsUsableAt_RevokedToken_ShouldBeFalse()
    {
        var token = CreateRoot();
        token.Revoke(Now.AddMinutes(1), RefreshTokenRevocationReason.Logout);

        token.IsUsableAt(Now.AddMinutes(2)).ShouldBeFalse();
    }

    [Fact]
    public void IsUsableAt_PastTheIdleWindow_ShouldBeFalse()
    {
        var token = CreateRoot();

        token.IsUsableAt(token.ExpiresAt).ShouldBeFalse();
    }

    [Fact]
    public void IsUsableAt_WithinTheIdleWindowButPastFamilyExpiry_ShouldBeFalse()
    {
        // The case the absolute cap exists for: a child rotated just before the cap has an idle
        // window reaching past it, and the cap must still win.
        var parent = CreateRoot();
        var child = CreateChild(parent, parent.FamilyExpiresAt.AddDays(-1));
        var afterTheCap = parent.FamilyExpiresAt.AddDays(1);

        child.ExpiresAt.ShouldBeGreaterThan(afterTheCap);
        child.IsUsableAt(afterTheCap).ShouldBeFalse();
    }

    [Fact]
    public void MarkRotated_ShouldSetRotatedAtAndReplacedByTokenId()
    {
        var token = CreateRoot();
        var successorId = Guid.NewGuid();
        var rotatedAt = Now.AddMinutes(5);

        token.MarkRotated(rotatedAt, successorId);

        token.RotatedAt.ShouldBe(rotatedAt);
        token.ReplacedByTokenId.ShouldBe(successorId);
    }

    [Fact]
    public void MarkRotated_OnAnAlreadyRotatedToken_ShouldThrowRatherThanReassignTheSuccessor()
    {
        // One row is consumed by exactly one successor. A second stamp would silently re-point
        // ReplacedByTokenId at another child and cut the chain a replay is traced along, so the
        // caller's bug is raised here instead of being persisted.
        var token = CreateRoot();
        var firstSuccessorId = Guid.NewGuid();
        token.MarkRotated(Now.AddMinutes(5), firstSuccessorId);

        Should.Throw<InvalidOperationException>(
            () => token.MarkRotated(Now.AddMinutes(6), Guid.NewGuid()));

        token.RotatedAt.ShouldBe(Now.AddMinutes(5));
        token.ReplacedByTokenId.ShouldBe(firstSuccessorId);
    }

    [Fact]
    public void Revoke_OnAnAlreadyRevokedToken_ShouldThrowRatherThanOverwriteTheFirstReason()
    {
        // Rotating and then revoking is the reuse-detection path and must stay legal, so the
        // arrange does exactly that. What must not happen is the second revocation: it would
        // re-stamp a row revoked for Reuse as a Logout and erase the only signal reuse detection
        // produces.
        var token = CreateRoot();
        token.MarkRotated(Now.AddMinutes(1), Guid.NewGuid());
        token.Revoke(Now.AddMinutes(5), RefreshTokenRevocationReason.Reuse);

        Should.Throw<InvalidOperationException>(
            () => token.Revoke(Now.AddMinutes(6), RefreshTokenRevocationReason.Logout));

        token.RevokedAt.ShouldBe(Now.AddMinutes(5));
        token.RevokedReason.ShouldBe("Reuse");
    }

    [Fact]
    public void Revoke_ShouldStoreTheReasonByName()
    {
        // The column is a name, not an ordinal: a row read directly in the database has to explain
        // itself, and inserting a reason into the enum must not reinterpret existing rows.
        var token = CreateRoot();
        var revokedAt = Now.AddMinutes(5);

        token.Revoke(revokedAt, RefreshTokenRevocationReason.Reuse);

        token.RevokedAt.ShouldBe(revokedAt);
        token.RevokedReason.ShouldBe("Reuse");
    }
}
