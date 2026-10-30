# Microsoft Entra app setup for Homecrest Weather Alert (Graph Mail.Send)

Admin walkthrough to create the **enterprise / app registration** Homecrest Weather Alert uses for **Exchange Online** email via Microsoft Graph (password reset, welcome mail, Integrations “Test connection”).

The app sends as a single mailbox (for example `noreply@yourdomain.com`) using **app-only** (client credentials) authentication — no interactive user sign-in for mail.

---

## What you will create

| Item | Purpose |
| --- | --- |
| Entra **App registration** | Application (client) ID + Directory (tenant) ID |
| **Client secret** | App password used by IIS / Admin → Integrations |
| Graph permission **Mail.Send** (Application) + **admin consent** | Allows `POST /users/{mailbox}/sendMail` |
| Exchange Online **sender mailbox** | The From address (shared or user mailbox) |
| Optional **Application Access Policy** (or App RBAC) | Limits the app so it can only send as that mailbox |

Values the Homecrest app needs (Admin → **Integrations**, or IIS env vars):

| Field in UI / config | Where it comes from |
| --- | --- |
| Tenant ID | Entra → App registration → Overview → Directory (tenant) ID |
| Client ID | Overview → Application (client) ID |
| Client secret | Certificates & secrets → client secret **Value** (copy once) |
| Sender mailbox | Exchange Online mailbox SMTP/UPN (e.g. `noreply@homecrest.com`) |

Env var names if you prefer IIS instead of the UI: `ExchangeOnline__TenantId`, `ExchangeOnline__ClientId`, `ExchangeOnline__ClientSecret`, `ExchangeOnline__SenderAddress`.

---

## 1. Register the application in Microsoft Entra ID

1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com/) as an admin who can create app registrations and grant consent.
2. Go to **Identity** → **Applications** → **App registrations** → **New registration**.
3. Name it clearly, e.g. `Homecrest-Weather-Alert-Mail`.
4. **Supported account types:** **Accounts in this organizational directory only** (single tenant) is appropriate for an internal IIS app.
5. Leave **Redirect URI** blank (not needed for client-credentials / app-only).
6. Click **Register**.
7. On **Overview**, copy and store:
   - **Application (client) ID**
   - **Directory (tenant) ID**

---

## 2. Create a client secret

1. In the app registration → **Certificates & secrets** → **Client secrets** → **New client secret**.
2. Add a description (e.g. `IIS Homecrest Weather Alert`) and an expiry that matches your rotation policy.
3. Click **Add**, then **immediately copy the Value** (not the Secret ID). You cannot see the value again later.
4. Plan to paste this into Homecrest **Admin → Integrations** (Client secret) or set `ExchangeOnline__ClientSecret` on the IIS site / app pool. You can rotate later from Integrations without redeploying.

Prefer a **certificate** instead of a secret for higher security if your ops process supports it; the Homecrest Integrations UI today is built around a **client secret**.

---

## 3. Grant Microsoft Graph Mail.Send (application) + admin consent

1. App registration → **API permissions** → **Add a permission**.
2. Choose **Microsoft Graph** → **Application permissions** (not Delegated).
3. Search for and select **`Mail.Send`**.
4. Click **Add permissions**.
5. Click **Grant admin consent for \<your tenant\>** and confirm. Status must show **Granted** for the organization.

Without admin consent, token acquisition or `sendMail` will fail even if the secret is correct.

**Scope the app uses at runtime:** `https://graph.microsoft.com/.default` (permissions come from this registration; you do not paste scopes into Homecrest).

---

## 4. Create or choose the sender mailbox

1. In Exchange Online / Microsoft 365 admin, create or pick a mailbox the app will send **as**, e.g.:
   - Shared mailbox: `noreply@homecrest.com`, or
   - Licensed user mailbox used only as a service address.
2. Confirm you can see the address in the Global Address List / Exchange admin center.
3. Use that exact SMTP address or UPN as **Sender mailbox** in Homecrest.

Homecrest calls Graph: `POST /users/{SenderAddress}/sendMail` — the sender must be a real mailbox in your tenant.

---

## 5. (Recommended) Limit mailbox access

By default, Application **Mail.Send** can send as **any** mailbox in the tenant. Restrict it.

