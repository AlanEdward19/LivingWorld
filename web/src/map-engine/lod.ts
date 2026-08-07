// Fase 15.1, T6: política de LOD por zoom (design.md "Components" -> `LodPolicy`; master
// prompt §4). Generaliza o binário `isToken = zoom >= lodTokenThreshold`
// (web/src/components/GridCanvas.tsx:36,59) para 4 níveis com limiares configuráveis — nenhuma
// constante fixa embutida, quem chama decide os thresholds (VTT2-37..41 AC4).
import type { AuthoritativeEntity, EntityRef, Vec2 } from "./types";

export type LodLevel = "aggregate" | "dot" | "token" | "token-detail";

/** Limiares em ordem ascendente: abaixo de `aggregate` = cluster; acima de `detail` = token-detail. */
export interface LodThresholds {
  aggregate: number;
  token: number;
  detail: number;
}

/**
 * WHEN zoom < aggregate       THEN "aggregate"     (AC1 — densidade/cluster)
 * WHEN aggregate <= zoom < token THEN "dot"         (AC2 — dot individual)
 * WHEN token <= zoom < detail THEN "token"          (AC3 — token com anel/cor)
 * WHEN zoom >= detail         THEN "token-detail"   (AC3 — + rótulo/info)
 */
export function levelFor(zoom: number, thresholds: LodThresholds): LodLevel {
  if (zoom < thresholds.aggregate) {
    return "aggregate";
  }
  if (zoom < thresholds.token) {
    return "dot";
  }
  if (zoom < thresholds.detail) {
    return "token";
  }
  return "token-detail";
}

export interface ClusterCell {
  bucketX: number;
  bucketY: number;
  count: number;
  refs: EntityRef[];
}

function bucketKey(p: Vec2, cellSize: number): { bucketX: number; bucketY: number } {
  return {
    bucketX: Math.floor(p.x / cellSize),
    bucketY: Math.floor(p.y / cellSize),
  };
}

/**
 * Agrupa entidades por bucket espacial determinístico (mesma posição -> mesmo bucket, sempre).
 * A contagem total das entidades de entrada é preservada na soma de `count` das saídas
 * (VTT2-37 AC1) — nenhuma entidade é descartada, só reduzida a densidade por região.
 */
export function aggregate(entities: AuthoritativeEntity[], cellSize: number): ClusterCell[] {
  const byBucket = new Map<string, ClusterCell>();

  for (const entity of entities) {
    const { bucketX, bucketY } = bucketKey(entity.position, cellSize);
    const key = `${bucketX}:${bucketY}`;
    const existing = byBucket.get(key);
    if (existing) {
      existing.count += 1;
      existing.refs.push(entity.ref);
    } else {
      byBucket.set(key, { bucketX, bucketY, count: 1, refs: [entity.ref] });
    }
  }

  return [...byBucket.values()];
}
