# Fase 4 — Necessidades e rotina — Design

**Spec**: `.specs/features/phase-04-needs/spec.md`
**Status**: Draft

---

## Approach exploration

**A — Estender `Npc`/`WorldState` com campos mutáveis novos (recomendado).**
Necessidades, personalidade, profissão, localização atual e ação corrente viram
propriedades de `Npc` (mesmo padrão de `Health`/`Household`); regras e catálogo novos
(`NeedsRules`, `ActionCatalog`) viram `[Canonical]` em `WorldState`, iguais a
`PopulationRules`/`PopulationCatalog`. Sistemas novos (`NeedsDecaySystem`,
`BehaviorDecisionSystem`) entram na lista ordenada do `WorldClock`.
*Prós*: zero indireção nova, reaproveita 100% do padrão canônico/volátil já provado
(reflexão sobre `WorldState`, sweep referencial, hash), sem sincronização entre coleções.
*Contras*: `Npc` cresce (mais um construtor com mais parâmetros) — aceitável, é o mesmo
trade-off que `WorldState` já paga.

**B — Estado de comportamento em dicionário paralelo (`Dictionary<NpcId, NpcBehaviorState>`
em `WorldState`).**
Separa "quem o NPC é" de "como ele decide agora". *Prós*: `Npc` fica enxuto. *Contras*: todo
nascimento/morte precisa manter duas coleções em sincronia (risco de exatamente o bug que
AD-031 já corrigiu, agora duplicado numa segunda coleção); sweep referencial e serialização
precisam de um segundo caminho de reidratação. Mais código para o mesmo resultado.

**C — Necessidade recomputada a partir do event log (nunca guardada).**
*Rejeitado*: contradiz `time-and-ticks.md` ("coisa rara vira evento; coisa contínua não") —
decair fome é *daily/hourly*, recomputar do zero a cada tick seria O(eventos acumulados),
crescente com o tempo. Fase 3 já resolveu esse mesmo trade-off a favor de estado mutável.

**Escolha: A.** Consistente com todo o código existente (`Npc`, `Household`, `WorldState`
já são "entidades gordas mutáveis"; classificação canônico/volátil já opera por reflexão no
nível de `WorldState`, então campos novos em `Npc` **não** precisam de atributo próprio —
eles viajam dentro de `Npcs`, que já é `[Canonical]`).

```mermaid
graph TD
    Clock[WorldClock.Tick] --> Decay[NeedsDecaySystem — Hourly]
    Decay --> Decision[BehaviorDecisionSystem — Hourly]
    Decision --> Routine[ActionCatalog.RoutineOf]
    Decision --> Utility[Utility scoring: base × contexto × personalidade]
    Decision --> Hysteresis[Bônus de continuidade + margem de troca]
    Utility --> Action[Npc.CurrentAction / CurrentLocation]
    Decay --> Starve{Hunger==0 por >= X ticks?}
    Starve -->|sim| Death[Npc.Die(Starvation)]
    Decision --> Move[MovementCost.Between — consome ticks]
```

---

## Code Reuse Analysis

### Existing Components to Leverage

