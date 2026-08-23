import { CATEGORY_COLOR } from "./categoryColors";
import { generateBuildingFootprint, MATERIAL_COLOR, roofColorFor } from "./buildingFootprint";
import type { AuthoritativeEntity, SpaceId } from "./types";
import type { CityBuildingMarker } from "../types";
import type { BuildingVisual } from "../data/contracts";

/**
 * Stage 4 T18 / LWV-04.5: API `location` is the footprint origin (min cell), not a
 * client-side ring around the city center. `locationIsDerived` drives dashed honesty
 * (`sizeIsDerived`), matching authored vs resolver fallback on the wire.
 */
export function cityBuildingEntityFromMarker(
  building: CityBuildingMarker,
  space: SpaceId,
  floor: number,
): AuthoritativeEntity {
  const buildingId = String(building.id.value);
  const footprintCells = generateBuildingFootprint(buildingId, building.buildingTypeId, floor);
  const width = Math.max(...footprintCells.map((c) => c.x)) + 1;
  const height = Math.max(...footprintCells.map((c) => c.y)) + 1;

  return {
    ref: { kind: "building", id: buildingId, space },
    position: { x: building.location.x, y: building.location.y },
    size: { w: width, h: height },
    sizeIsDerived: building.locationIsDerived,
    color: CATEGORY_COLOR.building,
    footprintCells: footprintCells.map((c) => ({
      x: c.x,
      y: c.y,
      color: c.material === "door" ? MATERIAL_COLOR.door : roofColorFor(`${buildingId}:${building.buildingTypeId}`),
      material: c.material === "door" ? ("door" as const) : ("roof" as const),
    })),
  };
}

function toBuildingMarker(building: CityBuildingMarker | BuildingVisual): CityBuildingMarker {
  return {
    id: building.id,
    buildingTypeId: building.buildingTypeId,
    location: building.location,
    locationIsDerived: "locationIsDerived" in building ? building.locationIsDerived : false,
  };
}

/**
 * Merge do snapshot inicial (`CitySnapshot.buildings`) com o delta vivo (`BuildingVisual`,
 * atualizado a cada tick via `ScopeTickDelta.buildingUpserts`) — mesma técnica de
 * `mergeWorldCityMarkers`: sem isso, casas/locais de trabalho concluídos após o snapshot
 * inicial nunca aparecem, porque `CitySnapshot.buildings` nunca é recarregado por delta.
 */
export function mergeCityBuildingMarkers(
  snapshotBuildings: readonly CityBuildingMarker[],
  livingBuildings: Iterable<BuildingVisual>,
  space: SpaceId,
  floor: number,
): AuthoritativeEntity[] {
  const byId = new Map<number, CityBuildingMarker>();
  for (const building of snapshotBuildings) byId.set(building.id.value, toBuildingMarker(building));
  for (const building of livingBuildings) byId.set(building.id.value, toBuildingMarker(building));
  return [...byId.values()].map((building) => cityBuildingEntityFromMarker(building, space, floor));
}
