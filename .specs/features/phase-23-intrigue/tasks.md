# Fase 23 — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path.

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-23-intrigue/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Guidelines found: none dedicada — segue o padrão já usado nas specs 4/7/10/17/21/22 (xUnit,
> mundo controle/tratado, determinismo por seed, enumeração por reflexão + par de mutação,
> mesmo padrão de `CombatMechanic.DamageOf` pra resolução com PartialSuccess).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain (`SecretAttributes`, `MoodModifier`, `LineageFeud`, `PersonaDescriptor`, `IdentityAttributionBelief`, `PublicationEvent`) | unit | Construção/invariantes | `tests/LivingWorld.Tests/Intrigue/**` (novo) | `dotnet test --filter "FullyQualifiedName~Intrigue"` |
| Guard "ninguém sabe sem caminho" | unit + enumeração por reflexão + par de mutação | 1:1 a INT-01..04 | `tests/LivingWorld.Tests/History/SecretQuerySeparationGuardTests.cs` (novo, mesmo padrão de guards de Fase 10/17) | mesmo comando |
| `MotiveOpportunityFilter` | unit | 1:1 a INT-10, INT-11 | `tests/LivingWorld.Tests/Behavior/**` | mesmo comando |
| `BlackmailAction` | unit + auditoria 10 anos | 1:1 a INT-20, INT-21 | `tests/LivingWorld.Tests/Intrigue/**` | mesmo comando |
| `BetrayalHandler` | unit + par densidade de segredo | 1:1 a INT-30..33, 18/20 seeds | `tests/LivingWorld.Tests/Intrigue/**` | mesmo comando |
| `MoodStackSystem` | unit + hash/diversidade | 1:1 a INT-40..42, 18/20 seeds | `tests/LivingWorld.Tests/Intrigue/**` | mesmo comando |
| `GrudgeDecaySystem`/`LineageFeudAggregator` | unit | 1:1 a INT-50..53 | `tests/LivingWorld.Tests/Intrigue/**` | mesmo comando |
| `BrawlResolver` | unit | 1:1 a INT-60, INT-61 | `tests/LivingWorld.Tests/Intrigue/**` | mesmo comando |
| `GossipDistortionModulator` | unit + par relação hostil/aliada | 1:1 a INT-70..72 | `tests/LivingWorld.Tests/History/**` | mesmo comando |
| `ReputationCache` | unit + invalidação seletiva | 1:1 a INT-80, INT-81 | `tests/LivingWorld.Tests/Intrigue/**` | mesmo comando |
| `Faction`/`FactionRecruiter` | unit | 1:1 a INT-90..92 | `tests/LivingWorld.Tests/Intrigue/**` | mesmo comando |
| `IdentityAttributionResolver` | unit | 1:1 a INT-A0..A4 | `tests/LivingWorld.Tests/Intrigue/**` | mesmo comando |
| `JournalistDecisionSystem` | unit | 1:1 a INT-B0..B3 | `tests/LivingWorld.Tests/Intrigue/**` | mesmo comando |
| Full regression | build gate | Backend inteiro verde, sem regressão em `History*`/`Population*`/`Society*`/`Extraordinary*` | `tests/LivingWorld.Tests/**` | `bash scripts/test.sh` |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit (Intrigue) | Yes | Mundo próprio por teste | Fase 4/7/10/21/22 |
| par base/tratamento (densidade de segredo, relação, humor) | Yes | Mundo controle/tratado (`PairedScenarioTests.cs`) | Fase 7/17 |
| enumeração por reflexão + mutação | Yes | Mesmo padrão de guards de Fase 10/17/20 | Fase 10/17/20 |
| build gate (`scripts/test.sh`) | No | Suíte completa sequencial | `.specs/STATE.md` |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Após cada task de domínio/sistema | `dotnet test --filter "FullyQualifiedName~Intrigue"` |
| Full (integração) | Após tasks que tocam `Behavior`/`History`/`Population` | `dotnet test --filter "Category!=Scenario&FullyQualifiedName~Intrigue"` |
| Build | Última task (antes do Verifier) | `bash scripts/test.sh` |

---

## Execution Plan

### Phase 1: Fundação de dados (Sequential)

```
T1 → T2 → T3
```

### Phase 2: Segredo + guard (depende de Phase 1)

```
T3 → T4 → T5
```

### Phase 3: Motivo/oportunidade + ações hostis (depende de Phase 2)

```
T5 → T6
```

### Phase 4: Chantagem e traição (depende de Phase 3)

```
T6 → T7 → T8
```

