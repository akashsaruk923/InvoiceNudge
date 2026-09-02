using InvoiceNudge.Application.Invoices;
using InvoiceNudge.Application.Reminders;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceNudge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<UserService>();
        services.AddScoped<ClientService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ReminderDispatcher>();
        return services;
    }
}
