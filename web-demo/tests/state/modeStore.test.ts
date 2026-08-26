import { describe, expect, it, vi } from "vitest";
import { ModeStore } from "../../src/state/modeStore";

describe("ModeStore", () => {
  it("starts in experience mode", () => {
    const store = new ModeStore();
    expect(store.currentMode()).toBe("experience");
  });

  it("toggleMode switches to debug", () => {
    const store = new ModeStore();
    store.toggleMode();
    expect(store.currentMode()).toBe("debug");
  });

  it("toggleMode switches back to experience", () => {
    const store = new ModeStore();
    store.toggleMode();
    store.toggleMode();
    expect(store.currentMode()).toBe("experience");
  });

  it("notifies subscribers on toggle", () => {
    const store = new ModeStore();
    const listener = vi.fn();
    store.subscribe(listener);
    store.toggleMode();
    expect(listener).toHaveBeenCalledTimes(1);
  });
});
