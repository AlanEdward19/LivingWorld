// Feature ad-hoc "criar mundo" (AD-001 em .specs/STATE.md): estado inicial do formulário
// espelha scenarios/default.json (map/population/behavior) + os blocos economy/city/dynamics
// usados em ScenarioLoaderV2Tests.FullValidRoot() — mesmos defaults que já passam no backend,
// só editáveis campo a campo em vez de vir só de um arquivo fixo.
import { citySide } from "./map-engine/citySizing";

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
  // 0 = sem override, população inicial (T44b) segue o split automático de sempre entre as
  // cidades autoradas. Valor > 0 fixa quantos NPCs essa cidade nasce com (o footprint dela ainda
  // é derivado dessa população pela mesma fórmula de sempre — CityBoundsResolver não muda).
  initialPopulation: number;
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

export interface ExtraordinaryDescriptorRow {
  id: string;
  source: string;
  effects: string; // csv de "alvo:magnitude"
  mode: string;
  costs: string;
  reliability: string;
  failureModes: string;
  intrinsicVulnerabilities: string;
  manifestations: string;
  acquisitionRules: string;
  appearanceScaleMultiplier: number;
  appearanceSkinTint: string;
  appearanceMovementTrail: string;
  needSubstitutionReplacesNeed: string;
  needSubstitutionResourceId: number | null;
  needSubstitutionUnitsPerUse: number;
  senescenceRateMultiplier: number;
  manifestationCondition: string;
}

export interface PaintedCell {
  terrain: number;
  biome: number;
  altitude: number;
  water: boolean;
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
  // T14 (fase 15, UX pass 2): células pintadas no editor de grid, chave "x,y". Vazio == mapa
  // 100% procedural a partir de Seed (comportamento anterior, sem quebrar quem não usa o editor).
  cells: Record<string, PaintedCell>;

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

  // Extraordinário é opcional e composicional; nenhum campo representa arquétipo nominal.
  extraordinaryEnabled: boolean;
  extraordinaryDescriptors: ExtraordinaryDescriptorRow[];
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
    cells: {},

    initialPopulation: 20,
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

    // O cenário em branco não possui cadeia produtiva/estoque inicial suficiente para sustentar
    // a população. Economia ligada aqui fazia todos morrerem de fome enquanto o usuário apenas
    // observava o mapa; presets econômicos continuam podendo habilitá-la explicitamente.
    economyEnabled: false,
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
    workplaces: [],

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
    // Bugfix real (usuário, 2026-08-13): uma cidade fantasma aqui — em (2,2), sem nenhuma
    // relação com `settlements`/`villageX/Y` (5,5) acima, nunca movida pelo editor visual —
    // sempre resultava numa segunda cidade real fundada pelo backend em (5,5) pra abrigar a
    // população de verdade (`ScenarioLoaderV2.LoadWorld`), confundindo quem só clicou "1
    // assentamento" no mapa e via 2 cidades ao criar o mundo. Igual aos 3 templates reais do
    // `DefaultPeriodSeeder` (`Cities: []`) — sem cidade pré-autorada, o backend funda exatamente
    // uma, no lugar onde a população nasce de verdade.
    cities: [],

    professionBiases: [],
    skillBiases: [],
    transformationRules: [],
    extraordinaryEnabled: false,
    extraordinaryDescriptors: [],
  };
}

export function parseCsvInts(csv: string): number[] {
  return csv
    .split(",")
    .map((s) => s.trim())
    .filter((s) => s.length > 0)
    .map(Number);
}

