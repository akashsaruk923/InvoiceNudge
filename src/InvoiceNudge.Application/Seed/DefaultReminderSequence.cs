using InvoiceNudge.Domain;
using InvoiceNudge.Domain.Entities;

namespace InvoiceNudge.Application.Seed;

/// <summary>The built-in reminder sequence every new user gets until they customise their own.</summary>
public static class DefaultReminderSequence
{
    public static readonly Guid SystemSequenceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static ReminderSequence Build()
    {
        var seq = new ReminderSequence
        {
            Id = SystemSequenceId,
            UserId = null,
            Name = "Standard (−3, due, +7, +14)",
            IsDefault = true
        };

        seq.Steps.Add(Step(-3, ReminderTone.Friendly,
            "Upcoming: invoice {{ invoice_number }} due {{ due_date }}",
            "<p>Hi {{ client_name }},</p>"
            + "<p>A quick heads-up that invoice <strong>{{ invoice_number }}</strong> for "
            + "{{ amount_formatted }} is due on <strong>{{ due_date }}</strong> "
            + "({{ days_until_due }} days away).</p>"
            + "{{ if payment_url }}<p><a href=\"{{ payment_url }}\">Pay now</a></p>{{ end }}"
            + "<p>Thanks,<br>{{ sender_name }}</p>"));

        seq.Steps.Add(Step(0, ReminderTone.Neutral,
            "Due today: invoice {{ invoice_number }}",
            "<p>Hi {{ client_name }},</p>"
            + "<p>Invoice <strong>{{ invoice_number }}</strong> for {{ outstanding_formatted }} "
            + "is due today.</p>"
            + "{{ if payment_url }}<p><a href=\"{{ payment_url }}\">Pay now</a></p>{{ end }}"
            + "<p>Thanks,<br>{{ sender_name }}</p>"));

        seq.Steps.Add(Step(7, ReminderTone.Neutral,
            "Overdue: invoice {{ invoice_number }} ({{ days_overdue }} days)",
            "<p>Hi {{ client_name }},</p>"
            + "<p>Invoice <strong>{{ invoice_number }}</strong> for {{ outstanding_formatted }} "
            + "was due on {{ due_date }} and is now {{ days_overdue }} days overdue.</p>"
            + "<p>Could you let me know when payment will be made?</p>"
            + "{{ if payment_url }}<p><a href=\"{{ payment_url }}\">Pay now</a></p>{{ end }}"
            + "<p>Thanks,<br>{{ sender_name }}</p>"));

        seq.Steps.Add(Step(14, ReminderTone.Firm,
            "Second notice: invoice {{ invoice_number }} {{ days_overdue }} days overdue",
            "<p>Hi {{ client_name }},</p>"
            + "<p>This is a second reminder that invoice <strong>{{ invoice_number }}</strong> "
            + "for {{ outstanding_formatted }} is {{ days_overdue }} days overdue.</p>"
            + "<p>Please arrange payment within 3 business days or get in touch to discuss.</p>"
            + "{{ if payment_url }}<p><a href=\"{{ payment_url }}\">Pay now</a></p>{{ end }}"
            + "<p>Regards,<br>{{ sender_name }}</p>"));

        return seq;
    }

    private static ReminderStep Step(int offsetDays, ReminderTone tone, string subject, string body) => new()
    {
        Id = DeterministicStepId(offsetDays),
        ReminderSequenceId = SystemSequenceId,
        OffsetDays = offsetDays,
        Channel = ReminderChannel.Email,
        Tone = tone,
        SubjectTemplate = subject,
        BodyTemplate = body
    };

    private static Guid DeterministicStepId(int offsetDays)
    {
        // Stable ids so re-seeding doesn't create duplicate steps.
        Span<byte> bytes = stackalloc byte[16];
        bytes[0] = 0x22;
        BitConverter.TryWriteBytes(bytes[4..], offsetDays);
        return new Guid(bytes);
    }
}
