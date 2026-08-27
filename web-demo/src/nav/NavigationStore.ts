import type { WorldFixture } from "../fixture/types";

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

/** Serializa uma `Route` pra um path de URL — inverso de `pathToRoute`. */
export function routeToPath(route: Route): string {
  switch (route.kind) {
    case "world":
      return "/";
    case "settlement":
      return `/settlement/${route.id}`;
    case "household":
      return `/household/${route.id}`;
    case "agent":
      return `/agent/${route.id}`;
    case "causal":
      return `/causal/${route.eventId}`;
    case "timeline":
      return route.scope.id ? `/timeline/${route.scope.type}/${route.scope.id}` : `/timeline/${route.scope.type}`;
    case "life":
      return `/life/${route.agentId}`;
    case "feed":
      return "/feed";
    case "threads":
      return "/threads";
    case "thread":
      return `/thread/${route.id}`;
  }
}

/**
 * Parseia um path de URL pra `Route`, validando o(s) id(s) contra o fixture (Edge Case da
 * spec: id inexistente redireciona pra World View). Retorna `null` quando o path não bate com
 * nenhum formato conhecido OU o id referenciado não existe no fixture.
 */
export function pathToRoute(path: string, fixture?: WorldFixture): Route | null {
  const segments = path.split("/").filter(Boolean);
  if (segments.length === 0) return ROOT_ROUTE;

  const [head, id, sub] = segments;
  const exists = {
    settlement: (candidate: string) => !fixture || fixture.settlements.some((s) => s.id === candidate),
    household: (candidate: string) => !fixture || fixture.households.some((h) => h.id === candidate),
    agent: (candidate: string) => !fixture || fixture.agents.some((a) => a.id === candidate),
    event: (candidate: string) => !fixture || fixture.events.some((e) => e.eventId === candidate),
    thread: (candidate: string) => !fixture || fixture.storyThreads.some((t) => t.id === candidate),
  };

  switch (head) {
    case "settlement":
      return id && exists.settlement(id) ? { kind: "settlement", id } : null;
    case "household":
      return id && exists.household(id) ? { kind: "household", id } : null;
    case "agent":
      return id && exists.agent(id) ? { kind: "agent", id } : null;
    case "causal":
      return id && exists.event(id) ? { kind: "causal", eventId: id } : null;
    case "timeline": {
      const type = id as "world" | "settlement" | "household" | "agent" | undefined;
      if (!type || !["world", "settlement", "household", "agent"].includes(type)) return null;
      if (sub) {
        const scopedExists =
          type === "settlement" ? exists.settlement(sub) : type === "household" ? exists.household(sub) : type === "agent" ? exists.agent(sub) : true;
        if (!scopedExists) return null;
        return { kind: "timeline", scope: { type, id: sub } };
      }
      return { kind: "timeline", scope: { type } };
    }
    case "life":
      return id && exists.agent(id) ? { kind: "life", agentId: id } : null;
    case "feed":
      return { kind: "feed" };
    case "threads":
      return { kind: "threads" };
    case "thread":
      return id && exists.thread(id) ? { kind: "thread", id } : null;
    default:
      return null;
  }
}

/**
 * Pilha de breadcrumb — fonte única de verdade de "onde estou" (design.md § Architecture).
 * Nenhuma view guarda estado de navegação próprio; toda view lê `current()`/`breadcrumb()`
 * daqui. Idioma de store igual a `web/src/state/viewStore.ts` (listeners + subscribe,
 * compatível com `useSyncExternalStore`), implementação própria desta demo.
 *
 * Sincronização de URL (T12): `push`/`back` escrevem em `history` e `syncWithHistory` escuta
 * `popstate` — só um lado escreve por vez (nunca os dois, ver Risk do design.md), evitando
 * dessincronia entre o botão voltar do browser e `back()`.
 */
export class NavigationStore {
  private stack: Route[] = [ROOT_ROUTE];
  private readonly listeners = new Set<() => void>();
  private readonly popstateHandler = () => this.syncFromLocation();

  constructor(private readonly fixture?: WorldFixture) {}

  /** Empilha uma nova rota no topo — nunca substitui a pilha existente. */
  push(route: Route): void {
    this.stack = [...this.stack, route];
    if (typeof window !== "undefined") {
      window.history.pushState(null, "", routeToPath(route));
    }
    this.notify();
  }

  /** Desempilha o topo, voltando pra rota anterior. Nunca esvazia o root (`world`). */
  back(): void {
    if (this.stack.length <= 1) return;
    this.stack = this.stack.slice(0, -1);
    if (typeof window !== "undefined") {
      window.history.pushState(null, "", routeToPath(this.current()));
    }
    this.notify();
  }

  current(): Route {
    return this.stack[this.stack.length - 1];
  }

  /**
   * Pilha completa, da raiz (`world`) até a rota atual, na ordem de navegação. Retorna a
   * referência interna (nunca uma cópia nova) — `push`/`back` sempre trocam `this.stack` por um
   * array novo em vez de mutar, então a referência só muda quando o conteúdo de fato muda.
   * Necessário pra `useSyncExternalStore` (Breadcrumb, T19) não entrar em loop de re-render.
   */
  breadcrumb(): Route[] {
    return this.stack;
  }

  /**
   * Carrega o estado inicial a partir da URL atual (deep-link) e passa a escutar `popstate`
   * (botão voltar/avançar do browser). Chamar uma única vez, no bootstrap da app.
   */
  syncWithHistory(): void {
    this.syncFromLocation(true);
    window.addEventListener("popstate", this.popstateHandler);
  }

  /** Só pra teste/cleanup — remove o listener de `popstate`. */
  stopSyncWithHistory(): void {
    window.removeEventListener("popstate", this.popstateHandler);
  }

  private syncFromLocation(isInitialLoad = false): void {
    const route = pathToRoute(window.location.pathname, this.fixture);
    if (!route) {
      this.stack = [ROOT_ROUTE];
      window.history.replaceState(null, "", routeToPath(ROOT_ROUTE));
      this.notify();
      return;
    }

    this.stack = route.kind === "world" ? [ROOT_ROUTE] : [ROOT_ROUTE, route];
    if (isInitialLoad) {
      window.history.replaceState(null, "", routeToPath(this.current()));
    }
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
