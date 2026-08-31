# Iris — Infrastructure Control Plane

## Descrizione del progetto per una LLM

**Iris ICP** è un progetto open source per realizzare un **Infrastructure Control Plane application-aware** destinato alla gestione centralizzata di infrastrutture, configurazioni applicative e processi di deployment in ambienti eterogenei e multi-cliente.

L'obiettivo non è sostituire Ansible, AWX, OpenBao, Grafana o le pipeline CI/CD, ma fornire il **control plane centrale** che conosce lo stato desiderato dell'infrastruttura e delle applicazioni, valida le configurazioni e orchestra questi strumenti.

La filosofia fondamentale è:

> **Iris defines, understands, validates and orchestrates. External tools execute.**

## Problema che Iris vuole risolvere

L'ambiente target comprende numerosi clienti e differenti topologie: server condivisi o dedicati, Windows e Linux, applicazioni C#, Java, JavaScript/Node e Docker, configurazioni distribuite tra `appsettings.json`, `web.config`, `application.properties`, environment variables e Docker.

Ansible/AWX permettono di eseguire operazioni, ma manca un livello superiore che sappia **cosa deve essere installato, dove, per quale cliente, con quale versione, con quali configurazioni e dipendenze e se il deployment sia valido**.

## Workflow operativo

Il workflow dell'operatore è il centro della UX:

```text
Infrastructure
    ↓
Applications
    ↓
Configuration Knowledge Import
    ↓
Deployment Association
    ↓
Placeholder / Domain Binding
    ↓
Validation
    ↓
Action Preparation
    ↓
AWX / Ansible / OpenBao
    ↓
Monitoring + Audit
```

### 1. Infrastructure

L'operatore registra server (`ServerNode`) descrivendo:
- nome, hostname/FQDN e IP;
- ambiente: Test, Staging, Production;
- OS: Linux o Windows;
- capability: Load Balancer, Database, Service/Application Host, Presentation/Web Layer;
- CPU, RAM e disco;
- porte ed endpoint.

Le capability partecipano alle regole di validazione.

### 2. Clienti e contesti

Iris è multi-cliente. Un `Customer` possiede uno o più contesti/ambienti. Gli utenti devono poter vedere e amministrare soltanto clienti e contesti autorizzati.

L'autenticazione futura è prevista tramite AD/OIDC/policy di dominio; nella prima demo può essere simulata.

### 3. Application Catalog

Le applicazioni registrate possono essere C#, Java, JavaScript, Node o Docker e possiedono repository e versioni.

Iris **non richiede un manifest Iris statico mantenuto manualmente nel repository applicativo**.

## Iris Extractor

L'integrazione applicativa avviene attraverso la pipeline:

```text
SOURCE → TEST → BUILD → IRIS EXTRACTOR → IRIS EXPORT PACKAGE → IRIS
```

L'Extractor analizza artefatti come:
- `appsettings.json`;
- strongly typed .NET options;
- `web.config`;
- `.env`;
- Docker Compose;
- `application.properties` / `application.yml`;
- metadata Java e altre sorgenti supportate.

Estrae **configuration knowledge**, non i valori specifici dell'ambiente.

Per ogni configurazione può ricavare:
- key;
- type;
- default;
- required/optional;
- secret/non-secret;
- description/purpose;
- source;
- runtime target;
- dependency;
- domain binding suggerito;
- confidence/review status.

## Domain Placeholders

Iris separa il requisito applicativo dal valore infrastrutturale.

Esempio:

```text
ConnectionStrings:MainDb
        ↓
${domain.db.main.connectionString}
```

Altri esempi:

```text
${domain.service.redis.endpoint}
${domain.auth.telemetry.clientId}
${domain.auth.telemetry.clientSecret}
${domain.identity.authority.url}
```

Il valore viene risolto in funzione di customer, context, server e deployment.

## Deployment Association

Un deployment associa:

```text
Customer + Context + Application + Version + Target Server(s)
```

Iris costruisce quindi la configurazione effettiva combinando:

```text
Application Configuration Knowledge
+ Customer Configuration
+ Context Configuration
+ Server Configuration
+ Domain Bindings
+ OpenBao Secret References
= Effective Configuration
```

## Validation Engine

Prima del deployment Iris controlla almeno:
- porte occupate;
- OS incompatibile;
- capability mancanti;
- placeholder obbligatori irrisolti;
- dipendenze mancanti;
- secret non configurati;
- CPU/RAM insufficienti;
- collisioni tra servizi.

Iris deve spiegare il problema e, quando possibile, suggerire una soluzione.

## Runtime Configuration Generation

Il modello logico può essere materializzato come:

```text
appsettings.json
web.config
application.properties
application.yml
.env
Docker environment
Ansible host_vars
Ansible group_vars
```

## Action Preparation

Dopo la validazione:

