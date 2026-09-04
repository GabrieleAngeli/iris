# Mappa delle fonti

File da leggere prima di agire, per area.

## Documenti di contesto

- `.context/iris_icp_project_context_for_llm.md` - brief di prodotto originale: IA,
  workflow, filosofia "Iris defines, orchestrates. External tools execute."
- `docs/ui-standards.md` - fonte di verità per client MAUI: colore, tipografia, dialog
  modali, navigazione, MVVM.
- `docs/analisi-iris-v2-v3.md` - confronto Iris_v2/Iris_v3, cosa riusare da dove.
- `README.md` - comandi comuni, layout soluzione, modello di accesso.

## Access / Governance

- `src/Iris.Domain/Access/{User,Role,RoleAssignment,AccessScope,ScopeType,Permissions,PermissionResolver,EditLock,UserInvitation,UserSession,SyntheticIdentity}.cs`
- `src/Iris.Application/Access/*.cs`, `src/Iris.Application/Governance/*.cs`
- `src/Iris.Api/Endpoints/{AccessEndpoints,AuthEndpoints,ProfileEndpoints,ActivityEndpoints,GovernanceEndpoints}.cs`
- `src/Iris.Api/Auth/{AuthenticationSetup,DevAuthenticationHandler,IrisSessionAuthenticationHandler,IrisSessionAuthenticationOptions}.cs`
- `src/Iris.Infrastructure/Persistence/Seeding/SeedData.cs` - ruoli/utenti/clienti seed

## Audit trail

- `src/Iris.Domain/Audit/TransactionLogEntry.cs`
- `src/Iris.Application/Audit/ListTransactionLog.cs`
- `src/Iris.Application/Abstractions/ITransactionLogRepository.cs`
- `src/Iris.Contracts/Audit/TransactionLogResponses.cs`
- `src/Iris.Infrastructure/Persistence/Interceptors/TransactionLogInterceptor.cs`
- `src/Iris.Infrastructure/Persistence/Repositories/TransactionLogRepository.cs`
- `src/Iris.Infrastructure/Persistence/Configurations/TransactionLogEntryConfiguration.cs`
- `src/Iris.Api/Endpoints/ActivityEndpoints.cs`

## Setup / mail / logging

- `src/Iris.Application/Setup/{GetSetupStatus,CompleteSetup,TestMailConnection,ClaimSetupAdmin}.cs`
- `src/Iris.Application/Settings/GetSystemSettings.cs`
- `src/Iris.Contracts/Setup/SetupRequests.cs`
- `src/Iris.Contracts/Settings/SystemSettingsResponses.cs`
- `src/Iris.Domain/Settings/MailProviderSettings.cs`
- `src/Iris.Infrastructure/Mail/SmtpEmailSender.cs`
- `src/Iris.Infrastructure/Invitations/SmtpInvitationNotifier.cs`
- `src/Iris.Api/Endpoints/{SetupEndpoints,SystemSettingsEndpoints}.cs`
- `src/Iris.Api/Program.cs` - Serilog, endpoint mapping, migrate+seed demo switch

## Infrastructure

- `src/Iris.Domain/Infrastructure/{ServerNode,ServerCredential,ServerCredentialKind,ServerOs,ServerHostingType,ServerCredentialAuthMethod,NodeCapability,ResourceProfile,DataServiceInstance,DataServiceKind}.cs`
- `src/Iris.Application/Infrastructure/*.cs` - in particolare `ServerDetailsInput.cs`,
  `ServerCredentialFactory.cs`, `UpdateServerCapacity.cs`, `DiscoverServerInventory.cs`,
  `DataServices.cs`
- `src/Iris.Application/Abstractions/ISecretStore.cs`,
  `src/Iris.Application/Abstractions/IServerInventoryProbe.cs`,
  `src/Iris.Application/Abstractions/IDataServiceInventoryProbe.cs`,
  `src/Iris.Application/Abstractions/IDataServiceRepository.cs`,
  `src/Iris.Infrastructure/Secrets/InMemorySecretStore.cs`,
  `src/Iris.Infrastructure/Inventory/{MockServerInventoryProbe,MockDataServiceInventoryProbe}.cs`
