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
