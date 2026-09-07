---
type: schema
name: iris-package-manifest
title: iris-package.json manifest contract
description: >-
  The configuration-knowledge package contract produced by Iris Extractor (or
  authored by hand) and accepted by the Applications import endpoint. Not a
  formal JSON Schema file yet — reconstructed here from the C# request DTOs
  plus one real demo instance.
status: active

source:
  repository: iris
  paths:
    - src/Iris.Contracts/Applications/ApplicationRequests.cs
    - docs/application-assimilation.md
    - docs/manifests/augeg4-engine.demo.iris-package.json
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
    - contract
    - manifest

provenance:
  method: static-analysis
  confidence: verified
  reviewed_by: ""
  reviewed_at: ""

relations: []
# No outbound relations declared here by design (see index.md "Relation
# direction"): this schema is the target of `consumes` (domains/applications.md,
# apis/applications-catalog-endpoints.md) and of `publishes` (modules/iris-extractor.md).
---

<!-- BEGIN GENERATED SECTION -->

## Shape (top level, from a real instance)

`docs/manifests/augeg4-engine.demo.iris-package.json`: `schemaVersion` (seen:
`"1.1"`), `releaseVersion`, `sourceReference`, `application {slug, name,
runtimeType, artifact{provider, feed, name, path}}`, `runtime {framework,
executionTargets[], osSupport[], minimumResources{cpuCores, memoryMb}, ...}`,
plus the arrays consumed by the import endpoint (below).

**No JSON Schema file exists for this contract** — this page and the C# DTOs
are the closest thing to one. Producing an actual `.json` schema file under
this same directory is a natural next PoC increment (would let the demo
manifest and future extractor output be validated mechanically instead of by
convention).

## What the import endpoint actually accepts

`ImportConfigurationPackageRequest` (`src/Iris.Contracts/Applications/ApplicationRequests.cs:115-123`) —
this is the **authoritative, verified** current contract, narrower than what
the manifest document above describes as intent:

- `SchemaVersion: string`
- `ConfigurationKeys: ConfigurationKeyInput[]` — `Key`, `TargetKind`,
  `Required`, `Secret`, `DefaultValue: string?` (⚠ always a string — numeric/
  boolean manifest values must be stringified before import, see
  `domains/applications.md` "Known failure modes"), `Description`, `Purpose`,
  `PlaceholderKey`, plus manifest-1.1 metadata: `ValueType`, `ItemType`,
  `Scope`, `SerializationJson`, `ResolutionJson`, `ProfilesJson`,
  `ProfileDefaultsJson`, `ItemSchemaJson`.
- `Dependencies: DependencyInput[]` — `Name`, `Category`, `Required`,
  `Description`, `PlaceholderKey`, and the application-to-application link:
  `ProviderApplicationSlug` + `ProviderPlaceholderKey`.
- `Placeholders: PlaceholderInput[]` — `Key`, `Category`, `Description`,
  `Required`.
- `Warnings: string[]?`
- `ApplicationUnits: ApplicationUnitInput[]?` — multiple launchables from the
  same artifact (`Key`, `DisplayName`, `Kind`, `EntryPoint`, `ArtifactPath`,
  `ExecutionTargets[]`, `Profiles[]`).
- `InstallationProfiles: InstallationProfileInput[]?` — e.g. master/slave
  (`Key`, `DisplayName`, `Required`, `Multiple`, `ConfigurationKeys[]`).
- `DependencyConstraints: DependencyConstraintInput[]?` — version-compatibility
  expressions (`PlaceholderKey`, `ServiceKind`, `VersionExpression`,
  `DetailsJson`).

## Produced by / consumed by (see index.md for why this isn't in `relations:`)

- **Produced by**: `Iris.Extractor` (`.NET` stack only today — see
  `modules/iris-extractor.md`), or authored by hand per
  `docs/application-assimilation.md`.
- **Consumed by**: `POST /applications/{id}/versions/{versionId}/import`
  (`apis/applications-catalog-endpoints.md`), which maps it into the
  `applications-data.md` model.

<!-- END GENERATED SECTION -->

<!-- BEGIN CURATED SECTION -->
_No manually-curated additions yet._
<!-- END CURATED SECTION -->
