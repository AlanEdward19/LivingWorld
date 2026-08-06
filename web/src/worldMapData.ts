import { colorById } from "./colorById";
import type { GlobalSnapshot, RiversLayerPayload, TerrainLayerPayload } from "./types";
import type { GridMarker } from "./components/GridCanvas";

/// Extraído de WorldMapView pra ser reusado pelo MapOverlay (tecla M, T15) sem duplicar a
/// leitura da camada Terrain/Rivers nem a montagem de marcadores de cidade/NPC externo.
export function terrainColorLookup(snapshot: GlobalSnapshot): (x: number, y: number) => string | undefined {
  const byCell = new Map<string, number>();
  const payload = snapshot.layers.Terrain?.payload as TerrainLayerPayload | undefined;
  for (const entry of payload ?? []) byCell.set(`${entry.key.x},${entry.key.y}`, entry.value.id);
  return (x, y) => {
    const id = byCell.get(`${x},${y}`);
    return id === undefined ? undefined : colorById(id);
  };
}

export function riverOverlayPoints(snapshot: GlobalSnapshot): { x: number; y: number; color: string }[] {
  const payload = snapshot.layers.Rivers?.payload as RiversLayerPayload | undefined;
  return (payload ?? []).map((c) => ({ x: c.x, y: c.y, color: "#3a7bd5" }));
}

export function worldMarkers(snapshot: GlobalSnapshot): GridMarker[] {
  return [
    ...snapshot.cities.map((c) => ({
      id: `city:${c.id.value}`,
      x: c.location.x,
      y: c.location.y,
      color: "#d9a94f",
    })),
    ...snapshot.externalNpcs.map((n) => ({
      id: `npc:${n.id.value}`,
      x: n.location.x,
      y: n.location.y,
      color: "#7fd9c4",
      dotRadius: 1.5,
    })),
  ];
}