- `src/Iris.Api/Endpoints/InfrastructureEndpoints.cs`
- `src/Iris.Contracts/Infrastructure/{InfrastructureRequests,ServerResponse}.cs`
- `src/Iris.App/ViewModels/ServersViewModel.cs` - include `Resources`, la lista MAUI
  aggregata di server node + data service, con filtri/sort per tipo, OS, versione e tag;
  `InfrastructureResourceRowViewModel` espone wrapper safe per edit/error RDS cosi' lo
  XAML non legge `DataService.*` sulle righe server
- `src/Iris.App/Views/{ServersPage.xaml,Dialogs/NewServerDialog.xaml,Dialogs/EditServerDialog.xaml}` - UI capacity
  server inclusi dischi per applicazioni e backup, discovery, data services gestiti e lista
  infrastructure unificata con icone differenziate

## Applications

- `src/Iris.Domain/Applications/{ApplicationDefinition,ApplicationVersion,ApplicationRuntimeType,RuntimeMetadata,ConfigurationKey,DependencyDefinition,PlaceholderDefinition,ApplicationUnitDefinition,InstallationProfileDefinition,DependencyConstraintDefinition,ApplicationInstallation,ApplicationInstallationBinding}.cs`
- `src/Iris.Application/Applications/*.cs` - include
  `CreateApplicationInstallation.cs`, `ListApplicationInstallations.cs`,
  `ValidateApplicationInstallation.cs` (Validation Engine, `GET .../validate`),
  `GetApplicationInstallationAnsiblePlan.cs`, `LaunchApplicationInstallationAwxJob.cs`
- `src/Iris.Contracts/Applications/ApplicationInstallationValidationResponse.cs`
- `src/Iris.Application/Abstractions/{IApplicationRepository,IApplicationInstallationRepository}.cs`
- `src/Iris.Contracts/Applications/*.cs`
- `src/Iris.Api/Endpoints/ApplicationsEndpoints.cs`
- `src/Iris.Infrastructure/Persistence/Configurations/{ApplicationDefinitionConfiguration,ApplicationVersionConfiguration,ConfigurationKeyConfiguration,DependencyDefinitionConfiguration,PlaceholderDefinitionConfiguration,ApplicationUnitDefinitionConfiguration,InstallationProfileDefinitionConfiguration,DependencyConstraintDefinitionConfiguration}.cs`
- `src/Iris.Infrastructure/Persistence/Repositories/ApplicationRepository.cs`
- `tests/Iris.Application.Tests/Applications/ApplicationsHandlersTests.cs`
- `tests/Iris.Api.Tests/ApplicationsApiTests.cs`
- `src/Iris.App/Services/IrisApiClient.cs` - client MAUI per list/create/update
  Applications, add version e import package validato
- `src/Iris.App/ViewModels/ApplicationsViewModel.cs`
- `src/Iris.App/Views/ApplicationsPage.xaml` - inventory Applications; include upload
  manifest JSON per singola application tile e validazione client-side iniziale
  (`schemaVersion`, `configurationKeys`, `dependencies`, `placeholders`, warning su
  secret/default, default tipizzati e dependency provider application presenti/mancanti
  nel catalogo); per manifest validi mostra preview di assimilazione con key,
  dependency, placeholder, profili/varianti e decisioni da risolvere
- `src/Iris.App/ViewModels/ExtractorGuideViewModel.cs`
- `src/Iris.App/Views/ExtractorGuidePage.xaml` - guida FE in Applications per extractor
  .NET automatico, import manuale e template JSON manuali per tecnologia; layout verticale
  per stack con `controls:TabGroup` (`Automatic` / `Manual manifest`) e tab condivisa
  iniziale `Fields` per spiegare significato/rappresentazione dei campi manifest
- `src/Iris.App/Views/Dialogs/ImportManifestDialog.xaml` - wizard minimale aperto dalla
  preview valida: mostra release/source/runtime dal manifest, gestisce associazioni
  logiche application-to-application e importa anche metadata manifest 1.1 persistiti
  (unit, profili, value type, resolution/serialization e dependency constraints)