### Phase 5: Humor (Parallel OK, depende de Phase 1)

```
T3 → T9
```

### Phase 6: Rancor e linhagem (Parallel OK, depende de Phase 1)

```
T3 → T10 → T11
```

### Phase 7: Briga (depende de Phase 3)

```
T6 → T12
```

### Phase 8: Fofoca e reputação (depende de Phase 2)

```
T5 → T13 → T14
```

### Phase 9: Facção (depende de Phase 2)

```
T5 → T15
```

### Phase 10: Persona e publicação (última — depende de Phase 8)

```
T14 → T16 → T17
```

---

## Task Breakdown

### T1: `SecretAttributes`

**What**: Record aditivo referenciando `FactId` (Fase 10).
**Where**: `src/LivingWorld.Domain/Intrigue/Secret.cs` (novo)
**Depends on**: None
**Reuses**: `Fact`/`FactId` (Fase 10), sem modificação
**Requirement**: INT-01

**Done when**:
- [ ] `SecretAttributes` nunca duplica dados de `Fact` — só referencia por `FactId`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add SecretAttributes over existing Fact`

---

### T2: `MoodModifier`, `LineageFeud`, `PersonaDescriptor`, `IdentityAttributionBelief`,
`PublicationEvent`

**What**: Records de domínio dos componentes genuinamente novos.
**Where**: `src/LivingWorld.Domain/Intrigue/*.cs` (novos arquivos)
**Depends on**: None
**Reuses**: nenhum
**Requirement**: (suporta INT-40, INT-52, INT-A0..A4, INT-B0..B3)

**Done when**:
- [ ] `MoodStack.CurrentMood` é sempre computed property, nunca campo armazenado

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add mood, lineage feud, persona and publication records`

---

### T3: `WorldEventKind` — 7 valores novos

**What**: `SecretLeaked`, `BlackmailExecuted`, `BetrayalOccurred`, `BrawlResolved`,
`FactionExposed`, `IdentityChanged`, `PublicationDecided`.
**Where**: `src/LivingWorld.Domain/History/WorldEventKind.cs` (modificado, aditivo)
**Depends on**: T1, T2
**Reuses**: enum existente, aditivo
**Requirement**: (auditoria)

**Done when**:
- [ ] Nenhum valor existente do enum muda de posição/significado

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add intrigue WorldEventKind values`

---

### T4: `ActionType` — 5 valores novos

**What**: `Blackmail`, `Betray`, `Brawl`, `Gossip`, `Publish`.
**Where**: `src/LivingWorld.Domain/Behavior/ActionType.cs` (modificado, aditivo)
**Depends on**: T3
**Reuses**: enum fechado existente (Fase 4), aditivo
**Requirement**: (suporta INT-10)

**Done when**:
- [ ] `ActionCatalog.Create` continua exigindo `MaxDurationHours`/`RequiredMilestone` (Fase 4/21) pra cada novo valor — nenhuma exceção de cobertura

**Tests**: unit
**Gate**: quick
**Commit**: `feat(behavior): add hostile action types`

---

### T5: Guard "ninguém sabe sem caminho"

**What**: Enumeração por reflexão de toda superfície de crença; par de mutação.
**Where**: `tests/LivingWorld.Tests/History/SecretQuerySeparationGuardTests.cs` (novo)
**Depends on**: T3
**Reuses**: mesmo padrão de guards de Fase 10/17/20
**Requirement**: INT-03, INT-04

**Done when**:
- [ ] Enumeração cobre 100% dos handlers de acesso à crença sobre segredo; falha se algum resolver pro fato direto ou ficar sem cobertura
- [ ] Par de mutação: desligar a checagem derruba o critério
- [ ] Auditoria de 10 anos: todo NPC que sabe tem cadeia até o dono
- [ ] Despejo do cânone vira fato irrevelável (fato nunca apagado, sem canal de aprendizado novo)

**Tests**: unit + enumeração por reflexão + par de mutação + auditoria 10 anos
**Gate**: quick
**Commit**: `test(intrigue): guard secret belief access, prove chain-to-owner and canon eviction`

---

### T6: `MotiveOpportunityFilter`

**What**: Filtra ações hostis do conjunto candidato exigindo motivo E oportunidade; testemunha
só se aplica em região materializada.
**Where**: `src/LivingWorld.Simulation/Behavior/MotiveOpportunityFilter.cs` (novo)
**Depends on**: T4
**Reuses**: `MaterializationSystem.HasFormalRole` (Fase 8), mesmo padrão de filtro-antes-da-utility (Fase 21)
**Requirement**: INT-10, INT-11

**Done when**:
- [ ] Ausência de motivo OU oportunidade remove a ação antes da pontuação
- [ ] Região agregada nunca produz ação hostil candidata

**Tests**: unit
**Gate**: quick
**Commit**: `feat(behavior): filter hostile actions by motive and opportunity`

---

### T7: `BlackmailAction`

**What**: Consulta `NpcBeliefQuery` (nunca verdade); recusa se segredo ausente das crenças.
**Where**: `src/LivingWorld.Simulation/Intrigue/BlackmailAction.cs` (novo)
**Depends on**: T6
**Reuses**: `NpcBeliefQuery` (Fase 10), sem modificação
**Requirement**: INT-20, INT-21

**Done when**:
- [ ] Auditoria de 10 anos: 100% das chantagens executadas têm o segredo nas crenças do chantagista no mesmo tick

**Tests**: unit + auditoria 10 anos
**Gate**: quick
**Commit**: `feat(intrigue): implement blackmail as belief-gated action`

---

### T8: `BetrayalHandler`

**What**: Dispara `RelationshipEventType.Betrayal` (já existente); grava `NpcMemory` em
testemunhas.
**Where**: `src/LivingWorld.Simulation/Intrigue/BetrayalHandler.cs` (novo)
**Depends on**: T6
**Reuses**: `Relationship.ApplyEvent(Betrayal, rules)` (Fase 7, já existente), `NpcMemory` (Fase 11)
**Requirement**: INT-30, INT-31, INT-32, INT-33

**Done when**:
- [ ] Colapso de confiança proporcional ao ganho, nunca binário
- [ ] Toda testemunha materializada grava `NpcMemory` de alta importância
- [ ] Inspeção de esquema não encontra campo `traidor`
- [ ] Par densidade de segredo: mais traições no braço tratado, 18/20 seeds

**Tests**: unit + par base/tratamento
**Gate**: quick
**Commit**: `feat(intrigue): implement betrayal via existing relationship event, no traitor flag`

---

### T9: `MoodStackSystem`

**What**: Empilha `MoodModifier`; `CurrentMood` derivado; alimenta utility como peso.
**Where**: `src/LivingWorld.Simulation/Intrigue/MoodStackSystem.cs` (novo), hook em
`BehaviorDecisionSystem`/`PersonalityWeighting` (modificado, aditivo)
**Depends on**: T3
**Reuses**: `PersonalityWeighting`/`SelectByUtility` (Fase 4) como consumidor
**Requirement**: INT-40, INT-41, INT-42

**Done when**:
- [ ] `CurrentMood` sempre derivado da pilha, nunca campo armazenado
- [ ] Desligar a pilha por flag muda o hash canônico em 10 anos E reduz diversidade de ação escolhida, par na seed, 18/20 seeds

**Tests**: unit + hash/diversidade
**Gate**: quick
**Commit**: `feat(intrigue): implement mood as derived stack feeding utility weight`

---

### T10: `GrudgeDecaySystem`

**What**: Rancor individual (`NpcMemory` `Social` negativa) decai até zero no prazo do
cenário; reacende com evento novo.
**Where**: `src/LivingWorld.Simulation/Intrigue/GrudgeDecaySystem.cs` (novo)
**Depends on**: T3
**Reuses**: `NpcMemory`/`MemoryCategory.Social` (Fase 11)
**Requirement**: INT-50, INT-51

**Done when**:
- [ ] `rancor(t+1) ≤ rancor(t)` sem evento novo, chegando a zero no prazo
- [ ] Evento novo do mesmo alvo reacende o rancor

**Tests**: unit
**Gate**: quick
**Commit**: `feat(intrigue): implement individual grudge decay with prescription and rekindling`

---

### T11: `LineageFeudAggregator`

**What**: Agrega rancor de linhagem com prazo próprio, mais longo, independente do individual.
**Where**: `src/LivingWorld.Simulation/Intrigue/LineageFeudAggregator.cs` (novo)
**Depends on**: T10
**Reuses**: `GrudgeDecaySystem` (T10) como fonte de rancor individual agregado
**Requirement**: INT-52, INT-53

**Done when**:
- [ ] Rixa de linhagem ainda ativa quando o rancor individual equivalente já teria prescrito
- [ ] Rixa continua existindo mesmo com origem esquecida (crença sobre a linhagem, não fato lembrado)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(intrigue): aggregate lineage feud with independent, longer prescription`

---

### T12: `BrawlResolver`

**What**: Uma rolagem `Resolver.Resolve` (`Dramatico`); `PartialSuccess` sempre dispara
consequência de testemunha.
**Where**: `src/LivingWorld.Simulation/Intrigue/BrawlResolver.cs` (novo)
**Depends on**: T6
**Reuses**: `Resolver.Resolve`/`VarianceProfile.Dramatico` (ADR-0011), mesmo padrão de
`CombatMechanic.DamageOf`
**Requirement**: INT-60, INT-61

**Done when**:
- [ ] Briga resolve numa única rolagem, nunca sequência de turnos
- [ ] `PartialSuccess` sempre gera consequência rastreável (testemunha/reputação), nunca ignorado

**Tests**: unit
**Gate**: quick
**Commit**: `feat(intrigue): implement brawl as single-roll resolution with first-class partial success`

---

### T13: `GossipDistortionModulator`

**What**: Modula probabilidade de `DistortionOperator` por relação contador↔alvo/ouvinte,
camada sobre `HistoryRules.OperatorProbability`.
**Where**: `src/LivingWorld.Simulation/History/GossipDistortionModulator.cs` (novo)
**Depends on**: T5
**Reuses**: `DistortionEngine`/`DistortionOperator`/`HistoryRules` (Fase 10), sem modificação
**Requirement**: INT-70, INT-71, INT-72

**Done when**:
- [ ] Relação hostil aumenta probabilidade de operadores de inflação vs. relação neutra
- [ ] Relação aliada aumenta probabilidade de operadores de omissão vs. relação neutra
- [ ] Modulação=1 (default) reproduz o comportamento sem esta fase (teste de regressão)

**Tests**: unit + par relação hostil/aliada
**Gate**: quick
**Commit**: `feat(intrigue): modulate distortion probability by counter-target-listener relationship`

---

### T14: `ReputationCache`

**What**: Cache dirty-flag por (comunidade, NPC), mesmo padrão de `CanonicalHashCache`,
derivado/reconstruível, nunca canônico.
**Where**: `src/LivingWorld.Simulation/Intrigue/ReputationCache.cs` (novo)
**Depends on**: T13
**Reuses**: mesmo padrão de `MarkXDirty`+version counter de `CanonicalHashCache` (Fase 9)
**Requirement**: INT-80, INT-81

**Done when**:
- [ ] Invalidação é seletiva por (comunidade, NPC), nunca global
- [ ] ≥1 caso onde reputação diverge entre 2 comunidades e ambas divergem da verdade (`HistoryTruthQuery`)
- [ ] Cache é reconstruível a qualquer momento sem afetar hash canônico (não é campo `[Canonical]`)

**Tests**: unit + invalidação seletiva
**Gate**: quick
**Commit**: `feat(intrigue): implement reputation as dirty-flagged, non-canonical cache`

---

### T15: `Faction` + `FactionRecruiter`

**What**: Organização com objetivo oculto = `SecretAttributes` multi-dono; recrutamento por
afinidade/rancor comum; exposição pública com consequência.
**Where**: `src/LivingWorld.Simulation/Intrigue/Faction.cs` (novo)
**Depends on**: T5
**Reuses**: `SecretAttributes` (T1), `ReputationCache` (T14, pra consequência de exposição)
**Requirement**: INT-90, INT-91, INT-92

**Done when**:
- [ ] Objetivo oculto é o mesmo tipo de segredo, nunca um sigilo paralelo
- [ ] Recrutamento correlaciona com afinidade + rancor comum
- [ ] Exposição pública altera reputação mensuravelmente (integração com T14)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(intrigue): implement faction with shared-secret hidden goal and recruitment`

---

### T16: `IdentityAttributionResolver`

**What**: Forma `IdentityAttributionBelief` só a partir de evidência observável; exposição
gradual em 4 estágios.
**Where**: `src/LivingWorld.Simulation/Intrigue/IdentityAttributionResolver.cs` (novo)
**Depends on**: T14
**Reuses**: `PowerDescriptor`/`ExtraordinaryCarrierState` (Fase 16) como fonte de verdade da
persona
**Requirement**: INT-A0, INT-A1, INT-A2, INT-A3, INT-A4

**Done when**:
- [ ] `PersonaDescriptor` consultável só pelo canal de Verdade, nunca handler de jogo
- [ ] Atribuição falsa funciona mecanicamente igual à verdadeira (mesmo pipeline)
- [ ] Ver efeito da potência não forma crença de identidade sozinho — exige evidência observável
- [ ] Exposição avança pelos 4 estágios gradualmente, nunca salto direto

**Tests**: unit
**Gate**: quick
**Commit**: `feat(intrigue): resolve persona identity attribution as observer belief, never global bool`

---

### T17: `JournalistDecisionSystem`

**What**: Avalia risco/interesse/ganho + crença própria; produz `PublicationEvent`; apelido
negociado; reação reusa `ReputationCache`.
**Where**: `src/LivingWorld.Simulation/Intrigue/JournalistDecisionSystem.cs` (novo)
**Depends on**: T16
**Reuses**: `ReputationCache` (T14) pra reação ao extraordinário, nunca `if target.HasPower`
**Requirement**: INT-B0, INT-B1, INT-B2, INT-B3

**Done when**:
- [ ] Decisão nunca é regra fixa "toda informação vira notícia" — depende de risco/interesse/ganho
- [ ] `PublicationEvent` é dado estruturado, zero texto gerado por este sistema
- [ ] Apelido de imprensa é negociado (portador aceita/rejeita)
- [ ] Reação pública ao portador é literalmente o mesmo cálculo de `ReputationCache`
- [ ] Gate final: `bash scripts/test.sh` verde (backend completo)

**Tests**: unit
**Gate**: build
**Commit**: `feat(intrigue): implement journalist as agent producing structured publication events`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1, T2 ──→ T3

Phase 2 (Sequential, depends on Phase 1):
  T3 ──→ T4 ──→ T5

Phase 3 (depends on Phase 2):
  T5 ──→ T6

Phase 4 (Sequential, depends on Phase 3):
  T6 ──→ T7 ──→ T8

Phase 5 (Parallel, depends on Phase 1):
  T3 ──→ T9

Phase 6 (Sequential, Parallel, depends on Phase 1):
  T3 ──→ T10 ──→ T11

Phase 7 (depends on Phase 3):
  T6 ──→ T12

Phase 8 (Sequential, depends on Phase 2):
  T5 ──→ T13 ──→ T14

Phase 9 (depends on Phase 2):
  T5 ──→ T15

Phase 10 (last — depends on Phase 8):
  T14 ──→ T16 ──→ T17
```

10 fases > 3 — Execute vai oferecer delegação por sub-agent por fase (offer-then-confirm). Fases
5, 6, 7, 9 são ramos independentes que correm em paralelo assim que suas dependências mínimas
fecham.

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1, T2, T3, T4 | 1 conjunto de modelos/enum cada | ✅ Granular |
| T5 | 1 suíte de guard dedicada | ✅ Granular |
| T6 | 1 filtro | ✅ Granular |
| T7, T8 | 1 ação + 1 handler | ✅ Granular |
| T9 | 1 sistema | ✅ Granular |
| T10, T11 | 1 decaimento + 1 agregador de linhagem | ✅ Granular |
| T12 | 1 resolvedor | ✅ Granular |
| T13, T14 | 1 modulador + 1 cache | ✅ Granular |
| T15 | 1 organização + recrutamento (mesmo domínio coeso) | ✅ Granular |
| T16, T17 | 1 resolvedor de identidade + 1 sistema de decisão | ✅ Granular |

## Diagram-Definition Cross-Check

| Task | Depends On (body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | None | None | ✅ Match |
| T3 | T1, T2 | T1,T2→T3 | ✅ Match |
| T4 | T3 | T3→T4 | ✅ Match |
| T5 | T3 | T3→T5 | ✅ Match |
| T6 | T5 | T5→T6 | ✅ Match |
| T7 | T6 | T6→T7 | ✅ Match |
| T8 | T6 | T6→T8 | ✅ Match |
| T9 | T3 | T3→T9 (paralelo) | ✅ Match |
| T10 | T3 | T3→T10 (paralelo) | ✅ Match |
| T11 | T10 | T10→T11 | ✅ Match |
| T12 | T6 | T6→T12 | ✅ Match |
| T13 | T5 | T5→T13 | ✅ Match |
| T14 | T13 | T13→T14 | ✅ Match |
| T15 | T5 | T5→T15 (paralelo) | ✅ Match |
| T16 | T14 | T14→T16 | ✅ Match |
| T17 | T16 | T16→T17 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1-T4 | Domain/business-logic | unit | unit | ✅ OK |
| T5 | Suíte de guard dedicada | unit + enumeração + mutação + auditoria | mesmo | ✅ OK |
| T6-T13, T15, T16 | Sistemas/filtros/resolvedores | unit (+ par pareado onde aplicável) | mesmo | ✅ OK |
| T14 | Cache | unit + invalidação seletiva | unit + invalidação | ✅ OK |
| T17 | Sistema final + build gate | unit, build | unit, build | ✅ OK |

No task defers its own tests to a later task.
