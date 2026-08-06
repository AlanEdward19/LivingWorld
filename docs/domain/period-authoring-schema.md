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
