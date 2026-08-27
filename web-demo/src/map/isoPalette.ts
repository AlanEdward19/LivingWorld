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

/** Piso/terreno neutro sob os blocos, em todos os níveis de zoom. */
export const TERRAIN_PALETTE: IsoPalette = { top: "#c7cdd1", left: "#a9b0b6", right: "#8c949b" };

/** Marcador de assentamento no zoom "mundo" (sem prédio individual renderizado ali). */
export const SETTLEMENT_PALETTE: IsoPalette = { top: "#d9c98f", left: "#b8a86e", right: "#8f8153" };
