# Fase 21 — Design

**Spec**: `.specs/features/phase-21-ontogeny/spec.md` (30 requisitos, ONT-01..92)
**Scope**: Complex (modelo de desenvolvimento novo, mas 100% composição da utility AI (Fase 4),
predisposição genética (Fase 6), canal ambiental (Fase 7) e LOD/materialização (Fase 8/9) já
existentes)

---

## Architecture Overview

Marco não é um sistema paralelo à utility AI — é um FILTRO que roda antes de
`BehaviorDecisionSystem.SelectByUtility` considerar uma ação candidata, e um VALOR que
`SkillCurve`-like progride pela mesma disciplina de `RateGene` já usada em `SkillPracticeSystem`.

```mermaid
flowchart TD
    ActionCatalog[ActionCatalog — ActionType existente] -->|declara| Milestone[RequiredMilestone: eixo+limiar, dado de catálogo]
    Milestone --> Filter[MilestoneEligibilityFilter — roda ANTES de SelectByUtility]
    Filter -->|remove do conjunto candidato| BehaviorDecisionSystem[BehaviorDecisionSystem.SelectByUtility — Fase 4, sem modificação de lógica]
    BehaviorDecisionSystem -->|chorar compete normalmente| EatUtilityOf[UtilityBaseOf — Deficit(fome) já existente]

    Household[Household.Members — Fase 7] -->|coabitação + ações de rotina| Exposure[ExposureAccumulator]
    Exposure --> Progress[MilestoneProgressSystem]
    RateGene[Npc.RateGene — Fase 6] -->|multiplicador de taxa, mesmo padrão de SkillPracticeSystem| Progress
    Progress -->|Resolver.Resolve perfil Agregado, nunca crítico| Resolver[Resolver — ADR-0011]
    Progress -->|janela fechada sem exposição mínima| Ceiling[teto permanente reduzido]

    Trauma[Evento de trauma/doença — gatilho externo] --> Regression[MilestoneRegressionSystem]
    Regression -->|WorldEventKind com causa nomeada| Log[log append-only]

    AggregatePool[AggregatePopulationPool — Fase 8, WealthSum/HealthSum já existentes] -->|+ MilestoneProgressSum por eixo| Materialize[MaterializationSystem.MaterializeOne]
    Materialize -->|resample a partir da média do pool, mesmo padrão de wealthPerHead/healthPerHead| Progress

    HeredityService[HeredityService.DeriveUpbringing — canal ambiental Fase 7] --> BeliefCopy[cópia de crença/traço, mesmo canal, sem novo]
```

Nenhuma edição na lógica de seleção de `BehaviorDecisionSystem`/`SkillCurve`/`HeredityService`/
`AggregatePopulationPool`/`MaterializationSystem` — este design só adiciona o filtro de
elegibilidade, o acumulador de exposição, o sistema de progresso/regressão, e um campo agregado
novo no pool (mesma forma de `WealthSum`/`HealthSum`).

---

## Code Reuse Analysis

| Peça | Reusa | Novo |
| --- | --- | --- |
| Catálogo de ações | `ActionCatalog`/`ActionType` (Fase 4, `src/LivingWorld.Domain/Behavior/`) — mesmo padrão de dicionário obrigatório por `ActionType` já usado por `MaxDurationHours` | `RequiredMilestone` — dicionário `ActionType -> (Axis, Threshold)`, mesma disciplina de `ActionCatalog.Create` validando cobertura total via `Enum.GetValues<ActionType>()` |
| Filtro de candidatos | `BehaviorDecisionSystem.SelectByUtility` (Fase 4) — mesmo loop `foreach (var action in AllActions)` | `MilestoneEligibilityFilter` — filtra `AllActions` ANTES do loop de utilidade, nunca dentro dele |
| Choro pontuado pela fome | `UtilityBaseOf`/`EatUtilityOf`/`Deficit(npc.HungerAt(tick))` (Fase 4, `NeedsRules`) — sem modificação | `ActionType.Cry` com `RequiredMilestone` no limiar mínimo (nenhum eixo exigido), pontuado pelo mesmo `Deficit` de fome |
| Predisposição/taxa de aquisição | `Npc.RateGene`/`RateGene.Inherit`/`SkillPracticeSystem`'s uso de `RateGene.Value` como multiplicador (Fase 6) — mesmo padrão, literal | `MilestoneProgressSystem` consome `npc.RateGene.Value` exatamente como `SkillPracticeSystem` consome |
| Rolagem de progresso | `Resolver.Resolve`/`VarianceProfileCatalog.Get("Agregado")` (ADR-0011, confirmado sem crítico — `RollAgregado` nunca atinge os branches de `IsNatural1`/`IsNatural20`/`IsTailEvent`) | Dificuldade multi-fator modelada no mesmo espírito de `CombatMechanic.PrepareEffect` (combina `Vitality`/`RateGene`/atributos numa única difficulty) |
| Canal ambiental | `HeredityService.DeriveUpbringing`/`DeriveUpbringingFromConceptionStock` (Fase 7) — canal já estruturalmente separado do genético (nunca lê valor genético, provado por construção) | Nenhuma modificação — cópia de crença/traço desta fase só ADICIONA conteúdo transmitido pelo canal, nunca toca a mecânica |
| Coabitação/cuidador | `Household.Members`/`Npc.MotherId`/`Npc.FatherId` (Fase 7) | `ExposureAccumulator` deriva "quem é cuidador" da interseção `household.Members` × adultos, sem novo conceito de relação |
| Materialização/agregado | `AggregatePopulationPool` (`WealthSum`/`HealthSum`), `MaterializationSystem.MaterializeOne` (resample de `wealthPerHead`/`healthPerHead` a partir da média do pool — **não** existe "reconciliação por replay", o padrão real é resample; corrigido aqui vs. a suposição inicial do roadmap) | `MilestoneProgressSum` por eixo no pool (mesma forma de `WealthSum`), resample de `progressPerHeadPerAxis` no `MaterializeOne`, mesmo padrão exato de `wealthPerHead` |
| Enumeração por reflexão de cobertura total | `ActionCatalogTests.Create_fails_naming_the_action_missing_a_declared_duration` (Fase 4) — clone direto do padrão, trocando `MaxDurationHours` por `RequiredMilestone` | `MilestoneCoverageGuardTests` — mesmo shape |

