# Homecrest Weather Alert

Internal ASP.NET Core (Razor Pages) app for Homecrest staff to compose weather alert text and send **individual** Twilio SMS messages to an editable employee phone list. Hosted on IIS (Windows Server 2022).

## Features

- Cookie authentication (ASP.NET Core Identity) with roles **User** and **Admin**
- Compose & send: one Twilio `MessageResource.Create` **per recipient** (never group SMS); only **active** employees are messaged
- Employee directory: searchable name + phone CRUD; CSV template + upload; bulk active/inactive/delete; US → E.164; SMS **STOP** opt-out (via Twilio Message **poll**, not a public webhook) sets inactive and records `OptedOutAt`
- Admin user management: create, disable/enable, reset password (set or **email reset link**), grant/revoke **Admin** role (Admin-only). The seed account `admin@homecrest.local` cannot be disabled or demoted.
- **Integrations** (Admin): Exchange Online / Microsoft Graph email + Twilio credentials, editable without redeploy
- Identity `IEmailSender` sends via Graph `Mail.Send` (password reset / welcome emails)

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (local development)
- On the server: [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0) for IIS + ASP.NET Core Module V2
- Twilio account (trial is fine for testing; only verified destinations receive SMS on trial)
- Entra ID app registration with Graph **Mail.Send** (application) for Exchange Online mail

## Default admin

On first run the app seeds:

| Field | Value |
| --- | --- |
| Email | `admin@homecrest.local` |
| Password | from env `HOMECREST_ADMIN_PASSWORD`, or config `Seed:AdminPassword`, or fallback **`ChangeMe!Admin1`** |

**Change this password immediately** after first login (Manage account), or set `HOMECREST_ADMIN_PASSWORD` / `Seed:AdminPassword` before the first start so the seed uses your secret.

Override email with `Seed:AdminEmail` if needed.

Public self-registration is disabled; only admins create logins under **Users**.

Auth cookie: **sliding** expiration, **30 minutes** (`ConfigureApplicationCookie`). Idle users are signed out after 30 minutes with no requests.

### Microsoft Graph / Exchange Online (email)

For password-reset and welcome email (when the Exchange Online Integrations feature is deployed), create an Entra app registration with Graph **Mail.Send** (application) and admin consent. Step-by-step:

**[docs/graph-enterprise-app-setup.md](docs/graph-enterprise-app-setup.md)** — app registration, client secret, sender mailbox, optional Application Access Policy, IIS / Admin Integrations values.

## Local run

```bash
dotnet restore
dotnet run --launch-profile http
```

Open http://localhost:5055 and sign in with the seeded admin.

### Twilio config (do not commit real secrets)

Authenticate with an **API key** (preferred). Placeholders live in `appsettings.json`. Prefer User Secrets or environment variables:

```bash
dotnet user-secrets set "Twilio:AccountSid" "ACxxxxxxxx"
dotnet user-secrets set "Twilio:ApiKey" "SKxxxxxxxx"
dotnet user-secrets set "Twilio:ApiSecret" "your_api_key_secret"
dotnet user-secrets set "Twilio:FromNumber" "+1xxxxxxxxxx"
```

Or environment variables (double underscore):

```bash
export Twilio__AccountSid=ACxxxxxxxx
export Twilio__ApiKey=SKxxxxxxxx
export Twilio__ApiSecret=your_api_key_secret
export Twilio__FromNumber=+1xxxxxxxxxx
export HOMECREST_ADMIN_PASSWORD='YourStrong!Pass1'
```

| Config key | Env var | Notes |
| --- | --- | --- |
| `Twilio:AccountSid` | `Twilio__AccountSid` | Account SID (`AC…`) |
| `Twilio:ApiKey` | `Twilio__ApiKey` | API Key SID (`SK…`) — preferred for send **and** Message reads |
| `Twilio:ApiSecret` | `Twilio__ApiSecret` | API Key secret |
| `Twilio:AuthToken` | `Twilio__AuthToken` | Optional send fallback if API key unset |
| `Twilio:FromNumber` | `Twilio__FromNumber` | E.164 From number |

