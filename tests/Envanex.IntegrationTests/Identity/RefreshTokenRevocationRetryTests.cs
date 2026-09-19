using Envanex.Application.Abstractions.Authentication;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Models;
using Envanex.Domain.Common;
using Envanex.Infrastructure;
using Envanex.Infrastructure.Identity;
using Envanex.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Envanex.IntegrationTests.Identity;

/// <summary>
/// The two ways the bounded revocation retry can run out of attempts, forced rather than raced.
/// <see cref="RefreshTokenConcurrencyTests"/> deliberately orchestrates nothing; this class
/// deliberately does, because the interleaving it needs — a concurrency failure on the
/// <em>final</em> attempt — cannot be produced on demand by two real racers. The injection is a
/// save interceptor registered in the test's own container: no seam is added to <c>src/</c>.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RefreshTokenRevocationRetryTests : IAsyncLifetime
{
    private const string Email = "refresh-token-revocation-retry@envanex.test";
    private const string Password = "CorrectHorse1Battery";

    private static readonly DateTimeOffset FakeNow = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly FakeTimeProvider _time = new(FakeNow);
    private readonly List<LogRecord> _logRecords = [];
    private readonly LosingSaveInterceptor _interceptor = new();
    private ServiceProvider _provider = null!;
    private Guid _userId;

    public RefreshTokenRevocationRetryTests(SqlServerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetIdentityAsync();

        _provider = BuildProvider();
        _userId = await IdentitySeeder.CreateUserAsync(_provider, Email, Password);
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task RevokeFamilyAsync_WhenTheFinalAttemptLosesToAWriterThatRevokedTheFamily_ShouldReturnSuccess()
    {
        var issued = await IssueAsync();
        var familyId = await GetFamilyIdAsync(issued.Token);

        // Every attempt loses, and the last one loses to a writer that revoked the family first.
        // That is the case the loop cannot see by itself: the exception skips the post-save check,
        // so without the re-read after the loop this reports failure over a family already dead.
        _interceptor.Arm(ct => RevokeOutOfBandAsync(familyId, ct));

        var result = await RevokeAsync(issued.Token);

        // Non-vacuous: if the interceptor never fired, the revocation simply succeeded and this
        // test would prove nothing about the path it names.
        _interceptor.Attempts.ShouldBe(RefreshTokenService.RevocationRetryLimit);

        result.IsSuccess.ShouldBeTrue($"Revocation returned: {Describe(result)}");

        var liveRows = await LoadLiveRowsAsync(familyId);
        liveRows.ShouldBeEmpty();

        // The Error line is what an operator gets paged on, so a dead family must not produce one.
        CapturedLogs.ShouldNotContain(record => record.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RevokeFamilyAsync_WhenEveryAttemptLosesAndTheFamilyIsStillLive_ShouldFailAndLogAnError()
    {
        var issued = await IssueAsync();
        var familyId = await GetFamilyIdAsync(issued.Token);

        // Same exhaustion, opposite post-state: nobody revoked anything, so the re-read after the
        // loop finds the family alive and the alarm is real. This is the assertion that keeps the
        // re-read from degenerating into "always report success".
        _interceptor.Arm(onFinalAttempt: null);

        var result = await RevokeAsync(issued.Token);

        _interceptor.Attempts.ShouldBe(RefreshTokenService.RevocationRetryLimit);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeSameAs(AuthErrors.InvalidRefreshToken);

        var liveRows = await LoadLiveRowsAsync(familyId);
        liveRows.Count.ShouldBe(1);

        CapturedLogs.ShouldContain(record =>
            record.Level == LogLevel.Error
            && record.Message.Contains(familyId.ToString(), StringComparison.Ordinal));
    }

    private static string Describe(Result result)
        => result.IsSuccess ? "success" : result.Error.Code;

    private async Task<IssuedRefreshToken> IssueAsync()
    {
        using var scope = _provider.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IRefreshTokenService>()
            .IssueAsync(_userId);

        result.IsSuccess.ShouldBeTrue();

        return result.Value;
    }

    private async Task<Result> RevokeAsync(string token)
    {
        using var scope = _provider.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IRefreshTokenService>()
            .RevokeFamilyAsync(token);
    }

    /// <summary>
    /// The winning writer: its own context on its own connection, exactly as a concurrent logout
    /// or reuse detection would be. It commits while the losing save is still pending.
    /// </summary>
    private async Task RevokeOutOfBandAsync(Guid familyId, CancellationToken ct)
    {
        await using var context = _fixture.CreateIdentityDbContext();

        var rows = await context.RefreshTokens
            .Where(row => row.FamilyId == familyId && row.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            row.Revoke(_time.GetUtcNow(), RefreshTokenRevocationReason.Logout);
        }

        await context.SaveChangesAsync(ct);
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

    private async Task<List<RefreshToken>> LoadLiveRowsAsync(Guid familyId)
    {
        await using var context = _fixture.CreateIdentityDbContext();

        return await context.RefreshTokens
            .AsNoTracking()
            .Where(row => row.FamilyId == familyId && row.RevokedAt == null)
            .ToListAsync();
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

        services.AddSingleton<TimeProvider>(_time);

        // Before AddInfrastructure for the same reason the fake clock is: AddDbContext registers
        // DbContextOptions<T> with TryAdd, so the first registration wins and the interceptor
        // survives. Registering it afterwards would be accepted and then silently ignored.
        services.AddDbContext<EnvanexIdentityDbContext>(options =>
            options
                .UseEnvanexIdentitySqlServer(_fixture.ConnectionString)
                .AddInterceptors(_interceptor));

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

    /// <summary>
    /// Fails every armed save the way losing a race does, and optionally lets the winner commit
    /// first on the final attempt. Disarmed by default, so seeding and issuing are untouched.
    /// </summary>
    private sealed class LosingSaveInterceptor : SaveChangesInterceptor
    {
        private Func<CancellationToken, Task>? _onFinalAttempt;
        private bool _armed;

        public int Attempts { get; private set; }

        public void Arm(Func<CancellationToken, Task>? onFinalAttempt)
        {
            Attempts = 0;
            _onFinalAttempt = onFinalAttempt;
            _armed = true;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_armed)
            {
                return await base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            Attempts++;

            if (Attempts == RefreshTokenService.RevocationRetryLimit && _onFinalAttempt is not null)
            {
                await _onFinalAttempt(cancellationToken);
            }

            // What losing actually looks like from here: the winner moved the RowVersion of every
            // row this save was about to stamp, so the UPDATE matches nothing.
            throw new DbUpdateConcurrencyException("Simulated loss of a revocation race.");
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
