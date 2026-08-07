// Fase 15.1, T0: fixtures estáticas — tipadas contra os contratos reais (types.ts +
// data/contracts.ts), nunca contra um shape inventado. Cobrem os 3 escopos (world, city,
// interior), 2 cidades, >=20 NPCs, >=2 portais para o mesmo par de espaços, e ao menos uma
// camada NotYetModeled — os mínimos exigidos pelo "Done when" de T0.
import {
  VisualScopeKind,
  ViewerMode,
  type CellCoord,
  type CitySnapshot,
  type GlobalNpcMarker,
  type InteriorSnapshot,
  type LayerBuildResult,
  type VisualLayerName,
  type VisualSnapshotEnvelope,
} from "../../types";
import type {
  CityFootprintFields,
  CityIndicators,
  FutureCityBuildingMarker,
  FutureCitySnapshot,
  FutureGlobalCityMarker,
  FutureGlobalSnapshot,
  NpcPositionDelta,
  SpatialPortalDto,
} from "../contracts";
import { mockScopeKey } from "./mockScopeKey";

const ALL_LAYER_NAMES: VisualLayerName[] = [
  "Terrain",
  "Biome",
  "Rivers",
  "Mountains",
  "Resources",
  "Roads",
  "Borders",
  "Kingdoms",
  "Cities",
  "Villages",
  "Routes",
  "Migrations",
  "Conflicts",
  "Climate",
];

// Mesmo conjunto modelado na Fase 15 (GlobalLayerBuilder.cs) — as outras 11 ficam
// NotYetModeled, exatamente como o motor real declara hoje (context.md gap 1).
const MODELED_LAYER_NAMES: ReadonlySet<VisualLayerName> = new Set(["Terrain", "Biome", "Rivers"]);

function buildLayers(): Record<VisualLayerName, LayerBuildResult> {
  const layers = {} as Record<VisualLayerName, LayerBuildResult>;
  for (const name of ALL_LAYER_NAMES) {
    const isModeled = MODELED_LAYER_NAMES.has(name);
    layers[name] = { isModeled, payload: isModeled ? [] : null };
  }
  return layers;
}

function cell(x: number, y: number): CellCoord {
  return { x, y };
}

const CITY_A_BOUNDS: CityFootprintFields = { bounds: { x: 40, y: 20, width: 12, height: 9 }, boundsAreDerived: true };
const CITY_B_BOUNDS: CityFootprintFields = { bounds: { x: 80, y: 60, width: 8, height: 8 }, boundsAreDerived: true };

const cityAMarker: FutureGlobalCityMarker = {
  id: { value: "city-a" },
  location: cell(46, 24),
  population: 340,
  ...CITY_A_BOUNDS,
};

const cityBMarker: FutureGlobalCityMarker = {
  id: { value: "city-b" },
  location: cell(84, 64),
  population: 120,
  ...CITY_B_BOUNDS,
};

function externalNpcMarkers(count: number, startX: number, startY: number): GlobalNpcMarker[] {
  return Array.from({ length: count }, (_, i) => ({
    id: { value: 1000 + i },
    location: cell(startX + i, startY),
  }));
}

const worldPayload: FutureGlobalSnapshot = {
  width: 128,
  height: 96,
  cities: [cityAMarker, cityBMarker],
  externalNpcs: externalNpcMarkers(6, 10, 10),
  activeEvents: [],
  layers: buildLayers(),
};

export const worldSnapshotEnvelope: VisualSnapshotEnvelope<FutureGlobalSnapshot> = {
  scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
  mode: ViewerMode.Spectator,
  cursor: { tick: 0, scopeKey: "world", sequence: 0 },
  activeLayers: [],
  payload: worldPayload,
};

// BUG real corrigido (2026-08-07): todo prédio começava em 2000, então cidade B tinha os
// MESMOS ids que cidade A — `toScopeKey` (space.ts) mapeia prédio só por `interior:${buildingId}`
// (sem cityId, contrato do servidor real), então os dois prédios "2000" colidiam no mesmo
// escopo. `idOffset` dá a cada cidade sua própria faixa de ids, como um motor real faria.
function cityBuildingMarkers(cityBoundsX: number, cityBoundsY: number, count: number, idOffset: number): FutureCityBuildingMarker[] {
  return Array.from({ length: count }, (_, i) => ({
    id: { value: idOffset + i },
    buildingTypeId: i % 3,
    location: cell(cityBoundsX + i, cityBoundsY + 1),
    locationIsDerived: true,
  }));
}

function cityResidentMarkers(count: number, startX: number, startY: number) {
  return Array.from({ length: count }, (_, i) => ({
    id: { value: 3000 + i },
    location: cell(startX + i, startY),
    currentAction: null,
  }));
}

function citySnapshot(
  id: string,
  boundsX: number,
  boundsY: number,
  residentCount: number,
  buildingCount: number,
  buildingIdOffset: number,
  indicators: CityIndicators,
): FutureCitySnapshot {
  return {
    id: { value: id },
    location: cell(boundsX, boundsY),
    aggregatePool: { count: 0, wealthSum: 0, healthSum: 0 },
    residents: cityResidentMarkers(residentCount, boundsX + 1, boundsY + 1),
    buildings: cityBuildingMarkers(boundsX, boundsY, buildingCount, buildingIdOffset),
    layers: buildLayers(),
    indicators,
  };
}

const cityAPayload = citySnapshot("city-a", CITY_A_BOUNDS.bounds.x, CITY_A_BOUNDS.bounds.y, 10, 4, 2000, {
  population: 340,
  wealth: 62,
  health: 71,
  inequality: 0.34,
  economy: 58,
  housing: 44,
});
const cityBPayload = citySnapshot("city-b", CITY_B_BOUNDS.bounds.x, CITY_B_BOUNDS.bounds.y, 6, 2, 2100, {
  population: 120,
  wealth: 40,
  health: 66,
  inequality: 0.28,
  economy: 35,
  housing: 60,
});

