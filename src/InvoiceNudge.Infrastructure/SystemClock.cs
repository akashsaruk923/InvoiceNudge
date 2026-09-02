using InvoiceNudge.Application.Common;

namespace InvoiceNudge.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
