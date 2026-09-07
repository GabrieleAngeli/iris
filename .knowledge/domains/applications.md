---
type: domain
name: applications
title: Applications (catalog)
description: >-
  Application catalog: inventory, versions, and the configuration knowledge
  (keys, dependencies, placeholders) imported from a build-time manifest or
  the Iris Extractor tool.
status: active

source:
  repository: iris
  paths:
    - src/Iris.Domain/Applications
    - src/Iris.Application/Applications
    - src/Iris.Contracts/Applications
    - src/Iris.Api/Endpoints/ApplicationsEndpoints.cs
    - src/Iris.Infrastructure/Persistence/Configurations/ApplicationDefinitionConfiguration.cs
    - src/Iris.Infrastructure/Persistence/Configurations/ApplicationVersionConfiguration.cs
    - src/Iris.Infrastructure/Persistence/Configurations/ApplicationUnitDefinitionConfiguration.cs
    - src/Iris.Infrastructure/Persistence/Repositories/ApplicationRepository.cs
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
    - catalog
    - configuration-knowledge
    - manifest

provenance:
  method: static-analysis
  confidence: verified
  reviewed_by: ""
  reviewed_at: ""

relations:
  - type: contains
    target: ../apis/applications-catalog-endpoints.md
    evidence:
      - src/Iris.Api/Endpoints/ApplicationsEndpoints.cs
  - type: owns_data
    target: ../data/applications-data.md
    evidence:
      - src/Iris.Infrastructure/Persistence/Configurations/ApplicationDefinitionConfiguration.cs
      - src/Iris.Infrastructure/Persistence/Configurations/ApplicationVersionConfiguration.cs
  - type: consumes
    target: ../schemas/iris-package-manifest.md
    evidence:
      - src/Iris.Application/Applications/ImportConfigurationPackage.cs
      - src/Iris.Contracts/Applications/ApplicationRequests.cs
  - type: documented_by
    target: ../decisions/adr-0001-applications-domain-origin.md
    evidence:
      - docs/analisi-iris-v2-v3.md
---

<!-- BEGIN GENERATED SECTION -->
<!-- Facts below are extracted deterministically from the paths in `source.paths`
     at `source.commit`. Regenerating this section replaces it wholesale; do not
     hand-edit between the markers. -->

## Responsibility

Owns the **application catalog**: what applications exist (`ApplicationDefinition`),
which versions of each exist (`ApplicationVersion`), and — per version — the
*configuration knowledge* extracted from that version's build artifact: which
configuration keys it needs, which other applications/services it depends on,
and which placeholders it exposes for other applications to consume.

_(inferred — synthesised from the verified facts below, not yet human-reviewed)_
This domain deliberately stops at "what a version needs to run correctly
somewhere" — it does not know *where* a version actually runs, or with what
concrete values. That is the responsibility of the (undocumented-in-this-PoC)
Deployments domain, which binds an `ApplicationVersion` to a `ServerNode` +
`CustomerContext` via `ApplicationInstallation`.

## Boundaries

- **In scope**: application identity/inventory, version history, the manifest
  import contract, and the typed configuration-knowledge model (keys,
  dependencies, placeholders, units, installation profiles, dependency
  constraints).