export const cityASnapshotEnvelope: VisualSnapshotEnvelope<FutureCitySnapshot> = {
  scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
  mode: ViewerMode.Spectator,
  cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
  activeLayers: [],
  payload: cityAPayload,
};

export const cityBSnapshotEnvelope: VisualSnapshotEnvelope<FutureCitySnapshot> = {
  scope: { kind: VisualScopeKind.City, refId: "city-b", scopeKey: "city:city-b" },
  mode: ViewerMode.Spectator,
  cursor: { tick: 0, scopeKey: "city:city-b", sequence: 0 },
  activeLayers: [],
  payload: cityBPayload,
};

/**
 * BUG real corrigido (2026-08-07): só existia fixture de interior pro prédio "2000" — qualquer
 * outro prédio da cidade (ids 2001+, e todos os da cidade B) não tinha entrada em
 * `snapshotsByScope`, então `MockSnapshotSource.load` dava throw e o prédio nunca carregava
 * ("Carregando…" para sempre). Gera um interior por prédio de cada cidade, não só o primeiro.
 *
 * `scopeKey` aqui precisa bater com `toScopeKey` real (`map-engine/space.ts`), não com o índice
 * interno do mock (`mockScopeKey`) — são coisas diferentes. `toScopeKey` de `Building` é só
 * `interior:${buildingId}` (sem cityId, mesmo contrato do servidor), e `SimulationStore.applySnapshot`
 * descarta qualquer envelope cujo `scope.scopeKey` não bata com o escopo observado — usar um
 * formato diferente aqui fazia o snapshot chegar e ser descartado em silêncio (mesmo sintoma:
 * preso em "Carregando…", só que sem nem chegar a dar throw).
 */
function interiorEnvelopeFor(cityId: string, building: FutureCityBuildingMarker): VisualSnapshotEnvelope<InteriorSnapshot> {
  const refId = String(building.id.value);
  const scopeKey = `interior:${refId}`;
  return {
    scope: { kind: VisualScopeKind.Interior, refId, scopeKey },
    mode: ViewerMode.Spectator,
    cursor: { tick: 0, scopeKey, sequence: 0 },
    activeLayers: [],
    payload: {
      id: { value: building.id.value },
      city: { value: cityId },
      buildingTypeId: building.buildingTypeId,
      occupancyModeled: false,
    },
  };
}

/** Indexado pela mesma chave que `MockTickStreamSource`/`MockPortalSource` usam (`mockScopeKey`). */
export const snapshotsByScope: Record<string, VisualSnapshotEnvelope<unknown>> = {
  [mockScopeKey({ kind: "World" })]: worldSnapshotEnvelope,
  [mockScopeKey({ kind: "City", cityId: "city-a" })]: cityASnapshotEnvelope,
  [mockScopeKey({ kind: "City", cityId: "city-b" })]: cityBSnapshotEnvelope,
  ...Object.fromEntries(
    cityAPayload.buildings.map((b) => [
      mockScopeKey({ kind: "Building", buildingId: String(b.id.value), cityId: "city-a" }),
      interiorEnvelopeFor("city-a", b),
    ]),
  ),
  ...Object.fromEntries(
    cityBPayload.buildings.map((b) => [
      mockScopeKey({ kind: "Building", buildingId: String(b.id.value), cityId: "city-b" }),
      interiorEnvelopeFor("city-b", b),
    ]),
  ),
};

function npcPositions(startId: number, count: number, startX: number, startY: number): NpcPositionDelta[] {
  return Array.from({ length: count }, (_, i) => ({
    npcId: startId + i,
    location: cell(startX + i, startY),
  }));
}

/** Fonte de movimento sintético para `MockTickStreamSource` — não é dado autoritativo real. */
export const npcsByScope: Record<string, NpcPositionDelta[]> = {
  [mockScopeKey({ kind: "World" })]: npcPositions(1000, 6, 10, 10),
  [mockScopeKey({ kind: "City", cityId: "city-a" })]: npcPositions(3000, 10, CITY_A_BOUNDS.bounds.x + 1, CITY_A_BOUNDS.bounds.y + 1),
  [mockScopeKey({ kind: "City", cityId: "city-b" })]: npcPositions(3010, 6, CITY_B_BOUNDS.bounds.x + 1, CITY_B_BOUNDS.bounds.y + 1),
};

/**
 * Dois portais para o MESMO par de espaços (World <-> City "city-a") — necessário para o
 * teste de AC3/AC5 em T11 (múltiplas entradas, nenhum ramo de código por entrada).
 */
export const portalFixtures: SpatialPortalDto[] = [
  {
    id: "portal-city-a-north",
    label: "Portão Norte",
    from: { space: "World", refId: "", cell: cell(46, 18) },
    to: { space: "City", refId: "city-a", cell: cell(46, 21) },
  },
  {
    id: "portal-city-a-south",
    label: "Portão Sul",
    from: { space: "World", refId: "", cell: cell(46, 30) },
    to: { space: "City", refId: "city-a", cell: cell(46, 28) },
  },
  {
    id: "portal-city-b-main",
    label: "Estrada Principal",
    from: { space: "World", refId: "", cell: cell(84, 58) },
    to: { space: "City", refId: "city-b", cell: cell(84, 61) },
  },
];

export const TOTAL_MOCK_NPC_COUNT =
  worldPayload.externalNpcs.length + cityAPayload.residents.length + cityBPayload.residents.length;
