# Mappa delle fonti

File da leggere prima di agire, per area.

## Documenti di contesto

- `.context/iris_icp_project_context_for_llm.md` — brief di prodotto originale (IA,
  workflow, filosofia "Iris defines, orchestrates. External tools execute.").
- `docs/ui-standards.md` — fonte di verità per client MAUI (colore, tipografia, dialog
  modali, navigazione, MVVM).
- `docs/analisi-iris-v2-v3.md` — confronto Iris_v2/Iris_v3, cosa riusare da dove.
- `README.md` — comandi comuni, layout soluzione, modello di accesso.

## Access / Governance

- `src/Iris.Domain/Access/{User,Role,RoleAssignment,AccessScope,ScopeType,Permissions,PermissionResolver,EditLock,UserInvitation}.cs`
- `src/Iris.Application/Access/*.cs`, `src/Iris.Application/Governance/*.cs`
- `src/Iris.Api/Endpoints/{AccessEndpoints,AuthEndpoints,GovernanceEndpoints}.cs`
- `src/Iris.Infrastructure/Persistence/Seeding/SeedData.cs` — ruoli/utenti/clienti seed

## Infrastructure

- `src/Iris.Domain/Infrastructure/{ServerNode,ServerCredential,ServerCredentialKind,ServerOs,ServerHostingType,ServerCredentialAuthMethod}.cs`
- `src/Iris.Application/Infrastructure/*.cs` (in particolare `ServerCredentialFactory.cs`,
  `ServerDetailsInput.cs`)
- `src/Iris.Application/Abstractions/ISecretStore.cs`,
  `src/Iris.Infrastructure/Secrets/InMemorySecretStore.cs`
- `src/Iris.Api/Endpoints/InfrastructureEndpoints.cs`

## Client MAUI

- `src/Iris.App/AppShell.xaml` + `ViewModels/AppShellViewModel.cs` — navigazione/gating
- `src/Iris.App/Services/DialogService.cs`,
  `Platforms/Windows/NativeWindowConfigurator.cs` — meccanica finestre modali
- `src/Iris.App/Views/Dialogs/` — ogni dialog esistente come riferimento di pattern
- `src/Iris.App/Resources/Styles/{Colors,Styles}.xaml` — token design system
- `src/Iris.App/ViewModels/{UsersViewModel,ServersViewModel}.cs` — pattern riga+form
  inline+eventi `…Requested`/`…Completed`

## Applications/Deployments/Actions (da costruire — nessun file esiste ancora)

- `F:\Work\Iris_v2\src\Iris.Domain\Models.cs`, `Enums.cs` — forma del dominio
- `F:\Work\Iris_v2\src\Iris.Application\Services.cs` — regole di validazione deployment
- `F:\Work\Iris_v2\iris_codex_prompt_*.md` — visione di prodotto (4 file in radice)

## Comandi utili

```bash
git status --short
git log --oneline -20
dotnet build Iris.sln -c Debug
dotnet test Iris.sln -c Debug
```

Nota: usare `Grep`/`Glob` per cercare file/simboli, non elencare ricorsivamente cartelle
enormi (`obj/`, `bin/`) senza filtro.
