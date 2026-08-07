// Fase 15.1, T9: SpatialContext (design.md "Components" -> `SpatialContext`/`SpaceStack`;
// master prompt §9). Modela a hierarquia WorldSpace > CitySpace > BuildingSpace e as
// transformações entre escalas.
//
// SPEC_DEVIATION: `localToParent`/`parentToLocal` assumem que a origem (0,0) do espaço filho
// coincide com a origem do espaço pai — não recebem (nem existe hoje) o footprint/âncora real
// de onde uma cidade ou prédio está posicionado dentro do pai. `Building` não tem posição no
// domínio (context.md gap 5) e `City` só tem `Location`, não bounds, até T20/T28/T34
// entregarem o campo de projeção real (OQ-1). O Done-when desta task só exige round-trip
// consistente entre as duas funções — não precisão contra um footprint real, que ainda não
// existe para testar contra. Quando T20/T28/T34 landarem, este módulo ganha o parâmetro de
// âncora e os call-sites passam a fornecê-lo.
import type { SpaceId, Vec2 } from "./types";

/**
 * Constante única de escala entre níveis (master prompt §10). O motor não fornece nenhuma
 * escala física de onde derivar isto (nenhuma unidade real por tile) — são valores de produto
 * até o domínio expor algo mensurável. Nunca literal espalhado pelo código: só aqui.
 */
export const SCALE = {
  /** Quantos tiles de CitySpace cabem em 1 tile de WorldSpace. */
  worldTilesPerCityTile: 20,
  /** Quantos tiles de BuildingSpace cabem em 1 tile de CitySpace. */
  cityTilesPerBuildingTile: 6,
} as const;

function childScaleFactor(space: SpaceId): number {
  switch (space.kind) {
    case "World":
      throw new Error("WorldSpace has no parent");
    case "City":
      return SCALE.worldTilesPerCityTile;
    case "Building":
      return SCALE.cityTilesPerBuildingTile;
  }
}

/** Converte uma coordenada local de `space` para a coordenada correspondente no espaço pai. */
export function localToParent(space: SpaceId, local: Vec2): Vec2 {
  const factor = childScaleFactor(space);
  return { x: local.x / factor, y: local.y / factor };
}

/** Inversa de `localToParent`: converte uma coordenada do espaço pai para local de `space`. */
export function parentToLocal(space: SpaceId, parentLocal: Vec2): Vec2 {
  const factor = childScaleFactor(space);
  return { x: parentLocal.x * factor, y: parentLocal.y * factor };
}

/** Cadeia de ancestrais raiz-primeiro, incluindo o próprio espaço — insumo direto do breadcrumb. */
export function ancestors(space: SpaceId): SpaceId[] {
  switch (space.kind) {
    case "World":
      return [space];
    case "City":
      return [{ kind: "World" }, space];
    case "Building":
      return [{ kind: "World" }, { kind: "City", cityId: space.cityId }, space];
  }
}

/**
 * Mesma regra de `VisualScope.ScopeKey` (`src/LivingWorld.Api/Visual/VisualScope.cs:13-19`):
 * `world`, `city:{id}`, `interior:{id}` — `Building` mapeia para `interior`, não `building`,
 * porque é essa a chave que o servidor real vai emitir (o motor não tem conceito de
 * "BuildingSpace" como nome, só `Interior`).
 */
export function toScopeKey(space: SpaceId): string {
  switch (space.kind) {
    case "World":
      return "world";
    case "City":
      return `city:${space.cityId}`;
    case "Building":
      return `interior:${space.buildingId}`;
  }
}
