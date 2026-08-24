export interface ArchitecturePalette {
  roof: string;
  roofLight: string;
  wall: string;
  trim: string;
}

export type BuildingAppearanceKind = "residence" | "agriculture" | "forge" | "generic";

export interface BuildingAppearance {
  kind: BuildingAppearanceKind;
  palette: ArchitecturePalette;
}

const PALETTES: readonly ArchitecturePalette[] = [
  { roof: "#8b4638", roofLight: "#b86449", wall: "#c7b18a", trim: "#54362b" },
  { roof: "#675149", roofLight: "#8e6e5d", wall: "#b9aa8d", trim: "#49362f" },
  { roof: "#75423d", roofLight: "#a65d4f", wall: "#a98e69", trim: "#4a3029" },
  { roof: "#76523a", roofLight: "#a97849", wall: "#c2aa7e", trim: "#533927" },
  { roof: "#594d52", roofLight: "#7c6970", wall: "#aaa38f", trim: "#40343a" },
] as const;

export function architectureHash(value: string): number {
  let result = 2166136261;
  for (let index = 0; index < value.length; index += 1) {
    result ^= value.charCodeAt(index);
    result = Math.imul(result, 16777619);
  }
  return result >>> 0;
}

/** Aparência cosmética pura: identidade igual produz sempre os mesmos materiais. */
export function architecturePalette(identity: string): ArchitecturePalette {
  return PALETTES[architectureHash(identity) % PALETTES.length];
}

/**
 * Convenções do cenário padrão: -1 é a residência física sem receita de construção, 1 é a
 * produção agrícola e 2 é a forja. Qualquer tipo autorado/futuro recebe uma arquitetura
 * genérica, portanto nenhum Building desaparece por falta de conhecimento do cliente.
 */
export function buildingAppearanceForType(buildingTypeId: number | undefined, identity: string): BuildingAppearance {
  if (buildingTypeId === -1) {
    return { kind: "residence", palette: architecturePalette(`${identity}:residence`) };
  }
  if (buildingTypeId === 1) {
    return {
      kind: "agriculture",
      palette: { roof: "#6f7f3d", roofLight: "#91a957", wall: "#8a683d", trim: "#493822" },
    };
  }
  if (buildingTypeId === 2) {
    return {
      kind: "forge",
      palette: { roof: "#393a3d", roofLight: "#5b5c60", wall: "#71685e", trim: "#2b2522" },
    };
  }
  return {
    kind: "generic",
    palette: { roof: "#53616b", roofLight: "#758793", wall: "#aaa18f", trim: "#39434a" },
  };
}

export function cityRoofPalette(cityId: string, count = 8): ArchitecturePalette[] {
  return Array.from({ length: count }, (_, index) => architecturePalette(`${cityId}:house:${index}`));
}
