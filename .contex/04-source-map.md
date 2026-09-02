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

- `src/Iris.Domain/Infrastructure/{ServerNode,ServerCredential,ServerCredentialKind,ServerOs,ServerHostingType,ServerCredentialAuthMethod,NodeCapability,ResourceProfile}.cs`
- `src/Iris.Application/Infrastructure/*.cs` - in particolare `ServerDetailsInput.cs`,
  `ServerCredentialFactory.cs`, `UpdateServerCapacity.cs`
- `src/Iris.Application/Abstractions/ISecretStore.cs`,
  `src/Iris.Infrastructure/Secrets/InMemorySecretStore.cs`
- `src/Iris.Api/Endpoints/InfrastructureEndpoints.cs`

## Applications

- `src/Iris.Domain/Applications/{ApplicationDefinition,ApplicationVersion,ApplicationRuntimeType,RuntimeMetadata,ConfigurationKey,DependencyDefinition,PlaceholderDefinition}.cs`
- `src/Iris.Application/Applications/*.cs`
- `src/Iris.Application/Abstractions/IApplicationRepository.cs`
- `src/Iris.Contracts/Applications/*.cs`
- `src/Iris.Api/Endpoints/ApplicationsEndpoints.cs`
- `src/Iris.Infrastructure/Persistence/Configurations/{ApplicationDefinitionConfiguration,ApplicationVersionConfiguration,ConfigurationKeyConfiguration,DependencyDefinitionConfiguration,PlaceholderDefinitionConfiguration}.cs`
- `src/Iris.Infrastructure/Persistence/Repositories/ApplicationRepository.cs`
- `tests/Iris.Application.Tests/Applications/ApplicationsHandlersTests.cs`
- `tests/Iris.Api.Tests/ApplicationsApiTests.cs`

## Client MAUI

- `src/Iris.App/AppShell.xaml` + `ViewModels/AppShellViewModel.cs` - navigazione/gating
- `src/Iris.App/appsettings.Development.json`
- `src/Iris.App/Services/{AppConfiguration,AppPreferenceService,IrisApiClient,AuthService,DialogService,WindowGeometry}.cs`
- `src/Iris.App/Platforms/Windows/NativeWindowConfigurator.cs` - restore/persistenza
  geometria e stato maximized delle finestre Windows
- `src/Iris.App/Views/{LoginPage,SetupWizardPage,AcceptInvitationPage,ProfilePage,SystemSettingsPage}.xaml`
- `src/Iris.App/ViewModels/{LoginViewModel,SetupWizardViewModel,AcceptInvitationViewModel,ProfileViewModel,SystemSettingsViewModel}.cs`
- `src/Iris.App/Views/Dialogs/` - dialog esistenti come riferimento di pattern
- `src/Iris.App/Resources/Styles/{Colors,Styles}.xaml` - token design system
- `src/Iris.App/ViewModels/{UsersViewModel,CustomersViewModel,ServersViewModel}.cs` -
  pattern riga+form+eventi `...Requested`/`...Completed`

## Deployments/Actions (da costruire)

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
