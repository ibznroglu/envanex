using System.Text.Json;
using System.Threading.RateLimiting;
using Envanex.Application;
using Envanex.Infrastructure;
using Envanex.Web.Components;
using Envanex.Web.DataSource;
using Envanex.Web.Middleware;
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

var rateLimitingSection = builder.Configuration.GetSection("RateLimiting");
bool rateLimitingEnabled = rateLimitingSection.GetValue("Enabled", true);

if (rateLimitingEnabled)
{
    int permitLimit = rateLimitingSection.GetValue("PermitLimit", 100);
    int windowSeconds = rateLimitingSection.GetValue("WindowSeconds", 60);

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

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
            RateLimitPartition.GetFixedWindowLimiter("global", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
            }));
    });
}

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

if (rateLimitingEnabled)
{
    app.UseRateLimiter();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Required for WebApplicationFactory<Program> access
public partial class Program { }
