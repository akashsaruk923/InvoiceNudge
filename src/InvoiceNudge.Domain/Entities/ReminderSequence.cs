namespace InvoiceNudge.Domain.Entities;

public class ReminderSequence
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Null = a built-in system template shared by all users.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    public ICollection<ReminderStep> Steps { get; set; } = new List<ReminderStep>();
}
