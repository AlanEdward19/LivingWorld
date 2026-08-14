import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { LivingTimeline } from "../src/components/LivingTimeline";
import { SimulationStore } from "../src/state/simulationStore";
import type { SnapshotSource, TickStreamSource } from "../src/data/sources";

const WORLD = { kind: "World" as const };
const ticks: TickStreamSource = { subscribe: () => () => {} };

function source(events: { tick: number; kind: number; label: string }[] = []): SnapshotSource {
  return { load: async () => ({
    scope: { kind: 0, refId: "", scopeKey: "world" }, mode: 0,
    cursor: { tick: 0, scopeKey: "world", sequence: 0 }, activeLayers: [],
    payload: { livingState: { npcs: [], cities: [], buildings: [], processes: [], indicators: [], events } },
  }) };
}

describe("LivingTimeline", () => {
  it("renders the public label and tick without exposing a technical payload", async () => {
    const store = new SimulationStore(source([{ tick: 12, kind: 1, label: "Um habitante faleceu" }]), ticks);
    await store.observeSpace(WORLD);

    render(<LivingTimeline space={WORLD} simulationStore={store} />);

    expect(screen.getByText("Tick 12")).toBeInTheDocument();
    expect(screen.getByText("Um habitante faleceu")).toBeInTheDocument();
    expect(screen.queryByText(/truth-only|payload/i)).not.toBeInTheDocument();
  });

  it("updates from a typed event delta without polling", async () => {
    const store = new SimulationStore(source(), ticks);
    await store.observeSpace(WORLD);
    render(<LivingTimeline space={WORLD} simulationStore={store} />);

    store.applyDelta({ tick: 13, moved: [], removed: [],
      events: [{ tick: 13, kind: 0, label: "Um novo habitante nasceu" }] });

    expect(await screen.findByText("Um novo habitante nasceu")).toBeInTheDocument();
  });

  it("shows an honest empty state when there are no recent events", async () => {
    const store = new SimulationStore(source(), ticks);
    await store.observeSpace(WORLD);
    render(<LivingTimeline space={WORLD} simulationStore={store} />);

    expect(screen.getByText("Nenhum acontecimento recente.")).toBeInTheDocument();
  });
});
