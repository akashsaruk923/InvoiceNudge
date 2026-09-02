namespace InvoiceNudge.Application.Abstractions;

public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string? ReplyToEmail = null,
    string? ReplyToName = null);

public sealed record EmailSendResult(bool Succeeded, string? ProviderMessageId, string? Error)
{
    public static EmailSendResult Ok(string? id) => new(true, id, null);
    public static EmailSendResult Fail(string error) => new(false, null, error);
}

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