---

## Components / Interfaces

```csharp
// Domain — src/LivingWorld.Domain/Ontogeny/DevelopmentAxis.cs (novo namespace)
public enum DevelopmentAxis { GrossMotor, FineMotor, Language, SelfCare, SocialCognition, Abstraction }

public sealed record MilestoneDefinition(
    string Id, DevelopmentAxis Axis,
    int WindowStartAge, int WindowMedianAge, int WindowLimitAge, // anos — dado de cenário
    IReadOnlyList<string> RequiredMilestoneIds); // pré-requisitos, nunca enum de código

// Domain — aditivo em ActionCatalog (Fase 4), mesma disciplina de MaxDurationHours
public sealed record MilestoneRequirement(DevelopmentAxis Axis, double Threshold); // [0,1)
// ActionCatalog ganha: IReadOnlyDictionary<ActionType, MilestoneRequirement> RequiredMilestone
// validado em Create() com o MESMO padrão de Enum.GetValues<ActionType>() já usado por MaxDurationHours

// Domain — estado por NPC, um double por eixo (6 campos)
public sealed record DevelopmentState(
    IReadOnlyDictionary<DevelopmentAxis, double> Progress, // [0,1) por eixo
    IReadOnlyDictionary<DevelopmentAxis, double> Ceiling); // teto permanente, default 1.0, cai por janela perdida
```

| Componente | Responsabilidade |
| --- | --- |
| `MilestoneEligibilityFilter` | Chamado ANTES de `BehaviorDecisionSystem.SelectByUtility` iterar `AllActions`: remove do conjunto qualquer `ActionType` cujo `RequiredMilestone.Threshold` o `DevelopmentState.Progress` do NPC ainda não atingiu. Nunca modifica `SelectByUtility` em si — só reduz a lista de entrada |
| `ExposureAccumulator` | A cada tick, soma exposição por eixo a partir de ações de rotina de adultos do mesmo `Household.Members` (fala dirigida, contato físico, brincadeira, ensino) — nenhum campo novo alimentado fora de ações já existentes na Fase 4/7 |
| `MilestoneProgressSystem` | Pra cada NPC ainda dentro de alguma janela: calcula dificuldade multi-fator (idade dentro da janela, exposição acumulada, `1/RateGene.Value` — mesmo espírito de `CombatMechanic`), chama `Resolver.Resolve` com perfil `Agregado`, aplica o delta de progresso ao eixo |
| `WindowClosureSystem` | Ao NPC ultrapassar `WindowLimitAge` de um marco sem a exposição mínima do cenário: reduz `Ceiling[Axis]` permanentemente (nunca recupera); registra evento |
| `MilestoneRegressionSystem` | Consumido por gatilho externo (trauma/doença, fora de escopo aqui — só a reação): aplica delta negativo ao `Progress`, respeitando `Ceiling`/piso de consolidação; sempre acompanhado de `WorldEventKind` com causa nomeada no mesmo tick |
| `LanguageFluencyResolver` | Marco de linguagem cujo alvo é o idioma de maior `ExposureAccumulator` entre os cuidadores — nunca o idioma da etnia/cultura declarada do NPC |
| `OntogenyLifecycleGate` | Consultado por todos os sistemas acima: retorna `false` (sistema inerte) assim que a última janela declarada fechou pro NPC — nenhum recálculo ocorre depois disso, custo cai a zero pra adultos desenvolvidos |

---

## Data Models

