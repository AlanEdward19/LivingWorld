import type { CityVisual, FutureGlobalCityMarker } from "../data/contracts";
import { generateCityWallFootprint, MATERIAL_COLOR, roofColorFor } from "./buildingFootprint";
import { CATEGORY_COLOR } from "./categoryColors";
import type { AuthoritativeEntity } from "./types";

const WORLD = { kind: "World" as const };

type CityMarkerInput = {
  id: string;
  name: string;
  bounds: { x: number; y: number; width: number; height: number };
  boundsAreDerived: boolean;
};

function toMarker(city: FutureGlobalCityMarker | CityVisual): CityMarkerInput {
  return {
    id: city.id.value,
    name: city.name ?? city.id.value,
    bounds: city.bounds,
    boundsAreDerived: "boundsAreDerived" in city ? city.boundsAreDerived : true,
  };
}

export function mergeWorldCityMarkers(
  snapshotCities: readonly FutureGlobalCityMarker[],
  livingCities: Iterable<CityVisual>,
  floor: number,
): AuthoritativeEntity[] {
  const byId = new Map<string, CityMarkerInput>();
  for (const city of snapshotCities) byId.set(city.id.value, toMarker(city));
  for (const city of livingCities) byId.set(city.id.value, toMarker(city));
  return [...byId.values()].map((city) => {
    const wallCells = generateCityWallFootprint(city.id, city.bounds.width, city.bounds.height, floor);
    return {
      ref: { kind: "city" as const, id: city.id, space: WORLD },
      label: city.name,
      position: { x: city.bounds.x, y: city.bounds.y },
      size: { w: city.bounds.width, h: city.bounds.height },
      sizeIsDerived: city.boundsAreDerived,
      color: CATEGORY_COLOR.city,
      footprintCells: wallCells.map((cell) => ({
        x: cell.x,
        y: cell.y,
        color: cell.material === "floor" ? roofColorFor(`${city.id}:${Math.floor(cell.x / 2)}:${Math.floor(cell.y / 2)}`) : MATERIAL_COLOR[cell.material],
        material: cell.material === "floor" ? "roof" as const : cell.material,
      })),
    };
  });
}
