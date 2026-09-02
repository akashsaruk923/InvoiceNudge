using InvoiceNudge.Application.Abstractions;
using InvoiceNudge.Application.Common;
using InvoiceNudge.Domain;
using Microsoft.EntityFrameworkCore;

namespace InvoiceNudge.Application.Invoices;

public sealed record DashboardStats(
    long OutstandingMinor,
    long OverdueMinor,
    int OpenInvoiceCount,
    int OverdueInvoiceCount,
    long CollectedLast30DaysMinor,
    double? AverageDaysToPayment,
    string Currency);

public sealed class DashboardService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public DashboardService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<DashboardStats> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .Select(i => new
            {
                i.AmountMinor,
                i.AmountPaidMinor,
                i.Status,
                i.DueDateUtc,
                i.IssueDateUtc,
                i.PaidAtUtc,
                i.Currency
            })
            .ToListAsync(ct);

        var open = invoices
            .Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.WrittenOff)
            .ToList();

        var outstanding = open.Sum(i => Math.Max(0, i.AmountMinor - i.AmountPaidMinor));
        var overdue = open.Where(i => i.DueDateUtc < now).ToList();

        var paidRecently = invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidAtUtc >= thirtyDaysAgo)
            .ToList();

        double? avgDays = null;
        var paidWithDates = invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidAtUtc is not null)
            .Select(i => (i.PaidAtUtc!.Value - i.IssueDateUtc).TotalDays)
            .ToList();
        if (paidWithDates.Count > 0)
            avgDays = Math.Round(paidWithDates.Average(), 1);

        return new DashboardStats(
            OutstandingMinor: outstanding,
            OverdueMinor: overdue.Sum(i => Math.Max(0, i.AmountMinor - i.AmountPaidMinor)),
            OpenInvoiceCount: open.Count,
            OverdueInvoiceCount: overdue.Count,
            CollectedLast30DaysMinor: paidRecently.Sum(i => i.AmountPaidMinor),
            AverageDaysToPayment: avgDays,
            Currency: invoices.Select(i => i.Currency).FirstOrDefault() ?? "INR");
    }
}
