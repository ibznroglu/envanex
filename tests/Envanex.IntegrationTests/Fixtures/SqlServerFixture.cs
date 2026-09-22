using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Envanex.Application.Authentication;
using Envanex.Application.Authentication.Commands;
using Envanex.Application.Authentication.DTOs;
using Envanex.Infrastructure.Identity;
using Envanex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Testcontainers.MsSql;

namespace Envanex.IntegrationTests.Fixtures;

public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>The fixture's user in <see cref="EnvanexRoles.Administrator"/>.</summary>
    public const string AdministratorEmail = "fixture-administrator@envanex.test";

    /// <summary>The fixture's user in <see cref="EnvanexRoles.Viewer"/>.</summary>
    public const string ViewerEmail = "fixture-viewer@envanex.test";

    /// <summary>The fixture's authenticated user in no role at all.</summary>
    public const string RoleLessEmail = "fixture-no-role@envanex.test";

    /// <summary>
    /// Long enough, with a digit and both cases, to satisfy the password policy
    /// <c>AddEnvanexIdentity</c> configures.
    /// </summary>
    public const string SeededPassword = "Envanex-Fixture-Parola-1";

    /// <summary>
    /// How close to its <c>exp</c> a cached token may come before it is re-minted. Access tokens
    /// live fifteen minutes and the bearer handler runs with <c>ClockSkew = TimeSpan.Zero</c>, so
    /// a run where a long gap opens between a cache fill and its last use — a cold CI agent, a
    /// paused debugger — would otherwise hand out an expired token and fail an arbitrary subset of
    /// the authenticated tests with 401.
    /// </summary>
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(1);

    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>
    /// Access tokens keyed by email. Mutated without a lock on purpose: every class that reads it
    /// belongs to <see cref="DatabaseCollection"/>, which xUnit runs serially.
    /// </summary>
    private readonly Dictionary<string, CachedToken> _tokenCache = new(StringComparer.OrdinalIgnoreCase);

    public string ConnectionString => _container.GetConnectionString();

    public EnvanexWebApplicationFactory WebApplicationFactory { get; private set; } = null!;

    public EnvanexDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnvanexDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new EnvanexDbContext(options);
    }

    public EnvanexIdentityDbContext CreateIdentityDbContext()
    {
        // Two steps on purpose: UseEnvanexIdentitySqlServer returns the non-generic builder,
        // whose Options property is not DbContextOptions<EnvanexIdentityDbContext>.
        var optionsBuilder = new DbContextOptionsBuilder<EnvanexIdentityDbContext>();
        optionsBuilder.UseEnvanexIdentitySqlServer(ConnectionString);

        return new EnvanexIdentityDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// The access token for <paramref name="email"/>, minted by a real
    /// <c>POST /api/auth/login</c> against the shared host.
    /// </summary>
    /// <param name="email">The user to log in as; created if it is not there yet.</param>
    /// <param name="role">The role the user must be in, or <see langword="null"/> for none.</param>
    /// <remarks>
    /// Acquired lazily, on a cache miss, and never in a constructor: a token taken once per class
    /// dies the moment another class's <see cref="ResetIdentityAsync"/> deletes its user. Caching
    /// is not a shortcut around logging in for real — the cached token was minted by the real
    /// endpoint against a real password hash, and it still passes full issuer, audience, lifetime
    /// and signature validation on every single use.
    /// </remarks>
    public async Task<string> GetAccessTokenAsync(string email, string? role)
    {
        if (_tokenCache.TryGetValue(email, out var cached) && cached.ExpiresAt - DateTimeOffset.UtcNow > ExpiryMargin)
        {
            return cached.Token;
        }

        var services = WebApplicationFactory.Services;

        if (role is not null)
        {
            await IdentitySeeder.EnsureRoleAsync(services, role);
        }

        await IdentitySeeder.EnsureUserAsync(services, email, SeededPassword);

        if (role is not null)
        {
            await IdentitySeeder.EnsureUserInRoleAsync(services, email, role);
        }

        var token = await LogInAsync(email);

        // Read off the token the issuer actually produced, never computed from
        // Jwt:AccessTokenMinutes: a cache that derives the expiry from configuration is a cache
        // that can disagree with the issuer.
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(
            new JsonWebToken(token).GetPayloadValue<long>(JwtRegisteredClaimNames.Exp));

        _tokenCache[email] = new CachedToken(token, expiresAt);

        return token;
    }

    public Task<string> GetAdministratorTokenAsync()
        => GetAccessTokenAsync(AdministratorEmail, EnvanexRoles.Administrator);

    public Task<string> GetViewerTokenAsync()
        => GetAccessTokenAsync(ViewerEmail, EnvanexRoles.Viewer);

    public Task<string> GetRoleLessTokenAsync()
        => GetAccessTokenAsync(RoleLessEmail, null);

    public Task<HttpClient> CreateAdministratorClientAsync()
        => CreateAdministratorClientAsync(WebApplicationFactory);

    /// <summary>
    /// An administrator client for <paramref name="factory"/>, carrying a token minted on the
    /// <em>shared</em> host.
    /// </summary>
    /// <remarks>
    /// Cross-minting is the point. All three factories share the signing key, the issuer, the
    /// audience and the database, so a token minted on one validates on any of them — and logging
    /// in through a deliberately rate-limited host would spend one of the very permits the test
    /// using that host exists to count.
    /// </remarks>
    public async Task<HttpClient> CreateAdministratorClientAsync(WebApplicationFactory<Program> factory)
        => Authenticate(factory, await GetAdministratorTokenAsync());

    public Task<HttpClient> CreateViewerClientAsync()
        => CreateViewerClientAsync(WebApplicationFactory);

    public async Task<HttpClient> CreateViewerClientAsync(WebApplicationFactory<Program> factory)
        => Authenticate(factory, await GetViewerTokenAsync());

    public async Task<HttpClient> CreateRoleLessClientAsync()
        => Authenticate(WebApplicationFactory, await GetRoleLessTokenAsync());

    /// <summary>
    /// Moves the cached token for <paramref name="email"/> to within <see cref="ExpiryMargin"/> of
    /// its expiry, without touching the database and without touching the token itself.
    /// </summary>
    /// <remarks>
    /// A test seam, and the only way to reach the re-mint branch without waiting fifteen minutes.
    /// It rewrites the expiry to <em>inside</em> the margin rather than into the past on purpose: a
    /// token that had already expired would also be re-minted by a naive "has it expired yet"
    /// check, so only a still-valid-but-near-expiry entry proves the margin is there.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Nothing is cached for <paramref name="email"/>, so the call would be a silent no-op and the
    /// test asking for it would pass without exercising anything.
    /// </exception>
    internal void ExpireCachedToken(string email)
    {
        if (!_tokenCache.TryGetValue(email, out var cached))
        {
            throw new InvalidOperationException($"No access token is cached for '{email}'.");
        }

        _tokenCache[email] = cached with { ExpiresAt = DateTimeOffset.UtcNow + (ExpiryMargin / 2) };
    }

    public async Task ResetAsync()
    {
        await using var context = CreateDbContext();

        // Deletes follow FK dependency order: children before parents.
        // Update this list when new tables are added.
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM UnitOfMeasures WHERE BaseUnitId IS NOT NULL");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM UnitOfMeasures WHERE BaseUnitId IS NULL");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Warehouses");
    }

    /// <summary>
    /// Resets the Identity context only. Deliberately separate from <see cref="ResetAsync"/>:
    /// the auth tables are a different context with a different FK graph, and mixing the two
    /// lists is what makes a hand-maintained delete order rot.
    /// </summary>
    public async Task ResetIdentityAsync()
    {
        // In the same method body as the deletes on purpose, and ahead of them: the only code that
        // can delete a seeded user is the only code that can orphan a cached token, so it cannot
        // orphan one — not even when a delete below throws half way through.
        _tokenCache.Clear();

        await using var context = CreateIdentityDbContext();

        // Deletes follow FK dependency order: children before parents.
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetUserTokens");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetUserLogins");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetUserClaims");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetUserRoles");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetRoleClaims");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetRoles");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM auth.AspNetUsers");
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        await using var identityContext = CreateIdentityDbContext();
        await identityContext.Database.MigrateAsync();

        WebApplicationFactory = new EnvanexWebApplicationFactory(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (WebApplicationFactory is not null)
        {
            await WebApplicationFactory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    private async Task<string> LogInAsync(string email)
    {
        using var client = WebApplicationFactory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, SeededPassword));

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Logging in as '{email}' answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        var body = await response.Content.ReadFromJsonAsync<AuthenticationResponse>()
            ?? throw new InvalidOperationException($"Logging in as '{email}' returned an empty body.");

        return body.AccessToken;
    }

    private static HttpClient Authenticate(WebApplicationFactory<Program> factory, string token)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);
}
