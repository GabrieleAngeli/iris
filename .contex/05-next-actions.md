# Prossime azioni

Ordinate per priorità. Aggiornare questa lista a ogni chiusura di iterazione significativa.

## Priorità correnti

1. ~~**Applications - catalogo + import configuration knowledge**~~ Fatto.
2. ~~**ServerNode - capability, resource hints, porte note**~~ Fatto.
3. ~~**Auth produzione + first-run setup + SMTP + SSO admin claim**~~ Fatto.
4. ~~**Profile / System settings / password recovery / remember me**~~ Fatto.
5. ~~**Audit trail / activity per area**~~ Fatto.
6. ~~**Applications inventory client**~~ Fatto: pagina MAUI `Applications`, create/edit
   inventory, slug immutabile, lock advisory `application`.
7. ~~**Infrastructure discovery / RDS / artifact assimilation prep**~~ Fatto: discovery
   server via port `IServerInventoryProbe`, inventory `/data-services` per MSSQL,
   PostgreSQL e Redis, artifact metadata su Applications, guida
   `docs/application-assimilation.md`.
8. **Deployments - associazione** *(parziale - fatto il primo strato)*: esiste
   `ApplicationInstallation` + `ApplicationInstallationBinding` con endpoint
   `GET/POST /applications/installations` e dialog MAUI di creazione. Manca: legame con
   `Customer`/`CustomerContext`, FK come navigation EF (oggi `Guid` semplici), update dei
   binding dopo la creazione, stato di ciclo di vita, UI di lista/dettaglio. Vedi anche il
   piano Ansible (`GET .../ansible-vars`) gia' implementato che consuma questi binding.
9. ~~**Validation Engine**~~ *(v1 fatto)*: `ValidateApplicationInstallationHandler` +
   `GET /applications/installations/{id}/validate` (perm `deployments.validate`).
   Regole coperte: placeholder/configuration key non risolti, dependency non legata /
   provider mancante, OS non testato, capability `ServiceHost` assente, collisione porte,
   capacità CPU/RAM insufficiente, vincoli servizio/versione sul data service legato.
   Da fare più avanti: UI MAUI del report, capability derivata dal runtime (non sempre
   `ServiceHost`), check disco, parser di versioni più completo, legame Customer/Context.
10. **Actions - preparazione / run history** *(run history v1 fatto)*: `InstallationRun` +
   `GET /applications/installations/{id}/runs` + `GET .../runs/{runId}` (polling AWX
   on-read). Il launch AWX persiste sempre una riga (Pending -> Submitted/Failed). Resta da
   fare: `PreparedAction` (draft di preparazione prima del launch), polling di background,
   log completo della run, endpoint `test-connection` (`probe:true`), pulsante Deploy +
   storico nel client MAUI, test per `AnsibleExecutionPackageBuilder`.
11. **Applications version detail/import UI**: esporre aggiunta versione, dettaglio
   configuration knowledge e import manuale/da package sopra l'inventory gia' presente.
12. Non pianificato in dettaglio: Monitoring/Audit reale, Grafana/capacity advisory, COM
   Matrix, generazione runtime config materializzata su disco.

## Stato recente delle sessioni

### 2026-09-04 - Run history AWX v1

- `InstallationRun` (aggregato + enum `InstallationRunKind`/`InstallationRunStatus`),
  `IInstallationRunRepository` + `InstallationRunRepository`, EF config + migrazione
  `AddInstallationRuns` (SQLite + Postgres), area `Deployments` in
  `TransactionLogInterceptor`.
- `LaunchApplicationInstallationAwxJobHandler` ora persiste una riga per ogni tentativo:
  `Pending` -> `MarkSubmitted` (successo) o `MarkFailed` + rethrow (AWX non configurato).
  Risposta con `RunId`.
- `ListInstallationRunsHandler` (`GET .../runs`) e `GetInstallationRunHandler`
  (`GET .../runs/{runId}`, poll AWX on-read via nuovo `IAwxClient.GetJobStatusAsync` ->
  `GET /api/v2/jobs/{id}/`; se AWX non raggiungibile il read non fallisce).
- Test: `InstallationRunTests` (5, Domain), 4 handler (`Launch*`/`List*`/`Get*`),
  3 API (`ApplicationsApiTests`: 403 launch reader, 404 runs, happy-path failed-run).
  `dotnet test Iris.sln` 192/192 verde, build MAUI verde.

