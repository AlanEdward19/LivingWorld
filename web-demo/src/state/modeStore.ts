export type ExplorerMode = "experience" | "debug";

/**
 * Toggle Experience ↔ Debug (doc#116) — troca a linguagem/detalhe exibido pelas views afetadas
 * sem trocar a navegação atual (mesma tela, mesma entidade selecionada). Mesmo idioma de store
 * de `followStore.ts` (singleton, `useSyncExternalStore`-compatible).
 */
export class ModeStore {
  private mode: ExplorerMode = "experience";
  private readonly listeners = new Set<() => void>();

  currentMode(): ExplorerMode {
    return this.mode;
  }

  toggleMode(): void {
    this.mode = this.mode === "experience" ? "debug" : "experience";
    this.notify();
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

export const modeStore = new ModeStore();
