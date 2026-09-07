---
type: module
name: iris-extractor
title: Iris Extractor (dotnet tool)
description: >-
  Standalone CLI (packaged as a dotnet tool, command iris-extractor) that
  statically scans a .NET application's source tree and produces/uploads an
  iris-package.json configuration-knowledge package.
status: draft
# status: draft on purpose — this is a stub created only because the
# Applications PoC needs to document what calls into it. It has not had a
# full pass of its own yet (that would be a "module" PoC in its own right:
# src/Iris.Extractor/DotNet/{AppSettingsScanner,RoslynConfigurationScanner,
# SecretHeuristics,...}.cs deserve the same treatment as domains/applications.md
# but are out of scope for this increment).

source:
  repository: iris
  paths:
    - src/Iris.Extractor
  commit: 7e011bf0dd7c5a152a612554ef523e6081923c50
  generated_at: "2026-09-07T11:54:15Z"

ownership:
  team: ""
  maintainers:
    - Gabriele Angeli

classification:
  domain: applications
  criticality: medium
  lifecycle: production
  tags:
    - cli
    - dotnet-tool
    - roslyn

provenance:
  method: static-analysis
  confidence: inferred
  reviewed_by: ""
  reviewed_at: ""

relations:
  - type: calls
    target: ../apis/applications-catalog-endpoints.md
    evidence:
      - src/Iris.Extractor/IrisUploadClient.cs
      - src/Iris.Extractor/Iris.Extractor.csproj
  - type: publishes
    target: ../schemas/iris-package-manifest.md
    evidence:
      - src/Iris.Extractor/Program.cs
      - src/Iris.Extractor/PackageJsonOptions.cs
---

<!-- BEGIN GENERATED SECTION -->

## What it is (verified)

`PackAsTool=true`, `ToolCommandName=iris-extractor`
(`src/Iris.Extractor/Iris.Extractor.csproj`). References only `Iris.Contracts`
(shares the DTOs described in `schemas/iris-package-manifest.md`) plus
`Microsoft.CodeAnalysis.CSharp` (Roslyn) and `System.CommandLine`. Only a
`dotnet` subcommand exists (`Program.cs`): scans `--root`, writes
`--output` (default `iris-package.json`), and — only if `--api`,
`--application-id`, `--version-id` and `--token` are **all** provided —
uploads straight to the import endpoint via `IrisUploadClient`.

Scanners present under `DotNet/`: `AppSettingsScanner`, `LaunchSettingsScanner`,
`RoslynConfigurationScanner`, `SecretHeuristics`, `ConfigurationFragment`,
`PathFiltering`. Not individually described here — see
`docs/application-assimilation.md` ("Stato dell'implementazione": only the
`.NET` extraction path is a real tool; Node/Java/Docker/Ansible sections in
that doc are guidance for manual manifest authoring, not other extractors).

<!-- END GENERATED SECTION -->

<!-- BEGIN CURATED SECTION -->
_Not reviewed. This stub exists to satisfy one relation
(`domains/applications.md` is `called_by` this module) discovered while
documenting Applications — it deliberately does not attempt full coverage of
`Iris.Extractor`'s own internals. Promote to a full module pass (with its own
`tests_associated`, failure modes, etc.) in Phase 5._
<!-- END CURATED SECTION -->
