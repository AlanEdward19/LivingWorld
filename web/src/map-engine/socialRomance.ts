import type { AuthoritativeEntity } from "./types";

export const SOCIALIZE_ACTION = 3;
export const SOCIAL_EVENT_KINDS = [9, 10, 11, 12] as const;

export interface SocialLink {
  fromId: string;
  toId: string;
}

function pairKey(a: string, b: string): SocialLink {
  return a < b ? { fromId: a, toId: b } : { fromId: b, toId: a };
}

function pushUnique(links: SocialLink[], a: string, b: string): void {
  if (a === b) return;
  const next = pairKey(a, b);
  if (!links.some((link) => link.fromId === next.fromId && link.toId === next.toId)) {
    links.push(next);
  }
}

// Adjacente = mesmo tile ou vizinho imediato (distância de Chebyshev <=1). Acima disso o
// traço tracejado leria como "gritando pelo mapa", não como conversa (feedback do usuário).
const ADJACENT_TILE_DISTANCE = 1;

function tileDistance(a: AuthoritativeEntity, b: AuthoritativeEntity): number {
  return Math.max(Math.abs(a.position.x - b.position.x), Math.abs(a.position.y - b.position.y));
}

function nearestAdjacentPair(candidates: readonly AuthoritativeEntity[]): [AuthoritativeEntity, AuthoritativeEntity] | null {
  let best: [AuthoritativeEntity, AuthoritativeEntity] | null = null;
  let bestDist = Infinity;
  for (let i = 0; i < candidates.length; i++) {
    for (let j = i + 1; j < candidates.length; j++) {
      const d = tileDistance(candidates[i], candidates[j]);
      if (d <= ADJACENT_TILE_DISTANCE && d < bestDist) {
        bestDist = d;
        best = [candidates[i], candidates[j]];
      }
    }
  }
  return best;
}

/** Visual overlay only: pair materialized tokens. Never a motor constraint. */
export function resolveSocialLinks(
  entities: readonly AuthoritativeEntity[],
  events: readonly { kind: number }[] = [],
): SocialLink[] {
  const npcs = entities.filter((entity) => entity.ref.kind === "npc");
  const links: SocialLink[] = [];
  const socializing = [...npcs]
    .filter((entity) => entity.currentAction === SOCIALIZE_ACTION)
    .sort((a, b) => a.ref.id.localeCompare(b.ref.id));

  const used = new Set<string>();
  const pool = [...socializing];
  let pair = nearestAdjacentPair(pool.filter((entity) => !used.has(entity.ref.id)));
  while (pair) {
    pushUnique(links, pair[0].ref.id, pair[1].ref.id);
    used.add(pair[0].ref.id);
    used.add(pair[1].ref.id);
    pair = nearestAdjacentPair(pool.filter((entity) => !used.has(entity.ref.id)));
  }

  const hasSocialEvent = events.some((event) =>
    (SOCIAL_EVENT_KINDS as readonly number[]).includes(event.kind),
  );
  if (hasSocialEvent && links.length === 0 && npcs.length >= 2) {
    const fallback = nearestAdjacentPair(npcs);
    if (fallback) pushUnique(links, fallback[0].ref.id, fallback[1].ref.id);
  }
  return links;
}
