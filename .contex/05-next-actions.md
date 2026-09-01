# Prossime azioni

Ordinate per priorità. Aggiornare questa lista a ogni chiusura di iterazione significativa.

1. ~~**Estendere `ServerNode`**~~ **fatto** (vedi sessione 2026-09-01, terza voce sotto).
   Capability come lista semplice (non `[Flags]`), profilo risorse nullable, porte come
   lista di interi — prerequisito del Validation Engine ora soddisfatto. Resta da fare:
   nessuna pagina client ancora (backend-first).
2. ~~**Applications — catalogo**~~ **fatto** (vedi sessione 2026-09-01 sotto). Resta da
   fare in un incremento successivo: la pagina client (nuova sezione flyout + lista +
   dettaglio versione/import, stesso pattern di `UsersPage`/`ServersPage`).
3. **Deployments — associazione**: `DeploymentAssociation` (Application+Version+
   Customer+Context+ServerNode target+binding placeholder+stato), FK reali verso
   `Customer`/`CustomerContext`/`ServerNode` esistenti (non duplicarli).
4. **Validation Engine**: riscrivere le 5 regole di Iris_v2
   (`DeploymentService.ValidateInternal`) come handler `ValidateDeployment` che produce
   una lista tipata di check con severità — usa i campi aggiunti al punto 1.
5. **Actions — preparazione**: `PreparedAction` (tipo Ansible inventory/vars, AWX draft,
   OpenBao plan — tutti mock), stato (Draft/Prepared/Pending/Running/Completed/Failed),
   endpoint di preview/prepare, pagina client con storico azioni.
6. Non pianificato in dettaglio: Monitoring/Audit reale, Grafana/capacity advisory, COM
   Matrix.

## Ultima sessione (2026-09-01)

- Confrontate `F:\Work\Iris_v2` e `F:\Work\Iris_v3` col progetto attivo — risultato in
  `docs/analisi-iris-v2-v3.md` (artifact pubblicato, e versione HTML sorgente nello
  scratchpad di sessione).
- Creata questa cartella `.contex/` adattando la convenzione da Iris_v3/Momentum.
- Prossimo passo dichiarato dall'utente: assimilare quanto proposto nel documento —
  `.contex/` (questo), tooling di security scanning (vedi nota sotto), e avviare il
  dominio Applications come primo incremento reale verso Deployments/Actions.

**Security scanning — fatto**: `.gitleaks.toml`, `.semgrepignore`,
`.github/workflows/security.yml` (solo Gitleaks + Semgrep, niente Checkov/tfsec/container/
IaC/DAST — non applicabili, nessun Docker/Terraform/Kubernetes nel repo).

## Ultima sessione (2026-09-01, continuazione) — Applications: catalogo + import

Implementato per intero il piano `Applications: catalogo + import della configuration
knowledge` (vedi `07-session-log.md` per il dettaglio). In sintesi:

- Dominio (`src/Iris.Domain/Applications/`): `ApplicationDefinition` (aggregate root),
  `ApplicationVersion`, `RuntimeMetadata` (owned type, riusa `ServerOs`), `ConfigurationKey`/
  `DependencyDefinition`/`PlaceholderDefinition` (entità figlie con propria identità).
- Application layer, Contracts, persistenza EF (5 configurazioni, migrazione
  `AddApplications` su entrambi i provider — SQLite: colonne JSON per le collezioni
  primitive; Postgres: array nativi `integer[]`/`text[]`), endpoint
  `src/Iris.Api/Endpoints/ApplicationsEndpoints.cs` (5 route, permessi già presenti nel
  catalogo), test applicativi e API (12 nuovi test, tutti verdi).
- **Bug corretto durante l'implementazione**: `ApplicationRepository.GetAllAsync`
  inizialmente non includeva le collezioni figlie delle versioni, quindi i conteggi
  (`ConfigurationKeyCount` ecc.) in `GET /applications` risultavano sempre a zero — un
  test API l'ha intercettato. Fix: `Include(...).ThenInclude(...)` anche in `GetAllAsync`
  (non solo in `GetAsync`/`GetForUpdateAsync`).
- Suite completa: `dotnet build Iris.sln` e `dotnet test Iris.sln` verdi (104 test totali,
  0 falliti).
- **Non incluso in questo incremento** (come da piano): pagina client MAUI Applications,
  estensione `ServerNode`, Deployments, Validation Engine, Actions — vedi punti 1, 3, 4, 5
  sopra.

## Ultima sessione (2026-09-01, terza continuazione) — ServerNode: capability/risorse/porte

Implementato il piano `ServerNode: capability, resource hints, porte note` (commit su
`feature/applications-catalog` dopo il commit di Applications). In sintesi:

- `NodeCapability` (enum semplice: LoadBalancer/Database/ServiceHost/Presentation, non
  `[Flags]`) e `ResourceProfile` (owned type nullable: CpuCores/MemoryMb/DiskGb, tutti
  nullable) nuovi in `src/Iris.Domain/Infrastructure/`. `ServerNode` esteso con
  `Capabilities`/`Resources`/`UsedPorts` e il metodo `UpdateCapacity` (replace wholesale,
  come `ApplicationVersion.ApplyImport`), tenuto separato da `UpdateDetails`.
- Nuovo endpoint `PUT /servers/{serverId}/capacity` (permesso `infrastructure.write`
  riusato, nessun permesso nuovo), handler `UpdateServerCapacityHandler`.
- Persistenza: `Capabilities` come collezione primitiva EF Core 9 con conversione
  per-elemento a stringa (`PrimitiveCollection(...).ElementType(e =>
  e.HasConversion<string>())` — non basta `.Property()` semplice per un `List<enum>` se
  si vuole la stringa invece dell'intero, a differenza di `List<int>`/`List<string>` dove
  `.Property()` normale basta), `UsedPorts` come collezione primitiva di interi,
  `Resources` come owned type opzionale (`Navigation(...).IsRequired(false)`). Migrazione
  `AddServerCapacity` su entrambi i provider — ispezionata: SQLite JSON text per le
  collezioni, Postgres `text[]`/`integer[]` nativi.
- 6 nuovi test (3 applicativi + 1 API end-to-end che ne copre più casi, incluso il 403 per
  Reader e il replace-non-accumula). Suite completa: 108/108 verdi.
- **Non incluso** (come da piano): Validation Engine (userà questi campi, ma il confronto
  vero e proprio è il prossimo passo), Deployments, pagina client.
