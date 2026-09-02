# Stato corrente

Aggiornato: 2026-09-02. Verificato in questa sessione con `dotnet test Iris.sln`
(146/146 verdi) e `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false` verde.

## Architettura

Backend .NET 9, esagonale: `Iris.Domain` (puro, nessuna dipendenza EF/HTTP) ->
`Iris.Application` (use case, port/interfacce, CQRS-lite: `XxxCommand`/`XxxHandler`) ->
`Iris.Infrastructure` (EF Core, repository, adapter) -> `Iris.Api` (minimal API,
composition root) -> `Iris.Contracts` (DTO condivisi backend/client). Persistenza EF Core,
doppio provider (SQLite dev, Postgres prod: migrazioni in due progetti separati, vedi
`03-iteration-guardrails.md`). Client: .NET MAUI Windows (`net9.0-windows...`), nessun
frontend web.

## Cosa è costruito

**Access / AAA** (`Iris.Domain.Access`) - RBAC capillare: `User`/`Role`/`RoleAssignment`
su `AccessScope` (Global/Customer/Context). Catalogo permessi in `Permissions.cs`
(`overview.*`, `infrastructure.*` incl. `infrastructure.secrets.manage`, `applications.*`,
`deployments.*`, `actions.*`, `governance.*`, `platform.admin`). Applications usa già i
suoi permessi; `deployments.*` e `actions.*` anticipano moduli non ancora costruiti.
Provisioning JIT + pre-provisioning admin (`User.Invite`, riconciliato per email al primo
login reale). Password locali PBKDF2 opzionali (`PasswordHash`/`PasswordSetupPending`),
separate dall'SSO.

**Auth** - modalità composita: header dev (`X-Dev-User` + eventuale `X-Dev-Password`),
Entra ID (Microsoft.Identity.Web, MSAL+WAM sul client) e session token Iris.
`POST /auth/login` valida email/password locale e rilascia un bearer token opaco, salvato come
hash in `UserSession` (non JWT). `IrisSessionAuthenticationHandler` ricostruisce l'identità
locale con `SyntheticIdentity`. `POST /invitations/accept` è anonimo e consuma un token
one-time per impostare la prima password locale. `POST /auth/password/reset` e'
anonimo/non-enumerante: se l'utente esiste ed e' attivo genera un nuovo link one-time via
lo stesso meccanismo inviti, altrimenti risponde comunque `Sent=true`.

**Governance** - CRUD utenti (crea/modifica/elimina/assegna ruolo/revoca), CRUD
clienti+contesti (`Iris.Domain.Tenancy.Customer`/`CustomerContext`, riusa `ContextKind`
Test/Staging/Production), inviti one-time con token hashato SHA-256 (`UserInvitation`),
edit lock advisory cross-risorsa (`EditLock`: heartbeat 45s, TTL 2min, force-unlock per
`platform.admin`) condiviso da utenti, clienti e server. Un operatore non puo'
auto-governarsi: update/delete/assign/revoke/invite sul proprio `User` sono bloccati lato
Application/API e il client mostra la propria riga in testa come tile read-only.

**Infrastructure** - `ServerNode` (nome, hostname, OS Linux/Windows, hosting
self-hosted/cloud, IP pubblico+privato, `Environment` come `ContextKind`) con
`ServerCredential` multipli per server, distinti `SystemUser` (legabile a uno `User` Iris)
vs `ServiceAccount`. I segreti reali non sono mai in DB: `ISecretStore` port +
`InMemorySecretStore` mock (stand-in per OpenBao). `infrastructure.secrets.manage`
permette di ruotare un segreto già salvato. `ServerNode` porta anche `Capabilities`
(`NodeCapability`: LoadBalancer/Database/ServiceHost/Presentation), `Resources`
(`ResourceProfile?`, CPU/RAM/disco opzionali) e `UsedPorts`, aggiornati con
`PUT /servers/{id}/capacity` in modalità replace-whole.

**Applications** (`Iris.Domain.Applications`) - catalogo applicazioni backend-first:
`ApplicationDefinition` (nome, slug, `RuntimeType`, repository, branch) con
`ApplicationVersion` figlie (versione, sorgente, `RuntimeMetadata` owned type che riusa
`ServerOs`). Ogni versione porta la configuration knowledge dell'ultimo import Iris
Extractor: `ConfigurationKey`/`DependencyDefinition`/`PlaceholderDefinition`, sostituite
e non accumulate a ogni `ApplyImport`; `RawImportPackageJson` conserva il pacchetto grezzo
per audit. Endpoint in `src/Iris.Api/Endpoints/ApplicationsEndpoints.cs`. Non esiste ancora
una pagina client MAUI Applications.

