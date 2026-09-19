using Envanex.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Envanex.IntegrationTests.Fixtures;

/// <summary>
/// A <see cref="UserManager{TUser}"/> that records which of four calls the service under test made
/// and in what order.
/// </summary>
/// <remarks>
/// This is the instrument that makes "the password is verified before the lockout check" and
/// "AccessFailedAsync fires at most once per attempt" assertable without a stopwatch. It is
/// test-only and is never referenced from <c>src/</c>: no seam, hook or interceptor was added to
/// production code for it. Every override delegates, so the behaviour it observes is the real one.
/// </remarks>
internal sealed class CountingUserManager : UserManager<EnvanexUser>
{
    private readonly List<string> _callLog = [];

    public CountingUserManager(
        IUserStore<EnvanexUser> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<EnvanexUser> passwordHasher,
        IEnumerable<IUserValidator<EnvanexUser>> userValidators,
        IEnumerable<IPasswordValidator<EnvanexUser>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<EnvanexUser>> logger)
        : base(
            store,
            optionsAccessor,
            passwordHasher,
            userValidators,
            passwordValidators,
            keyNormalizer,
            errors,
            services,
            logger)
    {
    }

    public int CheckPasswordCallCount { get; private set; }

    public int IsLockedOutCallCount { get; private set; }

    public int AccessFailedCallCount { get; private set; }

    public int ResetAccessFailedCountCallCount { get; private set; }

    /// <summary>Ordered names of the intercepted calls, for ordering assertions.</summary>
    public IReadOnlyList<string> CallLog => _callLog;

    public override Task<bool> CheckPasswordAsync(EnvanexUser user, string password)
    {
        CheckPasswordCallCount++;
        _callLog.Add(nameof(CheckPasswordAsync));

        return base.CheckPasswordAsync(user, password);
    }

    public override Task<bool> IsLockedOutAsync(EnvanexUser user)
    {
        IsLockedOutCallCount++;
        _callLog.Add(nameof(IsLockedOutAsync));

        return base.IsLockedOutAsync(user);
    }

    public override Task<IdentityResult> AccessFailedAsync(EnvanexUser user)
    {
        AccessFailedCallCount++;
        _callLog.Add(nameof(AccessFailedAsync));

        return base.AccessFailedAsync(user);
    }

    public override Task<IdentityResult> ResetAccessFailedCountAsync(EnvanexUser user)
    {
        ResetAccessFailedCountCallCount++;
        _callLog.Add(nameof(ResetAccessFailedCountAsync));

        return base.ResetAccessFailedCountAsync(user);
    }
}
