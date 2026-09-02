# LLM Bootstrap Prompt

Usare questo prompt all'inizio di una nuova sessione o dopo compattazione del contesto.

```text
Stai lavorando nel repository D:\Repos\gabriele-angeli\iris, branch feature/applications-catalog.

Prima di agire leggi la cartella .contex in ordine:
1. .contex/00-current-state.md
2. .contex/01-decisions.md
3. .contex/02-operational-plan.md
4. .contex/03-iteration-guardrails.md
5. .contex/04-source-map.md
6. .contex/05-next-actions.md
7. docs/ui-standards.md - solo se il lavoro tocca il client MAUI
8. .context/iris_icp_project_context_for_llm.md - brief di prodotto originale

Obiettivo di lungo periodo:
Iris è un Infrastructure Control Plane application-aware: definisce, capisce, valida e
orchestra; l'esecuzione resta ad AWX/Ansible/OpenBao/Grafana, mockati oggi. Sei in un
backend .NET 9 esagonale (Domain/Application/Infrastructure/Api/Contracts) + client .NET
MAUI Windows.

Decisioni vincolanti:
- Domain puro, nessuna dipendenza EF/HTTP;
- ID sempre Guid.CreateVersion7(), ValueGeneratedNever();
- enum di dominio persistiti come stringa;
- ogni comando di scrittura ha un Permissions.<area>.<azione> dedicato, mai riusato;
- nessun segreto reale in DB: sempre via ISecretStore, mai in chiaro;
- setup produzione one-shot: niente super-admin seed automatico fuori dal wizard;
- token sessione Iris opachi, persistiti solo come hash;
- ogni migrazione EF va creata sia per SQLite sia per Postgres;
- client MAUI: flussi secondari sono finestre modali OS reali (IDialogService), mai
  pannelli in-page; Shell.MenuItemTemplate non va riprovato;
- docs/ui-standards.md è la fonte di verità per qualunque cambiamento UI.

Stato:
Access/AAA, Governance (utenti/clienti/inviti/edit-lock/password), Infrastructure
(server+credenziali+capability/risorse/porte), Applications backend-first (catalogo,
versioni, import configuration knowledge), auth produzione con sessioni locali, first-run
setup wizard, SMTP, Serilog e security scanning minimo sono costruiti e verificati con
test backend verdi. Deployments, Validation Engine e Actions non esistono ancora:
prossimo lavoro pianificato in .contex/05-next-actions.md, con F:\Work\Iris_v2 come
riferimento concettuale di dominio (non di codice: mai buildato con successo, non in git).

Best practice operative:
- lavora su evidenze locali: rg/rg --files, git status, build/test reali;
- non dichiarare test passati se non lo sono stati davvero;
- non inventare nomi di file/entità/endpoint: verificarli;
- non confondere un modulo pianificato con uno implementato;
- quando tocchi un'area, aggiorna .contex/00-current-state.md e 05-next-actions.md se
  cambia lo stato di avanzamento.

Anti-allucinazione:
- verifica sempre file e stato locale prima di modificare;
- se un'informazione su Iris_v2/Iris_v3 è incerta, verificala di persona;
- se tocchi codice runtime, esegui build/test pertinenti o dichiara perché non lo hai fatto.
```
