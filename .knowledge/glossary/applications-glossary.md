---
type: glossary
name: applications-glossary
title: Applications domain glossary
description: Vocabulary used across code, docs and .contex for the Applications domain.
status: active

source:
  repository: iris
  paths:
    - src/Iris.Domain/Applications
    - docs/application-assimilation.md
    - .context/iris_icp_project_context_for_llm.md
  commit: 7e011bf0dd7c5a152a612554ef523e6081923c50
  generated_at: "2026-09-07T11:54:15Z"

ownership:
  team: ""
  maintainers:
    - Gabriele Angeli

classification:
  domain: applications
  criticality: low
  lifecycle: production
  tags:
    - glossary

provenance:
  method: curated
  confidence: curated
  reviewed_by: ""
  reviewed_at: ""

relations:
  - type: documented_by
    target: ../domains/applications.md
    evidence:
      - src/Iris.Domain/Applications
---

<!-- BEGIN CURATED SECTION -->

**Configuration knowledge** — what a version of an application *needs* to run
(keys, dependencies, placeholders), as opposed to the concrete values it will
run with in a specific deployment. See `.context/iris_icp_project_context_for_llm.md`,
"Iris Extractor" section.

**Placeholder** — a named requirement an application version exposes (e.g. a
connection string it needs) without saying what infrastructure resource fills
it. Resolved later, at installation time, against a concrete
server/data-service/other-application.

**Provider / consumer (application-to-application)** — a `DependencyInput` can
point at another application already in the Iris catalog via
`ProviderApplicationSlug` + `ProviderPlaceholderKey`, so the same placeholder
key can represent both the service that exposes it and the service that
consumes it (`src/Iris.Contracts/Applications/ApplicationRequests.cs`).

**Manifest / `iris-package.json`** — the file format a build pipeline (or a
human) produces to describe one version's configuration knowledge. See
`schemas/iris-package-manifest.md`.

**Configuration compiler** — not yet built; the future component that would
combine an application version's configuration knowledge with customer,
context, server and domain bindings into an *effective configuration*. See
`docs/application-configuration-model-analysis.md` (marked `inferred`/planned
in `domains/applications.md`).

**Application unit** — one of possibly several launchables built from the same
artifact/source (e.g. `augeg4.engine`, `augeg4.monitor-admin`,
`augeg4.p5.engine` from the same AugeG4 build).

**Installation profile** — a named installation mode for a version (e.g.
`master`/`slave`), each optionally requiring a distinct set of configuration
keys.

<!-- END CURATED SECTION -->