```csharp
// WorldEventKind (aditivo)
MilestoneWindowClosed    // campos: npcId, axis, finalCeiling, tick
MilestoneRegressed        // campos: npcId, axis, delta, causeEventId, tick
MilestoneAcquired          // campos: npcId, axis, milestoneId, tick

// AggregatePopulationPool (Fase 8, aditivo — mesma forma de WealthSum/HealthSum)
// + campo por eixo: MilestoneProgressSum (Dictionary<DevelopmentAxis, double> ou 6 doubles nomeados)

// Regra de cenário nova
public sealed record OntogenyRules(
    IReadOnlyList<MilestoneDefinition> Milestones,
    double MinimumExposureForWindow,   // por eixo, ou por marco — decisão de implementação
    double LateExposureRecoveryFactor); // < 1.0, garante tardia < prazo sempre
```

**Reconciliação de criança agregada (ONT-70..72)**: corrigido vs. a suposição da spec — o padrão
real do código NÃO é "replay/reconciliação", é **resample determinístico a partir da média do
pool**, exatamente como `MaterializationSystem.MaterializeOne` já faz pra `wealthPerHead`/
`healthPerHead`. `MilestoneProgressSum` por eixo é somado ao pool a cada tick (taxa média do
agregado), e na materialização o NPC recebe `progressPerHeadPerAxis = MilestoneProgressSum[axis]
/ Count` como valor inicial de `DevelopmentState.Progress[axis]` — determinístico pela mesma
seed, sem replay de histórico individual (que nunca existiu, pois NPC agregado não tem estado
por-indivíduo).

Nenhum campo existente de `ActionCatalog`/`Npc`/`Household`/`AggregatePopulationPool`/
`MaterializationSystem`/`HeredityService` muda de tipo ou significado — tudo aditivo.

---

## Error Handling Strategy

| Cenário | Comportamento |
| --- | --- |
| Marco declara pré-requisito de outro marco inexistente/inatingível no catálogo | Erro de configuração de cenário explícito na carga do `OntogenyRules` — nunca trava em dependência circular silenciosa |
| Criança sem nenhum cuidador (household vazio de adultos) | `ExposureAccumulator` retorna o piso mínimo declarado no cenário (default 0) — nunca erro |
| Dois cuidadores com exposição EXATAMENTE igual em idiomas diferentes | `LanguageFluencyResolver` usa desempate determinístico (ordem por `NpcId` do cuidador, mesma seed) — nunca resultado ambíguo |
| Janela fecha no MESMO tick em que a exposição mínima é atingida | Decisão de Design: conta como **"no prazo"** — a checagem de `WindowClosureSystem` roda DEPOIS de `MilestoneProgressSystem` no mesmo tick, então a exposição mínima já foi contabilizada antes do fechamento ser avaliado |
| `ActionCatalog.Create` recebe um `ActionType` sem `RequiredMilestone` declarado | Falha explícita nomeando a ação — mesmo padrão exato de `Create_fails_naming_the_action_missing_a_declared_duration` (Fase 4) |

## Risks & Concerns

| Risco | Mitigação |
| --- | --- |
| `MilestoneEligibilityFilter` rodar em todo NPC (inclusive adultos) e desperdiçar custo | `OntogenyLifecycleGate` é a primeira checagem — NPC fora de qualquer janela pula o filtro inteiro, custo O(1) por NPC adulto |
| Dificuldade multi-fator (idade+exposição+predisposição) divergir sutilmente do padrão de `CombatMechanic` e introduzir uma "segunda fórmula" não documentada | Mesma disciplina explícita já usada — fórmula documentada em código com referência cruzada ao padrão de `CombatMechanic.PrepareEffect`, revisão de Tasks garante 1 fórmula só |
| `MilestoneProgressSum` no pool crescer sem bound conhecido (soma acumulada, não média corrente) | Mesmo padrão de `WealthSum`/`HealthSum` já resolve isso (são somas correntes, divididas por `Count` no resample) — não é um problema novo desta fase |
| Recuperação tardia (`LateExposureRecoveryFactor`) precisar ser calibrado pra garantir `tardia < prazo` sempre, nos 18/20 seeds do critério | Fator é constante `< 1.0` aplicada estruturalmente ao delta de progresso no braço tardio — matematicamente garante `tardia < prazo` por construção, não depende de calibração fina (mitiga o risco na arquitetura, não no ajuste fino) |

## Tech Decisions

| Decisão | Nível | Registro |
| --- | --- | --- |
| Marco é filtro pré-utility, nunca ação pontuando zero | Feature (21) | Decisão confirmada com o usuário — barato, evita custo de utilidade de ação impossível |
| Choro é `ActionType` comum sem script especial | Feature (21) | Decisão confirmada com o usuário — consistente com "The Sims", nada hardcoded |
| Criança agregada usa RESAMPLE (não replay/reconciliação) — corrige a suposição inicial da spec | Feature (21) | Pesquisa de código confirmou que `MaterializationSystem` não tem mecanismo de reconciliação por replay — resample é o único padrão real, e já é suficiente pro requisito (determinístico pela seed) |
| Predisposição usa `Npc.RateGene` literal, mesmo campo da Fase 6 | Feature (21) | Nenhum campo de "predisposição de ontogenia" paralelo — reuso direto |
| Sistema para (custo zero) após a última janela — `OntogenyLifecycleGate` | Feature (21) | Decisão confirmada com o usuário |
