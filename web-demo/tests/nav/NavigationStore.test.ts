import { afterEach, describe, expect, it, vi } from "vitest";
import { NavigationStore, pathToRoute, routeToPath, type Route } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("NavigationStore", () => {
  it("starts at the world route", () => {
    const store = new NavigationStore();
    expect(store.current()).toEqual({ kind: "world" });
  });

  it("breadcrumb starts with only the world route", () => {
    const store = new NavigationStore();
    expect(store.breadcrumb()).toEqual([{ kind: "world" }]);
  });

  it("push adds the route to the top of the stack and makes it current", () => {
    const store = new NavigationStore();
    store.push({ kind: "settlement", id: "oakbridge" });
    expect(store.current()).toEqual({ kind: "settlement", id: "oakbridge" });
  });

  it("replace swaps the current route without growing the breadcrumb — non-location kinds never join it (AD-021)", () => {
    const store = new NavigationStore();
    store.push({ kind: "settlement", id: "oakbridge" });
    store.replace({ kind: "agent", id: "mira-valen" });
    expect(store.current()).toEqual({ kind: "agent", id: "mira-valen" });
    expect(store.breadcrumb()).toEqual([{ kind: "world" }, { kind: "settlement", id: "oakbridge" }]);
  });

  it("replace after replace keeps the breadcrumb flat — sibling switches never accumulate", () => {
    const store = new NavigationStore();
    store.push({ kind: "settlement", id: "oakbridge" });
    store.replace({ kind: "agent", id: "mira-valen" });
    store.replace({ kind: "agent", id: "corvin" });
    expect(store.current()).toEqual({ kind: "agent", id: "corvin" });
    expect(store.breadcrumb()).toEqual([{ kind: "world" }, { kind: "settlement", id: "oakbridge" }]);
  });

  it("building is a location kind — pushing one descends the breadcrumb, replacing a sibling swaps it", () => {
    const store = new NavigationStore();
    store.push({ kind: "settlement", id: "oakbridge" });
    store.push({ kind: "building", id: "bld-corvin-bakery" });
    expect(store.breadcrumb()).toEqual([{ kind: "world" }, { kind: "settlement", id: "oakbridge" }, { kind: "building", id: "bld-corvin-bakery" }]);

    store.replace({ kind: "building", id: "bld-valen-house" });
    expect(store.breadcrumb()).toEqual([{ kind: "world" }, { kind: "settlement", id: "oakbridge" }, { kind: "building", id: "bld-valen-house" }]);
  });

  it("replace updates the URL via replaceState, not pushState (doesn't grow browser history)", () => {
    const store = new NavigationStore();
    store.push({ kind: "settlement", id: "oakbridge" });
    store.replace({ kind: "agent", id: "mira-valen" });
    expect(window.location.pathname).toBe("/agent/mira-valen");
    window.history.replaceState(null, "", "/");
  });

  it("maintains a consistent stack across 5+ pushes and backs — only location kinds grow the breadcrumb", () => {
    const store = new NavigationStore();
    store.push({ kind: "settlement", id: "oakbridge" });
    store.push({ kind: "household", id: "valen-household" });
    store.push({ kind: "agent", id: "mira-valen" });
    store.push({ kind: "causal", eventId: "evt-grain-prices-rose" });
    store.push({ kind: "timeline", scope: { type: "agent", id: "mira-valen" } });

    expect(store.breadcrumb()).toEqual([{ kind: "world" }, { kind: "settlement", id: "oakbridge" }]);

    store.back();
    expect(store.current()).toEqual({ kind: "causal", eventId: "evt-grain-prices-rose" });

    store.back();
    store.back();
    expect(store.current()).toEqual({ kind: "household", id: "valen-household" });

    store.back();
    expect(store.current()).toEqual({ kind: "settlement", id: "oakbridge" });

    store.back();
    expect(store.current()).toEqual({ kind: "world" });
  });

  it("back() at the root route is a no-op — never empties the stack", () => {
    const store = new NavigationStore();
    store.back();
    store.back();
    expect(store.current()).toEqual({ kind: "world" });
    expect(store.breadcrumb()).toEqual([{ kind: "world" }]);
  });

  it("breadcrumb() returns only the location stack, in navigation order", () => {
    const store = new NavigationStore();
    store.push({ kind: "settlement", id: "oakbridge" });
    store.push({ kind: "household", id: "valen-household" });
    expect(store.breadcrumb()).toEqual([{ kind: "world" }, { kind: "settlement", id: "oakbridge" }]);
    expect(store.current()).toEqual({ kind: "household", id: "valen-household" });
  });

  it("canGoBack() is true for an open overlay even when the breadcrumb itself is only World", () => {
    const store = new NavigationStore();
    expect(store.canGoBack()).toBe(false);

    store.push({ kind: "causal", eventId: "evt-grain-prices-rose" });
    expect(store.breadcrumb()).toEqual([{ kind: "world" }]);
    expect(store.canGoBack()).toBe(true);

    store.back();
    expect(store.current()).toEqual({ kind: "world" });
    expect(store.canGoBack()).toBe(false);
  });

  it("goTo() jumps straight to a clicked breadcrumb entry, discarding everything pushed after it", () => {
    const store = new NavigationStore();
    store.push({ kind: "settlement", id: "oakbridge" });
    store.push({ kind: "building", id: "bld-corvin-bakery" });
    store.push({ kind: "agent", id: "mira-valen" });
    store.push({ kind: "causal", eventId: "evt-grain-prices-rose" });

    store.goTo({ kind: "settlement", id: "oakbridge" });

    expect(store.current()).toEqual({ kind: "settlement", id: "oakbridge" });
    expect(store.breadcrumb()).toEqual([{ kind: "world" }, { kind: "settlement", id: "oakbridge" }]);
  });

  it("notifies subscribers on push and back", () => {
    const store = new NavigationStore();
    const listener = vi.fn();
    store.subscribe(listener);

    store.push({ kind: "settlement", id: "oakbridge" });
    expect(listener).toHaveBeenCalledTimes(1);

    store.back();
    expect(listener).toHaveBeenCalledTimes(2);
  });

  it("does not notify subscribers when back() is a no-op at the root", () => {
    const store = new NavigationStore();
    const listener = vi.fn();
    store.subscribe(listener);
    store.back();
    expect(listener).not.toHaveBeenCalled();
  });

  it("stops notifying an unsubscribed listener", () => {
    const store = new NavigationStore();
    const listener = vi.fn();
    const unsubscribe = store.subscribe(listener);
    unsubscribe();
    store.push({ kind: "settlement", id: "oakbridge" });
    expect(listener).not.toHaveBeenCalled();
  });
});

