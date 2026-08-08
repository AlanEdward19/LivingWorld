export interface ArchitecturePalette {
  roof: string;
  roofLight: string;
  wall: string;
  trim: string;
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

export function cityRoofPalette(cityId: string, count = 8): ArchitecturePalette[] {
  return Array.from({ length: count }, (_, index) => architecturePalette(`${cityId}:house:${index}`));
}
