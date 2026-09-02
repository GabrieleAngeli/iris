# LLM Context Pack

Questa cartella contiene il contesto operativo sintetico per lavorazioni LLM-oriented a lungo termine su Iris.

Scopo:

- ridurre perdita di contesto tra sessioni;
- evitare allucinazioni su decisioni già prese;
- rendere esplicite fonti, vincoli, gate e prossime azioni;
- dare a un agente LLM un punto di ingresso breve prima di modificare codice o documentazione.

Convenzione adottata da `F:\Work\Iris_v3` ("Momentum"), un progetto diverso e non correlato — vedi
`../docs/analisi-iris-v2-v3.md` per il confronto. Solo la struttura è stata ripresa; i contenuti sono
scritti da zero per Iris.

## Come usare questa cartella

Prima di ogni nuova iterazione leggere, in ordine:

1. [`00-current-state.md`](00-current-state.md)
2. [`01-decisions.md`](01-decisions.md)
3. [`02-operational-plan.md`](02-operational-plan.md)
4. [`03-iteration-guardrails.md`](03-iteration-guardrails.md)
5. [`04-source-map.md`](04-source-map.md)
6. [`05-next-actions.md`](05-next-actions.md)
7. [`06-llm-bootstrap-prompt.md`](06-llm-bootstrap-prompt.md)
8. [`../docs/ui-standards.md`](../docs/ui-standards.md) — solo se il lavoro tocca il client MAUI
9. [`../.context/iris_icp_project_context_for_llm.md`](../.context/iris_icp_project_context_for_llm.md) — il brief di prodotto originale

## Procedura LLM di iterazione

Ogni iterazione dovrebbe seguire questo flusso:

1. **Orientamento:** leggere `.contex` (e `docs/ui-standards.md` se tocca il client).
2. **Classificazione:** indicare se il lavoro è definizione, implementazione, test o consolidamento.
3. **Verifica locale:** controllare `git status`, build corrente, file sorgenti prima di modificare.
4. **Intervento minimo:** applicare cambiamenti coerenti con i pattern esistenti (vedi `03-iteration-guardrails.md`).
5. **Validazione:** eseguire `dotnet build`/`dotnet test` pertinenti, o documentare perché non sono stati eseguiti.
6. **Aggiornamento contesto:** aggiornare `00-current-state.md`/`01-decisions.md`/`05-next-actions.md` se cambia
   una decisione o lo stato di avanzamento.
7. **Evidence:** chiudere riportando file modificati, test eseguiti, rischi residui e prossimo step.

Non saltare la classificazione: un punto progettato ma non implementato non va descritto come completato.

## Regola anti-allucinazione

Non dedurre dettagli implementativi se possono essere verificati nel repository.

Quando una decisione non è presente in questa cartella, trattarla come aperta e verificarla prima di agire —
in particolare non assumere che qualcosa esista in `Iris.Domain`/`Iris.Application` senza averlo cercato.
