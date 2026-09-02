using InvoiceNudge.Application.Abstractions;
using InvoiceNudge.Application.Common;
using InvoiceNudge.Domain;
using InvoiceNudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceNudge.Application.Invoices;

public sealed record CreateInvoiceRequest(
    Guid ClientId,
    string Number,
    decimal Amount,
    string Currency,
    DateTime IssueDateUtc,
    DateTime DueDateUtc,
    string? PaymentUrl,
    string? Notes);

public sealed class InvoiceService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public InvoiceService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task<List<Invoice>> ListAsync(Guid userId, CancellationToken ct = default)
        => _db.Invoices
            .AsNoTracking()
            .Include(i => i.Client)
            .Where(i => i.UserId == userId)
            .OrderBy(i => i.Status == InvoiceStatus.Paid)
            .ThenBy(i => i.DueDateUtc)
            .ToListAsync(ct);

    public async Task<Result<Invoice>> CreateAsync(Guid userId, CreateInvoiceRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Number))
            return Result<Invoice>.Fail("Invoice number is required.");
        if (req.Amount <= 0)
            return Result<Invoice>.Fail("Amount must be greater than zero.");
        if (req.DueDateUtc.Date < req.IssueDateUtc.Date)
            return Result<Invoice>.Fail("Due date cannot be before the issue date.");

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId && c.UserId == userId, ct);
        if (client is null)
            return Result<Invoice>.Fail("Client not found.");

        var duplicate = await _db.Invoices.AnyAsync(
            i => i.UserId == userId && i.Number == req.Number.Trim(), ct);
        if (duplicate)
            return Result<Invoice>.Fail($"Invoice number '{req.Number.Trim()}' already exists.");

        var now = _clock.UtcNow;
        var invoice = new Invoice
        {
            UserId = userId,
            ClientId = req.ClientId,
            Number = req.Number.Trim(),
            AmountMinor = ToMinor(req.Amount),
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "INR" : req.Currency.Trim().ToUpperInvariant(),
            IssueDateUtc = DateTime.SpecifyKind(req.IssueDateUtc, DateTimeKind.Utc),
            DueDateUtc = DateTime.SpecifyKind(req.DueDateUtc, DateTimeKind.Utc),
            PaymentUrl = string.IsNullOrWhiteSpace(req.PaymentUrl) ? null : req.PaymentUrl.Trim(),
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            Status = InvoiceStatus.Sent,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        return Result<Invoice>.Ok(invoice);
    }

    public async Task<Result> MarkPaidAsync(Guid userId, Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.UserId == userId, ct);
        if (invoice is null)
            return Result.Fail("Invoice not found.");

        invoice.MarkPaid(_clock.UtcNow);

        // Cancel any reminders that haven't gone out yet.
        var pending = await _db.ReminderLogs
            .Where(l => l.InvoiceId == invoiceId && l.Status == ReminderLogStatus.Pending)
            .ToListAsync(ct);
        foreach (var log in pending)
            log.Status = ReminderLogStatus.Skipped;

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetRemindersPausedAsync(Guid userId, Guid invoiceId, bool paused, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.UserId == userId, ct);
        if (invoice is null)
            return Result.Fail("Invoice not found.");

        invoice.RemindersPaused = paused;
        invoice.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static long ToMinor(decimal major) => (long)decimal.Round(major * 100m, 0, MidpointRounding.AwayFromZero);
}
