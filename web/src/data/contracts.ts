// Fase 15.1, T0: contratos ainda não implementados pelo motor (design.md "Data Models" /
// "Mock Adapter"). Nada aqui existe hoje em `src/LivingWorld.Api` — são os shapes que os
// tasks de Estágio 2 (T1-T4, T20, T21, T30) devem produzir byte-a-byte. Enquanto isso, é
// contra ESTES tipos que as fixtures do Estágio 1 são checadas por `tsc --noEmit`: uma
// fixture com um campo a mais/a menos/renomeado quebra o build, não silenciosamente.
import type {
  CellCoord,
  CitySnapshot,
  CityBuildingMarker,
  GlobalCityMarker,
  GlobalSnapshot,
} from "../types";

export interface NpcPositionDelta {
  npcId: number;
  location: CellCoord;
}

export interface ScopeTickDelta {
  tick: number;
  sequence?: number;
  fromSequence?: number;
  moved: NpcPositionDelta[];
  removed: number[];
  npcUpserts?: NpcVisual[];
  npcRemoved?: NumericId[];
  cityUpserts?: CityVisual[];
  cityRemoved?: StringId[];
  buildingUpserts?: BuildingVisual[];
  buildingRemoved?: NumericId[];
  processUpserts?: ProcessVisual[];
  processRemoved?: number[];
  indicators?: IndicatorUpdate[];
  events?: NotableVisualEvent[];
}

export interface NumericId { value: number }
export interface StringId { value: string }

export interface NpcVisual {
  id: NumericId;
  location: CellCoord;
  currentAction: number | null;
}

/** T50: valor de `NpcInspection.lod` para um id reservado num pool agregado (City.PoolNpcIds)
 * ainda não materializado — existe (não é "gone"), só não tem atributos reais até sortear. */
export const POOLED_LOD = 2;

export interface NpcInspection {
  id: NumericId;
  name: string;
  sex: number;
  ageYears: number;
  culture: { id: number };
  city: StringId;
  household: NumericId | null;
  motherId: NumericId | null;
  fatherId: NumericId | null;
  spouse: NumericId | null;
  profession: { id: number };
  employer: NumericId | null;
  health: number;
  hunger: number;
  thirst: number;
  sleep: number;
  social: number;
  personality: unknown;
  skills: { values: Record<string, number> };
  currentLocation: CellCoord;
  currentAction: number | null;
  actionStartedAtTick: number;
  actionTarget: { kind: string; id: string } | null;
  lod: number;
  beliefs: string[];
  memories: string[];
  /** T50 (bug "seguir NPC entre escopos"): 0 = World, 1 = City — mesmo critério geométrico que
   * já decide o que aparece no mapa-múndi vs. dentro da cidade (NpcScopeResolver, backend). */
  currentScope: { kind: number; cityId: StringId | null };
}

export interface CityVisual {
  id: StringId;
  location: CellCoord;
  population: number;
  bounds: CellBounds;
}

export interface BuildingVisual {
  id: NumericId;
  cityId: StringId;
  buildingTypeId: number;
  location: CellCoord;
}

export interface ProcessVisual {
  id: number;
  kind: string;
  targetId: number;
  progress: number;
  descriptorKey: string;
}

export interface IndicatorUpdate { key: string; value: number }
export interface NotableVisualEvent { tick: number; kind: number; label: string }

export interface LivingScopeStateWire {
  npcs: NpcVisual[];
  cities: CityVisual[];
  buildings: BuildingVisual[];
  processes: ProcessVisual[];
  indicators: IndicatorUpdate[];
  events: NotableVisualEvent[];
}

export interface SimulationStatus {
  isPaused: boolean;
  ticksPerSecond: number;
  tick?: number;
  year?: number;
}

export type PortalSpaceKind = "World" | "City" | "Building";

export interface PortalEndpointDto {
  space: PortalSpaceKind;
  refId: string;
  cell: CellCoord;
}

export interface SpatialPortalDto {
  id: string;
  label: string;
  from: PortalEndpointDto;
  to: PortalEndpointDto;
}

export interface CellBounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** Campos que T20 adiciona a `GlobalCityMarker` (OQ-1: projeção derivada, sem tocar o domínio). */
export interface CityFootprintFields {
  bounds: CellBounds;
  boundsAreDerived: boolean;
}

/** Campos que T20 adiciona a `CityBuildingMarker` (mesma decisão OQ-1). */
export interface BuildingPositionFields {
  location: CellCoord;
  locationIsDerived: boolean;
}

export type FutureGlobalCityMarker = GlobalCityMarker & CityFootprintFields;
export type FutureCityBuildingMarker = CityBuildingMarker & BuildingPositionFields;

export interface FutureGlobalSnapshot extends Omit<GlobalSnapshot, "cities"> {
  cities: FutureGlobalCityMarker[];
  // Opcional: as fixtures do Estágio 1 continuam servindo portais via `MockPortalSource`
  // separado (T0/T11), não embutidos no snapshot — só o payload real (T21) carrega este campo.
  /** `SpatialPortal` (T21) — portais que tocam o escopo World. */
  portals?: SpatialPortalDto[];
}

/**
 * Campo que T30 adiciona a `CitySnapshot` — os 6 indicadores que `CityPopulationQuery` já
 * calcula no motor (`src/LivingWorld.Simulation/Cities/CityPopulationQuery.cs:16-53`), hoje
 * inacessíveis ao cliente (só `aggregatePool.wealthSum/healthSum` chegam, sem população real
 * nem desigualdade/economia/habitação — spec.md story "Inspector de NPC e Cidade" AC1).
 */
export interface CityIndicators {
  population: number;
  wealth: number;
  health: number;
  inequality: number;
  economy: number;
  housing: number;
}

export interface FutureCitySnapshot extends Omit<CitySnapshot, "buildings"> {
  buildings: FutureCityBuildingMarker[];
  indicators: CityIndicators;
  /** `SpatialPortal` (T21) — portais que tocam esta cidade. */
  portals?: SpatialPortalDto[];
}

/** Fase 15.1, T7 (LWV-05): prosa narrada por `NarrativeRenderer` sobre um `NarrativeDraft`
 * (crônica de uma cidade ou biografia de um NPC) — mesmo shape em `ChronicleResponse` e
 * `BiographyResponse` no backend (`NarrativeEndpoints.cs`). */
export interface NarrativeProse {
  prose: string;
}

export type ConversationStartOutcome =
  | { accepted: true; sessionId: number }
  | { accepted: false; reason: string };

export interface ConversationTurn {
  dialogue: string;
  emotion: string;
  intent: string;
}

export type ConversationSendOutcome =
  | { ok: true; turn: ConversationTurn }
  | { ok: false; reason: "session-not-found" | "npc-dead" | "session-ended" };
