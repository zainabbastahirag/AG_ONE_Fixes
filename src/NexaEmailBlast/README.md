# Nexa Email Blast

A small **C# / .NET 8 console utility** that bulk-sends the AG ONE Marketplace
launch campaign from **Nexa `<nexa@aventragroup.com>`**. It reads recipients from
a CSV, renders four ready-made HTML emails (pixel-matched to the approved
designs), and delivers them either immediately or on a configured schedule.

> Everything is driven by `appsettings.json` — no code changes needed to run it.

---

## The campaign (4 emails)

| # | Key | Default time | Theme |
|---|-----|--------------|-------|
| 1 | `email1` | **30 Jun, 6:00 PM** | "What if you had a partner…" + *Hey, I'm Nexa* |
| 2 | `email2` | **1 Jul, 10:00 AM** | "Something new is coming… Two letters. Big impact." |
| 3 | `email3` | **1 Jul, 11:00 AM** | "Ready for another hint? …the number that brings everything together." |
| 4 | `email4` | **1 Jul, 12:00 PM** | **Launch** — full AG ONE Marketplace announcement with a clickable, shareable **feedback** link |

> The launch schedule was given two slightly different ways in the brief
> (e.g. 6 PM vs 10 AM for the first email). The times above follow the
> "Email Blast Schedule" list and are **fully configurable** in
> `appsettings.json` → `Campaign[].SendAtLocal`.

---

## Quick start

```bash
cd src/NexaEmailBlast

# 1) Preview the designs (writes self-contained .html files to ./preview)
dotnet run -- preview

# 2) See the plan (schedule + recipient count + config)
dotnet run -- plan

# 3) Send (DryRun is ON by default — nothing leaves your machine)
dotnet run -- send --now          # send all 4 right now
dotnet run -- send                # wait for each scheduled time, then send
dotnet run -- send --now --email email4   # just the launch email
```

### Go live
1. Open `appsettings.json`.
2. Set `Sending.DryRun` to `false`.
3. Choose the channel with `Sending.Provider`:
   - **`Graph`** (default) — Microsoft Graph API, app-only auth. Fill in
     `Graph.TenantId`, `Graph.ClientId`, `Graph.ClientSecret`.
   - **`Smtp`** — fill in `Smtp.Username` / `Smtp.Password`.
4. Point `Recipients.CsvPath` at your real list.
5. Run `dotnet run -- send` and leave it running until 1 Jul 12 PM, or use
   `--now` to fire immediately.

### Microsoft Graph setup (app-only)
The default provider sends via `https://graph.microsoft.com`. Create an Entra ID
app registration and grant it the **application** permission `Mail.Send`
(admin-consented), then create a client secret.

```jsonc
"Sending": { "Provider": "Graph" },
"Graph": {
  "Host": "graph.microsoft.com",
  "TenantId": "<your-tenant-id>",
  "ClientId": "<application-client-id>",
  "ClientSecret": "<client-secret>",
  "Scope": "https://graph.microsoft.com/.default",
  "SaveToSentItems": true
}
```

Mail is sent as the configured `Sender.Email` (`nexa@aventragroup.com`) using
`POST /users/{sender}/sendMail`. Because this is app-only, the app registration
must be allowed to send as that mailbox (tenant-wide `Mail.Send`, or scoped with
an Exchange Application Access Policy). The Nexa mascot is delivered as an inline
`fileAttachment` (Content-ID `nexa_sphere`) so it renders in the body.

Secrets can be kept out of the file via environment variables:

```bash
NEXA_Graph__ClientSecret='•••' dotnet run -- send --now
```

---

## Recipients CSV

Copy `recipients.sample.csv` to `recipients.csv` and edit. Headers are
case-insensitive; only `Email` is required, `Name` is optional.

```csv
Email,Name
allemployee@aventragroup.com,AG All Employee
zain.abbas@aventragroup.com,Zain Abbas
```

