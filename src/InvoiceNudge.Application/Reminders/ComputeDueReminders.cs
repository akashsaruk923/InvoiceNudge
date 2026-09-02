using InvoiceNudge.Domain;
using InvoiceNudge.Domain.Entities;

namespace InvoiceNudge.Application.Reminders;

/// <summary>
/// Pure decision logic: given an invoice, its reminder sequence, the reminders already
/// logged for it, and the current time, returns the steps that should fire now.
///
/// This is the one piece that must never double-send or spam a client, so it is kept
/// free of I/O and covered by unit tests.
/// </summary>
public static class ComputeDueReminders
{
    public static IReadOnlyList<ReminderStep> ForInvoice(
        Invoice invoice,
        ReminderSequence sequence,
        IReadOnlyCollection<ReminderLog> existingLogs,
        DateTime nowUtc)
    {
        if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.WrittenOff)
            return [];

        if (invoice.RemindersPaused)
            return [];

        if (invoice.OutstandingMinor <= 0)
            return [];

        var handledStepIds = existingLogs
            .Where(l => l.ReminderStepId is not null
                        && l.Status is ReminderLogStatus.Sent or ReminderLogStatus.Pending)
            .Select(l => l.ReminderStepId!.Value)
            .ToHashSet();

        return sequence.Steps
            .Where(step => !handledStepIds.Contains(step.Id))
            .Where(step => invoice.DueDateUtc.AddDays(step.OffsetDays) <= nowUtc)
            .OrderBy(step => step.OffsetDays)
            .ToList();
    }
}
