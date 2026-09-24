using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;
using Envanex.Infrastructure;
using Envanex.Infrastructure.Identity;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// Issue, rotate, replay and revoke against the real table, with a fake clock so that "eight days
/// later" costs nothing.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RefreshTokenServiceTests : IAsyncLifetime
{
    private const string Email = "refresh-token-service@envanex.test";
    private const string Password = "CorrectHorse1Battery";

    private static readonly DateTimeOffset FakeNow = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly FakeTimeProvider _time = new(FakeNow);
    private ServiceProvider _provider = null!;
    private Guid _userId;

    public RefreshTokenServiceTests(SqlServerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetIdentityAsync();

        _provider = BuildProvider();
        _userId = await IdentitySeeder.CreateUserAsync(_provider, Email, Password);
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task IssueAsync_ShouldPersistOnlyTheHashAndNeverThePlaintextToken()
    {
        var issued = await IssueAsync();

        await using var context = _fixture.CreateIdentityDbContext();
        var row = await context.RefreshTokens.SingleAsync();

        row.TokenHash.ShouldBe(RefreshTokenHasher.Hash(issued.Token));

        // Not only "the hash column is a hash": nothing in the row may carry the plaintext.
        var rendered = await ReadRowAsTextAsync(row.Id);
        rendered.ShouldNotContain(issued.Token, Case.Insensitive);
    }

    [Fact]
    public async Task IssueAsync_ShouldSetExpiresAtToSevenDaysAfterFakeNow()
    {
        var issued = await IssueAsync();

        issued.ExpiresAt.ShouldBe(FakeNow.AddDays(7));

        await using var context = _fixture.CreateIdentityDbContext();
        var row = await context.RefreshTokens.SingleAsync();

        row.ExpiresAt.ShouldBe(FakeNow.AddDays(7));
    }

    [Fact]
    public async Task IssueAsync_ShouldSetFamilyExpiresAtToThirtyDaysAfterFakeNow()
    {
        await IssueAsync();

        await using var context = _fixture.CreateIdentityDbContext();
        var row = await context.RefreshTokens.SingleAsync();

        row.FamilyExpiresAt.ShouldBe(FakeNow.AddDays(30));
    }

    [Fact]
    public async Task IssueAsync_CalledTwiceForOneUser_ShouldProduceTwoIndependentFamilies()
    {
        await IssueAsync();
        await IssueAsync();

        await using var context = _fixture.CreateIdentityDbContext();
        var familyIds = await context.RefreshTokens.Select(token => token.FamilyId).ToListAsync();

        familyIds.Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task RotateAsync_ValidToken_ShouldReturnADifferentTokenString()
    {
        var issued = await IssueAsync();

        var rotated = await RotateAsync(issued.Token);

        // Also the canary for EF's save ordering: if the child INSERT reached the server before
        // the parent UPDATE, IX_RefreshTokens_FamilyId_Live would reject every rotation and this
        // would be red for every token, not only for a racing one.
        rotated.IsSuccess.ShouldBeTrue($"Rotation failed with: {rotated.Error.Code}");
        rotated.Value.Token.ShouldNotBe(issued.Token);
    }

    [Fact]
    public async Task RotateAsync_ValidToken_ShouldStampParentRotatedAtAndReplacedByTokenId()
    {
        var issued = await IssueAsync();
        _time.Advance(TimeSpan.FromHours(1));

        await RotateAsync(issued.Token);

        await using var context = _fixture.CreateIdentityDbContext();
        var rows = await context.RefreshTokens.ToListAsync();
        var parent = rows.Single(row => row.TokenHash.SequenceEqual(RefreshTokenHasher.Hash(issued.Token)));
        var child = rows.Single(row => row.Id != parent.Id);

        parent.RotatedAt.ShouldBe(FakeNow.AddHours(1));
        parent.ReplacedByTokenId.ShouldBe(child.Id);
    }

    [Fact]
    public async Task RotateAsync_ValidToken_ShouldNotDeleteTheConsumedRow()
    {
        var issued = await IssueAsync();

        await RotateAsync(issued.Token);

        await using var context = _fixture.CreateIdentityDbContext();
        var rows = await context.RefreshTokens.ToListAsync();

        // Deleting the predecessor is the documented way reuse detection dies after one
        // generation: a replayed token would then match nothing.
        rows.Count.ShouldBe(2);
        rows.ShouldContain(row => row.TokenHash.SequenceEqual(RefreshTokenHasher.Hash(issued.Token)));
    }

    [Fact]
    public async Task RotateAsync_ValidToken_ShouldKeepTheSameFamilyId()
    {
        var issued = await IssueAsync();

        await RotateAsync(issued.Token);

        await using var context = _fixture.CreateIdentityDbContext();
        var familyIds = await context.RefreshTokens.Select(token => token.FamilyId).Distinct().ToListAsync();

        familyIds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RotateAsync_ValidToken_ShouldCarryFamilyExpiresAtForwardUnchanged()
    {
        var issued = await IssueAsync();
        _time.Advance(TimeSpan.FromDays(3));

        await RotateAsync(issued.Token);

        await using var context = _fixture.CreateIdentityDbContext();
        var familyExpiry = await context.RefreshTokens.Select(token => token.FamilyExpiresAt).Distinct().ToListAsync();

        // The absolute cap is the whole point: a rotation that extended it would make the family
        // immortal one refresh at a time.
        familyExpiry.ShouldBe([FakeNow.AddDays(30)]);
    }

    [Fact]
    public async Task RotateAsync_ValidToken_ShouldResetTheIdleWindowToSevenDays()
    {
        var issued = await IssueAsync();
        _time.Advance(TimeSpan.FromDays(3));

        var rotated = await RotateAsync(issued.Token);

        rotated.Value.ExpiresAt.ShouldBe(FakeNow.AddDays(10));
    }

    [Fact]
    public async Task RotateAsync_ValidToken_ShouldReturnTheOwningUser()
    {
        var issued = await IssueAsync();

        var rotated = await RotateAsync(issued.Token);

        rotated.Value.User.Id.ShouldBe(_userId);
        rotated.Value.User.Email.ShouldBe(Email);
    }

    [Fact]
    public async Task RotateAsync_ForAUserInTheAdministratorRole_ShouldCarryThatRoleIntoTheRotatedResult()
    {
        await IdentityRoleSeeder.EnsureRolesAsync(_provider);
        await AddToRoleAsync(EnvanexRoles.Administrator);

        var issued = await IssueAsync();

        var rotated = await RotateAsync(issued.Token);

        // The rotated result is what the access token issuer reads on a refresh. Drop the role
        // here and the refreshed token silently loses it, so the caller starts collecting 403s
        // fifteen minutes after a login that every other test in this repository saw succeed.
        rotated.IsSuccess.ShouldBeTrue($"Rotation failed with: {rotated.Error.Code}");
        rotated.Value.User.Roles.ShouldBe([EnvanexRoles.Administrator]);
    }

    private async Task AddToRoleAsync(string role)
    {
        using var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<EnvanexUser>>();

        var user = await userManager.FindByIdAsync(_userId.ToString());
        user.ShouldNotBeNull();

        var result = await userManager.AddToRoleAsync(user, role);

        result.Succeeded.ShouldBeTrue(
            $"Failed to add the user to '{role}': {string.Join(", ", result.Errors.Select(error => error.Code))}");
    }

    [Fact]
    public async Task RotateAsync_UnknownToken_ShouldReturnInvalidRefreshToken()
    {
        var result = await RotateAsync(RefreshTokenGenerator.CreateToken());

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task RotateAsync_BlankToken_ShouldReturnInvalidRefreshToken()
    {
        var result = await RotateAsync("   ");

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task RotateAsync_NullToken_ShouldThrowArgumentNullException()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        await Should.ThrowAsync<ArgumentNullException>(() => service.RotateAsync(null!));
    }

    [Fact]
    public async Task RotateAsync_ReplayOfGenerationOneAfterASingleRotation_ShouldReturnRefreshTokenReused()
    {
        var issued = await IssueAsync();
        await RotateAsync(issued.Token);

        var replay = await RotateAsync(issued.Token);

        replay.Error.ShouldBe(AuthErrors.RefreshTokenReused);
    }

    [Fact]
    public async Task RotateAsync_ReplayOfGenerationOneAfterThreeRotations_ShouldReturnRefreshTokenReusedAndRevokeTheWholeFamilyAndRejectTheLiveToken()
    {
        var generationOne = await IssueAsync();

        var second = await RotateAsync(generationOne.Token);
        second.IsSuccess.ShouldBeTrue();
        var third = await RotateAsync(second.Value.Token);
        third.IsSuccess.ShouldBeTrue();
        var fourth = await RotateAsync(third.Value.Token);
        fourth.IsSuccess.ShouldBeTrue();

        var replay = await RotateAsync(generationOne.Token);
        replay.Error.ShouldBe(AuthErrors.RefreshTokenReused);

        await using (var context = _fixture.CreateIdentityDbContext())
        {
            var rows = await context.RefreshTokens.ToListAsync();

            rows.Count.ShouldBe(4);
            rows.ShouldAllBe(row => row.RevokedAt != null);
            rows.ShouldAllBe(row => row.RevokedReason == "Reuse");
        }

        // RFC 9700 section 4.14.2: a replay ends the grant, not only the replayed token.
        var afterDetection = await RotateAsync(fourth.Value.Token);
        afterDetection.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task RotateAsync_AfterReuseDetection_ShouldNotTouchTheUsersOtherFamilies()
    {
        var compromised = await IssueAsync();
        var untouched = await IssueAsync();
        await RotateAsync(compromised.Token);

        await RotateAsync(compromised.Token);

        var stillWorks = await RotateAsync(untouched.Token);

        stillWorks.IsSuccess.ShouldBeTrue($"The second family was revoked too: {stillWorks.Error.Code}");
    }

    [Fact]
    public async Task RotateAsync_TokenPastTheIdleWindow_ShouldReturnRefreshTokenExpired()
    {
        var issued = await IssueAsync();
        _time.Advance(TimeSpan.FromDays(8));

        var result = await RotateAsync(issued.Token);

        result.Error.ShouldBe(AuthErrors.RefreshTokenExpired);
    }

    [Fact]
    public async Task RotateAsync_TokenRotatedEveryFiveDaysButPastTheThirtyDayCap_ShouldReturnRefreshTokenExpired()
    {
        var current = (await IssueAsync()).Token;

        // Five rotations at five-day intervals: the idle window is renewed every time and is never
        // the reason the last attempt fails.
        foreach (var _ in Enumerable.Range(0, 5))
        {
            _time.Advance(TimeSpan.FromDays(5));
            var rotated = await RotateAsync(current);
            rotated.IsSuccess.ShouldBeTrue($"Rotation within the idle window failed: {rotated.Error.Code}");
            current = rotated.Value.Token;
        }

        _time.Advance(TimeSpan.FromDays(6));

        var result = await RotateAsync(current);

        result.Error.ShouldBe(AuthErrors.RefreshTokenExpired);
    }

    [Fact]
    public async Task RotateAsync_ExpiredToken_ShouldRevokeTheFamily()
    {
        var issued = await IssueAsync();
        _time.Advance(TimeSpan.FromDays(8));

        await RotateAsync(issued.Token);

        await using var context = _fixture.CreateIdentityDbContext();
        var rows = await context.RefreshTokens.ToListAsync();

        rows.ShouldAllBe(row => row.RevokedReason == "Expired");
    }

    [Fact]
    public async Task RotateAsync_RevokedToken_ShouldReturnInvalidRefreshToken()
    {
        var issued = await IssueAsync();

        // Revoked directly on the row, so this covers the RevokedAt check itself rather than
        // whatever RevokeFamilyAsync happens to do.
        await using (var context = _fixture.CreateIdentityDbContext())
        {
            var row = await context.RefreshTokens.SingleAsync();
            row.Revoke(FakeNow, RefreshTokenRevocationReason.Logout);
            await context.SaveChangesAsync();
        }

        var result = await RotateAsync(issued.Token);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task RevokeFamilyAsync_ValidToken_ShouldRevokeEveryLiveRowInThatFamily()
    {
        var issued = await IssueAsync();
        var rotated = await RotateAsync(issued.Token);

        var result = await RevokeFamilyAsync(rotated.Value.Token);

        result.IsSuccess.ShouldBeTrue();

        await using var context = _fixture.CreateIdentityDbContext();
        var rows = await context.RefreshTokens.ToListAsync();

        // Both the consumed parent and the live child: "live" for revocation means RevokedAt IS
        // NULL, which a rotated-but-unrevoked row still satisfies.
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(row => row.RevokedReason == "Logout");
    }

    [Fact]
    public async Task RevokeFamilyAsync_ShouldNotTouchOtherFamiliesOfTheSameUser()
    {
        var loggedOut = await IssueAsync();
        var otherDevice = await IssueAsync();

        await RevokeFamilyAsync(loggedOut.Token);

        // "Log out of this device" is what the endpoint name promises; ending every session for a
        // user is a separate feature.
        var stillWorks = await RotateAsync(otherDevice.Token);

        stillWorks.IsSuccess.ShouldBeTrue($"The other family was revoked too: {stillWorks.Error.Code}");
    }

    [Fact]
    public async Task RevokeFamilyAsync_UnknownToken_ShouldReturnSuccess()
    {
        // Idempotent on purpose: a failure here would make logout an oracle for which tokens exist.
        var result = await RevokeFamilyAsync(RefreshTokenGenerator.CreateToken());

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RevokeFamilyAsync_CalledTwice_ShouldReturnSuccessBothTimes()
    {
        var issued = await IssueAsync();

        var first = await RevokeFamilyAsync(issued.Token);
        var second = await RevokeFamilyAsync(issued.Token);

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RotateAsync_AfterRevokeFamily_ShouldReturnInvalidRefreshToken()
    {
        var issued = await IssueAsync();
        await RevokeFamilyAsync(issued.Token);

        var result = await RotateAsync(issued.Token);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
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

    private async Task<Result<RotatedRefreshToken>> RotateAsync(string token)
    {
        using var scope = _provider.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IRefreshTokenService>()
            .RotateAsync(token);
    }

    private async Task<Result> RevokeFamilyAsync(string token)
    {
        using var scope = _provider.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IRefreshTokenService>()
            .RevokeFamilyAsync(token);
    }

    private async Task<string> ReadRowAsTextAsync(Guid id)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            SELECT CONCAT(
                CAST(Id AS nvarchar(50)), '|',
                CAST(UserId AS nvarchar(50)), '|',
                CAST(FamilyId AS nvarchar(50)), '|',
                CONVERT(nvarchar(200), TokenHash, 2), '|',
                ISNULL(RevokedReason, ''))
            FROM auth.RefreshTokens
            WHERE Id = @id
            """,
            connection);

        command.Parameters.AddWithValue("@id", id);

        return (string)(await command.ExecuteScalarAsync())!;
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