| Component | Location | How to Use |
| --- | --- | --- |
| `Npc.SetHealth` pattern (clamp + private setter) | `src/LivingWorld.Domain/Population/Npc.cs` | Mesmo padrão para `SetHunger/SetThirst/SetSleep/SetSocial` |
| `Household` como residência | `src/LivingWorld.Domain/Population/Household.cs` | `Npc.Household` **já é** a residência (task 9) — nenhum campo `Residence` novo; `Household is null` já é o estado homeless |
| `PopulationCatalog`/`GeographyCatalog` (ids validados, vazio = sem restrição) | `src/LivingWorld.Domain/Population/PopulationCatalog.cs`, `.../Geography/GeographyCatalog.cs` | Mesmo padrão para `ActionCatalog` (ids de ação são enum fechado, não catálogo de cenário — ver Data Models) |
| `PopulationRules.Create` (Result-based validation) | `src/LivingWorld.Domain/Population/PopulationRules.cs` | Mesmo padrão para `NeedsRules.Create` |
| `MovementCost.Between` | `src/LivingWorld.Domain/Geography/MovementCost.cs` | Reusado sem alteração — resultado (double) é convertido em ticks (`Math.Max(1, Math.Ceiling(cost))`) |
| `ISimulationSystem` + `WorldClock` (ordem declarada) | `src/LivingWorld.Simulation/ISimulationSystem.cs`, `WorldClock.cs` | Dois sistemas novos, `Hourly`, registrados depois de `MortalitySystem`/`NatalitySystem` na lista |
| `ScheduledEvent`/`TickContext.ScheduleEvent` | `src/LivingWorld.Simulation/TickContext.cs` | **Não** usado para morte por fome (depende de comportamento contínuo, não de data fixa) — ver Tech Decisions |
| `PopulationScenarioLoader` (parse manual + `Result`) | `src/LivingWorld.Simulation/Population/PopulationScenarioLoader.cs` | Mesmo padrão para `BehaviorScenarioLoader` |
| `TickBudgetExceededException` | `src/LivingWorld.Simulation/TickBudgetExceededException.cs` | Reusada tal qual para o teto de seleção de ação (NEEDS-09) — mesmo tipo, outro `systemName` |
| `WorldRngRegistry`/`ctx.Rng(streamKey)` | `src/LivingWorld.Domain/WorldRngRegistry.cs` | Personalidade e profissão sorteadas em streams próprios (`personality-{npcId}`, `profession-{npcId}`) na criação do NPC |
| `PopulationGenerator`/`NatalitySystem` (únicos 2 pontos de criação de `Npc`) | `.../Population/PopulationGenerator.cs`, `.../Population/NatalitySystem.cs` | Ambos ganham a chamada de sorteio de personalidade/profissão — nenhum ponto de criação novo |
| `ReferentialIntegritySweep.ValidIdResolvers` | `src/LivingWorld.Simulation/ReferentialIntegritySweep.cs` | Nenhum id novo introduzido (Profession já existe como `int` de catálogo, não um `...Id` tipado) — sem entrada nova exigida |

### Integration Points

| System | Integration Method |
| --- | --- |
| `WorldClock` (Fase 1) | Dois `ISimulationSystem` novos inseridos na lista ordenada, entre `NatalitySystem`/`MortalitySystem` e o fim |
| `WorldSnapshot` (Fase 3) | Nenhuma propriedade nova de `WorldState` além de `NeedsRules`/`ActionCatalog` (`[Canonical]`) — campos de `Npc` viajam de graça dentro de `Npcs` |
| `ScenarioLoader`/cenário JSON (Fase 2/3) | `BehaviorScenarioLoader` novo, mesmo padrão de `PopulationScenarioLoader`; `ScenarioRunner` monta `NeedsRules`/`ActionCatalog` a partir dele |
| `MovementCost` (Fase 2) | Consumida sem alteração pelo `BehaviorDecisionSystem` para custo de deslocamento |
| `IWorldEventSink`/`WorldEventKind` (Fase 3) | Novo valor de enum `WorldEventKind.Starvation` (morte por fome já usa `WorldEventKind.Death` com causa — ver Tech Decisions) |

---

## Components

### `Npc` (extensão) — `src/LivingWorld.Domain/Population/Npc.cs`

- **Purpose**: ganha necessidades, personalidade, profissão, localização e ação corrente.
- **Novas propriedades**: `Hunger/Thirst/Sleep/Social` (int, private set, `SetX` com clamp
  `[0,100]`, mesmo padrão de `Health`); `Personality` (`Personality`, imutável, definida no
  construtor); `Profession` (`ProfessionType`, private set — estático nesta fase, sem
  emprego real); `CurrentLocation` (`CellCoord`, private set, inicia em `BirthLocation`);
  `CurrentAction` (`ActionType?`, private set); `ActionStartedAtTick` (`long`, private set);
  `HungerZeroSinceTick` (`long?`, private set — dispara `Starvation` quando expira);
  `HomelessSince` (`WorldDate?`, private set — `null` enquanto `Household` existir).
- **Métodos novos**: `SetHunger/SetThirst/SetSleep/SetSocial(int)`; `MoveTo(CellCoord, long
  tick)`; `SetCurrentAction(ActionType, long tick)`; `MarkHomeless(WorldDate)` /
  `ClearHomeless()` (chamado por `JoinHousehold`/quando `Household` volta a existir).
- **Dependencies**: `Personality`, `ActionType`, `ProfessionType` (novos, Domain).
- **Reuses**: construtor único de reidratação (mesmo motivo do resto da classe — round-trip
  do `System.Text.Json`), `[JsonIgnore]` em qualquer propriedade computada nova.

### `Personality` — `src/LivingWorld.Domain/Population/Personality.cs`

