# Homecrest Weather Alert

Internal ASP.NET Core (Razor Pages) app for Homecrest staff to compose weather alert text and send **individual** Twilio SMS messages to an editable employee phone list. Hosted on IIS (Windows Server 2022).

## Features

- Cookie authentication (ASP.NET Core Identity) with roles **User** and **Admin**
- Compose & send: one Twilio `MessageResource.Create` **per recipient** (never group SMS); only **active** employees are messaged
- Employee directory: searchable name + phone CRUD; US → E.164; SMS **STOP** opt-out sets inactive and records `OptedOutAt`
- Admin user management: create, disable/enable, reset password, grant/revoke **Admin** role (Admin-only). The seed account `admin@homecrest.local` cannot be disabled or demoted.
- Twilio Account SID, API Key + secret, Auth Token (webhook signatures), and From number are **config-only**

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (local development)
- On the server: [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0) for IIS + ASP.NET Core Module V2
- Twilio account (trial is fine for testing; only verified destinations receive SMS on trial)

## Default admin

On first run the app seeds:

| Field | Value |
| --- | --- |
| Email | `admin@homecrest.local` |
| Password | from env `HOMECREST_ADMIN_PASSWORD`, or config `Seed:AdminPassword`, or fallback **`ChangeMe!Admin1`** |

**Change this password immediately** after first login (Manage account), or set `HOMECREST_ADMIN_PASSWORD` / `Seed:AdminPassword` before the first start so the seed uses your secret.

Override email with `Seed:AdminEmail` if needed.

Public self-registration is disabled; only admins create logins under **Users**.

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
dotnet user-secrets set "Twilio:AuthToken" "your_auth_token"
dotnet user-secrets set "Twilio:FromNumber" "+1xxxxxxxxxx"
```

Or environment variables (double underscore):

```bash
export Twilio__AccountSid=ACxxxxxxxx
export Twilio__ApiKey=SKxxxxxxxx
export Twilio__ApiSecret=your_api_key_secret
export Twilio__AuthToken=your_auth_token
export Twilio__FromNumber=+1xxxxxxxxxx
export HOMECREST_ADMIN_PASSWORD='YourStrong!Pass1'
```

| Config key | Env var | Notes |
| --- | --- | --- |
| `Twilio:AccountSid` | `Twilio__AccountSid` | Account SID (`AC…`) |
| `Twilio:ApiKey` | `Twilio__ApiKey` | API Key SID (`SK…`) — preferred for **sending** |
| `Twilio:ApiSecret` | `Twilio__ApiSecret` | API Key secret |
| `Twilio:AuthToken` | `Twilio__AuthToken` | Required in **Production** to validate inbound webhook signatures; also send fallback if API key unset |
| `Twilio:FromNumber` | `Twilio__FromNumber` | E.164 From number |

If both API key and Auth Token are present for sending, the app uses the **API key**. Auth Token is still needed for webhook signature validation.

### Inbound STOP / opt-out webhook

When someone texts **STOP** (also `STOPALL`, `UNSUBSCRIBE`, `CANCEL`, `END`, `QUIT`) to the Twilio From number, the app:

1. Matches `From` to an employee phone (E.164)
2. Sets `IsActive = false` and `OptedOutAt` (UTC)
3. Future Compose sends skip them (active-only filter)

**Webhook path:** `POST /api/twilio/sms` (anonymous; Twilio signature validated when `Twilio:AuthToken` is set)

**Twilio Console setup**

1. Open the Messaging phone number (or Messaging Service) used as From.
2. Under **Messaging** → **A message comes in**, set:
   - Webhook URL: `https://{your-iis-host}/api/twilio/sms`
   - Method: **HTTP POST**
3. Ensure the site is reachable on HTTPS from the internet (or use a tunnel for local testing).
4. On IIS behind HTTPS termination, forwarded headers (`X-Forwarded-Proto` / `X-Forwarded-Host`) are honored so signature validation uses the public URL Twilio called.

Empty TwiML is returned; carriers/Twilio still send their standard opt-out confirmation to the user.

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
   - `Twilio__AuthToken` (webhook signature validation)
   - `Twilio__FromNumber`
   - `HOMECREST_ADMIN_PASSWORD` (first run only, if not already seeded)
   - Optional: `ConnectionStrings__DefaultConnection`, `ASPNETCORE_ENVIRONMENT=Production`
6. Grant the app pool identity **Modify** on the folder that holds `app.db` (or your configured data path).
7. Bind HTTPS with a certificate; keep HTTP→HTTPS redirect enabled by the app.
8. Point the Twilio number’s inbound SMS webhook at `https://{host}/api/twilio/sms` (POST).

`web.config` in the repo shows the ANCM `inprocess` hosting model. Published output includes a generated `web.config` as well.

## Send behavior / privacy

- Only **active** employees with a valid normalized phone are messaged.
- Send results show **masked** phones (`***-***-1234`), never a full list meant for forwarding.
- Twilio trial accounts only deliver to **verified** destination numbers.

## Project layout

- `Pages/Compose` — message + send status (active employees only)
- `Pages/Employees` — CRUD + search + opt-out timestamp
- `Pages/Admin/Users` — admin-only login management
- `Controllers/TwilioSmsWebhookController.cs` — inbound STOP webhook (`/api/twilio/sms`)
- `Services/TwilioSmsSender.cs` — one Create per recipient
- `Services/PhoneNormalizer.cs` — US → E.164
- `Data/SeedData.cs` — roles + default admin
