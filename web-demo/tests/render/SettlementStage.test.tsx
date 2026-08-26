import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import * as PixiMock from "pixi.js";
import { SettlementStage } from "../../src/render/SettlementStage";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

const pixiMock = PixiMock as unknown as {
  __runTick: () => void;
  __resetPixiMock: () => void;
  __lastApplication: () => { stage: { children: { children: unknown[] }[] } };
};

const OAKBRIDGE = WORLD_FIXTURE.settlements.find((s) => s.id === "oakbridge")!;
const OAKBRIDGE_AGENTS = WORLD_FIXTURE.agents.filter((a) => a.settlementId === "oakbridge");

interface FakeNode {
  children: FakeNode[];
  parent: FakeNode | null;
  alpha: number;
  position: { x: number; y: number };
  scale: { x: number; y: number };
  emit: (event: string, ...args: unknown[]) => void;
}

/** Espelha a ordem `worldRoot.addChild(terrainLayer, roadLayer, buildingLayer, agentLayer)`
 * de `SettlementStage` — ver esse componente se essa ordem mudar. */
function layers() {
  const app = pixiMock.__lastApplication();
  const worldRoot = app.stage.children[0] as unknown as FakeNode;
  const [, , buildingLayer, agentLayer] = worldRoot.children as unknown as FakeNode[];
  return { worldRoot, buildingLayer, agentLayer };
}

async function flush() {
  // getNpcTexture encadeia `decode()`-fallback + `.then()` antes das textures resolverem
  // (npcTexture.ts) — mais de um microtask tick. `setTimeout(0)` esvazia a fila inteira de
  // microtasks pendentes de uma vez, sem depender de contar quantos `.then()` tem no meio.
  await act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
  });
}

beforeEach(() => {
  pixiMock.__resetPixiMock();
});

afterEach(() => {
  cleanup();
});

describe("SettlementStage — mounts the Pixi scene graph from the fixture", () => {
  it("creates one building node per settlement building and one sprite per resident agent", async () => {
    render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} />,
    );
    await flush();

    const { buildingLayer, agentLayer } = layers();
    expect(buildingLayer.children).toHaveLength(OAKBRIDGE.buildings.length);
    expect(agentLayer.children).toHaveLength(OAKBRIDGE_AGENTS.length);
  });

  it("renders nothing (null) for an unknown settlement id", () => {
    const { container } = render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="does-not-exist" onSelectAgent={() => {}} onFocusBuilding={() => {}} />,
    );
    expect(container.firstChild).toBeNull();
  });
});

describe("SettlementStage — clicking things (AD-020: physical interaction, not a page nav)", () => {
  it("tapping a building's roof calls onFocusBuilding with its id when it has an interior", async () => {
    const onFocusBuilding = vi.fn();
    render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={onFocusBuilding} />,
    );
    await flush();

    const { buildingLayer } = layers();
    const bakeryIndex = OAKBRIDGE.buildings.findIndex((b) => b.id === "bld-corvin-bakery");
    const bakeryRoot = buildingLayer.children[bakeryIndex];
    const [roof] = bakeryRoot.children;
    roof.emit("pointertap", { stopPropagation: () => {} });

    expect(onFocusBuilding).toHaveBeenCalledWith("bld-corvin-bakery");
  });

  it("tapping a building with no interior modeled (North Farm) does nothing", async () => {
    const onFocusBuilding = vi.fn();
    render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={onFocusBuilding} />,
    );
    await flush();

    const { buildingLayer } = layers();
    const farmIndex = OAKBRIDGE.buildings.findIndex((b) => b.id === "bld-north-farm");
    const [roof] = buildingLayer.children[farmIndex].children;
    roof.emit("pointertap", { stopPropagation: () => {} });

    expect(onFocusBuilding).not.toHaveBeenCalled();
  });

  it("tapping an agent's sprite calls onSelectAgent with its id", async () => {
    const onSelectAgent = vi.fn();
    render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={onSelectAgent} onFocusBuilding={() => {}} />,
    );
    await flush();

    const { agentLayer } = layers();
    const miraIndex = OAKBRIDGE_AGENTS.findIndex((a) => a.id === "mira-valen");
    agentLayer.children[miraIndex].emit("pointertap", { stopPropagation: () => {} });

    expect(onSelectAgent).toHaveBeenCalledWith("mira-valen");
  });
});

