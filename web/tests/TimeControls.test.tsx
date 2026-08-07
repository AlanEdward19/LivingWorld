import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { TimeControls } from "../src/components/TimeControls";
import { MockClock } from "../src/data/mock/MockClock";
import { MockTimeControlSource } from "../src/data/mock/MockTimeControlSource";
import { SimulationStore } from "../src/state/simulationStore";
import { SelectionStore } from "../src/state/selectionStore";
import { MockTickStreamSource } from "../src/data/mock/MockTickStreamSource";
import { VisualScopeKind, ViewerMode } from "../src/types";
import type { SnapshotSource } from "../src/data/sources";

function worldSnapshotSource(): SnapshotSource {
  return {
    load: async () => ({
      scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
      mode: ViewerMode.Spectator,
      cursor: { tick: 0, scopeKey: "world", sequence: 0 },
      activeLayers: [],
      payload: { width: 1, height: 1, cities: [], externalNpcs: [], activeEvents: [], layers: {} },
    }),
  };
}

describe("TimeControls", () => {
  it("each button calls the corresponding TimeControlSource method exactly once", async () => {
    // espião com estado mínimo (isPaused/ticksPerSecond de verdade) — sem isso os botões
    // desabilitados por status() nunca mudam de estado entre os cliques.
    let isPaused = true;
    let ticksPerSecond = 1;
    const source = {
      pause: vi.fn(async () => {
        isPaused = true;
      }),
      resume: vi.fn(async () => {
        isPaused = false;
      }),
      setSpeed: vi.fn(async (tps: number) => {
        ticksPerSecond = tps;
      }),
      step: vi.fn(async () => {}),
      status: vi.fn(async () => ({ isPaused, ticksPerSecond, tick: 0 })),
    };

    render(<TimeControls timeControlSource={source} />);
    await waitFor(() => expect(screen.getByTestId("time-controls-status")).toHaveTextContent("Pausado"));

    fireEvent.click(screen.getByRole("button", { name: "Resume" }));
    await waitFor(() => expect(source.resume).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole("button", { name: "4x" }));
    await waitFor(() => expect(source.setSpeed).toHaveBeenCalledWith(4));
    expect(source.setSpeed).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole("button", { name: "Pause" }));
    await waitFor(() => expect(source.pause).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole("button", { name: "+1 tick" }));
    await waitFor(() => expect(source.step).toHaveBeenCalledTimes(1));
  });

  it("shows the current speed, reflecting status()", async () => {
    const clock = new MockClock();
    clock.setSpeed(4);
    const source = new MockTimeControlSource(clock);

    render(<TimeControls timeControlSource={source} />);

    await waitFor(() => expect(screen.getByTestId("time-controls-status")).toHaveTextContent("4x"));
  });

  it("shows 'Pausado' instead of a speed when paused", async () => {
    const clock = new MockClock();
    clock.pause();
    const source = new MockTimeControlSource(clock);

    render(<TimeControls timeControlSource={source} />);

    await waitFor(() => expect(screen.getByTestId("time-controls-status")).toHaveTextContent("Pausado"));
  });

  it("never constructs fetch", async () => {
    const fetchSpy = vi.fn(() => {
      throw new Error("TimeControls must never call fetch");
    });
    vi.stubGlobal("fetch", fetchSpy);

    const clock = new MockClock();
    const source = new MockTimeControlSource(clock);
    render(<TimeControls timeControlSource={source} />);
    await waitFor(() => screen.getByTestId("time-controls-status"));

    fireEvent.click(screen.getByRole("button", { name: "Pause" }));
    fireEvent.click(screen.getByRole("button", { name: "2x" }));

    expect(fetchSpy).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it("enables '+1 tick' only while paused", async () => {
    const clock = new MockClock();
    const source = new MockTimeControlSource(clock);

    render(<TimeControls timeControlSource={source} />);
    await waitFor(() => expect(screen.getByRole("button", { name: "+1 tick" })).toBeDisabled());

    fireEvent.click(screen.getByRole("button", { name: "Pause" }));

    await waitFor(() => expect(screen.getByRole("button", { name: "+1 tick" })).toBeEnabled());
  });

  it("changing speed neither re-subscribes the tick stream nor clears the current selection", async () => {
    const clock = new MockClock();
    const timeControlSource = new MockTimeControlSource(clock);
    const tickStreamSource = new MockTickStreamSource(clock, { world: [{ npcId: 1, location: { x: 0, y: 0 } }] });
    const subscribeSpy = vi.spyOn(tickStreamSource, "subscribe");
    const simulationStore = new SimulationStore(worldSnapshotSource(), tickStreamSource);
    const selectionStore = new SelectionStore();
    await simulationStore.observeSpace({ kind: "World" });
    expect(subscribeSpy).toHaveBeenCalledTimes(1);
    selectionStore.select({ kind: "npc", id: "1", space: { kind: "World" } });

    render(<TimeControls timeControlSource={timeControlSource} />);
    fireEvent.click(screen.getByRole("button", { name: "8x" }));
    await waitFor(() => expect(screen.getByTestId("time-controls-status")).toHaveTextContent("8x"));

    expect(subscribeSpy).toHaveBeenCalledTimes(1); // ainda 1 — TimeControls não conhece o stream
    expect(selectionStore.current()).toEqual({ kind: "npc", id: "1", space: { kind: "World" } });
  });
});
