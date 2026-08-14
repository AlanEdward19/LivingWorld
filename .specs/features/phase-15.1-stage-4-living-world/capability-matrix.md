# Stage 4 Living-World Capability Matrix

**Purpose**: canonical design inventory for “engine capability → frontend use”.
`DiagnosticOnly` is allowed only for mechanics that do not describe or change the world.

| Capability ID | Engine sources | Frontend use | Channel |
| --- | --- | --- | --- |
| TIME | World clock, simulation control/status | Tick, date/year, speed, pause, period | HUD |
| GEO | World map, terrain, pathfinder, movement cost | Terrain, valid routes, destinations | Map |
| NEEDS | `NeedsDecaySystem`, health state | Need levels, urgency, reason for action | NPC inspector, cues |
| BEHAVIOR | `BehaviorDecisionSystem`, activity plans | Named action, intent, route/blocked state | Map, NPC inspector |
| REST | sleep action, rest-place quality | Destination, ground/house/bed quality, recovery, `Zzz` | Map, NPC/building |
| FOOD | eat/cook processes, edibility/preparation catalog | Raw/prepared item, recipe progress, consumption | Map, NPC/building |
| CROPS | plant/water/grow/harvest processes | Plot state, maturity, water demand, harvest | Map, city/building |
| WATER | source/collect/carry/deliver processes | Source, route, carried amount, destination stock | Map, NPC/city |
| EMPLOYMENT | `EmploymentSystem`; Hired/Fired | Job, employer, vacancy and demand | NPC/city/building, timeline |
| PRODUCTION | `ProductionSystem`, staged recipes; ResourceLost | Inputs, process progress, outputs, loss | City/building, timeline |
| MARKET | `MarketPricingSystem` | Prices and changing local economy | City inspector |
| WAGES | `WagePaymentSystem`; WageUnpaid | Pay, wealth pressure, unpaid wages | NPC/city, timeline |
| MONEY | Minted/Destroyed events | Monetary changes with cause | City inspector, timeline |
| SKILLS | `SkillPracticeSystem`, `SkillTeachingSystem` | Skill levels/progress and teaching | NPC inspector, cues |
| RELATIONSHIPS | `RelationshipSystem`, `CourtshipSystem`, marriage; courtship/marriage events | Bonds, spouse, courtship outcomes | NPC inspector, timeline |
| BIRTH | `NatalitySystem`; Birth/MaternalDeath/StillBirth | Family growth and outcomes | NPC/city, timeline |
| DEATH | `MortalitySystem`; Death/Starvation | Removal, cause, historical person | Map, NPC/history, timeline |
| ARCHIVE | `ColdArchiveSystem` | Archived/deceased history without live token | History/biography |
| CITY_GROWTH | `CityGrowthSystem` | Population/capacity trend | City inspector |
| CONSTRUCTION | `ConstructionSystem`, city demand | Queue, progress, completed building/workplace | Map, city/building, timeline |
| MIGRATION | `MigrationSystem` | Household route and membership on arrival | Map, city/NPC, timeline |
| MATERIALIZATION | `MaterializationSystem`, LOD | Aggregate count versus focused identities | HUD, map, inspector |
| FOUNDING | `SettlementFoundingSystem` | Expedition, route, distinct new settlement | Map, timeline |
| HISTORY_FACT | fact scheduler; FactRecorded/ReportConverted/CompensatingCorrection | Permitted beliefs and reports | Knowledge, timeline |
| HISTORY_BOOK | `BookRediscoverySystem`; BookLost/BookRediscovered | Book state and rediscovered knowledge | Knowledge, timeline |
| NARRATIVE | `ChronicleGenerationSystem`, chronicles, biographies | Engine-grounded world/city/NPC narration | Timeline, inspectors |
| CONVERSATION | conversation session system/API | Talk to selected NPC; show validated outcome | NPC interaction |
| PERIOD | `PeriodEvolutionSystem`, period catalog | Current period, labels and transformation | HUD, timeline |
| EXAMPLE_COUNTER | `ExampleCounterSystem` | Explicitly excluded: scheduler diagnostics only | DiagnosticOnly |

## Completeness Rules

1. Every concrete `ISimulationSystem` maps to one row; helper services map through their owning row.
2. Every `WorldEventKind` maps to one non-diagnostic row and a human-readable presentation policy.
3. Each non-diagnostic row names at least one typed React consumer with a representative contract test.
4. Adding a system, event kind, or catalog capability without completing this matrix fails CI.
5. A visible value must drive a decision, explanation, interaction, or world change; debug dumps do not count.
