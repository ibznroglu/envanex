using System.Text.Json;
using System.Threading.RateLimiting;
using Envanex.Application;
using Envanex.Application.Authentication;
using Envanex.Infrastructure;
using Envanex.Web.Authentication;
using Envanex.Web.Authorization;
using Envanex.Web.Components;
using Envanex.Web.Controllers;
using Envanex.Web.DataSource;
using Envanex.Web.Extensions;
using Envanex.Web.Middleware;
using Envanex.Web.RateLimiting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers(options =>
{
    options.ModelBinderProviders.Insert(0, new DataSourceLoadOptionsModelBinderProvider());
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEnvanexJwtBearer(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    // Roles map to policies here and nowhere else: a page or an endpoint names a policy, so adding
    // a role later is an edit to these two lines rather than to every place that checks one.
    options.AddPolicy(EnvanexPolicies.CanRead, policy => policy.RequireRole(EnvanexRoles.Administrator, EnvanexRoles.Viewer));
    options.AddPolicy(EnvanexPolicies.CanWrite, policy => policy.RequireRole(EnvanexRoles.Administrator));

    // FallbackPolicy lands in PR 6b Phase 4, when the API endpoints close.
});

// The Blazor side of authentication. The provider replaces the framework's plain
// ServerAuthenticationStateProvider, which never re-reads the user once a circuit is open.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider>();
builder.Services.AddScoped<CookieSignInService>();

var rateLimitingSection = builder.Configuration.GetSection("RateLimiting");
bool rateLimitingEnabled = rateLimitingSection.GetValue("Enabled", true);
int permitLimit = rateLimitingSection.GetValue("PermitLimit", 100);
int windowSeconds = rateLimitingSection.GetValue("WindowSeconds", 60);

var loginRateLimitingSection = rateLimitingSection.GetSection("Login");
bool loginRateLimitingEnabled = loginRateLimitingSection.GetValue("Enabled", true);
int loginPermitLimit = loginRateLimitingSection.GetValue("PermitLimit", 5);
int loginWindowSeconds = loginRateLimitingSection.GetValue("WindowSeconds", 300);

// AddRateLimiter is unconditional even when rate limiting is switched off: only the assignment of
// GlobalLimiter is conditional. The login policy has to be registered whatever the switches say,
// because [EnableRateLimiting("login")] naming a policy the middleware never sees throws at
// endpoint build time and takes down every endpoint in the application.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Çok fazla istek gönderildi.",
            Detail = "İstek sınırı aşıldı. Lütfen bir süre bekleyip tekrar deneyin.",
            Type = "https://httpstatuses.io/429",
        };

        await JsonSerializer.SerializeAsync(
            context.HttpContext.Response.Body,
            problemDetails,
            cancellationToken: cancellationToken);
    };

    if (rateLimitingEnabled)
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
            RateLimitPartition.GetFixedWindowLimiter("global", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
            }));
    }

    options.AddPolicy<string>(AuthController.LoginRateLimitPolicy, context =>
    {
        string partitionKey = LoginRateLimitPartition.GetKey(context);

        if (!loginRateLimitingEnabled)
        {
            return RateLimitPartition.GetNoLimiter(partitionKey);
        }

        // POST only. The policy also sits on the /login page, and a component endpoint serves GET
        // and POST from one endpoint, so without this every render of the form would spend a
        // permit and five reloads would lock login out for the whole window. AuthController.Login
        // is POST-only already and is unaffected.
        //
        // Its own fixed key, never the per-address partitionKey. A limiter is built once per key
        // and reused for the host's lifetime, so a no-limiter under the address key would let
        // whichever method arrived first decide that address's limiter for good: one GET of
        // /login switched off brute-force protection on POST /api/auth/login for that address.
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter("login-non-post");
        }

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginPermitLimit,
            Window = TimeSpan.FromSeconds(loginWindowSeconds),
        });
    });
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

// Unconditional: the middleware is what makes the named "login" policy resolvable, so leaving it
// out when the global limiter is off would break every endpoint rather than only the login one.
app.UseRateLimiter();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// After UseHttpsRedirection: there is no point authenticating a request about to be 307'd, and a
// bearer token should not be parsed off a plaintext request. Before UseAntiforgery and the endpoint
// mappings: antiforgery, routing, the Blazor circuit and the MVC filters all read HttpContext.User.
//
// This sits inside the UseStatusCodePagesWithReExecute wrapper, so a bodiless 401 would be
// re-executed as the not-found page. None is reachable in PR 6a: every 401 originates from
// ResultExtensions with a ProblemDetails body, and no endpoint is [Authorize], so the bearer
// handler never issues a challenge. AuthPipelineTests locks that in. The bodiless challenge 401
// becomes reachable in PR 6b, together with the OnChallenge body that answers it.
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Required for WebApplicationFactory<Program> access
public partial class Program { }
