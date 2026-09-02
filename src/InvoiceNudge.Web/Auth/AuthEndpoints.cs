using System.Security.Claims;
using InvoiceNudge.Application.Invoices;
using InvoiceNudge.Application.Reminders;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceNudge.Web.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        // Passwordless email sign-in for early access / demo. Enabled unless explicitly turned off.
        // For production, register Google OAuth (Authentication:Google:*) and disable this.
        group.MapPost("/login", async (
            HttpContext http,
            [FromForm] string email,
            [FromForm] string? name,
            [FromForm] string? returnUrl,
            UserService users,
            IConfiguration config) =>
        {
            if (!config.GetValue("Auth:AllowPasswordlessLogin", true))
                return Results.BadRequest("Passwordless login is disabled.");

            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return Results.BadRequest("A valid email is required.");

            var user = await users.GetOrCreateAsync(email, name);
            await SignInAsync(http, user.Email, user.DisplayName);
            var target = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') ? returnUrl : "/";
            return Results.LocalRedirect(target);
        }).DisableAntiforgery();

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/login");
        }).DisableAntiforgery();

        // Google OAuth (only meaningful when the provider is configured).
        group.MapGet("/google", (HttpContext http) =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = "/auth/google-complete" },
                [GoogleDefaults.AuthenticationScheme]));

        group.MapGet("/google-complete", async (HttpContext http, UserService users) =>
        {
            var result = await http.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var email = result.Principal?.FindFirstValue(ClaimTypes.Email);
            var name = result.Principal?.FindFirstValue(ClaimTypes.Name);
            if (!string.IsNullOrWhiteSpace(email))
            {
                var user = await users.GetOrCreateAsync(email, name);
                await SignInAsync(http, user.Email, user.DisplayName);
            }
            return Results.LocalRedirect("/");
        });

        // External trigger for the reminder pass — use this when running dispatch from a
        // cron (GitHub Actions, cron-job.org) instead of the in-process worker.
        app.MapPost("/internal/dispatch-reminders", async (
            HttpContext http,
            ReminderDispatcher dispatcher,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var expected = config["Reminders:DispatchSecret"];
            if (string.IsNullOrWhiteSpace(expected))
                return Results.Problem("Reminders:DispatchSecret is not configured.", statusCode: 500);

            var provided = http.Request.Headers["X-Dispatch-Secret"].ToString();
            if (!CryptographicEquals(provided, expected))
                return Results.Unauthorized();

            var summary = await dispatcher.RunAsync(ct);
            return Results.Ok(summary);
        }).DisableAntiforgery();
    }

    private static async Task SignInAsync(HttpContext http, string email, string displayName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, email),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, displayName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    private static bool CryptographicEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
