# Iris — Infrastructure Control Plane

Application-aware Infrastructure Control Plane for centralized management of
infrastructure, application configuration and deployment workflows across
heterogeneous, multi-customer environments.

> **Iris defines, understands, validates and orchestrates. External tools execute.**

See [`.context/iris_icp_project_context_for_llm.md`](.context/iris_icp_project_context_for_llm.md)
for the full project brief.

## Solution layout

```
Iris.sln
├── src/
│   ├── Iris.Domain          # Pure domain model — no EF Core / HTTP / AWX / OpenBao
│   ├── Iris.Contracts       # DTOs / transport contracts shared with API clients
│   ├── Iris.Application      # Use cases, ports, validation workflow (refs Domain, Contracts)
│   ├── Iris.Infrastructure  # EF Core, AWX, OpenBao, Ansible, Grafana adapters (refs Application)
│   ├── Iris.Migrations.Postgres # PostgreSQL-specific EF Core migrations
│   ├── Iris.Api             # HTTP adapter + composition root (refs Application, Infrastructure, Contracts)
│   └── Iris.App             # .NET MAUI desktop client (refs Contracts) — separate Iris.App.sln
└── tests/
    ├── Iris.Domain.Tests
    ├── Iris.Application.Tests
    └── Iris.Api.Tests        # WebApplicationFactory integration tests
```

`Iris.sln` is the backend + tests and builds with just the .NET SDK.
`Iris.App.sln` adds the MAUI client and needs the MAUI workload (see below).

Dependency direction: `Api → Infrastructure → Application → Domain`.
`Contracts` is referenced by `Application`, `Api` and the `Iris.App` client.

Shared build configuration lives in `Directory.Build.props`; NuGet versions are
pinned centrally in `Directory.Packages.props`. `nuget.config` restricts restore
to nuget.org. The EF Core CLI is a local tool (`dotnet tool restore`).

## Prerequisites

- .NET SDK 9.0.310+ (pinned in `global.json`)

## Common commands

```bash
dotnet restore Iris.sln
dotnet tool restore                 # dotnet-ef
dotnet build Iris.sln -c Release
dotnet test Iris.sln -c Release
dotnet run --project src/Iris.Api
```

## Access model (AAA)

Authorization is *capillary*: a **RoleAssignment** binds a **User** to a **Role**
(a bundle of fine-grained `area.action` permissions) at an **AccessScope** —
`Global`, a `Customer`, or a single `Context`. A global scope covers everything;
a customer scope covers that customer and all its contexts; a context scope
covers only itself. `platform.admin` implies every permission at every scope.

Effective permissions for a request = union of the permissions from every
assignment whose scope covers the request's target scope.

### Persistence

EF Core with **SQLite** for local dev (default) and **PostgreSQL** for production,
selected by `Iris:Database:Provider` (`Sqlite` | `Postgres`) with connection
string `ConnectionStrings:IrisDb`. On startup (when `Iris:Database:MigrateOnStartup`
is true) the API applies migrations and seeds built-in roles plus a demo tenancy
(`contoso`, `globex`).

Migrations are provider-specific and live in separate assemblies:

```bash
# SQLite (default) — migrations in src/Iris.Infrastructure/Persistence/Migrations
dotnet ef migrations add <Name> \
  --project src/Iris.Infrastructure --startup-project src/Iris.Infrastructure \
  --output-dir Persistence/Migrations

# PostgreSQL — migrations in src/Iris.Migrations.Postgres/Migrations
IRIS_MIGRATIONS_PROVIDER=Postgres dotnet ef migrations add <Name> \
  --project src/Iris.Migrations.Postgres --startup-project src/Iris.Api \
  --output-dir Migrations
```

At runtime `AddIrisInfrastructure` picks the matching migrations assembly from
`Iris:Database:Provider`.

### Authentication

`Iris:Auth:Mode` = `Dev` (default in Development) | `EntraId` | `Both`.

- **Dev** — send `X-Dev-User: <email>` matching an entry in `Iris:Auth:DevUsers`
  (see `appsettings.Development.json`). No tenant required.