**First-run setup / mail** - in produzione il seed demo è disattivo per default: dopo le
migrazioni l'istanza resta vuota e il wizard first-run crea mail provider + primo
super-admin. Endpoint anonimi: `GET /setup/status`, `POST /setup/test-mail`,
`POST /setup/complete`; `CompleteSetupHandler` è replay-safe perché fallisce se esiste già un
assignment `platform-admin`. Esiste anche bootstrap SSO controllato:
`POST /setup/claim-admin` richiede autenticazione, allow-list
`Iris:Setup:AdminClaimEmails` e database senza platform-admin; il client MAUI lo chiama
automaticamente dopo SSO se `/setup/status` indica setup necessario. SMTP reale via
MailKit (`SmtpEmailSender`), password SMTP conservata solo via `ISecretStore`. Il client
MAUI ha `SetupWizardPage` e `AcceptInvitationPage`.

**Client MAUI** - flyout custom (`Shell.MenuItemTemplate` è inaffidabile sull'handler
Windows, sostituito da `Shell.FlyoutContentTemplate`, vedi `docs/ui-standards.md` sezione
9), sezioni gated per permesso (`AppShellViewModel.CanManageX`). Flussi secondari tramite
finestre modali OS vere (`IDialogService` -> `Window` MAUI owned + modale su Windows via
Win32), non pannelli in-page. Setup wizard e accept invitation sono fuori dal flyout. Il
flyout header contiene il profilo utente con link `Profile` e `Sign out`; il footer porta
`System settings`. Login supporta `Remember me` per sessioni locali persistite in
SecureStorage (fallback Preferences) e recupero password dal form.

**Profile / System settings** - `GET /profile` restituisce `MeResponse`, permessi
effettivi e history delle sessioni; `ProfilePage` espone dati utente, cambio password,
permessi e access history. `GET /system/settings` espone integrazioni OpenBao/Ansible a
tutti gli utenti autenticati e include lo stato/config SMTP solo per `platform.admin`.
`SystemSettingsPage` permette a tutti di scegliere `System`/`Light`/`Dark` theme locale.

**Audit / logging** - Serilog è il provider di logging dell'API con sink guidati da
configurazione. Il transaction log applicativo (`TransactionLog`) viene scritto da un
interceptor EF nello stesso `SaveChanges`: per ogni create/update/delete registra
`TransactionId`, data UTC, area (`Governance`/`Infrastructure`/`Applications`/`Settings`),
azione, entity type/id e attore (`ActorUserId`, email, display name, external id). `GET
/activity?area=...&take=...` restituisce la history per area ai `platform.admin`;
`SystemSettingsPage` mostra un pannello Activity filtrabile per area. CI security minima
con Gitleaks + Semgrep, più `.gitleaks.toml`, `.semgrepignore` e `.gitignore` rafforzato
contro file segreti locali.

## Cosa NON è costruito

**Deployments** (associazione Customer+Context+Application+Version+Server, placeholder
binding, Validation Engine) e **Actions** (preparazione Ansible/AWX/OpenBao, monitoraggio
azioni) non esistono ancora lato dominio/API/client. Il Validation Engine ha ora entrambi
i lati pronti da confrontare (`ApplicationVersion.RuntimeMetadata` vs
`ServerNode.Capabilities`/`Resources`/`UsedPorts`), ma le regole vere e proprie non sono
ancora scritte.

OpenBao/AWX/Ansible/Grafana restano mockati o non integrati per design. La pagina System
settings mostra collegamenti/configurazioni dichiarate ma non esegue ancora test,
salvataggio o chiamate reali verso OpenBao/Ansible.

## Migrazioni applicate (ordine)

`InitialAccessModel` -> `AddUserIsProvisioned` -> `AddServers` ->
`AddServerCredentialOwnership` -> `AddUserInvitations` -> `AddEditLocks` ->
`AddUserLocalPassword` -> `AddApplications` -> `AddServerCapacity` -> `AddUserSessions` ->
`AddMailProviderSettings`. Ogni migrazione esiste in entrambi i provider
(`src/Iris.Infrastructure/Persistence/Migrations` per SQLite,
`src/Iris.Migrations.Postgres/Migrations` per Postgres).
