# InvoiceNudge

Automated payment-reminder tool for freelancers and small agencies. Add a client and an
invoice; InvoiceNudge emails the client on an escalating schedule (3 days before due, on
the due date, then +7 and +14 days) until the invoice is marked paid. A dashboard shows
outstanding vs. overdue amounts, collections, and average days-to-payment.

## Why this exists

Small service businesses lose weeks chasing unpaid invoices because follow-up is manual
and awkward. This automates the nagging.

**Business model:** free for up to 3 active clients; Pro (unlimited clients, WhatsApp
reminders, custom branding) is the paid tier. The free-plan cap lives in
`ClientService.FreePlanActiveClientLimit`.

## Architecture

| Project | Role |
|---|---|
| `InvoiceNudge.Domain` | Entities + enums, no dependencies |
| `InvoiceNudge.Application` | Use cases, `ComputeDueReminders` (pure reminder-decision logic), service classes |
| `InvoiceNudge.Infrastructure` | EF Core (`AppDbContext`), Postgres/SQLite provider selection, Brevo email, Scriban templating |
| `InvoiceNudge.Web` | Blazor Server UI, cookie auth, `ReminderDispatchService` background worker, `/internal/dispatch-reminders` endpoint |
| `InvoiceNudge.Application.Tests` | xUnit tests for the reminder logic |

The reminder pass is driven two ways (both safe to run together — a per-`(invoice, step)`
unique log row makes it idempotent):

1. `ReminderDispatchService`, an in-process `BackgroundService` (interval:
   `Reminders:IntervalMinutes`, default 15).
2. `POST /internal/dispatch-reminders` with header `X-Dispatch-Secret: <Reminders:DispatchSecret>`,
   for an external cron (see `.github/workflows/reminders-cron.yml`).

## Run locally

```bash
dotnet run --project src/InvoiceNudge.Web
# http://localhost:5021  — sign in with any email (passwordless dev login)
```

With no `DATABASE_URL` set it uses a local SQLite file (`invoicenudge.db`) and creates the
schema automatically. With no Brevo key set, reminder emails are written to the log instead
of sent, so the whole flow is testable offline.

```bash
dotnet test          # run the reminder-logic tests
```

## Configuration

| Key / env var | Purpose | Default |
|---|---|---|
| `DATABASE_URL` | Postgres connection (libpq URL or Npgsql string). Absent → SQLite. | – |
| `Reminders__DispatchSecret` | Guards the external dispatch endpoint | – |
| `Reminders__RunInProcessWorker` | Toggle the background worker | `true` |
| `Reminders__IntervalMinutes` | Worker interval | `15` |
| `Email__Brevo__ApiKey` | Brevo API key (absent → emails logged only) | – |
| `Email__Brevo__SenderEmail` / `__SenderName` | Verified sender in Brevo | – |
| `Authentication__Google__ClientId` / `__ClientSecret` | Optional Google OAuth | – |
| `Auth__AllowPasswordlessLogin` | Email-only sign-in (turn off once Google is set up) | `true` |

(Double underscore `__` is the env-var form of a nested `:` config key.)

## Deploy free (Render + Neon)

1. **Database — [Neon](https://neon.tech)** (free, no card): create a project, copy the
   connection string.
2. **Host — [Render](https://render.com)** (free web service): *New → Blueprint*, pick this
   repo. `render.yaml` provisions the service. Set these env vars when prompted:
   - `DATABASE_URL` → the Neon connection string
   - `Email__Brevo__ApiKey`, `Email__Brevo__SenderEmail` → from [Brevo](https://www.brevo.com) (300 emails/day free)
   - `Reminders__DispatchSecret` is auto-generated; copy its value.
3. **Backup cron — GitHub Actions:** move the workflow files into place
   (`mkdir -p .github && git mv deploy/github-workflows .github/workflows && git commit -m "enable workflows" && git push`
   — needs a token with `workflow` scope, e.g. `gh auth refresh -s workflow`), then in the
   repo settings add secrets `APP_BASE_URL` (your Render URL) and `DISPATCH_SECRET` (same
   value as `Reminders__DispatchSecret`). `reminders-cron.yml` then pokes the app every
   30 min in case the free web service has idled out.

Migrations run automatically on startup (`DbSeeder.MigrateAndSeedAsync`).

## Roadmap

- Razorpay payment links + "paid" webhook auto-reconciliation
- WhatsApp reminders (Pro)
- Custom reminder-sequence editor in the UI
- GST-compliant invoice PDF generation
- Stripe/Razorpay subscription billing for Pro
