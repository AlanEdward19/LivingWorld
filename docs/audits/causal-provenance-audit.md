# Auditoria de proveniência causal (issue #113, fatia A)

Índice. Inventário de **todo** `WorldEventKind` (67): quem emite, se passa `SourceSystem` real,
se passa causa e qual é a **causa proximal correta**. Base: `b29ed9f2`. Só leitura: nada foi
mudado em emissor, e o hash canônico do mundo não muda nesta fatia.

## Partes
| Parte | Cobre |
|---|---|
| [vida, família e economia](causal-provenance-audit-life-economy.md) | mortes, trabalho, moradia, herança, família |
| [cidade, saúde, jogador e histórico](causal-provenance-audit-city-history.md) | fundação, surto, tecnologia, jogador, autoria, livros |
| [extraordinário, combate e ecologia](causal-provenance-audit-extraordinary.md) | poderes, combate, flora, fauna, contabilidade |

## Regra de causa (AD-121)
*Causa = o evento registrado mais próximo que tornou a transição possível.*
- **Transformação** (precisa de `CauseEventId` e `SourceSystem` real): morte, despejo, contratação,
  demissão, fundação, fusão, surto, doença, tecnologia, herança, casamento, órfão acolhido...
- **Raiz** (pode não ter causa): nascimento natural, comando de autoria/jogador, acidente,
  contabilidade, evento que abre uma cadeia (`ExtraordinaryUseAttempted`).
- A lista vive em código: `CausalPolicy` (`src/LivingWorld.Simulation/History/Causality/`).
  `CausalPolicyTests` reprova se um kind ficar sem classificação.
- Quando a causa é uma **condição** sem evento (comida da cidade zerada, aluguel atrasado),
  emite-se **um evento de transição de condição por cidade/household quando a condição
  começa** (e outro quando termina), nunca um por NPC por tick. Custo limitado e medido
  em `tests/baselines/event-log-bytes.json`.

## Resumo
- Classificados: **46 transformações** e **21 raízes** (67 no total).
- `SourceSystem`: **todo emissor atual passa um nome real**. Nenhum chamador usa o overload
  legado `LogEvent(kind, payload)` (grep em `src/`); o `"Unknown"` existe como padrão do overload legado e de `WorldEvent`, e é usado como
  valor de reserva em `Api/World/SavedWorldLibrary.cs:61` (importação de log legado sem fonte).
- Causa: só passam `CauseEventId` `PowerInvoked`, `PowerInherited`, a cadeia do
  `ExtraordinaryInvocationEngine`, `Minted` da transmutação, `ConstructRemoved` (dano) e
  `ExtraordinaryManifested` (após `Acquired`). **Todo o resto das transformações não passa causa.**
- `NpcDeath.Apply` (`Population/Lifecycle/NpcDeath.cs:28`) não aceita causa: afeta 6 kinds de morte.

- Varredura por grep de todo `LogEvent` e de `NpcDeath.Apply`/`Kill` em `src/` contra as partes:
  as mortes de fauna e flora (`Kill`) foram incluídas em `Death` e `Starvation`.

## Divergências com o texto da issue (para o dono)
- `SettlementFounded` **não tem emissor**: só aparece no `[LivingCapability]` de
  `SettlementFoundingSystem.cs:27`, no catálogo de apresentação e em `Api/Visual/Scope/LivingScopeState.cs:245`
  (que o fabrica para a UI a partir de `FoundedFromCityId`, sem evento no log). Só `CityMerged` é emitido.
- `MigrationSystem` e `RelocationArrivalSystem` **não emitem evento algum**; migração hoje é
  invisível no log. A fatia C precisa criar o kind (a issue já prevê).
- `FactRecorded` e `CompensatingCorrection` não passam por `LogEvent` (só criam `Fact`).
- `DecisionMetrics.cs:96` monta um `WorldEvent` à mão para medir; não é emissor de produção.

## Emissores que não passam por `LogEvent` (varredura de `new WorldEvent(` em todo `src/`)
| Local | O que faz |
|---|---|
| `Scheduling/TickContext.cs:29,30` | o próprio `LogEvent`; único caminho de emissão do motor |
| `Infrastructure/EventLog/EventLogCompactor.cs:52` | cria `WageUnpaid`/`ResourceLost`/`PlantMatured` agregados: fonte `EventLogCompactor`, `EventId = 0`, sem causa; produção (`PersistentWorldRunner.cs:41`, `Workers/Program.cs:75`) |
| `Api/World/SavedWorldLibrary.cs:60` | reimporta log legado preservando id, causa e fonte (`"Unknown"` se nula) |
| `Simulation/Behavior/Decision/DecisionMetrics.cs:96`, `History/Distortion/HistoryDistortionDigest.cs:18` | medição e digest de teste, não produção |

## Eventos de condição propostos (decisão do dono, nada criado)
| Proposta | Granularidade | Serviria a |
|---|---|---|
| `FoodShortageBegan` / `FoodShortageEnded` | por cidade | `Starvation`, `Dehydration`, migração |
| `OvercrowdingBegan` / `OvercrowdingEnded` | por cidade | `SettlementFounded`, `OutbreakStarted` |
| `MigrationDecided` (kind novo de transição) | por NPC/família | migração e realocação |
| `TravelPressureObserved` | por cidade + cultura | `TechnologyDiscovered` |
| `NeedDeclared` (necessidade de casa em apuros) | por household | `AidGiven`, `TheftDiscovered` |
| `EmployerInsolvent` | por oficina | `WageUnpaid`, `Fired` |

Cada um muda `NextHistoryEventId` e talvez `Facts`: hash e goldens mudam (AD citando a #113,
pergunta ao dono antes). Dono decide também a granularidade (cidade x household).
