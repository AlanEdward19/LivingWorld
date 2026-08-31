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
  name: string;
  kind: BuildingKind;
  gridPosition: { x: number; y: number }; // relativo ao settlement, zoom "distrito"
  height: number; // nº de "andares" isométricos (exterior)
  /** Interior explorável (doc §29-36) — [] pra prédios sem interior modelado nesta demo (ex.:
   * a fazenda, um marcador puramente exterior). Presença de andares é o que torna o prédio
   * clicável/"enterable" no mapa de settlement. */
  floors: FloorFixture[];
}

export type FurnitureKind = "bed" | "table" | "chair" | "stove" | "oven" | "counter" | "shelf" | "workbench" | "desk";

/** Objeto físico dentro de um cômodo (doc §36) — posição local ao grid do FLOOR (não da room). */
export interface FurnitureFixture {
  id: string;
  kind: FurnitureKind;
  gridPosition: { x: number; y: number };
}

/** Cômodo (doc §35) — `bounds` é a área retangular ocupada dentro do grid do floor (RimWorld-
 * style top-down: paredes = borda do retângulo, piso = área preenchida). */
export interface RoomFixture {
  id: string;
  name: string; // "Kitchen"
  bounds: { x: number; y: number; width: number; height: number };
  furniture: FurnitureFixture[];
}

export interface FloorFixture {
  id: string;
  label: string; // "Ground Floor", "Floor 1"
  rooms: RoomFixture[];
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

/** Categoria pra ícone/agrupamento na aba social estilo Sims (doc redesign, pedido do usuário
 * 2026-08-26: "estilo aba social do the sims"). `familyRole` só existe pra `kind: "family"` —
 * é o que a árvore genealógica (`FamilyTree.tsx`) percorre pra montar gerações; sempre do ponto
 * de vista do DONO da relação em relação a `withAgentId` (ex.: Eli lista Mira como
 * `familyRole: "parent"` porque Mira É a mãe dele). */
export type RelationshipKind = "family" | "romantic" | "friend" | "professional" | "rival";
export type FamilyRole = "spouse" | "parent" | "child" | "sibling";
export type RelationshipStrength = "strong" | "warm" | "neutral" | "tense";

export interface RelationshipFixture {
  withAgentId: string;
  label: string; // "trusted", "mother", "disliked employer" — texto exibido
  kind: RelationshipKind;
  familyRole?: FamilyRole;
  strength: RelationshipStrength;
}

export interface SkillFixture {
  name: string;
  level: number;
}

/** Necessidades básicas do agente (0-100, quanto maior mais satisfeita). */
export interface NeedsFixture {
  health: number;
  hunger: number;
  thirst: number;
  sleep: number;
  social: number;
}

export interface AgentFixture {
  id: string;
  name: string;
  age: number;
  profession: string;
  skills: SkillFixture[];
  needs: NeedsFixture;
  settlementId: string;
  householdId: string | null;
  /** Pontos de patrulha (mesmo espaço de grid dos prédios do settlement) — o NPC se move entre
   * eles em loop, decorativo/scripted (AD-018: sem simulação real rodando nesta demo pra
   * derivar posição de verdade ao longo do tempo). Nunca vazio — mínimo 2 pontos formam o loop;
   * 1 ponto só = fica parado nesse ponto (ex.: crianças brincando perto de casa). */
  patrolPoints: { x: number; y: number }[];
  currentIntent: string; // "Looking for affordable grain"
  condition: string[]; // ["Healthy", "Tired", "Hungry"]
  bodySummary: { build: string }; // "Average height · Strong"
  bodyDetail: BodyDetailFixture;
  /** People "Notable" filter (doc §43) — protagonistas/vozes independentes da crise, não
   * dependentes (crianças) que só aparecem através de outro agent. */
  notable: boolean;
  /** Onde o NPC está agora dentro de um prédio (doc §39 location model), se estiver indoors —
   * um fato do fixture, decorativo/estático como o resto do movimento nesta demo (AD-018), não
   * sincronizado tick-a-tick com `patrolPoints` (que descreve a posição EXTERIOR/de settlement,
   * sempre válida pro zoom mundo/settlement mesmo quando o NPC também "está" num cômodo). */
  indoorLocation?: { buildingId: string; floorId: string; roomId: string; position: { x: number; y: number } };
  relationships: RelationshipFixture[];
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