describe("NavigationStore — URL sync (deep-linking)", () => {
  afterEach(() => {
    window.history.replaceState(null, "", "/");
  });

  it("push updates the URL to the serialized route path", () => {
    const store = new NavigationStore();
    store.push({ kind: "agent", id: "mira-valen" });
    expect(window.location.pathname).toBe("/agent/mira-valen");
  });

  it("back() updates the URL back to the previous route's path", () => {
    const store = new NavigationStore();
    store.push({ kind: "settlement", id: "oakbridge" });
    store.push({ kind: "agent", id: "mira-valen" });
    store.back();
    expect(window.location.pathname).toBe("/settlement/oakbridge");
  });

  it("syncWithHistory loads the initial route from a deep-link URL", () => {
    window.history.replaceState(null, "", "/agent/mira-valen");
    const store = new NavigationStore(WORLD_FIXTURE);
    store.syncWithHistory();
    expect(store.current()).toEqual({ kind: "agent", id: "mira-valen" });
    store.stopSyncWithHistory();
  });

  it("syncWithHistory loads a building deep-link (nested id, from any settlement's buildings)", () => {
    window.history.replaceState(null, "", "/building/bld-valen-house");
    const store = new NavigationStore(WORLD_FIXTURE);
    store.syncWithHistory();
    expect(store.current()).toEqual({ kind: "building", id: "bld-valen-house" });
    store.stopSyncWithHistory();
  });

  it("syncWithHistory redirects to World View when the deep-link id doesn't exist in the fixture", () => {
    window.history.replaceState(null, "", "/agent/does-not-exist");
    const store = new NavigationStore(WORLD_FIXTURE);
    store.syncWithHistory();
    expect(store.current()).toEqual({ kind: "world" });
    expect(window.location.pathname).toBe("/");
    store.stopSyncWithHistory();
  });

  it("popstate (browser back) syncs the internal stack from the URL without duplicating the entry", () => {
    window.history.replaceState(null, "", "/");
    const store = new NavigationStore(WORLD_FIXTURE);
    store.syncWithHistory();
    store.push({ kind: "settlement", id: "oakbridge" });
    expect(store.breadcrumb()).toHaveLength(2);

    // Simula o browser voltando pra "/" sem passar por store.back() — location muda primeiro
    // (como o browser faria ao consumir a entrada de history), depois o evento popstate dispara.
    window.history.pushState(null, "", "/");
    window.dispatchEvent(new PopStateEvent("popstate"));

    expect(store.current()).toEqual({ kind: "world" });
    expect(store.breadcrumb()).toEqual([{ kind: "world" }]);
    store.stopSyncWithHistory();
  });

  it("routeToPath/pathToRoute round-trip for every Route kind", () => {
    const routes: Route[] = [
      { kind: "world" },
      { kind: "settlement", id: "oakbridge" },
      { kind: "building", id: "bld-valen-house" },
      { kind: "household", id: "valen-household" },
      { kind: "agent", id: "mira-valen" },
      { kind: "causal", eventId: "evt-grain-prices-rose" },
      { kind: "timeline", scope: { type: "world" } },
      { kind: "timeline", scope: { type: "agent", id: "mira-valen" } },
      { kind: "life", agentId: "mira-valen" },
      { kind: "feed" },
      { kind: "threads" },
      { kind: "thread", id: "oakbridge-food-crisis" },
    ];
    for (const route of routes) {
      expect(pathToRoute(routeToPath(route), WORLD_FIXTURE)).toEqual(route);
    }
  });
});
