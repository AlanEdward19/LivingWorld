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
  moved: NpcPositionDelta[];
  removed: number[];
}

export interface SimulationStatus {
  isPaused: boolean;
  ticksPerSecond: number;
  tick: number;
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
}

export interface FutureCitySnapshot extends Omit<CitySnapshot, "buildings"> {
  buildings: FutureCityBuildingMarker[];
}
