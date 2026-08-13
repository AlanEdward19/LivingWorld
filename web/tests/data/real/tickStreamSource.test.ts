import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { RealTickStreamSource } from "../../../src/data/real/tickStreamSource";

class FakeWebSocket {
  static instances: FakeWebSocket[] = [];
  url: string;
  onmessage: ((event: { data: string }) => void) | null = null;
  onclose: (() => void) | null = null;
  onerror: (() => void) | null = null;
  closed = false;

  constructor(url: string) {
    this.url = url;
    FakeWebSocket.instances.push(this);
  }

  close(): void {
    this.closed = true;
  }
}

describe("RealTickStreamSource", () => {
  beforeEach(() => {
    FakeWebSocket.instances = [];
    vi.stubGlobal("WebSocket", FakeWebSocket as unknown as typeof WebSocket);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("opens a WebSocket at /visual/ws for the given space, Spectator mode", () => {
    const source = new RealTickStreamSource();
    source.subscribe({ kind: "City", cityId: "city-a" }, () => {});

    expect(FakeWebSocket.instances).toHaveLength(1);
    const url = FakeWebSocket.instances[0].url;
    expect(url).toContain("/visual/ws?");
    expect(url).toContain("scope=City");
    expect(url).toContain("refId=city-a");
  });

  it("unwraps a delta envelope and calls onDelta with only its payload", () => {
    const onDelta = vi.fn();
    const source = new RealTickStreamSource();
    source.subscribe({ kind: "World" }, onDelta);

    const socket = FakeWebSocket.instances[0];
    const delta = { tick: 5, moved: [{ npcId: 1, location: { x: 2, y: 3 } }], removed: [] };
    socket.onmessage?.({
      data: JSON.stringify({
        scope: { kind: 0, refId: "", scopeKey: "world" },
        fromCursor: { tick: 4, scopeKey: "world", sequence: 0 },
        toCursor: { tick: 5, scopeKey: "world", sequence: 1 },
        payload: delta,
      }),
    });

    expect(onDelta).toHaveBeenCalledWith(delta);
  });

  it("ignores the initial full-snapshot message (no toCursor)", () => {
    const onDelta = vi.fn();
    const source = new RealTickStreamSource();
    source.subscribe({ kind: "World" }, onDelta);

    FakeWebSocket.instances[0].onmessage?.({
      data: JSON.stringify({
        scope: { kind: 0, refId: "", scopeKey: "world" },
        mode: 0,
        cursor: { tick: 0, scopeKey: "world", sequence: 0 },
        activeLayers: [],
        payload: { width: 1, height: 1, cities: [], externalNpcs: [], activeEvents: [], layers: {}, portals: [] },
      }),
    });

    expect(onDelta).not.toHaveBeenCalled();
  });

  it("calls onDrop when the socket closes", () => {
    const onDrop = vi.fn();
    const source = new RealTickStreamSource();
    source.subscribe({ kind: "World" }, () => {}, onDrop);

    FakeWebSocket.instances[0].onclose?.();

    expect(onDrop).toHaveBeenCalledTimes(1);
  });

  it("unsubscribe closes the socket without triggering onDrop", () => {
    const onDrop = vi.fn();
    const source = new RealTickStreamSource();
    const unsubscribe = source.subscribe({ kind: "World" }, () => {}, onDrop);

    unsubscribe();

    expect(FakeWebSocket.instances[0].closed).toBe(true);
    expect(onDrop).not.toHaveBeenCalled();
  });
});
