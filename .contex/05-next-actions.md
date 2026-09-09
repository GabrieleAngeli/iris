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
8. ~~**Deployments - associazione**~~ *(FK Customer/Context fatta, topologia server fatta,
   UI dedicata fatta)*: `ApplicationInstallation` + `ApplicationInstallationBinding` con
   **`CustomerContextId` reale** (FK a `Customer`/`CustomerContext`), endpoint
   `GET/POST /applications/installations`; nuovo `EnvironmentServerAssignment`
   (`Iris.Domain.Deployments`, endpoint `/deployments/server-assignments*`) risponde a
   "quali server usa questo environment" prima ancora di installarci qualcosa - fatto
   2026-09-04 su richiesta esplicita: composizione top-down Customer -> Context -> Server
   (assegnato) -> applicativi+modalita'. Sezione MAUI **Deployments** dentro **Governance**
   (`DeploymentsPage`/`DeploymentsViewModel`) organizzata a tre livelli, con assegna/rimuovi
   server per ambiente e "+ Application" per server (riusa il wizard `ApplicationsViewModel`/
   `NewApplicationInstallationDialog` esistente, pre-selezionando server+context dopo il
   caricamento delle opzioni). **Da verificare avviando l'app Windows** prima di considerare
   la sezione chiusa. Manca ancora: FK come navigation EF (oggi `Guid` semplice, coerente
   con le altre FK del modulo), update dei binding dopo la creazione di una installazione,
   stato di ciclo di vita, check che `ServerNode.Environment` sia coerente col
   `CustomerContext.Kind` scelto quando si assegna un server. Vedi anche il piano Ansible
   (`GET .../ansible-vars`) gia' implementato che consuma questi binding.
9. ~~**Validation Engine**~~ *(v1 fatto, UI MAUI fatta ma non verificata a mano)*:
   `ValidateApplicationInstallationHandler` + `GET /applications/installations/{id}/validate`
   (perm `deployments.validate`), report mostrato in `InstallationOpsDialog` (ora aperto da
   `DeploymentsPage`). Regole coperte: placeholder/configuration key non risolti, dependency
   non legata / provider mancante, OS non testato, capability `ServiceHost` assente,
   collisione porte, capacità CPU/RAM insufficiente, vincoli servizio/versione sul data
   service legato. Da fare più avanti: capability derivata dal runtime (non sempre
   `ServiceHost`), check disco, parser di versioni più completo, check
   `CustomerContext.Kind` vs `ServerNode.Environment` (vedi punto 8).
10. **Actions - preparazione / run history** *(run history v1 fatto, UI MAUI fatta ma non
   verificata a mano)*: `InstallationRun` + `GET /applications/installations/{id}/runs` +
   `GET .../runs/{runId}` (polling AWX on-read). Il launch AWX persiste sempre una riga
   (Pending -> Submitted/Failed). `DeploymentsPage` mostra ora la lista installazioni per
   customer/context + `InstallationOpsDialog` (Validate/Deploy/Run history) — **da
   verificare avviando l'app Windows** prima di considerarla chiusa. Resta da fare:
   `PreparedAction` (draft di preparazione prima del launch), polling di background, log
   completo della run, test dedicati per gli adapter/connettori e per
   `AnsibleExecutionPackageBuilder`.
11. **Applications version detail/import UI**: esporre aggiunta versione, dettaglio
   configuration knowledge e import manuale/da package sopra l'inventory gia' presente.
12. Non pianificato in dettaglio: Monitoring/Audit reale, Grafana/capacity advisory, COM
   Matrix, generazione runtime config materializzata su disco.
