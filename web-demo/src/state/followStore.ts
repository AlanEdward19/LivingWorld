/**
 * Toggle de "seguir" — persistido só na sessão (em memória), NUNCA altera o fixture (doc#128:
 * "Follow altera apresentação, nunca simulação"). Idioma de store igual a `NavigationStore`/
 * `web/src/state/viewStore.ts` (listeners + subscribe, `useSyncExternalStore`-compatible).
 */
export class FollowStore {
  private followed = new Set<string>();
  // Cache de array pra `followedIds()` — só troca de referência quando o conteúdo muda de
  // verdade (mesmo motivo do `NavigationStore.breadcrumb()`: `useSyncExternalStore` entra em
  // loop se `getSnapshot()` devolver um array novo a cada chamada). Ordem = ordem em que cada
  // entidade foi seguida — NUNCA reordenada por `activate()` (bug real: reusar essa mesma lista
  // pra derivar o alvo da câmera fazia a lista "Followed" da sidebar mudar de ordem sozinha toda
  // vez que o usuário clicava num nome nela, só pra trocar quem a câmera acompanha).
  private followedList: string[] = [];
  // Alvo da câmera (`SettlementStage`) — completamente separado da ordem da lista acima.
  private activeId: string | null = null;
  private readonly listeners = new Set<() => void>();

  /** Alterna seguir/deixar de seguir uma entidade — nunca duplica o destaque (Edge Case da spec). */
  toggleFollow(entityId: string): void {
    if (this.followed.has(entityId)) {
      this.followed.delete(entityId);
      if (this.activeId === entityId) {
        // Recém des-seguiu quem a câmera acompanhava — cai pro último ainda seguido (mesma regra
        // de "sempre o mais recente" que valia antes de existir `activate()`).
        const remaining = this.followedList.filter((id) => id !== entityId);
        this.activeId = remaining.length > 0 ? remaining[remaining.length - 1] : null;
      }
    } else {
      this.followed.add(entityId);
      this.activeId = entityId;
    }
    this.followedList = [...this.followed];
    this.notify();
  }

  /**
   * Pedido do usuário 2026-08-26: dá pra seguir vários NPCs (lista "Followed" da sidebar
   * esquerda), mas a câmera só faz sentido travada em UM por vez. Clicar num nome já seguido
   * nessa lista troca QUEM a câmera acompanha sem mexer na ordem visível da lista (`followedList`
   * fica intocada) e sem precisar tirar/pôr o follow.
   */
  activate(entityId: string): void {
    if (!this.followed.has(entityId)) {
      this.followed.add(entityId);
      this.followedList = [...this.followed];
    }
    this.activeId = entityId;
    this.notify();
  }

  /** Quem a câmera acompanha agora (`SettlementStage`) — os demais na lista "Followed" continuam
   * só como bookmark. */
  activeFollowId(): string | null {
    return this.activeId;
  }

  /**
   * Pedido do usuário 2026-08-26: arrastar o mapa pra longe de quem a câmera está seguindo
   * "desgruda" — para de travar a câmera nele e esconde o anel — mas ele (e todo mundo mais que
   * já estava seguido) continua na lista "Followed" normalmente, ninguém é des-seguido. Só um
   * clique de novo no nome (`activate`) ou seguir outro agent (`toggleFollow`) volta a travar a
   * câmera em alguém.
   */
  detachCamera(): void {
    if (this.activeId === null) return;
    this.activeId = null;
    this.notify();
  }

  isFollowed(entityId: string): boolean {
    return this.followed.has(entityId);
  }

  /** Todos os ids atualmente seguidos — usado pelo Explorer "Followed" tab (doc §41). */
  followedIds(): string[] {
    return this.followedList;
  }

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

/** Instância única da demo — importar este módulo em qualquer view dá o mesmo estado, o que
 * garante que o destaque de follow persiste ao navegar entre telas (spec P2 AC3). */
export const followStore = new FollowStore();
