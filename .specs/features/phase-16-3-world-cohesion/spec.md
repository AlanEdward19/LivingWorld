# Fase 16.3 — Living World Cohesion Specification

Fonte: `Fase pós-16.2 — Living World Cohesion_ Causalidade, Agência, Atenção, Complexidade e Experiência.md` (doc do usuário, 162 seções — referenciadas abaixo como `doc#N`).

## Problem Statement

As Fases 1–16.2 entregaram muitos sistemas (needs, economia, skills, household, relacionamentos, memória, crença, history, powers, LOD) mas o survey de arquitetura (2026-08-25) confirmou o diagnóstico do doc: a maior parte deles **não participa da decisão do Agent**. `BehaviorDecisionSystem.SelectByUtility` recebe `WorldState` inteiro (não um `DecisionContext` escopado), `WorldEvent` é um record flat sem cadeia causal (`EventId`/`CauseEventId`/`RootCauseEventId`), Memory/Belief/Relationships são `PRESENTATION_ONLY` (zero leitura no loop de decisão), Powers rodam num pipeline paralelo (`ExtraordinaryInvocationEngine`) fora da utility normal, e não existe nenhum sistema de corpo/saúde. Resultado: sistemas coexistem sem produzir consequências reais uns nos outros — "vários sistemas simulando o mesmo mundo" em vez de "um mundo causal" (doc#5).

## Goals

- [ ] Decisão do Agent passa a ler um `DecisionContext` escopado (needs, body, memória relevante, crenças, relações, household, economia conhecida, powers) em vez de `WorldState` global — auditável e sem onisciência (doc#41-42).
- [ ] Eventos carregam proveniência causal (`CauseEventId`/`RootCauseEventId`/`SourceSystem`) suficiente para reconstruir cadeias como `HarvestReduced → ... → EmploymentAffected` sem função `CreateFamineStory()` (doc#25-29, #154).
- [ ] Memory/Belief/Relationships/Household/Body deixam de ser presentation-only: cada um tem pelo menos um consumidor real na decisão (doc#45-47, #66-69).
- [ ] Todos os 18+ `IExtraordinaryMechanic` entram como Opportunity/Capability scorável no mesmo `SelectByUtility` que Eat/Work/etc — sem "Power AI" separada (doc#61-63).
- [ ] Redecisão cai: Intent persiste entre wakes e só reconsidera por evento relevante roteado (Attention Router), não full re-scan a cada wake (doc#48-59).
- [ ] Cenário `test-living-village` demonstra determinística e sem scripting narrativo uma cadeia cross-system de pelo menos 5 sistemas (doc#88-97, #154-155).
- [ ] Baseline de performance (Fase 9) não regride sem justificativa documentada; golden hashes atualizados com motivo em `STATE.md` (doc#99, #162).

## Out of Scope

Explicitamente excluído. Documentado para prevenir scope creep.

| Feature | Reason |
| --- | --- |
| Web UI overhaul (World Explorer, Why?/Causal Explorer, Timeline/Life/Follow, semantic zoom, Experience/Debug mode) | Decisão do usuário 2026-08-25: web vira spec própria em sequência (`phase-16-3-web` / "16.3-web"), depois que o core causal (backend) estiver de pé. Esta spec entrega só a base de dados que a web vai consumir (eventos com proveniência, decision traces), não os endpoints/telas novos. |
| Fase 25 (Player = Agent com decisão humana) | doc#159 — preservar só o pipeline `World → Perception → DecisionContext → Decision Source → Intent → Action → World` já genérico; não implementar input humano. |
| Novo Relationship/Memory/Belief/History/Economy system em paralelo | doc#149 — REUSE > EXTEND > REFACTOR > REPLACE > CREATE (doc#6); estes sistemas já existem, esta fase os INTEGRA à decisão, não os substitui. |
| Novo "Power engine" / GOAP global / Behavior Tree global / LLM controlando Agent / Storyteller forçando outcome | doc#149, #73, #77-80 — Utility AI existente continua árbitro único; LLM só narra, nunca decide. |
| Combate multi-round, Fauna/Flora autônoma, temperatura sazonal | Já é a Fase 16.4 (ex-16.3, renomeada 2026-08-25) — worktree/branch próprios, não tocar aqui. |
| Full ECS rewrite / 3D engine | doc#149 — fora de escopo de qualquer fase deste ciclo. |
| Corpo/Saúde além do mínimo causal (fadiga fina, doenças, gordura corporal, envelhecimento físico detalhado) | Decisão do usuário 2026-08-25: criar só um sistema mínimo (altura/peso/massa muscular + 1-2 consumidores reais). Profundidade adicional é `FUTURE_DEPENDENCY` documentada, não implementada agora. |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Escopo de Body/Health | Sistema mínimo causal novo: `Height`, `Weight`, `MuscleMass` em `Npc`, alimentando `WorkCapacityMultiplier` (produtividade/skills) e `MovementCostMultiplier` (viagem/`AttributeMechanic`-style). Sem fadiga fina, doença ou composição corporal detalhada. | Escolha explícita do usuário — evita virar fase própria, mas cumpre doc#10-12, #46, #71, #110 com consumidores reais mínimos. | y |
| Escopo de Powers→Utility | Integração FULL: todos os `IExtraordinaryMechanic` do registry viram candidatos scoráveis em `SelectByUtility` (Opportunity/Capability/Cost/Risk), substituindo o caminho especial atual (só `ControlMechanic.TryDelegatedAction` fazia ponte). | Escolha explícita do usuário. | y |
| Escopo Web | Fora desta spec inteiramente — vira spec separada depois. | Escolha explícita do usuário ("web vira 16.3-web"). | y |
| `DecisionContext` — quão "congelado" fica no momento do wake | Construído on-demand no momento do wake (dirty-flag por categoria, doc#60), não persistido entre ticks; não faz parte do golden hash canônico (é derivado, reconstituível). | Consistente com doc#60 ("Dirty Decision Context") e com o padrão já usado por `LazyNeed`/scoped read-models na API (survey #9). | n — assumido, revisar em Design se `NpcWakeScheduler` exigir outro shape |
| Granularidade de `CauseEventId`/`RootCauseEventId` | Todo evento relevante ganha `CauseEventId` opcional (nullable); `RootCauseEventId` é derivado (segue a cadeia até achar um evento sem causa), não armazenado duas vezes por evento — calculado sob demanda (doc#29 já pede "sem persistir grafo global completo"). | Evita duplicar dado e mantém consistente com doc#29/#154. | n — assumido, revisar custo em Design |
| Coalescing de eventos ruidosos (doc#82) | Aplicado só a eventos "write-many-per-tick" já identificados como ruído no survey (ex.: nenhum hoje, mas thresholds futuros tipo preço) — não há candidato concreto nesta fase; documentado como `FUTURE_DEPENDENCY`, sem implementação obrigatória em P1-P3 desta spec. | Doc#82 é uma regra geral, não pede uma feature concreta agora; nenhum evento existente hoje demonstrou esse padrão no survey. | n — assumido |
| Powers full integration — regressão de comportamento | Cada mechanic migrado para `SelectByUtility` deve manter o resultado observável de cenários existentes com powers (golden hash) OU a mudança de golden é documentada com AD explícito (mesmo padrão de AD-065/AD-069). | Doc#158/#162 — "nenhuma regressão de determinismo pode ser aceita silenciosamente". | y (regra geral já em `STATE.md`) |
| Corpo/Saúde — origem dos valores (`Height`/`Weight`/`MuscleMass`) | Gerados na criação do NPC via RNG semeado (mesmo padrão de `CityId`/atributos existentes, AD-020/AD-068), com distribuição simples (normal truncada) parametrizável por cenário; sem herança genética completa nesta fase (herança já existe para Powers via `PowerInheritanceRules`, não estendida a Body agora). | Evita inventar um sistema de genética completo; mantém consistência de RNG determinístico já estabelecida no projeto. | n — assumido, revisar em Design |

**Open questions:** nenhuma sem marcação — todas resolvidas acima ou log de assumption com racional.

---

## User Stories

### P1a: Causal Event Provenance ⭐ MVP

**User Story**: Como desenvolvedor/Verifier do LivingWorld, quero que todo evento relevante carregue de onde veio, para poder reconstruir "por que isso aconteceu?" sem inspecionar código.

**Why P1**: É a infraestrutura que toda outra história desta fase depende (Decision Trace, Attention Router, cenário de validação) — sem proveniência causal não dá pra provar as cadeias cross-system exigidas no DoD.

**Acceptance Criteria**:

1. WHEN um evento é publicado por qualquer sistema THEN o registro do evento SHALL conter `EventId` (novo, único), `Tick`, `SourceSystem`, e `CauseEventId` opcional (nullable) apontando para o evento imediatamente anterior na cadeia, quando aplicável.
2. WHEN código consumidor pede a causa raiz de um evento THEN o sistema SHALL resolver `RootCauseEventId` percorrendo a cadeia de `CauseEventId` até o primeiro evento sem causa, sem exigir um grafo persistido à parte.
3. WHEN um evento não tem uma causa identificável no sistema que o produziu (ex.: evento externo/gerado por cenário) THEN `CauseEventId` SHALL ser `null` e o evento é tratado como raiz da própria cadeia.
4. WHEN `WorldEvent` (ou seu sucessor) é serializado/persistido THEN os campos novos SHALL ser compatíveis com o pipeline de persistência existente (`EventLogRecord`) sem quebrar leitores antigos que ignoram os campos novos.
5. WHEN dois eventos com o mesmo seed/cenário são reproduzidos THEN a cadeia causal resultante (`CauseEventId`→`RootCauseEventId`) SHALL ser idêntica (determinismo preservado).

**Independent Test**: Injetar um evento sintético em teste, publicar 2-3 eventos derivados dele manualmente com `CauseEventId` setado, e verificar que a resolução de `RootCauseEventId` retorna o evento raiz correto.

---

### P1b: Decision Context Integration ⭐ MVP

**User Story**: Como o mundo simulado, quero que a decisão de cada Agent use só o que ele legitimamente sabe/tem (needs, corpo, memórias relevantes, crenças, relações, household, economia conhecida), para que decisões diferentes emerjam de vidas diferentes em vez de acesso onisciente ao `WorldState`.

**Why P1**: É o núcleo do DoD da fase (doc#154-155) — sem isso, Memory/Belief/Relationships continuam `PRESENTATION_ONLY` e nenhuma cadeia causal chega a influenciar comportamento.

**Acceptance Criteria**:

1. WHEN `BehaviorDecisionSystem` avalia candidatos de ação para um NPC THEN a função de scoring SHALL receber um `DecisionContext` (tipo novo, escopado ao NPC) em vez de `WorldState` completo — chamadas ainda existentes tipo `Score(agent, world)` que acessem dados fora do escopo do Agent SHALL ser substituídas ou o acesso indevido documentado como exceção justificada.
2. WHEN o `DecisionContext` é construído para um NPC THEN ele SHALL incluir, quando aplicável e disponível: needs atuais, resumo corporal (Body/Health, ver P1c), memórias relevantes recuperadas via `MemoryRecall`, crenças relevantes via `NpcBeliefQuery`/`HistoryBeliefQuery`, relações relevantes via `RelationshipSystem`, estado do household, informação econômica conhecida (não o mercado global), intent atual, e capacidades/powers disponíveis.
3. WHEN uma memória ou crença relevante existe para a decisão em curso (ex.: "foi traído por X", "acredita em escassez") THEN ela SHALL aparecer no `DecisionContext` e SHALL ser capaz de alterar o resultado do scoring em pelo menos um cenário de teste (memória/crença diferente → decisão diferente, doc#92/#43).
4. WHEN uma relação relevante existe (ex.: trust alto com um household member) THEN ela SHALL poder influenciar o candidato vencedor em pelo menos um cenário de teste (doc#68/#93).
5. WHEN o household muda de composição (ex.: perde o provedor principal) THEN pressões/oportunidades subsequentes na decisão SHALL refletir a mudança (doc#67/#94) sem exigir uma função de "criar crise familiar" hardcoded.
6. WHEN não há memória/crença/relação relevante disponível para uma decisão THEN o `DecisionContext` SHALL simplesmente omitir esses fatores (sem erro, sem dado inventado) — Agent decide com o que tem.

**Independent Test**: Dois NPCs com estado material idêntico mas memória (ou crença, ou relação) relevante diferente produzem decisões diferentes no mesmo tick, comprovável via teste determinístico com dois NPCs golden-seeded (doc#92/#93).

---

### P1c: Body/Health Minimal Causal System ⭐ MVP

**User Story**: Como o mundo simulado, quero que Agents tenham um corpo mínimo (altura, peso, massa muscular) que realmente altere capacidade física, para que dois Agents com mesma skill/emprego possam ter performance diferente por causa do corpo.

**Why P1**: Decisão explícita do usuário — cumpre doc#10-12/#46/#71/#110 sem virar fase própria; é pré-requisito para a cadeia de "trabalho pesado → músculo → produtividade" do DoD (doc#155).

**Acceptance Criteria**:

1. WHEN um NPC é criado THEN ele SHALL receber `Height`, `Weight`, `MuscleMass` gerados via RNG semeado determinístico (mesmo padrão de outros atributos gerados, AD-020/AD-068), com distribuição parametrizável por cenário.
2. WHEN a produtividade/capacidade de trabalho de um NPC é calculada para uma ação físicamente relevante (ex.: trabalho pesado) THEN `MuscleMass` (combinado com skill e saúde, quando disponível) SHALL entrar no cálculo via um `WorkCapacityMultiplier` — dois NPCs com mesma skill e emprego mas `MuscleMass` diferente SHALL produzir performance/eficiência diferente.
3. WHEN um NPC se desloca (viagem/movimento) THEN `Weight` (e/ou `Height`) SHALL poder influenciar `MovementCostMultiplier`, análogo ao padrão já existente em `AttributeMechanic` para reaction speed/perception.
4. WHEN trabalho físico pesado é realizado repetidamente por um NPC ao longo do tempo THEN `MuscleMass` SHALL poder aumentar (lentamente, categoria SLOW conforme doc#19), sem exigir atualização a cada tick.
5. WHEN `Height`/`Weight`/`MuscleMass` não têm nenhum consumidor implementado ainda em algum contexto específico (ex.: equipment compatibility, combat) THEN esse contexto SHALL ser documentado como `FUTURE_DEPENDENCY` no relatório de auditoria (ver P3), não implementado silenciosamente como stub sem uso.

**Independent Test**: Dois NPCs golden-seeded com mesma skill/emprego/localização mas `MuscleMass` diferente produzem `WorkCapacityMultiplier` diferentes e, quando aplicável, throughput de produção diferente ao final de um período simulado (doc#95).

---

### P1d: Powers Full Utility Integration ⭐ MVP

**User Story**: Como o mundo simulado, quero que usar um poder seja só mais uma opção que a Utility AI compara com Walk/Buy/Work/etc (tempo, risco, custo, confiabilidade, urgência da necessidade), para que Powers deixem de ser um "caminho especial" e passem a fazer parte da mesma decisão de todo mundo.

**Why P1**: Decisão explícita do usuário (integração FULL, não subset) — cumpre doc#61-64 integralmente.

**Acceptance Criteria**:

1. WHEN um NPC tem uma capacidade extraordinária disponível e aplicável ao contexto de decisão atual (ex.: `ReachDestinationUrgently` com Teleport disponível) THEN essa capacidade SHALL aparecer como candidato scorável em `SelectByUtility`, ao lado de Walk/Ride/etc, com utility calculada a partir de tempo, risco, custo, confiabilidade e urgência da necessidade (doc#62).
2. WHEN QUALQUER `IExtraordinaryMechanic` do registry (todos os 18+) é aplicável ao NPC e ao contexto THEN ele SHALL ser exposto ao loop de utility via uma interface/adapter comum — nenhum mechanic fica de fora do mecanismo de exposição (ainda que, para alguns mechanics, a "opção" resultante raramente vença por custo/risco altos — o ponto é estar no conjunto de candidatos, não sempre vencer).
3. WHEN um Agent usa um poder através da decisão normal (não mais via `ControlMechanic.TryDelegatedAction` como único bridge) THEN o resultado observável SHALL disparar `PowerInvoked` e as consequências normais do mundo (doc#63): observadores percebem, conhecimento/crenças mudam quando aplicável, reputação/relações podem mudar.
4. WHEN um cenário existente que já usa powers (ex.: golden test de 16.1/16.2) é re-executado após a migração THEN o resultado observável SHALL ser idêntico (golden hash preservado) OU a divergência SHALL ser documentada com um AD explícito em `STATE.md` explicando por quê (mesmo padrão de AD-065/AD-069) — nunca uma regressão silenciosa.
5. WHEN um NPC não tem uma capacidade extraordinária aplicável ao contexto THEN nenhum candidato de power SHALL ser oferecido (sem falso-positivo de oportunidade) — teste comparando Agent-com-capacidade vs Agent-sem-capacidade SHALL mostrar diferença só nas oportunidades disponíveis (doc#97).
6. WHEN a decisão possessão-especial (`ControlMechanic.TryDelegatedAction`) ainda existe pós-migração THEN ela SHALL continuar funcionando para o caso de possessão (que é estruturalmente diferente — outro Decision Source, não uma Opportunity comum) — migração não quebra possessão.

**Independent Test**: Cenário com Agent A (tem Teleport) e Agent B (não tem), mesma pressão `ReachDestinationUrgently`, mesmo contexto material — A escolhe Teleport quando o utility score compensa, B nunca considera a opção porque ela nem aparece nos candidatos (doc#97).

---

### P2a: Intent Persistence & Attention Router

**User Story**: Como o mundo simulado, quero que um Agent continue sua intenção atual até um motivo real de reconsiderar aparecer (não recalcule tudo a cada wake), e que só Agents relevantes acordem por causa de um evento (não o mundo inteiro).

**Why P2**: Reduz redecisão/CPU (doc#48-59) — importante para a meta de performance da fase, mas depende de P1a/P1b estarem prontos (proveniência causal + decision context) para funcionar de forma correta; não é MVP porque o sistema já funciona hoje via `NpcWakeScheduler` (mesmo que sem `CurrentIntent` persistente formal).

**Acceptance Criteria**:

1. WHEN um NPC decide uma ação/intent THEN o estado SHALL registrar `CurrentIntent`, `IntentStartedTick`, `IntentTarget` (quando aplicável), `IntentStatus` (Active/Completed/Invalidated), análogo ao já existente `CurrentAction`/`ActionStartedAtTick` mas em nível de intent (não só ação imediata).
2. WHEN uma ação local dentro de um plano falha (ex.: vendedor indisponível) THEN o sistema SHALL tentar alternativas dentro do mesmo Intent (outro vendedor, pedir a household member, usar estoque) antes de invalidar o Intent inteiro (doc#51-52).
3. WHEN um evento relevante ocorre no mundo (ex.: preço de pão sobe 1%) THEN o Attention Router SHALL identificar quais NPCs específicos precisam reconsiderar (com base em critérios doc#59: localização, household, relacionamento, dependência do intent atual, conhecimento, dependência econômica, condição física, magnitude, urgência, ameaça, interação de capacidade) e agendar wake SÓ para eles — não a cidade inteira.
4. WHEN nenhum evento relevante ocorre para um NPC e seu Intent continua válido THEN ele NÃO SHALL ser re-avaliado a cada tick (mantém o comportamento já existente de `NpcWakeBatch`/`NpcWakeScheduler`, agora gated também por relevância de Intent, não só threshold de need).
5. WHEN um Intent é completado, invalidado, ou um `ScheduledWake` chega THEN o NPC SHALL reconsiderar via `SelectByUtility` com `DecisionContext` fresco (dirty-flag por categoria, doc#60) — reconstrução não recomputa categorias que não mudaram desde o último wake.

**Independent Test**: Comparar "full reconsideration" vs "event-driven attention" no mesmo cenário determinístico — mesmo resultado canônico final, com número de decisões/wakeups mensuravelmente menor no modo event-driven (doc#98).

---

### P2b: Pressure / Opportunity Formalization

**User Story**: Como desenvolvedor, quero que "por que agir?" (Pressure) e "o que posso fazer?" (Opportunity) sejam camadas derivadas explícitas sobre o estado já existente, para que a decisão fique legível e componha, sem duplicar dados (Hunger vs HungerPressure vs HungerUrgency).

**Why P2**: Formaliza o que P1b/P1d já produzem funcionalmente — é sobre tornar a lógica explicável (doc#55, #114-115), não sobre nova causalidade; por isso vem depois do P1.

**Acceptance Criteria**:

1. WHEN o `DecisionContext` de um NPC é construído THEN o sistema SHALL derivar uma lista de `Pressure`s ativas (ex.: `AcquireFood`, `EarnIncome`, `ProtectHousehold`) a partir de needs/household/relações/ameaças já existentes — sem introduzir um campo canônico novo redundante por need (doc#33).
2. WHEN uma Pressure complexa como `ProtectHouseholdPressure` é calculada THEN ela SHALL poder combinar múltiplos fatores (dependentes, força de relação, recursos do household, nível de ameaça, personalidade, saúde, capacidades) em vez de depender de uma única variável (doc#34).
3. WHEN o `DecisionContext` é construído THEN o sistema SHALL derivar uma lista de `Opportunity`s conhecidas (ex.: `FoodAtMarket`, `NearbyJob`, `PotentialPartner`, `ExtraordinaryCapability`) filtradas pelo que o Agent realmente conhece/percebe/pode alcançar fisicamente/tem permissão social — nunca oportunidades que o Agent não conhece via os sistemas de knowledge/perception existentes (doc#38-39).
4. WHEN uma decisão importante é tomada THEN o sistema SHALL permitir identificar top positive/negative factors, blocking factors, e alternativas conhecidas (Decision Trace, doc#55/#84) — sem exigir que a UI normal mostre números (isso é dado exposto, não front-end).

**Independent Test**: Para um cenário com Pressure `AcquireFood` alta e Opportunities conhecidas variando (mercado com/sem estoque), o candidato vencedor muda de forma explicável e seus fatores aparecem no Decision Trace.

---

### P3: Diagnostics, Metrics & Vertical Validation Scenario

**User Story**: Como Verifier/desenvolvedor, quero métricas de causalidade e um cenário vertical determinístico para provar que a fase entregou o que promete, sem depender de inspeção manual de código.

**Why P3**: É a camada de prova/observabilidade sobre o que P1/P2 constroem — só faz sentido medir depois que existe o que medir; mas é obrigatória para fechar a fase (doc#162), não opcional.

**Acceptance Criteria**:

1. WHEN a auditoria da fase é executada THEN o sistema SHALL produzir `docs/audits/living-world-cohesion-audit.md` com System Integration Matrix (doc#22) e Attribute Integration Matrix (doc#23) cobrindo pelo menos os sistemas/atributos tocados por esta fase (Events, DecisionContext, Body, Memory, Belief, Relationships, Household, Powers, Intent, Attention).
2. WHEN um evento e sua cadeia causal existem THEN o sistema SHALL expor uma função/query capaz de calcular `CausalDepth(event)` e `SystemsTouchedByCausalChain(event)` (doc#30-31), utilizável em testes/debug/profiling (não em produção de drama).
3. WHEN uma cadeia causal entra em ciclo (A muda B muda A muda B...) THEN o sistema SHALL abortar deterministicamente respeitando um `iteration budget` configurável, sem loop infinito (doc#81 — Event Storm Protection).
4. WHEN o cenário `test-living-village` (~40 Agents, 10 Households, 1 Settlement, com Farmers/Baker/Blacksmith/Merchant/Guards/Workers, Food/Employment/Markets/Relationships/Memory/Beliefs/Skills/Body/Family, Powers opcional) roda com um choque determinístico (`harvest output -30%`) THEN o resultado SHALL mostrar uma cadeia causal legítima cross-system (`HarvestReduced → FoodStockReduced → PriceIncreased → PurchaseFailed → HungerCritical → IntentChanged → EmploymentAffected` ou equivalente) tocando pelo menos 5 sistemas distintos, sem nenhuma função equivalente a `CreateFoodCrisis()`/`MakeXHungry()`/`ForceXToLeaveWork()` no código (doc#88-91, #154).
5. WHEN as métricas de decisão são coletadas em `test-living-village` THEN o sistema SHALL reportar pelo menos: decisions/agent-day, wakeups/agent-day, % wakeups que mudam intent, causal depth médio/p95/máximo, cross-system causal chains observadas, atributos sem consumidor (doc#85).
6. WHEN a fase é fechada THEN `bash scripts/verify.sh` SHALL estar verde e `STATE.md` SHALL ser atualizado com baseline anterior/posterior, métricas novas, golden hashes alterados e por quê, ADRs criados (doc#162).

**Independent Test**: Rodar `test-living-village` com e sem o choque de harvest, comparar métricas e cadeia causal capturada contra o esperado documentado no teste.

---

## Edge Cases

- WHEN um `DecisionContext` é construído para um NPC recém-criado sem memórias/crenças/relações ainda THEN o sistema SHALL retornar um `DecisionContext` válido com essas seções vazias (não erro, não crash).
- WHEN a resolução de `RootCauseEventId` encontra um ciclo de `CauseEventId` (bug futuro) THEN o sistema SHALL abortar a resolução com um limite de profundidade (não travar).
- WHEN um `IExtraordinaryMechanic` migrado para o loop de utility lança exceção ao ser avaliado como candidato THEN a falha SHALL ser isolada a esse candidato (ele não entra na comparação) sem derrubar a avaliação dos demais candidatos do NPC.
- WHEN um evento tem `SourceSystem` desconhecido/não mapeado (código legado não migrado) THEN o campo SHALL aceitar um valor `"Unknown"` explícito em vez de falhar a publicação do evento — mas o relatório de auditoria (P3-AC1) SHALL listar esses casos para migração futura.
- WHEN `Weight`/`Height`/`MuscleMass` estão fora de qualquer faixa fisiologicamente plausível por bug de geração THEN o sistema SHALL clampar na criação do NPC (mesmo padrão de outros atributos gerados por RNG) em vez de propagar valores absurdos para `WorkCapacityMultiplier`.
- WHEN dois eventos ocorrem no mesmo tick com potencial ambiguidade de ordem causal THEN o sistema SHALL usar a mesma utilidade de escolha determinística por hash já existente (`src/LivingWorld.Simulation/Extraordinary` — deterministic hash-based choice, ver commit `d3fc36b`) para desempate, preservando reprodutibilidade.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| COH-01 | P1a: Causal Event Provenance | Tasks | In Tasks |
| COH-02 | P1a: Causal Event Provenance | Tasks | In Tasks |
| COH-03 | P1a: Causal Event Provenance | Tasks | In Tasks |
| COH-04 | P1a: Causal Event Provenance | Tasks | In Tasks |
| COH-05 | P1a: Causal Event Provenance | Tasks | In Tasks |
| COH-11 | P1b: Decision Context Integration | Tasks | In Tasks |
| COH-12 | P1b: Decision Context Integration | Tasks | In Tasks |
| COH-13 | P1b: Decision Context Integration | Tasks | In Tasks |
| COH-14 | P1b: Decision Context Integration | Tasks | In Tasks |
| COH-15 | P1b: Decision Context Integration | Tasks | In Tasks |
| COH-16 | P1b: Decision Context Integration | Tasks | In Tasks |
| COH-21 | P1c: Body/Health Minimal Causal System | Tasks | In Tasks |
| COH-22 | P1c: Body/Health Minimal Causal System | Tasks | In Tasks |
| COH-23 | P1c: Body/Health Minimal Causal System | Tasks | In Tasks |
| COH-24 | P1c: Body/Health Minimal Causal System | Tasks | In Tasks |
| COH-25 | P1c: Body/Health Minimal Causal System | Tasks | In Tasks |
| COH-31 | P1d: Powers Full Utility Integration | Tasks | In Tasks |
| COH-32 | P1d: Powers Full Utility Integration | Tasks | In Tasks |
| COH-33 | P1d: Powers Full Utility Integration | Tasks | In Tasks |
| COH-34 | P1d: Powers Full Utility Integration | Tasks | In Tasks |
| COH-35 | P1d: Powers Full Utility Integration | Tasks | In Tasks |
| COH-36 | P1d: Powers Full Utility Integration | Tasks | In Tasks |
| COH-41 | P2a: Intent Persistence & Attention Router | Tasks | In Tasks |
| COH-42 | P2a: Intent Persistence & Attention Router | Tasks | In Tasks |
| COH-43 | P2a: Intent Persistence & Attention Router | Tasks | In Tasks |
| COH-44 | P2a: Intent Persistence & Attention Router | Tasks | In Tasks |
| COH-45 | P2a: Intent Persistence & Attention Router | Tasks | In Tasks |
| COH-51 | P2b: Pressure / Opportunity Formalization | Tasks | In Tasks |
| COH-52 | P2b: Pressure / Opportunity Formalization | Tasks | In Tasks |
| COH-53 | P2b: Pressure / Opportunity Formalization | Tasks | In Tasks |
| COH-54 | P2b: Pressure / Opportunity Formalization | Tasks | In Tasks |
| COH-61 | P3: Diagnostics, Metrics & Vertical Validation Scenario | Tasks | In Tasks |
| COH-62 | P3: Diagnostics, Metrics & Vertical Validation Scenario | Tasks | In Tasks |
| COH-63 | P3: Diagnostics, Metrics & Vertical Validation Scenario | Tasks | In Tasks |
| COH-64 | P3: Diagnostics, Metrics & Vertical Validation Scenario | Tasks | In Tasks |
| COH-65 | P3: Diagnostics, Metrics & Vertical Validation Scenario | Tasks | In Tasks |
| COH-66 | P3: Diagnostics, Metrics & Vertical Validation Scenario | Tasks | In Tasks |

**ID format:** `COH-[NUMBER]` (Cohesion), agrupado por dezena por story (01-05 = P1a, 11-16 = P1b, 21-25 = P1c, 31-36 = P1d, 41-45 = P2a, 51-54 = P2b, 61-66 = P3).

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 35 total, 35 mapeados a tasks (T1-T39), 0 unmapped.

---

## Success Criteria

- [ ] `test-living-village` roda determinístico e produz a cadeia causal `HarvestReduced → ... → EmploymentAffected` tocando ≥5 sistemas, sem função de scripting narrativo.
- [ ] Dois NPCs com memória/crença/relação/corpo diferentes produzem decisões observavelmente diferentes em cenários de teste dedicados (P1b, P1c ACs).
- [ ] Todos os 18+ `IExtraordinaryMechanic` aparecem como candidatos scoráveis em `SelectByUtility`; golden hashes de cenários com powers preservados ou divergência documentada via AD.
- [ ] Wakeups/agent-day e % de redecisão-sem-mudança-de-intent mensuravelmente menores que baseline pré-fase, sem perda de correção (mesmo resultado canônico).
- [ ] `bash scripts/verify.sh` verde; `STATE.md` atualizado com baseline anterior/posterior, ADRs, golden hashes alterados e por quê.
- [ ] `docs/audits/living-world-cohesion-audit.md` entregue com System/Attribute Integration Matrix cobrindo o escopo tocado.
