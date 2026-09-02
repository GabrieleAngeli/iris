# Piano operativo

## Fatto

Access/AAA, Governance (utenti/clienti/inviti/edit-lock/password locali), Infrastructure
(server + credenziali con riferimento segreto + capability/risorse/porte), Applications
backend-first (catalogo, versioni, import configuration knowledge), auth produzione con
sessioni locali, first-run setup wizard, SMTP, Serilog e security scanning minimo. Client
MAUI con finestre modali native e design system documentato. Dettaglio in
`00-current-state.md`.

## Prossimo: Deployments -> Validation Engine -> Actions

Ordine dettato dal brief originale (`.context/iris_icp_project_context_for_llm.md`) e
confermato dalla forma delle dipendenze: Deployments ha ora bisogno di collegare
Applications, Customer/Context e ServerNode; Actions ha bisogno di Deployments; il
Validation Engine è trasversale a Deployments. Applications e ServerNode capacity sono già
presenti nella branch `feature/applications-catalog`.

**Fonte di riferimento**: `F:\Work\Iris_v2` - bozza mai completata (mai in git, backend
non buildabile) ma con un modello di dominio e delle regole di validazione già pensate
per esattamente questi moduli. Non portare il codice as-is (record piatti, repository
in-memory, nessuna convenzione EF/CQRS attuale): usarlo come mappa concettuale.

- `F:\Work\Iris_v2\src\Iris.Domain\Models.cs` - forma concettuale residua per
  `DeploymentAssociation`/`DeploymentCheck`/`PreparedAction` e binding; non serve più per
  `ApplicationDefinition`/`ApplicationVersion` perché il catalogo applicazioni è già stato
  riscritto nel progetto attivo.
- `F:\Work\Iris_v2\src\Iris.Domain\Enums.cs` - `DeploymentStatus`, `CheckSeverity`,
  `ActionType` (Ansible inventory/vars, AWX draft, OpenBao plan), `ActionStatus`.
- `F:\Work\Iris_v2\src\Iris.Application\Services.cs`,
  `DeploymentService.ValidateInternal` - regole di validazione da riscrivere come handler
  + risposta tipata: placeholder non risolto, dipendenza non legata, OS incompatibile,
  capability mancante, collisione porte, capacità insufficiente.
- I quattro `iris_codex_prompt_*.md` alla radice di `Iris_v2` - visione di prodotto,
  utile per deployment, validation e action preparation.

**Differenza chiave da tenere presente**: Iris_v2 duplica `Customer`/`ServerNode` come
record propri. Il progetto attuale ha già `Customer`/`CustomerContext`
(`Iris.Domain.Tenancy`), `ServerNode` (`Iris.Domain.Infrastructure`) e Applications
(`Iris.Domain.Applications`): `Deployments` deve referenziarli con FK reali, non
riscriverli.

**Prerequisito già completato**: `ServerNode` espone `Capabilities` come lista di
`NodeCapability` (non `[Flags]`), `ResourceProfile?` e `UsedPorts`. Il Validation Engine
deve usare questi campi per i controlli `os.mismatch`/`capability.mismatch`/
`port.collision`/`capacity.warning`.

## Non ancora pianificato in dettaglio

Monitoring/Audit reale, integrazione Grafana/capacity advisory, COM Matrix, generazione
runtime config materializzata su disco: menzionati nel brief ma nessun lavoro di analisi
fatto finora.
