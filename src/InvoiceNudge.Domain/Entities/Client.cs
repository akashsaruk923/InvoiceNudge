namespace InvoiceNudge.Domain.Entities;

public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
