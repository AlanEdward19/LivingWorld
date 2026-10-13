# Fase 16.1 - closeout (specs-closeout 2026-10-12)

Spec tem 1162 linhas; a tabela de rastreio fica em `spec.md`. Evidencia abaixo conferida
contra arquivos existentes em `tests/LivingWorld.Tests.Unit/Extraordinary/` (listagem real)
e contra o relatorio de validacao PASS arquivado no git (`c8ab6c7^`, validation.md, 2026-08-25).

Ressalva (memoria projeto): o registro de mecanicas e C# fechado (`ExtraordinaryMechanicRegistry`),
nao um motor dinamico/extensivel por dado; catalogo web/UI de poderes nao foi entregue.

| Requisito | Estado | Evidencia |
|---|---|---|
| PWR-01..05 registro | Verified | Engine/ExtraordinaryMechanicRegistryTests, Engine/ExtraordinaryInvocationEngineTests |
| PWR-06..09 area | Verified | Mechanics/AreaTargetResolverTests |
| PWR-10..13 transferencia | Verified | Mechanics/TransferMechanicTests |
| PWR-20..23 senescencia | Verified | SenescenceMechanicTests |
| PWR-24..27 sorte | Verified | Mechanics/LuckMechanicTests |
| PWR-28..31, 80..83 mente/memoria | Verified | Mechanics/MindMechanicTests |
| PWR-32..34 anos de vida | Verified | LifespanTransferTests |
| PWR-35..38 transmutacao | Verified | Mechanics/MatterTransmuteMechanicTests |
| PWR-50..52 carga | Verified | CarryCapacityTests |
| PWR-53..55 producao por forca | Verified | AttributeStrengthProductionTests |
| PWR-56..61 percepcao/reacao | Verified | Unit/Behavior/BehaviorPerceptionTests |
| PWR-62..65 combate | Verified | Mechanics/CombatMechanicTests, CombatStrikeEngageContractTests |
| PWR-70..73 gravidade | Verified | Mechanics/GravityMechanicTests, ExtraordinaryLocomotionTests |
| PWR-74..76 temperatura | Verified | Mechanics/EnvironmentTemperatureMechanicTests |
| PWR-77..79 fauna | Verified | Mechanics/FaunaMechanicTests |
| PWR-101..103 flora | Verified | Mechanics/FloraMechanicTests |
| PWR-90..92 passivo | Verified | Systems/ExtraordinaryPassiveTickSystemTests |
| PWR-93..95 vulnerabilidade | Verified | VulnerabilityMechanicTests |
| PWR-96..98 skill | Verified | Mechanics/SkillMechanicTests (copy, learn-rate, sem residuo) |
| PWR-99..100 fertilidade | Verified | FertilityMechanicTests (validacao antiga notou PWR-99 fraco: afirma taxa, nao contagem de Natality) |
| PWR-104..107 instanciacao | Verified | Mechanics/NpcInstantiationMechanicTests |
| PWR-108..111 identidade | Verified | Mechanics/ControlMechanicTests, PossessionResistanceTests |
| PWR-112..114 vinculo | Verified | Mechanics/BondMechanicTests |
| PWR-115..116 alma | Verified | Mechanics/SoulMechanicTests |
| PWR-117..119 dimensional | Verified | Mechanics/DimensionMechanicTests |
| PWR-120..122 precognicao | Verified | Mechanics/ForesightMechanicTests |
| PWR-40 Power Evolution (P3) | Deferred | entregue depois como Fase 16.2 (ver spec 16.2) |
| PWR-41 mistura genetica (P3) | Deferred | entregue depois como Fase 16.2 (MixDescriptorBuilderTests) |

Contagem: 95 Verified (grupos conferidos membro a membro contra a validacao PASS), 0 Pending, 2 Deferred.
Fora da fase: poderes de tempo (Fase 18).
