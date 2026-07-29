# Fase 8 — Cidades Specification

## Problem Statement

O mundo hoje tem NPCs, famílias, economia e habilidades, mas nenhuma noção de cidade como
entidade — população e crescimento não têm onde aterrissar, e não existe forma de inspecionar
um NPC vivo sem abrir o banco. Fase 8 fecha os objetivos #2 (inspeção de qualquer NPC vivo por
API/CLI) e #4 (cidades crescem, encolhem e fundam assentamentos) do `ROADMAP.md`, sobre a base
fechada nas Fases 0–7. Ver `docs/roadmap/phase-08-cities.md` (tasks e critérios de fase — não
duplicados aqui) e `docs/domain/{cities,simulation-lod,world-map,society}.md` (modelo de domínio).

## Goals

- [ ] Cidade existe como entidade cujos campos agregados (população, riqueza, saúde,
      desigualdade) são **derivados** de NPCs e edifícios — nunca escritos à mão.
- [ ] Cidade cresce/encolhe por nascimento, morte, imigração e emigração; migração nunca perde
      NPC no caminho (sai de A, entra em B no mesmo tick).
- [ ] Simulation LOD (agregado ↔ materializado) mantém conservação **provada contra fonte
      independente** — não circular.
- [ ] Fundação de assentamento dispara por limiares do cenário, com tempo de organização
      declarado, sem perder população no split.
- [ ] Qualquer NPC vivo é inspecionável, somente leitura, por API e CLI.

## Out of Scope

| Feature | Reason |
|---|---|
| Guerra entre cidades, tratados, política externa | Fase 10 (ver phase-08-cities.md) |
| Cliente web de inspeção | Fase 15 |
| Memória histórica / crença sobre fundação de cidade | Fase 10 |
| Preservação de feitos/cargos ao desmaterializar (biografia histórica) | Fase 10 — nesta fase, desmaterializar só devolve estatística ao agregado |
| Diálogo com NPC via LLM | Fase 11 |
| Governo, cultura, tecnologia como sistemas simulados (eleição, pesquisa) | Fase 8 só guarda os campos/instituições como estado agregado da cidade; comportamento político é Fase 13+/`society.md` |
| Novo projeto CLI dedicado | `LivingWorld.Workers` já é o host CLI do repo (AD-020) — inspeção entra como novo subcomando, não um projeto novo |
| Persistência em Postgres | Alvo futuro (ADR-0002); Fase 8 continua em SQLite |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
|---|---|---|---|
| CLI de inspeção mora onde | `LivingWorld.Workers` ganha subcomando (`inspect-npc <id>`), reaproveitando o modo CLI já usado pelo teste de determinismo (`hash <seed> <ticks>`) | AD-020: projeto novo é decisão de arquitetura sem ganho quando `Workers` já referencia tudo | n |
| API de inspeção mora onde | `LivingWorld.Api` ganha `GET /npcs/{id}` (hoje só tem `/`); somente leitura, sem auth | `rules/database-entities.md`: entidade de persistência não vaza — API devolve DTO. Sem auth: mundo single-player local, mesma postura do resto do repo | n |
| Materialização é amostragem condicionada ou sorteio livre | Sorteio livre a partir das faixas/estatísticas agregadas da cidade nesta fase; amostragem condicionada por evento histórico específico (ex.: "perdeu alguém na fome de 10 anos atrás") fica para Fase 10 | `simulation-lod.md` descreve o ideal completo, mas fatos históricos individuais dependem do log de Fase 10 (fora de escopo aqui); a Fase 8 só precisa preservar as somas agregadas, não a narrativa | n |
| Política de materialização automática (líder, foco do observador, alvo de inspeção) | Implementa as 3 gatilhos do task 7: papel formal (líder/mestre/chefe de household), alvo de inspeção via API/CLI, e foco de "observador" tratado como o próprio alvo de inspeção (não há cliente com câmera nesta fase) | Task 7 do roadmap já lista os gatilhos; "foco do observador" sem cliente 3D/mapa (Fases 14/15) colapsa no mesmo gatilho de inspeção | n |
| Limiares de crescimento/fundação/migração | Todos os limiares (fome, moradia, segurança, concentração populacional, recurso, rota, defensabilidade, liderança, tempo de organização) são parâmetros do cenário, nunca literais em C# | `rules/eval-criteria.md` R3 — nenhum número mágico; mesmo padrão de `FamilyRules`/`EconomyRules` (Fases 5/7) | n |
| Edifícios: catálogo por período | Reusa o padrão `EconomyCatalog`/`PopulationCatalog` — `BuildingCatalog` com ids válidos por período (medieval: casa/fazenda/taverna/ferreiro/mercado/templo/castelo/moinho/porto/escola/quartel), sem nome/apresentação no engine | Convém com AD-023 (catálogos guardam só id/peso, nome é dado de cliente) e `cities.md` | n |
| Contagem independente de auditoria (task 9) | Persistida como coluna/tabela separada da propriedade `City.Population` (derivada), lida por query direta ao store — nunca via getter da entidade | Critério "conservação contra fonte independente" do roadmap exige que os dois lados não compartilhem o mesmo caminho de leitura | n |
| Concorrência | N/A — tick único-thread determinístico, já garantido pelas Fases 0/1; Fase 8 não introduz concorrência nova | `rules/simulation-determinism.md` já cobre; nada em cidades muda esse modelo |
| Auth / rate limit da API | N/A — endpoint somente leitura, mundo local, sem múltiplos usuários nesta fase | Mesma postura do `LivingWorld.Api` atual (`/` sem auth); cliente autenticado é Fase 15+ |
| Idempotência/retry | N/A — `GET` é naturalmente idempotente; não há escrita no caminho de inspeção | Task 8 exige explicitamente "somente leitura" |
| Ciclo de vida do dado agregado | Cidade nunca é deletada nesta fase, mesmo com população zero (abandono/destruição é `cities.md` mas fora dos critérios de verificação da Fase 8) | Critério de verificação do roadmap não pede remoção de cidade; manter simples evita inventar requisito |
| Observabilidade | Cobertura via os próprios critérios de verificação (conservação, round-trip, agregados recomputados) — sem métrica/log novo além do que os testes exigem | Roadmap já declara os sensores necessários; log adicional seria requisito não pedido |

