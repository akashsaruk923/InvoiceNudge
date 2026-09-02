using System.Security.Claims;
using InvoiceNudge.Application.Invoices;
using InvoiceNudge.Domain.Entities;
using Microsoft.AspNetCore.Components.Authorization;

namespace InvoiceNudge.Web.Services;

/// <summary>Resolves the signed-in <see cref="User"/> for the current circuit / request.</summary>
public sealed class CurrentUser
{
    private readonly AuthenticationStateProvider _authState;
    private readonly UserService _users;
    private User? _cached;

    public CurrentUser(AuthenticationStateProvider authState, UserService users)
    {
        _authState = authState;
        _users = users;
    }

    public async Task<User?> GetAsync(CancellationToken ct = default)
    {
        if (_cached is not null)
            return _cached;

        var state = await _authState.GetAuthenticationStateAsync();
        var principal = state.User;
        if (principal.Identity?.IsAuthenticated != true)
            return null;

        var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("email")
                    ?? principal.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var name = principal.FindFirstValue(ClaimTypes.GivenName)
                   ?? principal.FindFirstValue("name")
                   ?? principal.Identity?.Name;

        _cached = await _users.GetOrCreateAsync(email, name, ct);
        return _cached;
    }

    public async Task<Guid> RequireIdAsync(CancellationToken ct = default)
    {
        var user = await GetAsync(ct);
        return user?.Id ?? throw new InvalidOperationException("No authenticated user.");
    }
}
