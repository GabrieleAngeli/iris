# Stato corrente

Aggiornato: 2026-09-07. Verificato in questa sessione con `dotnet test Iris.sln --no-restore -p:UseAppHost=false`
(206/206 verdi), `dotnet build src\Iris.Api\Iris.Api.csproj -p:UseAppHost=false --no-restore`
verde e `dotnet build src\Iris.App\Iris.App.csproj -p:UseAppHost=false --no-restore`
verde.

Nota: il commit `11802b3` (Ansible plan + connettori) era stato committato senza
compilare — un errore CS0411 in `OpenBaoSecretStore.StoreAsync` (ternario KV v1/v2 con
due tipi diversi passato a `JsonContent.Create`). Corretto nel commit `39d769a`
branchando il ternario sui due `JsonContent.Create`. Da qui i 169 test.

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
Nota: `ResourceProfile` ora distingue anche `ApplicationDiskGb` e `BackupDiskGb`; la
pagina MAUI Servers li espone nella dialog di modifica insieme al disco totale.
Quando un server ha almeno una credenziale, `POST /servers/{id}/discover` richiama il port
`IServerInventoryProbe` (adapter mock deterministico oggi, futuro Ansible/SSH) e aggiorna
OS rilevato, versione OS, dimensione macchina, CPU/RAM/dischi e porte usate. La pagina
Servers avvia la discovery dopo l'inserimento credenziale e offre un comando manuale.
Nella stessa sezione esiste anche l'inventory dei data service gestiti/RDS:
`DataServiceInstance` per `Mssql`, `PostgreSql` e `Redis`, con endpoint, porta, username
non segreto, password solo via `ISecretStore`, versione, size, storage e ambiente. La
creazione RDS passa dal dialog `New server` tramite select `Server node` /
`Managed data service`; dopo le credenziali il port `IDataServiceInventoryProbe` rileva
tipo/versione/size/storage. Endpoint `/data-services` e discovery manuale
`POST /data-services/{id}/discover`. Nel client MAUI server node e data service sono
presentati nella stessa lista `Resources`, con icone differenti e filtri/sort per tipo,
OS, versione e tag. L'edit inline RDS e' protetto da proprieta' wrapper
(`IsEditingDataService`, `HasDataServiceError`) per evitare che i campi di input dei data
service compaiano nelle righe `Server node`, dove `DataService` e' nullo.

