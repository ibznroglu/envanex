using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Envanex.Infrastructure.Identity;

/// <summary>
/// Issues, rotates and revokes opaque refresh tokens. Only the SHA-256 of the token material is
/// ever stored, and a consumed row is stamped rather than deleted so that replaying a token of any
/// generation still resolves to its family.
/// </summary>
internal sealed partial class RefreshTokenService : IRefreshTokenService
{
    /// <summary>
    /// How many times a family revocation may re-read and re-revoke a family before giving up. A
    /// rotation committing between the read and the write produces a live child the previous pass
    /// never saw, so one pass is not enough; each pass observes a strictly later generation, so a
    /// handful is.
    /// </summary>
    public const int RevocationRetryLimit = 3;

    private readonly EnvanexIdentityDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly JwtOptions _options;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        EnvanexIdentityDbContext context,
        TimeProvider timeProvider,
        IOptions<JwtOptions> options,
        ILogger<RefreshTokenService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _context = context;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private TimeSpan IdleWindow => TimeSpan.FromDays(_options.RefreshTokenIdleDays);

    private TimeSpan AbsoluteWindow => TimeSpan.FromDays(_options.RefreshTokenAbsoluteDays);

    public async Task<Result<IssuedRefreshToken>> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        var token = RefreshTokenGenerator.CreateToken();

        var root = RefreshToken.CreateRoot(
            userId,
            RefreshTokenHasher.Hash(token),
            now,
            IdleWindow,
            AbsoluteWindow);

        _context.RefreshTokens.Add(root);
        await _context.SaveChangesAsync(ct);

