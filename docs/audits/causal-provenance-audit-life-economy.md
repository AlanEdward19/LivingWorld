# Auditoria causal: vida, família e economia

Parte de [causal-provenance-audit.md](causal-provenance-audit.md). Legenda: **T** transformação,
**R** raiz. Fonte = `SourceSystem` real passado hoje; causa = `CauseEventId` passado hoje.
Caminhos relativos a `src/LivingWorld.Simulation/`.

## Mortes (todas via `NpcDeath.Apply`, `Population/Lifecycle/NpcDeath.cs:28`, fonte `NpcDeath`, sem causa)
| Kind | Papel | Chamador (arquivo:linha) | Causa proximal correta |
|---|---|---|---|
| `Death` | T | `Population/Lifecycle/MortalitySystem.cs:31`; `Extraordinary/Systems/CombatEncounterSystem.cs:61,68` | idade: raiz natural; combate: `CombatRound`/`CombatResolved` que zerou a vida |
| `Starvation` | T | `Behavior/Needs/NeedsDecaySystem.cs:92` | início da escassez de comida da cidade (condição) ou último `WageUnpaid`/`Evicted` do household |
| `Dehydration` | T | `Behavior/Needs/NeedsDecaySystem.cs:53` | escassez de água da cidade (condição); mesmo fallback |
| `Exhaustion` | T | `Behavior/Needs/NeedsDecaySystem.cs:66` | `Evicted` do household (sem teto) ou condição de sobrecarga de trabalho |
| `Disease` | T | `Health/OutbreakSystem.cs:51` (evento extra em `:50`, fonte `OutbreakSystem`, sem causa) | `OutbreakStarted` da cidade |
| `MaternalDeath` | T | `Population/Lifecycle/NatalitySystem.cs:93` | evento do parto (`Birth`/`StillBirth`); hoje a morte é aplicada antes deles |

## Trabalho, renda e moradia
| Kind | Papel | Emissor (arquivo:linha) | Fonte real | Causa hoje | Causa proximal correta |
|---|---|---|---|---|---|
| `Hired` | T | `Economy/Labor/EmploymentSystem.cs:55`; `Behavior/Decision/BehaviorDecisionSystem.Profession.cs:94` | sim | não | vaga aberta: `Fired`/`Death` do antecessor ou nova oficina |
| `Fired` | T | `EmploymentSystem.cs:40`; `BehaviorDecisionSystem.Profession.cs:87`; `Players/PlayerCommands.cs:86` | sim | não | `WageUnpaid` (empregador sem caixa), troca de ofício (decisão) ou comando do jogador (raiz) |
| `WageUnpaid` | T | `Economy/Labor/WagePaymentSystem.cs:53` | sim | não | tesouro da oficina vazio (condição `EmployerInsolvent`) |
| `RentUnpaid` | T | `Cities/LodgingSystem.cs:88` | sim | não | `Fired`/`WageUnpaid` que tirou a renda do household |
| `Evicted` | T | `Cities/LodgingSystem.cs:93` | sim | não | último `RentUnpaid` do household (mesmo `LodgingSystem`) |
| `EstateSettled` | T | `Population/Lifecycle/EstateSystem.cs:47` | sim | não | `Death` do falecido (na mesma chamada de `NpcDeath`) |
| `AidGiven` | T | `Population/Family/MutualAidSystem.cs:116` | sim | não | condição de necessidade do receptor (`NeedDeclared`) |
| `TheftDiscovered` | T | `Behavior/Decision/TheftResolver.cs:53` | sim | não | necessidade do ladrão (fome: condição de escassez) |

## Família e desenvolvimento
| Kind | Papel | Emissor (arquivo:linha) | Fonte real | Causa hoje | Causa proximal correta |
|---|---|---|---|---|---|
| `Birth` | R | `Population/Lifecycle/NatalitySystem.cs:148` (fauna `Ecology/FaunaLifecycleSystem.cs:104`; flora `Ecology/FloraLifecycleSystem.cs:245`) | sim | não | raiz: nascimento natural |
| `StillBirth` | R | `NatalitySystem.cs:100` | sim | não | raiz: desfecho obstétrico natural |
| `CourtshipStarted` | R | `Population/Family/CourtshipSystem.cs:213` | sim | não | raiz: afinidade entre NPCs |
| `CourtshipRejected` | R | `CourtshipSystem.cs:190,201` | sim | não | raiz (rejeição por regra de elegibilidade) |
| `CourtshipSucceeded` | T | `CourtshipSystem.cs:96` | sim | não | `CourtshipStarted` do mesmo par |
| `Marriage` | T | `Population/Family/MarriageSystem.cs:42` | sim | não | `CourtshipSucceeded` do mesmo par |
| `OrphanTakenIn` | T | `Population/Family/OrphanCareSystem.cs:78` | sim | não | `Death` do último cuidador da criança |
| `PowerInherited` | T | `NatalitySystem.cs:191` | sim | **sim** (`Birth`) | `Birth` do filho (já correto) |
| `MilestoneRegressed` | T | `Ontogeny/MilestoneProgressSystem.cs:57` | sim | não | `Disease` ou infecção que causou a regressão |
