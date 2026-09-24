using System.Net;
using System.Text.RegularExpressions;
using Envanex.Web.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Envanex.IntegrationTests.Fixtures;

/// <summary>
/// The one place a test signs in through the login form. Without it, "sign in through the form"
/// gets written a different way in every class that needs a cookie.
/// </summary>
internal static partial class CookieAuthHelper
{
    /// <summary>
    /// The hidden field <c>EditForm</c> renders on its own under static server rendering, recorded
    /// by PR 6b Spike A. One constant, not a string literal per test.
    /// </summary>
    public const string AntiforgeryFieldName = "__RequestVerificationToken";

    /// <summary>
    /// The field that names which form on the page a POST belongs to. Its value is the form's
    /// <c>FormName</c>.
    /// </summary>
    public const string FormHandlerFieldName = "_handler";

    /// <summary>
    /// The auth cookie's name: the framework's prefix followed by the scheme name, since the host
    /// does not rename it.
    /// </summary>
    public const string AuthCookieName = ".AspNetCore." + EnvanexAuthenticationSchemes.Cookie;

    public const string LoginPath = "/login";

    public const string LoginFormName = "login";

    public const string SignOutPath = "/sign-out";

    public const string SignOutFormName = "sign-out";

    /// <summary>
    /// A client that reports redirects instead of following them.
    /// </summary>
    /// <remarks>
    /// <c>AllowAutoRedirect = false</c> is the point: <c>CreateClient()</c> follows redirects by
    /// default, and every 302 assertion would otherwise see the followed response instead.
    /// <c>HandleCookies</c> stays at its default of <see langword="true"/>; the cookie container is
    /// what replays the auth cookie, which is why the test host runs with
    /// <c>Auth:Cookie:SecurePolicy = SameAsRequest</c>.
    /// </remarks>
    public static HttpClient CreateNonRedirectingClient(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    /// <summary>
    /// GETs <paramref name="pagePath"/> and returns the antiforgery token its form carries. The
    /// matching antiforgery cookie lands in <paramref name="client"/>'s cookie container.
    /// </summary>
    public static async Task<string> ReadAntiforgeryTokenAsync(HttpClient client, string pagePath)
    {
        ArgumentNullException.ThrowIfNull(client);

        using var response = await client.GetAsync(pagePath);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"GET {pagePath} answered {(int)response.StatusCode}, so there is no form to read a token from.");
        }

        var body = await response.Content.ReadAsStringAsync();
        var match = AntiforgeryField().Match(body);

        if (!match.Success)
        {
            throw new InvalidOperationException($"GET {pagePath} rendered no '{AntiforgeryFieldName}' field.");
        }

        return match.Groups["token"].Value;
    }

    /// <summary>
    /// POSTs a statically rendered form. <paramref name="antiforgeryToken"/> is left out of the
    /// body when it is <see langword="null"/>, which is how a test proves antiforgery is enforced.
    /// </summary>
    public static Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string path,
        string formName,
        string? antiforgeryToken,
        IEnumerable<KeyValuePair<string, string>>? fields = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var form = new List<KeyValuePair<string, string>> { new(FormHandlerFieldName, formName) };

        if (antiforgeryToken is not null)
        {
            form.Add(new(AntiforgeryFieldName, antiforgeryToken));
        }

        if (fields is not null)
        {
            form.AddRange(fields);
        }

        return PostAsync(client, path, form);
    }

    /// <summary>
    /// POSTs the login form with the given credentials, carrying the given antiforgery token.
    /// </summary>
    public static Task<HttpResponseMessage> PostLoginFormAsync(
        HttpClient client,
        string? antiforgeryToken,
        string email,
        string password)
        => PostFormAsync(
            client,
            LoginPath,
            LoginFormName,
            antiforgeryToken,
            [new("Input.Email", email), new("Input.Password", password)]);

    /// <summary>
    /// GET <c>/login</c>, read the hidden field, POST the form, and return the same client holding
    /// the auth cookie. The user must exist already: this signs in, it does not create.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The form did not answer a redirect carrying the auth cookie, so the returned client would be
    /// anonymous and the calling test would fail later for a reason far from the cause.
    /// </exception>
    public static async Task<HttpClient> SignInAsync(
        WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = CreateNonRedirectingClient(factory);

        try
        {
            var token = await ReadAntiforgeryTokenAsync(client, LoginPath);

            using var response = await PostLoginFormAsync(client, token, email, password);

            if (response.StatusCode != HttpStatusCode.Found || !SetsAuthCookie(response))
            {
                throw new InvalidOperationException(
                    $"Signing in as '{email}' through the login form answered {(int)response.StatusCode} "
                    + "without setting the auth cookie.");
            }

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The path a redirect points at, whether the <c>Location</c> header is absolute or relative.
    /// </summary>
    public static string GetRedirectPath(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var location = response.Headers.Location
            ?? throw new InvalidOperationException($"The {(int)response.StatusCode} response carries no Location header.");

        return location.IsAbsoluteUri
            ? location.AbsolutePath
            : new Uri(new Uri("http://localhost"), location).AbsolutePath;
    }

    /// <summary>
    /// Every <c>Set-Cookie</c> header on the response that writes the auth cookie.
    /// </summary>
    public static IReadOnlyList<string> GetAuthCookieHeaders(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Where(value => value.StartsWith(AuthCookieName + "=", StringComparison.Ordinal)).ToList()
            : [];
    }

    private static bool SetsAuthCookie(HttpResponseMessage response) => GetAuthCookieHeaders(response).Count > 0;

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        List<KeyValuePair<string, string>> form)
    {
        using var content = new FormUrlEncodedContent(form);

        return await client.PostAsync(path, content);
    }

    [GeneratedRegex("name=\"" + AntiforgeryFieldName + "\" value=\"(?<token>[^\"]+)\"")]
    private static partial Regex AntiforgeryField();
}