- One email is sent **per row** so the greeting can be personalised.
- Rows with no `Name` fall back to the greeting word `Aventrian`
  (`Recipients.GreetingFallback`).
- Duplicate / invalid emails are skipped automatically.
- If the CSV is missing or empty, the campaign falls back to a single default
  recipient (`Recipients.DefaultToEmail`, default `zain.abbas@aventragroup.com`)
  so you can test safely.

You can also set a global `Cc` / `Bcc` (comma-separated) in `appsettings.json`.

---

## Configuration (`appsettings.json`)

| Section | Key | Meaning |
|---------|-----|---------|
| `Sending` | `Provider` | `Graph` (Microsoft Graph API, default) or `Smtp` |
| `Graph` | `Host` | Graph host (`graph.microsoft.com`) |
| `Graph` | `TenantId`, `ClientId`, `ClientSecret` | App-only credentials (`Mail.Send`) |
| `Graph` | `Scope`, `SaveToSentItems` | OAuth scope / keep a copy in Sent Items |
| `Smtp` | `Host`, `Port` | Mail server (default Office 365 `smtp.office365.com:587`) |
| `Smtp` | `Security` | `starttls` (default), `ssl`, `none`, or `auto` |
| `Smtp` | `Username`, `Password` | SMTP credentials for `nexa@aventragroup.com` |
| `Sender` | `Name`, `Email` | From header (Nexa) |
| `Recipients` | `CsvPath` | Path to the recipient CSV (relative paths resolve next to the app) |
| `Recipients` | `DefaultToEmail`, `DefaultToName` | Fallback recipient when the CSV is empty |
| `Recipients` | `GreetingFallback` | Greeting used when a row has no name |
| `Recipients` | `Cc`, `Bcc` | Optional global copy lists |
| `Feedback` | `Url` | Shareable link behind the word **feedback** in the launch email |
| `Sending` | `DryRun` | `true` = render to `./preview` only, no SMTP |
| `Sending` | `ThrottleMillisecondsBetweenEmails` | Pause between recipients |
| `Campaign[]` | `Subject`, `Preheader`, `Template`, `SendAtLocal` | Per-email content & timing |

Any setting can be overridden with environment variables using the `NEXA_`
prefix and `__` for nesting, e.g.:

```bash
NEXA_Smtp__Password='•••' NEXA_Sending__DryRun=false dotnet run -- send --now
```

---

## How it's built

```
NexaEmailBlast/
├─ Program.cs                 # CLI entry point (preview | plan | send)
├─ appsettings.json           # all configuration
├─ recipients.sample.csv      # example list
├─ Assets/nexa_sphere.png     # Nexa mascot (embedded inline via Content-ID)
├─ Templates/
│  ├─ _layout.html            # shared header (AG ONE) + dark footer
│  ├─ email1_partner.html
│  ├─ email2_teaser.html
│  ├─ email3_hint.html
│  └─ email4_launch.html
├─ Models/                    # config + recipient models
└─ Services/
   ├─ CsvRecipientReader.cs   # CsvHelper-based loader
   ├─ EmailTemplateRenderer.cs# layout + fragment composition / token replace
   ├─ IEmailSender.cs         # delivery-channel abstraction
   ├─ EmailSenderFactory.cs   # picks Graph or SMTP from config
   ├─ GraphEmailSender.cs     # Microsoft Graph API (app-only), inline attachment
   ├─ EmailSender.cs          # MailKit SMTP, inline CID image
   └─ CampaignRunner.cs       # scheduling + orchestration
```

- **Templates** use table-based, inline-styled HTML for broad email-client
  compatibility. The Nexa sphere is embedded inline (Content-ID) when sending,
  and as a base64 data-URI in `preview`/dry-run files so they open standalone.
- **Packages:** `Microsoft.Graph` + `Azure.Identity` (Graph API), `MailKit` (SMTP),
  `CsvHelper` (CSV), `Microsoft.Extensions.Configuration.*`.