13. **Integration settings persistite (OpenBao/AWX/Ansible)** - piano completo in
   `C:\Users\gabriele.angeli\.claude\plans\mighty-knitting-avalanche.md`.
   - ~~Fase 1: persistenza config + wizard a 4 step~~ Fatto: vedi `00-current-state.md`.
   - ~~Fase 2: OpenBao self-provisioning via Docker~~ Fatto e **verificato end-to-end con
     Docker Desktop realmente attivo il 2026-09-08** (non solo unit test): `IContainerRuntime`
     + `DockerCliContainerRuntime` (shell-out a `docker` via `IProcessRunner`, prima capacità
     del repo di eseguire processi di sistema), `POST /system/integrations/openbao/provision`
     (`platform.admin`), modalità dev-only esplicita (non per produzione). Root token estratto
     correttamente da un banner reale (`s.oi9YVUH2BqOJdjRl6BQKpOme`), ora fissato come test di
     regressione. Un bug reale di `RestartRequired` (sempre `true` per i default dev di
     AWX/Ansible mai persistiti) trovato e corretto proprio grazie a questa verifica manuale -
     vedi `00-current-state.md` per i dettagli. 10 test in `Iris.Infrastructure.Tests` (nuovo
     progetto) + 11 in Application + 4 in Api.
   - ~~Collegamento UI MAUI al provisioning + 3 bug reali da test manuale utente~~ Fatto
     2026-09-08: bottone `Provision`/`Configure` in `SystemSettingsPage`, hand-off wizard
     post-login; corretti "salvataggio invisibile" (`Pending restart` in
     `GetSystemSettingsHandler`), "warning di provisioning scartato silenziosamente" (nuovo
     `SetupCompleted`/`ContinueToDashboardCommand` nel wizard) e "nessun modo per
     configurare/testare OpenBao più tardi" (`ConfigureOpenBaoDialog`). **Da verificare a
     mano nell'app Windows in esecuzione** — solo build+test automatici finora, vedi la
     sessione datata sotto.
   - Fase 3: AWX self-provisioning via playbook Ansible bundlato, eseguito direttamente
     (`ansible-playbook`, non più solo l'API REST di AWX) - asincrono/pollable, richiede
     Ansible+Docker già presenti sull'host (Iris rileva, non installa i prerequisiti).
   - Fase 4: `ApplicationInstallation` oggi è create-only - aggiungere `Update(...)` +
     `ApplicationInstallationRevision` (history append-only) + `PUT .../installations/{id}` +
     `GET .../installations/{id}/history` + tab "History" in `InstallationOpsDialog`.

## Stato recente delle sessioni

### 2026-09-09 - Generazione scaffold Ansible (template .j2 + skeleton ruolo/playbook) dal manifest

Richiesta esplicita dell'utente: "tutti i template, role e playbook di awx/ansible dono da
tenere in riferimento, ma dovranno essere generati da iris, cosa ne pensi?" - opinione data
esplicitamente prima di implementare (tensione con la decisione già presa in
`docs/application-configuration-model-analysis.md` che Iris non deve generare i file finali),
poi scelta via `AskUserQuestion` la via di mezzo: template `.j2` + scaffold iniziale di
ruolo/playbook, non generazione continua/playbook logici (pacchetti OS, firewall, retry
restano fuori scope, a giudizio umano). Pianificato in Plan Mode con due agenti Explore in
parallelo (modello dati Application/ConfigurationKey/manifest; integrazione AWX/Ansible + UI
MAUI) prima di scrivere codice.

- Nuovo endpoint `GET /applications/{applicationId}/versions/{versionId}/ansible-scaffold`
  (perm `applications.read`) -> `AnsibleScaffoldGenerator` (puro). Dettagli completi in
  `00-current-state.md` (naming condiviso via `AnsibleNaming`, classificazione target
  file/fragment/code-only, format-aware .properties/.env/.json/reference-only).
- MAUI: bottone "Generate Ansible scaffold" + Picker versione su `ApplicationsPage`,
  `AnsibleScaffoldDialog` (lista file + `controls:CodeBlock`, riuso del copy-to-clipboard
  già esistente - **nessuna nuova infrastruttura di download/save-as**, scelta deliberata
  per l'MVP).
- **Bug reale trovato e corretto**: `GenerateApplicationAnsibleScaffoldHandler` non
  registrato in `Iris.Application/DependencyInjection.cs` faceva fallire l'inferenza
  Minimal API del parametro (letto come body implicito) e questo rompeva **l'intera
  route table del gruppo Applications**, non solo il nuovo endpoint - da qui 500 su test
  scollegati come `POST /applications`. Diagnosticato leggendo il body reale della
  risposta (Development espone il dettaglio dell'eccezione), non fidandosi del solo
  status code.
- Verifica: `dotnet test Iris.sln` **346/346 verdi** (15 nuovi test: 10 sul generatore
  puro + 3 sull'handler applicativo + 2 end-to-end API). `dotnet build src/Iris.App` verde
  (0 warning). **Da verificare a mano nell'app Windows in esecuzione.**

### 2026-09-08 (quarto giro) - Servizio di health-check periodico per le integrazioni

Richiesta esplicita: "dovrebbe esserci un servizio che controlla i servizi connessi se sono
raggiungibili e configurati correttamente [...] e mostrare nella sessione system se sono ok,
tentare l'avvio, altrimenti dare un wizard di configurazione".

- Primo servizio in background/schedulato del repo: `IntegrationHealthCheckBackgroundService`
  (`Iris.Api`) chiama periodicamente (default 5 min, configurabile) il nuovo
  `IntegrationHealthChecker` (`Iris.Infrastructure`), che probe realmente openbao/awx/ansible
  e registra l'esito in `IIntegrationHealthMonitor` (cache in RAM). `GetSystemSettingsHandler`
  sovrappone quell'esito reale allo stato "Configured" statico (con priorità a "Pending
  restart" quando c'è un salvataggio in attesa). `SystemSettingsPage` mostra "Checked Xm ago".
  Dettagli completi in `00-current-state.md`.
- **Nota per la prossima sessione**: "tentare l'avvio" NON è stato implementato come
  auto-restart/provisioning automatico in background - resta un'azione esplicita (bottone
  Provision già esistente), per coerenza col resto della sessione. Da confermare con l'utente
  se questa interpretazione va bene o se serve un vero tentativo automatico.
- Verifica: `dotnet test Iris.sln` **319/319 verdi** (9 nuovi test). `dotnet build
  src/Iris.App` verde. **Da verificare a mano nell'app Windows in esecuzione**, inclusa
  l'osservazione del ciclo periodico reale (non solo il click manuale "Test").

### 2026-09-08 (terzo giro) - Vault cifrato per i segreti pre-bootstrap

Richiesta esplicita dell'utente: il token OpenBao (e più in generale ogni segreto salvato
prima che OpenBao sia configurato) deve essere persistito su DB **cifrato con la password
dell'utente che lo imposta**, con sblocco che richiede sempre un re-inserimento esplicito
della password (mai automatico). Pianificato in Plan Mode (due domande di chiarimento poste
e confermate: modello di sblocco "richiede password" vs auto-decrypt vs chiave applicativa;
ambito "solo OpenBao" vs "tutti i segreti pre-bootstrap") + un agente Plan per validare lo
schema crittografico prima di implementare.

- `InMemorySecretStore` sostituito da `EncryptedFallbackSecretStore` (stesso comportamento
  `ISecretStore`, zero rotture per i chiamanti esistenti) + nuovo `FallbackSecretVault`
  (`POST /system/settings/secrets/unlock`, platform.admin) che persiste/ripristina i segreti
  cifrati (AES-256-GCM + PBKDF2, AAD=reference) su una nuova tabella `EncryptedSecretEntries`.
  Dettagli completi in `00-current-state.md`.
- **Bug reale trovato e corretto durante la verifica end-to-end** (non ipotetico): la
  risoluzione dell'utente corrente via `ICurrentUser.UserId` (claim `iris:uid`) non era
  affidabile per richieste dev-header+password - corretto usando
  `IUserProvisioningService.EnsureProvisionedAsync` (stesso schema di `SetMyPasswordHandler`)
  sia in `GetSystemSettingsHandler` che nel nuovo `UnlockFallbackSecretsHandler`.
- Verifica: `dotnet test Iris.sln` **310/310 verdi** (43 nuovi test su tutti i livelli:
  dominio, crypto, store/vault via SQLite reale, handler applicativo, endpoint API
  end-to-end). `dotnet build src/Iris.App` verde (dialog "Unlock secrets" + banner in
  System settings). **Da verificare a mano nell'app Windows in esecuzione.**

### 2026-09-08 (secondo giro) - 6 problemi ulteriori da un secondo test manuale

Stesso giorno del giro precedente: l'utente ha ritestato dopo i primi tre fix e trovato
altri sei problemi concreti, tutti diagnosticati e corretti nella stessa sessione. Riassunto
completo in `00-current-state.md`; punti chiave:

- OpenBao "install for me" falliva ancora su un container fermo lasciato da test precedenti
  → `IContainerRuntime.StartAsync` (docker start) riavvia invece di fallire;
  `ParseRootToken` ora prende l'ultima occorrenza del marker, non la prima (i log dopo un
  riavvio contengono anche il banner vecchio con un token non più valido).
- SMTP non era mai editabile dopo il wizard → `PUT /system/settings/mail` +
  `POST /system/settings/mail/test` (nessun riavvio richiesto, a differenza delle altre tre
  integrazioni) + dialog MAUI `ConfigureMailDialog`.
- Ansible mostrava sempre "Configured" (calcolato su un campo con default sempre non vuoto)
  e il suo Test ignorava completamente `probe` → ora "Configured" riflette l'endpoint, e
  `probe:true` esegue realmente `ansible-playbook --version` via `IProcessRunner`.
- AWX non aveva un dialog di configurazione → aggiunto `ConfigureAwxDialog`.
- Nexus/Azure DevOps non hanno mai avuto un connettore reale dietro: Test nascosto per
  queste due invece di mostrare un 404 travestito da "Unreachable".
- Dashboard (finora 100% dati mock) mostra ora un banner reale se qualcosa in System
  settings richiede attenzione, sourced da `GET /system/settings`.

Verifica: `dotnet build`/`dotnet test Iris.sln` verdi, **278/278** (17 nuovi test).
**Da ri-testare a mano nell'app Windows in esecuzione** prima di chiudere definitivamente
l'intera area integrazioni.

### 2026-09-08 - Fix da test manuale utente: 3 problemi reali su OpenBao

L'utente ha testato manualmente le tre feature aggiunte nella sessione precedente
(connessione a OpenBao esistente, install-for-me post-login, e la mancanza di un modo per
riconfigurare) e riportato tre problemi concreti verbatim. Tutti e tre diagnosticati e
corretti:

- **"non salva token o non sembra comunicare col servizio"**: non un bug di persistenza —
  `GetSystemSettingsHandler` mostrava sempre lo stato del connettore *attivo* (fissato
  all'avvio), mai il valore appena salvato. Aggiunto `OverrideIfPendingRestart`: quando c'è
  un salvataggio pendente per openbao/awx/ansible, la riga integrazione mostra
  `Status = "Pending restart"` + l'endpoint appena persistito + un messaggio esplicito.
- **"installa per me dopo la login non fa partire il container"**: bug reale.
  `SetupWizardViewModel.CompleteAsync` invocava `Completed` (naviga subito alla dashboard)
  incondizionatamente, scartando silenziosamente l'esito del provisioning prima che
  l'utente potesse leggerlo — successo o fallimento, la UI non mostrava mai nulla. Aggiunta
  `SetupCompleted` + `ContinueToDashboardCommand`: naviga subito solo se non c'è un
  warning, altrimenti resta sullo step 4 mostrando l'errore con un bottone esplicito per
  proseguire.
- **"configura dopo non c'è un modo per configurarlo/testarlo"**: gap reale confermato —
  nessun client/UI chiamava mai `PUT /system/integrations/openbao` fuori dal wizard
  one-shot. Aggiunto `ConfigureOpenBaoDialog`/`ConfigureOpenBaoDialogViewModel` (stesso
  pattern `CloseRequested`+`WasSaved` di `SelectApplicationForDeploymentDialog`) + bottone
  `Configure` sulla riga OpenBao.
- Verifica: `dotnet build` di `Iris.App`/`Iris.Application` verdi, `dotnet test Iris.sln`
  261/261 verdi (2 nuovi test di regressione per il primo punto). **Non ancora verificato a
  mano nell'app Windows in esecuzione** — prossimo passo prima di chiudere i tre problemi
  per davvero.

### 2026-09-07 - Probe connettori da System settings

- Aggiunto endpoint `GET /system/integrations/{key}/status?probe=true` protetto da
  `platform.admin`: usa `IIntegrationConnector.GetStatusAsync(probe)` e risponde con lo
  stesso `IntegrationLinkResponse` gia' usato da `/system/settings`.
- Client MAUI: `IIrisApiClient.GetIntegrationStatusAsync` + righe
  `IntegrationConnectionRow` in `SystemSettingsViewModel`; nella card Service connections
  ogni connettore ha il pulsante `Test`, aggiorna stato/messaggio della singola riga e
  segnala errori come `Unreachable`.
- Verifica: `dotnet build src\Iris.Api\Iris.Api.csproj -p:UseAppHost=false --no-restore`,
  `dotnet build src\Iris.App\Iris.App.csproj -p:UseAppHost=false --no-restore` e
  `dotnet test Iris.sln --no-restore -p:UseAppHost=false` verdi (206/206).

### 2026-09-04 - CustomerContext FK reale + sezione Deployments (rework su feedback utente)

- Feedback utente dopo la sessione precedente: "non ha senso che l'installation sia sotto
  le application, deve esserci una sezione che mi permetta di comporre, istanza, per
  cliente, con applicativi su server". Confermato che il gap era gia' segnalato in
  `01-decisions.md`.
- Backend: `ApplicationInstallation.CustomerContextId` (Guid, FK reale) sostituisce
  `Environment` (`ContextKind` libero). Migrazione `AddApplicationInstallationCustomerContext`
  (SQLite+Postgres, drop/add colonna). `ApplicationInstallationMapping.ResolveCustomerContextAsync`
  (extension su `ICustomerRepository`, correla in memoria - nessun lookup by-context diretto
  nel repository). Aggiornati `CreateApplicationInstallation`/`ListApplicationInstallations`/
  `GetApplicationInstallationAnsiblePlan`/`ValidateApplicationInstallation` handler +
  contratti (`CreateApplicationInstallationRequest.CustomerContextId`,
  `ApplicationInstallationResponse` con `CustomerId`/`CustomerName`/`CustomerContextId`/
  `CustomerContextName`/`Environment` derivato).
- MAUI: nuova sezione flyout standalone **Deployments** (route `//deployments`, gate
  `deployments.read`) con `DeploymentsPage`/`DeploymentsViewModel`: Customer -> Context ->
  installazioni. `ApplicationInstallationRowViewModel` decoupled da `ApplicationRowViewModel`
  (prende `canManageDeployments`/`openOps` come parametri). "New deployment" riusa
  `ApplicationsViewModel` (iniettata) per il picker applicazione e l'intero wizard
  `NewApplicationInstallationDialog` esistente, a cui e' stato aggiunto il campo
  obbligatorio `Customer & environment`. Rimossi da `ApplicationsPage` il bottone "New
  installation" e la sezione "Installations" aggiunti nell'iterazione precedente.
- Test: 8 call site in `ApplicationsHandlersTests` + 1 in `ApplicationsApiTests` aggiornati
  per seedare/creare un `Customer`+`Context` reale invece di passare `"Production"`.
  `dotnet test Iris.sln` 192/192 verde (invariato), build MAUI verde.
- **Non ancora verificata manualmente nell'app Windows in esecuzione**. Rischio noto
  segnalato in `00-current-state.md`: `NewApplicationInstallationDialog` chiude se stessa
  con `Navigation.PopModalAsync()` (codice preesistente) invece del pattern
  `CloseWindow` degli altri dialog — da controllare durante la verifica manuale.

### 2026-09-04 - UI MAUI installazioni: lista, Validate, Deploy, Run history

- `ApplicationsPage`: sotto ogni application tile, sezione `Installations` (visibile se
  `HasInstallations`) con badge ambiente/inattivo, `DetailText` e bottone `Manage`.
- Nuovo `Views/Dialogs/InstallationOpsDialog` (`dlg.installation-ops`, 720x680): console
  read-mostly non a edit-lock con tre sezioni - Validation (`ValidateCommand`, report con
  badge severità), Deploy (`DeployCommand` -> `awx/launch`, poi ricarica lo storico), Run
  history (`LoadRunsCommand`, badge stato). Auto-eseguiti all'apertura: validate + load
  runs.
- Nuove VM: `ApplicationInstallationRowViewModel` (su `ApplicationRowViewModel.Installations`,
  popolata in `ApplicationsViewModel.RefreshAsync` da `GetApplicationInstallationsAsync`
  raggruppata per app; anche l'installazione appena creata viene inserita in testa),
  `ValidationCheckRowViewModel`, `InstallationRunRowViewModel`.
- **Verificato solo con build**: `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false`
  verde - 0 warning/0 errori. Nessuna verifica manuale end-to-end nell'app Windows in questa
  sessione - da fare prima di considerare il flusso chiuso (evidence gate
  `03-iteration-guardrails.md`).

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
