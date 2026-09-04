# Log di sessione

Un'annotazione per iterazione significativa: cosa è cambiato, cosa è stato verificato,
cosa resta aperto. Non un changelog di commit — quello lo dà `git log`. Questo è il "perché"
e il "cosa resta da sapere" che il codice da solo non racconta.

---

## 2026-09-01 — Assimilazione da Iris_v2/Iris_v3, avvio `.contex/`

**Classificazione**: consolidamento (documentazione operativa) + definizione (piano per
Applications/Deployments/Actions).

**Cosa è successo**: analizzate `F:\Work\Iris_v2` (bozza di dominio Applications/
Deployments/Validation, mai completata) e `F:\Work\Iris_v3` (prodotto separato,
"Momentum", nessuna relazione con Iris se non il nome della cartella). Prodotto un
documento di confronto (artifact + `docs/analisi-iris-v2-v3.md`). Su richiesta
dell'utente ("procedi con l'assimilazione proposta nel documento"), creata questa
cartella `.contex/` adattando la convenzione di Momentum con contenuti scritti da zero
per Iris.

**Verificato**: `dotnet build Iris.sln` e `dotnet test Iris.sln` verdi prima di iniziare
(92/92 test), stato riportato in `00-current-state.md` con quella baseline.

**Rischi residui / cosa resta aperto**: il tooling di security scanning (Gitleaks +
Semgrep, non l'intera pipeline Momentum — Checkov/tfsec non applicabili, nessuna IaC nel
repo) e l'avvio del dominio Applications sono ancora da fare in questa stessa sessione,
vedi `05-next-actions.md`.

**Prossimo step**: (1) config Gitleaks/Semgrep minima; (2) plan mode per il primo
incremento di dominio Applications, con estensione di `ServerNode`
(capability/risorse/porte) come prerequisito.

---

## 2026-09-01 (continuazione) — Applications: catalogo + import della configuration knowledge

**Classificazione**: feature (nuovo dominio, primo incremento verso Deployments/Actions).

**Cosa è successo**: completata l'implementazione del piano approvato (salvato in
`C:\Users\angel\.claude\plans\twinkling-seeking-papert.md`), decidendo di non estendere
`ServerNode` in questo passaggio (rimandato — vedi `05-next-actions.md` punto 1) e di
tenere backend-first senza pagina client. Fonte di riferimento:
`F:\Work\Iris_v2\iris_codex_prompt_backend_hexagonal.md` (Domain Area 2 e 3), riscritta
sulle convenzioni EF/CQRS attuali — non portato codice, solo la forma del dominio.

Costruito dal basso verso l'alto:

- **Dominio** (`src/Iris.Domain/Applications/`): `ApplicationDefinition` (aggregate root,
  `AddVersion` con guardia su duplicati case-insensitive), `ApplicationVersion` (entità
  figlia, `ApplyImport` sostituisce — non accumula — le tre collezioni + i warning, tiene
  `RawImportPackageJson` per audit), `RuntimeMetadata` (owned type, riusa
  `Iris.Domain.Infrastructure.ServerOs` invece di duplicarlo), `ConfigurationKey`/
  `DependencyDefinition`/`PlaceholderDefinition` (entità figlie con propria `Id`, a
  differenza dei record senza identità di Iris_v2 — servirà per indirizzarle
  singolarmente quando arriverà il binding placeholder di una Deployment).
  Nota di design: `RequiredPorts` e `ImportWarnings` sono collezioni scalari, non
  navigation — quindi proprietà auto-implementate normali (EF Core 9 le mappa come
  collezione primitiva nativa), non il pattern `List` privato + `.AsReadOnly()` usato per
  le vere collezioni di entità figlie.
- **Application/Contracts**: 5 handler (`CreateApplication`, `AddApplicationVersion`,
  `ImportConfigurationPackage`, `ListApplications`, `GetApplicationVersionDetail`),
  `IApplicationRepository`, mapping verso `Iris.Contracts.Applications.*`.
- **Persistenza**: 5 `IEntityTypeConfiguration`, `ApplicationRepository`, 4 nuovi `DbSet`
  su `IrisDbContext`. Migrazione `AddApplications` generata su entrambi i provider — su
  SQLite le collezioni primitive (`RequiredPorts`, `ImportWarnings`) diventano colonne
  TEXT (JSON), su Postgres array nativi (`integer[]`, `text[]`); ispezionate a mano
  entrambe, corrispondono alle attese.
- **Api**: `src/Iris.Api/Endpoints/ApplicationsEndpoints.cs`, 5 route, ciascuna con il
  permesso già presente nel catalogo (`applications.read/write/import`, mai toccato
  Governance/Access).
- **Test**: `FakeApplicationRepository` in `Fakes/FakeAccessData.cs`, 9 test applicativi
  (`ApplicationsHandlersTests`) e 3 test API end-to-end (`ApplicationsApiTests`,
  incluso un 403 per il ruolo Reader).

