# LLM Bootstrap Prompt

Usare questo prompt all'inizio di una nuova sessione o dopo compattazione del contesto.

```text
Stai lavorando nel repository F:\Work\iris, branch main.

Prima di agire leggi la cartella .contex in ordine:
1. .contex/00-current-state.md
2. .contex/01-decisions.md
3. .contex/02-operational-plan.md
4. .contex/03-iteration-guardrails.md
5. .contex/04-source-map.md
6. .contex/05-next-actions.md
7. docs/ui-standards.md — solo se il lavoro tocca il client MAUI

Obiettivo di lungo periodo:
Iris è un Infrastructure Control Plane application-aware: definisce, capisce, valida e
orchestra; l'esecuzione resta ad AWX/Ansible/OpenBao (mockati oggi). Sei backend .NET 9
esagonale (Domain/Application/Infrastructure/Api/Contracts) + client .NET MAUI Windows.

Decisioni vincolanti (dettaglio in .contex/01-decisions.md):
- Domain puro, nessuna dipendenza EF/HTTP;
- ID sempre Guid.CreateVersion7(), ValueGeneratedNever();
- enum di dominio persistiti come stringa;
- ogni comando di scrittura ha un Permissions.<area>.<azione> dedicato, mai riusato;
- nessun segreto reale in DB — sempre via ISecretStore, mai in chiaro;
- ogni migrazione EF va creata sia per SQLite sia per Postgres;
- client MAUI: flussi secondari sono finestre modali OS reali (IDialogService), mai
  pannelli in-page; Shell.MenuItemTemplate non va riprovato (rotto sull'handler Windows);
- docs/ui-standards.md è la fonte di verità per qualunque cambiamento UI.

Stato: Access/AAA, Governance (utenti/clienti/inviti/edit-lock/password), Infrastructure
(server+credenziali) sono costruiti e verificati (dotnet test verde). Applications,
Deployments, Actions, Validation Engine non esistono ancora — prossimo lavoro pianificato
in .contex/05-next-actions.md, con F:\Work\Iris_v2 come riferimento di dominio (non di
codice: mai buildato con successo, non in git).

Best practice operative:
- lavora su evidenze locali: Grep/Glob/Read, git status, build/test reali;
- non dichiarare test passati se non eseguiti;
- non inventare nomi di file/entità/endpoint: verificarli;
- non confondere un modulo pianificato con uno implementato;
- per un cambiamento non banale multi-file, usa EnterPlanMode prima di scrivere codice;
- quando tocchi un'area, aggiorna .contex/00-current-state.md e 05-next-actions.md se
  cambia lo stato di avanzamento.

Anti-allucinazione:
- verifica sempre file e stato locale prima di modificare;
- se un'informazione su Iris_v2/Iris_v3 è incerta, verificala di persona;
- se tocchi codice runtime, esegui build/test pertinenti o dichiara perché non lo hai fatto.
```
