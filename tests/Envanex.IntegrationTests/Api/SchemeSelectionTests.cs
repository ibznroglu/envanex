using Envanex.IntegrationTests.Fixtures;
using Envanex.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

/// <summary>
/// The selector scheme's forwarding rule, read out of the host's own options. The rule keys on the
/// path prefix and never on whether an <c>Authorization</c> header is present: a browser holding a
/// cookie and sending no header to <c>/api/*</c> must still reach the bearer scheme.
/// </summary>
/// <remarks>
/// These call the selector directly. The end-to-end proof that an <c>/api/*</c> challenge never
/// redirects is
/// <c>CookieAuthPipelineTests.ApiPath_WithASessionCookieAndNoBearerToken_ShouldReturn401AndNotARedirect</c>,
/// which sends a session cookie and no bearer token to a protected API endpoint.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class SchemeSelectionTests
{
    private readonly SqlServerFixture _fixture;

    public SchemeSelectionTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact]
    public void ForwardDefaultSelector_ForAnApiPath_ShouldSelectTheBearerScheme()
    {
        var selector = Selector();

        // No Authorization header on any of them: the path alone decides.
        selector(RequestFor("/api/products")).ShouldBe(JwtBearerDefaults.AuthenticationScheme);
        selector(RequestFor("/api")).ShouldBe(JwtBearerDefaults.AuthenticationScheme);
        selector(RequestFor("/API/unit-of-measures")).ShouldBe(JwtBearerDefaults.AuthenticationScheme);
    }

    [Fact]
    public void ForwardDefaultSelector_ForANonApiPath_ShouldSelectTheCookieScheme()
    {
        var selector = Selector();

        selector(RequestFor("/")).ShouldBe(EnvanexAuthenticationSchemes.Cookie);
        selector(RequestFor("/login")).ShouldBe(EnvanexAuthenticationSchemes.Cookie);

        // A segment match, not a string prefix: "/apiary" is not under /api.
        selector(RequestFor("/apiary")).ShouldBe(EnvanexAuthenticationSchemes.Cookie);
    }

    private Func<HttpContext, string?> Selector()
    {
        var options = _fixture.WebApplicationFactory.Services
            .GetRequiredService<IOptionsMonitor<PolicySchemeOptions>>()
            .Get(EnvanexAuthenticationSchemes.Selector);

        return options.ForwardDefaultSelector.ShouldNotBeNull(
            "The selector scheme has no ForwardDefaultSelector, so it forwards nothing.");
    }

    private static DefaultHttpContext RequestFor(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        return context;
    }
}
