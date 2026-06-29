# Nexa Email Blast

A small **C# / .NET 8 console utility** that sends the AG ONE Marketplace
launch campaign from **Nexa `<nexa@aventragroup.com>`**. It renders four
ready-made HTML emails (pixel-matched to the approved designs) and delivers them
to **AG All Employee** (or a test address) either immediately or on a configured
schedule.

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

## Interactive console (default)

Just run it with no arguments to get a menu where you can send now, schedule,
send a test, inspect the outgoing request like an API call, and flip debug /
dry-run on the fly:

```bash
cd src/NexaEmailBlast
dotnet run
```

```
  Provider : Microsoft Graph (graph.microsoft.com, ...)
  Endpoint : POST https://graph.microsoft.com/v1.0/users/nexa@aventragroup.com/sendMail
  DryRun   : True      Debug: False      Recipients: 2
   1) Show plan & config
   2) Show recipients
   3) Preview emails (render HTML)
   4) Inspect request (API-style dump, no send)   <- see exact Graph JSON / SMTP MIME
   5) Send TEST to a single address
   6) Send ALL emails now
   7) Send ONE email now
   8) Send at a custom date/time
   9) Run the configured SCHEDULE
   D) Toggle Debug      R) Toggle DryRun      P) Switch provider
   Q) Quit
```

- **Inspect (4)** prints the actual request body — Graph `sendMail` JSON (with the
  image bytes omitted) or the SMTP MIME headers — without sending anything.
- **Test (5)** delivers one chosen email to an address you type (defaults to
  `zain.abbas@aventragroup.com`).
- **Custom time (8)** waits until a date/time you enter, then sends.
- **Debug (D)** adds per-recipient, API-style request logging.
- **DryRun (R)** flips between "render only" and live sending.

## Quick start (non-interactive)

```bash
cd src/NexaEmailBlast

# Preview the designs (writes self-contained .html files to ./preview)
dotnet run -- preview

# See the plan (schedule + recipients + provider/config)
dotnet run -- plan

# Inspect the exact outgoing request for one email (no send)
dotnet run -- inspect email4

# Send (DryRun is ON by default — nothing leaves your machine)
dotnet run -- send --now              # send all 4 right now
dotnet run -- send                    # wait for each scheduled time, then send
dotnet run -- send --now --debug      # send everything now with verbose logging
dotnet run -- send --now --email email4   # just the launch email
```

### Go live
1. Open `appsettings.json`.
2. Set `Sending.DryRun` to `false`.
3. Choose the channel with `Sending.Provider`:
   - **`Graph`** (default) — Microsoft Graph API, app-only auth. Fill in
     `Graph.TenantId`, `Graph.ClientId`, `Graph.ClientSecret`.
   - **`Smtp`** — fill in `Smtp.Username` / `Smtp.Password`.
4. Confirm the recipients in `Recipients` (live `ToEmail`, `TestEmail`).
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

## Images (`Assets/` folder — no CDN)

You do **not** need a CDN or public image URLs. Put your PNG files here:

```
src/NexaEmailBlast/Assets/
  ag_one_logo.png    ← header logo
  nexa_sphere.png    ← Nexa ball
  nexa_wordmark.png  ← NeXa wordmark
  footer.png         ← footer band
  card_bg.png        ← card gradient background
```

When you send, each image is **attached inside the email** automatically. Replace
any file (same name), run again — done. Leave `Branding.CardBackgroundUrl` empty
in `appsettings.json`.

---

## Recipients

There is no CSV — recipients live in `appsettings.json`:

```jsonc
"Recipients": {
  "ToName": "AG All Employee",
  "ToEmail": "allemployee@aventragroup.com",   // live target for every email
  "TestName": "Zain Abbas",
  "TestEmail": "zain.abbas@aventragroup.com",  // used by the "Send TEST" actions
  "Greeting": "Aventrian",                       // shows as "Hi Aventrian," in the body
  "Cc": "",
  "Bcc": ""
}
```