- **Purpose**: os 10 traços 0-100 de `npc.md`, imutáveis após o nascimento.
- **Interfaces**: `record Personality(int Extroversion, int Agreeableness, int
  Conscientiousness, int EmotionalStability, int Openness, int Ambition, int Loyalty, int
  Altruism, int Impulsivity, int RiskAversion)`; `static Result<Personality> Create(...)`
  valida `[0,100]` em cada traço (mesmo padrão de `PopulationRules.Create`).
- **Dependencies**: nenhuma.
- **Reuses**: `Result<T>` (Fase 0).

### `ProfessionType` / `LifeStage` — `src/LivingWorld.Domain/Population/ProfessionType.cs`, `LifeStage.cs`

- **Purpose**: `ProfessionType` é wrapper de `int` (mesmo padrão de `CultureId`) validado
  contra `PopulationCatalog.ProfessionIds`; `LifeStage` é `enum { Child, Adult, Elder }`.
- **`LifeStageRules`** (record, `ChildMaxAge`, `AdultMaxAge` do cenário): `LifeStageOf(int
  ageYears)` — nunca hardcoded (R3).
- **Reuses**: mesmo padrão de `Ids.cs`.

### `ActionType` — `src/LivingWorld.Domain/Behavior/ActionType.cs`

- **Purpose**: catálogo **fechado** de ações candidatas desta fase — não é conteúdo de
  cenário (é o próprio modelo de decisão, não nome de profissão/recurso), por isso é
  `enum` com valor estável: `Eat = 0, Sleep = 1, Work = 2, Socialize = 3, Travel = 4, Idle =
  5`. O valor inteiro **é** o `ActionId` do desempate (NEEDS-06).

### `ActionCatalog` — `src/LivingWorld.Domain/Behavior/ActionCatalog.cs`

- **Purpose**: duração máxima declarada por ação (task 5/NEEDS-13) + tabela de rotina
  `(ProfessionId|"any", LifeStage, HourRange) → ActionType` (task 6/NEEDS-10).
