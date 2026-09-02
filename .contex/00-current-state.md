# Stato corrente

Aggiornato: 2026-09-02. Verificato in questa sessione con `dotnet test Iris.sln -c Release`
(135/135 verdi).

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
one-time per impostare la prima password locale.

**Governance** - CRUD utenti (crea/modifica/elimina/assegna ruolo/revoca), CRUD
clienti+contesti (`Iris.Domain.Tenancy.Customer`/`CustomerContext`, riusa `ContextKind`
Test/Staging/Production), inviti one-time con token hashato SHA-256 (`UserInvitation`),
edit lock advisory cross-risorsa (`EditLock`: heartbeat 45s, TTL 2min, force-unlock per
`platform.admin`) condiviso da utenti, clienti e server.

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
assignment `platform-admin`. SMTP reale via MailKit (`SmtpEmailSender`), password SMTP
conservata solo via `ISecretStore`. Il client MAUI ha `SetupWizardPage` e
`AcceptInvitationPage`.

**Client MAUI** - flyout custom (`Shell.MenuItemTemplate` è inaffidabile sull'handler
Windows, sostituito da `Shell.FlyoutContentTemplate`, vedi `docs/ui-standards.md` sezione
9), sezioni gated per permesso (`AppShellViewModel.CanManageX`). Flussi secondari tramite
finestre modali OS vere (`IDialogService` -> `Window` MAUI owned + modale su Windows via
Win32), non pannelli in-page. Setup wizard e accept invitation sono fuori dal flyout.

**Security / logging** - Serilog è il provider di logging dell'API con sink guidati da
configurazione. CI security minima con Gitleaks + Semgrep, più `.gitleaks.toml`,
`.semgrepignore` e `.gitignore` rafforzato contro file segreti locali.

## Cosa NON è costruito

**Deployments** (associazione Customer+Context+Application+Version+Server, placeholder
binding, Validation Engine) e **Actions** (preparazione Ansible/AWX/OpenBao, monitoraggio
azioni) non esistono ancora lato dominio/API/client. Il Validation Engine ha ora entrambi
i lati pronti da confrontare (`ApplicationVersion.RuntimeMetadata` vs
`ServerNode.Capabilities`/`Resources`/`UsedPorts`), ma le regole vere e proprie non sono
ancora scritte.

OpenBao/AWX/Ansible/Grafana restano mockati o non integrati per design.

## Migrazioni applicate (ordine)

`InitialAccessModel` -> `AddUserIsProvisioned` -> `AddServers` ->
`AddServerCredentialOwnership` -> `AddUserInvitations` -> `AddEditLocks` ->
`AddUserLocalPassword` -> `AddApplications` -> `AddServerCapacity` -> `AddUserSessions` ->
`AddMailProviderSettings`. Ogni migrazione esiste in entrambi i provider
(`src/Iris.Infrastructure/Persistence/Migrations` per SQLite,
`src/Iris.Migrations.Postgres/Migrations` per Postgres).