- **Live** sends (menu 7/8/9/S, or `dotnet run -- send`) go to `ToEmail`
  (`AG All Employee <allemployee@aventragroup.com>`).
- **Test** sends (menu 5/6) go to `TestEmail`
  (`zain.abbas@aventragroup.com`) so you can preview the exact same emails first.
- The body greeting is always `Hi {Greeting},` (defaults to `Aventrian`).
- Optional global `Cc` / `Bcc` (comma-separated) apply to every message.

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
| `Recipients` | `ToName`, `ToEmail` | Live target (AG All Employee) |
| `Recipients` | `TestName`, `TestEmail` | Test target (zain.abbas@...) |
| `Recipients` | `Greeting` | Greeting word shown as `Hi {Greeting},` |
| `Recipients` | `Cc`, `Bcc` | Optional global copy lists |
| `Feedback` | `Url` | Shareable link behind the word **feedback** in the launch email |
| `Sending` | `DryRun` | `true` = render to `./preview` only, no send |
| `Sending` | `ThrottleMillisecondsBetweenEmails` | Pause between messages |
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
├─ Program.cs                 # entry point (menu | preview | plan | inspect | send)
├─ appsettings.json           # all configuration (incl. recipients)
├─ Assets/                    # brand images, embedded inline via Content-ID
│  ├─ ag_one_logo.png         #   header logo            ({{IMG:agone}})
│  ├─ nexa_sphere.png         #   Nexa ball / mascot     ({{IMG:ball}})
│  ├─ nexa_wordmark.png       #   "NeXa" wordmark        ({{IMG:nexaword}})
│  ├─ footer.png              #   whole Aventra footer   ({{IMG:footer}})
│  ├─ card_bg.png             #   tall card gradient (email1, email4)
│  └─ card_bg_short.png       #   short card gradient (email2, email3)
├─ Templates/
│  ├─ _layout.html            # shared header (AG ONE) + dark footer
│  ├─ email1_partner.html
│  ├─ email2_teaser.html
│  ├─ email3_hint.html
│  └─ email4_launch.html
├─ Models/                    # config + recipient models
└─ Services/
   ├─ EmailTemplateRenderer.cs# layout + fragment composition / token replace
   ├─ IEmailSender.cs         # delivery-channel abstraction
   ├─ EmailSenderFactory.cs   # picks Graph or SMTP from config
   ├─ GraphEmailSender.cs     # Microsoft Graph API (app-only), inline attachment
   ├─ EmailSender.cs          # MailKit SMTP, inline CID image
   ├─ CampaignRunner.cs       # send now / scheduled / test / inspect + debug
   └─ InteractiveMenu.cs      # the interactive console (REPL)
```

- **Brand images:** the AG ONE header logo, the Nexa ball, the "NeXa" wordmark
  and the entire Aventra footer are real images under `Assets/`, referenced in the
  templates with `{{IMG:agone}}`, `{{IMG:ball}}`, `{{IMG:nexaword}}`, `{{IMG:footer}}`.
  **To use the official artwork, just replace those four PNG files in `Assets/`
  with your own** (keep the same filenames) — the code embeds whatever is there.
  Only the images a template actually references are attached to that email.
- **Card gradient:** uses `Assets/card_bg.png`, embedded inline in every email (no CDN).
  The card shows the gradient via a visible `<img>` (Gmail/mobile), VML (Outlook),
  and cell `background` fallback. Recipients must have **images enabled** in their
  mail client to see graphics (same as any HTML newsletter).
- **Templates** use table-based, inline-styled HTML for broad email-client
  compatibility. Images are embedded inline (Content-ID) when sending, and as
  base64 data-URIs in `preview`/dry-run files so they open standalone.
- **Packages:** `Microsoft.Graph` + `Azure.Identity` (Graph API), `MailKit` (SMTP),
  `Microsoft.Extensions.Configuration.*`.
