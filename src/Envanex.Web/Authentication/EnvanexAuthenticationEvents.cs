using System.Text.Json;
using Envanex.Web.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace Envanex.Web.Authentication;

/// <summary>
/// The authentication events whose job is to make every rejection carry a body.
/// </summary>
/// <remarks>
/// A bodiless 4xx is exactly what <c>UseStatusCodePagesWithReExecute("/not-found")</c> re-executes,
/// and the re-executed request can then overwrite the original status with one of its own. Writing
/// a body starts the response, and a started response is never re-executed.
/// </remarks>
internal static class EnvanexAuthenticationEvents
{
    /// <summary>The detail of the bearer challenge's ProblemDetails.</summary>
    internal const string ChallengeDetail = "Kimlik doğrulaması gerekli.";

    /// <summary>The detail of the bearer forbid's ProblemDetails.</summary>
    internal const string ForbiddenDetail = "Bu işlem için yetkiniz yok.";

    private const string ProblemJsonContentType = "application/problem+json";

    // A string literal rather than a .razor page: the forbid has to answer 403 with a body, and
    // AccessDeniedPath would answer 302 instead. The known gap is recorded in the PR 6b plan; PR 7
    // replaces this with a re-executed access-denied component that keeps the 403.
    private const string ForbiddenHtml =
        "<!DOCTYPE html>\n"
        + "<html lang=\"tr\">\n"
        + "<head><meta charset=\"utf-8\" /><title>Erişim reddedildi</title></head>\n"
        + "<body>\n"
        + "<h1>Bu sayfayı görüntüleme yetkiniz yok.</h1>\n"
        + "<p>Farklı bir hesapla devam etmek için <a href=\"/sign-out\">oturumu kapatın</a>.</p>\n"
        + "</body>\n"
        + "</html>\n";

    /// <summary>
    /// The cookie scheme's events. A new instance per call, so no two registrations share one.
    /// </summary>
    /// <remarks>
    /// <c>OnRedirectToLogin</c> keeps the framework's 302: that is the right answer on a page, and
    /// the scheme selector never lets the cookie scheme see an <c>/api/*</c> path, where it would
    /// be the wrong one.
    /// </remarks>
    public static CookieAuthenticationEvents Cookie => new()
    {
        OnRedirectToAccessDenied = WriteCookieForbiddenAsync,
    };

    /// <summary>
    /// The bearer scheme's events. A new instance per call, so no two registrations share one.
    /// </summary>
    /// <remarks>
    /// The challenge body is load-bearing, not cosmetic. Without it a denied <c>/api/*</c> request
    /// answers a bodiless 401 that the wrapper re-executes as <c>/not-found</c>. While
    /// <c>/not-found</c> is open, the re-executed page renders and an API client receives a 401
    /// carrying an HTML page instead of problem+json. If <c>/not-found</c> is also closed, the
    /// selector hands the re-executed request to the cookie scheme, because <c>/not-found</c> is
    /// not under <c>/api/*</c>, and the client receives a 302 to the login page. Both were observed
    /// by mutation in PR 6b Phase 4. The redirect needs the two defects together, which is the
    /// configuration Spike C3's probe 7 measured.
    /// </remarks>
    public static JwtBearerEvents Bearer => new()
    {
        OnChallenge = WriteBearerChallengeAsync,
        OnForbidden = WriteBearerForbiddenAsync,
    };

    private static Task WriteCookieForbiddenAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/html; charset=utf-8";

        return context.Response.WriteAsync(ForbiddenHtml, context.HttpContext.RequestAborted);
    }

    private static async Task WriteBearerChallengeAsync(JwtBearerChallengeContext context)
    {
        await WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, ChallengeDetail);

        // Without this the handler carries on after the event and appends its own headers to a
        // response that has already started. The price is that no 401 in this application carries
        // WWW-Authenticate; the known gap is recorded in the PR 6b plan.
        context.HandleResponse();
    }

    private static Task WriteBearerForbiddenAsync(ForbiddenContext context)
        => WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, ForbiddenDetail);

    private static Task WriteProblemAsync(HttpContext httpContext, int statusCode, string detail)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = ProblemJsonContentType;

        // Titled by the same function ResultExtensions uses, so an authentication 401 and a
        // use-case 401 read the same to a client.
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = ResultExtensions.GetReasonPhrase(statusCode),
            Detail = detail,
        };

        return JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            problemDetails,
            cancellationToken: httpContext.RequestAborted);
    }
}