- **EntraId** — Microsoft Entra ID bearer tokens via `Microsoft.Identity.Web`,
  configured under `AzureAd` (`TenantId`, `ClientId`, `Audience`).
- **Both** — the dev header wins when present, otherwise a bearer token.

Users are provisioned just-in-time on first authenticated request.

The desktop client (`Iris.App`) supports both: username/email dev sign-in,
and a **Continue with single sign-on** button that signs in against the
vendor's Microsoft 365 tenant via MSAL.NET (Windows WAM broker). See
[`docs/entra-id-setup.md`](docs/entra-id-setup.md) for creating the Entra ID
App Registrations this requires and wiring up the resulting values.

Seeded dev identities: `admin@iris.local` (platform-admin / Global),
`lucia@contoso.example` (customer-admin / Contoso), `marco@contoso.example`
(operator / Contoso·Production), `sara@iris.local` (auditor / Global),
`gio@globex.example` (reader / Globex).

## API endpoints

| Method | Route                     | Auth                              | Purpose                                                     |
|--------|---------------------------|-----------------------------------|------------------------------------------------------------|
| GET    | `/`                       | anonymous                         | Service identity (name, version, env)                     |
| GET    | `/health`                 | anonymous                         | Liveness probe                                            |
| GET    | `/openapi/v1.json`        | anonymous (Development only)      | OpenAPI document                                          |
| GET    | `/me`                     | authenticated                     | Identity + effective permissions; `?customerId=&contextId=` to evaluate at a scope |
| GET    | `/customers`              | authenticated                     | Customers and contexts visible to the caller             |
| POST   | `/customers`              | `governance.customers.manage`     | Register a customer                                       |
| POST   | `/customers/{id}/contexts`| `governance.customers.manage`     | Add an environment/context to a customer                  |
| GET    | `/governance/roles`       | `governance.roles.manage`         | Role catalog                                             |
| GET    | `/governance/permissions` | authenticated                     | Every permission code Iris recognises                    |
| GET    | `/governance/users`       | `governance.read`                 | Users and the roles they hold                            |
| POST   | `/governance/users/{userId}/assignments` | `governance.assignments.manage` | Grant a role to a user at a scope          |
| DELETE | `/governance/users/{userId}/assignments/{assignmentId}` | `governance.assignments.manage` | Revoke a role assignment |

Application errors map to RFC 7807 problem responses: 404 (unknown resource),
409 (duplicate key / existing assignment), 400 (invalid scope or payload).

`src/Iris.Api/Iris.Api.http` has ready-made requests for all of these.

## Desktop client (Iris.App)

`src/Iris.App` is the .NET MAUI operator client (Windows / WinUI 3), ported from
`Project Reference/Demo UI`. It signs in against the API with a dev-header
identity and its **Access** page shows `/me` + `/customers` live.

Requires the MAUI workload (one-time, **elevated shell**):

```powershell
dotnet workload install maui        # or: maui-windows
```

Then, with the API running:

```powershell
dotnet build Iris.App.sln
dotnet build src/Iris.App/Iris.App.csproj -t:Run -f net9.0-windows10.0.19041.0
```

In VS Code:

- **Debug both** — run the **Iris (API + App)** compound launch config; it starts
  the API and the client together (VS Code has no dedicated MAUI debug type for
  Windows heads, so the client runs as the unpackaged `Iris.App.exe` under the
  .NET debugger). The client shows a connection error until the API is up — retry
  the login.
- **Run without debugging** — the **run-app** task builds and launches the client
  and first starts the API in the background (`run-api`), waiting until it prints
  *Now listening on*.
- **Client only** — the **Iris.App (Windows)** launch config (assumes the API is
  already running).

In Visual Studio: open `Iris.App.sln` with the ".NET MAUI" workload and press F5.

## License

MIT — Copyright © 2026 Gabriele Angeli. See [LICENSE.md](LICENSE.md),
[CONTRIBUTING.md](CONTRIBUTING.md) and [CLA.md](CLA.md).
