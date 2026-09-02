export type EntryScreen =
  | { kind: "main-menu" }
  | { kind: "create"; draftId?: string }
  | { kind: "worlds" }
  | { kind: "settings" }
  | { kind: "world"; worldId: string }
  | { kind: "sandbox" };

function parsePath(pathname: string): EntryScreen {
  const segments = pathname.split("/").filter(Boolean);
  if (segments.length === 0) return { kind: "main-menu" };

  const [head, id] = segments;
  switch (head) {
    case "create":
      return { kind: "create", draftId: id };
    case "worlds":
      return id ? { kind: "world", worldId: id } : { kind: "worlds" };
    case "settings":
      return { kind: "settings" };
    case "sandbox":
      return { kind: "sandbox" };
    default:
      return { kind: "main-menu" };
  }
}

/**
 * Top-level router that owns `/`, `/create(/:draftId)`, `/worlds(/:worldId)`, `/settings`.
 * Same store shape as `nav/NavigationStore` (listeners + subscribe, `useSyncExternalStore`-
 * compatible) but flat — the entry screens don't need a breadcrumb stack, and `/worlds/:worldId`
 * hands off pathname ownership to the existing shell's own `NavigationStore` (see EntryRoot).
 */
export class EntryRouter {
  private readonly listeners = new Set<() => void>();
  private readonly popstateHandler = () => this.sync();
  // `useSyncExternalStore` requires a referentially-stable snapshot when nothing changed —
  // parsing on every `current()` call returned a new object each render and looped forever.
  private snapshot: EntryScreen = parsePath(typeof window !== "undefined" ? window.location.pathname : "/");

  private sync(): void {
    this.snapshot = parsePath(window.location.pathname);
    this.notify();
  }

  navigate(path: string): void {
    window.history.pushState(null, "", path);
    this.sync();
  }

  replace(path: string): void {
    window.history.replaceState(null, "", path);
    this.sync();
  }

  current(): EntryScreen {
    return this.snapshot;
  }

  start(): void {
    this.sync();
    window.addEventListener("popstate", this.popstateHandler);
  }

  stop(): void {
    window.removeEventListener("popstate", this.popstateHandler);
  }

  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify(): void {
    for (const listener of this.listeners) listener();
  }
}