**Bug intercettato e corretto durante l'implementazione**: `ApplicationRepository
.GetAllAsync` caricava le versioni ma non le loro collezioni figlie; dato che
`ListApplicationsHandler`/`ToSummaryResponse` calcolano `ConfigurationKeyCount` ecc.
contando quelle collezioni, il risultato era sempre zero in `GET /applications` — non
un errore di compilazione, un bug silenzioso. Trovato dal test API end-to-end (il conteggio
atteso dopo un import non tornava), corretto aggiungendo `Include(...).ThenInclude(...)`
anche lì (non solo in `GetAsync`/`GetForUpdateAsync`), accettando il costo di caricare
sempre quelle collezioni: sono conteggi, non contenuto, quindi la lista è comunque leggera.

**Verificato**: `dotnet build Iris.sln` (0 errori/0 warning) e `dotnet test Iris.sln`
verdi — 104/104 test (92 preesistenti + 12 nuovi).

**Rischi residui / cosa resta aperto**: nessuna pagina client per questo dominio (voluto,
backend-first); `ServerNode` non ancora esteso con capability/risorse/porte, quindi il
confronto runtime-vs-server del Validation Engine non è ancora possibile — è il
prerequisito del prossimo incremento (Deployments), vedi `05-next-actions.md`.

**Prossimo step**: aggiornare `00-current-state.md`/`01-decisions.md` se necessario alla
prossima iterazione; poi plan mode per l'estensione di `ServerNode`
(capability/risorse/porte) seguita da Deployments.

---

## 2026-09-01 (terza continuazione) — ServerNode: capability, resource hints, porte note

**Classificazione**: feature (estensione di dominio esistente, prerequisito esplicito del
Validation Engine).

**Cosa è successo**: prima di iniziare, l'incremento Applications precedente è stato
committato (branch `feature/applications-catalog`, creato staccandosi da `main` per
convenzione). Poi piano approvato e implementato per intero: `NodeCapability`
(`src/Iris.Domain/Infrastructure/NodeCapability.cs`, enum semplice non `[Flags]`) e
`ResourceProfile` (owned type nullable) nuovi; `ServerNode` esteso con `Capabilities`/
`Resources`/`UsedPorts` e il metodo `UpdateCapacity` (replace wholesale, separato da
`UpdateDetails`); nuovo endpoint `PUT /servers/{serverId}/capacity` (riusa
`infrastructure.write`, nessun permesso nuovo); migrazione `AddServerCapacity` su
entrambi i provider.

**Decisione di modello presa in plan mode**: `UsedPorts` come semplice `IReadOnlyList<int>`
(simmetrico a `RuntimeMetadata.RequiredPorts`), non `{Port, Purpose}` come nello schizzo
Iris_v2 — motivata nel piano come scelta più adatta al confronto insiemistico che farà il
Validation Engine; segnalata esplicitamente come la decisione più discutibile, non
contestata in revisione.

**Dettaglio tecnico non scontato**: per ottenere `Capabilities` (un `List<NodeCapability>`)
salvato come array di stringhe leggibili (coerente con `Os`/`HostingType` mappati
`.HasConversion<string>()`) invece che come array di interi, serve
`builder.PrimitiveCollection(s => s.Capabilities).ElementType(e =>
e.HasConversion<string>())` — un semplice `.Property(...).HasConversion<string>()` non
compila su una collezione (quel metodo non esiste su `PropertyBuilder<IReadOnlyList<T>>`,
solo su `PrimitiveCollectionBuilder`, che si ottiene con `.PrimitiveCollection(...)` non
`.Property(...)`). Scoperto per errore di compilazione, corretto e verificato ispezionando
la migrazione generata (SQLite: colonna TEXT con array di stringhe JSON; Postgres:
`text[]` nativo).

**Verificato**: `dotnet build Iris.sln` (0 errori/0 warning) e `dotnet test Iris.sln`
verdi — 108/108 test (104 precedenti + 4 nuovi... nota: sono in realtà 6 nuovi ma 2 si
sommano dentro lo stesso test API multi-asserzione — vedi conteggio per progetto:
Domain.Tests 26, Application.Tests 56 (+3), Api.Tests 26 (+1)).

**Rischi residui / cosa resta aperto**: nessuna pagina client (voluto, backend-first);
Validation Engine non ancora scritto — ora ha tutto ciò che gli serve
(`ApplicationVersion.RuntimeMetadata` vs `ServerNode.Capabilities`/`Resources`/
`UsedPorts`) ma il confronto stesso è ancora da fare, insieme a Deployments.

**Prossimo step**: commit di questo incremento; poi plan mode per Deployments
(associazione Application+Version+Customer+Context+ServerNode) e/o Validation Engine.

---

## 2026-09-02 — riallineamento `.contex` alla branch `feature/applications-catalog`

**Classificazione**: consolidamento documentazione operativa.

**Cosa è successo**: dopo il passaggio alla branch `feature/applications-catalog`, è stato
riletto il context pack e confrontato con il codice/commit correnti. Trovate frasi ormai
stanti: Applications e Server capacity risultavano ancora "da fare" in alcuni file, e il
bootstrap prompt indicava repository/branch sbagliati. Aggiornati stato corrente,
decisioni, piano operativo, source map, prossime azioni e bootstrap prompt.

**Stato reale confermato**: Applications backend-first, ServerNode capability/risorse/
porte, login email/password con `UserSession`, accept invitation, setup wizard one-shot,
SMTP MailKit, Serilog e security scanning minimo sono presenti in branch.

**Verificato**: `dotnet test Iris.sln -c Release` verde — 135/135 test.

**Rischi residui / cosa resta aperto**: client MAUI non ricompilato in questa iterazione
perché il lavoro ha toccato solo documentazione `.contex`; resta assente la pagina MAUI
Applications. Deployments, Validation Engine e Actions sono ancora il prossimo blocco di
prodotto.

**Prossimo step**: modellare `DeploymentAssociation` con FK reali a
`ApplicationDefinition`/`ApplicationVersion`, `Customer`/`CustomerContext` e `ServerNode`,
poi aggiungere `ValidateDeployment` usando configuration knowledge e server capacity.

---

## 2026-09-02 — bootstrap SSO controllato / claim primo admin

**Classificazione**: feature di sicurezza/setup (bootstrap produzione senza seed nascosto).

**Cosa è successo**: aggiunto un meccanismo controllato per partire con SSO su database
vuoto: `POST /setup/claim-admin` richiede un'identita' gia' autenticata, verifica che
l'email sia presente in `Iris:Setup:AdminClaimEmails`, ricontrolla che non esista ancora
alcun assignment al ruolo `platform-admin`, provisiona JIT l'utente corrente e assegna il
ruolo di primo amministratore. Il client MAUI ora, dopo un SSO riuscito, interroga
`/setup/status` e invoca automaticamente il claim quando l'istanza necessita ancora setup.

**Decisione di prodotto**: il claim concede solo il ruolo admin e non configura SMTP. Il
wizard tradizionale `/setup/complete` resta il flusso completo mail + primo admin; il claim
serve per testare/attivare SSO in modo esplicito senza reintrodurre seed automatici.

**Documentazione**: aggiornato `docs/entra-id-setup.md` con la variabile
`Iris__Setup__AdminClaimEmails__0` e il comportamento atteso al primo login SSO. Il client
MAUI ora legge `src/Iris.App/appsettings.Development.json`, copiato nell'output di build,
per `IrisApi:BaseUrl` e `EntraId:{TenantId,ClientId,ApiScope}`.

**Verificato**: `dotnet build Iris.sln -c Release --no-restore` e
`dotnet test Iris.sln -c Release --no-build` verdi — 139/139 test. Il client MAUI compila
con `dotnet build src\Iris.App\Iris.App.csproj -c Debug --no-restore` usando `OutDir`
separato; l'output standard era bloccato da un processo `Iris.App` gia' in esecuzione.

**Rischi residui / cosa resta aperto**: manca ancora una UI dedicata alla configurazione
SMTP post-claim; finche' non c'e', gli inviti ricadono sul notifier/logging se SMTP non e'
stato configurato.

**Prossimo step**: tornare al piano Deployments/Validation, usando questo bootstrap SSO
come base per i test su installazione pulita.

---

## 2026-09-02 — persistenza stato maximized finestra MAUI

**Classificazione**: fix UX client Windows.

**Cosa è successo**: `WindowGeometryStore` salvava solo posizione e dimensione, quindi una
finestra principale chiusa in maximized veniva riaperta con l'ultimo rettangolo salvato.
Ora salva anche il flag `window.maximized.<key>` e i bounds del display
`window.display.<key>`; `NativeWindowConfigurator` ripristina la geometria normale, sposta
la finestra sul display salvato se ancora presente e poi massimizza, evitando di
sovrascrivere il rettangolo normale con le dimensioni fullscreen mentre la finestra e'
maximized.

**Verificato**: `dotnet build src\Iris.App\Iris.App.csproj -c Debug --no-restore` verde
con `OutDir` separato.

---

## 2026-09-02 — Governance users: tile read-only per l'utente corrente

**Classificazione**: fix UX + guardrail autorizzativo.

**Cosa è successo**: nella pagina Users il chiamante viene ordinato sempre per primo,
marcato con badge `You` e reso una tile di sola visualizzazione: niente edit, assign,
revoke, delete o invitation dalla UI. Lato Application/API e' stato aggiunto
`SelfGovernanceGuard`, cosi' anche chiamate dirette agli endpoint Governance ricevono 403
quando tentano update/delete/assign/revoke/invite sul proprio `User`.

**Verificato**: Governance application/API test verdi; `dotnet test Iris.sln -c Release
--no-build` verde — 141/141 test. Client MAUI compilato con `OutDir` separato.

---

## 2026-09-02 - Profilo, system settings, password recovery, remember me

**Classificazione**: feature UX/auth client + nuovi endpoint API.

**Cosa e' successo**: il flyout MAUI ora mostra `Profile` e `Sign out` sotto nome/email
dell'utente corrente; il footer non contiene piu' il sign-out e punta a `System settings`.
Creata `ProfilePage` con dati utente, cambio password locale, permessi effettivi e access
history; creata `SystemSettingsPage` con scelta theme `System`/`Light`/`Dark` per tutti e
visibilita' SMTP/integrations lato superadmin.

**Backend/API**: aggiunti `GET /profile`, `GET /system/settings` e
`POST /auth/password/reset`. Il reset password e' anonimo e non-enumerante: risponde
sempre `Sent=true`, ma per un utente attivo emette un token one-time tramite il repository
inviti e invia il link con il notifier esistente. `GetMyProfileHandler` legge le sessioni
Iris locali; se il chiamante arriva da SSO/dev header e non c'e' una sessione locale,
mostra una riga sintetica per la sessione autenticata corrente.

**Client**: `LoginPage` ora espone `Forgot password?` e `Remember me`. Le sessioni locali
ricordate usano SecureStorage con fallback Preferences; lo SSO resta affidato a MSAL/WAM.
La preferenza tema viene applicata all'avvio tramite `AppPreferenceService`.

**Bug intercettato e corretto**: SQLite non supporta bene l'ordinamento EF diretto su
`DateTimeOffset`; `UserSessionRepository.GetForUserAsync` ora filtra a DB e ordina in
memoria, sufficiente per la history utente e compatibile con i test SQLite.

**Verificato**: `dotnet test Iris.sln` verde - 145/145 test; `dotnet build Iris.App.sln`
verde - 0 warning/0 errori.

**Rischi residui / cosa resta aperto**: System settings mostra lo stato SMTP e i link
OpenBao/Ansible, ma non salva ancora modifiche post-setup ne' testa connessioni verso
OpenBao/Ansible; le integrazioni restano dichiarative finche' non arriva il modulo
Actions/Deployments.

---

## 2026-09-02 - Transaction log e activity per area

**Classificazione**: feature cross-cutting audit/compliance.

**Cosa e' successo**: aggiunto `TransactionLogEntry` e tabella `TransactionLog`, scritta
automaticamente da `TransactionLogInterceptor` durante `SaveChanges`. Ogni create/update/
delete su entita' dominio mappate produce una riga con `TransactionId`, timestamp UTC,
area, azione, entity type/id, summary e attore (`ActorUserId`, email, display name,
external id). Le aree iniziali sono Governance, Infrastructure, Applications e Settings.

**API/UI**: aggiunto `GET /activity?area=...&take=...`, riservato a `platform.admin`.
`SystemSettingsPage` mostra un pannello Activity con filtro per area (`All`, Governance,
Infrastructure, Applications, Settings), autore e target della modifica.

**Persistenza**: migrazione `AddTransactionLog` generata per entrambi i provider:
SQLite in `src/Iris.Infrastructure/Persistence/Migrations`, Postgres in
`src/Iris.Migrations.Postgres/Migrations`.

**Verificato**: `dotnet test Iris.sln` verde - 146/146 test; `dotnet build Iris.App.sln
--no-restore -p:UseAppHost=false` verde - 0 warning/0 errori. Il build standard puo'
mostrare warning di file bloccati se API/MAUI sono in esecuzione in Visual Studio.

**Rischi residui / cosa resta aperto**: per ora il log registra il livello entity
create/update/delete, non ancora diff di campo old/new; quando arriveranno Deployments e
Actions bisogna mappare i nuovi tipi in `TransactionLogInterceptor.AreaFor`.

---

## 2026-09-02 - Applications inventory nel client MAUI

**Classificazione**: feature UX/catalogo applicazioni.

**Cosa e' successo**: completato il primo strato visibile dell'assimilazione
Applications: endpoint `PUT /applications/{id}` per aggiornare l'inventory, mantenendo
immutabile lo slug; aggiunta la pagina MAUI `Applications` sotto Workspace, visibile con
`applications.read`, con creazione e modifica via dialog modali. Le modifiche usano il
lock advisory nuovo `application`, allineato a user/customer/server.

**Client/API**: `IrisApiClient` espone read/create/update delle applicazioni; la pagina
mostra runtime, repository, branch, stato attivo, conteggio versioni e riepilogo della
configuration knowledge importata. Il dettaglio versione/import resta il prossimo
incremento sopra l'inventory.

**Verificato**: `dotnet test Iris.sln` verde - 151/151 test; `dotnet build Iris.App.sln
--no-restore -p:UseAppHost=false` verde - 0 warning/0 errori.

**Rischi residui / cosa resta aperto**: manca ancora la UI per aggiungere versioni e
importare/consultare il dettaglio della configuration knowledge; subito dopo conviene
passare a `DeploymentAssociation`, usando questa inventory come sorgente applicazioni.

---

## 2026-09-02 - Startup splash e restore sessione ricordata

**Classificazione**: fix UX avvio client.

**Cosa e' successo**: la login non e' piu' la prima pagina reale dell'app. Aggiunta
`StartupPage`, uno splash interno registrato come ShellContent iniziale, che esegue il
check setup con retry e prova il restore del token salvato da `Remember me`. Se il token
e' valido naviga direttamente a dashboard o first-login; la login appare solo quando non
c'e' una sessione ricordata valida o l'API resta irraggiungibile.

**Motivazione**: prima il restore avveniva in `LoginPage.OnAppearing`, quindi la login
veniva renderizzata per un attimo anche nel caso felice. Ora l'utente resta sullo splash
durante il bootstrap.

**Verificato**: `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false` verde - 0
warning/0 errori.

---

## 2026-09-02 - Server discovery, data services e artifact assimilation

**Classificazione**: feature Infrastructure/Applications preparatoria ai Deployments.

**Cosa e' successo**: aggiunto il port `IServerInventoryProbe` e l'endpoint
`POST /servers/{id}/discover`, invocabile solo dopo aver registrato almeno una credenziale
server. L'adapter attuale e' `MockServerInventoryProbe`, deterministico, ma il contratto e'
gia' pronto per un probe Ansible/SSH reale: aggiorna OS rilevato, versione OS, dimensione
macchina, capability, CPU/RAM/dischi e porte usate. La pagina MAUI Servers mostra questi
dati e lancia la discovery dopo l'aggiunta credenziale o tramite comando manuale.

**Data services**: introdotto `DataServiceInstance` per RDS/cache gestiti (`Mssql`,
`PostgreSql`, `Redis`) con endpoint, porta, username, password solo via `ISecretStore`,
versione, size, storage, ambiente e stato attivo. Aggiunti repository, handler, endpoint
`/data-services`, mapping audit Infrastructure e UI nella sezione Servers. La creazione
non e' piu' inline nella pagina: e' dentro `NewServerDialog`, tramite select `Server node`
/ `Managed data service`; per RDS il form accetta solo username/password, non SSH.
`IDataServiceInventoryProbe` rileva tipo/versione/size/storage dopo la credenziale.

**Applications assimilation**: `ApplicationDefinition` ora conserva anche
`ArtifactProvider`, `ArtifactFeed`, `ArtifactName`, `ArtifactPath` e `BuildPipelineUrl`.
Le dependency importate possono puntare al provider con `ProviderApplicationSlug` e
`ProviderPlaceholderKey`, cosi' una stessa chiave placeholder puo' rappresentare il servizio
che la espone e quello che la consuma. Aggiunta la guida
`docs/application-assimilation.md` con esempi .NET, Node/JavaScript, Java e Docker.

**Persistenza**: migrazioni `AddInfrastructureDiscoveryDataServicesAndArtifacts` e
`AddDataServiceCredentialsAndDiscovery` generate per SQLite e Postgres.

**Verificato**: `dotnet test Iris.sln --no-restore` verde - 156/156 test; `dotnet build
Iris.App.sln --no-restore -p:UseAppHost=false` verde - 0 warning/0 errori.

---

## 2026-09-02 - Dischi server per applicazioni e backup

**Classificazione**: feature Infrastructure/capacity.

**Cosa e' successo**: esteso `ResourceProfile` con due quote dedicate:
`ApplicationDiskGb` e `BackupDiskGb`, mantenendo `DiskGb` come totale opzionale. Il
capacity endpoint `PUT /servers/{id}/capacity` salva e restituisce le nuove quote,
validando valori non negativi e, quando il totale e' noto, impedendo che applicazioni +
backup superino il disco totale.

**Client/API**: `IrisApiClient` ora espone `UpdateServerCapacityAsync`; la pagina Servers
mostra un riepilogo risorse nella tile, e `EditServerDialog` consente di settare CPU/RAM,
disco totale, disco applicazioni, disco backup e porte note nello stesso flusso di edit
server.

**Persistenza**: migrazione `AddServerDiskReservations` generata per SQLite e Postgres,
con colonne nullable `ResourceApplicationDiskGb` e `ResourceBackupDiskGb` sulla tabella
`Servers`.

**Verificato**: `dotnet test Iris.sln` verde - 151/151 test; `dotnet build Iris.App.sln
--no-restore -p:UseAppHost=false` verde - 0 warning/0 errori.

---

## 2026-09-02 - Riordino flyout MAUI

**Classificazione**: fix UX navigazione client.

**Cosa e' successo**: il flyout custom e' stato riorganizzato per leggere meglio le aree:
Dashboard e' sempre la prima voce, poi Workspace, Governance, Infrastructure,
Applications e Development. La pagina Applications e' ora sotto la sezione Applications
come voce `Inventory`; `Components` e' stata spostata in Development ed e' visibile solo
nelle build DEBUG.

**Dettaglio UI**: le righe del flyout hanno ora icona, testo allineato e una piccola
barra laterale che distingue la voce primaria Dashboard, avvicinando l'organizzazione al
pattern amministrativo mostrato nello screenshot di riferimento.

**Verificato**: `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false` verde - 0
warning/0 errori.

---

## 2026-09-02 - Lista infrastructure unificata server + RDS

**Classificazione**: feature UX Infrastructure.

**Cosa e' successo**: la pagina Servers non separa piu' "Managed data services" e
"Server nodes": entrambe le tipologie confluiscono nella stessa lista `Resources`.
Ogni riga usa una icona differente, badge per tipo/tecnologia/ambiente/versione e mantiene
le azioni contestuali: edit e discovery per tutti, add credential solo per i server node.
Il dialog `New resource` continua a scegliere tra `Server node` e `Managed data service`.

**Filtri/sort**: aggiunta una barra filtri con tipo risorsa, OS (`Linux`, `Windows`,
`N/A` per RDS), ricerca versione, ricerca tag e ordinamento per nome/tipo/OS/versione/tag,
con toggle discendente. I tag aggregano tipo, tecnologia, ambiente, capability, porte e
size/storage per rendere filtrabili anche informazioni operative.

**Fix assorbito il 2026-09-03**: il form inline degli RDS compariva anche dentro una riga
`Server node` per via del binding XAML annidato `DataService.IsEditMode` quando
`DataService` era nullo. `InfrastructureResourceRowViewModel` ora espone wrapper espliciti
(`IsEditingDataService`, `DataServiceError`, `HasDataServiceError`, lock notice server),
e lo XAML li usa per mostrare i campi RDS solo sulle righe data service.

**Verificato**: `dotnet test Iris.sln --no-restore` verde - 166/166 test. La build MAUI e'
verde con output isolato:
`dotnet build Iris.App.sln --no-restore -p:UseAppHost=false -p:BaseOutputPath=...\artifacts\verify-build\`
- 0 warning/0 errori. La build su output standard era bloccata da un processo `Iris.App`
gia' in esecuzione.

---

## 2026-09-03 - Guida estrazione manuale configuration knowledge

**Classificazione**: documentazione operativa Applications.

**Cosa e' successo**: estesa `docs/application-assimilation.md` con una sezione
`Estrazione manuale` per produrre a mano `iris-package.json` quando non esiste ancora un
extractor automatico o quando si vuole validare una application senza toccare la pipeline.
La guida copre flusso operativo, regole su segreti/placeholders/provider-consumer, import
manuale via PowerShell e template JSON per `.NET`, Node/JavaScript, Java/Spring e
Docker/container.

**Verificato**: rilettura mirata del documento e controllo dei riferimenti. Nessuna build
o test eseguiti perche' la modifica e' solo documentale.

---

## 2026-09-03 - Guida manuale nel FE Extractor guide

**Classificazione**: feature UX/documentazione in-app Applications.

**Cosa e' successo**: la guida di estrazione manuale non vive piu' solo nel documento
markdown. `ExtractorGuidePage`, gia' presente nella sezione Applications del flyout, ora
mostra nello stesso posto l'uso dell'extractor .NET automatico, il flusso di import
manuale, il comando PowerShell copiabile e template JSON copiabili per `.NET`,
Node/JavaScript, Java/Spring e Docker/container.

**Verificato**: `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false
-p:BaseOutputPath=...\artifacts\verify-build\` verde - 0 warning/0 errori;
`dotnet test Iris.sln --no-restore` verde - 166/166 test.

---

## 2026-09-03 - Extractor guide per tecnologia e Jinja2 Ansible

**Classificazione**: fix UX/documentazione in-app Applications.

**Cosa e' successo**: `ExtractorGuidePage` e' stata riorganizzata verticalmente per
tecnologia (`C# / .NET`, `Java / Spring`, `Node / JavaScript`, `Docker / container`,
`Ansible Jinja2 template`). Dentro ogni tecnologia ci sono due aree orizzontali:
`Automatic` per l'extractor disponibile o pianificato e `Manual manifest` per la
composizione a mano di `iris-package.json`. Rimossa l'ambiguita' del termine "manual
fallback": il concetto corretto e' estrazione manuale come scrittura controllata del
manifest.

