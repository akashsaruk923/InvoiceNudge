using InvoiceNudge.Application.Reminders;

namespace InvoiceNudge.Web.BackgroundServices;

/// <summary>
/// Runs <see cref="ReminderDispatcher"/> on a fixed interval. Cheap enough to host inside
/// the web process for a solo/free deployment; split into its own service when you scale out.
/// </summary>
public sealed class ReminderDispatchService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ReminderDispatchService> _log;

    public ReminderDispatchService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ReminderDispatchService> log)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(
            Math.Clamp(_config.GetValue("Reminders:IntervalMinutes", 15), 1, 1440));

        // Small startup delay so migrations/seed finish first.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<ReminderDispatcher>();
                    var summary = await dispatcher.RunAsync(stoppingToken);
                    if (summary.RemindersSent > 0 || summary.RemindersFailed > 0)
                        _log.LogInformation(
                            "Reminder pass: {Scanned} invoices, {Sent} sent, {Failed} failed",
                            summary.InvoicesScanned, summary.RemindersSent, summary.RemindersFailed);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "Reminder dispatch pass failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
