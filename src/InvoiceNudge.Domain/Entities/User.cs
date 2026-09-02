namespace InvoiceNudge.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PlanTier PlanTier { get; set; } = PlanTier.Free;
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Client> Clients { get; set; } = new List<Client>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<ReminderSequence> ReminderSequences { get; set; } = new List<ReminderSequence>();
}
