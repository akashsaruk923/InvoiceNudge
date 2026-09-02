namespace InvoiceNudge.Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string Number { get; set; } = string.Empty;

    /// <summary>Amount in minor units (paise / cents). Never use floating point for money.</summary>
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "INR";

    public DateTime IssueDateUtc { get; set; }
    public DateTime DueDateUtc { get; set; }

    public string? PaymentUrl { get; set; }
    public string? Notes { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public long AmountPaidMinor { get; set; }
    public DateTime? PaidAtUtc { get; set; }

    public Guid? ReminderSequenceId { get; set; }
    public ReminderSequence? ReminderSequence { get; set; }
    public bool RemindersPaused { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<ReminderLog> ReminderLogs { get; set; } = new List<ReminderLog>();

    public long OutstandingMinor => Math.Max(0, AmountMinor - AmountPaidMinor);

    public void MarkPaid(DateTime nowUtc)
    {
        AmountPaidMinor = AmountMinor;
        Status = InvoiceStatus.Paid;
        PaidAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }
}
