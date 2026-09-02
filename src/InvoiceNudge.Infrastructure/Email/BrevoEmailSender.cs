using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InvoiceNudge.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceNudge.Infrastructure.Email;

public sealed class BrevoOptions
{
    public const string SectionName = "Email:Brevo";
    public string ApiKey { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "InvoiceNudge";
}

/// <summary>Sends transactional email through Brevo (https://api.brevo.com/v3/smtp/email).</summary>
public sealed class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly BrevoOptions _options;
    private readonly ILogger<BrevoEmailSender> _log;

    public BrevoEmailSender(HttpClient http, IOptions<BrevoOptions> options, ILogger<BrevoEmailSender> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var payload = new BrevoSendRequest
        {
            Sender = new BrevoContact { Email = _options.SenderEmail, Name = _options.SenderName },
            To = [new BrevoContact { Email = message.ToEmail, Name = message.ToName }],
            Subject = message.Subject,
            HtmlContent = message.HtmlBody,
            ReplyTo = string.IsNullOrWhiteSpace(message.ReplyToEmail)
                ? null
                : new BrevoContact { Email = message.ReplyToEmail!, Name = message.ReplyToName ?? message.ReplyToEmail! }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("api-key", _options.ApiKey);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Brevo returned {Status}: {Body}", (int)response.StatusCode, body);
                return EmailSendResult.Fail($"Brevo {(int)response.StatusCode}: {body}");
            }

            var parsed = System.Text.Json.JsonSerializer.Deserialize<BrevoSendResponse>(body);
            return EmailSendResult.Ok(parsed?.MessageId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Brevo send threw");
            return EmailSendResult.Fail(ex.Message);
        }
    }

    private sealed class BrevoSendRequest
    {
        [JsonPropertyName("sender")] public BrevoContact Sender { get; set; } = new();
        [JsonPropertyName("to")] public List<BrevoContact> To { get; set; } = [];
        [JsonPropertyName("replyTo")] public BrevoContact? ReplyTo { get; set; }
        [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
        [JsonPropertyName("htmlContent")] public string HtmlContent { get; set; } = string.Empty;
    }

    private sealed class BrevoContact
    {
        [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }

    private sealed class BrevoSendResponse
    {
        [JsonPropertyName("messageId")] public string? MessageId { get; set; }
    }
}
