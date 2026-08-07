import { describe, expect, it, vi, beforeEach } from "vitest";
import { buildSubscribeUrl, buildWebSocketUrl, fetchPeriodCatalog, moveNpc } from "../src/api";
import { ViewerMode } from "../src/types";

describe("buildSubscribeUrl", () => {
  it("builds a world-scope url without refId", () => {
    const url = buildSubscribeUrl({ kind: "World" }, ViewerMode.Spectator);

    expect(url).toContain("/visual/subscribe?");
    expect(url).toContain("scope=World");
    expect(url).toContain("mode=0");
    expect(url).not.toContain("refId=");
  });

  it("builds a city-scope url with the city id as refId", () => {
    const url = buildSubscribeUrl({ kind: "City", cityId: "abc-123" }, ViewerMode.Player, 7);

    expect(url).toContain("scope=City");
    expect(url).toContain("refId=abc-123");
    expect(url).toContain("mode=1");
    expect(url).toContain("playerNpcId=7");
  });

  it("builds an interior-scope url with the building id as refId", () => {
    const url = buildSubscribeUrl(
      { kind: "Interior", buildingId: "42", cityId: "abc-123" },
      ViewerMode.Spectator,
    );

    expect(url).toContain("scope=Interior");
    expect(url).toContain("refId=42");
  });
});

describe("buildWebSocketUrl", () => {
  it("rewrites http to ws and keeps the same query shape as subscribe", () => {
    const url = buildWebSocketUrl({ kind: "World" }, ViewerMode.Spectator);

    expect(url.startsWith("ws")).toBe(true);
    expect(url).toContain("/visual/ws?");
    expect(url).toContain("scope=World");
  });
});

describe("moveNpc", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 200 })));
  });

  it("posts the move intent as json to the player move endpoint", async () => {
    await moveNpc(5, { targetX: 1, targetY: 2, inputMode: "click" });

    expect(fetch).toHaveBeenCalledWith(
      "/visual/player/5/move",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ targetX: 1, targetY: 2, inputMode: "click" }),
      }),
    );
  });
});

describe("fetchPeriodCatalog", () => {
  it("loads the readable profession and skill labels for one period", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ professionNames: { 1: "Ferreiro" }, skillNames: { 2: "Forja" } }), {
          status: 200,
        }),
      ),
    );

    await expect(fetchPeriodCatalog("cidade média")).resolves.toEqual({
      professionNames: { 1: "Ferreiro" },
      skillNames: { 2: "Forja" },
    });
    expect(fetch).toHaveBeenCalledWith("/periods/cidade%20m%C3%A9dia/catalog");
  });
});
