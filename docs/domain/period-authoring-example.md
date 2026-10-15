# Autoria de período — exemplo completo (PERIOD-12)

Volta pro [índice](period-authoring.md). Este é o `periodDefinition` **completo** aceito por
`POST /periods`, sem transformação. O teste
`PeriodAuthoringGuideTests` (integração) lê o bloco `json` abaixo do disco, envia ao endpoint
e exige `201` + `GET /periods/{id}`: se este exemplo ficar inválido, o gate reprova.

Economia e Cidades vêm desligadas (`false`) com todos os campos presentes — o bloco inteiro é
obrigatório (ver [schema](period-authoring-schema.md)). `Dynamics` é opcional e fica fora; veja
[dynamics](period-authoring-dynamics.md). Envelope e respostas: [fluxo](period-authoring-flow.md).

```json
{
  "Width": 20,
  "Height": 20,
  "Seed": 1,
  "RegionSize": 5,
  "TerrainIds": [1, 2, 3],
  "BiomeIds": [1],
  "ResourceIds": [],
  "CostWeights": {"Base": 0.3, "AltitudeWeight": 0.05, "TerrainWeight": {"1": 1.0, "2": 1.5, "3": 3.0}},
  "Settlements": [{"Name": "vila", "X": 5, "Y": 5}],
  "InitialPopulation": 20,
  "Culture": 1,
  "VillageX": 5,
  "VillageY": 5,
  "CultureIds": [1],
  "ProfessionIds": [1, 2, 3, 4],
  "LocationTypeIds": [],
  "MaxLongevityYears": 90,
  "LifeTableBrackets": [{"MinAgeYears": 0, "MaxAgeYears": 1, "BaseAnnualMortality": 0.08}, {"MinAgeYears": 2, "MaxAgeYears": 14, "BaseAnnualMortality": 0.01}, {"MinAgeYears": 15, "MaxAgeYears": 39, "BaseAnnualMortality": 0.004}, {"MinAgeYears": 40, "MaxAgeYears": 59, "BaseAnnualMortality": 0.01}, {"MinAgeYears": 60, "MaxAgeYears": 79, "BaseAnnualMortality": 0.04}, {"MinAgeYears": 80, "MaxAgeYears": 89, "BaseAnnualMortality": 0.15}],
  "FertilityMinAge": 16,
  "FertilityMaxAge": 45,
  "AnnualConceptionChance": 0.25,
  "GestationDays": 270,
  "ConditionalInitialMortality": true,
  "MaxBytesPerNpcPerYear": 80000,
  "HungerDecayPerHour": 2.0,
  "ThirstDecayPerHour": 3.0,
  "SleepDecayPerHour": 1.5,
  "SocialDecayPerHour": 1.0,
  "UrgencyThreshold": 70,
  "MaxActionSelectionSteps": 10,
  "HysteresisEnabled": true,
  "ContinuityBonus": 5.0,
  "HomelessSleepEfficiency": 0.5,
  "MaxDurationHours": {"Eat": 2, "Sleep": 8, "Work": 8, "Socialize": 3, "Travel": 4, "Idle": 2, "Buy": 2, "UsePower": 1},
  "RoutineSlots": [{"ProfessionId": 1, "Stage": "Adult", "HourStart": 6, "HourEnd": 14, "Action": "Work"}, {"ProfessionId": 2, "Stage": "Adult", "HourStart": 7, "HourEnd": 15, "Action": "Work"}, {"ProfessionId": 3, "Stage": "Adult", "HourStart": 7, "HourEnd": 15, "Action": "Work"}, {"ProfessionId": 4, "Stage": "Adult", "HourStart": 8, "HourEnd": 16, "Action": "Work"}, {"ProfessionId": null, "Stage": "Adult", "HourStart": 8, "HourEnd": 16, "Action": "Work"}, {"ProfessionId": null, "Stage": "Adult", "HourStart": 18, "HourEnd": 20, "Action": "Socialize"}, {"ProfessionId": null, "Stage": "Adult", "HourStart": 22, "HourEnd": 23, "Action": "Sleep"}, {"ProfessionId": null, "Stage": "Adult", "HourStart": 0, "HourEnd": 5, "Action": "Sleep"}, {"ProfessionId": null, "Stage": "Child", "HourStart": 20, "HourEnd": 23, "Action": "Sleep"}, {"ProfessionId": null, "Stage": "Child", "HourStart": 0, "HourEnd": 6, "Action": "Sleep"}, {"ProfessionId": null, "Stage": "Elder", "HourStart": 21, "HourEnd": 23, "Action": "Sleep"}, {"ProfessionId": null, "Stage": "Elder", "HourStart": 0, "HourEnd": 6, "Action": "Sleep"}],
  "DefaultAction": "Idle",
  "EconomyEnabled": false,
  "FoodResourceId": 1,
  "WaterResourceId": 2,
  "PriceSensitivity": 0.1,
  "WageTaxRate": 0.05,
  "CapacityByResourceLocation": {},
  "SpoilagePerDayByResource": {},
  "WageByProfession": {},
  "PriceFloor": {},
  "PriceCeiling": {},
  "DemandBaselinePerNpc": {},
  "Recipes": {},
  "MarketLocationTypeIds": [],
  "LocationTypeByProfession": {},
  "Workplaces": [],
  "CitiesEnabled": false,
  "FoodShortageThreshold": 0.1,
  "HousingShortageThreshold": 0.1,
  "SecurityShortageThreshold": 0.1,
  "EmigrationRatePerDeficitUnit": 0.1,
  "MigrationEmploymentWeight": 0.1,
  "MigrationFoodWeight": 0.1,
  "MigrationSecurityWeight": 0.1,
  "MigrationFamilyTiesWeight": 0.1,
  "FoundingConcentrationThreshold": 0.1,
  "FoundingResourceThreshold": 0.1,
  "FoundingRouteThreshold": 0.1,
  "FoundingDefensibilityThreshold": 0.1,
  "FoundingLeadershipThreshold": 0.1,
  "OrganizationTicks": 1,
  "MaterializationIdleTicksBeforeEligible": 1,
  "BuildingRecipes": {},
  "Cities": []
}
```