If both API key and Auth Token are present for sending, the app uses the **API key**.

**API key permissions:** the key must be allowed to **create Messages** (send) and **list/read Messages** (opt-out poll). A Standard key on the account that owns the From number is sufficient.

### Exchange Online email (Microsoft Graph)

App-only client credentials send as a mailbox (e.g. `noreply@homecrest…`) via `POST /users/{SenderAddress}/sendMail`. Used for Identity password-reset / welcome mail and the Integrations **Test connection** action.

#### Azure app registration

1. Entra ID → **App registrations** → New registration (single tenant is fine).
2. **Certificates & secrets** → New client secret; copy the value once.
3. **API permissions** → Microsoft Graph → **Application** permissions → **`Mail.Send`** → **Grant admin consent**.
4. Create or pick the sender mailbox in Exchange Online (shared or user mailbox). The app sends **as** that address.
5. (Recommended) Restrict app-only mail with an [Exchange Online application access policy](https://learn.microsoft.com/en-us/graph/auth-limit-mailbox-access) so `Mail.Send` can only use the noreply mailbox.

Token scope used by the app: `https://graph.microsoft.com/.default` (permissions come from the app registration, not from a scopes text box).

| Config key | Env var | Notes |
| --- | --- | --- |
| `ExchangeOnline:TenantId` | `ExchangeOnline__TenantId` | Directory (tenant) ID |
| `ExchangeOnline:ClientId` | `ExchangeOnline__ClientId` | Application (client) ID |
| `ExchangeOnline:ClientSecret` | `ExchangeOnline__ClientSecret` | Client secret value |
| `ExchangeOnline:SenderAddress` | `ExchangeOnline__SenderAddress` | Mailbox UPN/SMTP (From) |

```bash
dotnet user-secrets set "ExchangeOnline:TenantId" "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
dotnet user-secrets set "ExchangeOnline:ClientId" "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
dotnet user-secrets set "ExchangeOnline:ClientSecret" "your-secret"
dotnet user-secrets set "ExchangeOnline:SenderAddress" "noreply@homecrest.com"
```

#### Admin Integrations UI & secret storage

Admins open **Integrations** to view/edit Exchange Online and Twilio settings:

- Client secrets / API secrets are **write-only** (masked when already set). Paste a new value to rotate **without redeploy**.
- **Test connection** acquires a Graph token; optional test recipient sends a real message.
- Values are merged over appsettings/env and saved to **`App_Data/integrations.protected`**, encrypted with ASP.NET Core **Data Protection** (keys in `App_Data/keys`). Not stored as plaintext in SQLite.
- Grant the IIS app pool identity **Modify** on `App_Data\` (same idea as the SQLite folder). Back up `App_Data\keys` with the protected file — losing keys makes the overlay unreadable (fall back to env/User Secrets and re-save).

### STOP / opt-out (Twilio poll — no public webhook)

There is **no inbound webhook**. The app polls Twilio’s Messages API for inbound SMS **to** the From number.

When an inbound body matches **STOP** / `STOPALL` / `UNSUBSCRIBE` / `CANCEL` / `END` / `QUIT` (case-insensitive):

1. Match `From` → employee phone (E.164)
2. Set `IsActive = false` and `OptedOutAt` (UTC, first opt-out kept)
3. Compose skips inactive employees

**When the poll runs**

| Trigger | Behavior |
| --- | --- |
| Click **Send individual SMS** | Polls immediately before building the recipient list (skips newly opted-out people), then optionally again after send |

There is no manual sync UI and no poll when merely opening Compose. Lookback: since `OptOutSyncState.LastPolledAt`, or **7 days** on the first run. No Twilio Console webhook URL is required (and none should be exposed for this app).

### Connection string (SQLite → another store)

Default (SQLite file next to the app):

```json
"ConnectionStrings": {
  "DefaultConnection": "DataSource=app.db;Cache=Shared"
}
```

To use SQL Server instead:

1. Change the connection string, for example:

   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=YOUR_SERVER;Database=HomecrestWeatherAlert;Trusted_Connection=True;TrustServerCertificate=True"
   }
   ```

2. In `Program.cs`, swap `UseSqlite(connectionString)` for `UseSqlServer(connectionString)`.
3. Add package `Microsoft.EntityFrameworkCore.SqlServer`, remove or keep Sqlite as needed.
4. Add a new migration / update the database (`dotnet ef database update`).

For IIS, you can also set the connection string via environment variable:

`ConnectionStrings__DefaultConnection=DataSource=C:\apps\HomecrestWeatherAlert\data\app.db;Cache=Shared`

Ensure the app pool identity can write the SQLite file directory.

## IIS publish (Windows Server 2022)

1. Install the **ASP.NET Core 8 Hosting Bundle** and reboot if prompted.
2. Publish from a build machine:

   ```bash
   dotnet publish -c Release -o .\publish
   ```

3. Copy `publish\` to the server (e.g. `C:\inetpub\HomecrestWeatherAlert`).
4. In IIS Manager:
   - Create an Application Pool: **No Managed Code**, Integrated pipeline.
   - Create a site (or application) pointing at the publish folder; assign that pool.
5. Set environment variables on the site or app pool (Configuration Editor → `system.webServer/aspNetCore` → `environmentVariables`), or use `web.config` / Azure App Settings–style machine env:
   - `Twilio__AccountSid`
   - `Twilio__ApiKey`
   - `Twilio__ApiSecret`
   - `Twilio__FromNumber`
   - Optional: `Twilio__AuthToken` (send fallback only)
   - `ExchangeOnline__TenantId`, `ExchangeOnline__ClientId`, `ExchangeOnline__ClientSecret`, `ExchangeOnline__SenderAddress` (or configure later under Admin → Integrations)
   - `HOMECREST_ADMIN_PASSWORD` (first run only, if not already seeded)
   - Optional: `ConnectionStrings__DefaultConnection`, `ASPNETCORE_ENVIRONMENT=Production`
6. Grant the app pool identity **Modify** on the folder that holds `app.db` **and** `App_Data\` (Data Protection keys + encrypted integrations overlay).
7. Bind HTTPS with a certificate; keep HTTP→HTTPS redirect enabled by the app.
8. **Do not** configure a public inbound SMS webhook for this app — opt-outs are polled outbound via the Messages API.

`web.config` in the repo shows the ANCM `inprocess` hosting model. Published output includes a generated `web.config` as well.

## Send behavior / privacy

- Only **active** employees with a valid normalized phone are messaged.
- Send results show **masked** phones (`***-***-1234`), never a full list meant for forwarding.
- Twilio trial accounts only deliver to **verified** destination numbers.

## Project layout

- `Pages/Compose` — message + send status; polls Twilio for STOP opt-outs on send only
- `Pages/Employees` — CRUD + search + CSV upload/template + bulk active/inactive/delete
- `Pages/Admin/Users` — admin-only login management (+ email reset link)
- `Pages/Admin/Config` — Integrations (Exchange Online + Twilio; encrypted overlay)
- `Services/GraphEmailSender.cs` — Identity `IEmailSender` via Graph `Mail.Send`
- `Services/IntegrationsSettingsService.cs` — Data Protection–encrypted settings overlay
- `Services/TwilioOptOutPollService.cs` — MessageResource.Read poll for inbound STOP
- `Services/TwilioSmsSender.cs` — one Create per recipient
- `Services/PhoneNormalizer.cs` — US → E.164
- `Data/SeedData.cs` — roles + default admin
