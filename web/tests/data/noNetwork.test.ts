import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MockClock } from "../../src/data/mock/MockClock";
import { MockSnapshotSource } from "../../src/data/mock/MockSnapshotSource";
import { MockTickStreamSource } from "../../src/data/mock/MockTickStreamSource";
import { MockTimeControlSource } from "../../src/data/mock/MockTimeControlSource";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { npcsByScope, portalFixtures, snapshotsByScope } from "../../src/data/mock/fixtures";

describe("mock sources never touch the network", () => {
  let fetchSpy: ReturnType<typeof vi.fn>;
  let webSocketSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchSpy = vi.fn(() => {
      throw new Error("fetch must never be called by a mock source");
    });
    webSocketSpy = vi.fn(() => {
      throw new Error("WebSocket must never be constructed by a mock source");
    });
    vi.stubGlobal("fetch", fetchSpy);
    vi.stubGlobal("WebSocket", webSocketSpy);
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("exercises every Mock*Source without ever calling fetch or WebSocket", async () => {
    const clock = new MockClock();
    const snapshotSource = new MockSnapshotSource(snapshotsByScope);
    const tickStreamSource = new MockTickStreamSource(clock, npcsByScope, 20);
    const timeControlSource = new MockTimeControlSource(clock);
    const portalSource = new MockPortalSource(portalFixtures);

    await snapshotSource.load({ kind: "World" });
    const unsubscribe = tickStreamSource.subscribe({ kind: "World" }, () => {});
    vi.advanceTimersByTime(1000);
    unsubscribe();
    await timeControlSource.pause();
    await timeControlSource.setSpeed(2);
    await timeControlSource.status();
    portalSource.portalsOf({ kind: "City", cityId: "city-a" });

    expect(fetchSpy).not.toHaveBeenCalled();
    expect(webSocketSpy).not.toHaveBeenCalled();
  });
});
