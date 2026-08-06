// Feature ad-hoc "criar mundo" (AD-001 em .specs/STATE.md): estado inicial do formulário
// espelha scenarios/default.json (map/population/behavior) + os blocos economy/city/dynamics
// usados em ScenarioLoaderV2Tests.FullValidRoot() — mesmos defaults que já passam no backend,
// só editáveis campo a campo em vez de vir só de um arquivo fixo.

export interface KeyNumberRow {
  key: string;
  value: number;
}

export interface SettlementRow {
  name: string;
  x: number;
  y: number;
}

export interface LifeTableBracketRow {
  minAgeYears: number;
  maxAgeYears: number;
  baseAnnualMortality: number;
}

export interface RoutineSlotRow {
  professionId: number | null;
  stage: "Child" | "Adult" | "Elder";
  hourStart: number;
  hourEnd: number;
  action: ActionTypeName;
}

export type ActionTypeName = "Eat" | "Sleep" | "Work" | "Socialize" | "Travel" | "Idle" | "Buy";

export const ACTION_TYPES: ActionTypeName[] = [
  "Eat",
  "Sleep",
  "Work",
  "Socialize",
  "Travel",
  "Idle",
  "Buy",
];

export interface RecipeRow {
  locationTypeId: number;
  inputs: string; // "resourceId:qty,resourceId:qty"
  outputs: string;
  maxWorkersPerCycle: number;
  requiresCellResource: number | null;
}

export interface BuildingRecipeRow {
  buildingTypeId: number;
  inputs: string;
  ticksToBuild: number;
  housingCapacityProvided: number;
}

export interface WorkplaceRow {
  locationTypeId: number;
  x: number;
  y: number;
  maxVacancies: number;
  treasury: number;
  stock: string;
  prices: string;
}

export interface CityRow {
  x: number;
  y: number;
  foundedAtTick: number;
  count: number;
  wealthSum: number;
  healthSum: number;
}

export interface ProfessionBiasRow {
  professionId: number;
  weight: number;
  name: string;
}

export interface SkillBiasRow {
  skillId: number;
  weight: number;
  name: string;
}

export type TransformationKind = "Emerge" | "Merge" | "Split" | "Disappear";

export interface TransformationRuleRow {
  kind: TransformationKind;
  sourceProfessionIds: string; // csv
  targetProfessionIds: string; // csv
  triggerTick: number | null;
}

export interface ScenarioFormState {
  // Map
  width: number;
  height: number;
  seed: number;
  regionSize: number;
  terrainIds: string; // csv
  biomeIds: string;
  resourceIds: string;
  costWeightsBase: number;
  costWeightsAltitude: number;
  terrainWeight: KeyNumberRow[];
  settlements: SettlementRow[];

  // Population
  initialPopulation: number;
  culture: number;
  villageX: number;
  villageY: number;
  cultureIds: string;
  professionIds: string;
  locationTypeIds: string;
  maxLongevityYears: number;
  lifeTableBrackets: LifeTableBracketRow[];
  fertilityMinAge: number;
  fertilityMaxAge: number;
  annualConceptionChance: number;
  gestationDays: number;
  maxBytesPerNpcPerYear: number;

  // Behavior
  hungerDecayPerHour: number;
  thirstDecayPerHour: number;
  sleepDecayPerHour: number;
  socialDecayPerHour: number;
  urgencyThreshold: number;
  maxActionSelectionSteps: number;
  hysteresisEnabled: boolean;
  continuityBonus: number;
  homelessSleepEfficiency: number;
  maxDurationHours: Record<ActionTypeName, number>;
  routineSlots: RoutineSlotRow[];
  defaultAction: ActionTypeName;

  // Economy
  economyEnabled: boolean;
  foodResourceId: number;
  waterResourceId: number;
  priceSensitivity: number;
  capacityByResourceLocation: KeyNumberRow[]; // key = "resourceId,locationTypeId"
  spoilagePerDayByResource: KeyNumberRow[];
  wageByProfession: KeyNumberRow[];
  priceFloor: KeyNumberRow[];
  priceCeiling: KeyNumberRow[];
  demandBaselinePerNpc: KeyNumberRow[];
  recipes: RecipeRow[];
  marketLocationTypeIds: string;
  locationTypeByProfession: KeyNumberRow[];
  workplaces: WorkplaceRow[];

