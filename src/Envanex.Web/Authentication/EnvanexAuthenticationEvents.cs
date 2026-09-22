using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

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

    private static Task WriteCookieForbiddenAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/html; charset=utf-8";

        return context.Response.WriteAsync(ForbiddenHtml, context.HttpContext.RequestAborted);
    }
}