### 2026-09-04 - Validation Engine v1

- `ValidateApplicationInstallationHandler` (`src/Iris.Application/Applications/ValidateApplicationInstallation.cs`),
  contratti `ApplicationInstallationValidation*Response`, endpoint
  `GET /applications/installations/{id}/validate` (perm `deployments.validate`), DI.
- Solo lettura: confronta `ApplicationVersion` (placeholder/config key/dependency/
  `RuntimeMetadata`/`DependencyConstraintDefinition`) vs `ServerNode` +
  `DataServiceInstance` legati; lista tipata di check con severità error/warning/info.
- Parser interno `SatisfiesVersion` per espressioni tipo `>= 6.2 && < 8`, `== 6`,
  `6.2-8.0`; non parsabile => `info`, mai blocco.
- Test: 3 `ValidateApplicationInstallation_*` + 8 casi `SatisfiesVersion_*` in
  `ApplicationsHandlersTests`. `dotnet test Iris.sln` 180/180 verde, build MAUI verde.

### 2026-09-04 - ApplicationInstallation + connettori integrazione + fix build

- `f53eb2d`: `ApplicationInstallation`/`ApplicationInstallationBinding`, repository,
  `GET/POST /applications/installations`, dialog MAUI `NewApplicationInstallationDialog`,
  migrazione `AddApplicationInstallations` (SQLite + Postgres).
- `11802b3`: `GET /applications/installations/{id}/ansible-vars` (piano variabili `iris_*`
  + operations + templateTargets), `POST .../awx/launch`, porte `IIntegrationConnector`/
  `IAwxClient`/`IAnsibleExecutionPackageBuilder`, adapter `OpenBaoConnector`/`AwxClient`/
  `AnsibleExecutionPackageBuilder`/`OpenBaoSecretStore`, `GET /system/settings` con stato
  reale connettori + campo `Message`. Decisione: Iris produce il piano, Ansible/AWX
  renderizza e applica (mai Iris direttamente sul server).
- `11802b3` era stato committato senza compilare (CS0411 in `OpenBaoSecretStore`).
  Corretto in `39d769a`. Build `Iris.sln` verde, `dotnet test Iris.sln` 169/169 verde,
  build MAUI verde.
- Aperto: nessuna run history / polling AWX, nessun pulsante Deploy, nessun
  test-connection reale, Validation Engine ancora da scrivere. Vedi punti 8-10 sopra.

### 2026-09-01 - `.contex`, analisi Iris_v2/Iris_v3, security scanning

- Analizzate `F:\Work\Iris_v2` e `F:\Work\Iris_v3`; risultato in
  `docs/analisi-iris-v2-v3.md`.
- Creata la cartella `.contex/` adattando la convenzione da Iris_v3/Momentum.
- Aggiunto security scanning minimo: `.gitleaks.toml`, `.semgrepignore`,
  `.github/workflows/security.yml`.

### 2026-09-01 - Applications: catalogo + import

- Dominio `src/Iris.Domain/Applications/`: `ApplicationDefinition`, `ApplicationVersion`,
  `RuntimeMetadata`, `ConfigurationKey`, `DependencyDefinition`, `PlaceholderDefinition`.
- Application layer, Contracts, repository EF, endpoint `ApplicationsEndpoints`, migrazione
  `AddApplications` per SQLite e Postgres.
- Bug corretto: `ApplicationRepository.GetAllAsync` ora include anche le collezioni figlie
  delle versioni, così i conteggi in `GET /applications` sono reali.
- Suite di allora: 104/104 test verdi.

### 2026-09-01 - ServerNode: capability/risorse/porte

- Aggiunti `NodeCapability`, `ResourceProfile?`, `Capabilities`, `Resources`, `UsedPorts`
  e handler/endpoint `PUT /servers/{id}/capacity`.
- Decisione: `UsedPorts` resta lista semplice di interi, simmetrica a
  `RuntimeMetadata.RequiredPorts`.
- Migrazione `AddServerCapacity` per SQLite e Postgres.
- Suite di allora: 108/108 test verdi.

### 2026-09-02 - auth produzione, setup, mail, hardening

- `POST /auth/login` con email/password locale e `UserSession` persistita come hash.
- `POST /invitations/accept` per riscattare inviti one-time e impostare la prima password.
- First-run setup anonimo ma one-shot: `/setup/status`, `/setup/test-mail`,
  `/setup/complete`.
