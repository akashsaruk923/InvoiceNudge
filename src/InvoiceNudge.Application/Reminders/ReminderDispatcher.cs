using InvoiceNudge.Application.Abstractions;
using InvoiceNudge.Application.Common;
using InvoiceNudge.Domain;
using InvoiceNudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceNudge.Application.Reminders;

public sealed record DispatchSummary(int InvoicesScanned, int RemindersSent, int RemindersFailed);

/// <summary>
/// Orchestrates one pass: find open invoices, ask <see cref="ComputeDueReminders"/> which
/// steps are due, render them, send them, and write a <see cref="ReminderLog"/> row per attempt.
/// The log row is the idempotency guard — a step with a Sent/Pending log is never retried.
/// </summary>
public sealed class ReminderDispatcher
{
    private readonly IAppDbContext _db;
    private readonly IEmailSender _email;
    private readonly ITemplateRenderer _templates;
    private readonly IClock _clock;
    private readonly ILogger<ReminderDispatcher> _log;

    public ReminderDispatcher(
        IAppDbContext db,
        IEmailSender email,
        ITemplateRenderer templates,
        IClock clock,
        ILogger<ReminderDispatcher> log)
    {
        _db = db;
        _email = email;
        _templates = templates;
        _clock = clock;
        _log = log;
    }

    public async Task<DispatchSummary> RunAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var invoices = await _db.Invoices
            .Include(i => i.Client)
            .Include(i => i.User)
            .Include(i => i.ReminderLogs)
            .Where(i => i.Status != InvoiceStatus.Paid
                        && i.Status != InvoiceStatus.WrittenOff
                        && !i.RemindersPaused)
            .ToListAsync(ct);

        if (invoices.Count == 0)
            return new DispatchSummary(0, 0, 0);

        // Resolve each invoice's sequence (explicit, or the user's default, or the system default).
        var systemDefault = await LoadSequenceAsync(s => s.UserId == null && s.IsDefault, ct);

        var sent = 0;
        var failed = 0;

        foreach (var invoice in invoices)
        {
            ct.ThrowIfCancellationRequested();

            var sequence = invoice.ReminderSequenceId is { } seqId
                ? await LoadSequenceAsync(s => s.Id == seqId, ct)
                : await LoadSequenceAsync(s => s.UserId == invoice.UserId && s.IsDefault, ct)
                  ?? systemDefault;

            if (sequence is null)
            {
                _log.LogWarning("No reminder sequence resolved for invoice {InvoiceId}", invoice.Id);
                continue;
            }

            var dueSteps = ComputeDueReminders.ForInvoice(invoice, sequence, invoice.ReminderLogs.ToList(), now);

            foreach (var step in dueSteps)
            {
                var (ok, providerId, error) = await SendStepAsync(invoice, step, now, ct);
                _db.ReminderLogs.Add(new ReminderLog
                {
                    InvoiceId = invoice.Id,
                    ReminderStepId = step.Id,
                    Channel = step.Channel,
                    ScheduledForUtc = invoice.DueDateUtc.AddDays(step.OffsetDays),
                    SentAtUtc = ok ? now : null,
                    Status = ok ? ReminderLogStatus.Sent : ReminderLogStatus.Failed,
                    ProviderMessageId = providerId,
                    FailureReason = error
                });

                if (ok) sent++; else failed++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new DispatchSummary(invoices.Count, sent, failed);
    }

    private async Task<(bool ok, string? providerId, string? error)> SendStepAsync(
        Invoice invoice, ReminderStep step, DateTime now, CancellationToken ct)
    {
        if (step.Channel != ReminderChannel.Email)
            return (false, null, $"Channel {step.Channel} not supported yet");

        var client = invoice.Client!;
        if (string.IsNullOrWhiteSpace(client.ContactEmail))
            return (false, null, "Client has no contact email");

        try
        {
            var model = ReminderTemplateModel.From(invoice, now);
            var subject = _templates.Render(step.SubjectTemplate, model);
            var body = _templates.Render(step.BodyTemplate, model);

            var result = await _email.SendAsync(new EmailMessage(
                ToEmail: client.ContactEmail,
                ToName: client.Name,
                Subject: subject,
                HtmlBody: body,
                ReplyToEmail: invoice.User?.Email,
                ReplyToName: invoice.User?.DisplayName), ct);

            if (!result.Succeeded)
                _log.LogWarning("Reminder send failed for invoice {InvoiceId}: {Error}", invoice.Id, result.Error);

            return (result.Succeeded, result.ProviderMessageId, result.Error);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Unexpected error sending reminder for invoice {InvoiceId}", invoice.Id);
            return (false, null, ex.Message);
        }
    }

    private Task<ReminderSequence?> LoadSequenceAsync(
        System.Linq.Expressions.Expression<Func<ReminderSequence, bool>> predicate, CancellationToken ct)
        => _db.ReminderSequences
            .Include(s => s.Steps)
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate, ct);
}