function parseCsvText(csv: string): string[] {
  return csv.split(",").map((value) => value.trim()).filter(Boolean);
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
// T14 (fase 15, UX pass 2): se o usuário pintou pelo menos uma célula no editor de grid,
// `WorldMap.Create` exige o array `Cells` EXAUSTIVO sobre Width*Height (não dá pra mandar só as
// células pintadas) — as não pintadas viram o primeiro TerrainId/BiomeId declarado, altitude 0,
// sem água. Sem nenhuma célula pintada, `Cells` fica de fora e o mapa continua 100% procedural
// (mesmo comportamento de antes do editor existir).
export function buildCells(form: ScenarioFormState): object[] | undefined {
  if (Object.keys(form.cells).length === 0) return undefined;

  const defaultTerrain = parseCsvInts(form.terrainIds)[0] ?? 1;
  const defaultBiome = parseCsvInts(form.biomeIds)[0] ?? 0;
  const cells: object[] = [];
  for (let y = 0; y < form.height; y++) {
    for (let x = 0; x < form.width; x++) {
      const painted = form.cells[`${x},${y}`];
      cells.push({
        X: x,
        Y: y,
        Terrain: painted?.terrain ?? defaultTerrain,
        Altitude: painted?.altitude ?? 0,
        Biome: painted?.biome ?? defaultBiome,
        Water: painted?.water ?? false,
        Resources: [],
      });
    }
  }
  return cells;
}

/** Formato mínimo de <c>CreatorCityDraft</c> (definido em CreatorCityEditor.tsx) — duck-typed
 * aqui pra não criar import circular entre o módulo de dados e o de componentes do criador. */
export interface CreatorCityDraftLike {
  buildings: { x: number; y: number; rotation?: number }[];
}

/** Converte os rascunhos de cidade (Fase 15.1, T44) de coordenada local do canvas da cidade pra
 * coordenada absoluta do mundo, ancorada na posição real do assentamento — mesmo esquema de
 * <c>CityScenarioLoader.ParseBuildings</c> (X/Y absolutos, valida contra mapWidth/mapHeight). O
 * canvas local usa <c>citySide</c> (mesma fórmula de <c>CityBoundsResolver</c>, LIVE-POLISH) —
 * antes era um tamanho fixo (24x18) sempre maior que o footprint real. Descarta (não falha)
 * construção que caia fora do mapa ou colida com outra já aceita — <c>ParseBuildings</c> rejeita
 * a criação do mundo INTEIRA nesses dois casos; melhor perder a construção sobressalente do que
 * quebrar a criação do mundo todo. */
function authoredBuildingsFromDrafts(
  form: ScenarioFormState,
  cityDrafts: Record<number, CreatorCityDraftLike> | undefined,
): { CityIndex: number; BuildingTypeId: number; X: number; Y: number; Orientation: number }[] {
  if (!cityDrafts) return [];
  const occupied = new Set<string>();
  const result: { CityIndex: number; BuildingTypeId: number; X: number; Y: number; Orientation: number }[] = [];

  form.settlements.forEach((settlement, cityIndex) => {
    const draft = cityDrafts[cityIndex];
    if (!draft) return;

    const side = citySide(estimatedSettlementPopulation(form, cityIndex), form.width, form.height);
    for (const building of draft.buildings) {
      const x = Math.min(Math.max(Math.round(settlement.x + (building.x - side / 2)), 0), form.width - 1);
      const y = Math.min(Math.max(Math.round(settlement.y + (building.y - side / 2)), 0), form.height - 1);
      const key = `${x},${y}`;
      if (occupied.has(key)) continue;
      occupied.add(key);
      result.push({ CityIndex: cityIndex, BuildingTypeId: 1, X: x, Y: y, Orientation: building.rotation ?? 0 });
    }
  });

  return result;
}

/** Quantas cidades autoradas vão de fato existir no mundo criado (assentamentos + cidades extras
 * que não coincidem com um assentamento) — mesma contagem que `ScenarioLoaderV2` usa pra
 * distribuir `InitialPopulation` entre elas (LIVE-POLISH: nem toda cidade autorada nascia com
 * gente). Usado tanto pelo payload quanto pelo World Creator pra estimar o tamanho real de cada
 * assentamento antes de criar o mundo. */
export function authoredCityCount(form: ScenarioFormState): number {
  const extra = form.cities.filter(
    (city) => !form.settlements.some((settlement) => settlement.x === city.x && settlement.y === city.y),
  ).length;
  return form.settlements.length + extra;
}

/** Override de população explícito por cidade autorada (T44b), na mesma ordem que
 * `scenarioFormToJson` monta `Cities` (assentamentos primeiro, extras depois) — `null` quando o
 * usuário não fixou um valor (segue o split automático). */
function authoredPopulationOverrides(form: ScenarioFormState): (number | null)[] {
  const settlementOverrides = form.settlements.map((settlement) => {
    const configured = form.cities.find((city) => city.x === settlement.x && city.y === settlement.y);
    return configured?.initialPopulation ? configured.initialPopulation : null;
  });
  const extraOverrides = form.cities
    .filter((city) => !form.settlements.some((settlement) => settlement.x === city.x && settlement.y === city.y))
    .map((city) => (city.initialPopulation > 0 ? city.initialPopulation : null));
  return [...settlementOverrides, ...extraOverrides];
}

/** Estimativa da população que um assentamento (por índice em `form.settlements`) vai receber
 * ao criar o mundo — mesmo split de `ScenarioLoaderV2`: cidade com `initialPopulation` explícito
 * (T44b) usa esse valor, o resto do total é dividido igualmente entre as demais (resto da divisão
 * pra vila-sede). Só uma estimativa pro World Creator dimensionar o canvas de edição; a fonte de
 * verdade é sempre o backend. */
export function estimatedSettlementPopulation(form: ScenarioFormState, settlementIndex: number): number {
  const overrides = authoredPopulationOverrides(form);
  if (overrides[settlementIndex]) return overrides[settlementIndex]!;

  const remainingTargets = overrides.flatMap((value, index) => (value === null ? [index] : []));
  const explicitTotal = overrides.reduce<number>((sum, value) => sum + (value ?? 0), 0);
  const remainingPopulation = Math.max(0, form.initialPopulation - explicitTotal);
  const remainderIndex = remainingTargets.includes(0) ? 0 : (remainingTargets[0] ?? -1);
  const perCity = remainingTargets.length > 0 ? Math.floor(remainingPopulation / remainingTargets.length) : 0;
  const remainder = remainingTargets.length > 0 ? remainingPopulation % remainingTargets.length : 0;
  return perCity + (settlementIndex === remainderIndex ? remainder : 0);
}

export function scenarioFormToJson(
  form: ScenarioFormState,
  cityDrafts?: Record<number, CreatorCityDraftLike>,
): string {
  const cells = buildCells(form);
  const village = form.settlements[0] ?? { x: form.villageX, y: form.villageY };
  const authoredBuildings = authoredBuildingsFromDrafts(form, cityDrafts);
  const authoredCities = [
    ...form.settlements.map((settlement) => {
      const configured = form.cities.find((city) => city.x === settlement.x && city.y === settlement.y);
      return {
        X: settlement.x,
        Y: settlement.y,
        Name: settlement.name,
        FoundedAtTick: configured?.foundedAtTick ?? 0,
        AggregatePool: {
          Count: configured?.count ?? 0,
          WealthSum: configured?.wealthSum ?? 0,
          HealthSum: configured?.healthSum ?? 0,
        },
        ...(configured?.initialPopulation ? { InitialPopulation: configured.initialPopulation } : {}),
      };
    }),
    ...form.cities
      .filter((city) => !form.settlements.some((settlement) => settlement.x === city.x && settlement.y === city.y))
      .map((city) => ({
        X: city.x,
        Y: city.y,
        FoundedAtTick: city.foundedAtTick,
        AggregatePool: { Count: city.count, WealthSum: city.wealthSum, HealthSum: city.healthSum },
        ...(city.initialPopulation ? { InitialPopulation: city.initialPopulation } : {}),
      })),
  ];
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
    ...(cells ? { Cells: cells } : {}),

    InitialPopulation: form.initialPopulation,
    Culture: form.culture,
    VillageX: village.x,
    VillageY: village.y,
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
    Cities: authoredCities,
    Buildings: authoredBuildings,

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
    Extraordinary: {
      Enabled: form.extraordinaryEnabled,
      Descriptors: form.extraordinaryDescriptors.map((descriptor) => {
        const hasAppearance = descriptor.appearanceScaleMultiplier !== 1
          || descriptor.appearanceSkinTint.trim() !== ""
          || descriptor.appearanceMovementTrail.trim() !== "";
        const hasNeedSubstitution = descriptor.needSubstitutionReplacesNeed.trim() !== ""
          && descriptor.needSubstitutionResourceId !== null
          && descriptor.needSubstitutionUnitsPerUse > 0;
        return {
          Id: descriptor.id,
          Source: descriptor.source,
          Effects: parseCsvText(descriptor.effects),
          Mode: descriptor.mode,
          Costs: parseCsvText(descriptor.costs),
          Reliability: descriptor.reliability,
          FailureModes: parseCsvText(descriptor.failureModes),
          IntrinsicVulnerabilities: parseCsvText(descriptor.intrinsicVulnerabilities),
          Manifestations: parseCsvText(descriptor.manifestations),
          AcquisitionRules: parseCsvText(descriptor.acquisitionRules),
          Appearance: hasAppearance ? {
            ScaleMultiplier: descriptor.appearanceScaleMultiplier,
            SkinTint: descriptor.appearanceSkinTint,
            MovementTrail: descriptor.appearanceMovementTrail,
          } : undefined,
          NeedSubstitution: hasNeedSubstitution ? {
            ReplacesNeed: descriptor.needSubstitutionReplacesNeed,
            ResourceId: descriptor.needSubstitutionResourceId,
            UnitsPerUse: descriptor.needSubstitutionUnitsPerUse,
          } : undefined,
          SenescenceRateMultiplier: descriptor.senescenceRateMultiplier,
          ManifestationCondition: descriptor.manifestationCondition.trim() || undefined,
        };
      }),
    },
  };

  return JSON.stringify(root);
}

