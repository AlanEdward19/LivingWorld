import { colorById } from "./colorById";
import type { GlobalSnapshot, RiversLayerPayload, TerrainLayerPayload } from "./types";
import { cityGroundAt, type GroundVisual } from "./map-engine/worldVisuals";

/// Extraído de WorldMapView (Fase 15) — reusado por `WorldMapView.tsx` (T14) como `CellSource`/
/// `ActiveLayer` do map engine.
export function terrainColorLookup(snapshot: GlobalSnapshot): (x: number, y: number) => string | undefined {
  const byCell = new Map<string, number>();
  const payload = snapshot.layers.Terrain?.payload as TerrainLayerPayload | undefined;
  for (const entry of payload ?? []) byCell.set(`${entry.key.x},${entry.key.y}`, entry.value.id);
  return (x, y) => {
    const id = byCell.get(`${x},${y}`);
    return id === undefined ? cityGroundAt("world", x, y).color : colorById(id, 42, 38);
  };
}

export function terrainDetailLookup(snapshot: GlobalSnapshot): (x: number, y: number) => GroundVisual {
  const byCell = new Set<string>();
  const payload = snapshot.layers.Terrain?.payload as TerrainLayerPayload | undefined;
  for (const entry of payload ?? []) byCell.add(`${entry.key.x},${entry.key.y}`);
  return (x, y) => {
    const visual = cityGroundAt("world", x, y);
    return byCell.has(`${x},${y}`) ? { ...visual, detail: "grass" } : visual;
  };
}

export function riverOverlayPoints(snapshot: GlobalSnapshot): { x: number; y: number; color: string }[] {
  const payload = snapshot.layers.Rivers?.payload as RiversLayerPayload | undefined;
  return (payload ?? []).map((c) => ({ x: c.x, y: c.y, color: "#3a7bd5" }));
}