```text
Deployment
    ↓
Prepare Actions
    ├── Generate Ansible Inventory
    ├── Generate Ansible Vars
    ├── Prepare AWX Job
    └── Prepare OpenBao Secret Plan
```

Iris decide, valida e prepara; **AWX/Ansible eseguono**.

## OpenBao

I secret reali non devono essere memorizzati nel database Iris. Iris conserva riferimenti logici, mentre password, client secret, API key, certificati e altre credenziali rimangono in OpenBao.

## Communication Matrix

Conoscendo applicazioni, server, dipendenze, porte e protocolli, Iris può generare una COM Matrix per firewall e networking.

| Source | Destination | Protocol | Port | Reason |
|---|---|---|---:|---|
| Telemetry.Api | PostgreSQL | TCP | 5432 | Main database |
| Telemetry.Api | Redis | TCP | 6379 | Cache |
| Web | Telemetry.Api | HTTPS | 443 | REST API |

## Monitoring, Audit e History

Le azioni possono essere `Prepared`, `Pending`, `Running`, `Completed` o `Failed`.

Iris registra utente, timestamp, deployment, AWX Job ID, durata, stato, output, errori e artifact generati.

Configurazioni, binding, versioni, deployment, azioni e modifiche infrastrutturali devono essere storicizzati per audit, diff e futuri rollback logici.

## Grafana e Capacity Advisory

Grafana è previsto come fonte di metriche per un futuro advisory engine. Iris potrà utilizzare CPU, RAM e altre metriche per segnalare problemi di capacità e suggerire target alternativi per i deployment.

# Architettura software

Backend C#/.NET con **Hexagonal Architecture pragmatica**:

```text
src/
├── Iris.Domain
├── Iris.Application
├── Iris.Infrastructure
├── Iris.Api
└── Iris.Contracts
```

`Iris.Domain` contiene il modello puro e non dipende da EF Core, HTTP, AWX o OpenBao.

`Iris.Application` contiene use case, command/query, port/interface, orchestrazione e validation workflow.

`Iris.Infrastructure` contiene adapter EF Core, SQLite/PostgreSQL, AWX, OpenBao, generatori Ansible e futuro adapter Grafana.

`Iris.Api` è l'adapter HTTP e composition root.

# Frontend

Il frontend è Net Maui. Esiste già un progetto nella directory:

```text
Project References
```

Deve essere analizzato e riutilizzato, non sostituito da un nuovo frontend salvo impossibilità tecnica reale.

Navigazione prevista:

```text
Overview
Infrastructure
Applications
Deployments
Actions
Governance
```

La UX deve essere **workflow-first**, non un semplice backoffice CRUD.

# Prima demo

Possono essere mockati:
- AWX;
- OpenBao;
- Grafana;
- AD/OIDC;
- CI/CD publishing.

Devono invece essere navigabili:
- Infrastructure;
- Applications;
- import metadata;
- Configuration Knowledge;
- placeholder binding;
- Deployment;
- validation;
- Action preparation;
- Action monitoring.

I dati demo devono includere problemi intenzionali: porta occupata, placeholder irrisolto, capability incompatibile e capacità macchina insufficiente.

# Licensing e ownership

Iris è previsto come progetto open source MIT, con copyright dell'autore originale e governance vendor-led/maintainer-led.

```text
Copyright © 2026 Gabriele Angeli
MIT License
```

Le versioni pubblicate MIT rimangono MIT. Per mantenere la possibilità di relicensing delle versioni future, il modello contributivo previsto comprende CLA, controllo centrale della governance e separazione del trademark/branding dalla licenza del codice.

# Principio architetturale complessivo

> **Application knowledge + Infrastructure knowledge + Customer context + Policy = Deployment intent**

> **Deployment intent + Validation + External execution engines = Controlled deployment**

```text
SOURCE CODE
     ↓
BUILD-TIME KNOWLEDGE EXTRACTION
     ↓
APPLICATION CATALOG
     +
INFRASTRUCTURE MODEL
     +
CUSTOMER / CONTEXT
     +
SECRETS / DOMAIN BINDINGS
     ↓
DEPLOYMENT INTENT
     ↓
VALIDATION ENGINE
     ↓
ACTION PLAN
     ↓
ANSIBLE / AWX / OPENBAO
     ↓
AUDIT + MONITORING
```

# Cosa rende Iris diverso

La caratteristica centrale non è la semplice gestione dei server, ma il collegamento:

**codice applicativo → conoscenza della configurazione → infrastruttura → cliente → deployment.**

Iris deve sapere non soltanto che `ConnectionStrings:MainDb` possiede un certo valore, ma che rappresenta il database principale richiesto da una specifica versione dell'applicazione e che, per un determinato cliente e ambiente, deve essere risolto verso una precisa risorsa infrastrutturale, con credenziali custodite in OpenBao.

Questa direzione deve essere preservata nelle successive decisioni di design e implementazione.


Primo step implementazione AAA, con ruoli capillari e SSO appoggiata a Microsoft 365
