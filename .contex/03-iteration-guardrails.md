# Gate di iterazione

Da applicare a ogni iterazione che tocca codice.

## Pattern gate

- Layering rispettato: nessun riferimento a `EntityFrameworkCore`/`Microsoft.AspNetCore`
  dentro `Iris.Domain`; nessuna regola di business in `Iris.Api` (solo mapping
  request→command e `RequireAuthorization`).
- Ogni comando di scrittura ha un `Permissions.<Area>.<Azione>` esistente o
  deliberatamente aggiunto (con seed dei ruoli aggiornato), mai riusato per comodità.
- Nuovo aggregato → nuova `IEntityTypeConfiguration`, nuovo repository dietro
  un'interfaccia in `Iris.Application/Abstractions`, registrazione in
  `Iris.Infrastructure/DependencyInjection.cs`.
- Nuova pagina/flusso client → segue `docs/ui-standards.md` senza eccezioni (finestre
  modali per i flussi secondari, token colore/tipografia, pattern Button-over-Label nel
  flyout, stato busy/errore per-azione).

## Test gate

| Cambiamento | Test richiesti |
| --- | --- |
| Domain (nuova entità/invariante) | Unit test positivi/negativi in `tests/Iris.Domain.Tests`. |
| Application (nuovo handler) | Unit test con fake repository in `tests/Iris.Application.Tests` (pattern `FakeAccessData.cs`/`FakeStore`). |
| Api (nuovo endpoint) | Integration test via `IrisApiFactory` in `tests/Iris.Api.Tests` — almeno un caso 2xx e un caso 403/404/409 pertinente. |
| Migrazione EF | Verificare che la migrazione generata sia identica in forma (colonne/tipi/indici) tra SQLite e Postgres. |
| Client MAUI | Nessuna suite automatica oggi — verifica manuale end-to-end nell'app in esecuzione prima di considerare chiuso un flusso UI. |

Se un test viene rimandato, dirlo esplicitamente nel riepilogo di chiusura, non ometterlo.

## Evidence gate

Ogni chiusura di iterazione dovrebbe riportare:

- file toccati (nuovi/modificati), raggruppati per progetto;
- comandi di build/test eseguiti e il loro esito reale (non assunto);
- se sono state generate migrazioni, per quali provider;
- rischi residui o scope volutamente lasciato fuori;
- verifica manuale fatta (se il cambiamento tocca il client MAUI: build client +
  navigazione reale nell'app, non solo compilazione).

## Comandi di riferimento

```bash
dotnet build Iris.sln -c Debug
dotnet test Iris.sln -c Debug
dotnet build src/Iris.App/Iris.App.csproj -c Debug

# migrazione SQLite
dotnet ef migrations add <Nome> --project src/Iris.Infrastructure --startup-project src/Iris.Infrastructure --output-dir Persistence/Migrations

# migrazione Postgres (bash)
IRIS_MIGRATIONS_PROVIDER=Postgres dotnet ef migrations add <Nome> --project src/Iris.Migrations.Postgres --startup-project src/Iris.Api --output-dir Migrations
```

## Anti-allucinazione

- Prima di modificare codice, leggere i file locali coinvolti — non assumere la forma di
  un handler/entità/pagina esistente per analogia.
- Non dichiarare test eseguiti/passati se non lo sono stati davvero.
- Non confondere un modulo "pianificato in `02-operational-plan.md`" con uno
  implementato — verificare sempre con `Grep`/`Glob` prima di dare per scontato che
  qualcosa esista.
- Se un'informazione su Iris_v2/Iris_v3 è incerta, verificarla di persona in quelle
  cartelle piuttosto che fidarsi solo di questo pack.
