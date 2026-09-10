using System.Threading.RateLimiting;
using Envanex.Application;
using Envanex.Infrastructure;
using Envanex.Web.Components;
using Envanex.Web.DataSource;
using Envanex.Web.Middleware;
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
