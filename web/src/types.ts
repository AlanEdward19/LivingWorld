// Fase 15, T8: espelha os DTOs de src/LivingWorld.Api/Visual/*.cs à mão — geração real via
// OpenAPI (com gate anti-drift) é T9. Enums de propriedade (kind/mode) serializam como número
// (System.Text.Json Web defaults, sem JsonStringEnumConverter); chaves de Dictionary<enum,_>
// (Layers) serializam como o NOME do enum — as duas formas coexistem no mesmo payload.

export enum VisualScopeKind {
  World = 0,
  City = 1,
  Interior = 2,
}

export enum ViewerMode {
  Spectator = 0,
  Player = 1,
}

export type VisualLayerName =
  | "Terrain"
  | "Biome"
  | "Rivers"
  | "Mountains"
  | "Resources"
  | "Roads"
  | "Borders"
  | "Kingdoms"
  | "Cities"
  | "Villages"
  | "Routes"
  | "Migrations"
  | "Conflicts"
  | "Climate";

export interface VisualScope {
  kind: VisualScopeKind;
  refId: string;
  scopeKey: string;
}

export interface VisualCursor {
  tick: number;
  scopeKey: string;
  sequence: number;
}

export interface LayerBuildResult {
  isModeled: boolean;
  payload: unknown;
}

// Shapes reais de payload por camada (GlobalLayerBuilder.cs) — só as que carregam dado
// per-célula que o cliente efetivamente renderiza (T12); as demais (Mountains/Roads/Borders/
// Kingdoms/Climate/Cities/Villages/Routes/Migrations/Conflicts) ficam NotYetModeled hoje.
export interface TerrainCellEntry {
  key: CellCoord;
  value: { id: number };
}
export type TerrainLayerPayload = TerrainCellEntry[];
export type BiomeLayerPayload = TerrainCellEntry[];
export type RiversLayerPayload = CellCoord[];

export interface VisualSnapshotEnvelope<TPayload> {
  scope: VisualScope;
  mode: ViewerMode;
  cursor: VisualCursor;
  activeLayers: number[];
  payload: TPayload | null;
}

export interface CellCoord {
  x: number;
  y: number;
}

export interface GlobalCityMarker {
  id: { value: string };
  name: string;
  location: CellCoord;
  population: number;
}

export interface GlobalNpcMarker {
  id: { value: number };
  location: CellCoord;
}

export interface GlobalSnapshot {
  width: number;
  height: number;
  cities: GlobalCityMarker[];
  externalNpcs: GlobalNpcMarker[];
  activeEvents: unknown[];
  layers: Record<VisualLayerName, LayerBuildResult>;
}

export interface CityResidentMarker {
  id: { value: number };
  location: CellCoord;
  currentAction: number | null;
}

export interface CityBuildingMarker {
  id: { value: number };
  buildingTypeId: number;
}

export interface AggregatePopulationPool {
  count: number;
  wealthSum: number;
  healthSum: number;
}

export interface CitySnapshot {
  id: { value: string };
  name: string;
  location: CellCoord;
  aggregatePool: AggregatePopulationPool;
  residents: CityResidentMarker[];
  /** Ids reservados (T50, `City.PoolNpcIds`) de membros do pool agregado ainda não
   * materializados — cada um clicável/inspecionável (backend devolve `Lod.Pooled` com opção de
   * materializar), diferente de `aggregatePool.count`, que é só a contagem. */
  pendingResidentIds: number[];
  buildings: CityBuildingMarker[];
  layers: Record<VisualLayerName, LayerBuildResult>;
  /** Mesmo footprint que o marcador da cidade desenha no mapa-múndi (SpatialBoundsResolver) —
   * cresce com a população, nunca um envelope visual fixo. */
  bounds: { x: number; y: number; width: number; height: number };
  boundsAreDerived: boolean;
}

export interface InteriorSnapshot {
  id: { value: number };
  city: { value: string };
  buildingTypeId: number;
  occupancyModeled: boolean;
}

// Escopo de foco no cliente — pilha de drill-down (T8 controla, servidor só sabe o escopo atual).
export type FocusScope =
  | { kind: "World" }
  | { kind: "City"; cityId: string }
  | { kind: "Interior"; buildingId: string; cityId: string };

// Mesma regra de VisualScope.ScopeKey (src/LivingWorld.Api/Visual/VisualScope.cs) — usada pra
// confirmar que um envelope recebido é realmente do escopo atual antes de renderizar (evita
// mostrar o payload antigo por uma render enquanto o WebSocket do novo escopo ainda não respondeu).
export function focusScopeKey(scope: FocusScope): string {
  switch (scope.kind) {
    case "World":
      return "world";
    case "City":
      return `city:${scope.cityId}`;
    case "Interior":
      return `interior:${scope.buildingId}`;
  }
}
