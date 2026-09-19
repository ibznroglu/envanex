using System.Net;
using Envanex.Web.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

/// <summary>
/// Unit tests for the login rate-limit partition key. They live in this project because it is the
/// one with <c>InternalsVisibleTo</c> from <c>Envanex.Web</c>; none of them touches a database.
/// </summary>
public sealed class LoginRateLimitPartitionTests
{
    [Fact]
    public void GetKey_WithARemoteIpAddress_ShouldReturnThatAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.42");

        LoginRateLimitPartition.GetKey(context).ShouldBe("198.51.100.42");
    }

    [Fact]
    public void GetKey_WithNoRemoteIpAddress_ShouldReturnTheUnknownPartitionKey()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = null;

        // An unhandled null here would throw inside the partition factory on every request.
        LoginRateLimitPartition.GetKey(context).ShouldBe(LoginRateLimitPartition.UnknownPartitionKey);
    }

    [Fact]
    public void GetKey_ShouldIgnoreXForwardedForUntilPr7()
    {
        // PR 7 TRIPWIRE. This test records a deliberate gap, not a desired behaviour: behind a
        // reverse proxy every client collapses into the proxy's partition. PR 7 configures
        // UseForwardedHeaders with real KnownProxies and DELETES this test. Do not "fix" the
        // partition key by reading the header here — an unvalidated X-Forwarded-For is
        // attacker-controlled and makes the limiter worthless.
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7";

        LoginRateLimitPartition.GetKey(context).ShouldBe("10.0.0.1");
    }

    [Fact]
    public void GetKey_NullContext_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => LoginRateLimitPartition.GetKey(null!));
    }
}
