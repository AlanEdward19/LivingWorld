import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { RealSnapshotSource } from "../../../src/data/real/snapshotSource";
import { VisualScopeKind, ViewerMode } from "../../../src/types";

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } });
}

describe("RealSnapshotSource", () => {
  let fetchSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchSpy = vi.fn();
    vi.stubGlobal("fetch", fetchSpy);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("GETs /visual/subscribe for the World space, always as Spectator", async () => {
    fetchSpy.mockResolvedValue(
      jsonResponse({
        scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
        mode: ViewerMode.Spectator,
        cursor: { tick: 0, scopeKey: "world", sequence: 0 },
        activeLayers: [],
        payload: { width: 10, height: 10, cities: [], externalNpcs: [], activeEvents: [], layers: {}, portals: [] },
      }),
    );

    const source = new RealSnapshotSource();
    const envelope = await source.load({ kind: "World" });

    const requestedUrl = fetchSpy.mock.calls[0][0] as string;
    expect(requestedUrl).toContain("/visual/subscribe?");
    expect(requestedUrl).toContain("scope=World");
    expect(requestedUrl).toContain(`mode=${ViewerMode.Spectator}`);
    expect(requestedUrl).not.toContain("refId=");
    expect(envelope.scope.scopeKey).toBe("world");
  });

  it("GETs /visual/subscribe for a City space with the cityId as refId", async () => {
    fetchSpy.mockResolvedValue(
      jsonResponse({
        scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
        mode: ViewerMode.Spectator,
        cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
        activeLayers: [],
        payload: null,
      }),
    );

    const source = new RealSnapshotSource();
    await source.load({ kind: "City", cityId: "city-a" });

    const requestedUrl = fetchSpy.mock.calls[0][0] as string;
    expect(requestedUrl).toContain("scope=City");
    expect(requestedUrl).toContain("refId=city-a");
  });

  it("propagates a non-ok response as a rejection", async () => {
    fetchSpy.mockResolvedValue(new Response(null, { status: 403 }));

    const source = new RealSnapshotSource();
    await expect(source.load({ kind: "World" })).rejects.toThrow();
  });
});
