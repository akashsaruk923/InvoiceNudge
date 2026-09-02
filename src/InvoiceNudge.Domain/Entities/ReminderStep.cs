namespace InvoiceNudge.Domain.Entities;

public class ReminderStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReminderSequenceId { get; set; }
    public ReminderSequence? ReminderSequence { get; set; }

    /// <summary>Days relative to the invoice due date. Negative = before due, 0 = on due date, positive = after.</summary>
    public int OffsetDays { get; set; }

    public ReminderChannel Channel { get; set; } = ReminderChannel.Email;
    public ReminderTone Tone { get; set; } = ReminderTone.Friendly;

    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
}
