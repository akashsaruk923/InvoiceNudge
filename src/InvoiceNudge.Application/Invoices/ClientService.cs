using InvoiceNudge.Application.Abstractions;
using InvoiceNudge.Application.Common;
using InvoiceNudge.Domain;
using InvoiceNudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceNudge.Application.Invoices;

public sealed record CreateClientRequest(string Name, string ContactEmail, string? ContactPhone);

public sealed class ClientService
{
    // Free plan cap — enforced here so the UI and API can't diverge.
    public const int FreePlanActiveClientLimit = 3;

    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public ClientService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task<List<Client>> ListAsync(Guid userId, bool includeArchived = false, CancellationToken ct = default)
        => _db.Clients
            .AsNoTracking()
            .Where(c => c.UserId == userId && (includeArchived || !c.IsArchived))
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    public async Task<Result<Client>> CreateAsync(Guid userId, CreateClientRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Result<Client>.Fail("Client name is required.");
        if (string.IsNullOrWhiteSpace(req.ContactEmail) || !req.ContactEmail.Contains('@'))
            return Result<Client>.Fail("A valid contact email is required.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return Result<Client>.Fail("User not found.");

        if (user.PlanTier == PlanTier.Free)
        {
            var activeCount = await _db.Clients.CountAsync(c => c.UserId == userId && !c.IsArchived, ct);
            if (activeCount >= FreePlanActiveClientLimit)
                return Result<Client>.Fail(
                    $"The free plan is limited to {FreePlanActiveClientLimit} active clients. Upgrade to Pro for unlimited.");
        }

        var client = new Client
        {
            UserId = userId,
            Name = req.Name.Trim(),
            ContactEmail = req.ContactEmail.Trim(),
            ContactPhone = string.IsNullOrWhiteSpace(req.ContactPhone) ? null : req.ContactPhone.Trim(),
            CreatedAtUtc = _clock.UtcNow
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);
        return Result<Client>.Ok(client);
    }

    public async Task<Result> ArchiveAsync(Guid userId, Guid clientId, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.UserId == userId, ct);
        if (client is null)
            return Result.Fail("Client not found.");

        client.IsArchived = true;
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
