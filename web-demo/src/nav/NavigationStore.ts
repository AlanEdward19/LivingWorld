export type Route =
  | { kind: "world" }
  | { kind: "settlement"; id: string }
  | { kind: "household"; id: string }
  | { kind: "agent"; id: string }
  | { kind: "causal"; eventId: string }
  | { kind: "timeline"; scope: { type: "world" | "settlement" | "household" | "agent"; id?: string } }
  | { kind: "life"; agentId: string }
  | { kind: "feed" }
  | { kind: "threads" }
  | { kind: "thread"; id: string };

const ROOT_ROUTE: Route = { kind: "world" };

/**
 * Pilha de breadcrumb — fonte única de verdade de "onde estou" (design.md § Architecture).
 * Nenhuma view guarda estado de navegação próprio; toda view lê `current()`/`breadcrumb()`
 * daqui. Idioma de store igual a `web/src/state/viewStore.ts` (listeners + subscribe,
 * compatível com `useSyncExternalStore`), implementação própria desta demo.
 */
export class NavigationStore {
  private stack: Route[] = [ROOT_ROUTE];
  private readonly listeners = new Set<() => void>();

  /** Empilha uma nova rota no topo — nunca substitui a pilha existente. */
  push(route: Route): void {
    this.stack = [...this.stack, route];
    this.notify();
  }

  /** Desempilha o topo, voltando pra rota anterior. Nunca esvazia o root (`world`). */
  back(): void {
    if (this.stack.length <= 1) return;
    this.stack = this.stack.slice(0, -1);
    this.notify();
  }

  current(): Route {
    return this.stack[this.stack.length - 1];
  }

  /** Pilha completa, da raiz (`world`) até a rota atual, na ordem de navegação. */
  breadcrumb(): Route[] {
    return [...this.stack];
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