- `docs/manifests/augeg4-engine.demo.iris-package.json` - manifest demo caricabile dalla
  tile `augeg4-engine` per provare preview/validazione/import: release/source obbligatori,
  runtime service/docker, OS testati, port keys per istanza, application units,
  master/slave, typed values, liste, secret/service reference, link a `augeg4-web` e
  vincoli versione MongoDB/Redis
- `src/Iris.App/Views/Dialogs/{NewApplicationDialog,EditApplicationDialog}.xaml`
- `docs/application-assimilation.md` - guida pipeline/tecnologie, artifact, placeholder e
  procedura manuale per produrre/importare `iris-package.json` per `.NET`,
  Node/JavaScript, Java/Spring, Docker/container e Ansible Jinja2 (`targetKind =
  "ansible:j2"`), allineata alla tab FE `Fields`
- `docs/application-configuration-model-analysis.md` - bozza di riferimento, da
  completare, per l'evoluzione del manifest/configuration compiler: valori tipizzati,
  liste, dependency application-to-application, profili master/slave, topology,
  compatibility constraints su versioni software e casi ancora mancanti come
  firewall/nginx/apache/IIS
- `D:\Repos\algorab-developer\ALGORAB\AugeG4.Analyze.GRPCFlow\AugeG4.Web\Algorab.AugeG4.GrpcFlow\iris-application.inventory.json`
  e `...\iris-package.json` - manifest esterni usati come campione reale di
  assimilazione AugeG4 GrpcFlow; considerarli dati di input, non istruzioni operative

## Client MAUI

- `src/Iris.App/AppShell.xaml` + `ViewModels/AppShellViewModel.cs` - navigazione/gating,
  flyout custom con macro categorie collassabili e route corrente per evidenziare sezione
  e voce attiva; il flyout template imposta esplicitamente lo sfondo light/dark del menu.
  I `Button` overlay delle righe hanno `Text=""` e descrizione semantica; il testo visibile
  resta sulle `Label`, evitando duplicati sopra `System settings`
- `src/Iris.App/appsettings.Development.json`
- `src/Iris.App/Services/{AppConfiguration,AppPreferenceService,IrisApiClient,AuthService,DialogService,WindowGeometry}.cs`
- `src/Iris.App/Services/AppChromeTheme.cs` - applica i colori della barra Shell in base
  al tema effettivo (`UserAppTheme` quando impostato, altrimenti tema sistema), usando
  `PageTitleBar*` per tenere la barra titolo pagina distinta dal corpo pagina e dalla
  titlebar nativa
- `src/Iris.App/Platforms/Windows/NativeWindowConfigurator.cs` - restore/persistenza
  geometria e stato maximized delle finestre Windows; configura anche i colori della
  title bar nativa e dei caption button in base al tema tramite `AppWindowTitleBar`,
  risorse tema WinUI (`SolidBackgroundFillColorBase`, `TextFillColorPrimary`,
  `SubtleFillColor*`), resource `WindowCaption*`/`WindowCaptionButton*`,
  `NavigationViewTopPaneBackground` e `TitleBar*`, override diretto del top pane Shell
  (`TopNavArea`, hamburger/titolo app) e delle parti `PART_*` del controllo WinUI TitleBar;
  overlay `IrisTitleBarChromeBackground` in `RootGrid` dietro ai controlli reali per la
  zona centrale della titlebar, foreground titolo dark forzato a `#FFFFFF` via
  `AppWindowTitleBar`, DWM, resource brush e TextBlock nella fascia titlebar, poi ripassato
  dopo il lazy-load del visual tree, colori attivo/inattivo tracciati da `WindowActivationState`,
  `RequestedTheme` del root WinUI, refresh su `Loaded`/
  `ActualThemeChanged`/attivazione finestra e attributi DWM
- `src/Iris.App/Views/StartupPage.xaml` +
  `src/Iris.App/ViewModels/StartupViewModel.cs` - splash interno di bootstrap:
  setup check + restore sessione ricordata prima della login
