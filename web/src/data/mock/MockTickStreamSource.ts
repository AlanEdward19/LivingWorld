// Fase 15.1, T0: implementação mock de `TickStreamSource` — emite `ScopeTickDelta` sintéticos
// num intervalo derivado de `MockClock.ticksPerSecond` (nunca uma constante fixa: master
// prompt §21 proíbe o backlog que uma constante produziria a velocidades altas) e não emite
// nada enquanto o clock está pausado. Nenhum `fetch`/`WebSocket` — só `setInterval`.
import type { TickStreamSource } from "../sources";
import type { NpcPositionDelta, ScopeTickDelta } from "../contracts";
import type { SpaceId } from "../../map-engine/types";
import type { MockClock } from "./MockClock";
import { mockScopeKey } from "./mockScopeKey";

interface ActiveSubscription {
  timer: ReturnType<typeof setInterval>;
  onDrop?: () => void;
}

export class MockTickStreamSource implements TickStreamSource {
  private readonly accumulatedMsByScope = new Map<string, number>();
  private readonly activeByScope = new Map<string, ActiveSubscription>();

  constructor(
    private readonly clock: MockClock,
    private readonly npcsByScope: Record<string, NpcPositionDelta[]>,
    private readonly checkIntervalMs = 20,
  ) {}

  subscribe(space: SpaceId, onDelta: (delta: ScopeTickDelta) => void, onDrop?: () => void): () => void {
    const key = mockScopeKey(space);
    const npcs = this.npcsByScope[key] ?? [];
    if (npcs.length === 0) {
      return () => {};
    }

    let cursor = 0;
    const timer = setInterval(() => {
      if (this.clock.isPaused) {
        return;
      }

      const periodMs = 1000 / this.clock.ticksPerSecond;
      const accumulated = (this.accumulatedMsByScope.get(key) ?? 0) + this.checkIntervalMs;
      let remaining = accumulated;
      const moved: NpcPositionDelta[] = [];

      while (remaining >= periodMs) {
        remaining -= periodMs;
        this.clock.advanceOneTick();
        const moving = npcs[cursor];
        cursor = (cursor + 1) % npcs.length;
        moved.push({ npcId: moving.npcId, location: { x: moving.location.x + 1, y: moving.location.y } });
      }

      this.accumulatedMsByScope.set(key, remaining);

      if (moved.length > 0) {
        onDelta({ tick: this.clock.tick, moved, removed: [] });
      }
    }, this.checkIntervalMs);

    this.activeByScope.set(key, { timer, onDrop });

    return () => {
      clearInterval(timer);
      this.activeByScope.delete(key);
    };
  }

  /** Simula queda de conexão do stream deste escopo — dispara o `onDrop` registrado, se houver. */
  simulateDrop(space: SpaceId): void {
    const key = mockScopeKey(space);
    const active = this.activeByScope.get(key);
    if (!active) {
      return;
    }
    clearInterval(active.timer);
    this.activeByScope.delete(key);
    active.onDrop?.();
  }
}
