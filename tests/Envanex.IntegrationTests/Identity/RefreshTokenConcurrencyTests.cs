using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;
using Envanex.Infrastructure;
using Envanex.Infrastructure.Identity;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit.Abstractions;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// Real races against the real container. Nothing here is orchestrated by a barrier, a hook or a
/// seam: no interleaving is forced, so every assertion has to hold under all of them. This class
/// does not guard the database-level invariants — the deterministic Phase 2 tests
/// <c>RefreshTokens_UpdatingARowLoadedBeforeAConcurrentUpdate_ShouldThrowDbUpdateConcurrencyException</c>
/// and <c>RefreshTokens_InsertingASecondLiveTokenForTheSameFamily_ShouldThrowUniqueViolation</c>
/// do that.
/// </summary>
/// <remarks>
/// Each concurrent task takes its own <see cref="IServiceScope"/>, and therefore its own
/// <c>EnvanexIdentityDbContext</c>, because a DbContext is not thread-safe. The
/// <see cref="FakeTimeProvider"/> is shared and is only read by a racer; it is advanced from the
/// test thread alone, before a race starts.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class RefreshTokenConcurrencyTests : IAsyncLifetime
{
    /// <summary>
    /// How many times each race is run. A single pass proves nothing about an interleaving nobody
    /// controls. Kept as a constant so the number is turned in one place.
    /// </summary>
    private const int ConcurrencyIterations = 20;

    private const string Email = "refresh-token-concurrency@envanex.test";
    private const string Password = "CorrectHorse1Battery";

    private static readonly DateTimeOffset FakeNow = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly FakeTimeProvider _time = new(FakeNow);
    private ServiceProvider _provider = null!;
    private Guid _userId;

    public RefreshTokenConcurrencyTests(SqlServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetIdentityAsync();

        _provider = BuildProvider();
        _userId = await IdentitySeeder.CreateUserAsync(_provider, Email, Password);
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task RotateAsync_TwoConcurrentRotationsOfTheSameToken_ShouldLetExactlyOneSucceed()
    {
        for (var iteration = 0; iteration < ConcurrencyIterations; iteration++)
        {
            var issued = await IssueAsync();

            var results = await RaceTwoRotationsAsync(issued.Token);

            // Holds under every interleaving: whoever arrives second either loses the RowVersion
            // predicate or reads an already-rotated parent, and both of those are failures.
            results.Count(result => result.IsSuccess)
                .ShouldBe(1, $"Iteration {iteration} returned: {Describe(results)}");
        }
    }

    [Fact]
    public async Task RotateAsync_TwoConcurrentRotationsOfTheSameToken_ShouldNeverLeaveTwoLiveRowsInTheFamily()
    {
        for (var iteration = 0; iteration < ConcurrencyIterations; iteration++)
        {
            var issued = await IssueAsync();
            var familyId = await GetFamilyIdAsync(issued.Token);

            await RaceTwoRotationsAsync(issued.Token);

            var liveRows = await LoadLiveRowsAsync(familyId);

            // The family invariant, independent of who won: zero when the loser detected reuse and
            // revoked everything, one otherwise.
            liveRows.Count.ShouldBeLessThanOrEqualTo(1, $"Iteration {iteration} left {liveRows.Count} live rows.");
        }
    }

    [Fact]
    public async Task RotateAsync_TwoConcurrentRotationsOfTheSameToken_ShouldEndInOneOfTheTwoAcceptedStates()
    {
        // Which branch an iteration takes is decided by an interleaving nobody controls: the
        // loser-exit branch only runs when the two racers genuinely overlap. If every iteration
        // serialized, only the reuse branch would execute and this test would be green without
        // ever asserting the loser-exit post-state. The split is reported, never asserted on —
        // whether the iteration count is high enough is a call for the human, not this test.
        var loserExitIterations = 0;
        var reuseIterations = 0;

        for (var iteration = 0; iteration < ConcurrencyIterations; iteration++)
        {
            var issued = await IssueAsync();
            var parentHash = RefreshTokenHasher.Hash(issued.Token);
            var familyId = await GetFamilyIdAsync(issued.Token);

            // An escaped exception fails the iteration here, which is one of the outcomes this
            // test rejects: removing the loser-exit catch makes a DbUpdateConcurrencyException
            // surface exactly at this line.
            var results = await RaceTwoRotationsAsync(issued.Token);

            results.Count(result => result.IsSuccess)
                .ShouldBe(1, $"Iteration {iteration} returned: {Describe(results)}");

            var loser = results.Single(result => result.IsFailure);
            var rows = await LoadFamilyAsync(familyId);
            var parent = rows.Single(row => row.TokenHash.SequenceEqual(parentHash));
            var liveRows = rows.Where(row => row.RotatedAt is null && row.RevokedAt is null).ToList();

            // Only the returned error code and the database post-state are asserted. Which
            // exception type fired is exactly the distinction the loser exit collapses, so an
            // assertion on it would re-introduce the dependency on EF's save ordering.
            if (loser.Error == AuthErrors.InvalidRefreshToken)
            {
                loserExitIterations++;
                liveRows.Count.ShouldBe(1, $"Iteration {iteration}: the loser exit left {liveRows.Count} live rows.");
                parent.RotatedAt.ShouldNotBeNull();
                parent.ReplacedByTokenId.ShouldBe(liveRows[0].Id);
                rows.ShouldAllBe(row => row.RevokedAt == null);
            }
            else if (loser.Error == AuthErrors.RefreshTokenReused)
            {
                reuseIterations++;
                liveRows.ShouldBeEmpty($"Iteration {iteration}: reuse detection left a live row.");
                rows.ShouldAllBe(row => row.RevokedReason == "Reuse");
                parent.RotatedAt.ShouldNotBeNull();
            }
            else
            {
                Assert.Fail($"Iteration {iteration} ended in an unaccepted state: {Describe(results)}");
            }
        }

        _output.WriteLine(
            $"Branch split over {ConcurrencyIterations} iterations: " +
            $"{loserExitIterations} loser exit (Auth.InvalidRefreshToken), " +
            $"{reuseIterations} serialized (Auth.RefreshTokenReused).");
    }

    [Fact]
    public async Task RotateAsync_TwoConcurrentRotationsOfAnAlreadyRotatedToken_ShouldBothReturnRefreshTokenReused()
    {
        var codes = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var iteration = 0; iteration < ConcurrencyIterations; iteration++)
        {
            var issued = await IssueAsync();

            var setup = await RotateInOwnScopeAsync(issued.Token);
            setup.IsSuccess.ShouldBeTrue($"Iteration {iteration}: the setup rotation failed.");

            // Three concurrent refreshes of one token put two of them here. Both racers read an
            // already-rotated parent, so both reach the reuse branch, load the same live rows and
            // try to stamp them; the second save matches zero rows. Without the bounded retry
            // around that revocation, a DbUpdateConcurrencyException leaves a method whose whole
            // contract is Result — a 500 where the design promises a 401, after "reuse detected"
            // has already been logged.
            var results = Array.Empty<Result<RotatedRefreshToken>>();

            await Should.NotThrowAsync(async () => results = await RaceTwoRotationsAsync(issued.Token));

            results.Length.ShouldBe(2, $"Iteration {iteration}: the race did not return two results.");

            foreach (var result in results)
            {
                result.IsFailure.ShouldBeTrue($"Iteration {iteration} returned: {Describe(results)}");
                result.Error.Code.ShouldBe(
                    AuthErrors.RefreshTokenReused.Code,
                    $"Iteration {iteration} returned: {Describe(results)}");

                Count(codes, result.Error.Code);
            }
        }

        _output.WriteLine($"Replay race codes over {ConcurrencyIterations} iterations: {Describe(codes)}");
    }

    [Fact]
    public async Task RotateAsync_TwoConcurrentRotationsOfATokenPastItsIdleWindow_ShouldBothReturnRefreshTokenExpired()
    {
        var codes = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var iteration = 0; iteration < ConcurrencyIterations; iteration++)
        {
            var issued = await IssueAsync();

            // One client with two tabs, both refreshing the same stored token after the idle
            // window closed. The clock moves on the test thread only, never inside a racer.
            _time.Advance(TimeSpan.FromDays(8));

            var results = Array.Empty<Result<RotatedRefreshToken>>();

            await Should.NotThrowAsync(async () => results = await RaceTwoRotationsAsync(issued.Token));

            results.Length.ShouldBe(2, $"Iteration {iteration}: the race did not return two results.");

            foreach (var result in results)
            {
                result.IsFailure.ShouldBeTrue($"Iteration {iteration} returned: {Describe(results)}");
                result.Error.Code.ShouldBe(
                    AuthErrors.RefreshTokenExpired.Code,
                    $"Iteration {iteration} returned: {Describe(results)}");

                Count(codes, result.Error.Code);
            }
        }

        _output.WriteLine($"Expiry race codes over {ConcurrencyIterations} iterations: {Describe(codes)}");
    }

    [Fact]
    public async Task RevokeFamilyAsync_RacingARotation_ShouldLeaveNoLiveRowInTheFamily()
    {
        for (var iteration = 0; iteration < ConcurrencyIterations; iteration++)
        {
            var issued = await IssueAsync();
            var familyId = await GetFamilyIdAsync(issued.Token);

            var rotateTask = RotateInOwnScopeAsync(issued.Token);
            var revokeTask = RevokeInOwnScopeAsync(issued.Token);
            await Task.WhenAll(rotateTask, revokeTask);

            var liveRows = await LoadLiveRowsAsync(familyId);

            // This is what the bounded retry loop exists for: a rotation committing between the
            // revocation's read and its write produces a child the first pass never saw.
            liveRows.ShouldBeEmpty(
                $"Iteration {iteration}: logout left {liveRows.Count} live rows in the family.");
        }
    }

    [Fact]
    public async Task RevokeFamilyAsync_RacingARotation_ShouldNotThrow()
    {
        for (var iteration = 0; iteration < ConcurrencyIterations; iteration++)
        {
            var issued = await IssueAsync();

            Result<RotatedRefreshToken>? rotated = null;
            Result? revoked = null;

            await Should.NotThrowAsync(async () =>
            {
                var rotateTask = RotateInOwnScopeAsync(issued.Token);
                var revokeTask = RevokeInOwnScopeAsync(issued.Token);
                await Task.WhenAll(rotateTask, revokeTask);

                rotated = await rotateTask;
                revoked = await revokeTask;
            });

            // Both sides answer with a Result; neither leaks an infrastructure exception.
            rotated.ShouldNotBeNull($"Iteration {iteration}: rotation returned nothing.");
            revoked.ShouldNotBeNull($"Iteration {iteration}: revocation returned nothing.");
        }
    }

    private async Task<Result<RotatedRefreshToken>[]> RaceTwoRotationsAsync(string token)
    {
        var first = RotateInOwnScopeAsync(token);
        var second = RotateInOwnScopeAsync(token);

        return await Task.WhenAll(first, second);
    }

    private static void Count(Dictionary<string, int> codes, string code)
        => codes[code] = codes.TryGetValue(code, out var seen) ? seen + 1 : 1;

    private static string Describe(Dictionary<string, int> codes)
        => string.Join(", ", codes.Select(entry => $"{entry.Key}={entry.Value}"));

    private static string Describe(IEnumerable<Result<RotatedRefreshToken>> results)
        => string.Join(
            ", ",
            results.Select(result => result.IsSuccess ? "success" : result.Error.Code));

    private async Task<Result<RotatedRefreshToken>> RotateInOwnScopeAsync(string token)
    {
        // Task.Run so the two racers really are on different threads rather than interleaved by
        // the await points of a single one.
        return await Task.Run(async () =>
        {
            using var scope = _provider.CreateScope();

            return await scope.ServiceProvider
                .GetRequiredService<IRefreshTokenService>()
                .RotateAsync(token);
        });
    }

    private async Task<Result> RevokeInOwnScopeAsync(string token)
    {
        return await Task.Run(async () =>
        {
            using var scope = _provider.CreateScope();

            return await scope.ServiceProvider
                .GetRequiredService<IRefreshTokenService>()
                .RevokeFamilyAsync(token);
        });
    }

    private async Task<IssuedRefreshToken> IssueAsync()
    {
        using var scope = _provider.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IRefreshTokenService>()
            .IssueAsync(_userId);

        result.IsSuccess.ShouldBeTrue();

        return result.Value;
    }

    private async Task<Guid> GetFamilyIdAsync(string token)
    {
        var hash = RefreshTokenHasher.Hash(token);

        await using var context = _fixture.CreateIdentityDbContext();

        return await context.RefreshTokens
            .Where(row => row.TokenHash == hash)
            .Select(row => row.FamilyId)
            .SingleAsync();
    }

    private async Task<List<RefreshToken>> LoadFamilyAsync(Guid familyId)
    {
        await using var context = _fixture.CreateIdentityDbContext();

        return await context.RefreshTokens
            .AsNoTracking()
            .Where(row => row.FamilyId == familyId)
            .ToListAsync();
    }

    private async Task<List<RefreshToken>> LoadLiveRowsAsync(Guid familyId)
    {
        var rows = await LoadFamilyAsync(familyId);

        return rows.Where(row => row.RotatedAt is null && row.RevokedAt is null).ToList();
    }

    private ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EnvanexDb"] = _fixture.ConnectionString,
                ["Jwt:Issuer"] = "https://envanex.local",
                ["Jwt:Audience"] = "envanex-api",
                ["Jwt:SigningKey"] = EnvanexWebApplicationFactory.TestSigningKey,
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenIdleDays"] = "7",
                ["Jwt:RefreshTokenAbsoluteDays"] = "30",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_time);
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
