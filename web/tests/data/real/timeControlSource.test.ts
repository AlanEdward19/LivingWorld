import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { RealTimeControlSource } from "../../../src/data/real/timeControlSource";

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

describe("RealTimeControlSource", () => {
  let fetchSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchSpy = vi.fn().mockResolvedValue(new Response(null, { status: 200 }));
    vi.stubGlobal("fetch", fetchSpy);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("pause() POSTs to /simulation/pause exactly once", async () => {
    await new RealTimeControlSource().pause();

    expect(fetchSpy).toHaveBeenCalledTimes(1);
    expect(fetchSpy).toHaveBeenCalledWith("/simulation/pause", expect.objectContaining({ method: "POST" }));
  });

  it("resume() POSTs to /simulation/resume exactly once", async () => {
    await new RealTimeControlSource().resume();

    expect(fetchSpy).toHaveBeenCalledWith("/simulation/resume", expect.objectContaining({ method: "POST" }));
  });

  it("setSpeed(n) POSTs the ticksPerSecond body to /simulation/speed", async () => {
    await new RealTimeControlSource().setSpeed(4);

    expect(fetchSpy).toHaveBeenCalledWith(
      "/simulation/speed",
      expect.objectContaining({ method: "POST", body: JSON.stringify({ ticksPerSecond: 4 }) }),
    );
  });

  it("step() POSTs to /simulation/step exactly once", async () => {
    await new RealTimeControlSource().step();

    expect(fetchSpy).toHaveBeenCalledWith("/simulation/step", expect.objectContaining({ method: "POST" }));
  });

  it("advanceYear() POSTs to /simulation/advance-year exactly once", async () => {
    await new RealTimeControlSource().advanceYear();

    expect(fetchSpy).toHaveBeenCalledWith("/simulation/advance-year", expect.objectContaining({ method: "POST" }));
  });

  it("a 409 from step() does not throw — the UI is left to reflect the unchanged status", async () => {
    fetchSpy.mockResolvedValueOnce(new Response("Step só é permitido com a simulação pausada.", { status: 409 }));

    await expect(new RealTimeControlSource().step()).resolves.toBeUndefined();
  });

  it("a 400 from setSpeed() does not throw", async () => {
    fetchSpy.mockResolvedValueOnce(new Response("Velocidade deve ser positiva.", { status: 400 }));

    await expect(new RealTimeControlSource().setSpeed(-1)).resolves.toBeUndefined();
  });

  it("status() GETs /simulation/status and maps isPaused/ticksPerSecond", async () => {
    fetchSpy.mockResolvedValueOnce(jsonResponse({ isPaused: true, ticksPerSecond: 2 }));

    const status = await new RealTimeControlSource().status();

    expect(fetchSpy).toHaveBeenCalledWith("/simulation/status");
    expect(status.isPaused).toBe(true);
    expect(status.ticksPerSecond).toBe(2);
  });
});
