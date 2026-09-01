# Stato corrente

Aggiornato: 2026-09-01. Verificato con `dotnet build Iris.sln` (0 errori, 0 warning),
`dotnet test Iris.sln` (108/108 verdi), `dotnet build src/Iris.App/Iris.App.csproj` (0 errori, 0 warning).

## Architettura

Backend .NET 9, esagonale: `Iris.Domain` (puro, nessuna dipendenza EF/HTTP) →
`Iris.Application` (use case, port/interfacce, CQRS-lite: `XxxCommand`/`XxxHandler`) →
`Iris.Infrastructure` (EF Core, repository, adapter) → `Iris.Api` (minimal API, composition
root) → `Iris.Contracts` (DTO condivisi backend/client). Persistenza EF Core, doppio
provider (SQLite dev, Postgres prod — migrazioni in due progetti separati, vedi
`03-iteration-guardrails.md`). Client: **.NET MAUI, solo Windows** (`net9.0-windows...`,
altri target commentati in `Iris.App.csproj`) — nessun frontend web.

## Cosa è costruito

**Access / AAA** (`Iris.Domain.Access`) — RBAC capillare: `User`/`Role`/`RoleAssignment`
su `AccessScope` (Global/Customer/Context). Catalogo permessi in `Permissions.cs`
(`overview.*`, `infrastructure.*` incl. `infrastructure.secrets.manage`, `applications.*`,
`deployments.*`, `actions.*`, `governance.*`, `platform.admin`) — **il catalogo esiste già
per moduli non ancora costruiti** (Applications/Deployments/Actions), non aggiungere
permessi duplicati quando si arriva a costruirli. Auth duale: header dev (`X-Dev-User`) o
Entra ID (Microsoft.Identity.Web, MSAL+WAM sul client). Provisioning JIT + pre-provisioning
admin (`User.Invite`, riconciliato per email al primo login reale). Password locali PBKDF2
opzionali (`PasswordHash`/`PasswordSetupPending`), separate dall'SSO.

**Governance** — CRUD utenti (crea/modifica/elimina/assegna ruolo/revoca), CRUD
clienti+contesti (`Iris.Domain.Tenancy.Customer`/`CustomerContext`, riusa `ContextKind`
Test/Staging/Production), inviti one-time con token hashato SHA-256
(`UserInvitation`), **edit lock advisory** cross-risorsa (`EditLock` — heartbeat 45s,
TTL 2min, force-unlock per `platform.admin`) condiviso da utenti e server.

**Infrastructure** — `ServerNode` (nome, hostname, OS Linux/Windows, hosting
self-hosted/cloud, IP pubblico+privato, `Environment` come `ContextKind`) con
`ServerCredential` multipli per server, distinti `SystemUser` (legabile a uno `User` Iris)
vs `ServiceAccount`. **I segreti reali non sono mai in DB**: `ISecretStore` port +
`InMemorySecretStore` mock (stand-in per OpenBao, sostituzione dichiarata nel commento).
`infrastructure.secrets.manage` permesso separato per ruotare un segreto già salvato.
`ServerNode` porta anche `Capabilities` (lista di `NodeCapability`:
LoadBalancer/Database/ServiceHost/Presentation), `Resources` (`ResourceProfile?` owned,
CPU/RAM/disco tutti opzionali) e `UsedPorts` (lista di interi) — impostati tramite
`PUT /servers/{id}/capacity` (replace wholesale, come `ApplicationVersion.ApplyImport`),
il prerequisito che serviva al Validation Engine (vedi `05-next-actions.md`).

**Client MAUI** — flyout custom (`Shell.MenuItemTemplate` è inaffidabile sull'handler
Windows, sostituito da `Shell.FlyoutContentTemplate` fatto a mano, vedi
`docs/ui-standards.md` §9), sezioni gated per permesso (`AppShellViewModel.CanManageX`).
Flussi secondari sono **finestre modali OS vere** (`IDialogService` → `Window` MAUI owned
+ modale su Windows via Win32), non pannelli in-page — convenzione completa in
`docs/ui-standards.md` (obbligatoria prima di toccare una schermata).

**Applications** (`Iris.Domain.Applications`) — catalogo applicazioni: `ApplicationDefinition`
(nome, slug, `RuntimeType`, repository, branch) con `ApplicationVersion` (versione,
sorgente, `RuntimeMetadata` owned type che riusa `ServerOs`) come entità figlie. Ogni
versione porta la configuration knowledge dell'ultimo import da un ipotetico Iris
Extractor: `ConfigurationKey`/`DependencyDefinition`/`PlaceholderDefinition` (entità
proprie, ciascuna con `Id`), sostituite — non accumulate — a ogni `ApplyImport`;
`RawImportPackageJson` tiene il pacchetto grezzo per audit. Endpoint su
`src/Iris.Api/Endpoints/ApplicationsEndpoints.cs` (catalogo, versioni, import), permessi
già presenti (`applications.read/write/import`). **Nessuna pagina client** per questo
dominio ancora (backend-first, per scelta).

## Cosa NON è costruito (gap rispetto al brief originale)

**Deployments** (associazione Customer+Context+Application+Version+Server, domain
placeholder binding, Validation Engine), **Actions** (preparazione Ansible/AWX/OpenBao,
monitoraggio azioni) — nessuno dei due esiste, né lato dominio né API né client. Il
permesso catalog li anticipa già (`Permissions.Deployments`, `Permissions.Actions`), ma
non c'è nessuna entità/endpoint/pagina. Il Validation Engine ora ha entrambi i lati pronti
da confrontare (`ApplicationVersion.RuntimeMetadata` vs `ServerNode.Capabilities`/
`Resources`/`UsedPorts`, vedi sopra), ma il confronto stesso — le regole vere e proprie —
non è ancora scritto. Vedi `F:\Work\Iris_v2` per uno schizzo di dominio + regole di
validazione già pensate per questi moduli (non in git, mai buildato con successo, ma
concettualmente utile — dettagli in `02-operational-plan.md`).

OpenBao/AWX/Ansible/Grafana restano tutti mockati per design (vedi brief originale).

## Migrazioni applicate (ordine)

`InitialAccessModel` → `AddUserIsProvisioned` → `AddServers` →
`AddServerCredentialOwnership` → `AddUserInvitations` → `AddEditLocks` →
`AddUserLocalPassword` → `AddApplications` → `AddServerCapacity`. Ogni migrazione esiste
in entrambi i provider (`src/Iris.Infrastructure/Persistence/Migrations` per SQLite,
`src/Iris.Migrations.Postgres/Migrations` per Postgres).