describe("SettlementStage — roof cutaway (AD-020: reveal in place, not a route swap)", () => {
  it("fades the focused building's roof out and its interior in over ticks", async () => {
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-corvin-bakery"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
      />,
    );
    await flush();

    const { buildingLayer } = layers();
    const bakeryIndex = OAKBRIDGE.buildings.findIndex((b) => b.id === "bld-corvin-bakery");
    const [roof, interior] = buildingLayer.children[bakeryIndex].children;

    for (let i = 0; i < 60; i += 1) act(() => pixiMock.__runTick());

    expect(roof.alpha).toBeLessThan(0.3);
    expect(interior.alpha).toBeGreaterThan(0.7);
  });

  it("moves the camera onto the focused building on mount — not just a transparent roof, the camera actually approaches (deep-link safe)", async () => {
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-corvin-bakery"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
      />,
    );
    await flush();

    const { worldRoot } = layers();
    act(() => pixiMock.__runTick());

    // Câmera focada tem zoom > 1 (FOCUS_ZOOM) — o worldRoot deve escalar de acordo.
    expect(worldRoot.scale.x).toBeGreaterThan(1);
  });

  it("returns the camera to the settlement overview when focus clears (clicking street-view)", async () => {
    const { rerender } = render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-corvin-bakery"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
      />,
    );
    await flush();
    act(() => pixiMock.__runTick());
    const { worldRoot } = layers();
    expect(worldRoot.scale.x).toBeGreaterThan(1);

    rerender(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" focusBuildingId={null} onSelectAgent={() => {}} onFocusBuilding={() => {}} />,
    );
    act(() => pixiMock.__runTick());

    expect(worldRoot.scale.x).toBeCloseTo(1, 5);
  });

  it("builds room rectangles for the focused building's interior on mount (deep-link safe)", async () => {
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-valen-house"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
      />,
    );
    await flush();

    const { buildingLayer } = layers();
    const valenIndex = OAKBRIDGE.buildings.findIndex((b) => b.id === "bld-valen-house");
    const [, interior] = buildingLayer.children[valenIndex].children;
    const valenHouse = OAKBRIDGE.buildings.find((b) => b.id === "bld-valen-house")!;
    const expectedShapes = valenHouse.floors[0].rooms.length + valenHouse.floors[0].rooms.reduce((sum, r) => sum + r.furniture.length, 0);

    expect(interior.children.length).toBe(expectedShapes);
  });

  it("does not fade an unfocused building's roof away", async () => {
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-corvin-bakery"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
      />,
    );
    await flush();

    const { buildingLayer } = layers();
    const valenIndex = OAKBRIDGE.buildings.findIndex((b) => b.id === "bld-valen-house");
    const [valenRoof] = buildingLayer.children[valenIndex].children;

    for (let i = 0; i < 60; i += 1) act(() => pixiMock.__runTick());

    expect(valenRoof.alpha).toBeCloseTo(1, 1);
  });

  it("shows the floor selector overlay only for a multi-floor building, and the street-view button while focused", () => {
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-corvin-bakery"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
      />,
    );
    expect(screen.getByTestId("street-view-button")).toBeInTheDocument();
    expect(screen.getByTestId("floor-selector")).toBeInTheDocument();
    expect(screen.getByTestId("focused-building-name")).toHaveTextContent("Corvin's Bakery");
  });

  it("does not show a floor selector for a single-floor building", () => {
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-valen-house"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
      />,
    );
    expect(screen.queryByTestId("floor-selector")).not.toBeInTheDocument();
  });

  it("clicking the street-view button calls onFocusBuilding(null)", () => {
    const onFocusBuilding = vi.fn();
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-valen-house"
        onSelectAgent={() => {}}
        onFocusBuilding={onFocusBuilding}
      />,
    );
    screen.getByTestId("street-view-button").click();
    expect(onFocusBuilding).toHaveBeenCalledWith(null);
  });

  it("shows no overlay at all when nothing is focused", () => {
    render(<SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} />);
    expect(screen.queryByTestId("settlement-stage-overlay")).not.toBeInTheDocument();
  });
});
