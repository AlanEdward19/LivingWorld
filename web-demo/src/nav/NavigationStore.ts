import type { WorldFixture } from "../fixture/types";

export type Route =
  | { kind: "world" }
  | { kind: "settlement"; id: string }
  | { kind: "building"; id: string }
  | { kind: "household"; id: string }
  | { kind: "agent"; id: string }
  | { kind: "causal"; eventId: string }
  | { kind: "timeline"; scope: { type: "world" | "settlement" | "household" | "agent"; id?: string } }
  | { kind: "life"; agentId: string }
  | { kind: "feed" }
  | { kind: "threads" }
  | { kind: "thread"; id: string };

const ROOT_ROUTE: Route = { kind: "world" };

const LOCATION_KINDS = new Set<Route["kind"]>(["world", "settlement", "building"]);

/** Rotas de localização (World/Settlement/Building) — as únicas que formam a hierarquia
 * espacial do breadcrumb. As demais (household/agent/causal/timeline/life/feed/threads/thread)
 * nunca entram na pilha, só trocam a rota atual (pedido do usuário 2026-08-31: breadcrumb
 * cheio de entidades não-espaciais confundia "onde estou no mundo"). */
function isLocationRoute(route: Route): boolean {
  return LOCATION_KINDS.has(route.kind);
}

/** Profundidade de uma rota de localização na hierarquia World(0) > Settlement(1) > Building(2). */
function locationDepth(kind: Route["kind"]): number {
  switch (kind) {
    case "world":
      return 0;
    case "settlement":
      return 1;
    case "building":
      return 2;
    default:
      return -1;
  }
}

