using FluentAssertions;
using InvoiceNudge.Application.Reminders;
using InvoiceNudge.Domain;
using InvoiceNudge.Domain.Entities;

namespace InvoiceNudge.Application.Tests;

public class ComputeDueRemindersTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc);

    private static ReminderSequence Sequence(params int[] offsets)
    {
        var seq = new ReminderSequence { Id = Guid.NewGuid(), Name = "test" };
        foreach (var o in offsets)
            seq.Steps.Add(new ReminderStep
            {
                Id = Guid.NewGuid(),
                ReminderSequenceId = seq.Id,
                OffsetDays = o,
                SubjectTemplate = "s",
                BodyTemplate = "b"
            });
        return seq;
    }

    private static Invoice Invoice(DateTime dueUtc, InvoiceStatus status = InvoiceStatus.Sent) => new()
    {
        Id = Guid.NewGuid(),
        Number = "INV-1",
        AmountMinor = 100_00,
        DueDateUtc = dueUtc,
        Status = status
    };

    [Fact]
    public void Returns_steps_whose_fire_date_has_passed()
    {
        var invoice = Invoice(Now.AddDays(2));           // due in 2 days
        var seq = Sequence(-3, 0, 7);                     // -3 fires 1 day ago; 0 and +7 are future

        var due = ComputeDueReminders.ForInvoice(invoice, seq, [], Now);

        due.Select(s => s.OffsetDays).Should().Equal(-3);
    }

    [Fact]
    public void Returns_all_past_steps_in_offset_order_when_far_overdue()
    {
        var invoice = Invoice(Now.AddDays(-30));
        var seq = Sequence(7, -3, 0, 14);

        var due = ComputeDueReminders.ForInvoice(invoice, seq, [], Now);

        due.Select(s => s.OffsetDays).Should().Equal(-3, 0, 7, 14);
    }

    [Fact]
    public void Skips_steps_that_already_have_a_sent_or_pending_log()
    {
        var invoice = Invoice(Now.AddDays(-30));
        var seq = Sequence(-3, 0, 7);
        var sentStep = seq.Steps.First(s => s.OffsetDays == -3);
        var pendingStep = seq.Steps.First(s => s.OffsetDays == 0);

        var logs = new[]
        {
            new ReminderLog { InvoiceId = invoice.Id, ReminderStepId = sentStep.Id, Status = ReminderLogStatus.Sent },
            new ReminderLog { InvoiceId = invoice.Id, ReminderStepId = pendingStep.Id, Status = ReminderLogStatus.Pending }
        };

        var due = ComputeDueReminders.ForInvoice(invoice, seq, logs, Now);

        due.Select(s => s.OffsetDays).Should().Equal(7);
    }

    [Fact]
    public void Retries_a_step_whose_previous_attempt_failed()
    {
        var invoice = Invoice(Now.AddDays(-10));
        var seq = Sequence(0);
        var step = seq.Steps.Single();
        var logs = new[]
        {
            new ReminderLog { InvoiceId = invoice.Id, ReminderStepId = step.Id, Status = ReminderLogStatus.Failed }
        };

        var due = ComputeDueReminders.ForInvoice(invoice, seq, logs, Now);

        due.Should().ContainSingle().Which.OffsetDays.Should().Be(0);
    }

    [Theory]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.WrittenOff)]
    public void Sends_nothing_for_closed_invoices(InvoiceStatus status)
    {
        var invoice = Invoice(Now.AddDays(-30), status);
        var due = ComputeDueReminders.ForInvoice(invoice, Sequence(-3, 0, 7), [], Now);
        due.Should().BeEmpty();
    }

    [Fact]
    public void Sends_nothing_when_reminders_are_paused()
    {
        var invoice = Invoice(Now.AddDays(-30));
        invoice.RemindersPaused = true;
        var due = ComputeDueReminders.ForInvoice(invoice, Sequence(-3, 0), [], Now);
        due.Should().BeEmpty();
    }

    [Fact]
    public void Sends_nothing_when_nothing_is_outstanding()
    {
        var invoice = Invoice(Now.AddDays(-30));
        invoice.AmountPaidMinor = invoice.AmountMinor;
        var due = ComputeDueReminders.ForInvoice(invoice, Sequence(-3, 0), [], Now);
        due.Should().BeEmpty();
    }

    [Fact]
    public void Does_not_fire_a_step_exactly_before_its_moment()
    {
        // due in exactly 3 days, step at -3 => fires at Now precisely; boundary is inclusive.
        var invoice = Invoice(Now.AddDays(3));
        var due = ComputeDueReminders.ForInvoice(invoice, Sequence(-3), [], Now);
        due.Should().ContainSingle();

        var notYet = ComputeDueReminders.ForInvoice(Invoice(Now.AddDays(3).AddSeconds(1)), Sequence(-3), [], Now);
        notYet.Should().BeEmpty();
    }
}
