using InvoiceNudge.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace InvoiceNudge.Infrastructure.Email;

/// <summary>
/// Default sender used when no email provider is configured: it logs the message and
/// reports success, so the reminder pipeline is fully exercisable locally without keys.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _log;

    public LoggingEmailSender(ILogger<LoggingEmailSender> log) => _log = log;

    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _log.LogInformation(
            "[EMAIL - not actually sent, no provider configured]\n  To: {ToName} <{ToEmail}>\n  Subject: {Subject}\n  Body: {Body}",
            message.ToName, message.ToEmail, message.Subject, message.HtmlBody);
        return Task.FromResult(EmailSendResult.Ok($"logged-{Guid.NewGuid():N}"));
    }
}
