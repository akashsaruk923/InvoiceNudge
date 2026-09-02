namespace InvoiceNudge.Domain.Entities;

public class ReminderLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public Guid? ReminderStepId { get; set; }
    public ReminderStep? ReminderStep { get; set; }

    public ReminderChannel Channel { get; set; }
    public DateTime ScheduledForUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }

    public ReminderLogStatus Status { get; set; } = ReminderLogStatus.Pending;
    public string? ProviderMessageId { get; set; }
    public string? FailureReason { get; set; }
}
