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

## Task 2 — pure navigation ("add `GET /applications/{id}/versions`")

Same method, different question shape: "which files do I create/change to add this
endpoint, which permission, which test file" — a lookup-the-existing-pattern task,
not an impact-analysis or comprehension task.

| Metric | Baseline | Knowledge-layer | Delta |
|---|---:|---:|---:|
| Tool uses (harness-counted) | 18 | 28 | **+56%** |
| Tokens | 57,254 | 67,947 | **+19%** |
| Duration | 82.6s | 106.0s | **+28%** |
| Lines read | ~780 | ~1,384 | **+77%** |
| Distinct files | 10 | 14 (4 `.knowledge/` + 10 repo) | **+40%** |
| Turns | 5 | 9 | **+80%** |

**Knowledge-layer was worse on every single axis here — stated plainly, not
softened.** Both agents reached the same core finding (the exact response DTO,
`ApplicationVersionSummaryResponse`, already exists and just needs a new route),
found independently via grep by the baseline in 5 turns. The knowledge-layer agent
still had to open essentially the same source files as the baseline, plus 4
`.knowledge/` files on top, for no compensating shortcut — the layer currently has
no "if you're adding a read endpoint, follow this shape" scaffolding content, so it
added pure overhead on a task that plain grep already handles cheaply at this repo's
size.

## Task 3 — pure comprehension ("is this permission split a bug?")

"Why does `GET /applications/installations` need `Deployments.Read` instead of
`Applications.Read`, despite sharing a file and a C# namespace with `GET
/applications` — bug or intentional?"

| Metric | Baseline | Knowledge-layer | Delta |
|---|---:|---:|---:|
| Tool uses (harness-counted) | 29 | 13 | **-55%** |
| Tokens | 67,862 | 52,729 | **-22%** |
| Duration | 174.0s | 81.7s | **-53%** |
| Lines read | ~980 | ~600 | **-39%** |
| Distinct files | 10 | 6 (3 `.knowledge/` + 3 repo) | **-40%** |
| Turns | ~21 | ~4-6 | **-71%..-76%** |

**Knowledge-layer was clearly better on every axis here.** The baseline had to
reconstruct the boundary from scratch by independently checking five different
subsystems (permission catalog comments, `TransactionLogInterceptor.AreaFor`, seed
roles, the MAUI client's nav gating, and the founding product brief) before
concluding "intentional." The knowledge-layer agent went almost straight to
verifying a claim already stated in `domains/applications.md` ("Boundaries": *"treat
that split as the real domain boundary, not the C# folder"*) against source, then
spent its remaining turns gathering corroborating evidence rather than building the
argument from zero. It also found an extra insight neither prompt asked for (the
`ApplicationInstallation.cs` file is arguably mis-filed relative to the boundary the
rest of the system enforces) and explicitly cross-checked the knowledge layer's own
claim against source before trusting it — exactly the verification discipline
`index.md` asks for.

## Aggregate across all 3 tasks (n=3 tasks, still 1 trial each — not statistically robust)

| Metric | Baseline (sum) | Knowledge-layer (sum) | Delta |
|---|---:|---:|---:|
| Tool uses | 112 | 92 | -18% |
| Tokens | 250,288 | 250,443 | **+0.06% (a wash)** |
| Duration | 691.2s | 622.5s | -10% |

**The aggregate hides the real finding, and the real finding is more useful than a
single percentage would be.** Total tokens across three tasks are, for practical
purposes, identical between conditions — there is no blanket "knowledge layer saves
tokens" effect here. What varies enormously is *which task shape* benefits:

- **Comprehension / "why" / boundary questions** (Task 3): large, consistent win on
  every metric, because a fact that's expensive to *reconstruct* by cross-referencing
  several subsystems was already stated once, with evidence, in one place.
- **Impact analysis across many files** (Task 1): mixed — fewer tool calls/turns,
  flat-to-higher tokens/lines, but a real, attributable quality gain (respected a
  domain boundary the baseline violated; caught an extra bug).
- **Pure code-pattern navigation** (Task 2): the knowledge layer added cost with no
  offsetting benefit, because the layer doesn't yet encode "how to add a new
  endpoint" scaffolding knowledge, and grep already finds the analogous existing
  code quickly at this repo's size.

## Recommendation before Phase 4/5

Do not generalize a single percentage from this data. What it does support:

1. **Prioritize "why/boundary/gotcha" content over "where do I add code" content**
   when extending to new domains — that's where the measured payoff is, not a
   general property of having a `.knowledge/` directory.
2. **Try closing the Task-2-style gap cheaply**: add a short "to add a new read
   endpoint in this domain, follow the shape of X" scaffolding note to
   `domains/applications.md` and re-run Task 2 once to see if that specific gap
   closes, before assuming navigation tasks are just a bad fit for this layer.
3. **Repo-size caveat**: this repository is small enough that baseline grep-based
   discovery is already fast. The comprehension-task win (Task 3) plausibly grows
   with codebase size (more subsystems to cross-reference by hand); the
   navigation-task loss (Task 2) plausibly shrinks or reverses once grep-based
   discovery itself becomes expensive. Neither is tested here.
4. Still no RAG or text-search-only arm — see "What this does NOT test" above,
   unchanged.
5. Three tasks, one trial each, is enough to form a hypothesis (task-shape-dependent
   payoff), not enough to size a rollout. Widening to more domains should keep
   measuring per-task-shape rather than reporting a single blended number.
