export type CreatorGroundKind = "grass" | "soil" | "water";

export interface CreatorGroundCell {
  kind: CreatorGroundKind;
  color: string;
  variant: number;
}

const GRASS = ["#506f43", "#5b7848", "#627b4c", "#47683f"] as const;
const SOIL = ["#776044", "#876b48", "#6f5941"] as const;
const WATER = ["#39758a", "#3f8292", "#346b81"] as const;

function hash(seed: number, x: number, y: number): number {
  let value = (seed ^ Math.imul(x + 31, 374761393) ^ Math.imul(y + 17, 668265263)) >>> 0;
  value = Math.imul(value ^ (value >>> 13), 1274126177);
  return (value ^ (value >>> 16)) >>> 0;
}

/** Prévia cosmética pura, compartilhada entre a entrada e o editor. */
export function creatorGroundAt(seed: number, x: number, y: number): CreatorGroundCell {
  const value = hash(seed, x, y);
  const riverBand = Math.abs(x - ((seed + y * 3) % 11)) <= 1 && value % 4 !== 0;
  if (riverBand || value % 19 === 0) {
    return { kind: "water", color: WATER[value % WATER.length], variant: value };
  }
  if (value % 8 === 0 || value % 11 === 0) {
    return { kind: "soil", color: SOIL[value % SOIL.length], variant: value };
  }
  return { kind: "grass", color: GRASS[value % GRASS.length], variant: value };
}

export function creatorPaintColor(
  seed: number,
  x: number,
  y: number,
  cell: PaintedCell,
): string {
  const value = hash(seed, x, y);
  if (cell.water) return WATER[value % WATER.length];
  const palette = cell.terrain === 2 ? SOIL : GRASS;
  return palette[value % palette.length];
}
import type { PaintedCell } from "../../scenarioDefaults";
