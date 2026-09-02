using InvoiceNudge.Application.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InvoiceNudge.Infrastructure.Persistence;

public static class DbSeeder
{
    /// <summary>Applies migrations and ensures the built-in reminder sequence exists.</summary>
    public static async Task MigrateAndSeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        // Migrations are authored for PostgreSQL (the production target). For a local SQLite
        // run there are no provider-matching migrations, so create the schema directly.
        var providerName = db.Database.ProviderName ?? string.Empty;
        if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            await db.Database.EnsureCreatedAsync(ct);
        else
            await db.Database.MigrateAsync(ct);

        var exists = await db.ReminderSequences
            .AnyAsync(s => s.Id == DefaultReminderSequence.SystemSequenceId, ct);
        if (!exists)
        {
            db.ReminderSequences.Add(DefaultReminderSequence.Build());
            await db.SaveChangesAsync(ct);
            log.LogInformation("Seeded system reminder sequence.");
        }
    }
}