**Open questions:** nenhuma — todo item acima resolvido por convenção existente do repo ou
registrado como decisão/`N/A` com justificativa.

---

## User Stories

### P1: Cidade como entidade agregada derivada ⭐ MVP

**User Story**: Como o motor de simulação, quero que cada cidade tenha população, riqueza,
saúde e desigualdade computados a partir dos NPCs/edifícios reais, para que o estado da cidade
nunca divirja do mundo que a compõe.

**Why P1**: Sem isso não há "cidade" — é a base de dado sobre a qual crescimento, migração e
fundação escrevem.

**Acceptance Criteria**:

1. WHEN o mundo carrega um cenário com cidades THEN o sistema SHALL expor `City` com
   população, governo, economia, recursos, segurança, saúde, educação, infraestrutura,
   habitação e desigualdade, todos computados a partir de NPCs e edifícios daquela cidade.
2. WHEN um campo agregado de cidade é lido a cada `N` ticks (`N` do cenário) THEN o sistema
   SHALL recomputar do zero a partir dos NPCs materializados e comparar com o valor
   incremental — divergência de uma unidade falha o teste.
3. WHEN nenhum NPC ou edifício muda entre dois ticks THEN os agregados da cidade SHALL
   permanecer byte-idênticos (nenhuma escrita manual de campo de cidade existe no código).

**Independent Test**: Rodar cenário de 1 cidade/N NPCs por alguns ticks e comparar
`City.Population` (e demais agregados) contra soma recalculada do zero sobre o store de NPCs.

