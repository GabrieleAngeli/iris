# Knowledge layer changelog

This tracks changes to `.knowledge/` itself — not the project (that's
`.contex/07-session-log.md`) and not commits (that's `git log`).

---

## 2026-09-07 — Phase 2 proof of concept: Applications domain

**Scope**: one domain (Applications catalog), end to end, per the approved
Phase 1 assessment and the two decisions taken before starting
(domain = Applications; CI validation deferred to Phase 4).

**Created**:
- `index.md`, `manifest.yaml`, `log.md` (this file) — layer scaffolding.
- `domains/applications.md` — the core concept: responsibility, boundaries,
  inbound/outbound dependencies, data, failure modes, tests, technical debt.
- `apis/applications-catalog-endpoints.md` — the 6 catalog endpoints (excludes
  the 7 `/installations/*` endpoints in the same file, which belong to the
  undocumented Deployments area — see "Boundaries" in the domain file).
- `data/applications-data.md` — EF Core entities + migration history.
- `schemas/iris-package-manifest.md` — the manifest contract, reconstructed
  from the C# request DTOs (no formal JSON Schema exists yet — flagged as a
  natural next increment).
- `modules/iris-extractor.md` — deliberately a **stub** (`status: draft`,
  `provenance.confidence: inferred`): created only to satisfy one relation
  discovered while documenting Applications, not a full pass over the
  extractor's own internals.
- `decisions/adr-0001-applications-domain-origin.md` — normalised from
  `docs/analisi-iris-v2-v3.md` + `.contex/01-decisions.md`.
- `glossary/applications-glossary.md`.
- `architecture/module-graph.md` — the real `<ProjectReference>` graph,
  solution-wide (cheap to extract, useful beyond just this domain).
- `tools/Validate-Knowledge.ps1` — PoC-grade regex validator (explicitly not a
  real YAML parser; see the script's own header for why and what Phase 4
  should replace it with).

**Validated**: `tools/Validate-Knowledge.ps1` run twice.

- **First run**: 4 blocking errors — `domains/applications.md` declared
  relation targets as `apis/foo.md` instead of `../apis/foo.md` (wrong
  relative-path base). This is a real authoring bug the validator caught
  exactly as intended, not a validator false positive.
- **Second run** (after fixing the paths): 1 blocking error — the validator
  had picked up its own prior output (`validation-report.md`) as an
  undocumented concept file. Fixed the exclusion list in the script.
- **Third run**: 8/8 concept files, 0 errors, 0 warnings. See
  `validation-report.md` for the full report (regenerated on every run — not
  hand-maintained).

**Explicitly NOT done in this pass** (by design, not oversight):
- No CI job — deferred to Phase 4 per the approved decision.
- No inverse-relation generator — inverse edges (`belongs_to`, `called_by`,
  …) were derived by hand for this small batch and written as prose, not
  frontmatter. Noted in `index.md` "Relation direction" as a Phase 4 item.
- Deployments/Validation, Access/Governance, Infrastructure, Setup/Mail,
  Audit and the `Iris.App` client are not modelled. `index.md` "Coverage"
  says this explicitly so an agent doesn't mistake absence for non-existence.
- No JSON Schema file for `iris-package.json` — the contract is documented in
  prose + a link to the C# DTOs, not machine-validated yet.

**Human review status**: nothing in this pass has `provenance.reviewed_by`
set — every `verified`/`inferred` fact should be treated as agent-produced
and spot-checked against the code before being trusted for anything
consequential, exactly as `index.md` instructs.

---

## 2026-09-07 — Phase 3 benchmark (n=1)

**What**: two fresh, isolated sub-agents given the identical impact-analysis
task ("retype `ConfigurationKeyInput.DefaultValue`"), one forbidden from
reading `.knowledge/`, one instructed to start there. Full results in
`benchmark-report.md`.

**Headline finding, stated as measured, not as hoped for**: tokens, lines
read and wall-clock time were flat-to-slightly-higher with the knowledge
layer (it added 5 extra files to read on top of the same repo files);
tool-call count and reasoning turns were ~18-22% and ~16% lower. The
knowledge-layer agent also produced a materially better-scoped answer,
directly attributable to one sentence in `domains/applications.md`
("Boundaries") that stopped it from proposing changes to Deployments-area
files the current branch is actively touching, and it caught an extra real
bug (`AppSettingsScanner.Flatten`'s `.ToString()` boolean-casing defect) the
baseline missed.

**Explicitly not claimed**: any percentage cost saving, any statistically
significant result (n=1), any comparison against text-search-only or RAG
arms (not constructed — see `benchmark-report.md` "What this does NOT test").

**Recommendation carried forward**: repeat on 2-3 differently-shaped tasks
(pure navigation, pure comprehension) with more than one trial each before
Phase 4/5 resourcing decisions.

---

## 2026-09-07 — Phase 3 benchmark extended: 2 more task shapes (n=3 tasks total)

**What**: ran the same blind two-agent method on a pure navigation task ("add
`GET /applications/{id}/versions`") and a pure comprehension task ("is the
`Deployments.Read` vs `Applications.Read` split on `/applications/installations`
a bug?"). Full tables in `benchmark-report.md`.

**Headline, stated plainly, including the uncomfortable part**: the knowledge
layer's payoff is **task-shape-dependent, not a blanket property**:

- Navigation task: knowledge-layer was **worse on every metric** (+56% tool
  calls, +19% tokens, +28% time) for no quality gain — both agents reached the
  identical conclusion, and the layer currently has no endpoint-scaffolding
  content to shortcut a "find the existing pattern" task that grep already
  handles cheaply at this repo's size.
- Comprehension task: knowledge-layer was **better on every metric** (-55%
  tool calls, -22% tokens, -53% time) plus a quality gain (an extra insight,
  and explicit verification of the layer's own claim against source) —
  because `domains/applications.md`'s "Boundaries" section had already stated,
  with evidence, the exact fact the baseline had to reconstruct from five
  different subsystems by hand.
- Aggregated across all 3 tasks so far: tool calls -18%, tokens **+0.06% (a
  wash)**, time -10%. The aggregate number is not the useful output here — the
  per-task-shape split is.

**Decision this drives**: when extending to more domains, prioritise writing
"why/boundary/gotcha" content (proven payoff) over "where do I add code"
scaffolding content (no measured payoff yet, possibly closable — see
`benchmark-report.md` recommendation 2 for a cheap follow-up experiment).

**Still not claimed**: a rollout-sized sample, a repo-size-independent result,
or any single blended savings percentage.
