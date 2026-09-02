# Decisioni architetturali

Vincolanti finché non cambiate con un aggiornamento esplicito di questo file.

## Layering

- `Iris.Domain` non dipende da EF Core, HTTP, AWX, OpenBao: mai. Regole/invarianti nel
  costruttore o in metodi comportamentali dell'entità; validazioni cross-campo che
  richiedono parsing di stringhe dalla request vivono nell'handler di `Iris.Application`,
  non nel dominio.
- Comandi/query: `record XxxCommand(...)` + `class XxxHandler(porte...)` con
  `HandleAsync`, un file per operazione sotto `Iris.Application/<Area>/`. Registrati in
  `Iris.Application/DependencyInjection.cs` con `TryAddScoped<XxxHandler>()`.
- Aggregati radice: `Entity<Guid>, IAggregateRoot, IAuditableEntity`. Ctor pubblico con
  guardie, ctor privato senza parametri per EF, eventuali entità figlie con ctor `internal`
  create solo dall'aggregato padre.
- ID: sempre `Guid.CreateVersion7()` generato nell'handler, mai dal DB
  (`builder.Property(x => x.Id).ValueGeneratedNever()` in ogni `IEntityTypeConfiguration`).
- Enum di dominio persistiti come stringa (`.HasConversion<string>()`), mai come int.
  Per collezioni primitive di enum usare `PrimitiveCollection(...).ElementType(...)`.

## Permessi

- Il catalogo (`Iris.Domain.Access.Permissions`) è piatto: `<area>.<azione>`, aggiunto ad
  `All` e ai ruoli seed pertinenti in `SeedData.cs` quando si introduce un'area nuova.
  Non riusare un permesso di un'altra area per comodità.
- Eccezione deliberata già presa: `infrastructure.secrets.manage` è separato da
  `infrastructure.write` per isolare chi può ruotare/rivedere un segreto già salvato.
- Endpoint minimal-API senza `customerId`/`contextId` in route sono valutati a scope
  Global da `PermissionAuthorizationHandler`; `/me` non scopato risponde alla domanda
  "quali sezioni vede questo utente nel client".

## Segreti

- Nessun valore segreto reale (password, chiave SSH, client secret, password SMTP) tocca
  mai il database Iris. Passa per `ISecretStore`, che restituisce solo un riferimento
  logico da persistere. L'adapter attuale (`InMemorySecretStore`) è un mock dichiarato da
  sostituire con OpenBao senza cambiare la firma del port né i chiamanti.

## Setup e sessioni locali

- Il first-run setup è anonimo ma one-shot: `CompleteSetupHandler` ricontrolla lato server
  che non esista già un assignment al ruolo `platform-admin`, anche se il client ha
  chiamato prima `/setup/status`.
- In produzione il database nasce vuoto dopo le migrazioni; `SeedDemoData` resta attivo
  solo per sviluppo/demo. Non introdurre seed automatici di super-admin fuori dal flusso
  setup.
- I token di sessione Iris sono opachi, non JWT, e sono persistiti solo come hash. La
  selezione dello schema auth resta in `AuthenticationSetup`: dev header se presente, JWT
  Entra ID se il bearer token ha forma JWT, altrimenti sessione Iris.

## Persistenza

- Doppio provider sempre in coppia: ogni migrazione va creata sia in
  `src/Iris.Infrastructure/Persistence/Migrations` (SQLite, progetto/startup
  `Iris.Infrastructure`) sia in `src/Iris.Migrations.Postgres/Migrations` (Postgres,
  `IRIS_MIGRATIONS_PROVIDER=Postgres`, startup `Iris.Api`). Non lasciare un provider
  indietro.
- Seed (`IrisDbSeeder`/`SeedData.cs`) resta idempotente e usa gli stessi costruttori
  pubblici del dominio; non bypassare le invarianti per comodità di seeding.

## Client MAUI

- Fonte di verità unica per UI/navigazione: `docs/ui-standards.md`. Non introdurre uno
  stile/pattern nuovo senza aggiornarlo.
- Flussi secondari (creazione, modifica, conferma eliminazione) sono finestre modali OS
  reali via `IDialogService`, mai pannelli in-page o `Grid`+`TapGestureRecognizer`.
- `Shell.MenuItemTemplate` non va riprovato sull'handler Windows: le sezioni del flyout
  restano in `Shell.FlyoutContentTemplate` fatto a mano.
- Una nuova sezione di navigazione è gated da un `CanManageX` su `AppShellViewModel`,
  calcolato da `IAuthService.Me.EffectivePermissions` e ricalcolato su `StateChanged`.

## Scelte già prese per i prossimi moduli

- Applications esiste già backend-first: non rimodellare `ApplicationDefinition`/
  `ApplicationVersion` da Iris_v2, usare il codice attuale.
- `ServerNode` espone già `NodeCapability`, `ResourceProfile?` e `UsedPorts`. Il
  Validation Engine deve riusarli invece di introdurre un modello parallelo.
- Deployments deve referenziare con FK reali `ApplicationDefinition`/`ApplicationVersion`,
  `Customer`/`CustomerContext` e `ServerNode`; non duplicare quei concetti come record
  locali.

## Cosa resta fuori per scelta

- Nessun frontend web: solo il client MAUI Windows.
- AWX, OpenBao, Ansible, Grafana restano mockati/non integrati finché non arriva un
  incremento dedicato; non introdurre chiamate HTTP reali verso questi sistemi.
