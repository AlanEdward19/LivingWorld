import type { AuthoritativeEntity } from "./types";

/** LWV-07.3 birth/death family — SettlementFounded (20) is T22, not this list. */
export const LIFECYCLE_EVENT_KINDS = [0, 1, 2, 13, 14] as const;

export interface LifecycleMoment {
  kind: number;
  position: { x: number; y: number };
}

export function resolveLifecycleMoments(
  events: readonly { kind: number; location?: { x: number; y: number } | null }[],
  entities: readonly AuthoritativeEntity[],
): LifecycleMoment[] {
  const fallback = entities.find((entity) => entity.ref.kind === "npc")?.position;
  const moments: LifecycleMoment[] = [];
  for (const event of events) {
    if (!(LIFECYCLE_EVENT_KINDS as readonly number[]).includes(event.kind)) continue;
    // LWV-07.3: burst at the event cell when the wire has one. First-NPC fallback only if absent.
    const position = event.location != null ? event.location : fallback;
    if (!position) continue;
    moments.push({ kind: event.kind, position });
  }
  return moments;
}
