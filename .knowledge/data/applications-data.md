---
type: data-model
name: applications-data
title: Applications catalog — persisted data
description: >-
  EF Core entities and migration history for the application catalog
  (ApplicationDefinition, ApplicationVersion and its configuration-knowledge
  children). Dual-provider: SQLite (dev) and PostgreSQL (prod), one migration
  per provider, kept in sync by convention.
status: active

source:
  repository: iris
  paths:
    - src/Iris.Domain/Applications
    - src/Iris.Infrastructure/Persistence/Configurations/ApplicationDefinitionConfiguration.cs
    - src/Iris.Infrastructure/Persistence/Configurations/ApplicationVersionConfiguration.cs
    - src/Iris.Infrastructure/Persistence/Configurations/ApplicationUnitDefinitionConfiguration.cs
    - src/Iris.Infrastructure/Persistence/Repositories/ApplicationRepository.cs
    - src/Iris.Infrastructure/Persistence/Migrations
    - src/Iris.Migrations.Postgres/Migrations
  commit: 7e011bf0dd7c5a152a612554ef523e6081923c50
  generated_at: "2026-09-07T11:54:15Z"

ownership:
  team: ""
  maintainers:
    - Gabriele Angeli

classification:
  domain: applications
  criticality: high
  lifecycle: production
  tags:
    - ef-core
    - sqlite
    - postgresql

provenance:
  method: static-analysis
  confidence: verified
  reviewed_by: ""
  reviewed_at: ""

relations:
  - type: belongs_to
    target: ../domains/applications.md
    evidence:
      - src/Iris.Infrastructure/Persistence/Repositories/ApplicationRepository.cs
---

<!-- BEGIN GENERATED SECTION -->

## Aggregate

`ApplicationDefinition` (aggregate root, `src/Iris.Domain/Applications/ApplicationDefinition.cs`)
→ child `ApplicationVersion` (`ApplicationVersion.cs`), which owns (replace-whole
on import, not accumulated):

- `ConfigurationKey[]` (`ConfigurationKey.cs`)
- `DependencyDefinition[]` (`DependencyDefinition.cs`)
- `PlaceholderDefinition[]` (`PlaceholderDefinition.cs`)
- `ApplicationUnitDefinition[]` (`ApplicationUnitDefinition.cs`)
- `InstallationProfileDefinition[]` (`InstallationProfileDefinition.cs`)
- `DependencyConstraintDefinition[]` (`DependencyConstraintDefinition.cs`)
- owned type `RuntimeMetadata` (reuses `Iris.Domain.Infrastructure.ServerOs`)
- `RawImportPackageJson` — the raw imported package kept for audit.

## Migration history (catalog-relevant only)

Each entry exists once per provider: `src/Iris.Infrastructure/Persistence/Migrations`
(SQLite) and `src/Iris.Migrations.Postgres/Migrations` (PostgreSQL). Verified by
listing both directories — names match 1:1 except one PostgreSQL migration is
named `PersistApplicationManifestSemanticsEf` where SQLite has
`PersistApplicationManifestSemantics` (cosmetic naming drift, not a schema
divergence — not independently verified column-by-column in this pass).

| Order | Migration | Adds |
|---|---|---|
| 1 | `AddApplications` (2026-09-01) | `ApplicationDefinition`, `ApplicationVersion`, `ConfigurationKey`, `DependencyDefinition`, `PlaceholderDefinition` tables |
| 2 | `PersistApplicationManifestSemantics(Ef)` (2026-09-04) | `ApplicationUnitDefinition`, `InstallationProfileDefinition`, `DependencyConstraintDefinition` tables + manifest 1.1 metadata columns |

**Out of scope for this domain but sharing the same C# namespace / migration
folder** (belongs to Deployments — see `domains/applications.md` Boundaries):
`AddApplicationInstallations`, `AddInstallationRuns`,
`AddApplicationInstallationCustomerContext`, `AddEnvironmentServerAssignments`.

## Repository

`ApplicationRepository` (`src/Iris.Infrastructure/Persistence/Repositories/ApplicationRepository.cs`)
behind `IApplicationRepository` (`src/Iris.Application/Abstractions`, not
independently re-verified path in this pass — see `.contex/04-source-map.md`).
Known historical bug (fixed): `GetAllAsync` originally didn't `Include` version
child collections, so catalog counts were silently always zero — fixed by
adding the same `Include(...).ThenInclude(...)` used by `GetAsync` (
`.contex/07-session-log.md`, 2026-09-01 entry). Documented here as a cautionary
example for anyone re-touching this repository's query methods, not as an open
issue — it is fixed.

<!-- END GENERATED SECTION -->

<!-- BEGIN CURATED SECTION -->
_No manually-curated additions yet._
<!-- END CURATED SECTION -->