  // City
  citiesEnabled: boolean;
  foodShortageThreshold: number;
  housingShortageThreshold: number;
  securityShortageThreshold: number;
  emigrationRatePerDeficitUnit: number;
  migrationEmploymentWeight: number;
  migrationFoodWeight: number;
  migrationSecurityWeight: number;
  migrationFamilyTiesWeight: number;
  foundingConcentrationThreshold: number;
  foundingResourceThreshold: number;
  foundingRouteThreshold: number;
  foundingDefensibilityThreshold: number;
  foundingLeadershipThreshold: number;
  organizationTicks: number;
  materializationIdleTicksBeforeEligible: number;
  buildingRecipes: BuildingRecipeRow[];
  cities: CityRow[];

  // Dynamics (opcional no backend; sempre enviado aqui, vazio == sem viés/regra nenhuma)
  professionBiases: ProfessionBiasRow[];
  skillBiases: SkillBiasRow[];
  transformationRules: TransformationRuleRow[];
}

export function defaultScenarioForm(): ScenarioFormState {
  return {
    width: 10,
    height: 10,
    seed: 1,
    regionSize: 5,
    terrainIds: "1, 2, 3",
    biomeIds: "1",
    resourceIds: "",
    costWeightsBase: 1.0,
    costWeightsAltitude: 0.5,
    terrainWeight: [
      { key: "1", value: 1.0 },
      { key: "2", value: 1.5 },
      { key: "3", value: 3.0 },
    ],
    settlements: [{ name: "vila", x: 5, y: 5 }],

    initialPopulation: 100,
    culture: 1,
    villageX: 5,
    villageY: 5,
    cultureIds: "1",
    professionIds: "1, 2",
    locationTypeIds: "",
    maxLongevityYears: 90,
    lifeTableBrackets: [
      { minAgeYears: 0, maxAgeYears: 1, baseAnnualMortality: 0.08 },
      { minAgeYears: 2, maxAgeYears: 14, baseAnnualMortality: 0.01 },
      { minAgeYears: 15, maxAgeYears: 39, baseAnnualMortality: 0.004 },
      { minAgeYears: 40, maxAgeYears: 59, baseAnnualMortality: 0.01 },
      { minAgeYears: 60, maxAgeYears: 79, baseAnnualMortality: 0.04 },
      { minAgeYears: 80, maxAgeYears: 89, baseAnnualMortality: 0.15 },
    ],
    fertilityMinAge: 16,
    fertilityMaxAge: 45,
    annualConceptionChance: 0.25,
    gestationDays: 270,
    maxBytesPerNpcPerYear: 4000,

    hungerDecayPerHour: 2.0,
    thirstDecayPerHour: 3.0,
    sleepDecayPerHour: 1.5,
    socialDecayPerHour: 1.0,
    urgencyThreshold: 70,
    maxActionSelectionSteps: 10,
    hysteresisEnabled: true,
    continuityBonus: 5.0,
    homelessSleepEfficiency: 0.5,
    maxDurationHours: { Eat: 2, Sleep: 8, Work: 8, Socialize: 3, Travel: 4, Idle: 2, Buy: 2 },
    routineSlots: [
      { professionId: 1, stage: "Adult", hourStart: 6, hourEnd: 14, action: "Work" },
      { professionId: 2, stage: "Adult", hourStart: 7, hourEnd: 15, action: "Work" },
      { professionId: null, stage: "Adult", hourStart: 8, hourEnd: 16, action: "Work" },
      { professionId: null, stage: "Adult", hourStart: 18, hourEnd: 20, action: "Socialize" },
      { professionId: null, stage: "Adult", hourStart: 22, hourEnd: 23, action: "Sleep" },
      { professionId: null, stage: "Adult", hourStart: 0, hourEnd: 5, action: "Sleep" },
      { professionId: null, stage: "Child", hourStart: 20, hourEnd: 23, action: "Sleep" },
      { professionId: null, stage: "Child", hourStart: 0, hourEnd: 6, action: "Sleep" },
      { professionId: null, stage: "Elder", hourStart: 21, hourEnd: 23, action: "Sleep" },
      { professionId: null, stage: "Elder", hourStart: 0, hourEnd: 6, action: "Sleep" },
    ],
    defaultAction: "Idle",

    economyEnabled: true,
    foodResourceId: 1,
    waterResourceId: 2,
    priceSensitivity: 0.1,
    capacityByResourceLocation: [],
    spoilagePerDayByResource: [],
    wageByProfession: [],
    priceFloor: [],
    priceCeiling: [],
    demandBaselinePerNpc: [],
    recipes: [],
    marketLocationTypeIds: "",
    locationTypeByProfession: [],
    workplaces: [{ locationTypeId: 1, x: 1, y: 1, maxVacancies: 3, treasury: 0, stock: "", prices: "" }],

    citiesEnabled: true,
    foodShortageThreshold: 0.1,
    housingShortageThreshold: 0.1,
    securityShortageThreshold: 0.1,
    emigrationRatePerDeficitUnit: 0.1,
    migrationEmploymentWeight: 0.1,
    migrationFoodWeight: 0.1,
    migrationSecurityWeight: 0.1,
    migrationFamilyTiesWeight: 0.1,
    foundingConcentrationThreshold: 0.1,
    foundingResourceThreshold: 0.1,
    foundingRouteThreshold: 0.1,
    foundingDefensibilityThreshold: 0.1,
    foundingLeadershipThreshold: 0.1,
    organizationTicks: 1,
    materializationIdleTicksBeforeEligible: 1,
    buildingRecipes: [],
    cities: [{ x: 2, y: 2, foundedAtTick: 0, count: 0, wealthSum: 0, healthSum: 0 }],

    professionBiases: [],
    skillBiases: [],
    transformationRules: [],
  };
}