        // The plaintext leaves this method and is never written anywhere: the caller returns it to
        // the client, and the only durable trace of it is the hash just saved.
        return Result.Success(new IssuedRefreshToken(token, root.ExpiresAt));
    }

    public async Task<Result<RotatedRefreshToken>> RotateAsync(string presentedToken, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(presentedToken);

        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            return Result.Failure<RotatedRefreshToken>(AuthErrors.InvalidRefreshToken);
        }

        var now = _timeProvider.GetUtcNow();
        var hash = RefreshTokenHasher.Hash(presentedToken);

        // One round trip for the row and its owner: the owner is needed on the success path and
        // the join rides along on the same seek of IX_RefreshTokens_TokenHash.
        var match = await _context.RefreshTokens
            .Where(token => token.TokenHash == hash)
            .Join(
                _context.Users,
                token => token.UserId,
                user => user.Id,
                (token, user) => new { Token = token, User = user })
            .FirstOrDefaultAsync(ct);

        if (match is null)
        {
            return Result.Failure<RotatedRefreshToken>(AuthErrors.InvalidRefreshToken);
        }

        var parent = match.Token;

        // Redundant against the unique index that just matched, and kept anyway so that the
        // constant-time comparison is a property of code somebody can read rather than of a
        // query plan somebody has to infer.
        if (!RefreshTokenHasher.Matches(presentedToken, parent.TokenHash))
        {
            return Result.Failure<RotatedRefreshToken>(AuthErrors.InvalidRefreshToken);
        }

        if (parent.RevokedAt is not null)
        {
            return Result.Failure<RotatedRefreshToken>(AuthErrors.InvalidRefreshToken);
        }

        if (parent.RotatedAt is not null)
        {
            // Reuse. The grace period is zero in this PR, so a replay of any generation ends the
            // whole grant immediately — RFC 9700 section 4.14.2.
            LogReuseDetected(parent.FamilyId);

            // The retry, and therefore the loser exit, is what keeps two replays of the same token
            // from ending in a DbUpdateConcurrencyException: both racers load the same live rows
            // and only one of them can stamp them. Whether this racer revoked the family itself or
            // lost to the one that did, the answer it owes the caller is the same.
            _ = await RevokeLiveFamilyRowsWithRetryAsync(
                parent.FamilyId,
                now,
                RefreshTokenRevocationReason.Reuse,
                ct);

            return Result.Failure<RotatedRefreshToken>(AuthErrors.RefreshTokenReused);
        }

        if (now >= parent.ExpiresAt || now >= parent.FamilyExpiresAt)
        {
            // Same race, same reasoning: one client with two tabs refreshing the same stored token
            // past the idle window reaches this branch twice.
            _ = await RevokeLiveFamilyRowsWithRetryAsync(
                parent.FamilyId,
                now,
                RefreshTokenRevocationReason.Expired,
                ct);

            return Result.Failure<RotatedRefreshToken>(AuthErrors.RefreshTokenExpired);
        }

        var childToken = RefreshTokenGenerator.CreateToken();
        var child = parent.CreateChild(RefreshTokenHasher.Hash(childToken), now, IdleWindow);

        parent.MarkRotated(now, child.Id);
        _context.RefreshTokens.Add(child);

        try
        {
            // One SaveChangesAsync, no explicit transaction: a single save is already atomic, and
            // the parent's RowVersion predicate is what decides a race between two rotations.
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception exception) when (IsLostRotationRace(exception))
        {
            // One loser exit for both failures. They report the same fact — another rotation
            // consumed this token first — so nothing downstream benefits from telling them apart,
            // and collapsing them removes any dependency on EF Core's UPDATE/INSERT batch order.
            LogRotationRaceLost(parent.FamilyId);

            // The failed save left the child Added and the parent Modified. This context is the
            // scoped one UserManager also uses, so a later SaveChangesAsync in the same request
            // would re-attempt both writes; dropping them makes that impossible.
            _context.ChangeTracker.Clear();

            return Result.Failure<RotatedRefreshToken>(AuthErrors.InvalidRefreshToken);
        }

        var user = match.User;

        // The rotated result feeds the access token issuer exactly as a fresh login does, so it
        // has to carry the same roles. Without this a refreshed access token silently loses the
        // user's role and every policy starts answering 403 fifteen minutes after sign-in — a
        // failure no test of a fresh login can see.
        //
        // On the success path only: a rotation that fails above returns before this line and never
        // pays for the read.
        var roles = await _context.UserRoles
            .Where(userRole => userRole.UserId == user.Id)
            .Join(
                _context.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (_, role) => role.Name!)
            .ToListAsync(ct);

        return Result.Success(
            new RotatedRefreshToken(
                new AuthenticatedUser(user.Id, user.Email!, user.UserName!, roles),
                childToken,
                child.ExpiresAt));
    }

    public async Task<Result> RevokeFamilyAsync(string presentedToken, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(presentedToken);

        var now = _timeProvider.GetUtcNow();
        var hash = RefreshTokenHasher.Hash(presentedToken);

        // A token that matches nothing — including a blank one — returns success. Logout is
        // deliberately idempotent so that it cannot be used as an oracle for token existence.
        var familyId = await _context.RefreshTokens
            .Where(token => token.TokenHash == hash)
            .Select(token => (Guid?)token.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId is null)
        {
            return Result.Success();
        }

        var revoked = await RevokeLiveFamilyRowsWithRetryAsync(
            familyId.Value,
            now,
            RefreshTokenRevocationReason.Logout,
            ct);

        // A logout that cannot guarantee revocation must not report success.
        return revoked ? Result.Success() : Result.Failure(AuthErrors.InvalidRefreshToken);
    }

    /// <summary>
    /// Revokes every live row of a family, tolerating a concurrent writer. Returns
    /// <see langword="false"/> only when a live row is still there after
    /// <see cref="RevocationRetryLimit"/> attempts <em>and</em> a final re-read, which is logged at
    /// <c>Error</c> before the method returns. The verdict is always the database's, never this
    /// method's own write having failed.
    /// </summary>
    /// <remarks>
    /// Every caller reaches this method from a path whose contract is <c>Result</c>, so a
    /// <see cref="DbUpdateConcurrencyException"/> must never leave it: two callers revoking the
    /// same family both stamp the same rows, and the second save matches zero rows.
    /// </remarks>
    private async Task<bool> RevokeLiveFamilyRowsWithRetryAsync(
        Guid familyId,
        DateTimeOffset now,
        RefreshTokenRevocationReason reason,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= RevocationRetryLimit; attempt++)
        {
            try
            {
                await RevokeLiveFamilyRowsAsync(familyId, now, reason, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another writer moved a row between this pass's read and its write. Drop
                // everything tracked and look again; the next pass sees the generation this one
                // missed, or finds nothing left to revoke because the winner revoked it.
                _context.ChangeTracker.Clear();
                continue;
            }

            if (!await HasLiveFamilyRowAsync(familyId, ct))
            {
                return true;
            }

            _context.ChangeTracker.Clear();
        }

        // A concurrency failure on the final attempt skips the in-loop check above, so the loop
        // can end without this method ever having looked at the family after the writer that won
        // that race committed. That winner may have revoked what was left, in which case the
        // family is dead and there is nothing to alarm about: look once more before saying
        // otherwise, because the Error below is a line an operator gets paged on.
        if (!await HasLiveFamilyRowAsync(familyId, ct))
        {
            return true;
        }

        LogFamilyRevocationExhausted(familyId, RevocationRetryLimit);

        return false;
    }

    /// <summary>
    /// Whether any row of the family is still unrevoked. Read with no tracking: it decides control
    /// flow and must never resurrect entities the caller has just discarded.
    /// </summary>
    private Task<bool> HasLiveFamilyRowAsync(Guid familyId, CancellationToken ct)
        => _context.RefreshTokens
            .AsNoTracking()
            .AnyAsync(token => token.FamilyId == familyId && token.RevokedAt == null, ct);

    /// <summary>
    /// The family query: an equality seek on <c>IX_RefreshTokens_FamilyId</c> with the handful of
    /// matching rows filtered on <c>RevokedAt IS NULL</c>.
    /// </summary>
    private async Task RevokeLiveFamilyRowsAsync(
        Guid familyId,
        DateTimeOffset now,
        RefreshTokenRevocationReason reason,
        CancellationToken ct)
    {
        var liveRows = await _context.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var row in liveRows)
        {
            row.Revoke(now, reason);
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The two ways a rotation can lose. <see cref="DbUpdateConcurrencyException"/> means another
    /// rotation or a revocation moved the parent's <c>RowVersion</c> first. A 2601/2627 on
    /// <c>IX_RefreshTokens_FamilyId_Live</c> means the child insert found a live sibling; that is
    /// expected to be unreachable while the parent UPDATE precedes the child INSERT in the same
    /// save, and it is handled here regardless so a future change to save ordering cannot turn it
    /// into an unhandled exception.
    /// </summary>
    private static bool IsLostRotationRace(Exception exception)
        => exception is DbUpdateConcurrencyException
            || exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } };

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Refresh token reuse detected; revoking family {FamilyId}")]
    private partial void LogReuseDetected(Guid familyId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Rotation of refresh token family {FamilyId} lost to a concurrent writer")]
    private partial void LogRotationRaceLost(Guid familyId);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Failed to revoke refresh token family {FamilyId} after {Attempts} attempts")]
    private partial void LogFamilyRevocationExhausted(Guid familyId, int attempts);
}