**Decisione**: interpretare template Ansible `.j2` ha senso come passo futuro per
standardizzare il manifest, perche' i template descrivono spesso la forma finale della
configurazione runtime. La guida introduce `targetKind = "ansible:j2"` e un esempio di
manifest derivato da variabili Jinja.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore` verde - 166/166 test.

---

## 2026-09-03 - Flyout collassabile e route-aware

**Classificazione**: feature UX navigazione client.

**Cosa e' successo**: le macro categorie del flyout (`Workspace`, `Governance`,
`Infrastructure`, `Applications`, `Development`) sono ora bottoni con chevron che aprono e
chiudono le sotto-voci. La route corrente viene osservata da `AppShell` e propagata a
`AppShellViewModel`, cosi' la categoria della pagina attiva resta aperta e il menu evidenzia
sia header sia voce attiva. `Dashboard` resta una voce standalone in cima; `System settings`
rimane nel footer ma ora viene evidenziato quando e' la pagina corrente.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore` verde - 166/166 test.

---

## 2026-09-03 - Componente globale TabGroup

**Classificazione**: feature UX/design system client.

**Cosa e' successo**: aggiunto `src/Iris.App/Controls/TabGroup.cs`, un controllo MAUI
riutilizzabile text-based con `Title`, `ItemsSource` e `SelectedIndex` two-way. Il
componente mostra tab orizzontali, indicatore attivo in stile Fluent e contenuto della tab
selezionata. `ComponentsPage` ora lo espone nella gallery globale con tre tab di esempio.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore` verde - 166/166 test.

---

## 2026-09-03 - Extractor guide usa TabGroup

**Classificazione**: refactor UX client.

**Cosa e' successo**: `ExtractorGuidePage` ora usa il componente globale
`controls:TabGroup` invece di disegnare manualmente due colonne per `Automatic` e
`Manual manifest`. `ExtractorGuideViewModel` espone `SharedManifestTabs` e
`TechnologyGuides`, una collezione verticale di tecnologie; ogni tecnologia contiene le
due tab standard. `TabGroup` ora permette anche di copiare il contenuto della tab
selezionata dall'icona code.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore` verde - 166/166 test.

