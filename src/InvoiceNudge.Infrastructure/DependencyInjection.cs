using InvoiceNudge.Application.Abstractions;
using InvoiceNudge.Application.Common;
using InvoiceNudge.Infrastructure.Email;
using InvoiceNudge.Infrastructure.Persistence;
using InvoiceNudge.Infrastructure.Templating;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceNudge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var (provider, connectionString) = ResolveDatabase(config);

        services.AddDbContext<AppDbContext>(options =>
        {
            if (provider == DbProvider.Postgres)
                options.UseNpgsql(connectionString, npg => npg.MigrationsAssembly("InvoiceNudge.Infrastructure"));
            else
                options.UseSqlite(connectionString, sql => sql.MigrationsAssembly("InvoiceNudge.Infrastructure"));
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ITemplateRenderer, ScribanTemplateRenderer>();

        // Email: Brevo when an API key is present, otherwise a logging no-op sender.
        var brevoKey = config[$"{BrevoOptions.SectionName}:ApiKey"];
        if (!string.IsNullOrWhiteSpace(brevoKey))
        {
            services.Configure<BrevoOptions>(config.GetSection(BrevoOptions.SectionName));
            services.AddHttpClient<IEmailSender, BrevoEmailSender>(http =>
            {
                http.BaseAddress = new Uri("https://api.brevo.com/");
                http.Timeout = TimeSpan.FromSeconds(30);
            });
        }
        else
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        }

        return services;
    }

    private enum DbProvider { Sqlite, Postgres }

    private static (DbProvider, string) ResolveDatabase(IConfiguration config)
    {
        // Priority: DATABASE_URL (host convention) -> ConnectionStrings:Postgres -> SQLite file.
        var url = config["DATABASE_URL"] ?? config.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(url))
            return (DbProvider.Postgres, NormalizePostgres(url));

        var sqlitePath = config.GetConnectionString("Sqlite") ?? "Data Source=invoicenudge.db";
        return (DbProvider.Sqlite, sqlitePath);
    }

    /// <summary>Accepts either a libpq URL (postgres://user:pass@host:port/db) or a raw Npgsql string.</summary>
    private static string NormalizePostgres(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = Npgsql.SslMode.Require
        };

        // Honour an explicit sslmode from the query string if present.
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<Npgsql.SslMode>(kv[1], true, out var parsed))
                builder.SslMode = parsed;
        }

        return builder.ConnectionString;
    }
}
