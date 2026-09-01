# Prossime azioni

Ordinate per priorità. Aggiornare questa lista a ogni chiusura di iterazione significativa.

1. **Estendere `ServerNode`** con capability (enum `[Flags]`), profilo risorse
   (CPU/RAM/disco) e porte usate — prerequisito del Validation Engine, oggi assente per
   scelta (`01-decisions.md`). Migrazione EF su entrambi i provider.
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
