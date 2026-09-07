# Phase 3 benchmark — Applications `DefaultValue` typing task (n=1)

_Not a concept file (no frontmatter, excluded from `tools/Validate-Knowledge.ps1` like
`index.md`/`log.md`). This documents one benchmark run of the Phase 2 PoC, per the
"non dichiarare risparmi senza dati riproducibili" rule from the Phase 1 plan._

## Method

Two identical `general-purpose` sub-agents, each with **fresh context** (no memory of
this conversation, no prior exposure to the repo), given the **same task prompt**
verbatim, differing in exactly one instruction:

- **Baseline**: explicitly forbidden from reading anything under `.knowledge/`.
- **Knowledge-layer**: instructed to start at `.knowledge/index.md`, follow relevant
  links, then verify against real source before answering (per the layer's own
  "don't trust it blindly" rule).

Both were read-only investigations, asked to self-report tool-call count, lines read,
files opened, and reasoning turns.

**Why a fresh-context sub-agent and not me doing it twice**: I had already read the
whole repository earlier in this conversation, so a same-session "baseline" would be
contaminated by knowledge I couldn't un-know. A blind comparison requires two agents
that have genuinely never seen the code before.

**Task given** (a real, representative impact-analysis question, not synthetic):
"Change `ConfigurationKeyInput.DefaultValue` from `string?` to hold a native typed
default (number/boolean), not a stringified one. What needs to change end-to-end, and
what could break?" — chosen because it stresses exactly what a knowledge layer claims
to help with: cross-cutting impact analysis across Contracts → Domain → Application →
Infrastructure → Extractor → MAUI client → tests.

## What this does NOT test

Only 2 of the 5 arms described in the original plan's benchmark section were run
(no knowledge layer vs. knowledge layer + progressive disclosure). **Text-search-only**
and **RAG** arms were not constructed: this repo has no RAG infrastructure, and
building one solely to benchmark against would be disproportionate engineering for a
single PoC-scale test (`grep`/`Glob` *is* what "sola ricerca testuale" looks like here,
and both agents already had those tools — the baseline arm effectively **is** the
text-search-only condition for a codebase of this size). This is one task, run once
per condition — LLM agents are stochastic, so treat every number below as indicative,
not proof. Extending to more tasks/trials before any resourcing decision is the
Phase 3 recommendation below, not a formality.

## Results

| Metric | Baseline (no `.knowledge/`) | Knowledge-layer | Delta |
|---|---:|---:|---:|
| Tool calls (self-reported) | 60 | 49 | -18% |
| Tool uses (harness-counted) | 65 | 51 | -22% |
| Lines of file content read | ~2,439 | ~2,680 | **+10%** |
| Distinct files opened | 16 | 22 (5 under `.knowledge/` + 17 in-repo) | **+38%** |
| Reasoning turns to a confident answer | ~32 | 27 | -16% |
| Subagent tokens consumed | 125,172 | 129,767 | **+4%** |
| Wall-clock duration | 434,609 ms | 434,836 ms | ~0% (tie) |

**Headline, stated plainly**: on this single trial, the knowledge layer did **not**
reduce total context consumed, tokens spent, or wall-clock time — all three were flat
or slightly *higher* for the knowledge-layer agent, because it opened 5 extra files
(`.knowledge/*`) on top of the same repo files, and then still verified nearly
everything against source. It **did** need fewer tool calls and reasoning turns to
arrive there, consistent with the layer's job (point straight at the relevant files
instead of discovering them by grep-and-miss) rather than the job of loading less.
**Do not repeat "X% cheaper" from this data point** — it isn't what happened.

## Qualitative difference (where the two answers actually diverged)

The more interesting result isn't the metrics table — it's what each agent found:

1. **Domain boundary respected only by the knowledge-layer agent.** The baseline
   listed `ValidateApplicationInstallation.cs` and
   `GetApplicationInstallationAnsiblePlan.cs` (Deployments-area files) as needing
   changes. The knowledge-layer agent explicitly chose *not* to touch them, citing
   the domain split documented in `domains/applications.md`'s "Boundaries" section,
   and noted this matters concretely because `feature/deployments-validation` (the
   current branch) is actively changing that exact code. This is a direct,
   attributable payoff of one specific sentence I wrote in Phase 2 — not a generic
   LLM capability.
2. **An extra real bug caught only by the knowledge-layer agent**: it found that
   `Iris.Extractor`'s `AppSettingsScanner.Flatten` stringifies JSON booleans via
   `.ToString()`, producing `"True"`/`"False"` (.NET casing — invalid JSON), a worse
   variant of the bug than the one being fixed. The baseline flagged the same file as
   needing changes but did not surface this specific defect.
3. **Different, both-defensible designs.** Baseline assumed a schema change (EF
   converter + new migrations in both SQLite and Postgres providers). Knowledge-layer
   proposed converting at the API boundary only (`JsonElement?` in the contract,
   string still in the domain/DB), explicitly to minimize blast radius — and then
   correctly flagged the risk that existing DB rows aren't valid JSON and would break
   a naive parse-on-read. This is a genuine design trade-off, not a right/wrong split;
   worth a human decision, not something to resolve from this benchmark.
4. **The knowledge-layer agent flagged its own layer as going stale**: it noted that
   `domains/applications.md` and `schemas/iris-package-manifest.md` assert
   "`DefaultValue` is always a string" as fact, and that shipping this change would
   make those concept files wrong until regenerated — exactly the drift-detection
   behaviour Phase 4 needs to automate instead of relying on an agent to notice by
   hand.

## Interpretation

For a single, well-scoped domain question, the knowledge layer traded a small amount
of *extra* reading (the concept files themselves) for fewer blind exploratory tool
calls and a materially better-scoped answer — mainly because the domain-boundary
knowledge that's expensive to *discover* by grep (it requires knowing to compare
`TransactionLogInterceptor.AreaFor` mappings against permission names across files)
was already stated as a fact with evidence in `domains/applications.md`. That is a
plausible, real mechanism, not an artifact of prompt luck — but n=1 cannot separate
"the knowledge layer helped" from "this particular agent happened to reason better."

## Recommendation before Phase 4/5

Do not generalize from this run. Before committing engineering time to
CI/drift-detection/extension:

1. Repeat this comparison on **2-3 more tasks of different shapes** — at minimum a
   pure navigation task ("which files do I touch to add endpoint X") and a pure
   comprehension question ("why does `/applications/installations` need a different
   permission than the rest of `/applications`") — the current n=1 task is an
   impact-analysis task specifically, the shape the layer should be best at.
2. Run each task **more than once per condition** if the result is close, since a
   single LLM trial carries real sampling variance.
3. Treat the "fewer tool calls / turns, same-or-more tokens" pattern from this run as
   the working hypothesis to confirm or refute, not as an established property of the
   layer.
