# Iris — Codebase Knowledge Layer

This is the entry point for an agent or developer who needs to work on Iris without
reading the whole repository first. It is a **Phase 2 proof of concept** covering
exactly one domain (**Applications**) end to end, so the pattern can be judged before
it is extended to the rest of the codebase. See `manifest.yaml` for exact scope.

## This is not a replacement for `.contex/` and `.context/`

The repository already has a curated, actively-maintained context pack that this
layer **links to, and does not duplicate**:

- [`../.context/iris_icp_project_context_for_llm.md`](../.context/iris_icp_project_context_for_llm.md)
  — the original product brief (vision, operator workflow, philosophy). Treat as
  `curated`, Level 0.
- [`../.contex/`](../.contex/) — operational memory updated every iteration:
  `00-current-state.md` (what's built, verified with real `dotnet test` runs),
  `01-decisions.md` (binding architectural decisions), `02-operational-plan.md`
  (roadmap), `03-iteration-guardrails.md` (pattern/test/evidence gates),
  `04-source-map.md` (file-per-area map), `05-next-actions.md` (prioritised
  backlog + recent session log), `06-llm-bootstrap-prompt.md`, `07-session-log.md`
  (narrative history — treat as an append-only log, not a source to parse into
  concepts directly).

**If `.knowledge/` and `.contex/` ever disagree, `.contex/` wins** — it is closer
to the code and updated by whoever is actually changing it. Open an issue against
this layer rather than trusting a stale concept file over `.contex/00-current-state.md`.

## How an agent should use this (progressive disclosure protocol)

1. Read the repository's own instructions first (there is no `AGENTS.md`/`CLAUDE.md`
   yet — `.contex/06-llm-bootstrap-prompt.md` fills that role today).
2. Read this file.
3. Identify the domain your task touches. If it's **Applications**, continue below.
   If it's anything else, this layer does not cover it yet — fall back to
   `.contex/04-source-map.md` and read the code directly.
4. Load only the concept files linked from that domain's page — not the whole
   `.knowledge/` tree.
5. Check `source.commit` in each concept's frontmatter against the current
   `HEAD`. If it's behind, treat the concept as a hint, not a fact, and verify
   against the code before relying on it for anything risky.
6. Expand context only on demand (follow a `relations` edge) — don't preload the
   whole graph.

## Concept types in this layer

| `type` | Meaning | Where |
|---|---|---|
| `domain` | A bounded area of the product (Applications, Deployments, …) | `domains/` |
| `api` | A group of HTTP endpoints and what they do | `apis/` |
| `data-model` | Persisted entities + migration history for a domain | `data/` |
| `schema` | An external contract (a JSON manifest, a DTO shape) | `schemas/` |
| `module` | A standalone tool/project that isn't a "domain" (e.g. the extractor CLI) | `modules/` |
| `decision` | An ADR — normalised from `.contex/01-decisions.md` and `docs/` | `decisions/` |
| `glossary` | Domain vocabulary | `glossary/` |
| `architecture` | Deterministically-extracted structural facts (project graph, build config) | `architecture/` |

## Relation direction (canonical vs derived)

To avoid two files disagreeing about the same edge, each relation is declared
**once**, on the side that "owns" the fact (the container declares `contains`,
the caller declares `calls`, the consumer declares `consumes`/`depends_on`, the
data owner declares `owns_data`). The inverse (`belongs_to`, `called_by`,
`publishes`→consumed-by, `impacts`) is not re-declared in frontmatter for this
PoC — it is written as prose in the target file's "Referenced by" section,
derived by hand for this small batch. **Phase 4 automates this** with a generator
that computes the inverse index instead of trusting a second hand-written copy.

## Provenance levels used here

- `verified` — read directly from code/config/migrations at `source.commit`.
- `curated` — written and reviewed by a person (an ADR, a runbook).
- `inferred` — a description synthesised from verified facts (e.g. "responsibility"
  prose). Never promoted to `verified` automatically.
- `stale` / `conflicting` — not used yet in this PoC; will appear once the
  incremental-update pipeline (Phase 4) can detect them.

## Coverage (honest, not aspirational)

**Documented here:** the Applications catalog domain — `ApplicationDefinition`/
`ApplicationVersion` and their configuration knowledge, the catalog HTTP endpoints,
the persisted data model, the `iris-package.json` manifest schema, and the
`Iris.Extractor` tool that produces it.

**Deliberately out of scope for this PoC** (do not assume undocumented = doesn't
exist — see `.contex/00-current-state.md` instead): Access/Governance, Infrastructure
(servers, data services), **Deployments & the Validation Engine**
(`ApplicationInstallation`, `InstallationRun`, AWX/Ansible/OpenBao integration —
note this is exactly what branch `feature/deployments-validation` is working on),
Setup/Mail, Audit, and the `Iris.App` MAUI client. These are real, built, and
described in `.contex/00-current-state.md` — they are just not yet modelled as
typed concepts in `.knowledge/`.

## Start here for Applications

→ [`domains/applications.md`](domains/applications.md)
