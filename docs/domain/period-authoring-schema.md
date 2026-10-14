# Autoria de período — campos obrigatórios

Volta pro [índice](period-authoring.md). Um `periodDefinition` é um único objeto JSON — todos
os blocos abaixo vêm no mesmo nível (raiz do objeto), exceto onde indicado.

## Mapa

| Campo | Tipo | Obrigatório | Nota |
|---|---|---|---|
| `Width`, `Height` | int | sim | dimensões do grid |
| `Seed` | ulong | sim | semente determinística — sobrescrita por `POST /worlds/start` |
| `RegionSize` | int | sim | tamanho de região pra particionamento |
| `TerrainIds`, `BiomeIds`, `ResourceIds` | int[] | não (vazio = sem restrição) | ids válidos |
| `CostWeights.Base`, `CostWeights.AltitudeWeight` | double | sim | custo de movimento |
| `CostWeights.TerrainWeight` | objeto `{ "id": peso }` | não | peso por terreno |
| `Settlements[]` | array de `{ Name, X, Y }` | não | âncoras de assentamento inicial |
| `Cells[]` | array de célula autoral | não | ausente = mapa gerado proceduralmente a partir de `Seed` |

## População

| Campo | Tipo | Obrigatório |
|---|---|---|
| `InitialPopulation` | int | sim |
| `Culture` | int (id de cultura) | sim |
| `VillageX`, `VillageY` | int | sim |
| `CultureIds`, `ProfessionIds`, `LocationTypeIds` | int[] | não (vazio = sem restrição) |
| `MaxLongevityYears`, `LifeTableBrackets`, `FertilityMinAge`, `FertilityMaxAge`, `AnnualConceptionChance`, `GestationDays`, `MaxBytesPerNpcPerYear` | ver `scenarios/default.json` | sim |

`ProfessionIds` vazio significa "qualquer id de profissão é aceito" — usado por
[`Dynamics`](period-authoring-dynamics.md) pra saber se uma profissão citada é válida.

## Comportamento

| Campo | Tipo | Obrigatório |
|---|---|---|
| `HungerDecayPerHour`, `ThirstDecayPerHour`, `SleepDecayPerHour`, `SocialDecayPerHour` | double | sim |
| `UrgencyThreshold`, `MaxActionSelectionSteps` | int | sim |
| `HysteresisEnabled` | bool | sim |
| `ContinuityBonus`, `HomelessSleepEfficiency` | double | sim |
| `MaxDurationHours` | objeto `{ "AçãoConhecida": horas }` | sim |
| `RoutineSlots[]` | array de `{ ProfessionId?, Stage, HourStart, HourEnd, Action }` | sim |
| `DefaultAction` | string (nome de ação conhecida do motor) | sim |

`Stage`/`Action` são nomes de enums **do motor** (`LifeStage`/`ActionType`), fixos — consulte
`scenarios/default.json` pra ver os valores em uso.

## Economia

| Campo | Tipo | Obrigatório |
|---|---|---|
| `EconomyEnabled` | bool | sim |
| `FoodResourceId`, `WaterResourceId` | int | sim |
| `PriceSensitivity` | double | sim |
| `CapacityByResourceLocation` | objeto `{ "resourceId,locationTypeId": long }` | sim |
| `SpoilagePerDayByResource`, `DemandBaselinePerNpc` | objeto `{ "id": double }` | sim |
| `WageByProfession`, `PriceFloor`, `PriceCeiling` | objeto `{ "id": long }` | sim |
| `Recipes` | objeto `{ "locationTypeId": { Inputs, Outputs, MaxWorkersPerCycle, RequiresCellResource? } }` | sim |
| `MarketLocationTypeIds` | int[] | sim |
| `LocationTypeByProfession` | objeto `{ "professionId": locationTypeId }` | sim |
| `Workplaces[]` | array de `{ LocationTypeId, X, Y, MaxVacancies, Treasury, Stock, Prices }` | sim |

`EconomyEnabled: false` ainda exige todos os campos acima presentes (objetos/arrays vazios
quando não há dado) — o bloco inteiro é obrigatório, só o comportamento em runtime muda.

## Cidades

| Campo | Tipo | Obrigatório |
|---|---|---|
| `CitiesEnabled` | bool | sim |
| `FoodShortageThreshold`, `HousingShortageThreshold`, `SecurityShortageThreshold` | double | sim |
| `EmigrationRatePerDeficitUnit`, `MigrationEmploymentWeight`, `MigrationFoodWeight`, `MigrationSecurityWeight`, `MigrationFamilyTiesWeight` | double | sim |
| `FoundingConcentrationThreshold`, `FoundingResourceThreshold`, `FoundingRouteThreshold`, `FoundingDefensibilityThreshold`, `FoundingLeadershipThreshold` | double | sim |
| `OrganizationTicks`, `MaterializationIdleTicksBeforeEligible` | long | sim |
| `BuildingRecipes` | objeto `{ "buildingTypeId": { Inputs, TicksToBuild, HousingCapacityProvided } }` | sim |
| `Cities[]` | array de `{ X, Y, FoundedAtTick, AggregatePool: { Count, WealthSum, HealthSum } }` | sim |

Mesma regra de `EconomyEnabled`: `CitiesEnabled: false` ainda exige os campos presentes.

## Ontogenia (opcional)

| Campo | Tipo | Obrigatório |
|---|---|---|
| `Ontogeny.Enabled` | bool | sim, se o bloco existir (`false` desliga) |
| `Ontogeny.Milestones[]` | `{ Id, Axis, Threshold, WindowStartYears, WindowMedianYears, WindowLimitYears, Requires[] }` | não (padrão: marcos humanos) |
| `Ontogeny.Requirements` | objeto `{ "AçãoDoMotor": { Axis, Threshold } }`, **uma entrada por ação** | não (padrão: humano) |
| `Ontogeny.LateRecoveryFactor` | double em (0,1] | não (padrão 0,25): fração do atraso que a exposição tardia recupera por mês |
| `Ontogeny.MissedWindowKeep` | double em (0,1) | não (padrão 0,5): fração do que falta até 1 que segue alcançável após perder a janela |

Sem o bloco, valem os marcos humanos e a ontogenia fica ligada. `Axis` é um de `GrossMotor`, `FineMotor`, `Language`,
`SelfCare`, `SocialCognition`, `Abstraction`. Cada eixo precisa de ao menos um marco e o último deve ter `Threshold` 1.
`Threshold` 0 em `Requirements` = sempre elegível (o recém-nascido só chora, mama e dorme).
