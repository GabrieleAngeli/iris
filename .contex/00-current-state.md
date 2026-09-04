# Stato corrente

Aggiornato: 2026-09-03. Verificato in questa sessione con `dotnet test Iris.sln`
(166/166 verdi) e build MAUI verde con `dotnet build Iris.App.sln --no-restore
-p:UseAppHost=false -p:BaseOutputPath=...\artifacts\verify-build\` (output standard
bloccato se l'app e' gia' aperta).

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
corrente.
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
`AddMailProviderSettings` -> `AddTransactionLog` -> `AddServerDiskReservations` ->
`AddInfrastructureDiscoveryDataServicesAndArtifacts` -> `AddDataServiceCredentialsAndDiscovery`.
Ogni migrazione esiste in entrambi i provider
(`src/Iris.Infrastructure/Persistence/Migrations` per SQLite,
`src/Iris.Migrations.Postgres/Migrations` per Postgres).