---

## 2026-09-03 - TabGroup con contenuto strutturato per Extractor guide

**Classificazione**: fix UX/design system client.

**Cosa e' successo**: `TabGroup` non renderizza piu' solo un unico blocco testuale piatto:
`TabGroupItem` puo' esporre `Blocks` di tipo `Text`, `Note` e `Code`. Il contenuto codice
usa font monospace, sfondo dedicato e scroll orizzontale, mentre testo e note hanno
gerarchia visiva separata. `ExtractorGuideViewModel` usa questi blocchi per distinguere
spiegazioni, stato dell'extractor, comandi, pipeline e manifest JSON.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\` verde - 166/166 test.

---

## 2026-09-03 - Extractor guide: tab Fields per compilazione manifest

**Classificazione**: feature documentazione FE/applications.

**Cosa e' successo**: il tab group condiviso in testa a `Extractor guide` ora apre con
`Fields`, una guida operativa su come compilare e interpretare `configurationKeys`,
`dependencies`, `placeholders` e `warnings`. Include regole per non inserire segreti reali
nel manifest, esempi copiabili per connection string PostgreSQL gestita, Redis opzionale,
HTTP API provider/consumer e convenzione dei `placeholderKey`. Allineato anche
`docs/application-assimilation.md`.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\` verde - 166/166 test.

---

## 2026-09-03 - Flyout: rimosso testo dai bottoni overlay

**Classificazione**: fix UX client MAUI.

**Cosa e' successo**: il footer `System settings` mostrava un secondo testo sopra la label
e non era cliccabile in modo affidabile perche' il `Button` overlay della riga aveva
ancora `Text="System settings"`. Tutti i bottoni overlay del flyout ora hanno `Text=""`
e `SemanticProperties.Description`; il testo visibile resta sulle label della riga.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\` verde - 166/166 test.

---

## 2026-09-03 - Titlebar: titolo app bianco in dark mode

**Classificazione**: fix UX/theme client Windows.

**Cosa e' successo**: il titolo app `Iris` in dark mode restava grigio/scuro come la
barra nativa. Il foreground della titlebar ora usa un valore esplicito bianco
(`#FFFFFF`) in dark mode su tutti i canali coinvolti: `AppWindowTitleBar`, DWM
`DWMWA_TEXT_COLOR`, resource brush WinUI e TextBlock rilevati nella fascia fisica della
titlebar. Il passaggio resta ripetuto dopo il layout iniziale per intercettare il
lazy-load della titlebar WinUI.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\` verde - 166/166 test.

---

## 2026-09-03 - Titlebar: foreground titolo dark mode

**Classificazione**: fix UX/theme client Windows.

**Cosa e' successo**: il titolo app in dark mode poteva restare dello stesso colore della
barra perche' alcune parti WinUI consumano `WindowCaptionForeground` come brush, non come
color, e `PART_TitleText` puo' essere creato in lazy-load dopo il primo passaggio. Il
configuratore ora imposta `WindowCaptionForeground`/disabled come `SolidColorBrush`,
propaga il foreground ai discendenti testuali dei pezzi titlebar e ripassa dopo il layout
iniziale.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\` verde - 166/166 test.

---

## 2026-09-03 - CodeBlock globale selezionabile/copiabile

**Classificazione**: feature UX/design system client.

**Cosa e' successo**: aggiunto `controls:CodeBlock`, componente globale per snippet,
comandi e manifest. Usa un `Editor` read-only invece di una `Label`, quindi il codice puo'
essere selezionato dall'utente; il copy button copia l'intero blocco. `TabGroup` ora delega
i blocchi `Code` a `CodeBlock`, e `ComponentsPage` lo mostra nella gallery globale come
standard di layout.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\` verde - 166/166 test.

---

## 2026-09-03 - Feedback visivo copia CodeBlock

**Classificazione**: fix UX/design system client.

**Cosa e' successo**: il copy button di `controls:CodeBlock` ora, dopo una copia riuscita,
cambia temporaneamente icona in una spunta verde e aggiorna il tooltip a `Copied`, poi
torna allo stato normale `Copy code`.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\` verde - 166/166 test.

---

## 2026-09-03 - Titlebar: hamburger e titolo app nuovamente visibili

**Classificazione**: fix UX/theme client Windows.

**Cosa e' successo**: dopo l'allineamento focus/unfocus l'overlay della titlebar poteva
restare davanti al contenuto WinUI reale, nascondendo hamburger e titolo app. L'overlay
`IrisTitleBarChromeBackground` ora resta dietro ai controlli, mentre il configuratore
aggancia anche le parti del template moderno `controls:TitleBar`
(`PART_LayoutRoot`, `PART_PaneToggleButton`, `PART_TitleText`, ecc.) oltre ai nomi
storici del `NavigationView`. Aggiunto anche il resource key corretto
`TitleBarPaneToggleButtonForegroundDisabled`, mantenendo quello legacy senza `Button` per
compatibilita' col dizionario WinUI.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\` verde - 166/166 test.

---

## 2026-09-03 - Chrome superiore stratificato light/dark focus/unfocus

**Classificazione**: fix UX/theme client Windows.

**Cosa e' successo**: consolidati i livelli cromatici del chrome Windows/MAUI in tre
token espliciti. La titlebar applicativa/nativa (hamburger, titolo app, zona centrale e
caption button Windows) usa `AppChrome*` quando la finestra ha focus e
`AppChromeInactive*` quando lo perde; la barra MAUI sotto con il titolo pagina usa
`PageTitleBar*`; il corpo pagina usa `AppBackground*`. Questo evita sia che il page header venga
sovrascritto a runtime con il background della pagina, sia che la titlebar applicativa
resti separata dai bottoni nativi.
`AppChromeTheme` applica il colore Shell/page title bar via codice e usa il tema effettivo
(`UserAppTheme` se impostato, altrimenti tema sistema); `AppPreferenceService` forza anche
il refresh della titlebar nativa quando l'utente cambia Light/Dark. Il configuratore
Windows usa `AppWindowTitleBar`, resource WinUI `WindowCaption*`/`WindowCaptionButton*`,
`NavigationViewTopPaneBackground`, resource WinUI `TitleBar*` (pane toggle/hamburger,
foreground e stato deactivated), `RequestedTheme` del root WinUI, refresh su
`Loaded`/`ActualThemeChanged`/attivazione finestra e `DwmSetWindowAttribute` per caption,
testo, bordo e immersive dark mode. Lo stato attivo/inattivo della finestra viene tracciato
da `WindowActivationState` e applicato a tutti e tre i pezzi del chrome superiore. Il
foreground della titlebar e' ora esplicito dai token Iris (`TextPrimaryLight/Dark`) per
evitare testo nero in dark mode. Le risorse background del template `WindowCaption*` e del
top pane `NavigationView` sono impostate come `SolidColorBrush`
quando WinUI le consuma come brush. In piu' viene applicato un override diretto al visual
tree del top pane Shell (`TopNavArea`, `PaneToggleButtonGrid`, `ButtonHolderGrid`,
`TogglePaneButton`, `PaneTitleTextBlock`) e viene inserito/aggiornato l'overlay
`IrisTitleBarChromeBackground` dentro `RootGrid`, alto quanto la titlebar nativa, per
coprire la zona centrale tra hamburger/titolo e caption button. Cosi' hamburger/titolo
app, zona centrale e pulsanti nativi leggono lo stesso valore reale e cambiano insieme tra
focus/unfocus.
Rimossa anche l'ombra Shell dalla NavBar per non introdurre una fascia intermedia che
sembri un quarto colore.

**Verificato**: build MAUI verde su output isolato - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\` verde - 166/166 test.

---

## 2026-09-03 - Assimilazione manifest AugeG4 GrpcFlow

**Classificazione**: prova dati Applications / import reale da manifest esterni.

**Cosa e' successo**: assimilati i manifest di prova
`iris-application.inventory.json` e `iris-package.json` dell'applicativo AugeG4 GrpcFlow.
L'import e' passato dagli endpoint reali Applications, non da scrittura diretta sul DB:
creata application `algorab-augeg4-grpcflow`
(`01a067ae-ba15-7559-af21-bf01a4948938`) e versione
`net8.0-Windows-win-x64-self-contained`
(`01a067ae-bb10-7da7-8278-3c10c8cfe127`), poi importato il package con 41
configuration key, 5 dependency, 3 placeholder e 5 warning.

**Note emerse**: il file `iris-package.json` conteneva `defaultValue` numerici e booleani
validi semanticamente, ma il contratto API oggi usa `ConfigurationKeyInput.DefaultValue`
come `string?`; per completare l'import sono stati normalizzati a stringa (`587`,
`true`, `false`, ecc.). Per usare le permission admin senza password locale e senza
alterare il DB e' stata avviata un'istanza API dev dedicata mappando `admin@iris.local`
sull'`ExternalId` dell'admin reale `gabriele.angeli@algorab.com`; questo conferma che il
dev header con `AllowAnyEmail` da solo non basta quando serve ereditare grant gia'
presenti su un `ExternalId` specifico.

**Verificato**: `GET /applications/{applicationId}/versions/{versionId}` su API locale ha
restituito il dettaglio completo con configuration knowledge e warning importati.

---

## 2026-09-03 - Analisi modello configuration compiler

**Classificazione**: documento di riferimento / analisi da completare.

**Cosa e' successo**: aggiunto `docs/application-configuration-model-analysis.md` per
fissare l'analisi emersa dai manifest AugeG4.Engine/AugeG4.Web e dai file reali
`application.properties`/`AppSettings.config`, inclusi gli esempi master/slave. Il
documento chiarisce che il manifest descrive il contratto configurativo, mentre il valore
finale nasce in fase di installazione/deployment quando Iris conosce server, data service,
application collegate, profilo installativo e topologia.

**Casi marcati come incompleti**: firewall, nginx/apache, IIS binding avanzati, TLS/DNS,
porte interne/esterne, target multi-file/template Ansible, valori tipizzati, liste,
dependency application-to-application, profili master/slave, vincoli di compatibilita'
tra versioni applicative e versioni servizio (`MongoDB == 6`, Redis con range min/max,
PostgreSQL oppure MSSQL a seconda della configurazione).

**Verificato**: rilettura mirata del documento. Nessuna build/test eseguiti perche' la
modifica e' solo documentale.

---

## 2026-09-04 - Applications: upload e validazione manifest da UI

**Classificazione**: primo step UX verso manifest 1.1 / import guidato.

**Cosa e' successo**: nella pagina MAUI `Applications` e' stato aggiunto il comando
`Upload manifest` direttamente nella tile della singola application, visibile agli utenti
con gestione Applications. Il file picker accetta JSON e legge il manifest localmente
senza importarlo automaticamente. Al caricamento viene eseguita una validazione
client-side associata all'application scelta: `schemaVersion`, array
`configurationKeys`/`dependencies`/`placeholders`, campi obbligatori, booleani
`required`/`secret`, duplicati, default valorizzati su chiavi secret, default tipizzati
non-stringa, coerenza base tra `valueType` e `defaultValue`, `scope`, `serialization`,
`resolution` e dependency che puntano a `providerApplicationSlug`.
Per i manifest validi il report ora costruisce anche la preview di assimilazione nella
stessa tile: configuration key, dependency, placeholder esposti, profili/varianti
(`profiles`, `deploymentProfiles`, `installationProfiles`, `variants`) e decisioni da
risolvere prima di import/binding (segreti, valori required senza default, liste e
provider application mancanti o da confermare).

**Decisione di flusso**: questa iterazione si ferma alla validazione e al report visivo
dei problemi. Non chiama ancora `/applications/{id}/versions/{id}/import` e non crea
binding: il prossimo step sara' trasformare la preview valida in wizard di import,
risoluzione link application-to-application e poi binding fisico in deployment.

**Verificato**: `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false
-p:BaseOutputPath=...\artifacts\verify-build\` verde - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\` verde - 166/166 test.

**Aggiornamento successivo**: allineato il wizard al modello corretto: `releaseVersion` e
`sourceReference` sono obbligatori nel manifest e mostrati read-only; runtime, execution
targets, OS testati, minimum resources e port policy arrivano dal manifest. CPU/RAM e
porte non sono piu' input del wizard: le prime sono hint minimi, le seconde restano valori
per istanza/installazione. Aggiunta preview `applicationUnits`/`launchables` per modellare
piu' avviabili dallo stesso sorgente, come `augeg4.engine`, `augeg4.monitor-admin` e
`augeg4.p5.engine`.

---

## 2026-09-04 - Applications: manifest demo AugeG4 Engine

**Classificazione**: dati demo / test upload manifest per singola application.

**Cosa e' successo**: creato
`docs/manifests/augeg4-engine.demo.iris-package.json`, manifest demo `schemaVersion`
`1.1` per `augeg4-engine`. Il file rappresenta il contratto applicativo, non una
installazione finale: profili `master`/`slave`, configuration key tipizzate, liste CSV e
pipe-tuples, secret da secret store, service reference MongoDB/Redis/SMTP, dependency
applicativa verso `augeg4-web`, placeholder esposti e vincoli demo per MongoDB 6 e Redis
6.2-8.0.

**Verificato**: parsing JSON con `ConvertFrom-Json` completato senza errori. Nessuna build
eseguita perche' la modifica e' solo un artifact JSON documentale/demo.

---

## 2026-09-04 - Applications: wizard minimale di import manifest

**Classificazione**: feature UX/API client Applications.

**Cosa e' successo**: la preview valida ora espone `Start import`, visibile con permesso
`applications.import`. Il comando apre `ImportManifestDialog`: release version,
source reference, runtime target, OS testati, minimum resources e port policy arrivano dal
manifest e sono mostrati come dato non editabile. Il wizard si concentra sulle associazioni
logiche application-to-application, proponendo select con le application presenti nel
catalogo Iris. Il client MAUI espone gli endpoint gia' presenti nel backend:
`POST /applications/{id}/versions` e `POST /applications/{id}/versions/{versionId}/import`.
L'import crea una versione e salva nel modello attuale configuration key, dependency,
placeholder e warning normalizzati dal manifest validato.

**Limite esplicito**: application unit/launchable, profili master/slave, `valueType`,
`resolution`, `serialization`, `dependencyConstraints`, porte per istanza e default JSON
tipizzati restano rappresentati come preview/warning finche' non estendiamo dominio,
contratti e persistenza.

**Verificato**: `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false
-p:BaseOutputPath=...\artifacts\verify-build\` verde - 0 warning/0 errori; `dotnet test
Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\` verde - 166/166 test.

---

## 2026-09-04 - Applications: persistenza semantica manifest 1.1

**Classificazione**: feature domain/API/persistence, prosecuzione del wizard import.

**Cosa e' successo**: esteso il contratto `ImportConfigurationPackageRequest` e il modello
Applications per salvare i dati che il wizard gia' leggeva dal manifest: value type,
item type, scope, serialization/resolution/profile metadata sulle configuration key;
runtime execution targets, OS support testati, risorse minime e port keys; application
unit avviabili dallo stesso sorgente/artifact; installation profile master/slave; vincoli
di compatibilita' delle dependency come `MongoDB == 6` o range Redis. L'import resta
replace-whole come configuration key/dependency/placeholder, quindi un reimport rimpiazza
anche unit/profili/constraint.

**Persistenza/API**: aggiunte entita' figlie
`ApplicationUnitDefinition`, `InstallationProfileDefinition` e
`DependencyConstraintDefinition`, nuove configurazioni EF e migration SQLite/Postgres
`PersistApplicationManifestSemantics`. `ApplicationMapping` restituisce i nuovi dati nei
detail response e i conteggi nei summary.

**Client**: `ApplicationsViewModel` non produce piu' warning "preview-only" per questi
campi: li include nel payload reale di import. Le associazioni application-to-application
continuano a essere risolte dal wizard prima della chiamata API.

**Verificato**: `dotnet test Iris.sln --no-restore -p:BaseOutputPath=...\artifacts\verify-test\`
verde - 166/166 test; `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false
-p:BaseOutputPath=...\artifacts\verify-app-build\` verde - 0 warning/0 errori.

---

## 2026-09-04 - Application installation + binding (commit `f53eb2d`)

**Classificazione**: feature domain/API/persistence/client - primo strato del modulo
Deployments, sotto il nome "installation".

**Cosa e' successo**: aggiunto l'aggregato `ApplicationInstallation` che lega una
`ApplicationDefinition` + `ApplicationVersion` + `ApplicationUnitKey?` +
`InstallationProfileKey?` + `ServerNodeId` + `Environment` (`ContextKind`), con entita'
figlie `ApplicationInstallationBinding` (replace-whole via `ReplaceBindings`): ogni binding
mappa un `PlaceholderKey` a un target concreto tipizzato
(`ApplicationInstallationTargetKinds`: `data-service`, `application`, ...) via
`TargetId`/`TargetSlug` + `ValuePreview`. Repository
`ApplicationInstallationRepository`, handler `CreateApplicationInstallation` e
`ListApplicationInstallations`, endpoint `GET/POST /applications/installations` con permessi
`deployments.read`/`deployments.write`. Client MAUI: `NewApplicationInstallationDialog` e
metodi in `ApplicationsViewModel`/`IrisApiClient`. Migrazione `AddApplicationInstallations`
per SQLite e Postgres, mapping `Applications` in `TransactionLogInterceptor.AreaFor`.

**Decisione**: le FK sono `Guid` semplici, non navigation EF; non c'e' ancora legame con
`Customer`/`CustomerContext`. Sono scelte da rivedere quando si chiude l'associazione
completa (punto 8 di `05-next-actions.md`), ma bastano al piano Ansible.

**Rischi residui**: nessuna UI di lista/dettaglio, nessun update dei binding dopo la
creazione, nessuno stato di ciclo di vita.

---

## 2026-09-04 - Ansible plan + connettori OpenBao/AWX/Ansible (commit `11802b3`, fix `39d769a`)

**Classificazione**: feature domain/API/infrastructure - port/adapter verso i sistemi di
esecuzione esterni, ancora mock-first.

**Cosa e' successo**: fissata in `docs/application-configuration-model-analysis.md` la
decisione architetturale: **Iris non genera i file di configurazione finali**. Iris
produce un piano di variabili `iris_*` + binding + operations; il rendering `.j2` e ogni
modifica infrastrutturale (servizi, container, firewall, reverse proxy, TLS, DNS) le fa
Ansible/AWX tramite ruoli e task `template`.

- `GET /applications/installations/{id}/ansible-vars` ->
  `GetApplicationInstallationAnsiblePlanHandler`: variabili filtrate per
  `InstallationProfileKey`, `templateTargets` normalizzati `ansible:j2:<target>`,
  `operations` ordinate (load plan -> fetch artifact -> render template -> runtime
  service/container -> network apply), `associations` risolte/non risolte, `source` per
  variabile (`iris:data-service`, `iris:application`, `manifest:default`, `manual`),
  warning per required non risolti e per il fatto che Iris non renderizza i file.
- `POST /applications/installations/{id}/awx/launch` ->
  `LaunchApplicationInstallationAwxJobHandler`: compone il piano, `IAnsibleExecutionPackageBuilder`
  costruisce `extra_vars`, `IAwxClient` lancia il job template. Non persiste la run, non
  collegato a UI.
- Porte in `Iris.Application/Abstractions`: `IIntegrationConnector`, `IAwxClient`,
  `IAnsibleExecutionPackageBuilder`. Adapter in `src/Iris.Infrastructure/Integrations`:
  `OpenBaoConnector`, `AwxClient`, `AnsibleExecutionPackageBuilder`; `OpenBaoSecretStore`
  in `src/Iris.Infrastructure/Secrets`. DI non distruttivo: `ISecretStore` -> OpenBao solo
  con `Endpoint` + `Token`, altrimenti `InMemorySecretStore` (ora singleton). AWX/Ansible
  options da `Iris:Integrations:*`.
- `GET /system/settings` aggrega lo stato reale via `IEnumerable<IIntegrationConnector>`
  (`GetStatusAsync(probe:false)`) e aggiunge `Message` a `IntegrationLinkResponse`,
  mostrato in `SystemSettingsPage`.

**Bug bloccante trovato in questa sessione**: `11802b3` era stato committato senza
compilare. `OpenBaoSecretStore.StoreAsync` sceglieva il payload KV v1/v2 dentro un unico
`JsonContent.Create` con un ternario che produceva due tipi diversi (tipo anonimo
`{ data = ... }` vs `Dictionary<string,string>`), quindi `T` non inferibile - CS0411,
`Iris.sln` non buildava. Corretto in `39d769a` branchando il ternario sui due
`JsonContent.Create`, ognuno con il proprio `T`.

**Verificato**: `dotnet build Iris.sln` verde - 0 warning/0 errori; `dotnet test Iris.sln`
verde - 169/169; `dotnet build Iris.App.sln --no-restore -p:UseAppHost=false
-p:BaseOutputPath=...\scratchpad\verify-app-build\` verde.

**Rischi residui / cosa resta aperto**: nessuna run history / persistenza del launch AWX,
nessun polling stato o log della run, nessun endpoint test-connection (`probe:true`),
nessun pulsante Deploy nel client, nessun test sugli adapter nuovi
(`AnsibleExecutionPackageBuilder` e' logica pura e andrebbe coperto). Il Validation Engine
resta il pezzo centrale non ancora scritto.

**Prossimo step**: da decidere - (A) chiudere il loop connettori con `InstallationRun`/
`PreparedAction` + polling + pulsante Deploy, oppure (B) Validation Engine
(`05-next-actions.md` punto 9), che non dipende da un AWX reale.

---

## 2026-09-04 - Validation Engine v1

**Classificazione**: feature domain/application/API - il pezzo centrale mancante dei
Deployments (scelta B della sessione precedente).

**Cosa e' successo**: aggiunto `ValidateApplicationInstallationHandler`
(`src/Iris.Application/Applications/ValidateApplicationInstallation.cs`), i contratti
`ApplicationInstallationValidationResponse` / `ApplicationInstallationValidationCheckResponse`,
l'endpoint `GET /applications/installations/{installationId}/validate` (perm
`deployments.validate`, gia' nel catalogo) e la registrazione DI. Solo lettura: prende una
`ApplicationInstallation` + i suoi binding, risolve `ApplicationDefinition`/
`ApplicationVersion`/`ServerNode` + tutti i `DataServiceInstance`, e produce una lista
tipata di check con severita' `error`/`warning`/`info` e `IsValid` = nessun errore.

**Regole v1** (categorie `placeholder`/`configuration`/`dependency`/`os`/`capability`/
`port`/`capacity`/`constraint`):
- placeholder required non coperto da un binding risolto -> `placeholder.unbound` /
  `placeholder.unresolved` (binding presente ma senza target);
- configuration key required (filtrata per `InstallationProfileKey` via `ProfilesJson`)
  senza binding ne' default -> `configuration.secret-unbound` / `configuration.unresolved`
  / `configuration.secret-missing` (error) o `configuration.manual-value` (warning);
- dependency required non legata -> `dependency.unbound`; `ProviderApplicationSlug` non nel
  catalogo -> `dependency.provider-missing` (warning); opzionale non legata -> info;
- OS: `OsSupportJson` non vuoto e nessun match con `server.Os` -> `os.incompatible`
  (error); altrimenti `PreferredOs != server.Os` -> `os.not-preferred` (warning);
- capability: se l'app espone porte/port key/unit e il server ha capability dichiarate ma
  non `ServiceHost` -> `capability.missing` (error); server senza capability ->
  `capability.unknown` (info);
- `RequiredPorts` ∩ `UsedPorts` -> `port.collision` (error) per porta;
- `MinimumCpuCores`/`MinimumMemoryMb` > `ResourceProfile` -> `capacity.cpu`/`capacity.memory`
  (error); `RequiredCpuCores`/`RequiredMemoryMb` sforati -> variante `-recommended`
  (warning); `Resources` null -> `capacity.unknown` (info);
- `DependencyConstraintDefinition` con `PlaceholderKey` legato a un `dataService`:
  `ServiceKind` mappato (`postgres`/`redis`/`mssql`) e diverso dal `Kind` ->
  `constraint.service-kind` (error); `VersionExpression` non soddisfatta da `ds.Version`
  -> `constraint.version` (error). Parser `SatisfiesVersion` (internal, testato) copre
  token nudo (major), `>= <= == > < ~> ^`, range `A-B`, clausole unite da `&&`/`,`/`and`;
  espressione o versione non parsabile -> `info`, mai blocco.

**Decisioni / limiti noti**: capability sempre valutata come `ServiceHost` (euristica),
nessun check su disco, nessun legame con `Customer`/`CustomerContext`, nessuna UI MAUI del
report. Il parser di versioni e' volutamente minimale e non-bloccante sull'ignoto.

**Verificato**: `dotnet build Iris.sln` verde - 0 warning/0 errori; `dotnet test Iris.sln`
verde - 180/180 (11 nuovi: 3 handler + 8 casi `SatisfiesVersion`); `dotnet build
Iris.App.sln --no-restore -p:UseAppHost=false -p:BaseOutputPath=...\scratchpad\verify-app-build\`
verde.

**Prossimo step**: esporre il report nel client MAUI sopra la lista installation, oppure
tornare ad A (run history / polling AWX). Poi l'associazione completa Deployments con
`Customer`/`CustomerContext`.

---

## 2026-09-04 - Run history AWX v1 (opzione A)

**Classificazione**: feature domain/application/infrastructure/API - traccia l'esecuzione
dei deployment lanciati verso AWX (era il pezzo su cui l'utente si era bloccato prima del
Validation Engine).

**Cosa e' successo**: aggiunto l'aggregato `InstallationRun`
(`src/Iris.Domain/Applications/InstallationRun.cs`) con enum `InstallationRunKind`
(`AwxJob`) e `InstallationRunStatus` (`Pending`/`Running`/`Succeeded`/`Failed`/`Canceled`,
ultimi tre terminali). FK `Guid` verso `ApplicationInstallation` (coerente con come
`ApplicationInstallation` stessa referenzia version/server). Metodi comportamentali
`MarkSubmitted`/`UpdateStatus`/`MarkFailed`, tutti con `nowUtc` passato dall'handler
(`IClock`), guardia sui terminali. `CompletedAtUtc` stampato una sola volta al primo stato
terminale.

- `LaunchApplicationInstallationAwxJobHandler` riscritto: compone il piano + package,
  crea `InstallationRun(Pending)`, `SaveChanges` (la riga esiste anche se AWX fallisce),
  poi `IAwxClient.LaunchAsync`. Successo -> `MarkSubmitted(jobId, url, FromAwxStatus(status),
  message)` + `SaveChanges`. `ValidationException` da AWX -> `MarkFailed(ex.Message)` +
  `SaveChanges` + rethrow (endpoint resta 400). La risposta
  `ApplicationInstallationAwxLaunchResponse` ha ora `RunId` come primo campo.
- `ListInstallationRunsHandler` -> `GET /applications/installations/{id}/runs`
  (404 se l'installation non esiste, newest first).
- `GetInstallationRunHandler` -> `GET .../runs/{runId}` (404 se il run non appartiene
  all'installation): se il run non e' terminale e ha `ExternalJobId`, chiama il nuovo
  `IAwxClient.GetJobStatusAsync` (GET `/api/v2/jobs/{id}/`, legge `status`/`finished`/
  `failed`/`job_explanation`) e aggiorna lo stato; `ValidationException` (AWX non
  configurato/irraggiungibile) viene assorbita, il read restituisce l'ultimo stato noto.
- Persistenza: `IInstallationRunRepository` + `InstallationRunRepository` (filtra a DB,
  ordina in memoria per `CreatedAtUtc` - stesso accorgimento di `UserSessionRepository`
  per SQLite/`DateTimeOffset`), `InstallationRunConfiguration`, `DbSet` su `IrisDbContext`,
  mapping `Deployments` in `TransactionLogInterceptor.AreaFor`. Migrazione
  `AddInstallationRuns` generata per SQLite e Postgres; confrontate a mano, identiche per
  colonne/tipi/indice (differiscono solo nei nomi di tipo nativi).

**Decisioni / limiti noti**: nessun `PreparedAction` (fase di preparazione/draft) ancora;
polling solo on-read (nessun job di background); niente log completo oltre a
`job_explanation`; nessun pulsante Deploy / storico nel client MAUI; `AnsibleExecutionPackageBuilder`
(logica pura in Infrastructure) ancora senza test dedicati.

**Verificato**: `dotnet build Iris.sln` verde - 0 warning/0 errori; `dotnet test Iris.sln`
verde - 192/192 (12 nuovi: 5 Domain `InstallationRunTests`, 4 Application handler, 3 API);
`dotnet build Iris.App.sln --no-restore -p:UseAppHost=false
-p:BaseOutputPath=...\scratchpad\verify-app-build\` verde.

**Prossimo step**: portare in UI (MAUI) sia il report di validazione sia lo storico run +
un pulsante Deploy; oppure `PreparedAction` + endpoint `test-connection` (`probe:true`).
Poi l'associazione completa Deployments con `Customer`/`CustomerContext`.
