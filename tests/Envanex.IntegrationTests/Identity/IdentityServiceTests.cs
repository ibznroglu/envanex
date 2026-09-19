using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;
using Envanex.Infrastructure;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// The credential path against the real Identity stores. Every failure case asserts the same error
/// code on purpose: the point of Decision 5 is that a wrong password, an unknown address and a
/// locked-out account are indistinguishable to the caller.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class IdentityServiceTests : IAsyncLifetime
{
    private const string Email = "identity-service@envanex.test";
    private const string CorrectPassword = "CorrectHorse1Battery";
    private const string WrongPassword = "WrongHorse9Battery";

    // Deliberately a fixed instant in the past: Identity reads the system clock when it stamps a
    // lockout, so LockoutEnd is always ahead of this one and the step-5 log condition is stable.
    private static readonly DateTimeOffset FakeNow = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    // The sink, not the provider: the provider is an ILoggerProvider and therefore IDisposable,
    // and the container that creates it is the thing that owns it.
    private readonly List<LogRecord> _logRecords = [];
    private ServiceProvider _provider = null!;
    private Guid _userId;

    public IdentityServiceTests(SqlServerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetIdentityAsync();

        _provider = BuildProvider();
        _userId = await IdentitySeeder.CreateUserAsync(_provider, Email, CorrectPassword);
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task ValidateCredentialsAsync_CorrectPassword_ShouldReturnAuthenticatedUserWithMatchingId()
    {
        var result = await ValidateAsync(Email, CorrectPassword);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(_userId);
        result.Value.Email.ShouldBe(Email);
        result.Value.UserName.ShouldBe(Email);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_UnknownEmail_ShouldReturnInvalidCredentials()
    {
        var result = await ValidateAsync("nobody@envanex.test", CorrectPassword);

        result.Error.ShouldBe(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WrongPassword_ShouldReturnInvalidCredentials()
    {
        var result = await ValidateAsync(Email, WrongPassword);

        result.Error.ShouldBe(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WrongPassword_ShouldIncrementAccessFailedCount()
    {
        await ValidateAsync(Email, WrongPassword);

        var count = await IdentitySeeder.GetAccessFailedCountAsync(_provider, Email);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_FifthWrongPassword_ShouldReturnInvalidCredentials()
    {
        Result<AuthenticatedUser> result = null!;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            result = await ValidateAsync(Email, WrongPassword);
        }

        // The attempt that trips the lockout answers exactly like the four before it.
        result.Error.ShouldBe(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_FifthWrongPassword_ShouldSetLockoutEndAboutFifteenMinutesAhead()
    {
        // Identity's lockout reads the system clock, not the injected TimeProvider, so this asserts
        // the stored value against real time with a tolerance rather than advancing a fake clock.
        var before = DateTimeOffset.UtcNow;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await ValidateAsync(Email, WrongPassword);
        }

        var lockoutEnd = await IdentitySeeder.GetLockoutEndAsync(_provider, Email);

        lockoutEnd.ShouldNotBeNull();
        lockoutEnd.Value.ShouldBeGreaterThan(before.AddMinutes(14));
        lockoutEnd.Value.ShouldBeLessThan(DateTimeOffset.UtcNow.AddMinutes(16));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_CorrectPasswordWhileLockedOut_ShouldReturnInvalidCredentials()
    {
        await IdentitySeeder.LockOutAsync(_provider, Email);

        var result = await ValidateAsync(Email, CorrectPassword);

        result.Error.ShouldBe(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_CorrectPasswordWhileLockedOut_ShouldLeaveAccessFailedCountUnchanged()
    {
        // One real failure first, so "unchanged" means "still one" rather than "still zero":
        // neither AccessFailedAsync nor ResetAccessFailedCountAsync may run on a locked-out
        // account, and a reset to zero would be just as wrong as an increment to two.
        await ValidateAsync(Email, WrongPassword);
        await IdentitySeeder.LockOutAsync(_provider, Email);

        await ValidateAsync(Email, CorrectPassword);

        var count = await IdentitySeeder.GetAccessFailedCountAsync(_provider, Email);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_UnknownEmailWrongPasswordAndLockedOut_ShouldAllReturnTheSameErrorCode()
    {
        var unknownEmail = await ValidateAsync("nobody@envanex.test", CorrectPassword);
        var wrongPassword = await ValidateAsync(Email, WrongPassword);

        await IdentitySeeder.LockOutAsync(_provider, Email);
        var lockedOut = await ValidateAsync(Email, CorrectPassword);

        // Same instance, not merely the same code: three identical answers at the source cannot
        // drift apart into three distinguishable responses later.
        unknownEmail.Error.ShouldBeSameAs(AuthErrors.InvalidCredentials);
        wrongPassword.Error.ShouldBeSameAs(AuthErrors.InvalidCredentials);
        lockedOut.Error.ShouldBeSameAs(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_LockedOut_ShouldLogAWarningCarryingTheUserId()
    {
        await IdentitySeeder.LockOutAsync(_provider, Email);

        await ValidateAsync(Email, CorrectPassword);

        // The response says nothing about lockout, so the log is the only place it is observable.
        CapturedLogs
            .Where(record => record.Level == LogLevel.Warning)
            .ShouldContain(record => record.Message.Contains(_userId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_LockedOut_ShouldNotLogTheEmailOrPassword()
    {
        await IdentitySeeder.LockOutAsync(_provider, Email);

        await ValidateAsync(Email, CorrectPassword);

        foreach (var record in CapturedLogs)
        {
            record.Message.ShouldNotContain(Email, Case.Insensitive);
            record.Message.ShouldNotContain(CorrectPassword, Case.Insensitive);
        }
    }

    [Fact]
    public async Task ValidateCredentialsAsync_CorrectPasswordAfterTwoFailures_ShouldResetAccessFailedCountToZero()
    {
        await ValidateAsync(Email, WrongPassword);
        await ValidateAsync(Email, WrongPassword);

        await ValidateAsync(Email, CorrectPassword);

        var count = await IdentitySeeder.GetAccessFailedCountAsync(_provider, Email);

        count.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_NullEmail_ShouldThrowArgumentNullException()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        await Should.ThrowAsync<ArgumentNullException>(() => service.ValidateCredentialsAsync(null!, CorrectPassword));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_NullPassword_ShouldThrowArgumentNullException()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        await Should.ThrowAsync<ArgumentNullException>(() => service.ValidateCredentialsAsync(Email, null!));
    }

    private async Task<Result<AuthenticatedUser>> ValidateAsync(string email, string password)
    {
        // A fresh scope per call, because one login is one request is one scope in the host.
        using var scope = _provider.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IIdentityService>()
            .ValidateCredentialsAsync(email, password);
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
        services.AddLogging(logging =>
            logging.SetMinimumLevel(LogLevel.Trace).AddProvider(new CapturingLoggerProvider(_logRecords)));

        // Before AddInfrastructure on purpose: AddEnvanexIdentity uses TryAddSingleton, so the
        // first registration wins and the fake clock survives.
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(FakeNow));
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private IReadOnlyList<LogRecord> CapturedLogs
    {
        get
        {
            lock (_logRecords)
            {
                return [.. _logRecords];
            }
        }
    }

    private sealed record LogRecord(string Category, LogLevel Level, string Message);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<LogRecord> _records;

        public CapturingLoggerProvider(List<LogRecord> records) => _records = records;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void Dispose()
        {
        }

        private void Add(LogRecord record)
        {
            lock (_records)
            {
                _records.Add(record);
            }
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerProvider _provider;
            private readonly string _category;

            public CapturingLogger(CapturingLoggerProvider provider, string category)
            {
                _provider = provider;
                _category = category;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                ArgumentNullException.ThrowIfNull(formatter);

                _provider.Add(new LogRecord(_category, logLevel, formatter(state, exception)));
            }
        }
    }
}
