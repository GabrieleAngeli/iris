# Iris_v2 e Iris_v3 — cosa resta utile

Analisi di due cartelle sorelle del progetto attivo (`F:\Work\iris`), per capire cosa
contengono davvero e cosa vale la pena riportare nel prodotto in corso. Versione
markdown del documento pubblicato come artifact il 2026-09-01.

## In sintesi

Se il prossimo passo è costruire **Applications / Deployments / Actions**, **Iris_v2** è
la fonte da riprendere: il modello dati, le regole di validazione e i quattro prompt di
visione. Da **Iris_v3 «Momentum»** conviene adottare solo due elementi operativi — la
convenzione `.contex/` (fatto, vedi `../.contex/`) e la configurazione di security
scanning — perché è un prodotto a sé, non va confuso con Iris.

## Iris_v2 — `F:\Work\Iris_v2`

Bozza generata da quattro prompt per un tool AI ("Codex"), mai committata in git.

**Cos'è**: il risultato parziale di prompt (`iris_codex_prompt_*.md` nella radice) che
descrivono la visione di prodotto originale con più dettaglio del brief attuale: workflow
operatore Overview → Infrastructure → Applications → Deployments → Actions → Governance,
con estrazione della configuration knowledge a build-time invece di un manifest scritto a
mano.

**Stato**: mai un repository git. I log di restore del backend (`api-restore.log`,
`infra-restore.log`, ~3 MB ciascuno) terminano entrambi con "Compilazione NON RIUSCITA".
Persistenza in-memory, non SQLite come richiesto dai prompt. Documentazione generata sotto
`docs/` tutta stub, sotto 1 KB a file. Il frontend Angular ha invece una build riuscita
almeno una volta.

**Vale la pena portare**:

- Il modello di dominio e le regole di validazione in
  `src/Iris.Domain/Models.cs`/`Enums.cs` e `src/Iris.Application/Services.cs`:
  `ServerNode`, `ApplicationDefinition`/`Version`, `PlaceholderDefinition`,
  `DeploymentAssociation`, `DeploymentCheck`, `PreparedAction` — e i controlli di
  deployability già scritti (placeholder non risolto, dipendenza non legata, OS
  incompatibile, capability mancante, collisione porte, capacità insufficiente). È il
  Validation Engine mai costruito nel progetto attivo. Da riscrivere per le convenzioni
  attuali (EF Core, CQRS, GUIDv7), ma come mappa concettuale è il pezzo più prezioso
  trovato.
- I quattro `iris_codex_prompt_*.md` come materiale di visione di prodotto.

**Non serve**: entrambi i tentativi di frontend Angular (il client attivo è MAUI);
`References/Momentum` (ispirazione di layout, non specifico di Iris); il codice backend
così com'è (in-memory, senza le convenzioni EF/CQRS del progetto attivo).

## Iris_v3 «Momentum» — `F:\Work\Iris_v3`

`Momentum.sln`, prodotto enterprise separato e maturo. Il nome della cartella è
fuorviante: non è un'evoluzione di Iris. Il README dichiara esplicitamente una
**piattaforma IoT** — ingestion telemetria via Kafka, persistenza TimescaleDB, notifiche
multi-canale, dashboard SignalR. Una ricerca del termine "Iris" in tutto il codice e nella
cronologia git non trova riferimenti reali al prodotto, solo il nome della cartella.

**Stato**: attivamente mantenuto (512 commit, ultimo il 28 agosto 2026), pipeline di
sicurezza reale in CI (Checkov, Gitleaks, Semgrep, tfsec) — molto più maturo e
strumentato del progetto attivo.

**Vale la pena rubare come pattern, non come dominio**:

- La cartella `.contex/`: sette file numerati per mantenere coerenza tra sessioni di
  lavoro assistite da AI. **Adottato** — vedi `../.contex/`.
- La configurazione di security scanning (Gitleaks per i secret, Semgrep per il SAST) —
  Checkov e tfsec sono scanner IaC e non si applicano a Iris finché non esiste
  Terraform/Docker nel repo.
- Il modulo `Identifier` (auth/RBAC/multi-tenant, impersonation con audit trail, MFA
  TOTP + OIDC) come secondo parere di design sull'AAA — non da portare, da confrontare.

**Non serve**: nessun modulo Applications/Deployments/Actions/infrastruttura-server da
portare — i moduli presenti (streamer, notifier, graph-realtime, calculator,
condominium-billing) sono verticali IoT o scaffold, non legati al dominio di Iris. Il
codice prodotto stesso è di un prodotto diverso, non va mescolato con Iris.
