import type { BuildingKind } from "../fixture/types";

export interface IsoPalette {
  top: string;
  left: string;
  right: string;
}

// Paleta nova, neutra/atmosférica (doc#136) — não rústica/medieval como architectureAppearance.ts
// (design.md § O que NÃO é reusado). 3 faces flat-shaded fixas, sem gradiente/textura.
const BUILDING_PALETTES: Record<BuildingKind, IsoPalette> = {
  residence: { top: "#8f9bb3", left: "#6b7794", right: "#4f5975" },
  agriculture: { top: "#a8b892", left: "#829271", right: "#606f52" },
  forge: { top: "#b3958a", left: "#8f7266", right: "#6b524a" },
  generic: { top: "#9aa5ad", left: "#76818a", right: "#565f66" },
};

export function paletteForBuildingKind(kind: BuildingKind): IsoPalette {
  return BUILDING_PALETTES[kind];
}

/** Footprint de settlement no World Map (`render/WorldStage.tsx`) — reaproveitada em vez de cada
 * renderer ter sua própria cópia dos mesmos tons. */
export const SETTLEMENT_PALETTE: IsoPalette = { top: "#d9c98f", left: "#b8a86e", right: "#8f8153" };
