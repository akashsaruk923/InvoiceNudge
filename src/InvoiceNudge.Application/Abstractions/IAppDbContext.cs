using InvoiceNudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceNudge.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Client> Clients { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<ReminderSequence> ReminderSequences { get; }
    DbSet<ReminderStep> ReminderSteps { get; }
    DbSet<ReminderLog> ReminderLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
