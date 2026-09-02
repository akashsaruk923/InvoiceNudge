namespace InvoiceNudge.Domain;

public enum PlanTier
{
    Free = 0,
    Pro = 1
}

public enum InvoiceStatus
{
    Draft = 0,
    Sent = 1,
    PartiallyPaid = 2,
    Paid = 3,
    WrittenOff = 4
}

public enum ReminderChannel
{
    Email = 0,
    WhatsApp = 1
}

public enum ReminderTone
{
    Friendly = 0,
    Neutral = 1,
    Firm = 2
}

public enum ReminderLogStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3
}