function parseCsvInts(csv: string): number[] {
  return csv
    .split(",")
    .map((s) => s.trim())
    .filter((s) => s.length > 0)
    .map(Number);
}

function rowsToDict(rows: KeyNumberRow[]): Record<string, number> {
  return Object.fromEntries(rows.map((r) => [r.key, r.value]));
}

// "resourceId:qty,resourceId:qty" -> { "resourceId": qty, ... }
function parseCompactDict(text: string): Record<string, number> {
  const result: Record<string, number> = {};
  for (const part of text.split(",")) {
    const trimmed = part.trim();
    if (!trimmed) continue;
    const [k, v] = trimmed.split(":").map((s) => s.trim());
    if (k && v !== undefined) result[k] = Number(v);
  }
  return result;
}

/// Monta o body de `POST /worlds/create` no mesmo shape PascalCase que
/// `PeriodDefinitionValidator`/`ScenarioLoaderV2.LoadWorld` espera (ver
/// tests/LivingWorld.Tests/Periods/ScenarioLoaderV2Tests.cs `FullValidRoot()`).
export function scenarioFormToJson(form: ScenarioFormState): string {
  const root = {
    Width: form.width,
    Height: form.height,
    Seed: form.seed,
    RegionSize: form.regionSize,
    TerrainIds: parseCsvInts(form.terrainIds),
    BiomeIds: parseCsvInts(form.biomeIds),
    ResourceIds: parseCsvInts(form.resourceIds),
    CostWeights: {
      Base: form.costWeightsBase,
      AltitudeWeight: form.costWeightsAltitude,
      TerrainWeight: rowsToDict(form.terrainWeight),
    },
    Settlements: form.settlements.map((s) => ({ Name: s.name, X: s.x, Y: s.y })),

    InitialPopulation: form.initialPopulation,
    Culture: form.culture,
    VillageX: form.villageX,
    VillageY: form.villageY,
    CultureIds: parseCsvInts(form.cultureIds),
    ProfessionIds: parseCsvInts(form.professionIds),
    LocationTypeIds: parseCsvInts(form.locationTypeIds),
    MaxLongevityYears: form.maxLongevityYears,
    LifeTableBrackets: form.lifeTableBrackets.map((b) => ({
      MinAgeYears: b.minAgeYears,
      MaxAgeYears: b.maxAgeYears,
      BaseAnnualMortality: b.baseAnnualMortality,
    })),
    FertilityMinAge: form.fertilityMinAge,
    FertilityMaxAge: form.fertilityMaxAge,
    AnnualConceptionChance: form.annualConceptionChance,
    GestationDays: form.gestationDays,
    MaxBytesPerNpcPerYear: form.maxBytesPerNpcPerYear,

    HungerDecayPerHour: form.hungerDecayPerHour,
    ThirstDecayPerHour: form.thirstDecayPerHour,
    SleepDecayPerHour: form.sleepDecayPerHour,
    SocialDecayPerHour: form.socialDecayPerHour,
    UrgencyThreshold: form.urgencyThreshold,
    MaxActionSelectionSteps: form.maxActionSelectionSteps,
    HysteresisEnabled: form.hysteresisEnabled,
    ContinuityBonus: form.continuityBonus,
    HomelessSleepEfficiency: form.homelessSleepEfficiency,
    MaxDurationHours: form.maxDurationHours,
    RoutineSlots: form.routineSlots.map((r) => ({
      ProfessionId: r.professionId,
      Stage: r.stage,
      HourStart: r.hourStart,
      HourEnd: r.hourEnd,
      Action: r.action,
    })),
    DefaultAction: form.defaultAction,

    EconomyEnabled: form.economyEnabled,
    FoodResourceId: form.foodResourceId,
    WaterResourceId: form.waterResourceId,
    PriceSensitivity: form.priceSensitivity,
    CapacityByResourceLocation: rowsToDict(form.capacityByResourceLocation),
    SpoilagePerDayByResource: rowsToDict(form.spoilagePerDayByResource),
    WageByProfession: rowsToDict(form.wageByProfession),
    PriceFloor: rowsToDict(form.priceFloor),
    PriceCeiling: rowsToDict(form.priceCeiling),
    DemandBaselinePerNpc: rowsToDict(form.demandBaselinePerNpc),
    Recipes: Object.fromEntries(
      form.recipes.map((r) => [
        String(r.locationTypeId),
        {
          Inputs: parseCompactDict(r.inputs),
          Outputs: parseCompactDict(r.outputs),
          MaxWorkersPerCycle: r.maxWorkersPerCycle,
          RequiresCellResource: r.requiresCellResource,
        },
      ]),
    ),
    MarketLocationTypeIds: parseCsvInts(form.marketLocationTypeIds),
    LocationTypeByProfession: rowsToDict(form.locationTypeByProfession),
    Workplaces: form.workplaces.map((w) => ({
      LocationTypeId: w.locationTypeId,
      X: w.x,
      Y: w.y,
      MaxVacancies: w.maxVacancies,
      Treasury: w.treasury,
      Stock: parseCompactDict(w.stock),
      Prices: parseCompactDict(w.prices),
    })),

    CitiesEnabled: form.citiesEnabled,
    FoodShortageThreshold: form.foodShortageThreshold,
    HousingShortageThreshold: form.housingShortageThreshold,
    SecurityShortageThreshold: form.securityShortageThreshold,
    EmigrationRatePerDeficitUnit: form.emigrationRatePerDeficitUnit,
    MigrationEmploymentWeight: form.migrationEmploymentWeight,
    MigrationFoodWeight: form.migrationFoodWeight,
    MigrationSecurityWeight: form.migrationSecurityWeight,
    MigrationFamilyTiesWeight: form.migrationFamilyTiesWeight,
    FoundingConcentrationThreshold: form.foundingConcentrationThreshold,
    FoundingResourceThreshold: form.foundingResourceThreshold,
    FoundingRouteThreshold: form.foundingRouteThreshold,
    FoundingDefensibilityThreshold: form.foundingDefensibilityThreshold,
    FoundingLeadershipThreshold: form.foundingLeadershipThreshold,
    OrganizationTicks: form.organizationTicks,
    MaterializationIdleTicksBeforeEligible: form.materializationIdleTicksBeforeEligible,
    BuildingRecipes: Object.fromEntries(
      form.buildingRecipes.map((b) => [
        String(b.buildingTypeId),
        {
          Inputs: parseCompactDict(b.inputs),
          TicksToBuild: b.ticksToBuild,
          HousingCapacityProvided: b.housingCapacityProvided,
        },
      ]),
    ),
    Cities: form.cities.map((c) => ({
      X: c.x,
      Y: c.y,
      FoundedAtTick: c.foundedAtTick,
      AggregatePool: { Count: c.count, WealthSum: c.wealthSum, HealthSum: c.healthSum },
    })),

    Dynamics: {
      ProfessionBiases: form.professionBiases.map((p) => ({
        ProfessionId: p.professionId,
        Weight: p.weight,
        Name: p.name || undefined,
      })),
      SkillBiases: form.skillBiases.map((s) => ({
        SkillId: s.skillId,
        Weight: s.weight,
        Name: s.name || undefined,
      })),
      TransformationRules: form.transformationRules.map((t) => ({
        Kind: t.kind,
        SourceProfessionIds: parseCsvInts(t.sourceProfessionIds),
        TargetProfessionIds: parseCsvInts(t.targetProfessionIds),
        TriggerTick: t.triggerTick,
      })),
    },
  };

  return JSON.stringify(root);
}
