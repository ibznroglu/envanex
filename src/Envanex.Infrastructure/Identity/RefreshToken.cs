namespace Envanex.Infrastructure.Identity;

/// <summary>
/// One issued refresh token. Rows are stamped, never deleted: a replayed token of any generation
/// must still resolve to its family, so rotation marks the consumed row with
/// <see cref="RotatedAt"/> and inserts a child rather than updating or removing the predecessor.
/// </summary>
/// <remarks>
/// This is a persistence entity of the Identity context, not a domain aggregate. It still owns its
/// invariants — every setter is private and every state change goes through a method here — so the
/// expiry shape (an absolute family cap combined with a shorter idle window) cannot be recomputed
/// differently by two callers.
/// </remarks>
internal sealed class RefreshToken
{
    // EF materializes through this constructor. TokenHash is assigned by the factory methods and
    // by EF when it reads the column; it is never legitimately null on a materialized instance.
    private RefreshToken() => TokenHash = null!;

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>
    /// The grant this token belongs to. Every token produced by rotating an ancestor carries the
    /// same value, so a replay revokes the whole family rather than only the token presented.
    /// </summary>
    public Guid FamilyId { get; private set; }

    /// <summary>SHA-256 of the token material. The token itself is never stored.</summary>
    public byte[] TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The idle window: recomputed as <c>now + idleWindow</c> on every rotation.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// The absolute cap of the family, copied forward unchanged by every rotation so that checking
    /// a token stays a single-row read.
    /// </summary>
    public DateTimeOffset FamilyExpiresAt { get; private set; }

    public DateTimeOffset? RotatedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    /// <summary>
    /// Starts a new family: a new <see cref="FamilyId"/>, an idle window from <paramref name="now"/>
    /// and an absolute cap from <paramref name="now"/> that no rotation may extend.
    /// </summary>
    public static RefreshToken CreateRoot(
        Guid userId,
        byte[] tokenHash,
        DateTimeOffset now,
        TimeSpan idleWindow,
        TimeSpan absoluteWindow)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = Guid.NewGuid(),
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now + idleWindow,
            FamilyExpiresAt = now + absoluteWindow,
        };
    }

    /// <summary>
    /// The successor of this token. It inherits the family and the absolute cap unchanged; only
    /// the idle window moves.
    /// </summary>
    public RefreshToken CreateChild(byte[] tokenHash, DateTimeOffset now, TimeSpan idleWindow)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            FamilyId = FamilyId,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now + idleWindow,
            FamilyExpiresAt = FamilyExpiresAt,
        };
    }

    /// <summary>
    /// Stamps this row as consumed by <paramref name="replacedByTokenId"/>. Rotating twice is a
    /// caller bug, not a business rule violation, so it throws: a second stamp would reassign
    /// <see cref="ReplacedByTokenId"/> and break the family chain a replay is traced along.
    /// </summary>
    /// <remarks>
    /// Independent of <see cref="Revoke"/>: a revoked row may still be stamped rotated, because
    /// this guard reads only <see cref="RotatedAt"/>.
    /// </remarks>
    public void MarkRotated(DateTimeOffset now, Guid replacedByTokenId)
    {
        if (RotatedAt is not null)
        {
            throw new InvalidOperationException(
                "This refresh token has already been rotated. " +
                "Rotating it again would reassign ReplacedByTokenId and break the family chain.");
        }

        RotatedAt = now;
        ReplacedByTokenId = replacedByTokenId;
    }

    /// <summary>
    /// Stamps this row revoked. Revoking twice is a caller bug, not a business rule violation, so
    /// it throws: a second stamp would overwrite <see cref="RevokedAt"/> and
    /// <see cref="RevokedReason"/>, letting a row revoked for <c>Reuse</c> be re-stamped
    /// <c>Logout</c> — the only signal reuse detection produces.
    /// </summary>
    /// <remarks>
    /// Independent of <see cref="MarkRotated"/>: revoking a rotated row is the normal path, since
    /// reuse detection revokes rows that already carry <see cref="RotatedAt"/>.
    /// </remarks>
    public void Revoke(DateTimeOffset now, RefreshTokenRevocationReason reason)
    {
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException(
                "This refresh token has already been revoked. " +
                "Revoking it again would overwrite RevokedAt and RevokedReason, so a row revoked " +
                "for reuse could be re-stamped as a logout.");
        }

        RevokedAt = now;
        RevokedReason = reason.ToString();
    }

    /// <summary>
    /// Exactly the filter of <c>IX_RefreshTokens_FamilyId_Live</c> plus the two time checks, so a
    /// token the application accepts is a token the database would still call live.
    /// </summary>
    public bool IsUsableAt(DateTimeOffset now)
        => RotatedAt is null
            && RevokedAt is null
            && now < ExpiresAt
            && now < FamilyExpiresAt;
}