### Option A — Application Access Policy (common for Mail.Send)

Microsoft documents this for limiting Graph app permissions (including `Mail.Send`) to a mail-enabled security group. See [Limit mailbox access](https://learn.microsoft.com/en-us/graph/auth-limit-mailbox-access).

1. Create a **mail-enabled security group** that contains **only** the noreply/sender mailbox (or its identity).
2. Connect to **Exchange Online PowerShell**.
3. Create a restrict policy (replace IDs/addresses):

```powershell
New-ApplicationAccessPolicy `
  -AppId "<YOUR_APPLICATION_CLIENT_ID>" `
  -PolicyScopeGroupId "hwa-mail-senders@yourdomain.com" `
  -AccessRight RestrictAccess `
  -Description "Homecrest Weather Alert may only send as noreply group members"
```

4. Test:

```powershell
Test-ApplicationAccessPolicy -AppId "<YOUR_APPLICATION_CLIENT_ID>" -Identity "noreply@yourdomain.com"
```

Expect access allowed for the sender mailbox and denied for a normal user mailbox.

> Note: Microsoft is moving toward **Role Based Access Control for Applications** in Exchange Online for new lockdown designs ([Application RBAC](https://learn.microsoft.com/en-us/Exchange/permissions-exo/application-rbac)). If your tenant already standardizes on App RBAC, use that instead of creating new Application Access Policies; the goal is the same — Homecrest’s app should only be able to send as the noreply mailbox.

---

## 6. Configure Homecrest (IIS and/or Admin Integrations)

### Prefer Admin UI (after deploy of Exchange Online / Integrations feature)

1. Sign in as an **Admin** (seed admin works).
2. Open **Integrations**.
3. Under **Exchange Online (Microsoft Graph)**, enter:
   - Tenant ID  
   - Client ID  
   - Client secret (write-only; leave masked to keep an existing secret, paste new value to rotate)  
   - Sender mailbox  
4. Save.
5. **Test connection** — without a recipient, only acquires an app token; with a test recipient, also sends a real test email.

Settings saved here go to an **encrypted** file under `App_Data/integrations.protected` (Data Protection keys in `App_Data/keys`). They **override** `ExchangeOnline__*` / appsettings for those fields; they do **not** rewrite IIS environment variables.

### Or set IIS environment variables

On the site or app pool (Configuration Editor → `system.webServer/aspNetCore` → `environmentVariables`), or in `web.config`:

- `ExchangeOnline__TenantId`
- `ExchangeOnline__ClientId`
- `ExchangeOnline__ClientSecret`
- `ExchangeOnline__SenderAddress`

Grant the app pool **Modify** on the app’s `App_Data` folder if you use the Integrations UI (keys + encrypted overlay).

---

## 7. Smoke test checklist

- [ ] Admin consent shows **Granted** for Graph **Mail.Send** (Application).
- [ ] Integrations (or env) has all four values; status shows Exchange **Configured**.
- [ ] **Test connection** succeeds; optional test email arrives From the sender mailbox.
- [ ] Admin → Users → Reset password → **Email password reset link** (or Create user with welcome email) delivers mail.
- [ ] If you set an access policy / App RBAC scope, sending as a different mailbox fails as expected.

---

## Troubleshooting (short)

| Symptom | Likely cause |
| --- | --- |
| Token / AADSTS errors | Wrong Tenant ID, Client ID, or secret; secret expired |
| `Authorization_RequestDenied` / 403 on sendMail | Missing admin consent, or access policy/RBAC blocking the mailbox |
| Mailbox not found / invalid user | Sender address not a real EXO mailbox UPN/SMTP |
| Works after Integrations save but env still old | Expected: overlay overrides env; env is not updated by the UI |

---

## Security notes

- Treat the client secret like a password; rotate on a schedule; use Integrations to rotate without redeploy.
- Prefer locking the app to the noreply mailbox (access policy or App RBAC).
- Do not grant broader Graph application permissions (e.g. `Mail.ReadWrite`) unless you have a separate, reviewed need.
- Keep `App_Data\keys` backed up with `integrations.protected` — losing keys makes the encrypted overlay unreadable (fall back to env and re-save).
