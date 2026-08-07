// Fase 15.1, T11: store de VIEW — espaço observado, câmera por espaço, camadas ativas, follow
// (design.md "Components" -> `ViewStore`; master prompt §33). Recebe `PortalSource` por
// construtor (T0/OQ-2) — o mock no Estágio 1, a projeção real (`Portals` de
// `GlobalSnapshot`/`CitySnapshot`) na T33, mesma interface, mesma chamada.
import type { PortalSource } from "../data/sources";
import type { PortalEndpointDto } from "../data/contracts";
import type { CameraState, EntityRef, SpaceId } from "../map-engine/types";
import { toScopeKey } from "../map-engine/space";

function endpointToSpaceId(endpoint: PortalEndpointDto): SpaceId {
  switch (endpoint.space) {
    case "World":
      return { kind: "World" };
    case "City":
      return { kind: "City", cityId: endpoint.refId };
    case "Building":
      // SPEC_DEVIATION: PortalEndpointDto (data/contracts.ts) não carrega o cityId do prédio,
      // só o refId do próprio prédio — não dá pra montar um SpaceId de Building completo.
      // Nenhuma fixture desta fase declara portal de/para Building (só World<->City); fica sem
      // suporte até o contrato ganhar esse campo ou uma fase precisar de portal de prédio.
      throw new Error("portal endpoints into a Building space are not supported yet");
  }
}

export class ViewStore {
  private current: SpaceId = { kind: "World" };
  private readonly cameraByScope = new Map<string, CameraState>();
  private readonly activeLayerIds = new Set<string>();
  private followed: EntityRef | null = null;
  private readonly listeners = new Set<() => void>();

  constructor(private readonly portalSource: PortalSource) {}

  currentSpace(): SpaceId {
    return this.current;
  }

  /** Navegação direta (clique numa cidade/prédio, botão Open, breadcrumb) — sem portal específico. */
  enter(target: SpaceId): void {
    this.current = target;
    this.notify();
  }

  goToAncestor(target: SpaceId): void {
    this.current = target;
    this.notify();
  }

  /**
   * Resolve a transição por um portal nomeado do espaço atual (VTT2-66 AC5): consulta
   * `portalSource.portalsOf(current)` e entra pelo extremo oposto do portal encontrado — o
   * MESMO código resolve qualquer portal, nunca um `if` por nome/id de entrada.
   */
  enterViaPortal(portalId: string): SpaceId {
    const portal = this.portalSource.portalsOf(this.current).find((p) => p.id === portalId);
    if (!portal) {
      throw new Error(`no portal "${portalId}" reachable from the current space`);
    }

    const currentKey = toScopeKey(this.current);
    const fromKey = toScopeKey(endpointToSpaceId(portal.from));
    const otherEndpoint = fromKey === currentKey ? portal.to : portal.from;
    const target = endpointToSpaceId(otherEndpoint);
    this.enter(target);
    return target;
  }

  /** Câmera guardada para `space`, ou `fallback` (ex.: fit-to-screen) se nunca foi visitado. */
  cameraFor(space: SpaceId, fallback: CameraState): CameraState {
    return this.cameraByScope.get(toScopeKey(space)) ?? fallback;
  }

  /** Persiste a câmera do espaço — chamado continuamente por quem move a câmera (T13). */
  recordCamera(space: SpaceId, state: CameraState): void {
    this.cameraByScope.set(toScopeKey(space), { center: { ...state.center }, scale: state.scale });
  }

  setLayerActive(id: string, on: boolean): void {
    if (on) {
      this.activeLayerIds.add(id);
    } else {
      this.activeLayerIds.delete(id);
    }
  }

  isLayerActive(id: string): boolean {
    return this.activeLayerIds.has(id);
  }

  startFollow(entity: EntityRef): void {
    this.followed = entity;
  }

  stopFollow(): void {
    this.followed = null;
  }

  followedEntity(): EntityRef | null {
    return this.followed;
  }

  /**
   * Registro de listener (T14) — quem monta React (`useSyncExternalStore`) reage só quando
   * `currentSpace()` de fato muda de referência; `recordCamera`/`setLayerActive`/follow não
   * chamam `notify()`, então não geram re-render nenhum (nada os lê via este canal hoje).
   */
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
