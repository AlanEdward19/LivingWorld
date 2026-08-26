import { describe, expect, it, vi } from "vitest";
import { NavigationStore } from "../../src/nav/NavigationStore";

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

  it("maintains a consistent stack across 5+ pushes and backs", () => {
    const store = new NavigationStore();
    store.push({ kind: "settlement", id: "oakbridge" });
    store.push({ kind: "household", id: "valen-household" });
    store.push({ kind: "agent", id: "mira-valen" });
    store.push({ kind: "causal", eventId: "evt-grain-prices-rose" });
    store.push({ kind: "timeline", scope: { type: "agent", id: "mira-valen" } });

    expect(store.breadcrumb()).toEqual([
      { kind: "world" },
      { kind: "settlement", id: "oakbridge" },
      { kind: "household", id: "valen-household" },
      { kind: "agent", id: "mira-valen" },
      { kind: "causal", eventId: "evt-grain-prices-rose" },
      { kind: "timeline", scope: { type: "agent", id: "mira-valen" } },
    ]);

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

  it("breadcrumb() returns the full stack in navigation order", () => {
    const store = new NavigationStore();
    store.push({ kind: "settlement", id: "oakbridge" });
    store.push({ kind: "household", id: "valen-household" });
    expect(store.breadcrumb()).toEqual([
      { kind: "world" },
      { kind: "settlement", id: "oakbridge" },
      { kind: "household", id: "valen-household" },
    ]);
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
