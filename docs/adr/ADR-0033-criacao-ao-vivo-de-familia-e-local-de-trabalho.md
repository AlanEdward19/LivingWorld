# ADR-0033: Criação ao vivo de família e local de trabalho (rotas de autoria de admin)

- **Status**: aceito
- **Data**: 2026-10-15
- **Decisores**: Alan (issue #46, fatia 4)

## Contexto
`AuthoredHouseholds` e `Workplaces` só valem ao criar o mundo (ADR-0024). Para criar gente e prédio com o mundo rodando, sem recriá-lo,
é preciso um comando externo no molde de `WorldAuthoringService`, com validação igual à do cenário e rastro no log causal.

## Decisão
- **Rotas**: `POST /authoring/households` e `POST /authoring/workplaces`. O corpo é **o mesmo JSON do cenário** (um item de
  `AuthoredHouseholds[]` ou de `Workplaces[]`), validado pelos mesmos loaders (`AuthoredHouseholdsLoader`, `EconomyScenarioLoader`);
  zero DTO novo, `Appearance`/`Skills` da fatia 2 valem sem mudança. Resposta 200 traz os ids criados; 400 `{ error }` com o campo.
- **Acesso**: exigem `X-Admin-Token` (ADR-0015; 403 sem credencial) e entram na auditoria de admin. Pausa **de admin** ativa recusa (409);
  a pausa comum não.
- **Efeito**: aplicado na hora, no mundo do host, como os demais comandos de autoria. Família: assentamento nomeado (ou a cidade mais antiga),
  cultura mais comum dali, a mesma colocação de casa e agenda de óbito/despertar da geração inicial (`PopulationSeeder.SeedAuthored`);
  com economia ligada, recebem o mesmo pecúlio inicial, **cunhado** (`Mint`) para a conservação do dinheiro continuar valendo.
  Local de trabalho: a colocação do cenário foi extraída para `WorkplacePlacer` (cenário e rota usam a mesma); o caixa inicial é cunhado.
- **Origem**: eventos novos no fim de `WorldEventKind`: `AuthoredImmigration` (`domicilioId|cidadeId|npcIds`) e `AuthoredWorkplaceFounded`
  (`localId|cidadeId|tipo|x|y`), apresentados em `LivingEventPresentationCatalog`. Rejeição não escreve no mundo (nem evento): o motivo vai só para a auditoria de admin, e o hash fica igual.
- **Hash**: criação válida muda o hash e aparece no log; inválida deixa o hash igual. Sem chamada à rota nada muda.

## Alternativas consideradas
- Rotas abertas como `/authoring/*` — criar gente é mais forte que retocar personalidade; o token barra o uso acidental.
- DTO próprio — duplica a validação e sai de sincronia com o cenário.
- Fila drenada pelo laço do tick — mais isolada, mas o molde atual já aplica na hora e ninguém lê o mundo entre a chamada e o próximo tick.

## Consequências
- **Positivas**: o autor cria família e oficina no mundo vivo; elas vivem pelas regras de sempre (comem, trabalham, envelhecem).
- **Negativas / trade-offs**: sem trava entre a thread HTTP e o laço do tick (como as demais rotas de autoria); pausa comum não protege.
- **Follow-ups**: fila de comandos por tick se a corrida aparecer; prédio de plantas livres (fatia 5).
