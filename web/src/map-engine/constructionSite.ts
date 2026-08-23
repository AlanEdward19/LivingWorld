import type { ProcessVisual } from "../data/contracts";
import type { AuthoritativeEntity, SpaceId } from "./types";

const SCAFFOLD_COLOR = "#8a6a3a";

export function constructionProgressLabel(progress: number): string {
  const pct = Math.round(Math.max(0, Math.min(1, progress)) * 100);
  return `Obra ${pct}%`;
}

export function constructionAccessibleLabel(progress: number): string {
  const pct = Math.round(Math.max(0, Math.min(1, progress)) * 100);
  return `Construção em andamento, ${pct}%`;
}

/** LWV-04.4: queued/in-progress construction is a site at the process location, not a finished building. */
export function constructionSiteEntityFromProcess(
  process: ProcessVisual,
  space: SpaceId,
): AuthoritativeEntity | null {
  if (process.kind !== "construction" || !process.location) {
    return null;
  }
  return {
    ref: { kind: "building", id: `construction:${process.id}`, space },
    position: { x: process.location.x, y: process.location.y },
    size: { w: 2, h: 2 },
    sizeIsDerived: true,
    color: SCAFFOLD_COLOR,
    label: constructionProgressLabel(process.progress),
    process: {
      kind: "construction",
      progress: process.progress,
      accessibleLabel: constructionAccessibleLabel(process.progress),
    },
  };
}

export function constructionSitesFromProcesses(
  processes: Iterable<ProcessVisual>,
  space: SpaceId,
): AuthoritativeEntity[] {
  const sites: AuthoritativeEntity[] = [];
  for (const process of processes) {
    const site = constructionSiteEntityFromProcess(process, space);
    if (site) sites.push(site);
  }
  return sites;
}
