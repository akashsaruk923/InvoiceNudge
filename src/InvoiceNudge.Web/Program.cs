using InvoiceNudge.Application;
using InvoiceNudge.Infrastructure;
using InvoiceNudge.Infrastructure.Persistence;
using InvoiceNudge.Web.Auth;
using InvoiceNudge.Web.BackgroundServices;
using InvoiceNudge.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

var builder = WebApplication.CreateBuilder(args);

// Many hosts (Render, Railway, Heroku-style) inject the listen port as $PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://+:{port}");

// ---- Framework ----
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();

// ---- Auth ----
var authBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.Name = "invoicenudge_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.CallbackPath = "/auth/google-callback";
    });
}

builder.Services.AddAuthorization();

// ---- App + Infrastructure ----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Background reminder loop (in-process worker). Disable with Reminders:RunInProcessWorker=false
// when you prefer to drive dispatch from an external cron hitting /internal/dispatch-reminders.
if (builder.Configuration.GetValue("Reminders:RunInProcessWorker", true))
    builder.Services.AddHostedService<ReminderDispatchService>();

var app = builder.Build();

// ---- Pipeline ----
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Respect X-Forwarded-* so cookies/redirects work behind a platform proxy (Render, Koyeb, Fly).
app.UseForwardedHeaders(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                       | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// Most free hosts terminate TLS at their proxy; an in-app redirect there causes loops.
// Default off in production; set ForceHttpsRedirect=true to re-enable.
if (app.Configuration.GetValue("ForceHttpsRedirect", app.Environment.IsDevelopment()))
    app.UseHttpsRedirection();

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<InvoiceNudge.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapAuthEndpoints();
app.MapGet("/healthz", () => Results.Ok("ok"));

// Apply migrations + seed the built-in reminder sequence on startup.
await DbSeeder.MigrateAndSeedAsync(app.Services);

app.Run();