- **Interfaces**: `IReadOnlyDictionary<ActionType,int> MaxDurationHours` (todas as 6 ações
  **obrigatórias** — `Create` falha nomeando a que falta, cobrindo "reprova se ação não
  declarar duração"); `ActionType RoutineOf(ProfessionType?, LifeStage, int hour)` — sem
  slot correspondente, cai no `DefaultAction` declarado do cenário (nunca exceção).
- **Reuses**: mesmo padrão de vazio-é-sem-restrição do `PopulationCatalog` para o `"any"`.

### `NeedsRules` — `src/LivingWorld.Domain/Behavior/NeedsRules.cs`

- **Purpose**: todo parâmetro numérico do utility AI e das necessidades, cenário-driven
  (R3 — zero literal em C#).
- **Campos**: `HungerDecayPerHour, ThirstDecayPerHour, SleepDecayPerHour, SocialDecayPerHour`
  (double); `UrgencyThreshold` (int); `MaxActionSelectionSteps` (int);
  `HysteresisEnabled` (bool); `ContinuityBonus` (double); `HomelessSleepEfficiency`
  (double `[0,1]`); `ChildMaxAge, AdultMaxAge` (int).
- **`Create`**: `Result<NeedsRules>`, valida faixas (mesmo padrão de
  `PopulationRules.Create`).

### `PersonalityWeighting` — `src/LivingWorld.Domain/Behavior/PersonalityWeighting.cs`

- **Purpose**: `peso(traço, ação) = 1 + (traço/100 − 0.5) × influência[traço][ação]` — a
  **fórmula** e a tabela de influência são parte do modelo de IA (algoritmo, não conteúdo de
  cenário), logo vivem em código, não em JSON — mesmo status que a própria fórmula de
  utilidade.
- **Tabela de influência (uma ação primária por traço, positiva; a tabela completa e o
  case-por-caso dos 10 traços — incluindo qual par de ações cada teste usa para
  discriminar — é finalizada na Task correspondente, junto do teste que a exercita)**:
  Conscientiousness→Work, Ambition→Work, Extroversion→Socialize, Openness→Socialize,
  Altruism→Socialize, Loyalty→Work, Impulsivity→Idle (e negativo em Work), RiskAversion→
  negativo em Travel, EmotionalStability→negativo em Idle (estabilidade baixa refugia em
  ócio), Agreeableness→Socialize (segunda entrada, mesma direção de Extroversion/Openness,
  mas testável isolando o par Socialize×Idle com os outros traços fixos em 50).
- **Dependencies**: `NeedsRules` (nenhum limiar mágico — só a tabela fixa do algoritmo).

### `NeedsDecaySystem` — `src/LivingWorld.Simulation/Behavior/NeedsDecaySystem.cs`

- **Purpose**: decai as 4 necessidades por tick Hourly; dispara objetivo em 0; mata por
  fome sustentada (NEEDS-01/02/03).
- **`Tick`**: para cada `Npc` vivo, `SetHunger(Hunger − rate)` etc.; se `Hunger` chegou a 0
  neste tick e `HungerZeroSinceTick` era nulo, grava o tick atual; se `Hunger > 0`, limpa;
  se `now − HungerZeroSinceTick >= X` (`X = ceil(100/HungerDecayPerHour)`, calculado do
  cenário em runtime — nunca constante), `npc.Die(now)` com `causa = Starvation`, log de
  evento.
- **Reuses**: `TickContext.LogEvent`, `Npc.Die` (Fase 3).

### `BehaviorDecisionSystem` — `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs`

- **Purpose**: escolhe a ação do tick — rotina por padrão, utility AI quando algo supera o
  limiar de urgência, com histerese e teto de passos (NEEDS-05..14).
- **Fluxo por NPC vivo**:
  1. `LifeStage` via `LifeStageRules.LifeStageOf(AgeYears)`.
  2. Ação-base = `ActionCatalog.RoutineOf(Profession, LifeStage, hora)`.
  3. Pontua candidatas urgentes (necessidade acima de `UrgencyThreshold`) com
     `utilidadeBase(necessidade, contexto) × PersonalityWeighting.WeightOf(...)`.
  4. Se `HysteresisEnabled`, soma `ContinuityBonus` à nota da ação corrente antes de
     comparar; só troca se alguma candidata superar essa nota efetiva.
  5. Resolve dependência de local: se a ação vencedora exige um local diferente de
     `CurrentLocation`, a ação **efetiva** deste tick é `Travel` até lá (task 8); resolvido
     em loop com teto `MaxActionSelectionSteps` — cenário adversarial de teste injeta uma
     cadeia de dependência circular (A exige estar onde B decide, e vice-versa) para provar
     o abort nomeado (`TickBudgetExceededException`-like, nomeando NPC e ações empatadas).
  6. Ao concluir a duração declarada da ação (`ActionCatalog.MaxDurationHours`), aplica o
     efeito (`Eat`→`SetHunger(100)`, `Sleep`→`SetSleep(100 × (Homeless ?
     HomelessSleepEfficiency : 1))`, etc.) e libera o NPC pra nova seleção no tick seguinte.
- **Reuses**: `MovementCost.Between`, `TickContext.Rng` (desempate probabilístico não
  existe — desempate é sempre por `ActionId`, determinístico).

### `BehaviorScenarioLoader` — `src/LivingWorld.Simulation/Behavior/BehaviorScenarioLoader.cs`

- **Purpose**: parse de `NeedsRules` + `ActionCatalog` do JSON de cenário — mesmo padrão de
  `PopulationScenarioLoader`.
- **Reuses**: `Result<T>`, convenção de campo obrigatório nomeado no erro.

---

## Data Models

### `Personality` (Domain, imutável)
```
Extroversion, Agreeableness, Conscientiousness, EmotionalStability, Openness,
Ambition, Loyalty, Altruism, Impulsivity, RiskAversion : int [0,100]
```

### `NeedsRules` (Domain, cenário)
```
HungerDecayPerHour, ThirstDecayPerHour, SleepDecayPerHour, SocialDecayPerHour : double
UrgencyThreshold, MaxActionSelectionSteps, ChildMaxAge, AdultMaxAge : int
HysteresisEnabled : bool
ContinuityBonus, HomelessSleepEfficiency : double
```

### `ActionCatalog` (Domain, cenário)
```
MaxDurationHours : IReadOnlyDictionary<ActionType,int>   // as 6 ações, obrigatório
RoutineSlots     : IReadOnlyList<RoutineSlot>
DefaultAction    : ActionType
```
```
record RoutineSlot(int? ProfessionId, LifeStage Stage, int HourStart, int HourEnd, ActionType Action)
```

**Relationships**: `Npc.Profession` referencia `PopulationCatalog.ProfessionIds` (já
existente); `Npc.Household` (já existente) é a residência; `ActionCatalog`/`NeedsRules`
entram em `WorldState` como novas propriedades `[Canonical]`.

---

## Error Handling Strategy

| Error Scenario | Handling | User Impact |
| --- | --- | --- |
| `ActionCatalog` sem as 6 durações declaradas | `Create` retorna `Result.Fail` nomeando a ação faltante | Carga do cenário falha na borda, nunca em runtime |
| Utilidade cíclica (dependência de local sem solução) | Resolução aborta em `MaxActionSelectionSteps`, exceção nomeia NPC + ações empatadas | Falha alta e imediata, nunca timeout silencioso |
| NPC sem `Household` (homeless) tenta dormir | Dorme em `CurrentLocation` com eficiência reduzida — nunca exceção | Comportamento degradado, não erro |
| Necessidade decairia abaixo de 0 | Clamp em 0 + dispara objetivo no mesmo tick | Nunca valor fora de faixa, nunca silêncio |

---

## Risks & Concerns

| Concern | Location | Impact | Mitigation |
| --- | --- | --- | --- |
| `Npc` já tem 2 construtores extensos (task 1); mais ~8 parâmetros aumenta ainda mais o risco de trocar posição por engano | `src/LivingWorld.Domain/Population/Npc.cs` | Bug silencioso de parâmetro trocado no round-trip do snapshot | Testes de round-trip (`WorldSnapshotTests`) já existem e cobrem `Npc`; task de execução adiciona um caso por campo novo antes de mexer no construtor |
| `PersonalityWeighting` é tabela fixa em código — se um traço não tiver linha de teste, o critério "reprova se traço sem linha" (NEEDS-08) não teria como falhar sozinho | `src/LivingWorld.Domain/Behavior/PersonalityWeighting.cs` | Cobertura incompleta passaria silenciosa | Task de execução inclui teste por reflexão sobre o `enum`/tabela de traços (mesmo padrão do sweep de ids) que falha se `Personality` ganhar um traço sem entrada na tabela de casos |
| Nenhum teste de arquitetura hoje impede `LivingWorld.Simulation` de referenciar `System.Threading.Tasks.Parallel` em sistema novo | `rules/simulation-determinism.md` | Novo sistema paralelizado sem querer quebraria determinismo | `ArchitectureTests.cs` já existe (Fase 0); nenhuma mudança de escopo — só seguir a regra ao escrever os sistemas novos |

> Nenhuma dívida técnica nova introduzida além da extensão natural de `Npc`.

---

## Tech Decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Morte por fome não usa `ScheduledEvent` | Checagem incremental por tick (`HungerZeroSinceTick`) | Depende de comportamento contínuo (o NPC pode comer a qualquer tick), não de uma data fixa como gravidez — não dá pra pré-agendar no momento em que a fome chega a 0 |
| `Residence` não é campo novo | Reusa `Npc.Household` (já existente desde a Fase 3) | Household já tem `Location` e já é o conceito de "onde mora"; campo novo duplicaria o mesmo dado |
| Tabela traço→peso vive em código, não em cenário | `PersonalityWeighting` é constante de algoritmo | Diferente de terreno/profissão/recurso (conteúdo do mundo), é o **modelo de decisão** em si — trocar de cenário não deveria mudar como personalidade pesa utilidade |
| `ActionType` é `enum` fechado, não catálogo de cenário como `ProfessionType` | Ações candidatas desta fase são fixas (Eat/Sleep/Work/Socialize/Travel/Idle) | Ao contrário de profissão/recurso, o conjunto de ações não varia por cenário nesta fase — variar exigiria repensar toda a rotina/utility, fora do escopo declarado |
| Histerese com um único parâmetro (`ContinuityBonus`) em vez de bônus + custo separados | Soma-se à nota da ação corrente antes de comparar | Um único número já produz o efeito pedido (menos trocas, mesma direção causal); dois parâmetros redundantes por enquanto sem caso de uso que os distinga |

> **Project-level**: nenhuma decisão aqui supera constraint ativa em `STATE.md`. As
> decisões acima ficam só nesta tabela (não viram `AD-NNN`) — são escolhas locais da Fase 4,
> não convenção que outras fases precisem seguir.

---

## Tips
- Modelo de aprendizado gauge (**Note**: leve para faster/cheaper model): validação de
  `spec.md`/`design.md` roda bem em modelo mais barato — considerar para a fase de
  validação após a implementação.
