using InvoiceNudge.Domain.Entities;

namespace InvoiceNudge.Application.Reminders;

/// <summary>Data exposed to reminder subject/body templates.</summary>
public sealed class ReminderTemplateModel
{
    public required string ClientName { get; init; }
    public required string SenderName { get; init; }
    public required string InvoiceNumber { get; init; }
    public required string AmountFormatted { get; init; }
    public required string OutstandingFormatted { get; init; }
    public required string DueDate { get; init; }
    public required int DaysUntilDue { get; init; }
    public required int DaysOverdue { get; init; }
    public string? PaymentUrl { get; init; }

    public static ReminderTemplateModel From(Invoice invoice, DateTime nowUtc)
    {
        var client = invoice.Client ?? throw new InvalidOperationException("Invoice.Client not loaded.");
        var sender = invoice.User ?? throw new InvalidOperationException("Invoice.User not loaded.");
        var daysDelta = (int)Math.Round((invoice.DueDateUtc.Date - nowUtc.Date).TotalDays);

        return new ReminderTemplateModel
        {
            ClientName = client.Name,
            SenderName = sender.DisplayName,
            InvoiceNumber = invoice.Number,
            AmountFormatted = FormatMoney(invoice.AmountMinor, invoice.Currency),
            OutstandingFormatted = FormatMoney(invoice.OutstandingMinor, invoice.Currency),
            DueDate = invoice.DueDateUtc.ToString("dd MMM yyyy"),
            DaysUntilDue = Math.Max(0, daysDelta),
            DaysOverdue = Math.Max(0, -daysDelta),
            PaymentUrl = invoice.PaymentUrl
        };
    }

    public static string FormatMoney(long minor, string currency)
    {
        var major = minor / 100m;
        var symbol = currency switch
        {
            "INR" => "₹",
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            _ => currency + " "
        };
        return symbol + major.ToString("N2");
    }
}
