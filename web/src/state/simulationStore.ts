// Fase 15.1, T10: dono único do estado autoritativo do escopo observado (design.md
// "Components" -> `SimulationStore`; master prompt §5/§33). Recebe `SnapshotSource` e
// `TickStreamSource` por construtor (T0) — nunca constrói transporte próprio, é isso que torna
// a troca mock->real (T31) uma troca de argumento, não uma reescrita. `subscribe` é um registro
// simples de listener, fora do ciclo de render do React (VTT2-32) — quem monta React decide
// quando reagir, o store não sabe que React existe.
import type { SnapshotSource, TickStreamSource } from "../data/sources";
import type { NpcPositionDelta, ScopeTickDelta } from "../data/contracts";
import type { AuthoritativeEntity, SpaceId } from "../map-engine/types";
import type { VisualSnapshotEnvelope } from "../types";
import { toScopeKey } from "../map-engine/space";
import { colorById } from "../colorById";

const RECONNECT_BACKOFF_MS = 500;

interface NpcMarkerLike {
  id: { value: number };
  location: { x: number; y: number };
}

/**
 * Extrai a lista de NPCs de um payload de snapshot sem assumir qual escopo é — `GlobalSnapshot`
 * usa `externalNpcs`, `CitySnapshot` usa `residents`; `InteriorSnapshot` não tem lista de NPC
 * ainda (context.md), então devolve vazio.
 */
function extractNpcMarkers(payload: unknown): NpcMarkerLike[] {
  if (!payload || typeof payload !== "object") {
    return [];
  }
  const candidate = payload as Record<string, unknown>;
  const list = candidate.externalNpcs ?? candidate.residents;
  return Array.isArray(list) ? (list as NpcMarkerLike[]) : [];
}

export class SimulationStore {
  private envelope: VisualSnapshotEnvelope<unknown> | null = null;
  private observedScopeKey: string | null = null;
  private readonly positionOverrides = new Map<number, NpcPositionDelta["location"]>();
  private readonly removedIds = new Set<number>();
  private readonly listeners = new Set<() => void>();
  private stopTickStream: (() => void) | null = null;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private readonly snapshotSource: SnapshotSource,
    private readonly tickStreamSource: TickStreamSource,
  ) {}

  /** Começa a observar `space`: carrega o snapshot inicial e assina o stream de deltas dele. */
  async observeSpace(space: SpaceId): Promise<void> {
    this.stopTickStream?.();
    this.stopTickStream = null;
    if (this.reconnectTimer !== null) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }

    const key = toScopeKey(space);
    this.observedScopeKey = key;
    this.positionOverrides.clear();
    this.removedIds.clear();

    await this.loadSnapshot(space, key);

    this.stopTickStream = this.tickStreamSource.subscribe(
      space,
      (delta) => {
        if (key !== this.observedScopeKey) {
          return; // trocou de espaço entre a assinatura e a entrega deste delta
        }
        this.applyDelta(delta);
      },
      () => this.scheduleReconnect(space, key),
    );
  }

  private async loadSnapshot(space: SpaceId, expectedKey: string): Promise<void> {
    const envelope = await this.snapshotSource.load(space);
    if (this.observedScopeKey !== expectedKey) {
      return; // usuário já mudou de espaço enquanto o load estava em voo
    }
    this.applySnapshot(envelope);
  }

  private scheduleReconnect(space: SpaceId, key: string): void {
    if (this.reconnectTimer !== null) {
      clearTimeout(this.reconnectTimer);
    }
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      void this.loadSnapshot(space, key);
    }, RECONNECT_BACKOFF_MS);
  }

  /** Aplica um snapshot completo — descartado se o escopo não é o observado (VTT2-11). */
  applySnapshot(envelope: VisualSnapshotEnvelope<unknown>): void {
    if (envelope.scope.scopeKey !== this.observedScopeKey) {
      return;
    }
    this.envelope = envelope;
    this.positionOverrides.clear();
    this.removedIds.clear();
    this.notify();
  }

  /** Aplica um delta incremental sobre o snapshot corrente — nunca refaz um `load()`. */
  applyDelta(delta: ScopeTickDelta): void {
    for (const moved of delta.moved) {
      this.positionOverrides.set(moved.npcId, moved.location);
      this.removedIds.delete(moved.npcId);
    }
    for (const removedId of delta.removed) {
      this.removedIds.add(removedId);
      this.positionOverrides.delete(removedId);
    }
    this.notify();
  }

  entitiesOf(space: SpaceId): AuthoritativeEntity[] {
    if (!this.envelope || toScopeKey(space) !== this.observedScopeKey) {
      return [];
    }
    return extractNpcMarkers(this.envelope.payload)
      .filter((marker) => !this.removedIds.has(marker.id.value))
      .map((marker) => ({
        ref: { kind: "npc" as const, id: String(marker.id.value), space },
        position: this.positionOverrides.get(marker.id.value) ?? marker.location,
        size: { w: 1, h: 1 },
        sizeIsDerived: false,
        color: colorById(marker.id.value),
      }));
  }

  /** Registro de listener puro — nenhuma dependência de React, notificação síncrona. */
  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify(): void {
    for (const listener of this.listeners) {
      listener();
    }
  }
}
