using InvoiceNudge.Application.Abstractions;
using InvoiceNudge.Application.Common;
using InvoiceNudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceNudge.Application.Invoices;

public sealed class UserService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public UserService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Finds the user by email or creates one. Used by the auth callback.</summary>
    public async Task<User> GetOrCreateAsync(string email, string? displayName, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is not null)
            return user;

        user = new User
        {
            Email = normalized,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalized.Split('@')[0] : displayName.Trim(),
            CreatedAtUtc = _clock.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public Task<User?> FindAsync(Guid id, CancellationToken ct = default)
        => _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
}
