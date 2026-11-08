# Verificação independente: issue #113, fatia A

Commit verificado: `10f5da56` (base `b29ed9f2`). Verifier sem participação na autoria.

## Veredito: FAIL (2 FAIL bloqueantes, 1 correção menor)

## Critérios
| Id | Resultado | Evidência |
|---|---|---|
| AC1 kind sem classificação reprova | PASS | M1, M2 e M5 abaixo; a mensagem lista o kind |
| AC2 uma lista por kind, só kinds definidos | PASS | M3 e M4; parser próprio: enum 67, T 46, R 21, sem interseção, sem faltas |
| AC3 auditoria com emissor por kind | **FAIL** | F1: emissores de `Death` e `Starvation` omitidos |
| AC4 não muda o mundo | PASS | `git diff --name-status`: 7 arquivos novos e `decisions-log.md`; em `src/` só `CausalPolicy.cs` (sem referência em produção); nenhum `tests/baselines`, cenário ou golden. `GoldenHashesTests`: 4 passam, 1 skip |
| AC5 docs até 100 linhas | PASS | `wc -l`: índice 55, partes 40/39/46, plano 62; `check-docs: OK` (só cobre `docs/`, `rules/`; plano medido por `wc`) |
| Regra de causa no plano e em AD | PASS | `plan.md` § Rule; `AD-121` único em `docs/decisions-log.md` |
| Classificação vive no código | PASS | `CausalPolicy.cs`; todos os exemplos de transformação e raiz da issue batem (exceto clima: não há kind) |
| Comportamento novo tem teste (`rules/tests.md`) | **FAIL** | F2: `IsTransformation` sem teste, mutante M8 sobrevive |
| Escopo só fatia A | PASS | Nenhum emissor, `NpcDeath` ou overload legado alterado; nenhum kind novo |
| `bash scripts/verify.sh` | PASS | exit 0; `Passed: 2467, Skipped: 3, Failed: 0`; `verify: OK` |
| Custo (`compare-simulation-cost.sh`) | Adiado | Fora desta verificação por instrução do orquestrador |

## FAIL
**F1. Emissores omitidos pela auditoria.** Extração própria de todas as 96 ocorrências de `LogEvent(` em
`src/`, cruzada com as citações por kind. Três chamadas não citadas no kind: uma é `Disease` `:50`, citada em
prosa (ok); as outras duas são omissões reais:
- `Ecology/FloraLifecycleSystem.cs:327`: `LogEvent(WorldEventKind.Death, ..., sourceSystem: SystemName)`, morte de planta, sem causa.
- `Ecology/FaunaLifecycleSystem.cs:185`: `LogEvent(kind, ...)`, recebe `Starvation` de `:54` e `Death` de `:174`, sem causa.

Com isso, `docs/audits/causal-provenance-audit-life-economy.md:7` ("todas via `NpcDeath.Apply`") é falso.
Os dois sistemas estão em `ScenarioRunner.Systems.cs:86-87`. A fatia D exigirá causa em todo `Death`/`Starvation`,
e a fatia B só trata `NpcDeath`: fauna e flora ficariam descobertas.
**Correção:** citar os emissores nas linhas `Death` e `Starvation`, corrigir o título da seção e dizer a causa
proximal de morte animal/vegetal (ou registrar a decisão de tratá-las à parte).

**F2. `CausalPolicy.IsTransformation` sem teste.** Mutante M8 inverte o método e os 8 testes passam.
`grep` confirma: nenhuma outra referência a `CausalPolicy` em `src/` ou `tests/` fora do par novo. O plano o nomeia
"fonte da verdade" das fatias B, C e D.
**Correção:** em `CausalPolicyTests.cs`, cobrir `IsTransformation` (transformação, raiz e kind não classificado).

**Correção menor.** `life-economy.md:14` diz "fonte `OutbreakSystem`"; o valor real é `health-outbreak` (`OutbreakSystem.cs:20`).

## Amostragem da auditoria
Mecânico: as 101 citações `arquivo:linha` das três partes resolvem; 97 caem na linha do kind e 4 na abertura da
chamada multilinha (kind na linha seguinte). Conferência manual de emissor, fonte e causa:

