# Microsoft 365 (Entra ID) single sign-on setup

Iris supports signing in with the vendor's own Microsoft 365 tenant: the API
accepts Entra ID bearer tokens (`Iris:Auth:Mode` = `EntraId` or `Both`), and
the desktop client (`Iris.App`) signs in interactively via MSAL.NET, using
Windows' native broker (WAM) so it can reuse the account already signed into
Windows where possible.

This is **single-tenant only** — restricted to the vendor's own Microsoft 365
tenant. Do not use `common`/`organizations` as the authority, and do not
enable personal Microsoft accounts. Iris's own team manages customer
infrastructure through Iris; customers do not sign into Iris themselves.

You'll create two App Registrations in the vendor's Entra ID tenant: one for
the Iris API (exposes a scope), one for the desktop client (a public client
that calls that scope on the user's behalf).

## 1. Create the API app registration

In the [Entra admin center](https://entra.microsoft.com) → **App registrations** → **New registration**:

- Name: `Iris API` (or similar).
- Supported account types: **Accounts in this organizational directory only**
  (single tenant).
- No redirect URI needed for this registration.

After creation, note the **Application (client) ID** and the **Directory
(tenant) ID** — these become `AzureAd:ClientId` and `AzureAd:TenantId` on the
API.

### Expose an API

**Expose an API** → **Add a scope**:

- Application ID URI: accept the default (`api://<api-client-id>`) — this
  becomes `AzureAd:Audience`.
- Scope name: `access_as_user`.
- Who can consent: **Admins and users**.
- Add a short admin/user consent description (e.g. "Access Iris as the
  signed-in user").

The full scope value (`api://<api-client-id>/access_as_user`) is what the
desktop client requests through `Iris.App/appsettings.Development.json`.

## 2. Create the desktop client app registration

**New registration**:

- Name: `Iris Desktop` (or similar).
- Supported account types: **Accounts in this organizational directory only**
  (single tenant — same tenant as the API registration).
- Redirect URI: platform **Mobile and desktop applications**, URI:

  ```text
  ms-appx-web://microsoft.aad.brokerplugin/<desktop-client-id>
  ```

  (substitute the client ID of *this* registration, visible after you save
  it once — you may need to add the redirect URI in a second pass). This is
  the fixed convention Windows' WAM broker requires; MSAL.NET doesn't need
  this value in code (`WithDefaultRedirectUri()` handles it), it only needs
  to exist on the app registration.

Note the **Application (client) ID** of this registration — that's the desktop
client's `EntraId:ClientId` value.

### Grant it the API permission

**API permissions** → **Add a permission** → **My APIs** → `Iris API` →
**Delegated permissions** → check `access_as_user` → **Add permissions**.
Then **Grant admin consent for `<tenant>`** — with single-tenant + admin
consent granted once, individual users won't see a consent prompt.

## 3. Restrict both registrations to your tenant

Confirm both app registrations' **Supported account types** are set to
"Accounts in this organizational directory only (Single tenant)" —
**Authentication** blade on each registration. This is what keeps sign-in
scoped to the vendor's own Microsoft 365 tenant.

## 4. Plug the values in

| Value | Where it goes |
|---|---|
| API app's Tenant ID | `Iris.Api/appsettings.json` → `AzureAd:TenantId` (or user-secrets) |
| API app's Client ID | `Iris.Api/appsettings.json` → `AzureAd:ClientId` |
| API app's Application ID URI | `Iris.Api/appsettings.json` → `AzureAd:Audience` |
| Tenant ID (same tenant) | `Iris.App/appsettings.Development.json` -> `EntraId:TenantId` |
| Desktop app's Client ID | `Iris.App/appsettings.Development.json` -> `EntraId:ClientId` |
| `api://<api-client-id>/access_as_user` | `Iris.App/appsettings.Development.json` -> `EntraId:ApiScope` |

Prefer user-secrets or environment variables over committing real tenant
values into `appsettings.json` — the checked-in file should keep the
placeholder GUIDs.

## 5. Turn on Entra ID auth on the API

Set `Iris:Auth:Mode` to `Both` (dev header still works alongside SSO — useful
while testing) or `EntraId` (SSO only) in configuration.

For a fresh SSO-only bootstrap, allow-list the Microsoft 365 user who is allowed
to claim the first platform-admin role:

```powershell
$env:Iris__Setup__AdminClaimEmails__0="you@company.com"
```

`POST /setup/claim-admin` is authenticated and one-shot: it works only while
`/setup/status` still says setup is needed, and only for an email in
`Iris:Setup:AdminClaimEmails`. The desktop client calls it automatically after
a successful SSO sign-in when no platform admin exists yet. This grants the role
only; it does not configure SMTP. Invitations will fall back to logging until a
mail configuration screen exists.

## 6. Verify

1. Run the API with `Iris:Auth:Mode=Both`, the real `AzureAd` values, and
   `Iris__Setup__AdminClaimEmails__0` set to your Microsoft 365 email if the
   database is empty.
2. Run `Iris.App` with the real `EntraId` values in its
   `appsettings.Development.json`.
3. On the login screen, click **Continue with single sign-on**. Windows'
   account picker (or an interactive Entra ID prompt, if WAM isn't
   available) should appear; on success, the app claims the first
   `platform-admin` role automatically when setup is still needed, then lands
   on the dashboard.
4. Confirm `GET /me` returns the signed-in user's Entra `oid` as
   `ExternalId` — check `Iris.Api`'s logs or database to confirm a matching
   `Iris.Domain.Access.User` row was just-in-time provisioned and has the
   `platform.admin` effective permission.

## Known limitation

Token cache persistence across app restarts isn't wired up yet — every
Iris.App launch attempts a silent sign-in against the Windows-signed-in
account first (usually no prompt at all thanks to WAM), falling back to an
interactive prompt only if that fails. Persisting MSAL's cache to disk
(`Microsoft.Identity.Client.Extensions.Msal`) is a natural follow-up if that
Windows-account silent path ever isn't sufficient.
