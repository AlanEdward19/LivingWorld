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

export type WorldConfig = {
  name: string;
  seed: string;
  size: "Small" | "Medium" | "Large" | "Huge";
  era: string;
  preset: string;
  historyLengthYears: number;
  initialPopulation: number;
  extraordinary: "None" | "Rare" | "Common" | "Abundant";

  // World / Geography (doc §32 "World" group)
  oceanCoverage: number;
  terrainStyle: "Varied" | "Mountainous" | "Flatlands" | "Archipelago" | "Canyons";

  // World / Climate
  climateZone: "Temperate" | "Arid" | "Tropical" | "Polar" | "Varied";
  seasonalIntensity: "Mild" | "Moderate" | "Extreme";
  rainfall: "Low" | "Moderate" | "High";

  // World / Resources
  mineralAbundance: "Scarce" | "Balanced" | "Abundant";
  fertility: "Poor" | "Moderate" | "Rich";
};

export type WorldDraft = {
  id: string;
  mode: "simple" | "advanced";
  world: WorldConfig;
  lockedFields: string[];
  createdAt: string;
  updatedAt: string;
};

export type GenerationStage =
  | "validating"
  | "forming-terrain"
  | "shaping-climate"
  | "carving-rivers"
  | "growing-ecosystems"
  | "founding-settlements"
  | "populating"
  | "simulating-history"
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
