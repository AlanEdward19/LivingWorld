# Auditoria causal: extraordinário, combate, ecologia e contabilidade

Parte de [causal-provenance-audit.md](causal-provenance-audit.md). **T** transformação, **R** raiz.
Todos os emissores passam `SourceSystem` real. Caminhos relativos a `src/LivingWorld.Simulation/`.

## Ecologia, economia e contabilidade (raízes)
| Kind | Papel | Emissor (arquivo:linha) | Causa hoje | Causa proximal correta |
|---|---|---|---|---|
| `PlantMatured` | R | `Ecology/FloraLifecycleSystem.cs:197` | não | raiz: crescimento por tempo; compactador: `Infrastructure/EventLog/EventLogCompactor.cs:52` (`NoiseKinds` `:18-21`) recria o kind agregado por mês, fonte `EventLogCompactor`, `EventId = 0`, sem causa. |
| `ResourceLost` | R | `Economy/Production/ProductionSystem.cs:108,172`; `CropSystem.cs:56`; `FloraLifecycleSystem.cs:292`; `Population/Family/HouseholdCleanup.cs:22` | não | raiz: perda/estrago contábil; compactador: `Infrastructure/EventLog/EventLogCompactor.cs:52` (`NoiseKinds` `:18-21`) recria o kind agregado por mês, fonte `EventLogCompactor`, `EventId = 0`, sem causa. |
| `Minted` | R | `Core/WorldState.Economy.cs:73`; `Extraordinary/Mechanics/MatterTransmuteMechanic.cs:50` | só na transmutação | raiz; na transmutação, o `Destroyed` pareado |
| `Destroyed` | R | `WorldState.Economy.cs:82,96`; `MatterTransmuteMechanic.cs:47` | não | raiz: contabilidade |
| `Trade` | R | `Economy/Trade/TradeLedgerSystem.cs:34` | não | raiz: primeiro contato de compra |

## Cadeia de invocação (já tem causa)
| Kind | Papel | Emissor (arquivo:linha) | Causa hoje | Causa proximal correta |
|---|---|---|---|---|
| `ExtraordinaryUseAttempted` | R | `Extraordinary/Engine/ExtraordinaryInvocationEngine.cs:68` | parâmetro do chamador | decisão (`PowerInvoked`) quando vem do utility AI |
| `ExtraordinaryCostPaid` | T | `ExtraordinaryInvocationEngine.cs:80`; `Mechanics/ExtraordinaryLocomotion.cs:147` | sim no motor; **não** em `Locomotion` | evento anterior da cadeia |
| `ExtraordinaryEffectApplied` | T | `ExtraordinaryInvocationEngine.cs:93`; `Mechanics/MindMechanic.cs:32,77`; `ForesightMechanic.cs:59`; `Behavior/Decision/BehaviorDecisionSystem.cs:123` | sim no motor; **não** nos demais | `CostPaid`/`UseAttempted` da invocação |
| `ExtraordinaryUseFailed` | T | `ExtraordinaryInvocationEngine.cs:400` | sim | `UseAttempted` |
| `ExtraordinaryFailureApplied` | T | `ExtraordinaryInvocationEngine.cs:391` | sim | `UseAttempted` |
| `PowerInvoked` | T | `Behavior/Decision/BehaviorDecisionSystem.Effects.cs:59,65` | sim (a 2ª aponta à 1ª) | decisão (já correto) |

## Aquisição, manifestação e construtos
| Kind | Papel | Emissor (arquivo:linha) | Causa hoje | Causa proximal correta |
|---|---|---|---|---|
| `ExtraordinaryAcquired` | R | `Extraordinary/Systems/ExtraordinaryStateSystem.cs:92,119` | não | raiz: gatilho de cenário ou autoria |
| `ExtraordinaryAcquisitionFailed` | R | `ExtraordinaryStateSystem.cs:80` | não | raiz: gatilho recusado |
| `ExtraordinaryRevoked` | R | `ExtraordinaryStateSystem.cs:134` | não | raiz: intervenção de autoria |
| `ExtraordinaryManifested` | T | `ExtraordinaryStateSystem.cs:53,97` | sim após `Acquired` (`:97`); **não** por condição (`:53`) | `Acquired`, ou condição autorada ativa |
| `ExtraordinaryDormant` | T | `ExtraordinaryStateSystem.cs:53` | não | `Manifested` anterior (condição deixou de valer) |
| `ExtraordinaryCulturalReaction` | T | `ExtraordinaryStateSystem.cs:209` | não | `Manifested` que a cultura interpretou |
| `ExtraordinaryConstructCreated` | T | `Mechanics/ConstructMechanic.cs:60` | não | `EffectApplied` da invocação |
| `ExtraordinaryConstructDamaged` | T | `Mechanics/ExtraordinaryConstructOperations.cs:24` | não | evento que causou o dano |
| `ExtraordinaryConstructRemoved` | T | `ExtraordinaryConstructOperations.cs:31` (sim, ← `Damaged`); `ExtraordinaryStateSystem.cs:32` (**não**, expiração) | parcial | `Damaged`, ou `ConstructCreated` na expiração |

## Combate e identidade
| Kind | Papel | Emissor (arquivo:linha) | Causa hoje | Causa proximal correta |
|---|---|---|---|---|
| `CombatEncounterStarted` | R | `Extraordinary/Systems/CombatEncounterSystem.cs:28` | não | raiz: comando `combat.engage:` |
| `CombatRound` | T | `CombatEncounterSystem.cs:74` | não | `CombatEncounterStarted` (ou round anterior) |
| `CombatResolved` | T | `CombatEncounterSystem.cs:84,113,165`; `Mechanics/CombatMechanic.cs:65` | não | último `CombatRound`; em `CombatMechanic`, o `EffectApplied` |
| `NpcInstantiated` | T | `Mechanics/NpcInstantiationMechanic.cs:89,123,155` | não | `EffectApplied` da invocação (reencarnação: `Death`) |
| `IdentityChanged` | T | `Mechanics/ControlMechanic.cs:100,197`; `Mechanics/AppearanceMechanic.cs:41,53` | não | `EffectApplied` da invocação |
| `PossessionResisted` | T | `ControlMechanic.cs:136` | não | `IdentityChanged` da possessão |