- MailKit SMTP con test reale della connessione; password SMTP sempre via `ISecretStore`.
- Serilog configurato; rimosso file segreto accidentale e rafforzato `.gitignore`.
- Suite corrente verificata in questa sessione: `dotnet test Iris.sln -c Release`, 135/135
  test verdi.

### 2026-09-02 - bootstrap SSO controllato

- Aggiunto `POST /setup/claim-admin`: richiede autenticazione, allow-list
  `Iris:Setup:AdminClaimEmails` e nessun platform-admin gia' assegnato.
- Il client MAUI, dopo SSO riuscito, chiama automaticamente il claim se `/setup/status`
  indica che il setup serve ancora; questo sblocca il primo accesso SSO senza seed
  automatici e senza dover configurare subito SMTP.
- Suite corrente verificata in questa sessione: `dotnet test Iris.sln -c Release`, 139/139
  test verdi.

### 2026-09-02 - profilo, impostazioni sistema, recovery, remember me

- Flyout: `Profile` e `Sign out` sotto l'identita' utente; footer spostato su
  `System settings`.
- `ProfilePage`: dati utente, cambio password, permessi effettivi, access history.
- `SystemSettingsPage`: tema locale `System`/`Light`/`Dark` per tutti; SMTP e integrazioni
  OpenBao/Ansible visibili lato API solo con `platform.admin` per la parte sensibile SMTP.
- Login: recupero password anonimo/non-enumerante e `Remember me` per sessione locale.
- Suite corrente verificata in questa sessione: `dotnet test Iris.sln`, 145/145 test
  verdi; `dotnet build Iris.App.sln` verde.

### 2026-09-02 - audit trail / activity per area

- Aggiunto `TransactionLogEntry` scritto automaticamente da EF durante `SaveChanges`.
- `GET /activity?area=...&take=...` e pannello Activity in `SystemSettingsPage` per
  superadmin.
- Suite corrente verificata in questa sessione: `dotnet test Iris.sln`, 146/146 test
  verdi; `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false` verde.

### 2026-09-02 - Applications inventory client

- Aggiunto `PUT /applications/{id}` per aggiornare nome, runtime, repository, branch,
  descrizione e stato attivo mantenendo lo slug immutabile.
- Esteso il lock advisory con resource type `application`.
- Aggiunta `ApplicationsPage` nel client MAUI sotto Workspace, con lista catalogo,
  riepilogo versioni/knowledge, dialog `NewApplication` e `EditApplication`.
- Client API esteso con `GetApplicationsAsync`, `CreateApplicationAsync` e
  `UpdateApplicationAsync`.
- Suite corrente verificata in questa sessione: `dotnet test Iris.sln`, 151/151 test
  verdi; `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false` verde.

### 2026-09-02 - startup splash e restore sessione

- Aggiunta `StartupPage` come prima ShellContent, prima della login.
- `StartupViewModel` esegue setup check con retry e poi restore del token `Remember me`.
- Se la sessione salvata e' valida naviga direttamente a dashboard/first-login; la login
  viene mostrata solo se non c'e' sessione valida o se l'API resta irraggiungibile.
- Verificato `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false` verde.

### 2026-09-02 - riordino flyout MAUI

- Dashboard resa la prima voce operativa del flyout.
- Flyout reso piu' leggibile con righe iconate, indentazione e sezioni nette:
  Workspace, Governance, Infrastructure, Applications, Development.
- Applications spostata nella sezione Applications come voce `Inventory`.
- Components spostata in Development e visibile solo nelle build DEBUG.
- Verificato `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false` verde.

### 2026-09-02 - dischi server per applicazioni e backup

- `ResourceProfile` esteso con `ApplicationDiskGb` e `BackupDiskGb`, oltre al disco
  totale gia' presente.
- `PUT /servers/{id}/capacity` accetta/restituisce le due quote e valida che non siano
  negative; se e' noto il disco totale, app+backup non puo' superarlo.
- Migrazioni `AddServerDiskReservations` generate per SQLite e Postgres.
- `ServersPage` mostra il riepilogo risorse; `EditServerDialog` consente di modificare
  CPU/RAM, disco totale, disco applicazioni, disco backup e porte note.
- Verificato `dotnet test Iris.sln` verde - 151/151; `dotnet build Iris.App.sln
  --no-restore -p:UseAppHost=false` verde.
