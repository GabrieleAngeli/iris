# Maintaining `.knowledge/`

Operational notes for whoever (human or agent) changes this layer or the CI around
it. Not a concept file — no frontmatter, excluded from `tools/Validate-Knowledge.ps1`
like `index.md`/`log.md`.

## CI

`.github/workflows/knowledge-layer.yml` runs on every PR/push to `main` and on
`workflow_dispatch`, with no path filter — see that file's own comment for why (a
path filter would miss the exact case drift detection exists for: code changing
without `.knowledge/` being touched at all). It runs two scripts and uploads their
reports as build artifacts:

1. **`tools/Validate-Knowledge.ps1`** — structural validation. **Blocking** (exit 1
   fails the build) for: missing required frontmatter fields, a relation `target`
   that doesn't resolve to a real file, unbalanced `GENERATED`/`CURATED` section
   markers. **Non-blocking warnings** for: an unrecognised `status`/`confidence`
   value, a missing `source.commit`, an evidence path that doesn't exist in the repo.
2. **`tools/Detect-Drift.ps1`** — advisory only, always exits 0. Reports which
   concepts reference source paths that changed after their `source.commit`. A
   drifted concept is not proven wrong, only unverified — re-check it before relying
   on it. No blocking drift-ratio threshold is wired in yet (see "What's
   deliberately not done" below).

Both scripts are regex-based against YAML frontmatter, **not a real YAML parser**
(PowerShell 5.1/7 ship none, and this environment has no package feed access to add
one mid-session). They are good enough to catch the concrete failure modes above,
not a substitute for real schema validation. Replace them with a small dotnet tool
(YamlDotNet + Markdig — idiomatic for this repo, which is already .NET) once the
layer grows past a handful of files, or once someone hand-edits a concept file
with severely malformed YAML that this regex approach can't detect.

## Manual-content protection

Every concept body is split into:

```markdown
<!-- BEGIN GENERATED SECTION -->
... replaceable on every regeneration pass ...
<!-- END GENERATED SECTION -->

<!-- BEGIN CURATED SECTION -->
... never auto-overwritten ...
<!-- END CURATED SECTION -->
```

**There is no automatic regenerator yet** — every concept file so far was written by
an agent in one pass, by hand, not produced by a repeatable generator script. The
marker convention is in place so that *when* a generator exists (a natural Phase 5
increment once more domains are added and hand-authoring doesn't scale), it has a
contract to respect from day one: replace only between `GENERATED` markers, refuse
to touch anything between `CURATED` markers, and fail loudly (not silently overwrite)
if a `GENERATED` section was hand-edited outside of a regeneration run — that
"detect a manual edit inside a generated section" check does not exist yet either;
today `Validate-Knowledge.ps1` only checks that markers are *balanced* (every BEGIN
has a matching END), not that generated content matches what a regenerator would
produce, since no regenerator exists to compare against.

**Decisions ADR-normalized from `.contex/`/`docs/`** (currently only
`decisions/adr-0001-applications-domain-origin.md`) are entirely curated — do not
run any future automated regeneration pass against files under `decisions/`.

## Drift, concretely

A concept "drifts" when one of its `source.paths` changes after `source.commit`. Run
`tools/Detect-Drift.ps1` locally any time before trusting a concept for something
consequential, or read the CI-uploaded `drift-report.md` artifact on the latest run.
When a concept is reported drifted:

1. Read the actual diff on the changed path(s) since `source.commit`.
2. Decide if the concept's claims still hold. If yes, bump `source.commit` and
   `source.generated_at` in that file's frontmatter (a one-line, low-risk edit) —
   this is "re-verified, nothing changed in substance."
3. If the claims no longer hold, update the `GENERATED SECTION` content to match,
   and bump `source.commit`/`generated_at`. Do not touch `CURATED SECTION` content
   as part of this unless the underlying decision itself changed (in which case
   that's a content decision for a human, not a mechanical drift fix).

## What's deliberately not done yet (don't assume oversight)

- **No drift-ratio threshold that fails CI.** The Phase 1 plan asks for one
  ("drift superiore alla soglia concordata" as a blocking condition), but choosing a
  number now, with 8 concept files and one benchmark's worth of data, would be an
  invented threshold, not an evidence-based one. Revisit once more domains exist and
  there's a real distribution of drift frequency to look at.
- **No conflict resolution UI/diff tool.** A conflict today just means: the
  validator or drift-detector reports it, a person reads the file and the source,
  and edits by hand. No tooling automates the "generated section changed, show me a
  readable diff" step from the original Phase 1 plan.
- **No schema-version enforcement beyond `manifest.yaml`'s `schema_version` field.**
  Individual concept files don't carry their own schema version; if the frontmatter
  shape in `index.md`'s "Concept types" table ever changes incompatibly, bump
  `manifest.yaml`'s `schema_version` and treat every existing concept file as needing
  re-validation against the new shape — there's no automated migration between
  schema versions.

## Rollback

`.knowledge/` is plain files under normal git version control — nothing here is a
live database or an external system with its own state. Rolling back is exactly
`git revert`/`git checkout` on the relevant commit(s), like any other part of this
repository. There is no special procedure, and specifically no synchronization step
with anything outside the repo to worry about (no server, no external index to
invalidate).
