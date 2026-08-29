import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { EntryRouter } from "../../src/entry/EntryRouter";

let router: EntryRouter;

beforeEach(() => {
  window.history.pushState(null, "", "/");
  router = new EntryRouter();
  router.start();
});

afterEach(() => {
  router.stop();
});

describe("EntryRouter", () => {
  it("maps paths to screens", () => {
    expect(router.current()).toEqual({ kind: "main-menu" });

    router.navigate("/create");
    expect(router.current()).toEqual({ kind: "create", draftId: undefined });

    router.navigate("/create/draft-1");
    expect(router.current()).toEqual({ kind: "create", draftId: "draft-1" });

    router.navigate("/worlds");
    expect(router.current()).toEqual({ kind: "worlds" });

    router.navigate("/worlds/eldoria");
    expect(router.current()).toEqual({ kind: "world", worldId: "eldoria" });

    router.navigate("/settings");
    expect(router.current()).toEqual({ kind: "settings" });
  });

  it("unknown paths fall back to the Main Menu (doc §2 — root is always the entry point)", () => {
    router.navigate("/whatever");
    expect(router.current()).toEqual({ kind: "main-menu" });
  });

  it("notifies subscribers on browser back/forward", () => {
    router.navigate("/create");
    router.navigate("/worlds");
    let notified = 0;
    router.subscribe(() => notified++);
    window.history.back();
    // popstate is async in jsdom; give it a tick via dispatch instead for determinism.
    window.dispatchEvent(new PopStateEvent("popstate"));
    expect(notified).toBeGreaterThan(0);
  });
});