---

### P1: Crescimento e encolhimento por saldo vital e migratório ⭐ MVP

**User Story**: Como projetista de cenário, quero que a população da cidade suba e desça com
nascimento, morte, imigração e emigração — e que falta de comida/moradia/segurança empurre
gente pra fora — para que o mundo reaja a escassez sem evento escrito à mão.

**Why P1**: É o mecanismo central do objetivo #4; sem ele "crescer/encolher" é só o texto do
roadmap.

**Acceptance Criteria**:

1. WHEN comida, moradia ou segurança da cidade caem abaixo do limiar do cenário THEN o
   sistema SHALL reduzir a população por emigração, em taxa proporcional ao déficit declarado
   no cenário (não um valor fixo em C#).
2. WHEN a produção de comida de uma cidade é zerada num par base/tratamento de mesma seed
   THEN a população do tratamento SHALL cair mais que a do baseline, com a diferença **maior
   que o spread** entre duas seeds do baseline (≥10 seeds, contagem de acertos — R4 de
   `rules/eval-criteria.md`).
3. WHEN um NPC decide migrar de A para B THEN o sistema SHALL remover exatamente 1 da
   população de A e adicionar exatamente 1 a B no mesmo tick — nunca um tick de NPC "no
   caminho".

**Independent Test**: Cenário base/tratamento com fome induzida; assert de queda populacional
maior no tratamento com controle de seeds.

---

### P1: Construção de edifícios por demanda e recurso ⭐ MVP

**User Story**: Como cidade simulada, quero abrir obras quando há demanda e recurso, e
consumi-los ao longo de vários ticks, para que infraestrutura seja consequência de economia,
não de spawn instantâneo.

**Why P1**: Habitação (task 2) e instituições (task 1) dependem de edifícios existirem antes.

**Acceptance Criteria**:

1. WHEN uma obra é iniciada sem o material/mão de obra declarado na receita do cenário THEN o
   sistema SHALL retornar `Failure` e deixar `Hash(world)` inalterado.
2. WHEN uma obra é iniciada com insumo suficiente THEN o sistema SHALL consumi-lo ao longo dos
   ticks declarados na receita do cenário, e o edifício só SHALL concluir com consumo total
   registrado igual à receita.
3. WHEN múltiplas obras competem pela fila de uma cidade THEN o sistema SHALL processá-las
   em ordem declarada pelo cenário (fila), nunca em ordem não-determinística.

**Independent Test**: Iniciar obra sem insumo → `Failure` + hash intacto; iniciar com insumo →
consumo tick a tick bate com a receita ao concluir.

---

### P1: Simulation LOD com conservação provada ⭐ MVP

**User Story**: Como motor, quero materializar e desmaterializar NPCs entre agregado e
detalhado sem criar ou destruir gente, para sustentar cidades grandes sem custo de simular
todo mundo por igual.

**Why P1**: É o mecanismo que permite objetivo #2 (inspeção) e #4 (cidades grandes) sem explodir
custo — e é o critério mais fácil de violar silenciosamente (roadmap chama isso de "inflação
silenciosa").

**Acceptance Criteria**:

1. WHEN um NPC agregado é materializado THEN o sistema SHALL debitar exatamente 1 do contador
   agregado da cidade e criar exatamente 1 linha de NPC no store.
2. WHEN um NPC materializado é desmaterializado THEN o sistema SHALL devolver seus atributos
   (população, e o que ele consumia/ocupava: comida, moeda, emprego, casa) ao agregado e
   remover a linha do store.
3. WHEN o mesmo NPC é materializado e depois desmaterializado sem nenhuma outra mudança
   THEN `Hash(world)` (incluindo população e somas agregadas) SHALL ser byte-idêntico ao
   estado antes da materialização.
4. WHEN `COUNT(*)` de NPCs materializados no store e o contador agregado persistido são lidos
   **sem tocar a propriedade derivada de população** THEN a soma dos dois SHALL bater com a
   população total, a cada tick, em todo horizonte testado (10 anos no gate).
5. WHEN a flag de teste desliga LOD e migração THEN `Hash(world)` após 10 anos SHALL divergir
   do mundo com LOD ligado (prova que o sistema entra na conta).

**Independent Test**: Materializar N NPCs de uma cidade, assert conservação via contagem
independente; desmaterializar, assert round-trip de hash.

---

### P1: Política de materialização por relevância ⭐ MVP

**User Story**: Como motor, quero materializar automaticamente quem tem papel, é alvo de
inspeção ou está no foco do observador, e manter o resto agregado, para equilibrar realismo e
custo sem intervenção manual.

**Why P1**: Sem política automática, materialização vira decisão manual — quebra o objetivo de
"nada de número/estado editado à mão".

**Acceptance Criteria**:

1. WHEN um NPC ocupa papel formal (líder de assentamento, mestre de ofício, chefe de
   household) THEN o sistema SHALL mantê-lo materializado enquanto ocupar o papel.
2. WHEN a API ou o CLI consulta um NPC agregado THEN o sistema SHALL materializá-lo sob
   demanda antes de responder.
3. WHEN um NPC materializado perde todo papel relevante e não é alvo de inspeção ativa
   THEN o sistema SHALL torná-lo elegível a desmaterialização (ver política de FIFO/tempo
   ocioso — parâmetro do cenário).

**Independent Test**: Consultar NPC nunca materializado via API → aparece materializado no
store logo após a chamada; conceder papel de líder a um NPC agregado → passa a persistir
materializado no tick seguinte.

---

### P1: API + CLI de inspeção somente leitura ⭐ MVP

**User Story**: Como jogador/operador, quero consultar identidade, família, profissão,
atributos, rotina e memórias de qualquer NPC vivo por API e por CLI, para inspecionar o mundo
sem abrir o banco (objetivo #2).

**Why P1**: É a entrega literal do objetivo #2 do `ROADMAP.md`.

**Acceptance Criteria**:

1. WHEN `GET /npcs/{id}` é chamado com um NPC vivo THEN o sistema SHALL responder identidade,
   família, profissão, atributos, rotina e memórias daquele NPC, sem nenhuma escrita no mundo.
2. WHEN `LivingWorld.Workers inspect-npc <id>` é chamado com um NPC vivo THEN o sistema SHALL
   imprimir o mesmo conjunto de dados que a API, lendo do mesmo caminho (nenhuma lógica
   duplicada entre API e CLI).
3. WHEN `{id}` não corresponde a NPC vivo THEN o sistema SHALL responder 404 (API) / código de
   saída não-zero (CLI), sem lançar exceção não tratada.
4. WHEN um mundo de 100 NPCs é iterado por completo (todos os vivos, sem sorteio) THEN a
   resposta da API SHALL bater campo a campo com o estado do motor no mesmo tick — os campos
   do DTO enumerados por reflexão, teste falha se algum campo ficar sem comparação.

**Independent Test**: Cenário de 100 NPCs; loop de teste itera todos os IDs vivos, compara
DTO vs estado do motor campo a campo via reflexão.

---

### P2: Migração multifatorial (emprego, comida, segurança, laços familiares)

**User Story**: Como NPC, quero decidir migrar pesando emprego, comida, segurança e laços
familiares — nessa ordem de peso típica — para que padrões de migração sejam plausíveis (posso
ficar num lugar ruim por causa da família).

**Why P2**: Task 4 do roadmap; incrementa realismo sobre o P1 de crescimento/encolhimento, mas
o objetivo #4 já é parcialmente atendido sem os 4 fatores completos.

**Acceptance Criteria**:

1. WHEN um NPC avalia migrar THEN o sistema SHALL pesar emprego, comida, segurança e laços
   familiares conforme os pesos declarados no cenário (não uma ordem fixa em C#).
2. WHEN um household migra em conjunto THEN o sistema SHALL mover todos os membros no mesmo
   tick, preservando `HouseholdId`.

**Independent Test**: Cenário com NPC de família em cidade ruim vs sem família em cidade ruim —
o segundo migra mais cedo (contagem de acertos em seeds, R4).

---

### P2: Fundação de assentamento

**User Story**: Como grupo de NPCs que bate os limiares do cenário (concentração, recurso,
rota, defensabilidade, liderança), quero fundar um novo assentamento após o tempo de
organização declarado, para que o mapa cresça organicamente.

**Why P2**: Task 5; depende de crescimento (P1) e migração (P2 acima) já funcionando.

**Acceptance Criteria**:

1. WHEN todos os limiares de fundação do cenário são satisfeitos THEN o sistema SHALL fundar
   o novo assentamento em `≤ K` ticks, `K` = tempo de organização declarado no cenário.
2. WHEN o assentamento é fundado THEN a soma das populações de todas as cidades antes e
   depois do split SHALL ser idêntica (o grupo migra junto; a cidade-mãe perde exatamente
   ele).

**Independent Test**: Cenário com todos os limiares satisfeitos no tick 0; assert de fundação
em `≤ K` ticks e soma de população constante no split.

---

## Edge Cases

- WHEN uma cidade fica com população zero THEN o sistema SHALL manter a entidade `City`
  existente (abandono/destruição fica fora do escopo desta fase — ver Out of Scope).
- WHEN dois NPCs de households diferentes decidem migrar para a mesma cidade no mesmo tick
  THEN ambos SHALL chegar sem conflito (a capacidade da cidade destino não bloqueia chegada
  nesta fase — moradia insuficiente é pressão que gera *nova* emigração, não uma trava de
  entrada).
- WHEN a API é consultada por um NPC morto THEN o sistema SHALL responder 404, nunca dados de
  um cadáver (inspeção é só de NPC vivo, conforme task 8).
- WHEN uma obra está em andamento e a cidade perde o insumo por outro consumidor concorrente
  no meio do processo THEN a obra SHALL pausar (não reverter progresso já pago) até o insumo
  voltar, nunca concluir sem o total da receita.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
|---|---|---|---|
| CITY-01 | P1: Cidade como entidade agregada derivada | Tasks (T1,T5,T8) | In Tasks |
| CITY-02 | P1: Crescimento e encolhimento | Tasks (T11,T19) | In Tasks |
| CITY-03 | P1: Construção de edifícios | Tasks (T3,T10) | In Tasks |
| CITY-04 | P1: Simulation LOD com conservação | Tasks (T1,T9,T17,T18,T22) | In Tasks |
| CITY-05 | P1: Política de materialização | Tasks (T9) | In Tasks |
| CITY-06 | P1: API + CLI de inspeção | Tasks (T14,T15,T16,T21) | In Tasks |
| CITY-07 | P2: Migração multifatorial | Tasks (T12) | In Tasks |
| CITY-08 | P2: Fundação de assentamento | Tasks (T13,T20) | In Tasks |
| CITY-09 | Contagem independente de auditoria (todas as stories acima) | Tasks (T8,T17) | In Tasks |

**ID format:** `CITY-NN`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 9 total, 9 mapped to tasks (T1-T22), 0 unmapped

---

## Success Criteria

- [ ] `bash scripts/verify.sh` limpo (check-docs + build + lint + test) com os novos sistemas.
- [ ] Todos os critérios de `docs/roadmap/phase-08-cities.md` provados por teste automatizado
      (conservação, round-trip de hash, agregados recomputados, fundação, fome com controle,
      migração sem perda, obra sem material, inspeção exaustiva, LOD entrando no hash).
- [ ] `bash scripts/test.sh --filter Category=Scenario` limpo (100 anos, nightly).
- [ ] `STATE.md` atualizado com Fase 8 fechada e handoff para Fase 9 (Escala).
