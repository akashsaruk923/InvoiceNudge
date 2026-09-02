using InvoiceNudge.Application.Abstractions;
using InvoiceNudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceNudge.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<ReminderSequence> ReminderSequences => Set<ReminderSequence>();
    public DbSet<ReminderStep> ReminderSteps => Set<ReminderStep>();
    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(200);
        });

        b.Entity<Client>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.ContactEmail).HasMaxLength(320).IsRequired();
            e.Property(x => x.ContactPhone).HasMaxLength(32);
            e.HasOne(x => x.User).WithMany(u => u.Clients)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.IsArchived });
        });

        b.Entity<Invoice>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).HasMaxLength(64).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.PaymentUrl).HasMaxLength(2048);
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.Ignore(x => x.OutstandingMinor);
            e.HasOne(x => x.User).WithMany(u => u.Invoices)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Client).WithMany(c => c.Invoices)
                .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReminderSequence).WithMany()
                .HasForeignKey(x => x.ReminderSequenceId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.UserId, x.Number }).IsUnique();
            e.HasIndex(x => new { x.Status, x.DueDateUtc });
        });

        b.Entity<ReminderSequence>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.User).WithMany(u => u.ReminderSequences)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.IsDefault });
        });

        b.Entity<ReminderStep>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SubjectTemplate).HasMaxLength(500).IsRequired();
            e.Property(x => x.BodyTemplate).HasMaxLength(8000).IsRequired();
            e.HasOne(x => x.ReminderSequence).WithMany(s => s.Steps)
                .HasForeignKey(x => x.ReminderSequenceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ReminderLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ProviderMessageId).HasMaxLength(256);
            e.Property(x => x.FailureReason).HasMaxLength(2000);
            e.HasOne(x => x.Invoice).WithMany(i => i.ReminderLogs)
                .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ReminderStep).WithMany()
                .HasForeignKey(x => x.ReminderStepId).OnDelete(DeleteBehavior.SetNull);
            // Idempotency guard: at most one log row per (invoice, step).
            e.HasIndex(x => new { x.InvoiceId, x.ReminderStepId }).IsUnique();
        });
    }
}
