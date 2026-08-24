import type { ProcessVisual } from "../data/contracts";
import type { AuthoritativeEntity, SpaceId } from "./types";

function colorOf(token: string): string {
  let hash = 0;
  for (let index = 0; index < token.length; index += 1) {
    hash = (hash * 31 + token.charCodeAt(index)) | 0;
  }
  return `hsla(${Math.abs(hash) % 360}, 78%, 56%, 0.72)`;
}

export function extraordinaryConstructEntity(
  process: ProcessVisual,
  space: SpaceId,
): AuthoritativeEntity | null {
  if (process.kind !== "extraordinary-construct" || !process.location || !process.footprint?.length) {
    return null;
  }
  const origin = process.location;
  const cells = process.footprint.map((cell) => ({
    x: cell.x - origin.x,
    y: cell.y - origin.y,
    color: colorOf(process.appearanceToken ?? process.descriptorKey),
  }));
  const width = Math.max(...cells.map((cell) => cell.x)) + 1;
  const height = Math.max(...cells.map((cell) => cell.y)) + 1;
  return {
    ref: { kind: "building", id: `construct:${-process.id - 1}`, space },
    position: origin,
    size: { w: width, h: height },
    sizeIsDerived: false,
    color: colorOf(process.appearanceToken ?? process.descriptorKey),
    footprintCells: cells,
    decorative: true,
  };
}

export function extraordinaryConstructEntities(
  processes: Iterable<ProcessVisual>,
  space: SpaceId,
): AuthoritativeEntity[] {
  return [...processes]
    .map((process) => extraordinaryConstructEntity(process, space))
    .filter((entity): entity is AuthoritativeEntity => entity !== null);
}
