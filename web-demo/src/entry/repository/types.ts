export type WorldStatus = "active" | "paused";

export type WorldSummary = {
  id: string;
  name: string;
  year: number;
  season: "Spring" | "Summer" | "Autumn" | "Winter";
  population: number;
  status: WorldStatus;
  lastOpenedAt: number;
};

/**
 * Real periods the engine ships scenario templates for (`scenarios/periods/*.json` in the
 * backend repo) — not an invented enum. `POST /worlds/start` instantiates one of these by id.
 */
export type WorldPeriod = "Medieval" | "Modern" | "Futuristic" | "Prehistoric" | "Creatures";

/**
 * Mirrors what the real backend actually takes to create a world (`CreateWorldRequest` /
 * `MapScenarioLoader` / `PopulationScenarioLoader` / `ExtraordinaryScenarioLoader` in
 * `src/LivingWorld.*`) — no invented knobs (no "ocean coverage", "terrain style", "climate
 * zone", "mineral abundance"; the real engine has none of those at world-creation time).
 * `size` is a frontend-only convenience preset that fills in Width/Height/RegionSize — those
 * three are the real fields, `size` never leaves the client.
 */
export type WorldConfig = {
  name: string;
  /** Real backend seed is a `ulong` (0..18446744073709551615) — kept as a numeric-only string
      here since JS numbers lose precision above 2^53. */
  seed: string;
  period: WorldPeriod;

  size: "Small" | "Medium" | "Large" | "Huge";
  width: number;
  height: number;
  regionSize: number;

  /** Real, required field: `CostWeights.Base` — flat travel-cost base per distance unit
      (`GeographyCatalog.cs`'s `CostWeights` record). Every shipped period defaults it to 1.0. */
  costBase: number;
  /** Real, required field: `CostWeights.AltitudeWeight` — extra travel cost per unit of altitude
      climbed. Every shipped period defaults it to 0.5. */
  costAltitudeWeight: number;
  /** Real, optional field: `CostWeights.TerrainWeight[id]` — per-terrain-id travel cost
      multiplier (defaults to 1.0 for any id not listed). Keyed on the real terrain ids every
      shipped period's catalog uses (1/2/3, see `TileMapPreview.tsx`'s CATALOG comment) — there's
      no name for what each id "is" anywhere in the engine, just the id and its weight. */
  terrainWeight1: number;
  terrainWeight2: number;
  terrainWeight3: number;

  initialPopulation: number;

  extraordinaryEnabled: boolean;
  /** Real field is `Extraordinary.Prevalence`, a 0..1 float — stored here as a 0..100 percent
      for the UI and converted at the edge. */
  extraordinaryPrevalence: number;
};

export type WorldDraft = {
  id: string;
  mode: "simple" | "advanced";
  world: WorldConfig;
  lockedFields: string[];
  createdAt: string;
  updatedAt: string;
};

/** Mirrors the real backend pipeline (`PeriodDefinitionValidator.Validate` ->
    `ScenarioLoaderV2.LoadWorld`, `src/LivingWorld.Simulation/Periods/`) — not invented flavor
    text like "carving rivers"; the real engine has no such stage. */
export type GenerationStage =
  | "validating"
  | "loading-map"
  | "seeding-population"
  | "configuring-behavior-economy"
  | "founding-cities"
  | "wiring-portals"
  | "seeding-extraordinary"
  | "complete";

export type GenerationEvent = {
  stage: GenerationStage;
  progress: number;
  message: string;
  timestamp: number;
};

export type GenerationResult = {
  worldId: string;
  worldName: string;
};
