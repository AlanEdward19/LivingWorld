export interface WorldFixture {
  world: { name: string; summary: string };
  regions: RegionFixture[];
  settlements: SettlementFixture[];
  households: HouseholdFixture[];
  agents: AgentFixture[];
  events: WorldEventFixture[]; // ordem cronológica
  storyThreads: StoryThreadFixture[];
  organizations: OrganizationFixture[];
}

/** Hierarquia de lugares (doc §42: "Regions > Westreach > Oakbridge"). */
export interface RegionFixture {
  id: string;
  name: string;
}

export type BuildingKind = "residence" | "agriculture" | "forge" | "generic";

export interface SettlementFixture {
  id: string;
  name: string;
  regionId: string;
  gridPosition: { x: number; y: number }; // posição no mapa "mundo"
  population: number;
  populationTrend: "up" | "down" | "stable";
  food: "abundant" | "stable" | "scarce";
  employment: "stable" | "declining";
  migration: "arriving" | "stable" | "leaving";
  construction: number; // projetos ativos
  buildings: BuildingFixture[]; // pra zoom "distrito"
}

export interface BuildingFixture {
  id: string;
  kind: BuildingKind;
  gridPosition: { x: number; y: number }; // relativo ao settlement, zoom "distrito"
  height: number; // nº de "andares" isométricos
}

export interface HouseholdFixture {
  id: string;
  name: string; // "Valen Household"
  settlementId: string;
  memberIds: string[]; // NpcId
  headId: string;
  stock: Record<string, number>; // recurso → quantidade
}

/** Detalhe físico expandido (doc §52) — "o que isso afeta" por traço, não só o número. */
export interface BodyDetailFixture {
  height: string;
  weight: string;
  muscleMass: string;
  fatMass: string;
  physicalStrength: string;
  endurance: string;
  mobility: string;
  currentInjuries: string[];
  diseases: string[];
  conditions: string[];
  affects: { trait: string; effects: string[] }[];
}

export interface AgentFixture {
  id: string;
  name: string;
  age: number;
  profession: string;
  settlementId: string;
  householdId: string | null;
  gridPosition: { x: number; y: number }; // zoom "agente"
  currentIntent: string; // "Looking for affordable grain"
  condition: string[]; // ["Healthy", "Tired", "Hungry"]
  bodySummary: { build: string }; // "Average height · Strong"
  bodyDetail: BodyDetailFixture;
  /** People "Notable" filter (doc §43) — protagonistas/vozes independentes da crise, não
   * dependentes (crianças) que só aparecem através de outro agent. */
  notable: boolean;
  relationships: { withAgentId: string; label: string }[]; // "Rowan · trusted"
  recentLifeEvents: string[];
  lifeMilestones: { label: string; approxDate: string }[]; // pra Life View
  whyFactors: { text: string; linkedEventId?: string }[]; // painel Why?
}

/** Doc §173: routine (sem acento) → notable (acento pequeno) → major (borda de acento) →
 * critical/world-changing (toast maior + marcador de timeline). */
export type EventSeverity = "routine" | "notable" | "major" | "critical";

export interface WorldEventFixture {
  eventId: string;
  tick: string; // rótulo temporal legível ("Year 312 · Spring · 09")
  kind: string; // "GrainPriceIncreased", "PurchaseFailed", ...
  summary: string; // texto humano
  severity: EventSeverity;
  causeEventId: string | null; // proveniência causal
  sourceSystem: string; // "Agriculture" | "Economy" | "Household" | "Needs" | "Decision" | "Employment"
  affectedAgentIds: string[];
  affectedHouseholdIds: string[];
  settlementId: string;
}

export interface StoryThreadFixture {
  id: string;
  title: string; // "The Oakbridge Food Crisis"
  eventIds: string[];
  householdIds: string[];
  agentIds: string[];
  systemsTouched: string[];
}

/** Explorer "Organizations" (doc §44) — facções/companhias/guildas/governos/religiões/militares. */
export interface OrganizationFixture {
  id: string;
  name: string;
  kind: "guild" | "company" | "faction" | "government" | "religious" | "military";
  memberIds: string[]; // AgentId
  description: string;
}
