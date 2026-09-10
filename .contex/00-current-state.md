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

**Generazione scaffold Ansible dal manifest (2026-09-09)** - richiesta esplicita
dell'utente: "tutti i template, role e playbook di awx/ansible devono essere tenuti come
riferimento, ma dovranno essere generati da iris". Scelta la via di mezzo (opzione
approvata via `AskUserQuestion`): Iris genera i **template Jinja2 di configurazione** e
uno **scaffold iniziale** di ruolo/playbook Ansible per una `ApplicationVersion` - un
punto di partenza one-shot da committare a mano nel repo Ansible/AWX dell'operatore, MAI
rigenerato/riscritto silenziosamente da Iris (nessun coupling con
`LaunchApplicationInstallationAwxJobHandler`/deploy). Playbook/ruoli restano manutenuti a
mano dopo la generazione, coerente con la decisione già presa in
`docs/application-configuration-model-analysis.md` ("i file finali non devono essere
generati direttamente da Iris").
- `GET /applications/{applicationId}/versions/{versionId}/ansible-scaffold` (perm
  `applications.read`, stesso gate di `GetApplicationVersionDetail` visto che è
  version-scoped, non installation-scoped) -> `GenerateApplicationAnsibleScaffoldHandler`
  -> `AnsibleScaffoldGenerator` (puro, `internal static`, nessuna I/O): produce
  `AnsibleScaffoldResponse { ApplicationSlug, Version, GeneratedAtUtc, Files[] }`, ogni
  file con `RelativePath`/`Description`/`Content`.
- `ConfigurationKey.TargetKind` è free-form; il generatore lo classifica in 3 categorie
  (non enumerate nel dominio, solo nel generatore): **file-backed** (nome file reale o
  `ansible:j2:<target>`) -> `.j2` completo sotto `roles/<slug>/templates/`; **fragment**
  (`dockerfile:*`/`compose:*`) -> snippet sotto `templates/snippets/`, non un file intero;
  **code-only** (`code:*`) -> nessun template, solo documentato in `defaults/main.yml` e
  README. Format-aware per i file-backed, scelta deliberatamente conservativa (mai
  generare sintassi strutturata a caso): `.properties`/`.env`/nessuna estensione
  riconosciuta -> righe piatte `key={{ iris_x }}`; `.json` -> nesting reale sullo split
  su `:` delle chiavi dotted (convenzione .NET config-binder già presente nei dati, es.
  `ConnectionStrings:Main`), quoting condizionale su `ValueType`
  (integer/decimal/boolean non quotati); `.xml`/`.config`/tutto il resto strutturalmente
  ambiguo -> lista di riferimento commentata (`{# key -> iris_x #}`), MAI una sintassi
  indovinata che sembra valida ma non lo è.
- Skeleton ruolo/playbook generato sempre: `roles/<slug>/defaults/main.yml` (ogni
  variabile `iris_*`, deduplicata per nome, con default sicuro/tipizzato, mai il valore
  reale per le chiavi secret), `tasks/main.yml` (un task `ansible.builtin.template` per
  target file-backed con `dest` segnato `# TODO` - Iris non conosce i path di deploy reali
  - più task `community.docker.docker_container`/`ansible.builtin.systemd_service` per
  unit che li richiedono), `handlers/main.yml`/`meta/main.yml` stub,
  `playbooks/<slug>.yml`, `README.md` con i metadati di generazione e le chiavi
  `code:*` documentate.
- Naming (nome variabile `iris_*`, normalizzazione target `ansible:j2:`, nome file `.j2`,
  inferenza docker/systemd da `ApplicationUnitDefinition`) estratto da
  `GetApplicationInstallationAnsiblePlanHandler` nella nuova `AnsibleNaming` (`internal
  static`, `Iris.Application/Applications`) così piano e scaffold non possono mai
  disallinearsi per la stessa chiave - **nessun cambio di comportamento**, coperto dal
  test esistente `GetApplicationInstallationAnsiblePlan_exports_variables_for_jinja_templates`.
- MAUI: bottone "Generate Ansible scaffold" + `Picker` versione nella card
  `ApplicationsPage` (riusa `VersionOptions`/`SelectedInstallVersion`, già presenti ma
  finora non renderizzati in nessuna XAML). `AnsibleScaffoldDialog` (bind diretto su
  `ApplicationRowViewModel`, stesso pattern di `InstallationOpsDialog`): lista file a
  sinistra, `controls:CodeBlock` a destra (riusa il copy-to-clipboard già esistente, primo
  uso di `CodeBlock` fuori da `ComponentsPage`) - **nessuna nuova infrastruttura di
  file-download/save-as introdotta** (non esisteva alcun precedente nel repo, `FileSaver`
  incluso; scelta esplicita per tenere lo scope MVP, un vero export .zip resta possibile
  in futuro).
- Bug reale trovato e corretto durante l'implementazione:
  `GenerateApplicationAnsibleScaffoldHandler` non era registrato in
  `Iris.Application/DependencyInjection.cs` (`TryAddScoped`) - un handler non registrato
  fa fallire l'INFERENZA del parametro Minimal API (viene letto come body implicito invece
  che come servizio), e questo rompe la endpoint data source **dell'intero gruppo di
  route**, non solo la nuova - da qui il 500 su ogni test di `ApplicationsApiTests`,
  incluse richieste scollegate come `POST /applications`. Diagnosticato leggendo il body
  reale della risposta (Development environment espone il dettaglio dell'eccezione) invece
  di fidarsi del solo status code.
- **346/346 test backend verdi** (56 Domain + 10 Extractor + 170 Application + 37
  Infrastructure + 73 Api; erano 331 prima di questo incremento).

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

**Bug reale (2026-09-09): il wizard di setup non salvava mai OpenBao/AWX "use existing"** -
segnalato dall'utente dopo un test manuale: valori inseriti nei passi 1-2 del wizard, poi in
System settings "Test" non raggiungeva il servizio e "Configure" risultava vuoto, sia per
OpenBao che per AWX. Diagnosticato interrogando direttamente il DB SQLite dev
(`src/Iris.Api/iris.dev.db`, `IntegrationSettings` con **zero righe** nonostante
`Users`/`MailProviderSettings` popolate dalla stessa chiamata) invece di fidarsi solo del
comportamento della UI. Causa reale: `SetupEndpoints.cs`, `POST /setup/complete` costruiva
`new CompleteSetupCommand(body.Mail, body.AdminEmail, body.AdminDisplayName,
body.AdminPassword)` - **senza** `body.OpenBao`/`body.Awx`, che quindi arrivavano sempre
`null` a `CompleteSetupHandler` (i parametri hanno default `null` nel record, quindi nessun
errore di compilazione l'ha mai segnalato) e i due `if (command.OpenBao is { Skip: false,
... })`/`if (command.Awx is { ... })` non scattavano mai, a prescindere da cosa l'utente
avesse scelto/digitato nel wizard MAUI (che invece costruiva `OpenBaoSetupInput`/
`AwxSetupInput` correttamente). Bug puro di wiring nell'endpoint, mai la persistenza né la UI
di lettura, entrambe corrette. **Nessun test API copriva `/setup/complete` con
OpenBao/Awx** (solo `CompleteSetupHandler` testato direttamente a livello Application,
bypassando la mappatura request->command dell'endpoint) - da qui il bug invisibile ai 331+
test esistenti. Corretto passando `body.OpenBao, body.Awx` nel costruttore; aggiunto test di
regressione end-to-end (`SetupApiTests.Setup_persists_openbao_and_awx_use_existing_choices`)
che completa il setup con entrambi "use existing" e verifica via `GET /system/settings` che
`Endpoint`/`Status: "Pending restart"` riflettano davvero i valori inviati - **347/347 test
verdi**.

**Wizard valida OpenBao/AWX + Ansible "managed via AWX" (2026-09-10)** - due follow-up dal
primo giro pulito del wizard (post-fix `/setup/complete`):
1. *Il wizard non testava OpenBao/AWX.* L'SMTP fa un connect+auth+send reale prima di
   persistere; OpenBao/AWX venivano salvati alla cieca. Aggiunto `IIntegrationReachabilityProbe`
   (`Iris.Application.Abstractions`, stesso schema di `IEmailSender.TestConnectionAsync` +
   `MailConnectionException`/`MailTestStage`): `IntegrationConnectionException` con
   `IntegrationTestStage` (`Connect`/`Authenticate`). Impl `IntegrationReachabilityProbe`
   (`Iris.Infrastructure/Integrations`, singleton, `HttpClient` proprio 10s, ctor `internal`
   con `HttpMessageHandler` come test seam). OpenBao: `GET {ep}/v1/sys/health` (qualsiasi
   risposta HTTP = "c'è") + se token `GET /v1/auth/token/lookup-self` con `X-Vault-Token` (non
   2xx → `Authenticate`). AWX: `GET {ep}/api/v2/ping/` + se token `GET /api/v2/me/` con
   `Bearer` (401 → `Authenticate`). `CompleteSetupHandler` chiama la probe *prima* di
   `saveOpenBao`/`saveAwx`, converte `IntegrationConnectionException` → `ValidationException`
   ("OpenBao: ..." / "AWX: ..."), quindi `/setup/complete` torna 400 e non persiste nulla;
   nel wizard MAUI compare come `AdminError` (stesso path del fallimento SMTP).
2. *Ansible non deve essere un'integrazione a sé.* Iris non esegue mai `ansible-playbook`:
   `AnsibleExecutionPackageBuilder.Build()` compone solo gli `extra_vars` che il launch AWX
   invia via HTTP; AWX pilota l'unica Ansible sul server ops. Rimossa la probe locale
   `ansible-playbook --version` (e la dipendenza `IProcessRunner`): `GetStatusAsync` ora
   ritorna **"Managed via AWX"** (endpoint vuoto, caso normale, nessuna probe) oppure
   "Configured" se qualcuno imposta esplicitamente un endpoint diretto (comunque non
   health-checkato). `appsettings.Development.json`: `Ansible:Endpoint` → `""` (era
   `http://localhost:8043`, la causa del "configurato ma irraggiungibile" in dashboard).
   Rimosso il fallback legacy `awxEndpoint = integrations["Ansible:Endpoint"]` in
   `RegisterIntegrations` (AWX e Ansible ora del tutto disaccoppiati).
   `DashboardViewModel.RefreshIntegrationWarningAsync` tratta "Managed via AWX" come sano. Il
   bottone Configure/Test della riga Ansible resta (escape hatch per chiamate dirette,
   `SaveAnsibleIntegrationSettings*` intatti). "Pending restart" per OpenBao/AWX dopo il
   wizard resta corretto e atteso (conferma che la persistenza ora funziona; serve un riavvio
   di Iris.Api).
