namespace Envanex.Web.RateLimiting;

/// <summary>
/// Produces the partition key of the login rate-limit policy. A named method rather than an inline
/// lambda so that the reverse-proxy fix in PR 7 has exactly one edit site.
/// </summary>
internal static class LoginRateLimitPartition
{
    /// <summary>
    /// Partition key used when the connection carries no remote address, which is the case
    /// under WebApplicationFactory and — until PR 7 configures forwarded headers — would also
    /// be the effective case behind a reverse proxy.
    /// </summary>
    public const string UnknownPartitionKey = "unknown";

    /// <summary>
    /// Returns the remote address of the connection, or <see cref="UnknownPartitionKey"/> when the
    /// connection has none. An unhandled null would throw inside the partition factory on every
    /// request.
    /// </summary>
    /// <remarks>
    /// <c>X-Forwarded-For</c> is deliberately not read. Trusting it without a configured
    /// <c>KnownProxies</c> list would make the partition key attacker-controlled and the limiter
    /// worthless, which is strictly worse than one shared bucket: a global limit fails closed and
    /// loudly. PR 7 owns the forwarded-headers configuration.
    /// </remarks>
    public static string GetKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Connection.RemoteIpAddress?.ToString() ?? UnknownPartitionKey;
    }
}
