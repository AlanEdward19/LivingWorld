/**
 * Toggle de "seguir" — persistido só na sessão (em memória), NUNCA altera o fixture (doc#128:
 * "Follow altera apresentação, nunca simulação"). Idioma de store igual a `NavigationStore`/
 * `web/src/state/viewStore.ts` (listeners + subscribe, `useSyncExternalStore`-compatible).
 */
export class FollowStore {
  private followed = new Set<string>();
  private readonly listeners = new Set<() => void>();

  /** Alterna seguir/deixar de seguir uma entidade — nunca duplica o destaque (Edge Case da spec). */
  toggleFollow(entityId: string): void {
    if (this.followed.has(entityId)) {
      this.followed.delete(entityId);
    } else {
      this.followed.add(entityId);
    }
    this.notify();
  }

  isFollowed(entityId: string): boolean {
    return this.followed.has(entityId);
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
