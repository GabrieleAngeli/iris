# Prossime azioni

Ordinate per priorità. Aggiornare questa lista a ogni chiusura di iterazione significativa.

## Priorità correnti

1. ~~**Applications - catalogo + import configuration knowledge**~~ Fatto.
2. ~~**ServerNode - capability, resource hints, porte note**~~ Fatto.
3. ~~**Auth produzione + first-run setup + SMTP + SSO admin claim**~~ Fatto.
4. ~~**Profile / System settings / password recovery / remember me**~~ Fatto.
5. **Deployments - associazione**: introdurre `DeploymentAssociation` con Application +
   Version + Customer + Context + ServerNode target + binding placeholder + stato. Usare FK
   reali verso `Customer`/`CustomerContext`/`ServerNode`/`ApplicationVersion`; non duplicare
   quei concetti.
6. **Validation Engine**: riscrivere le regole di Iris_v2 (`DeploymentService.ValidateInternal`)
   come handler `ValidateDeployment`, producendo una lista tipata di check con severità.
   Regole iniziali: placeholder non risolto, dipendenza non legata, OS incompatibile,
   capability mancante, collisione porte, capacità insufficiente.
7. **Actions - preparazione**: `PreparedAction` (tipo Ansible inventory/vars, AWX draft,
   OpenBao plan: tutti mock), stato (Draft/Prepared/Pending/Running/Completed/Failed),
   endpoint preview/prepare, storico azioni.
8. **Pagina client Applications**: nuova sezione flyout + lista + dettaglio versione/import,
   stesso pattern di `UsersPage`/`ServersPage`, dopo aver deciso quanto del workflow
   backend-first deve essere esposto subito.
9. Non pianificato in dettaglio: Monitoring/Audit reale, Grafana/capacity advisory, COM
   Matrix, generazione runtime config materializzata su disco.

## Stato recente delle sessioni

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