| Parte | Kind e citação | Fonte no código | Causa no código | OK |
|---|---|---|---|---|
| vida | `Starvation` `NeedsDecaySystem.cs:92` | `NpcDeath` | não | sim |
| vida | `Fired` `EmploymentSystem.cs:40` | `EmploymentSystem` | não | sim |
| vida | `Fired` `PlayerCommands.cs:86` | `PlayerCommands` | não | sim |
| vida | `RentUnpaid` `LodgingSystem.cs:88` | `LodgingSystem` | não | sim |
| vida | `Evicted` `LodgingSystem.cs:93` | `LodgingSystem` | não | sim |
| vida | `EstateSettled` `EstateSystem.cs:47` | `SystemName` | não | sim |
| vida | `TheftDiscovered` `TheftResolver.cs:53` | `BehaviorDecisionSystem.SystemName` | não | sim |
| vida | `PowerInherited` `NatalitySystem.cs:191` | `NatalitySystem` | `causeEventId` | sim |
| cidade | `CityMerged` `SpatialSettlementFoundingSystem.cs:203` | literal | não | sim |
| cidade | `TechnologyDiscovered` `TechDiscoverySystem.cs:96` | `tech-discovery` | não | sim |
| cidade | `PlayerDisappeared` `PlayerCommands.cs:77` | `PlayerCommands` | não | sim |
| cidade | `BookLost` `BookOperations.cs:59` | `BookOperations` | não | sim |
| cidade | `SettlementFounded` sem emissor | só `[LivingCapability]` `:27` | n/a | sim |
| extra | `ExtraordinaryCostPaid` `ExtraordinaryInvocationEngine.cs:80` | `ExtraordinaryInvocationEngine` | `priorId` | sim |
| extra | `ExtraordinaryCostPaid` `ExtraordinaryLocomotion.cs:147` | literal | não | sim |
| extra | `ExtraordinaryManifested` `ExtraordinaryStateSystem.cs:53` / `:97` | `SystemName` | não / `acquiredId` | sim |
| extra | `Minted` `MatterTransmuteMechanic.cs:50` | literal | `destroyedId` | sim |
| extra | `ExtraordinaryConstructRemoved` `ExtraordinaryConstructOperations.cs:31` | literal | `damagedId` | sim |

Resumo do índice confirmado: nenhuma chamada de 2 argumentos (overload legado); passam causa só os emissores listados.
Sem emissor confirmados: `SettlementFounded`, `FactRecorded`, `CompensatingCorrection`; migração não emite evento.

## Mutações (`bash scripts/test.sh --filter "FullyQualifiedName~CausalPolicyTests"`)
| Id | Mutação | Resultado |
|---|---|---|
| M0 | controle sem mutação | 8 passam |
| M1 | remove `Trade` de `Roots` | 2 reprovam; lista `Trade` |
| M2 | remove `Death` de `Transformations` | 3 reprovam; lista `Death` |
| M3 | `Death` nas duas listas | 8 reprovam (`TypeInitializationException`) |
| M4 | `(WorldEventKind)9999` em `Roots` | 2 reprovam; "9999 não existe no enum" |
| M5 | kind novo no enum sem classificar | 2 reprovam; lista o kind |
| M6 | `Birth` movido para `Transformations` | 1 reprova |
| M7 | `RoleOf` assume `Root` para desconhecido | 1 reprova |
| M8 | `IsTransformation` invertido | **sobrevive: 8 passam** (F2) |

Cada mutação restaurada com `git checkout --`; `git status --short` vazio ao final.

## Observações para as fatias B a D (não bloqueiam a A)
- `Death` é T, mas a própria auditoria dá "idade: raiz natural"; o mesmo vale para `Fired` por comando do jogador e para o 1º `PowerInvoked` (`BehaviorDecisionSystem.Effects.cs:58`, `causeEventId: null`). O gate D precisa de exceção por emissor ou de outra classificação.
- `EstateSettled` e `NpcInstantiated` por morte têm causa proposta `Death`, mas são emitidos antes dela (`NpcDeath.cs:23,25` antes de `:28`); reordenar muda ids de evento.
- `CausalPolicy` ainda não tem consumidor de produção: registrar como `FUTURE_DEPENDENCY` em `living-world-cohesion-audit.md` ao fechar.
