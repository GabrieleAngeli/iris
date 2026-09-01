# Piano operativo

## Fatto

Access/AAA, Governance (utenti/clienti/inviti/edit-lock/password locali), Infrastructure
(server + credenziali con riferimento segreto), client MAUI con finestre modali native e
design system documentato. Dettaglio in `00-current-state.md`.

## Prossimo: Applications → Deployments → Actions → Validation Engine

Ordine dettato dal brief originale (`.context/iris_icp_project_context_for_llm.md`) e
confermato dalla forma delle dipendenze: Deployments ha bisogno di Applications (e di
`ServerNode` esteso con capability/risorse/porte) per esistere; Actions ha bisogno di
Deployments; il Validation Engine è trasversale a Deployments.

**Fonte di riferimento**: `F:\Work\Iris_v2` — bozza mai completata (mai in git, backend
non buildabile) ma con un modello di dominio e delle regole di validazione già pensate
per esattamente questi moduli. Non portare il codice as-is (record piatti, repository
in-memory, nessuna convenzione EF/CQRS attuale) — usarlo come mappa concettuale.

- `F:\Work\Iris_v2\src\Iris.Domain\Models.cs` — forma di `ApplicationDefinition`/
  `ApplicationVersion`/`ImportedArtifact`/`ConfigurationKey`/`DependencyDefinition`/
  `PlaceholderDefinition`/`DeploymentAssociation`/`DeploymentCheck`/`PreparedAction`.
- `F:\Work\Iris_v2\src\Iris.Domain\Enums.cs` — `ApplicationRuntimeType`, `DeploymentStatus`,
  `CheckSeverity`, `ActionType` (Ansible inventory/vars, AWX draft, OpenBao plan),
  `ActionStatus`.
- `F:\Work\Iris_v2\src\Iris.Application\Services.cs`,
  `DeploymentService.ValidateInternal` — le 5 regole di validazione già scritte
  (placeholder non risolto, dipendenza non legata, OS incompatibile, capability mancante,
  collisione porte, capacità insufficiente) — direttamente riusabili come lista di
  requisiti per il Validation Engine, da riscrivere come handler+eccezioni coerenti con
  `Iris.Application.Common.ApplicationExceptions`.
- I quattro `iris_codex_prompt_*.md` alla radice di `Iris_v2` — visione di prodotto,
  utile per capire l'intento dietro l'estrazione della configuration knowledge
  (Iris Extractor) prima di modellare `ImportedArtifact`.

**Differenza chiave da tenere presente**: Iris_v2 duplica `Customer`/`ServerNode` come
record propri. Il progetto attuale ha già `Customer`/`CustomerContext`
(`Iris.Domain.Tenancy`) e `ServerNode` (`Iris.Domain.Infrastructure`) — `Deployments` deve
referenziarli con FK reali, non riscriverli.

**Prerequisito da fare insieme al primo incremento**: estendere `ServerNode` con
capability (probabile `[Flags] enum`), `ResourceProfile` (CPU/RAM/disco) e porte usate —
oggi assenti per scelta (vedi `01-decisions.md`), servono al Validation Engine per
replicare i controlli `os.mismatch`/`capability.mismatch`/`port.collision`/
`capacity.warning` di Iris_v2.

## Non ancora pianificato in dettaglio

Monitoring/Audit reale, integrazione Grafana/capacity advisory, COM Matrix, generazione
runtime config materializzata su disco — menzionati nel brief ma nessun lavoro di analisi
fatto finora.
