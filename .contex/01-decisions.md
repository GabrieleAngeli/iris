# Decisioni architetturali

Vincolanti finché non cambiate con un aggiornamento esplicito di questo file.

## Layering

- `Iris.Domain` non dipende da EF Core, HTTP, AWX, OpenBao — mai. Regole/invarianti nel
  costruttore o in metodi comportamentali dell'entità; validazioni cross-campo che
  richiedono parsing di stringhe (es. un `ScopeType`/`Os` arrivato come `string` dalla
  request) vivono nell'**handler** dell'`Iris.Application`, non nel dominio.
- Comandi/query: `record XxxCommand(...)` + `class XxxHandler(porte...)` con
  `HandleAsync`, un file per operazione sotto `Iris.Application/<Area>/`. Registrati in
  `Iris.Application/DependencyInjection.cs` con `TryAddScoped<XxxHandler>()`.
- Aggregati radice: `Entity<Guid>, IAggregateRoot, IAuditableEntity`. Ctor pubblico con
  guardie (`ArgumentException.ThrowIfNullOrWhiteSpace`), ctor privato senza parametri per
  EF, eventuali entità figlie con ctor `internal` create solo dall'aggregato padre.
- ID: sempre `Guid.CreateVersion7()` generato nell'handler, mai dal DB
  (`builder.Property(x => x.Id).ValueGeneratedNever()` in ogni `IEntityTypeConfiguration`).
- Enum di dominio persistiti come stringa (`.HasConversion<string>().HasMaxLength(20)`),
  mai come int — coerenza tra le migrazioni SQLite/Postgres e leggibilità diretta del DB.

## Permessi

- Il catalogo (`Iris.Domain.Access.Permissions`) è piatto: `<area>.<azione>`, aggiunto
  ad `All` e ai ruoli seed pertinenti in `SeedData.cs` quando si introduce un'area nuova
  — non riusare un permesso di un'altra area per comodità (eccezione già presa:
  `infrastructure.secrets.manage` è deliberatamente separato da `infrastructure.write`
  per isolare chi può *rivedere* un segreto già salvato).
- Endpoint minimal-API senza `customerId`/`contextId` in route sono sempre valutati a
  scope **Global** da `PermissionAuthorizationHandler` — è per questo che `/me` non
  scopato risponde già alla domanda "quali sezioni vede questo utente nel client".

## Segreti

- Nessun valore segreto reale (password, chiave SSH, client secret) tocca mai il
  database Iris. Passa per `ISecretStore` (`Iris.Application.Abstractions`), che
  restituisce solo un riferimento logico da persistere. L'adapter attuale
  (`InMemorySecretStore`) è un mock dichiarato — sostituirlo con un adapter OpenBao reale
  senza cambiare la firma del port né i chiamanti.

## Persistenza

- Doppio provider sempre in coppia: ogni migrazione va creata sia in
  `src/Iris.Infrastructure/Persistence/Migrations` (SQLite, progetto/startup
  `Iris.Infrastructure`) sia in `src/Iris.Migrations.Postgres/Migrations` (Postgres,
  `IRIS_MIGRATIONS_PROVIDER=Postgres`, startup `Iris.Api`) — vedi i comandi esatti in
  `03-iteration-guardrails.md`. Non lasciare un provider indietro.
- Seed (`IrisDbSeeder`/`SeedData.cs`) resta idempotente e usa gli stessi costruttori
  pubblici del dominio — mai bypassare le invarianti per comodità di seeding.

## Client MAUI

- Fonte di verità unica per UI/navigazione: `docs/ui-standards.md`. Non introdurre uno
  stile/pattern nuovo senza aggiornarlo.
- Flussi secondari (creazione, modifica, conferma eliminazione) sono **finestre modali
  OS reali** via `IDialogService`, mai pannelli in-page o `Grid`+`TapGestureRecognizer`
  come bottone (nessun pattern Invoke UIA, non raggiungibile da tastiera).
- `Shell.MenuItemTemplate` non va riprovato sull'handler Windows — non lega mai il
  `MenuItem` come `BindingContext` del template, il testo bindato resta vuoto in
  silenzio. Le sezioni del flyout restano in `Shell.FlyoutContentTemplate` fatto a mano.
- Una nuova sezione di navigazione è gated da un `CanManageX` su `AppShellViewModel`,
  calcolato da `IAuthService.Me.EffectivePermissions` e ricalcolato su `StateChanged`.

## Cosa resta fuori per scelta

- Nessun frontend web — solo il client MAUI Windows.
- AWX, OpenBao, Ansible, Grafana restano mockati/non integrati finché non arriva un
  incremento dedicato — non introdurre chiamate HTTP reali verso questi sistemi.
- Capability/CPU/RAM/disco/porte su `ServerNode` non esistono ancora — deliberatamente
  rimandate al momento in cui si costruisce il Validation Engine (vedi
  `02-operational-plan.md`), per non anticipare un modello che potrebbe cambiare forma.