- Hardening della probe dopo il primo test manuale (il wizard restava a girare senza mostrare
  nulla su un endpoint problematico): `IntegrationReachabilityProbe` ora `AllowAutoRedirect =
  false` + `UseProxy = false`, timeout **per singola richiesta** via `CancellationTokenSource`
  (6s) linkato al token del chiamante, catch allargato a `OperationCanceledException`/
  `UriFormatException`. Un 3xx non viene più seguito (seguendolo su cambio schema si perde
  l'header `Authorization` → "rejected the token" fuorviante) ma riportato come errore
  `Connect` che nomina la `Location` ("AWX redirects http://... -> https://...; use that URL").
  `SetupWizardViewModel.CompleteAsync` ora ha anche un catch per `TaskCanceledException`/
  `TimeoutException` + un catch generico: il wizard non resta mai appeso senza messaggio.
- Verifica: `dotnet test Iris.sln` **356/356 verdi** (10 test probe HTTP con stub handler,
  incl. redirect-non-seguito + 2 handler-level + 1 API-level; -2 test CLI Ansible rimossi).
  `dotnet build src/Iris.App` verde. **Da verificare a mano dall'utente** rifacendo il wizard
  con endpoint/token errati (deve rifiutare con messaggio, non appendersi) poi corretti.

**AWX OAuth2 refresh-token (2026-09-10)** - l'access token AWX dell'utente scade dopo 1 giorno
e la scadenza non è modificabile sulla sua istanza; Iris risolveva il bearer una volta
all'avvio e non lo rinnovava mai, quindi i deploy iniziavano a fallire con 401 dopo un giorno.
Scelta dell'utente (via `AskUserQuestion`, alternativa era HTTP Basic con service account):
implementato il flusso **OAuth2 refresh**. Confermato sulla doc AWX: refresh =
`POST {endpoint}/api/o/token/`, `Basic base64(client_id:client_secret)`, form
`grant_type=refresh_token&refresh_token=<rt>`; la risposta porta un **nuovo** `refresh_token`
("the refresh operation replaces the existing token by deleting the original") → Iris deve
ripersistere access **e** refresh token dopo ogni refresh.
- `IntegrationSettings` +3 colonne (migration `AddAwxOAuthRefreshToIntegrationSettings`,
  SQLite + Postgres, generate con `dotnet ef`, parità verificata): `AwxOAuthClientId` (colonna
  in chiaro, è un identificatore), `AwxOAuthClientSecretReference` / `AwxRefreshTokenSecretReference`
  (ref opache su `ISecretStore`, path logici `awx/oauth-client-secret` / `awx/refresh-token`).
  `ConfigureAwx` allargato. Tutto **opzionale**: senza le 3 credenziali AWX si comporta come
  prima (token statico, 401 resta 401).
- `AwxOptions` +`OAuthClientId`/`OAuthClientSecret`/`RefreshToken` + `CanRefresh`. `AwxClient`
  (singleton) ora inietta `ISecretStore` (singleton; `StoreAsync` ritorna una ref
  **deterministica** per path logico, quindi ripersistere su `awx/token`/`awx/refresh-token`
  non invalida le ref in `IntegrationSettings` — nessuna scrittura DB nel refresh). `_accessToken`/
  `_refreshToken` mutabili, `SemaphoreSlim(1,1)` + guardia stale-token per il refresh
  concorrente. `SendWithAuthRetryAsync`: su 401 con `CanRefresh` → refresh una volta → riprova.
  Applicato a `LaunchAsync`/`GetJobStatusAsync`/probe. Ctor `internal (AwxOptions, ISecretStore,
  HttpMessageHandler)` come test seam.
- `IIntegrationReachabilityProbe.ProbeAwxAsync` ritorna `AwxProbeResult(RefreshedAccessToken,
  RefreshedRefreshToken)` e, quando gli passi le credenziali OAuth, **esegue il primo refresh**
  (prova endpoint + client creds + refresh token in un colpo) e restituisce la coppia ruotata.
  `CompleteSetupHandler`/`SaveAwxIntegrationSettingsHandler` persistono quella coppia (non
  quella digitata) — il refresh token digitato viene consumato/ruotato dalla validazione
  stessa, niente refresh token morto lasciato in giro.
- Contratti `SaveAwxIntegrationSettingsRequest`/`AwxSetupInput` + `SaveAwxIntegrationSettingsCommand`
  +3 campi opzionali (`string? = null`, non breaking). Endpoint AWX PUT + `/setup/complete`
  mapping aggiornati. MAUI: 3 campi (client id / client secret / refresh token) in
  `ConfigureAwxDialog` e nello step AWX del wizard, con caption "AWX tokens expire; fill these
  to auto-renew". Dialog AWX 520×660.
- **Client OAuth2 Public supportato** (l'utente ha un'Application AWX che dà solo client id +
  token + refresh token, nessun secret): `AwxOAuth.BuildRefreshRequest` — se il client secret
  è presente → HTTP Basic (Confidential, come da doc AWX); se assente → `client_id` nel body
  del form, nessun header Authorization (Public). `CanRefresh` non richiede più il secret.
- Verifica: `dotnet test Iris.sln` **366/366 verdi** (`AwxClientTests`: 401→refresh→retry /
  rotazione persistita / refresh fallito → ValidationException / doppio-401 concorrente = 1
  solo refresh / **Public client = client_id nel body, niente Basic**; probe: refresh
  Confidential + **Public**; 1 handler setup; +1 Domain `ConfigureAwx`; test API setup passa
  anche i campi OAuth). `dotnet build src/Iris.App` verde. **Da verificare a mano dall'utente**
  col wizard + credenziali OAuth reali (nel suo caso: Application Public → lasciare vuoto il
  campo client secret).

**AWX: risoluzione lazy dei segreti + auto-unlock del wizard (2026-09-10)** - dopo il primo giro
reale, l'utente ha visto: dopo il wizard → riavvio → unlock, AWX restava "Not configured" e i
Test fallivano. Causa: `RegisterIntegrations` risolveva `AwxOptions.Token` **una volta
all'avvio**, ma il token era nel vault di fallback in memoria (vuoto all'avvio, si popola solo
con l'unlock a runtime) → `IsConfigured` falso per sempre, e le options singleton non
rileggono dopo l'unlock. Serviva un secondo riavvio che comunque non risolveva.
- **`AwxClient` ora risolve token / client secret / refresh token in modo lazy** al primo uso
  (`EnsureCredentialsAsync`, doppia guardia con `SemaphoreSlim`): usa il valore da config/env se
  presente, altrimenti fa `ISecretStore.RetrieveAsync(reference)` dal riferimento persistito —
  come già fa `SmtpEmailSender` per la password SMTP. `AwxOptions` ha ora sia i valori
  (`Token`/`OAuthClientSecret`/`RefreshToken`, per config/env) sia i riferimenti
  (`*SecretReference`, mutuamente esclusivi). `IsConfigured`/`CanRefresh` considerano "valore
  OPPURE riferimento". `RegisterIntegrations` non chiama più `ResolvePersistedToken` per AWX
  (resta per AzureDevOps/Nexus/OpenBao) — passa i riferimenti. Se `RetrieveAsync` torna null
  (vault ancora bloccato) → `ValidationException`/status "Unreachable" con messaggio "unlock the
  fallback secrets in System settings". **Nuovo flusso**: wizard → auto-unlock → **1 solo
  riavvio** → unlock (restore) → AWX funziona subito via lazy resolve, niente secondo riavvio.
- **Il wizard fa Unlock automatico** subito dopo `ApplySessionAsync` (ha ancora in mano la
  password admin): `_api.UnlockFallbackSecretsAsync(AdminPassword)` best-effort — i 5 segreti
  diventano durevoli subito, il banner "5 to save" si risolve da solo. Il pulsante Unlock
  manuale resta come fallback.
- **Anche la login password fa Unlock automatico**: `AuthService.SignInAsync`, dopo un
  `ApplySessionAsync` riuscito, chiama `UnlockFallbackSecretsAsync(password)` best-effort se
  l'utente è `platform.admin` — così dopo un riavvio di Iris.Api l'admin non deve più cliccare
  "Unlock" in System settings, basta rifare login. Non copre il resume da "remember me" (lì non
  c'è password) né l'SSO (nessuna password locale): in quei casi resta il pulsante manuale.
- OpenBao resta il caso non risolvibile lazy (il suo token serve a costruire `OpenBaoSecretStore`
  stesso — chicken-and-egg vero): per averlo stabile va messo in `appsettings`/env all'avvio,
  altrimenti Iris gira sul vault di fallback cifrato con un unlock per riavvio (modello scelto
  dall'utente: nessun auto-decrypt al boot).
- MAUI: `SystemSettingsPage` — la lista "Service connections" era un `CollectionView`
  `HeightRequest="220"` con scroll interno → sostituito con `VerticalStackLayout` +
  `BindableLayout` che si adatta al contenuto.
- Verifica: `dotnet test Iris.sln` **368/368 verdi** (+2 `AwxClientTests`: risoluzione lazy da
  reference / fallimento chiaro se il reference non è ancora risolvibile). `dotnet build
  src/Iris.App` verde. **Da verificare a mano dall'utente**.

**Promozione runtime del secret store a OpenBao (2026-09-10)** - risolve il chicken-and-egg:
il token di OpenBao inserito da UI finisce nel vault di fallback, non può bootstrappare
`OpenBaoSecretStore` all'avvio, quindi Iris resta sul fallback e mostra "token missing, uses
the encrypted fallback secret store". Ora c'è la **promozione a runtime**.
- `SwitchableSecretStore` (`Iris.Infrastructure/Secrets`, singleton, **unico** `ISecretStore`
  registrato): delega al `EncryptedFallbackSecretStore` (o direttamente a OpenBao se un token
  è in config/env all'avvio) e può essere **commutato a OpenBao a runtime** via il nuovo port
  `ISecretStorePromotion`. `PromoteToOpenBaoAsync`: verifica OpenBao con un round-trip KV
  (write/read/delete di `iris/_promotion-probe`), copia **ogni** segreto dal cache di fallback
  in OpenBao al suo logical path, poi swap atomico di `_active`. Fallimento verifica →
  `SecretStorePromotionException`, nessuno swap.
- `OpenBaoSecretStore.TryParseReference` ora tollera anche i riferimenti formato fallback
  (`mock-openbao:<path>` → `<path>`), così **nessuna riga persistita va riscritta**: dopo la
  promozione `IntegrationSettings.AwxTokenSecretReference = "mock-openbao:awx/token"` risolve
  leggendo da OpenBao al path `awx/token` (dove la migrazione ha scritto il valore).
- `PromoteSecretStoreToOpenBaoHandler` + `POST /system/integrations/openbao/promote`
  (`platform.admin`): legge `IntegrationSettings`, risolve il token OpenBao dal secret store
  attivo (fallback sbloccato), chiama la promozione, ritorna `PromoteSecretStoreResponse`
  (Promoted, MigratedSecrets, Message). `ValidationException` se OpenBao non configurato / token
  non risolvibile / già attivo.
- **Auto-promozione dopo l'unlock**: `UnlockFallbackSecretsHandler`, dopo `vault.UnlockAsync`,
  chiama best-effort il promote handler (swallow `ValidationException`). Combinato con
  l'auto-unlock su login, ogni riavvio diventa: login → unlock → auto-promote, trasparente,
  finché OpenBao è raggiungibile e il token è nel vault. **Nessun flag persistito** (token nel
  vault + OpenBao che verifica = segnale sufficiente, idempotente).
- `OpenBaoConnector` inietta `ISecretStorePromotion`: quando `IsOpenBaoActive` (o token in
  config) il messaggio è il normale "Mount: …" invece di "uses the fallback store".
- `FallbackSecretVault` ora sempre registrato (rimosso `NullFallbackSecretVault`): con OpenBao
  attivo il cache è vuoto → `GetStatusAsync` None, `UnlockAsync` no-op.
- MAUI: bottone **"Promote"** sulla riga OpenBao (accanto a Provision), `_promoter` su
  `IntegrationConnectionRow` sullo stesso pattern di `_provisioner`.
- Verifica: `dotnet test Iris.sln` **381/381 verdi** (+4 `SwitchableSecretStoreTests`, +5
  `PromoteSecretStoreToOpenBaoHandlerTests`, +3 API). `dotnet build src/Iris.App` verde.
  **Da verificare a mano dall'utente**: login → la riga OpenBao passa da "uses the fallback
  store" a "Configured" senza riavvio; Test OpenBao e AWX passano; nei dati di OpenBao
  compaiono `secret/data/awx/token`, `secret/data/mail/smtp`, ecc.

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

**MAUI collegata al provisioning OpenBao + tre bug reali corretti da test manuale utente
(2026-09-08)** - dopo il collegamento UI (bottone `Provision` in `SystemSettingsPage`,
hand-off automatico dal wizard post-login se `OpenBaoProvisionRequested`), l'utente ha
testato manualmente le tre feature principali e riportato tre problemi concreti, tutti
diagnosticati e corretti nella stessa sessione:

1. *"connettiti ad OpenBao già presente ... non salva token o non sembra comunicare col
   servizio"* - non era un bug di salvataggio (il PUT persiste correttamente), ma di
   visibilità: `GetSystemSettingsHandler` mostrava sempre lo stato/endpoint del connettore
   *attivo* (fissato all'avvio del processo), mai il valore appena persistito, quindi un
   salvataggio riuscito appariva identico a un no-op fino al riavvio. Corretto: quando un
   gruppo (`openbao`/`awx`/`ansible`) ha un salvataggio pendente (stessa logica di
   `RestartRequired`), la riga integrazione ora mostra lo `Status` `"Pending restart"` e
   l'endpoint appena salvato invece di quello ancora attivo, con messaggio esplicito
   `"Saved. Restart Iris.Api to connect using this endpoint."`. Nuovo helper
   `GetSystemSettingsHandler.OverrideIfPendingRestart`, due test di regressione in
   `IntegrationSettingsHandlersTests`.
2. *"installa per me dopo la login non fa partire il container"* - bug reale, non solo di
   percezione: `SetupWizardViewModel.CompleteAsync` invocava `Completed` (che naviga subito
   alla dashboard, `SetupWizardPage.xaml.cs`) incondizionatamente subito dopo il tentativo
   di provisioning, scartando silenziosamente qualunque messaggio di errore
   (`openBaoProvisionWarning`) prima ancora che l'utente potesse leggerlo - il container
   poteva fallire ad avviarsi (Docker non attivo, container fermo preesistente, root token
   non leggibile) senza che nulla fosse visibile. Corretto: nuova property
   `SetupCompleted` + comando `ContinueToDashboardCommand`; `Completed` viene invocato
   subito solo se non c'è alcun warning (flusso invariato per il caso di successo), altrimenti
   lo step 4 resta a schermo mostrando l'errore in `AdminError` e un bottone "Continue to
   dashboard" per proseguire consapevolmente.
3. *"configura dopo non c'è un modo per configurarlo/testarlo in un secondo momento"* -
   gap reale confermato: nessun metodo client/UI chiamava mai `PUT
   /system/integrations/openbao` fuori dal wizard one-shot. Aggiunto
   `IIrisApiClient.SaveOpenBaoIntegrationSettingsAsync`, dialog
   `ConfigureOpenBaoDialog`/`ConfigureOpenBaoDialogViewModel` (stesso pattern sicuro
   `CloseRequested`+`WasSaved` di `SelectApplicationForDeploymentDialog`) e bottone
   `Configure` sulla riga OpenBao in `SystemSettingsPage`, visibile solo a chi ha
   `platform.admin`.

Verificato con `dotnet build` di `Iris.App`/`Iris.Application` e l'intera suite
(`dotnet test Iris.sln`, 261/261 verdi dopo le modifiche). Non ancora ri-testato a mano
nell'app Windows in esecuzione dopo questi tre fix - da fare prima di chiudere
definitivamente i tre problemi riportati.

**Secondo giro di test manuale utente (2026-09-08, stesso giorno): 6 problemi ulteriori
corretti** - dopo i tre fix precedenti, l'utente ha rifatto il giro di test end-to-end e
trovato altri problemi reali:

1. *"installa per me" ancora non avvia il container* - causa reale: un container
   `iris-openbao` **fermo** (lasciato da un test manuale precedente) faceva fallire
   `ProvisionOpenBaoHandler` con un errore esplicito ("rimuovilo con `docker rm`"),
   comportamento *by design* nella Fase 2 ma ostile in pratica. Corretto: nuovo
   `IContainerRuntime.StartAsync` (shell a `docker start`), usato per riavviare un container
   fermo invece di fallire - dev-mode OpenBao non mantiene stato tra i riavvii, quindi è
   sicuro quanto crearne uno nuovo. Corretto anche `ProvisionOpenBaoHandler.ParseRootToken`
   per prendere l'**ultima** occorrenza di `Root Token:` nei log (non la prima): dopo un
   riavvio `docker logs` restituisce l'intera storia del container, banner vecchio incluso,
   e il token vecchio non è più valido.
2. *SMTP non editabile in System settings* - vero gap: non esisteva alcun endpoint per
   modificare l'SMTP dopo il wizard iniziale. Aggiunto `PUT /system/settings/mail`
   (`SaveMailProviderSettingsHandler`) e `POST /system/settings/mail/test` (riusa
   `TestMailConnectionHandler`), entrambi `platform.admin`. A differenza di
   OpenBao/AWX/Ansible **non serve alcun riavvio**: `SmtpEmailSender` legge le impostazioni
   dal DB a ogni invio, non le fissa in un singleton all'avvio. Dialog MAUI
   `ConfigureMailDialog` con bottone "Send test" prima di salvare.
3. *Ansible sempre "Configured", Test non fa nulla* - due bug distinti, entrambi corretti in
   `AnsibleExecutionPackageBuilder`: (a) "Configured" era calcolato su `Playbook`, che ha
   sempre un default non vuoto (`iris-deploy-application.yml`) - cambiato per riflettere
   `Endpoint`, stessa convenzione di OpenBao/AWX; (b) `GetStatusAsync(probe:true)` ignorava
   completamente `probe` e restituiva sempre lo stesso valore statico - ora esegue
   davvero `ansible-playbook --version` tramite `IProcessRunner` (stesso seam della Fase 2)
   e riporta `Reachable`/`Unreachable` in base all'esito, l'unico controllo onesto possibile
   oggi dato che Ansible qui è un eseguibile CLI locale, non un servizio HTTP remoto.
4. *AWX "Not configured", Test non fa nulla, non configurabile* - Test non faceva nulla
   perché non c'era nulla da testare (nessuna configurazione); il vero problema era
   l'assenza di un modo per configurarlo. Aggiunto `ConfigureAwxDialog` (stesso pattern di
   OpenBao).
5. *Nexus "configured" ma Test senza feedback utile* - nessun `IIntegrationConnector` reale
   esiste per nexus/azure-devops (solo voci di visualizzazione derivate da
   `IConfiguration`, mai state costruite oltre quello): cliccare Test produceva un 404 letto
   come "Unreachable" - tecnicamente un feedback, ma fuorviante per qualcosa mai davvero
   controllato. Scelta onesta: bottone Test nascosto per queste due integrazioni
   (`IntegrationConnectionRow.CanTestAtAll`), sostituito da un'etichetta "Not available yet"
   invece di fingere un controllo che non esiste.
6. *Nessuna notifica in dashboard* - `DashboardViewModel` (finora dati 100% mock) ora
   chiama `GET /system/settings` (solo per `platform.admin`) e mostra un banner con la
   lista di ciò che richiede attenzione (SMTP non configurato, OpenBao/AWX/Ansible non
   `Configured`/`Reachable`, o `RestartRequired`), con bottone verso System settings.

Verificato: `dotnet build` di `Iris.App`/`Iris.sln` verdi, `dotnet test Iris.sln` **278/278
verdi** (17 nuovi test: riavvio container fermo, token più recente, Ansible
Configured-by-endpoint + probe reale, salvataggio SMTP con/senza password, gate reader su
mail). **Non ancora ri-testato a mano nell'app Windows in esecuzione dopo questo secondo
giro** - prossimo passo prima di considerare l'intera area OpenBao/AWX/Ansible/SMTP chiusa.

**Vault cifrato per i segreti pre-bootstrap (password dell'utente, sblocco esplicito)** -
richiesta esplicita dell'utente (2026-09-08): "Il token di OpenBao deve essere persistito in
DB criptato con la password dell'utente che lo setta. Tutto il sistema... deve essere
criptato." Prima di questo incremento, `InMemorySecretStore` (fallback quando OpenBao non è
ancora configurato) teneva i segreti in chiaro in RAM, persi a ogni riavvio - noto e
documentato, ma mai risolto. Design (via Plan Mode, due decisioni confermate
dall'utente: sblocco che richiede sempre re-inserimento password - mai automatico da hash
salvato - e copertura di *tutti* i segreti pre-bootstrap, non solo il token OpenBao):

- `ISecretStore` **non tocca il proprio contratto**: `EncryptedFallbackSecretStore`
  (`Iris.Infrastructure/Secrets/`, sostituisce `InMemorySecretStore` in DI) si comporta
  esattamente come prima - un dizionario in RAM, stesso schema di reference `mock-openbao:*` -
  quindi zero modifiche per ogni chiamante esistente (`SaveOpenBaoIntegrationSettingsHandler`,
  `ServerCredentialFactory`, ecc.).
- Nuova entità `EncryptedSecretEntry` (`Iris.Domain.Secrets`, una riga per reference, non un
  singleton) persiste il valore cifrato: `aesgcm-pbkdf2sha256$iterazioni$salt$nonce$tag$cifrato`
  (`AesGcmSecretProtector`, prima cifratura simmetrica del repo - AES-256-GCM con AAD = la
  reference stessa, così un blob scambiato tra due righe fallisce subito invece di decifrare
  silenziosamente sotto la riga sbagliata). Chiave derivata via PBKDF2-SHA256 (210.000
  iterazioni, stesso fattore di costo di `Pbkdf2PasswordHasher` ma KDF distinta con salt
  proprio - mai la hash di login riusata come chiave).
- `FallbackSecretVault` (nuovo servizio, port `IFallbackSecretVault`) fa da ponte tra il
  dizionario in RAM e le righe cifrate su DB, SOLO dietro un'azione esplicita
  `POST /system/settings/secrets/unlock` (`platform.admin`, verifica la password contro
  `PasswordHash` dell'utente reale prima di usarla): persiste ogni segreto ancora solo in RAM
  (durevole da quel momento) e ripristina in RAM ogni riga durevole di proprietà dello stesso
  utente non ancora caricata. Righe di un admin diverso restano intoccate (solo chi ha
  impostato un segreto può sbloccarlo - limite operativo noto e documentato, non risolto: se
  quella persona non c'è, va reinserito da zero). Un re-salvataggio della stessa reference da
  un admin diverso **riassegna la proprietà** (sicuro, dietro platform.admin, non rivela mai
  il valore precedente) - riportato onestamente come `OwnershipTransferred`, non nascosto.
  `NullFallbackSecretVault` sostituisce `FallbackSecretVault` quando OpenBao è già configurato
  (niente da sbloccare in quel mondo).
- `GET /system/settings` espone `FallbackSecrets` (bool `HasPendingWork` + conteggi, mai i
  nomi dei segreti) solo a `platform.admin`; `SystemSettingsPage` mostra un banner "Unlock"
  quando c'è qualcosa in sospeso, dialog MAUI `UnlockFallbackSecretsDialog` (stesso pattern
  sicuro `CloseRequested`/`WasSaved` degli altri dialog Configure).
- **Bug reale trovato e corretto durante la verifica end-to-end**: sia `GetSystemSettingsHandler`
  sia il primo tentativo di `UnlockFallbackSecretsHandler` risolvevano l'utente corrente via
  `ICurrentUser.UserId` (claim `iris:uid`, stampata da `AccessProvisioningClaimsTransformation`)
  - per una richiesta autenticata via header dev **con password** quella claim non risultava
  sempre presente, pur con `/me` e l'autorizzazione `platform.admin` perfettamente funzionanti
  sulla stessa richiesta. Corretto risolvendo l'utente via
  `IUserProvisioningService.EnsureProvisionedAsync` (per `ExternalId`, stesso schema già
  usato da `SetMyPasswordHandler` in `ManageMyPassword.cs`) invece di leggere la claim
  direttamente - bug pre-esistente nell'infrastruttura di auth condivisa, mai emerso prima
  perché nessun altro handler dipendeva da `ICurrentUser.UserId` così direttamente; non
  toccata la claims transformation stessa (fuori scope). Il test end-to-end
  (`FallbackSecretVaultApiTests`, via `IrisApiFactory` reale) che replica esattamente il
  flusso dev-header+password è ciò che ha trovato il bug.
- Migrazione `AddEncryptedSecretEntries` (SQLite + Postgres, colonne verificate a parità).
  52+31+148+69 = 310 test totali verdi dopo l'incremento (43 nuovi: entità dominio, crypto
  helper con test di manomissione/AAD, store/vault via SQLite reale, handler applicativo,
  endpoint API end-to-end).
- **Non ancora verificato a mano nell'app Windows in esecuzione**.

**Servizio di health-check periodico per le integrazioni** - richiesta esplicita dell'utente
(2026-09-08): "dovrebbe esserci un servizio che controlla i servizi connessi se sono
raggiungibili e configurati correttamente [...] e mostrare nella sessione system se sono ok".
Prima di questo incremento l'unico modo di sapere se OpenBao/AWX/Ansible fossero *davvero*
raggiungibili (non solo "configurati") era cliccare manualmente "Test" - `GET
/system/settings` mostrava solo presenza di configurazione (`probe:false`, economico), mai
una verifica reale in automatico.

- `IntegrationHealthCheckBackgroundService` (`Iris.Api/Diagnostics/`, **primo
  servizio in background/schedulato del repo**) - `BackgroundService` con `PeriodicTimer`
  (intervallo configurabile via `Iris:HealthCheck:IntervalMinutes`, default 5 minuti; primo
  giro immediato all'avvio). Deliberatamente sottile: chiama solo
  `IIntegrationHealthChecker.RunOnceAsync`, tutta la logica vera vive in
  `Iris.Infrastructure/Integrations/IntegrationHealthChecker.cs` (testabile senza un vero
  timer) - itera `IEnumerable<IIntegrationConnector>` (openbao/awx/ansible), fa una probe
  reale (`probe:true`) per ciascuno con isolamento per-connettore (un fallimento non blocca
  gli altri) e registra l'esito in `IIntegrationHealthMonitor` (singleton in RAM,
  `ConcurrentDictionary`, non persistito - si ripopola da solo a ogni ciclo).
- **Deliberatamente separato** dall'endpoint `/health` di ASP.NET Core già esistente (usato
  per liveness/readiness da orchestratori): un OpenBao down non deve far apparire Iris.Api
  stesso "unhealthy" e rischiare un riavvio del container per un motivo esterno a Iris.Api.
  Segnale puramente informativo per gli operatori, esposto solo via `GetSystemSettingsHandler`.
- `GetSystemSettingsHandler` sovrappone l'ultimo esito reale del monitor allo stato
  `probe:false` di ciascuna riga integrazione, ma **solo se non c'è già un salvataggio in
  attesa di riavvio** (altrimenti mostrerebbe lo stato stantio della configurazione VECCHIA
  invece di "Pending restart", che è il segnale più utile in quel momento) - ordine di
  priorità esplicito e testato. Anche un click manuale su "Test" registra il proprio esito
  nello stesso monitor condiviso (`GET /system/integrations/{key}/status?probe=true`), così
  il prossimo caricamento di System settings mostra subito "Checked just now" invece di
  aspettare il prossimo ciclo schedulato.
- `IntegrationLinkResponse` porta ora `CheckedAtUtc` (nullable); `SystemSettingsPage` mostra
  "Checked Xm ago" sotto ogni riga integrazione quando disponibile.
- Nel test host (`IrisApiFactory`) `IIntegrationHealthChecker` è sostituito con un no-op
  (`NoOpIntegrationHealthChecker`) - il vero checker per Ansible farebbe un vero spawn di
  processo (`ansible-playbook --version`) a ogni avvio di test, non deterministico tra
  macchine diverse; stessa logica già usata per `FakeEmailSender`/`FakeContainerRuntime`.
- **Interpretazione deliberata, non ancora confermata dall'utente**: "tentare l'avvio" (dalla
  richiesta originale) non è stato implementato come avvio automatico in background di
  container/processi esterni - resta un'azione esplicita (il bottone "Provision" già
  esistente per OpenBao), coerente con il resto della sessione (sblocco segreti, provisioning,
  configurazione: sempre azione esplicita dell'operatore, mai automatica e silenziosa). Da
  confermare con l'utente se questo è ciò che intendeva o se vuole un tentativo di
  riavvio/provisioning automatico quando il check periodico trova un servizio non
  raggiungibile.
- 52+10+37+150+70 = 319 test totali verdi (9 nuovi: monitor RAM, checker con isolamento
  errori, due test di precedenza "pending restart vs check reale" a livello handler, test
  end-to-end che una probe manuale finisce nel monitor condiviso).
- **Non ancora verificato a mano nell'app Windows in esecuzione.**

**Bug reale trovato dall'utente subito dopo**: Azure DevOps e Nexus Repository apparivano
"Configured" nella UI pur non essendo mai stati configurati da nessuno. Causa:
`GetSystemSettingsHandler` leggeva `Iris:Integrations:AzureDevOps/Nexus:Endpoint` da
`appsettings.Development.json`, che spedisce valori placeholder d'esempio
(`https://dev.azure.com/your-organization`, `http://localhost:8081`) mai realmente impostati
da un admin, e trattava qualunque stringa non vuota come "Configured". Nessuno dei due ha mai
avuto un connettore reale né un modo per essere configurato da Iris (da qui la scelta
precedente di nascondere Test/Configure con "Not available yet") - quindi mostrare
"Configured" era doppiamente fuorviante. Corretto: questi due ora riportano sempre
"Not configured", `Endpoint = null`, a prescindere da cosa c'è in config;
`GetSystemSettingsQuery` non porta più `AzureDevOpsEndpoint`/`NexusEndpoint` (parametri mai
davvero usati per nulla di significativo, rimossi insieme alla lettura di `IConfiguration`
nell'endpoint). Test di regressione dedicato. 320/320 test verdi dopo il fix.

**Azure DevOps e Nexus: supporto reale (stesso schema di OpenBao/AWX/Ansible)** - subito
dopo il fix sopra, l'utente ha chiesto di costruire il supporto vero per entrambi, con ambito
"solo raggiungibilità" per ora (nessuna funzionalità applicativa collegata, es. niente lettura
pipeline/artifact). Stesso schema esatto delle altre tre integrazioni, replicato:

- `IntegrationSettings` (entità dominio) estesa con
  `AzureDevOpsEndpoint`/`AzureDevOpsTokenSecretReference`/`NexusEndpoint`/
  `NexusTokenSecretReference` + `ConfigureAzureDevOps`/`ConfigureNexus`. Migrazione
  `AddAzureDevOpsAndNexusIntegrationSettings` (SQLite + Postgres, colonne verificate a
  parità).
- `SaveAzureDevOpsIntegrationSettingsHandler`/`SaveNexusIntegrationSettingsHandler` +
  `PUT /system/integrations/azure-devops`/`PUT /system/integrations/nexus` (platform.admin),
  stesso comportamento "token vuoto mantiene quello già salvato".
- `AzureDevOpsConnector`/`NexusConnector` (`Iris.Infrastructure/Integrations/`), registrati
  come `IIntegrationConnector` reali - ora coperti dallo stesso ciclo di
  `GetSystemSettingsHandler`/health-check periodico/banner "Pending restart" delle altre tre.
  Verifica di raggiungibilità: Azure DevOps chiama `GET {org}/_apis/projects?api-version=7.1`
  con Basic auth (username vuoto, PAT come password - convenzione Azure DevOps); Nexus chiama
  `GET {endpoint}/service/rest/v1/status` (anonimo, non richiede token per il check - il
  token è comunque raccolto/salvato per quando servirà per operazioni reali sugli artifact).
  Per Azure DevOps "Configured" richiede endpoint+token (nessuna chiamata reale è possibile
  senza PAT); per Nexus basta l'endpoint (lo status check è anonimo).
- `GetSystemSettingsHandler`: rimossa la logica fallback "Not available yet"/`NotYetAvailable`
  introdotta nel fix precedente - non serve più, questi due passano ora dallo stesso ciclo
  connettori reale di openbao/awx/ansible. `GetSystemSettingsQuery` torna a essere solo
  `CanManageSystem` (nessun parametro riaggiunto).
- MAUI: `ConfigureAzureDevOpsDialog`/`ConfigureNexusDialog` (stesso pattern sicuro
  `CloseRequested`/`WasSaved`), bottoni Configure/Test ora attivi anche per queste due righe
  in `SystemSettingsPage`.
- 56+10+157+37+71 = 331 test totali verdi (11 nuovi: due mutator di dominio, due handler Save
  con relative validazioni, due test end-to-end su `GetSystemSettingsHandler` che esercitano
  la pipeline reale con connettori finti, gating permessi + happy path a livello API).
- **Non ancora verificato a mano nell'app Windows in esecuzione.**

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