function dictToRows(dict: Record<string, number> | undefined): KeyNumberRow[] {
  return Object.entries(dict ?? {}).map(([key, value]) => ({ key, value }));
}

function compactDictText(dict: Record<string, number> | undefined): string {
  return Object.entries(dict ?? {})
    .map(([k, v]) => `${k}:${v}`)
    .join(",");
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type Raw = Record<string, any>;

/// UX pass 3 (feedback do usuário: "permitir usar algum dos templates que temos"): inverso de
/// `scenarioFormToJson` — carrega um `PeriodDefinition` (de `GET /periods/{id}`, mesmo shape que
/// o backend valida) de volta no estado do formulário, pra editar em cima de um template em vez
/// de sempre começar do zero. `Cells` autorado no template (se houver) não é reconstruído pro
/// editor de pintura — o mapa carrega como procedural a partir da Seed, e o usuário pode pintar
/// por cima; templates seedados (DefaultPeriodSeeder.cs) nunca autoram Cells mesmo.
export function jsonToScenarioForm(json: Raw): ScenarioFormState {
  const base = defaultScenarioForm();
  const dynamics = json.Dynamics ?? {};
  const extraordinary = json.Extraordinary ?? {};

  return {
    ...base,
    width: json.Width ?? base.width,
    height: json.Height ?? base.height,
    seed: json.Seed ?? base.seed,
    regionSize: json.RegionSize ?? base.regionSize,
    terrainIds: (json.TerrainIds ?? []).join(", "),
    biomeIds: (json.BiomeIds ?? []).join(", "),
    resourceIds: (json.ResourceIds ?? []).join(", "),
    costWeightsBase: json.CostWeights?.Base ?? base.costWeightsBase,
    costWeightsAltitude: json.CostWeights?.AltitudeWeight ?? base.costWeightsAltitude,
    terrainWeight: dictToRows(json.CostWeights?.TerrainWeight),
    settlements: (json.Settlements ?? []).map((s: Raw) => ({ name: s.Name, x: s.X, y: s.Y })),
    cells: {},

    initialPopulation: json.InitialPopulation ?? base.initialPopulation,
    culture: json.Culture ?? base.culture,
    villageX: json.VillageX ?? base.villageX,
    villageY: json.VillageY ?? base.villageY,
    cultureIds: (json.CultureIds ?? []).join(", "),
    professionIds: (json.ProfessionIds ?? []).join(", "),
    locationTypeIds: (json.LocationTypeIds ?? []).join(", "),
    maxLongevityYears: json.MaxLongevityYears ?? base.maxLongevityYears,
    lifeTableBrackets: (json.LifeTableBrackets ?? []).map((b: Raw) => ({
      minAgeYears: b.MinAgeYears,
      maxAgeYears: b.MaxAgeYears,
      baseAnnualMortality: b.BaseAnnualMortality,
    })),
    fertilityMinAge: json.FertilityMinAge ?? base.fertilityMinAge,
    fertilityMaxAge: json.FertilityMaxAge ?? base.fertilityMaxAge,
    annualConceptionChance: json.AnnualConceptionChance ?? base.annualConceptionChance,
    gestationDays: json.GestationDays ?? base.gestationDays,
    maxBytesPerNpcPerYear: json.MaxBytesPerNpcPerYear ?? base.maxBytesPerNpcPerYear,

    hungerDecayPerHour: json.HungerDecayPerHour ?? base.hungerDecayPerHour,
    thirstDecayPerHour: json.ThirstDecayPerHour ?? base.thirstDecayPerHour,
    sleepDecayPerHour: json.SleepDecayPerHour ?? base.sleepDecayPerHour,
    socialDecayPerHour: json.SocialDecayPerHour ?? base.socialDecayPerHour,
    urgencyThreshold: json.UrgencyThreshold ?? base.urgencyThreshold,
    maxActionSelectionSteps: json.MaxActionSelectionSteps ?? base.maxActionSelectionSteps,
    hysteresisEnabled: json.HysteresisEnabled ?? base.hysteresisEnabled,
    continuityBonus: json.ContinuityBonus ?? base.continuityBonus,
    homelessSleepEfficiency: json.HomelessSleepEfficiency ?? base.homelessSleepEfficiency,
    maxDurationHours: json.MaxDurationHours ?? base.maxDurationHours,
    routineSlots: (json.RoutineSlots ?? []).map((r: Raw) => ({
      professionId: r.ProfessionId ?? null,
      stage: r.Stage,
      hourStart: r.HourStart,
      hourEnd: r.HourEnd,
      action: r.Action,
    })),
    defaultAction: json.DefaultAction ?? base.defaultAction,

    economyEnabled: json.EconomyEnabled ?? base.economyEnabled,
    foodResourceId: json.FoodResourceId ?? base.foodResourceId,
    waterResourceId: json.WaterResourceId ?? base.waterResourceId,
    priceSensitivity: json.PriceSensitivity ?? base.priceSensitivity,
    capacityByResourceLocation: dictToRows(json.CapacityByResourceLocation),
    spoilagePerDayByResource: dictToRows(json.SpoilagePerDayByResource),
    wageByProfession: dictToRows(json.WageByProfession),
    priceFloor: dictToRows(json.PriceFloor),
    priceCeiling: dictToRows(json.PriceCeiling),
    demandBaselinePerNpc: dictToRows(json.DemandBaselinePerNpc),
    recipes: Object.entries(json.Recipes ?? {}).map(([locationTypeId, r]) => ({
      locationTypeId: Number(locationTypeId),
      inputs: compactDictText((r as Raw).Inputs),
      outputs: compactDictText((r as Raw).Outputs),
      maxWorkersPerCycle: (r as Raw).MaxWorkersPerCycle,
      requiresCellResource: (r as Raw).RequiresCellResource ?? null,
    })),
    marketLocationTypeIds: (json.MarketLocationTypeIds ?? []).join(", "),
    locationTypeByProfession: dictToRows(json.LocationTypeByProfession),
    workplaces: (json.Workplaces ?? []).map((w: Raw) => ({
      locationTypeId: w.LocationTypeId,
      x: w.X,
      y: w.Y,
      maxVacancies: w.MaxVacancies,
      treasury: w.Treasury,
      stock: compactDictText(w.Stock),
      prices: compactDictText(w.Prices),
    })),

    citiesEnabled: json.CitiesEnabled ?? base.citiesEnabled,
    foodShortageThreshold: json.FoodShortageThreshold ?? base.foodShortageThreshold,
    housingShortageThreshold: json.HousingShortageThreshold ?? base.housingShortageThreshold,
    securityShortageThreshold: json.SecurityShortageThreshold ?? base.securityShortageThreshold,
    emigrationRatePerDeficitUnit: json.EmigrationRatePerDeficitUnit ?? base.emigrationRatePerDeficitUnit,
    migrationEmploymentWeight: json.MigrationEmploymentWeight ?? base.migrationEmploymentWeight,
    migrationFoodWeight: json.MigrationFoodWeight ?? base.migrationFoodWeight,
    migrationSecurityWeight: json.MigrationSecurityWeight ?? base.migrationSecurityWeight,
    migrationFamilyTiesWeight: json.MigrationFamilyTiesWeight ?? base.migrationFamilyTiesWeight,
    foundingConcentrationThreshold: json.FoundingConcentrationThreshold ?? base.foundingConcentrationThreshold,
    foundingResourceThreshold: json.FoundingResourceThreshold ?? base.foundingResourceThreshold,
    foundingRouteThreshold: json.FoundingRouteThreshold ?? base.foundingRouteThreshold,
    foundingDefensibilityThreshold: json.FoundingDefensibilityThreshold ?? base.foundingDefensibilityThreshold,
    foundingLeadershipThreshold: json.FoundingLeadershipThreshold ?? base.foundingLeadershipThreshold,
    organizationTicks: json.OrganizationTicks ?? base.organizationTicks,
    materializationIdleTicksBeforeEligible:
      json.MaterializationIdleTicksBeforeEligible ?? base.materializationIdleTicksBeforeEligible,
    buildingRecipes: Object.entries(json.BuildingRecipes ?? {}).map(([buildingTypeId, b]) => ({
      buildingTypeId: Number(buildingTypeId),
      inputs: compactDictText((b as Raw).Inputs),
      ticksToBuild: (b as Raw).TicksToBuild,
      housingCapacityProvided: (b as Raw).HousingCapacityProvided,
    })),
    cities: (json.Cities ?? []).map((c: Raw) => ({
      x: c.X,
      y: c.Y,
      foundedAtTick: c.FoundedAtTick,
      count: c.AggregatePool?.Count ?? 0,
      wealthSum: c.AggregatePool?.WealthSum ?? 0,
      healthSum: c.AggregatePool?.HealthSum ?? 0,
      initialPopulation: c.InitialPopulation ?? 0,
    })),

    professionBiases: (dynamics.ProfessionBiases ?? []).map((p: Raw) => ({
      professionId: p.ProfessionId,
      weight: p.Weight,
      name: p.Name ?? "",
    })),
    skillBiases: (dynamics.SkillBiases ?? []).map((s: Raw) => ({
      skillId: s.SkillId,
      weight: s.Weight,
      name: s.Name ?? "",
    })),
    transformationRules: (dynamics.TransformationRules ?? []).map((t: Raw) => ({
      kind: t.Kind,
      sourceProfessionIds: (t.SourceProfessionIds ?? []).join(", "),
      targetProfessionIds: (t.TargetProfessionIds ?? []).join(", "),
      triggerTick: t.TriggerTick ?? null,
    })),
    extraordinaryEnabled: extraordinary.Enabled ?? base.extraordinaryEnabled,
    extraordinaryDescriptors: (extraordinary.Descriptors ?? []).map((descriptor: Raw) => ({
      id: descriptor.Id ?? "",
      source: descriptor.Source ?? "",
      effects: (descriptor.Effects ?? []).join(", "),
      mode: descriptor.Mode ?? "",
      costs: (descriptor.Costs ?? []).join(", "),
      reliability: descriptor.Reliability ?? "",
      failureModes: (descriptor.FailureModes ?? []).join(", "),
      intrinsicVulnerabilities: (descriptor.IntrinsicVulnerabilities ?? []).join(", "),
      manifestations: (descriptor.Manifestations ?? []).join(", "),
      acquisitionRules: (descriptor.AcquisitionRules ?? []).join(", "),
      appearanceScaleMultiplier: descriptor.Appearance?.ScaleMultiplier ?? 1,
      appearanceSkinTint: descriptor.Appearance?.SkinTint ?? "",
      appearanceMovementTrail: descriptor.Appearance?.MovementTrail ?? "",
      needSubstitutionReplacesNeed: descriptor.NeedSubstitution?.ReplacesNeed ?? "",
      needSubstitutionResourceId: descriptor.NeedSubstitution?.ResourceId ?? null,
      needSubstitutionUnitsPerUse: descriptor.NeedSubstitution?.UnitsPerUse ?? 1,
      senescenceRateMultiplier: descriptor.SenescenceRateMultiplier ?? 1,
      manifestationCondition: descriptor.ManifestationCondition ?? "",
    })),
  };
}