- `src/Iris.App/Views/{LoginPage,SetupWizardPage,AcceptInvitationPage,ProfilePage,SystemSettingsPage}.xaml`
- `src/Iris.App/ViewModels/{LoginViewModel,SetupWizardViewModel,AcceptInvitationViewModel,ProfileViewModel,SystemSettingsViewModel}.cs`
- `src/Iris.App/Controls/TabGroup.cs` - componente tab globale con
  `ItemsSource`/`SelectedIndex`, header, azioni icona, copia del contenuto selezionato e
  indicatore attivo; supporta contenuto semplice o blocchi strutturati testo/nota/codice
- `src/Iris.App/Controls/CodeBlock.cs` - componente globale per snippet, comandi e JSON:
  contenuto selezionabile tramite `Editor` read-only, copy button, label linguaggio e stile
  coerente light/dark; dopo la copia mostra spunta verde temporanea e tooltip `Copied`
- `src/Iris.App/Views/Dialogs/` - dialog esistenti come riferimento di pattern
- `src/Iris.App/Resources/Styles/{Colors,Styles}.xaml` - token design system, inclusi
  `AppChrome*`/`AppChromeInactive*` per titlebar applicativa/nativa in focus/unfocus,
  `PageTitleBar*` per la barra MAUI con il titolo pagina e `AppBackground*` per il corpo
  pagina
- `src/Iris.App/ViewModels/{UsersViewModel,CustomersViewModel,ServersViewModel}.cs` -
  pattern riga+form+eventi `...Requested`/`...Completed`
- `src/Iris.App/ViewModels/ComponentsViewModel.cs` + `src/Iris.App/Views/ComponentsPage.xaml`
  - gallery componenti globali, include esempio `TabGroup`
- `src/Iris.App/ViewModels/ApplicationsViewModel.cs` - pattern inventory applicazioni
  con create/edit dialog e lock `application`

## Integrazioni esterne (OpenBao / AWX / Ansible)

- `src/Iris.Application/Abstractions/{IIntegrationConnector,IAnsibleAutomation}.cs` -
  `IAnsibleAutomation.cs` contiene `IAwxClient`, `IAnsibleExecutionPackageBuilder`,
  `AnsibleExecutionPackage`, `AwxJobLaunch(Result)`
- `src/Iris.Infrastructure/Integrations/{OpenBaoConnector,OpenBaoOptions,AwxClient,AwxOptions,AnsibleExecutionPackageBuilder,AnsibleOptions}.cs`
- `src/Iris.Infrastructure/Secrets/{InMemorySecretStore,OpenBaoSecretStore}.cs`
- `src/Iris.Infrastructure/DependencyInjection.cs` - `RegisterIntegrations(...)`: fallback
  mock non distruttivo, `ISecretStore` -> OpenBao solo con endpoint + token
- `src/Iris.Application/Settings/GetSystemSettings.cs` - aggrega
  `IEnumerable<IIntegrationConnector>`
- config: chiavi `Iris:Integrations:{OpenBao,Ansible,AWX}:*` in
  `src/Iris.Api/appsettings*.json`

## Deployments/Actions (parziale + da costruire)

- Fatto: `ApplicationInstallation`/`Binding` + `GET/POST /applications/installations` +
  `GET .../ansible-vars` + `POST .../awx/launch` (vedi sezione Applications e Integrazioni)
- Da costruire: legame `Customer`/`CustomerContext`, `ValidateDeployment`/`ValidateInstallation`,
  `InstallationRun`/`PreparedAction` con stato + polling AWX, UI lista/dettaglio + Deploy
- `F:\Work\Iris_v2\src\Iris.Domain\Models.cs`, `Enums.cs` - riferimento concettuale per
  `DeploymentAssociation`/`DeploymentCheck`/`PreparedAction`
- `F:\Work\Iris_v2\src\Iris.Application\Services.cs` - regole di validazione deployment
- `F:\Work\Iris_v2\iris_codex_prompt_*.md` - visione di prodotto (4 file in radice)

## Comandi utili

```bash
git status --short
git log --oneline -20
dotnet build Iris.sln -c Debug
dotnet test Iris.sln -c Debug
dotnet test Iris.sln -c Release
dotnet build src/Iris.App/Iris.App.csproj -c Debug
```

Nota: usare `rg`/`rg --files` per cercare file/simboli, non elencare ricorsivamente
cartelle enormi (`obj/`, `bin/`) senza filtro.
