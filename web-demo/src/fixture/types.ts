export interface WorldFixture {
  world: { name: string; summary: string };
  settlements: SettlementFixture[];
  households: HouseholdFixture[];
  agents: AgentFixture[];
  events: WorldEventFixture[]; // ordem cronológica
  storyThreads: StoryThreadFixture[];
}

export type BuildingKind = "residence" | "agriculture" | "forge" | "generic";

export interface SettlementFixture {
  id: string;
  name: string;
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
  relationships: { withAgentId: string; label: string }[]; // "Rowan · trusted"
  recentLifeEvents: string[];
  lifeMilestones: { label: string; approxDate: string }[]; // pra Life View
  whyFactors: { text: string; linkedEventId?: string }[]; // painel Why?
}

export interface WorldEventFixture {
  eventId: string;
  tick: string; // rótulo temporal legível ("Year 312 · Spring · 09")
  kind: string; // "GrainPriceIncreased", "PurchaseFailed", ...
  summary: string; // texto humano
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