- **Out of scope** (owned elsewhere, not modelled in this PoC pass):
  `ApplicationInstallation` / `ApplicationInstallationBinding` / `InstallationRun`
  live in `src/Iris.Domain/Applications/` too (same C# namespace) but are
  operated by the **Deployments** area — `TransactionLogInterceptor.AreaFor`
  maps them to `"Deployments"`, not `"Applications"`, and their endpoints in
  `ApplicationsEndpoints.cs` require `Deployments.*` permissions, not
  `Applications.*`. Treat that split as the real domain boundary, not the C#
  folder.

## Exposed interfaces

See [`apis/applications-catalog-endpoints.md`](../apis/applications-catalog-endpoints.md)
for the full list. Summary: `GET/POST /applications`, `PUT /applications/{id}`,
`POST /{id}/versions`, `GET /{id}/versions/{versionId}`,
`POST /{id}/versions/{versionId}/import`.

## Inbound dependencies (who calls into this domain)

- **`Iris.Extractor`** (`src/Iris.Extractor/IrisUploadClient.cs`) — a standalone
  `dotnet tool` that scans a .NET source tree and, optionally, uploads the
  resulting package straight to `POST /{id}/versions/{versionId}/import` using
  a bearer token with `applications.import`. See
  [`modules/iris-extractor.md`](../modules/iris-extractor.md).
- The **Iris.App MAUI client** (`ApplicationsPage`/`ApplicationsViewModel`,
  not yet modelled in this PoC) calls the catalog endpoints for inventory,
  and separately does its own client-side manifest validation before calling
  import (`.contex/00-current-state.md`, section "Applications").

## Outbound dependencies

- `Iris.Domain.Infrastructure.ServerOs` — `RuntimeMetadata` (owned type on
  `ApplicationVersion`) reuses this enum rather than duplicating it
  (`src/Iris.Domain/Applications/RuntimeMetadata.cs`).
- `Iris.Domain.Access.Permissions` — every write/import endpoint requires a
  dedicated permission (`Applications.Read`/`Write`/`ImportKnowledge`,
  confirmed in `src/Iris.Domain/Access/Permissions.cs:65`).

## Data owned

See [`data/applications-data.md`](../data/applications-data.md).

## Configuration

No `Iris:*` configuration keys are specific to this domain — it has no
external integration of its own (unlike Infrastructure's `Iris:Integrations:*`).

## Deployment

Part of `Iris.Api` (single deployable, no separate service). No dedicated
Docker/K8s manifest exists for Iris at all (verified absent repo-wide).

## Observability

Every create/update on `ApplicationDefinition`/`ApplicationVersion` is captured
automatically by `TransactionLogInterceptor` under area `"Applications"`
(imports and installations instead fall under `"Deployments"` — see Boundaries
above). No domain-specific metrics/health checks beyond the process-wide
`GET /health`.

## Known failure modes / edge cases

- `ImportConfigurationPackage` is **replace-whole**: a re-import discards the
  version's previous configuration keys/dependencies/placeholders/units/
  profiles/constraints rather than merging (`.contex/00-current-state.md`,
  confirmed by `ApplicationVersion.ApplyImport` in
  `src/Iris.Domain/Applications/ApplicationVersion.cs`).
- `ConfigurationKeyInput.DefaultValue` is `string?` in the current contract —
  numeric/boolean defaults from a manifest must be stringified before import
  (hit for real during the AugeG4 GrpcFlow assimilation, `.contex/07-session-log.md`,
  2026-09-03 entry).

## Constraints / invariants

- Application `slug` is immutable after creation; `PUT /applications/{id}`
  updates everything except the slug (`ApplicationsEndpoints.cs:51-76`).
- Adding a duplicate version name to the same application is guarded
  case-insensitively in `ApplicationDefinition.AddVersion`
  (`.contex/01-decisions.md`, Layering section).

## Tests

- `tests/Iris.Application.Tests/Applications/ApplicationsHandlersTests.cs`
- `tests/Iris.Api.Tests/ApplicationsApiTests.cs`
- `tests/Iris.Extractor.Tests/` (extractor side of the contract, not this
  domain's handlers, but shares the manifest schema)

## Runbooks

- [`docs/application-assimilation.md`](../../docs/application-assimilation.md) —
  the operational guide for bringing an application into the catalog, per
  technology (only the `.NET` section describes a tool that actually exists
  today; the rest is guidance for manual manifest authoring).

## Architectural decisions

- [`decisions/adr-0001-applications-domain-origin.md`](../decisions/adr-0001-applications-domain-origin.md)

## Known technical debt

- `docs/application-configuration-model-analysis.md` describes a more advanced
  configuration-compiler model (typed values, master/slave profiles, version
  compatibility constraints) than what is fully wired end-to-end today; treat
  that document as `inferred`/planned, not as a description of shipped
  behaviour, until cross-checked against `.contex/00-current-state.md`.

<!-- END GENERATED SECTION -->

<!-- BEGIN CURATED SECTION -->
_No manually-curated additions yet. A human reviewer should replace this note
once the generated section above has been checked against the code at
`source.commit` and either confirmed or corrected._
<!-- END CURATED SECTION -->

## Referenced by (derived inverse relations — see index.md)

- `apis/applications-catalog-endpoints.md` — `belongs_to` this domain.
- `data/applications-data.md` — `belongs_to` this domain.
- `modules/iris-extractor.md` — `calls` this domain's import endpoint (declared
  as `called_by` here because the canonical `calls` edge is declared on the
  extractor side).
