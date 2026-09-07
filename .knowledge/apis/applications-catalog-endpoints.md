---
type: api
name: applications-catalog-endpoints
title: Applications catalog — HTTP endpoints
description: >-
  Minimal-API endpoints under /applications that manage the catalog and
  configuration-knowledge import (excludes the /applications/installations/*
  group, which belongs to the Deployments area).
status: active

source:
  repository: iris
  paths:
    - src/Iris.Api/Endpoints/ApplicationsEndpoints.cs
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
    - http
    - minimal-api

provenance:
  method: static-analysis
  confidence: verified
  reviewed_by: ""
  reviewed_at: ""

relations:
  - type: belongs_to
    target: ../domains/applications.md
    evidence:
      - src/Iris.Api/Endpoints/ApplicationsEndpoints.cs
  - type: reads
    target: ../data/applications-data.md
    evidence:
      - src/Iris.Api/Endpoints/ApplicationsEndpoints.cs#L14-L18
  - type: writes
    target: ../data/applications-data.md
    evidence:
      - src/Iris.Api/Endpoints/ApplicationsEndpoints.cs#L26-L49
      - src/Iris.Api/Endpoints/ApplicationsEndpoints.cs#L205-L229
  - type: consumes
    target: ../schemas/iris-package-manifest.md
    evidence:
      - src/Iris.Api/Endpoints/ApplicationsEndpoints.cs#L205-L229
---

<!-- BEGIN GENERATED SECTION -->

## Endpoints (catalog scope only)

Extracted directly from `src/Iris.Api/Endpoints/ApplicationsEndpoints.cs`. All
routes are mounted under `app.MapGroup("/applications")`.

| Method | Route | Handler | Permission | Line |
|---|---|---|---|---|
| GET | `/applications` | `ListApplicationsHandler` | `Applications.Read` | L14 |
| POST | `/applications` | `CreateApplicationHandler` | `Applications.Write` | L26 |
| PUT | `/applications/{applicationId:guid}` | `UpdateApplicationHandler` | `Applications.Write` | L51 |
| POST | `/applications/{applicationId:guid}/versions` | `AddApplicationVersionHandler` | `Applications.Write` | L174 |
| GET | `/applications/{applicationId:guid}/versions/{versionId:guid}` | `GetApplicationVersionDetailHandler` | `Applications.Read` | L190 |
| POST | `/applications/{applicationId:guid}/versions/{versionId:guid}/import` | `ImportConfigurationPackageHandler` | `Applications.ImportKnowledge` | L205 |

**Not listed here** (same file, same route group, but belong to the Deployments
area per permission and per `TransactionLogInterceptor.AreaFor` — see
`domains/applications.md` "Boundaries"): `GET /applications/installations`,
`POST /{applicationId}/installations`, `GET .../validate`,
`GET .../ansible-vars`, `POST .../awx/launch`, `GET .../runs`,
`GET .../runs/{runId}`. All of these require `Deployments.*` permissions, not
`Applications.*` (verified at L20-24, L100-172).

## Error mapping

Not endpoint-specific: application errors map to RFC 7807 problem responses
repo-wide (404 unknown resource, 409 duplicate key, 400 invalid payload/scope)
— see `README.md`, "API endpoints" section.

<!-- END GENERATED SECTION -->

<!-- BEGIN CURATED SECTION -->
_No manually-curated additions yet._
<!-- END CURATED SECTION -->