/** Serializa uma `Route` pra um path de URL — inverso de `pathToRoute`. */
export function routeToPath(route: Route): string {
  switch (route.kind) {
    case "world":
      return "/";
    case "settlement":
      return `/settlement/${route.id}`;
    case "building":
      return `/building/${route.id}`;
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
    building: (candidate: string) => !fixture || fixture.settlements.some((s) => s.buildings.some((b) => b.id === candidate)),
    household: (candidate: string) => !fixture || fixture.households.some((h) => h.id === candidate),
    agent: (candidate: string) => !fixture || fixture.agents.some((a) => a.id === candidate),
    event: (candidate: string) => !fixture || fixture.events.some((e) => e.eventId === candidate),
    thread: (candidate: string) => !fixture || fixture.storyThreads.some((t) => t.id === candidate),
  };

  switch (head) {
    case "settlement":
      return id && exists.settlement(id) ? { kind: "settlement", id } : null;
    case "building":
      return id && exists.building(id) ? { kind: "building", id } : null;
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
 * Só rotas de localização (World/Settlement/Building) entram nessa pilha; o resto (household,
 * agent, causal, timeline, life, feed, threads, thread) só troca `current()`, sem virar crumb —
 * pedido do usuário 2026-08-31. Nenhuma view guarda estado de navegação próprio; toda view lê
 * `current()`/`breadcrumb()` daqui. Idioma de store igual a `web/src/state/viewStore.ts`
 * (listeners + subscribe, compatível com `useSyncExternalStore`), implementação própria desta
 * demo.
 *
 * Sincronização de URL (T12): `push`/`back` escrevem em `history` e `syncWithHistory` escuta
 * `popstate` — só um lado escreve por vez (nunca os dois, ver Risk do design.md), evitando
 * dessincronia entre o botão voltar do browser e `back()`.
 */
export class NavigationStore {
  /** Hierarquia espacial (World > Settlement > Building) — o breadcrumb exibido é exatamente
   * esta pilha. */
  private locationStack: Route[] = [ROOT_ROUTE];
  /** Rotas não-espaciais empilhadas por cima da localização atual (ex.: agent → causal →
   * timeline abertos em sequência) — nunca aparecem no breadcrumb, só em `current()`/`back()`. */
  private overlayStack: Route[] = [];
  /** Última rota de localização vigente quando a `overlayStack` começou a crescer — o que
   * `CenterStage` deve continuar mostrando embaixo de um overlay (causal/timeline/life/...). */
  private overlayBase: Route | null = null;
  private readonly listeners = new Set<() => void>();
  private readonly popstateHandler = () => this.syncFromLocation();

  constructor(private readonly fixture?: WorldFixture) { }

  /** Empilha uma nova rota no topo — localização cresce a pilha do breadcrumb, o resto só vira
   * a rota atual. */
  push(route: Route): void {
    this.setRoute(route, "push");
  }

  /**
   * Substitui o topo em vez de empilhar — usado quando o foco troca ENTRE irmãos (ex.: clicar
   * num NPC depois de já estar vendo outro), não quando entra de verdade num nível novo
   * (world→settlement→building). Sem isso a pilha crescia sem fim e "Back"/clicar fora nunca
   * voltava direto pra a cidade — feedback do usuário 2026-08-26 (AD-021).
   */
  replace(route: Route): void {
    this.setRoute(route, "replace");
  }

  private setRoute(route: Route, mode: "push" | "replace"): void {
    if (isLocationRoute(route)) {
      // Sempre reconstrói pela profundidade (não pelo push/replace do chamador): entrar mais
      // fundo cresce a pilha, trocar de irmão ao mesmo nível ou subir a substitui — mesmo
      // resultado não importa de onde a navegação veio (mapa, sidebar, busca...).
      this.locationStack = [...this.locationStack.slice(0, locationDepth(route.kind)), route];
      this.overlayStack = [];
      this.overlayBase = null;
    } else {
      if (this.overlayStack.length === 0) this.overlayBase = this.current();
      this.overlayStack = mode === "push" ? [...this.overlayStack, route] : [...this.overlayStack.slice(0, -1), route];
    }

    if (typeof window !== "undefined") {
      if (mode === "push") window.history.pushState(null, "", routeToPath(route));
      else window.history.replaceState(null, "", routeToPath(route));
    }
    this.notify();
  }

  /** Desempilha o topo (overlay primeiro, depois localização), voltando pra rota anterior.
   * Nunca esvazia o root (`world`). */
  back(): void {
    if (this.overlayStack.length > 0) {
      this.overlayStack = this.overlayStack.slice(0, -1);
      if (this.overlayStack.length === 0) this.overlayBase = null;
    } else if (this.locationStack.length > 1) {
      this.locationStack = this.locationStack.slice(0, -1);
    } else {
      return;
    }
    if (typeof window !== "undefined") {
      window.history.pushState(null, "", routeToPath(this.current()));
    }
    this.notify();
  }

  current(): Route {
    return this.overlayStack.length > 0 ? this.overlayStack[this.overlayStack.length - 1] : this.locationStack[this.locationStack.length - 1];
  }

  /** Se `back()` tem pra onde voltar (overlay aberto ou localização acima do root). */
  canGoBack(): boolean {
    return this.overlayStack.length > 0 || this.locationStack.length > 1;
  }

  /**
   * Pilha de localização, da raiz (`world`) até a rota espacial atual — só World/Settlement/
   * Building. Retorna a referência interna (nunca uma cópia nova) — muda só quando o conteúdo
   * de fato muda. Necessário pra `useSyncExternalStore` (Breadcrumb, T19) não entrar em loop de
   * re-render.
   */
  breadcrumb(): Route[] {
    return this.locationStack;
  }

  /** Rota espacial (world/settlement/building) que `CenterStage` deve mostrar embaixo de um
   * overlay não-espacial (causal/timeline/life/feed/threads/thread) atualmente aberto. */
  spatialContext(): Route {
    return this.overlayBase ?? this.locationStack[this.locationStack.length - 1];
  }

  /** Clique num crumb do breadcrumb — salta direto pra aquela rota de localização, descartando
   * tudo que foi empilhado depois dela (localização mais funda e qualquer overlay aberto). */
  goTo(route: Route): void {
    const index = this.locationStack.findIndex((entry) => routeToPath(entry) === routeToPath(route));
    if (index === -1) return;
    this.locationStack = this.locationStack.slice(0, index + 1);
    this.overlayStack = [];
    this.overlayBase = null;
    if (typeof window !== "undefined") {
      window.history.pushState(null, "", routeToPath(this.current()));
    }
    this.notify();
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
      this.locationStack = [ROOT_ROUTE];
      this.overlayStack = [];
      this.overlayBase = null;
      window.history.replaceState(null, "", routeToPath(ROOT_ROUTE));
      this.notify();
      return;
    }

    this.locationStack = this.locationStackFor(route);
    this.overlayStack = isLocationRoute(route) ? [] : [route];
    this.overlayBase = isLocationRoute(route) ? null : this.locationStack[this.locationStack.length - 1];

    if (isInitialLoad) {
      window.history.replaceState(null, "", routeToPath(this.current()));
    }
    this.notify();
  }

  /** Reconstrói a pilha de localização pra um deep-link — building precisa achar o settlement
   * dono no fixture pra manter World > Settlement > Building completo (sem fixture, cai pra
   * World > Building, ponytail: aceitável pra esse caso raro sem dados). */
  private locationStackFor(route: Route): Route[] {
    if (route.kind === "settlement") return [ROOT_ROUTE, route];
    if (route.kind === "building") {
      const owner = this.fixture?.settlements.find((s) => s.buildings.some((b) => b.id === route.id));
      return owner ? [ROOT_ROUTE, { kind: "settlement", id: owner.id }, route] : [ROOT_ROUTE, route];
    }
    return [ROOT_ROUTE];
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