**Applications** (`Iris.Domain.Applications`) - catalogo applicazioni:
`ApplicationDefinition` (nome, slug, `RuntimeType`, repository, branch, artifact provider,
artifact feed/name/path e build pipeline URL) con
`ApplicationVersion` figlie (versione, sorgente, `RuntimeMetadata` owned type che riusa
`ServerOs`). Ogni versione porta la configuration knowledge dell'ultimo import Iris
Extractor: `ConfigurationKey`/`DependencyDefinition`/`PlaceholderDefinition`, sostituite
e non accumulate a ogni `ApplyImport`; `RawImportPackageJson` conserva il pacchetto grezzo
per audit. `DependencyDefinition` puo' collegare una dependency consumata a un placeholder
esposto da un'altra application tramite `ProviderApplicationSlug` e
`ProviderPlaceholderKey`. Endpoint in `src/Iris.Api/Endpoints/ApplicationsEndpoints.cs`, incluso
`PUT /applications/{id}` per aggiornare l'inventory mantenendo lo slug stabile. Il client
MAUI ha `ApplicationsPage` sotto Workspace: lista catalogo, create/edit via dialog modali,
gating con `applications.read/write` e lock advisory `application`. Ogni tile application
include ora il primo step di upload manifest: selezione file JSON via FilePicker,
validazione client-side senza import automatico, associazione immediata del report alla
application scelta, riepilogo schema/conteggi/tipi default rilevati e issue list per
errori, warning e link application-to-application presenti o mancanti nel catalogo Iris
corrente. Se il manifest e' valido, nella stessa tile viene costruita una preview di
assimilazione con configuration key, dependency, placeholder, profili/varianti e decisioni
da risolvere nel wizard (segreti, required senza default, liste e provider application).
Il pulsante `Start import` apre `ImportManifestDialog`: release version, source reference,
runtime target, OS testati, minimum resources e port policy vengono letti dal manifest e
mostrati come dato non editabile; il wizard si concentra sulle associazioni logiche tra
application Iris. Il client crea una `ApplicationVersion` e chiama l'import package.
Il dominio/API persistono ora anche la semantica manifest 1.1: value type/item type/scope
delle configuration key, metadata JSON di serialization/resolution/profile defaults,
runtime execution targets, OS support testati, risorse minime, port keys per istanza,
application unit avviabili, installation profile master/slave e dependency constraints
di versione. I valori finali restano comunque da comporre nel futuro binding
server/data-service/application <-> application installation.
Esiste un manifest demo caricabile dalla tile `augeg4-engine` in
`docs/manifests/augeg4-engine.demo.iris-package.json`: copre release/source nel manifest,
runtime service/docker, OS testati, minimum resources, port keys per istanza, application
unit (`augeg4.engine`, `augeg4.monitor-admin`, `augeg4.p5.engine`), master/slave, chiavi
tipizzate, liste, segreti, service reference MongoDB/Redis/SMTP, riferimento a
`augeg4-web`, placeholder esposti e vincoli demo di versione servizio.
Nel database dev locale e' stata assimilata come prova l'application
`algorab-augeg4-grpcflow` da manifest esterni AugeG4 GrpcFlow: versione
`net8.0-Windows-win-x64-self-contained`, artifact Nexus
`algorab-raw/augeg4.web.$(PACKAGE_VERSION).7z!/GrpcFlow`, 41 configuration key, 5
dependency, 3 placeholder e 5 warning. Durante l'import i `defaultValue` numerici e
booleani sono stati normalizzati a stringa per aderire al contratto API attuale
(`ConfigurationKeyInput.DefaultValue` e' `string?`).
La guida operativa e' in `docs/application-assimilation.md` e nel client MAUI alla voce
Applications -> `Extractor guide`: include pipeline/extractor .NET e una procedura di
estrazione manuale per tecnologia (`.NET`, Node/JavaScript, Java/Spring,
Docker/container, Ansible Jinja2) per produrre e importare `iris-package.json` anche prima
di avere extractor automatici dedicati. La pagina FE e' organizzata verticalmente per
tecnologia e usa `controls:TabGroup` per le due tab di ciascuna tecnologia: `Automatic` e
`Manual manifest`; in testa il tab group condiviso parte da `Fields`, che spiega come
compilare `configurationKeys`, `dependencies`, `placeholders` e `warnings`, come
rappresentare connection string PostgreSQL, endpoint Redis, HTTP API, secret esterni e
placeholder provider/consumer. Il contenuto delle tab e' strutturato in blocchi testo,
note operative e code block tramite il componente globale `controls:CodeBlock`,
selezionabile e copiabile, per distinguere spiegazioni, comandi e manifest JSON. Jinja2
Ansible e' trattato come target sensato di standardizzazione futura tramite
`targetKind = "ansible:j2"`.

**Application installation / deployment** (`Iris.Domain.Applications`) -
`ApplicationInstallation` (aggregate root) lega `ApplicationId` + `ApplicationVersionId` +
`ApplicationUnitKey?` + `InstallationProfileKey?` + `ServerNodeId` + **`CustomerContextId`**
(FK reale a `Iris.Domain.Tenancy.CustomerContext`, non piu' un `ContextKind` libero: un
deployment e' sempre "questa applicazione, per questo ambiente di questo cliente, su
questo server", vedi `01-decisions.md`) + `Notes`. Porta `ApplicationInstallationBinding`
figlie (`ReplaceBindings`, replace-whole): ogni binding lega un `PlaceholderKey` a un
target concreto tipizzato (`ApplicationInstallationTargetKinds`: `data-service`,
`application`, ecc.) via `TargetId`/`TargetSlug` + `ValuePreview`. `ICustomerRepository`
non ha un lookup diretto by-context (i context sono owned entity, non un aggregato a se');
`ApplicationInstallationMapping.ResolveCustomerContextAsync` (extension) carica tutti i
customer accessibili e correla in memoria - usato da create/list/validate/ansible-plan.
`ApplicationInstallationResponse` porta ora `CustomerId`/`CustomerName`/`CustomerContextId`/
`CustomerContextName`/`Environment` (quest'ultimo derivato da `context.Kind`, sola
visualizzazione). Endpoint in `ApplicationsEndpoints.cs`:
`GET/POST /applications/installations` (perm `deployments.read`/`deployments.write`,
`CreateApplicationInstallationRequest.CustomerContextId` sostituisce il vecchio
`Environment` string), handler `ListApplicationInstallations`/`CreateApplicationInstallation`.
Migrazioni `AddApplicationInstallations` -> `AddApplicationInstallationCustomerContext`
(drop colonna `Environment`, add `CustomerContextId`) per SQLite e Postgres. Mappato in
`TransactionLogInterceptor.AreaFor` come `Deployments`.

**Client MAUI - sezione Deployments** (sostituisce la lista installazioni sotto
Applications di due iterazioni fa, spostata su richiesta esplicita dell'utente:
"non ha senso che l'installation sia sotto le application"; poi rispostata dentro
Governance su ulteriore richiesta - "la sezione deploy la sposti sotto governance"):
voce flyout `Deployments` dentro la sezione collassabile **Governance** (dopo
Customers), route `//deployments`, icona rocket, riga gated `CanSeeDeployments` =
`deployments.read`; la sezione Governance stessa e' ora visibile se
`CanManageUsers` (`governance.read`) **oppure** `CanSeeDeployments`
(`AppShellViewModel.CanSeeGovernanceSection`), cosi' un operatore con solo
`deployments.read` non perde l'accesso. Pagina `DeploymentsPage` + `DeploymentsViewModel`,
organizzata a **tre livelli**: Customer -> Context -> **Server** -> installazioni
(non piu' Context -> installazioni piatto: l'utente ha chiesto esplicitamente "per
enviroment lo step e' scegliere i server, una volta scelti i server, per ogni server
si scelgono gli applicativi e le modalita' di installazione"): il livello Server
(`DeploymentServerGroupViewModel`) e' ora guidato da `EnvironmentServerAssignment` (vedi
sopra), non piu' dedotto dalle installazioni esistenti. Ogni `DeploymentContextGroupViewModel`
espone `AvailableServers` (server attivi non ancora assegnati a quel context) +
`SelectedServerToAssign` + `AssignServerCommand` (picker inline + bottone `Assign` nella
card ambiente); ogni `DeploymentServerGroupViewModel` espone `UnassignCommand` (icona
`✕`, chiama `DELETE /deployments/server-assignments/{id}`, il 409 del backend se ci sono
ancora installazioni li' arriva in chiaro come errore) e `AddApplicationCommand` (`+
Application`): quest'ultimo riusa il wizard esistente (vedi sotto) ma pre-seleziona
server e customer/context DOPO che `PrepareInstallationAsync` ha popolato le opzioni (il
dialog e' gia' aperto/bindato quando arriva la pre-selezione, quindi si aggiorna a vista -
vedi `DeploymentsViewModel.StartComposingAsync`). Un server compare quindi anche con zero
applicazioni ("No applications on this server yet."), e un ambiente senza server assegnati
mostra "No servers chosen for this environment yet." `GetCustomersAsync()` +
`GetApplicationInstallationsAsync()` + `GetEnvironmentServerAssignmentsAsync()` +
`GetServersAsync()` sono la fonte dati, tutte ricaricate insieme in
`DeploymentsViewModel.RefreshAsync`. Ogni installazione mostra `ApplicationName` (aggiunto a
`ApplicationInstallationRowViewModel`, decoupled da `ApplicationRowViewModel`: ora prende
`canManageDeployments`/`openOps` come parametri invece di un parent tipizzato, cosi'
riusabile sia da Deployments sia in futuro altrove) e apre lo stesso `InstallationOpsDialog`
(Validate/Deploy/Run history, invariato). Il comando `New deployment` riusa
`ApplicationsViewModel` (iniettata come sorgente dati, istanza transient separata da quella
di `ApplicationsPage`) per il picker applicazione e per l'intero wizard
`NewApplicationInstallationDialog` gia' esistente (release/unit/profilo/server/binding),
a cui e' stato aggiunto un campo obbligatorio `Customer & environment`
(`InstallCustomerContextOptions`/`SelectedInstallCustomerContext`,
`CustomerContextOptionViewModel`). Dopo la creazione, `DeploymentsViewModel` si iscrive a
`ApplicationRowViewModel.ApplicationInstallationCompleted` per ricaricare la lista.
`ApplicationsPage` e' tornata a essere solo il catalogo applicazioni: rimossi il bottone
"New installation" e la sezione "Installations" che una iterazione precedente aveva
aggiunto li'.
**Non ancora verificata manualmente nell'app Windows in esecuzione** in questa sessione
(solo `dotnet build Iris.App.sln` verde) - da fare prima di considerare il flusso chiuso.
Nota di rischio: `NewApplicationInstallationDialog` chiude se stessa con
`Navigation.PopModalAsync()` (codice preesistente, non toccato) invece del pattern
`Application.Current.CloseWindow(Window)` usato dagli altri dialog aperti via
`IDialogService`/`native.MakeModalDialog` (incl. `InstallationOpsDialog`); se questo non
chiude davvero la `Window` del dialog, verificarlo durante il test manuale - il refresh di
`DeploymentsViewModel` non dipende comunque da quella chiusura (si aggancia all'evento
`ApplicationInstallationCompleted`, non al completamento di `ShowAsync`).

**Topologia ambiente: server assegnati (`EnvironmentServerAssignment`)** - nuovo aggregato
(`Iris.Domain.Deployments`, primo file in quella cartella dominio): lega `CustomerContextId`
+ `ServerNodeId` + `Notes` opzionali - "quali server usa questo ambiente", indipendente da
avere gia' installato qualcosa li'. Coppia (context, server) unica (indice EF). Endpoint
`src/Iris.Api/Endpoints/DeploymentsEndpoints.cs` (nuovo file, gruppo `/deployments`, non
`/applications`): `GET /deployments/server-assignments` (tutte, perm `deployments.read`),
`POST /deployments/contexts/{customerContextId:guid}/server-assignments` (perm
`deployments.write`, 409 se gia' assegnato), `DELETE /deployments/server-assignments/{id}`
(perm `deployments.write`, 409 se esistono ancora `ApplicationInstallation` su quel
server+context - blocco esplicito, non cascade). Handler in
`Iris.Application/Deployments/`: `AssignServerToEnvironment`, `ListEnvironmentServerAssignments`,
`UnassignServerFromEnvironment`. Migrazione `AddEnvironmentServerAssignments` per SQLite e
Postgres, area `Deployments` nel `TransactionLogInterceptor`.

**Trappola di routing scoperta e corretta in questa sessione**: `PermissionAuthorizationHandler`
legge i valori di route/query chiamati **letteralmente** `customerId`/`contextId` per decidere
lo scope della richiesta (`ScopeFactory.From`); un parametro di route chiamato `contextId`
SENZA un `customerId` gemello fa fallire `ScopeFactory.From` con `InvalidScopeRequestException`,
che il gestore interpreta come "nega silenziosamente" -> **403 per chiunque, incluso
`platform-admin`**. Il primo tentativo di endpoint (`/deployments/contexts/{contextId:guid}/...`)
ci e' caduto dentro; corretto rinominando il parametro in `customerContextId`, restando
Global-scoped come gia' fanno gli endpoint `/applications/installations/*` (nessun
`customerId`/`contextId` in route). **Da tenere a mente per qualunque nuovo endpoint**: mai
chiamare un parametro di route/query `contextId` (o `customerId`) a meno di voler
esplicitamente lo scope-check automatico, e in quel caso serve sempre la coppia completa.

**Ansible plan + connettori integrazione (mock-first, execution bridge AWX)** -
decisione architetturale (in `docs/application-configuration-model-analysis.md`): Iris
NON renderizza i file di configurazione finali; produce un piano di variabili `iris_*` e
binding che Ansible/AWX consuma nei template Jinja2 (`.j2`), e ogni modifica infra la fa
Ansible.
- `GET /applications/installations/{id}/ansible-vars` (perm `deployments.read`) ->
  `GetApplicationInstallationAnsiblePlanHandler`: compone variabili filtrate per profilo,
  `templateTargets` normalizzati `ansible:j2:<target>`, `operations` ordinate (load plan
  -> fetch artifact -> render template -> runtime service/container -> network apply),
  `associations` risolte/non risolte, source per variabile (`iris:data-service`,
  `iris:application`, `manifest:default`, `manual`) e warning per required non risolti.
- `POST /applications/installations/{id}/awx/launch` (perm `deployments.write`) ->
  `LaunchApplicationInstallationAwxJobHandler`: prende il piano, `IAnsibleExecutionPackageBuilder`
  compone `extra_vars`, crea e persiste un `InstallationRun` (stato `Pending`), poi
  `IAwxClient.LaunchAsync`. Successo -> `run.MarkSubmitted(jobId/url/status mappato)`;
  `ValidationException` da AWX (es. non configurato) -> `run.MarkFailed(...)` e rilancia
  (endpoint resta 400). Risposta con `RunId` + job id/status/url + preview extra_vars.
  NON collegato a nessun pulsante MAUI.
- **Run history**: `InstallationRun` (aggregato, `Iris.Domain/Applications`), FK `Guid`
  verso `ApplicationInstallation`, `Kind` (`AwxJob`), `Status`
  (`Pending`/`Running`/`Succeeded`/`Failed`/`Canceled`, ultimi tre terminali),
  `ExternalJobId`/`ExternalUrl`, `SubmittedVariablesJson`, `Message`, `CompletedAtUtc`.
  `GET /applications/installations/{id}/runs` (lista, newest first) e
  `GET .../runs/{runId}` (perm `deployments.read`): il GET singolo, se il run non e'
  terminale e ha un job id, chiama `IAwxClient.GetJobStatusAsync` (GET `/api/v2/jobs/{id}/`)
  e aggiorna lo stato; se AWX non e' raggiungibile/configurato il read non fallisce, resta
  l'ultimo stato noto. Migrazione `AddInstallationRuns` (SQLite + Postgres), area
  `Deployments` nel `TransactionLogInterceptor`.
- Porte in `Iris.Application/Abstractions`: `IIntegrationConnector` (status/health),
  `IAwxClient` (`LaunchAsync` + `GetJobStatusAsync`), `IAnsibleExecutionPackageBuilder`,
  `IInstallationRunRepository`.
- Adapter in `src/Iris.Infrastructure/Integrations`: `OpenBaoConnector` (probe
  `/v1/sys/health`), `AwxClient` (`POST /api/v2/job_templates/{id}/launch/`, probe
  `/api/v2/ping/`, `HttpClient` via `new()` come singleton),
  `AnsibleExecutionPackageBuilder`. `OpenBaoSecretStore` (`src/Iris.Infrastructure/Secrets`,
  KV v1/v2) sostituisce `InMemorySecretStore` come `ISecretStore` SOLO se
  `Iris:Integrations:OpenBao:Endpoint` e `:Token` sono entrambi presenti; altrimenti resta
  il mock in memoria (ora singleton con `ConcurrentDictionary`, quindi i segreti mock
  persistono tra le request).
- `GET /system/settings` ora aggrega lo stato reale dei connettori via
  `IEnumerable<IIntegrationConnector>` (`GetStatusAsync(probe:false)`) e aggiunge il campo
  `Message` a `IntegrationLinkResponse`, mostrato in `SystemSettingsPage`. `GET
  /system/integrations/{key}/status?probe=true` (perm `platform.admin`) invoca la probe
  reale del connettore; la UI System settings espone il pulsante `Test` per ogni riga
  OpenBao/Ansible/AWX.

**Validation Engine (deployment)** - `GET /applications/installations/{id}/validate`
(perm `deployments.validate`) -> `ValidateApplicationInstallationHandler`: solo lettura,
confronta la configuration knowledge della `ApplicationVersion` (placeholder, configuration
key filtrate per profilo, dependency, `RuntimeMetadata`, `DependencyConstraintDefinition`)
contro il target concreto (`ServerNode.Capabilities`/`Resources`/`UsedPorts` +
`DataServiceInstance` legati dai binding) e restituisce
`ApplicationInstallationValidationResponse` con `IsValid`, conteggi e lista tipata di
`ApplicationInstallationValidationCheckResponse { Code, Severity(error/warning/info),
Category, Target, Message }`. Regole v1: placeholder required non legato
(`placeholder.unbound`/`placeholder.unresolved`), configuration key required senza
binding ne' default (`configuration.*`), dependency required non legata / provider
application mancante (`dependency.*`), OS non testato (`os.incompatible` da `OsSupportJson`,
`os.not-preferred` da `PreferredOs`), capability `ServiceHost` assente (`capability.missing`,
`capability.unknown` se il server non e' profilato), collisione porte
(`RequiredPorts` ∩ `UsedPorts` -> `port.collision`), capacita' insufficiente
(`Minimum*`/`Required*` vs `ResourceProfile` -> `capacity.cpu`/`capacity.memory` +
varianti `-recommended`), vincoli servizio non soddisfatti dal data service legato
(`constraint.service-kind`, `constraint.version` via un parser di espressioni tipo
`>= 6.2 && < 8`, `== 6`, `6.2-8.0`; non parsabile -> `info`, mai blocco). Nessuna UI MAUI
ancora. Test: `ValidateApplicationInstallation_*` in `ApplicationsHandlersTests`.

**First-run setup / mail** - in produzione il seed demo è disattivo per default: dopo le
migrazioni l'istanza resta vuota e il wizard first-run crea mail provider + primo
super-admin. Endpoint anonimi: `GET /setup/status`, `POST /setup/test-mail`,
`POST /setup/complete`; `CompleteSetupHandler` è replay-safe perché fallisce se esiste già un
assignment `platform-admin`. Esiste anche bootstrap SSO controllato:
`POST /setup/claim-admin` richiede autenticazione, allow-list
`Iris:Setup:AdminClaimEmails` e database senza platform-admin; il client MAUI lo chiama
automaticamente dopo SSO se `/setup/status` indica setup necessario. SMTP reale via
MailKit (`SmtpEmailSender`), password SMTP conservata solo via `ISecretStore`. Il client
MAUI ha `SetupWizardPage` e `AcceptInvitationPage`, ora a **4 step**: OpenBao, AWX, mail,
admin (era 2 - mail, admin).

**Integration settings persistite (OpenBao/AWX/Ansible)** - prima di questo incremento
`Iris:Integrations:*` era solo `appsettings.json`/env, letto una volta all'avvio, senza
alcun endpoint di salvataggio (gap già segnalato più sotto). Aggiunto `IntegrationSettings`
(`Iris.Domain.Settings`, riga singola come `MailProviderSettings` ma con tre mutator
indipendenti `ConfigureOpenBao`/`ConfigureAwx`/`ConfigureAnsible` così un salvataggio non
cancella gli altri due gruppi) + `IIntegrationSettingsRepository` +
`PUT /system/integrations/{openbao|awx|ansible}` (perm `platform.admin`, mai anonimi). I
token passano da `ISecretStore` come per la password SMTP; un token vuoto in una PUT
successiva mantiene il riferimento già salvato invece di cancellarlo.
`RegisterIntegrations` (`Iris.Infrastructure/DependencyInjection.cs`) ora fa una lettura
sincrona best-effort del DB *prima* di `builder.Build()` (stesso pattern di
`IrisDbContextFactory`, nessun DI ancora disponibile) per far vincere i valori persistiti
su quelli di config, con fallback silenzioso se il DB non è ancora migrato o raggiungibile.
**Limite noto e voluto, non un bug**: il token di OpenBao stesso non è mai risolvibile da un
riferimento persistito al riavvio (richiederebbe una connessione OpenBao già funzionante
autenticata con quello stesso token - circolare); l'operatore deve reinserirlo una volta
dopo il primo riavvio che attiva OpenBao reale. Il token AWX invece è risolvibile una volta
che OpenBao è reale. `GET /system/settings` espone `RestartRequired` (confronta
`ActiveIntegrationSnapshot`, catturato all'avvio, con una lettura fresca del DB) - vero ogni
volta che una PUT ha salvato qualcosa che il processo in esecuzione non ha ancora caricato;
`SystemSettingsPage` mostra un banner giallo quando è vero. Il wizard di setup raccoglie
solo intento/config per OpenBao/AWX (nessun comando di sistema da un endpoint anonimo);
`CompleteSetupResponse` porta `OpenBaoProvisionRequested`/`AwxProvisionRequested` per un
futuro hand-off post-login verso endpoint di provisioning reali (il wizard MAUI stesso non li
chiama ancora - vedi `05-next-actions.md`).

**OpenBao self-provisioning via Docker** - `POST /system/integrations/openbao/provision`
(`platform.admin`) è la prima capacità del repo di eseguire processi di sistema:
`IContainerRuntime` (`Iris.Application.Abstractions`) + `DockerCliContainerRuntime`
(`Iris.Infrastructure/Containers`, shell-out a `docker` via un seam `IProcessRunner` per
restare testabile senza demone reale). Avvia `openbao/openbao:2.1` in modalità `-dev` (solo
convenienza/non-produzione, dichiarato esplicitamente nella risposta), legge il root token
dai log del container (`docker logs`, marker `Root Token: `) e lo persiste tramite lo stesso
`SaveOpenBaoIntegrationSettingsHandler` della Fase 1. Se il container esiste già fermo,
errore esplicito (niente riavvio automatico); se Docker non è raggiungibile, errore chiaro
invece di crash. Nuovo progetto `Iris.Infrastructure.Tests` (colma un gap segnalato sopra -
prima nessun adapter aveva test dedicati).

**Verificato end-to-end il 2026-09-08** con Docker Desktop realmente attivo: API avviata su
DB SQLite usa-e-getta, primo admin via `/setup/claim-admin` (bootstrap SSO, nessun SMTP
necessario), chiamata reale a `POST /system/integrations/openbao/provision` → container
`openbao/openbao:2.1` avviato per davvero (`docker ps` lo conferma), banner reale contiene
`Root Token: s.oi9YVUH2BqOJdjRl6BQKpOme`, `ParseRootToken` l'ha estratto correttamente (il
banner contiene anche altre righe con "root token" in prosa - "core: root token generated",
ecc. - che il marker `"Root Token: "` non confonde). Il testo di log reale è ora un test di
regressione permanente in `ProvisionOpenBaoHandlerTests`.

**Bug reale trovato e corretto tramite questa verifica manuale**: `RestartRequired` in
`GET /system/settings` risultava sempre `true` anche senza modifiche pendenti, perché il
confronto trattava "gruppo mai salvato" (null) contro "valore di default da
`appsettings.Development.json`" (AWX/Ansible puntano entrambi a `http://localhost:8043` per
default dev) come una discrepanza. Corretto in `GetSystemSettingsHandler.HasPendingChange`:
un gruppo conta come "in attesa di riavvio" solo se è stato davvero persistito (PUT/provision
chiamato) E il valore persistito differisce da quello attivo - mai per un gruppo ancora
puramente config-driven. Confermato con un secondo giro reale (`restartRequired: false` dopo
il fix, dati identici). Nessun unit test esistente lo aveva intercettato perché usavano dati
di test comodi (active null per i gruppi non toccati) che non replicavano i default reali di
`appsettings.Development.json` - aggiunto un test di regressione che replica esattamente
questo scenario.

**Client MAUI** - flyout custom (`Shell.MenuItemTemplate` è inaffidabile sull'handler
Windows, sostituito da `Shell.FlyoutContentTemplate`, vedi `docs/ui-standards.md` sezione
9), Dashboard sempre prima voce, macro categorie come bottoni collassabili/espandibili
(`Workspace`, `Governance`, `Infrastructure`, `Applications`, `Development`) e gating per
permesso (`AppShellViewModel.CanManageX`). `AppShellViewModel` traccia la route corrente:
la categoria della pagina attiva resta aperta, header e voce attiva sono evidenziati. Le
righe del flyout mostrano testo solo tramite `Label`; i `Button` overlay cliccabili hanno
`Text=""` e `SemanticProperties.Description`, cosi' MAUI/Windows non puo' renderizzare un
secondo testo sopra la voce e non blocca il click del footer `System settings`.
La barra superiore distingue tre layer: la titlebar applicativa/nativa (hamburger, titolo
app, zona centrale e caption button Windows) usa `AppChromeLight`/`AppChromeDark` quando
la finestra ha focus e `AppChromeInactiveLight`/`AppChromeInactiveDark` quando lo perde;
la barra MAUI con il titolo pagina (`Dashboard`, `System settings`, ecc.) usa
`PageTitleBarLight`/`PageTitleBarDark`; il corpo pagina usa `AppBackgroundLight`/
`AppBackgroundDark`.
`AppChromeTheme` riapplica il colore Shell/page title bar via codice quando cambia
`UserAppTheme`, evitando che venga sovrascritto con il background del corpo pagina. Su
Windows il refresh della titlebar nativa passa da `AppWindowTitleBar`, resource WinUI
`WindowCaption*`/`WindowCaptionButton*`, `NavigationViewTopPaneBackground` e `TitleBar*`
(pane toggle/hamburger, foreground, deactivated opacity), con risorse background impostate
come `SolidColorBrush` quando il template WinUI le consuma come brush. Include override
diretto del visual tree del top pane Shell (`TopNavArea`, `PaneToggleButtonGrid`,
`ButtonHolderGrid`, `TogglePaneButton`, `PaneTitleTextBlock`) e del template moderno
WinUI TitleBar (`PART_LayoutRoot`, `PART_PaneToggleButton`, `PART_TitleText`). L'overlay
`IrisTitleBarChromeBackground` resta dentro `RootGrid` per coprire la zona centrale della
fascia nativa, ma viene mantenuto dietro ai controlli reali cosi' non nasconde hamburger
e titolo app. Il titolo app in dark mode viene forzato a bianco pieno (`#FFFFFF`) tramite
`AppWindowTitleBar`, DWM `DWMWA_TEXT_COLOR`, resource brush
`WindowCaptionForeground`/`TitleBarForegroundBrush`, passata sui `TextBlock` nella fascia
fisica della titlebar e refresh lazy sul visual tree, perche' `PART_TitleText` puo'
essere creato dopo il primo render. Lo stato focus e'
tracciato da `WindowActivationState`; include
`RequestedTheme` del root WinUI, refresh su `Loaded`/`ActualThemeChanged`/attivazione
finestra e
`DwmSetWindowAttribute` (`caption`, `text`, `border`, `immersive dark mode`).
`Components` e' visibile solo in build DEBUG sotto la sezione Development e include la
gallery dei controlli globali, incluso `controls:TabGroup`: tab orizzontali con indicatore
attivo, header e contenuto bindati (`ItemsSource` + `SelectedIndex`) e contenuto opzionale
a blocchi (`Text`, `Note`, `Code`). La gallery include anche `controls:CodeBlock`, standard
per snippet/comandi/manifest: usa un `Editor` read-only per rendere il codice selezionabile
e un pulsante copy dedicato con feedback temporaneo tramite spunta verde e tooltip
`Copied`. Flussi secondari tramite
finestre modali OS vere (`IDialogService` -> `Window` MAUI owned + modale su Windows via
Win32), non pannelli in-page. Setup wizard e accept invitation sono fuori dal flyout. Il
flyout header contiene il profilo utente con link `Profile` e `Sign out`; il footer porta
`System settings`. L'app parte da `StartupPage`, uno splash interno che controlla setup e
sessione ricordata prima di mostrare la login; se il token `Remember me` e' valido naviga
direttamente a dashboard/first-login senza flash della login. Login supporta `Remember me`
per sessioni locali persistite in SecureStorage (fallback Preferences) e recupero password
dal form.

**Profile / System settings** - `GET /profile` restituisce `MeResponse`, permessi
effettivi e history delle sessioni; `ProfilePage` espone dati utente, cambio password,
permessi e access history. `GET /system/settings` espone integrazioni OpenBao/Ansible/Azure DevOps/Nexus a
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

**Deployments - associazione completa**: `ApplicationInstallation` ora referenzia un vero
`CustomerContextId` (vedi sopra) e ha una sezione MAUI dedicata organizzata per
customer/context. Resta parziale: FK come `Guid` non navigation EF (stesso stile di
`ApplicationId`/`ServerNodeId`, coerente col resto del modulo ma non navigabile via
`Include`), nessuno stato di ciclo di vita, nessuna UI per modificare i binding dopo la
creazione, nessun vincolo che impedisca di legare un `ServerNode` con `Environment`
diverso dal `Kind` del `CustomerContext` scelto (nessun check, ne' in Validation Engine ne'
lato create).

**Validation Engine**: prima versione presente (vedi sopra) con UI MAUI in
`InstallationOpsDialog`; alcune regole restano euristiche (capability = sempre
`ServiceHost`, nessun check disco) e il parser di versioni copre solo espressioni
semplici. Nessuna regola oggi confronta `CustomerContext.Kind` con `ServerNode.Environment`
(vedi sopra) — candidato naturale per un prossimo check.

**Actions / run history**: `InstallationRun` + `GET .../runs` esistono (vedi sopra). Manca:
`PreparedAction` per la fase di preparazione (draft prima del launch), log completo della
run oltre a `job_explanation`, polling di background (oggi si aggiorna solo quando qualcuno
apre il dettaglio).

**UI MAUI Validation Engine/run history/Deploy**: vive nella sezione **Deployments**, non
sotto Applications — vedi il blocco "Client MAUI - sezione Deployments" sopra per i
dettagli di `InstallationOpsDialog` (Validate/Deploy/Run history) e dello stato di verifica
manuale.

OpenBao/AWX/Ansible/Grafana: gli adapter HTTP esistono (`OpenBaoConnector`, `AwxClient`,
`OpenBaoSecretStore`) con fallback mock non distruttivo. **Fatto** in questa sessione:
endpoint di salvataggio configurazione da UI (`PUT /system/integrations/*`, vedi sopra
"Integration settings persistite"). **Resta da fare**: nessun adapter ha test dedicati
(`AnsibleExecutionPackageBuilder` e' logica pura e andrebbe coperto); Iris non installa/avvia
OpenBao o AWX lei stessa - il wizard raccoglie solo l'intento (`InstallForMe`), non esiste
ancora nessun endpoint `platform.admin` che esegua `docker run`/`ansible-playbook` (nessuna
capacità di eseguire processi di sistema esiste nel repo - da costruire da zero, vedi
`05-next-actions.md`). Grafana resta del tutto assente.

## Migrazioni applicate (ordine)

`InitialAccessModel` -> `AddUserIsProvisioned` -> `AddServers` ->
`AddServerCredentialOwnership` -> `AddUserInvitations` -> `AddEditLocks` ->
`AddUserLocalPassword` -> `AddApplications` -> `AddServerCapacity` -> `AddUserSessions` ->
`AddMailProviderSettings` -> `AddTransactionLog` -> `AddServerDiskReservations` ->
`AddInfrastructureDiscoveryDataServicesAndArtifacts` -> `AddDataServiceCredentialsAndDiscovery` ->
`PersistApplicationManifestSemantics` -> `AddApplicationInstallations` -> `AddInstallationRuns` ->
`AddApplicationInstallationCustomerContext` (drop `Environment`, add `CustomerContextId`) ->
`AddEnvironmentServerAssignments`.
Ogni migrazione esiste in entrambi i provider
(`src/Iris.Infrastructure/Persistence/Migrations` per SQLite,
`src/Iris.Migrations.Postgres/Migrations` per Postgres).
