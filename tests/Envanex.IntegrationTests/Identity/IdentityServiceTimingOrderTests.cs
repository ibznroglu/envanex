using Envanex.Application.Abstractions.Authentication;
using Envanex.Infrastructure;
using Envanex.Infrastructure.Identity;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// Decision 6 expressed as call counts and call order rather than as elapsed milliseconds. A
/// stopwatch assertion on a ~100 ms PBKDF2 step would be flaky on a loaded machine and would still
/// not say *why* the timing changed; <see cref="CountingUserManager"/> says exactly which calls the
/// service made and in what order.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class IdentityServiceTimingOrderTests : IAsyncLifetime
{
    private const string Email = "timing-order@envanex.test";
    private const string CorrectPassword = "CorrectHorse1Battery";
    private const string WrongPassword = "WrongHorse9Battery";

    private static readonly DateTimeOffset FakeNow = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private ServiceProvider _provider = null!;

    public IdentityServiceTimingOrderTests(SqlServerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetIdentityAsync();

        _provider = BuildProvider();
        await IdentitySeeder.CreateUserAsync(_provider, Email, CorrectPassword);
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task ValidateCredentialsAsync_LockedOutUser_ShouldStillCallCheckPasswordAsyncExactlyOnce()
    {
        await IdentitySeeder.LockOutAsync(_provider, Email);

        var manager = await ValidateAndCaptureAsync(Email, WrongPassword);

        // The core of Decision 6. Red here means a locked-out account answers without paying the
        // PBKDF2 cost, and that step change is what tells an attacker the lockout tripped.
        manager.CheckPasswordCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_LockedOutUser_ShouldCallCheckPasswordAsyncBeforeIsLockedOutAsync()
    {
        await IdentitySeeder.LockOutAsync(_provider, Email);

        var manager = await ValidateAndCaptureAsync(Email, WrongPassword);

        manager.CallLog.Take(2).ShouldBe(["CheckPasswordAsync", "IsLockedOutAsync"]);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_LockedOutUserWithTheCorrectPassword_ShouldStillCallCheckPasswordAsyncExactlyOnce()
    {
        await IdentitySeeder.LockOutAsync(_provider, Email);

        var manager = await ValidateAndCaptureAsync(Email, CorrectPassword);

        manager.CheckPasswordCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_LockedOutUser_ShouldNotCallAccessFailedAsync()
    {
        await IdentitySeeder.LockOutAsync(_provider, Email);

        var manager = await ValidateAndCaptureAsync(Email, WrongPassword);

        // Incrementing here would let an attacker extend a victim's lockout indefinitely.
        manager.AccessFailedCallCount.ShouldBe(0);

        // The role lookup belongs to the success path alone. Today every failure branch returns
        // above it, but that is code shape, and code shape disappears silently under refactoring:
        // a role read on this branch would be a round trip only locked-out attempts pay for, which
        // is exactly the timing channel Decision 6 exists to keep closed.
        manager.GetRolesCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_LockedOutUserWithTheCorrectPassword_ShouldNotCallResetAccessFailedCountAsync()
    {
        await IdentitySeeder.LockOutAsync(_provider, Email);

        var manager = await ValidateAndCaptureAsync(Email, CorrectPassword);

        // Resetting here would hand a locked-out account a way back in.
        manager.ResetAccessFailedCountCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WrongPassword_ShouldCallAccessFailedAsyncExactlyOnce()
    {
        var manager = await ValidateAndCaptureAsync(Email, WrongPassword);

        manager.AccessFailedCallCount.ShouldBe(1);

        // Same guarantee on the wrong-password branch: it must not pay for a role read either.
        manager.GetRolesCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_FifthWrongPassword_ShouldCallAccessFailedAsyncExactlyOnce()
    {
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await ValidateAndCaptureAsync(Email, WrongPassword);
        }

        var manager = await ValidateAndCaptureAsync(Email, WrongPassword);

        // The attempt a post-increment lockout re-check would have double-counted.
        manager.AccessFailedCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_CorrectPassword_ShouldNotCallAccessFailedAsync()
    {
        var manager = await ValidateAndCaptureAsync(Email, CorrectPassword);

        manager.AccessFailedCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_CorrectPassword_ShouldCallResetAccessFailedCountAsyncExactlyOnce()
    {
        var manager = await ValidateAndCaptureAsync(Email, CorrectPassword);

        manager.ResetAccessFailedCountCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_UnknownEmail_ShouldNotCallCheckPasswordAsync()
    {
        var manager = await ValidateAndCaptureAsync("nobody@envanex.test", CorrectPassword);

        // The deliberate, documented half of the timing channel: an unknown address answers
        // without verifying a hash. Closing it means inverting this test, not deleting it.
        manager.CheckPasswordCallCount.ShouldBe(0);

        // And the unknown-email branch returns before the user exists to read roles for.
        manager.GetRolesCallCount.ShouldBe(0);
    }

    private async Task<CountingUserManager> ValidateAndCaptureAsync(string email, string password)
    {
        using var scope = _provider.CreateScope();

        // Resolved before the service so that the assertion reads the very instance the service
        // is about to be handed: both are scoped, so the scope decides.
        var manager = (CountingUserManager)scope.ServiceProvider.GetRequiredService<UserManager<EnvanexUser>>();

        await scope.ServiceProvider
            .GetRequiredService<IIdentityService>()
            .ValidateCredentialsAsync(email, password);

        return manager;
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
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(FakeNow));
        services.AddInfrastructure(configuration);

        // After AddInfrastructure, so this registration is the one the container resolves.
        services.AddScoped<UserManager<EnvanexUser>, CountingUserManager>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
