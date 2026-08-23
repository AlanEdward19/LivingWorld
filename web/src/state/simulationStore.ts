// Fase 15.1, T10: dono único do estado autoritativo do escopo observado (design.md
// "Components" -> `SimulationStore`; master prompt §5/§33). Recebe `SnapshotSource` e
// `TickStreamSource` por construtor (T0) — nunca constrói transporte próprio, é isso que torna
// a troca mock->real (T31) uma troca de argumento, não uma reescrita. `subscribe` é um registro
// simples de listener, fora do ciclo de render do React (VTT2-32) — quem monta React decide
// quando reagir, o store não sabe que React existe.
import type { NpcInspectionSource, SnapshotSource, TickStreamSource } from "../data/sources";
import type { ScopeTickDelta } from "../data/contracts";
import type { AuthoritativeEntity, SpaceId } from "../map-engine/types";
import type { VisualSnapshotEnvelope } from "../types";
import { toScopeKey } from "../map-engine/space";
import { CATEGORY_COLOR } from "../map-engine/categoryColors";
import { overlayProcessOnNpc } from "../map-engine/cityNpcOverlay";
import {
  applyLivingDelta,
  emptyLivingViewState,
  livingViewStateFromWire,
  type LivingViewState,
} from "./frontendCapabilityConsumers";
import type { LivingScopeStateWire } from "../data/contracts";
import type { NpcInspection } from "../data/contracts";

const RECONNECT_BACKOFF_MS = 500;

interface NpcMarkerLike {
  id: { value: number };
  location: { x: number; y: number };
  currentAction?: number | null;
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
  private observedSpace: SpaceId | null = null;
  // T50 fix (bug "seguir NPC entre escopos"): `entitiesOf`/`currentPayload` já ficam vazios
  // entre o `observeSpace(newSpace)` síncrono e o `loadSnapshot` assíncrono resolver (o
  // `envelope` antigo só é substituído quando `applySnapshot` bate o `scopeKey` novo) — sem
  // sinal explícito disso, quem consome `entitiesOf` (SelectionStore.syncWithSpace via MapView)
  // não sabia distinguir "ainda carregando o novo escopo" de "escopo carregado e genuinamente
  // vazio", e limpava a seleção/follow numa troca de escopo por engano.
  private snapshotReady = false;
  private livingState: LivingViewState = emptyLivingViewState();
  private lastSequence = 0;
  private readonly listeners = new Set<() => void>();
  private stopTickStream: (() => void) | null = null;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly npcInspections = new Map<number, NpcInspection | null>();
  private readonly inspectionLoads = new Map<number, Promise<NpcInspection | null>>();

  constructor(
    private readonly snapshotSource: SnapshotSource,
    private readonly tickStreamSource: TickStreamSource,
    private readonly npcInspectionSource?: NpcInspectionSource,
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
    this.observedSpace = space;
    this.livingState = emptyLivingViewState();
    this.lastSequence = 0;
    this.snapshotReady = false;

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
    this.lastSequence = envelope.cursor.sequence;
    this.livingState = livingStateFromSnapshot(envelope.payload);
    this.snapshotReady = true;
    this.notify();
  }

  /** T50 fix: `true` só depois que o snapshot do escopo observado ATUAL terminou de carregar —
   * o sinal que faltava pra distinguir "escopo trocou e ainda não chegou nada" de "chegou e não
   * tem nada mesmo". */
  isSpaceReady(space: SpaceId): boolean {
    return this.snapshotReady && toScopeKey(space) === this.observedScopeKey;
  }

  /** Aplica um delta incremental sobre o snapshot corrente — nunca refaz um `load()`. */
  applyDelta(delta: ScopeTickDelta): void {
    if (delta.sequence !== undefined) {
      if (delta.sequence <= this.lastSequence) return;
      if (delta.fromSequence !== this.lastSequence) {
        if (this.observedSpace && this.observedScopeKey)
          void this.loadSnapshot(this.observedSpace, this.observedScopeKey);
        return;
      }
      this.lastSequence = delta.sequence;
    }
    this.livingState = applyLivingDelta(this.livingState, delta);
    this.notify();
    for (const npcId of this.npcInspections.keys()) void this.refreshNpcInspection(npcId);
  }

  npcInspectionOf(npcId: number): NpcInspection | null | undefined {
    return this.npcInspections.get(npcId);
  }

  inspectNpc(npcId: number): Promise<NpcInspection | null> {
    return this.refreshNpcInspection(npcId);
  }

  private refreshNpcInspection(npcId: number): Promise<NpcInspection | null> {
    if (!this.npcInspectionSource) {
      // Sem fonte configurada: cacheia `null` já síncrono em vez de deixar `npcInspectionOf`
      // indefinido pra sempre — quem consulta o cache pra decidir "ainda não sei" vs. "não
      // existe" (MapView.refreshEntities, T50 round 3) precisa desse veredito imediato.
      this.npcInspections.set(npcId, null);
      return Promise.resolve(null);
    }
    const active = this.inspectionLoads.get(npcId);
    if (active) return active;

    const load = this.npcInspectionSource.load(npcId)
      .then((inspection) => {
        this.npcInspections.set(npcId, inspection);
        this.notify();
        return inspection;
      })
      .catch(() => {
        this.npcInspections.set(npcId, null);
        this.notify();
        return null;
      })
      .finally(() => this.inspectionLoads.delete(npcId));
    this.inspectionLoads.set(npcId, load);
    return load;
  }

  livingStateOf(space: SpaceId): LivingViewState {
    return this.envelope && toScopeKey(space) === this.observedScopeKey
      ? this.livingState
      : emptyLivingViewState();
  }

  /**
   * Payload bruto do snapshot corrente, tipado por quem chama — usado por views (T14) que
   * precisam de dados que `entitiesOf` não expõe (cidades, prédios, camadas). Referência
   * estável entre chamadas até o próximo `applySnapshot`, então um consumidor via
   * `useSyncExternalStore` não re-renderiza a cada delta — só quando o snapshot de fato muda.
   */
  currentPayload<TPayload>(space: SpaceId): TPayload | null {
    if (!this.envelope || toScopeKey(space) !== this.observedScopeKey) {
      return null;
    }
    return this.envelope.payload as TPayload;
  }

  entitiesOf(space: SpaceId): AuthoritativeEntity[] {
    if (!this.envelope || toScopeKey(space) !== this.observedScopeKey) {
      return [];
    }
    const processes = [...this.livingState.processes.values()];
    return [...this.livingState.npcs.values()]
      .map((marker) => overlayProcessOnNpc({
        ref: { kind: "npc" as const, id: String(marker.id.value), space },
        position: marker.location,
        size: { w: 1, h: 1 },
        sizeIsDerived: false,
        color: CATEGORY_COLOR.npc,
        currentAction: marker.currentAction,
        cityId: marker.city?.value,
        travelDestination: marker.relocationDestination ?? undefined,
      }, processes));
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

function livingStateFromSnapshot(payload: unknown): LivingViewState {
  if (payload && typeof payload === "object" && "livingState" in payload) {
    return livingViewStateFromWire((payload as { livingState?: LivingScopeStateWire }).livingState);
  }
  const npcs = extractNpcMarkers(payload).map((marker) => ({
    id: marker.id,
    location: marker.location,
    currentAction: marker.currentAction ?? null,
  }));
  return livingViewStateFromWire({ npcs, cities: [], buildings: [], processes: [], indicators: [], events: [] });
}
