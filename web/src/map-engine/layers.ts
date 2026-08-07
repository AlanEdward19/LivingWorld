// Fase 15.1, T18: ordem de composição das camadas globais, declarada e determinística (as
// mesmas 14 chaves de `VisualLayerName`/`GlobalLayerBuilder.cs`). Terrain é sempre a base
// (`CellSource`, desenhada fora do array `layers`); as demais compõem por cima nesta ordem.
import type { VisualLayerName } from "../types";
import type { ActiveLayer } from "./renderer";

export const LAYER_Z_ORDER: VisualLayerName[] = [
  "Terrain",
  "Biome",
  "Climate",
  "Mountains",
  "Rivers",
  "Resources",
  "Roads",
  "Borders",
  "Kingdoms",
  "Cities",
  "Villages",
  "Routes",
  "Migrations",
  "Conflicts",
];

export function sortActiveLayers(layers: ActiveLayer[]): ActiveLayer[] {
  const order = new Map(LAYER_Z_ORDER.map((name, i) => [name, i]));
  return [...layers].sort((a, b) => (order.get(a.id as VisualLayerName) ?? 999) - (order.get(b.id as VisualLayerName) ?? 999));
}
