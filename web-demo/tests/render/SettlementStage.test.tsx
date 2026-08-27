import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import * as PixiMock from "pixi.js";
import { SettlementStage } from "../../src/render/SettlementStage";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";
import { followStore } from "../../src/state/followStore";

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

let setPointerCaptureSpy: ReturnType<typeof vi.fn>;

beforeEach(() => {
  pixiMock.__resetPixiMock();
  // jsdom não implementa setPointerCapture/releasePointerCapture — stub sempre reatribuído
  // (não `vi.spyOn`, o método real não existe pra espiar) só pra poder verificar SE/QUANDO o
  // componente chama, sem lançar "is not a function".
  setPointerCaptureSpy = vi.fn();
  Element.prototype.setPointerCapture = setPointerCaptureSpy;
  Element.prototype.releasePointerCapture = vi.fn();
});

afterEach(() => {
  cleanup();
});

describe("SettlementStage — mounts the Pixi scene graph from the fixture", () => {
  it("creates one building node per settlement building and one sprite per resident agent", async () => {
    render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />,
    );
    await flush();

    const { buildingLayer, agentLayer } = layers();
    expect(buildingLayer.children).toHaveLength(OAKBRIDGE.buildings.length);
    expect(agentLayer.children).toHaveLength(OAKBRIDGE_AGENTS.length);
  });

  it("renders nothing (null) for an unknown settlement id", () => {
    const { container } = render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="does-not-exist" onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />,
    );
    expect(container.firstChild).toBeNull();
  });
});

describe("SettlementStage — clicking things (AD-020: physical interaction, not a page nav)", () => {
  it("tapping a building's roof calls onFocusBuilding with its id when it has an interior", async () => {
    const onFocusBuilding = vi.fn();
    render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={onFocusBuilding} onBackgroundClick={() => {}} />,
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
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={onFocusBuilding} onBackgroundClick={() => {}} />,
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
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={onSelectAgent} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />,
    );
    await flush();

    const { agentLayer } = layers();
    const miraIndex = OAKBRIDGE_AGENTS.findIndex((a) => a.id === "mira-valen");
    agentLayer.children[miraIndex].emit("pointertap", { stopPropagation: () => {} });

    expect(onSelectAgent).toHaveBeenCalledWith("mira-valen");
  });

  it("tapping empty terrain calls onBackgroundClick (AD-021: click away returns to the settlement)", async () => {
    const onBackgroundClick = vi.fn();
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
        onBackgroundClick={onBackgroundClick}
      />,
    );
    await flush();

    const app = pixiMock.__lastApplication();
    const worldRoot = app.stage.children[0] as unknown as FakeNode;
    const [terrainLayer] = worldRoot.children as unknown as FakeNode[];
    terrainLayer.emit("pointertap");

    expect(onBackgroundClick).toHaveBeenCalled();
  });

  it("also calls onBackgroundClick from empty terrain while a building is focused (AD-023 revert: 'click outside the house' is the explicit reverse of entering it, symmetric with the Street button)", async () => {
    const onBackgroundClick = vi.fn();
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-corvin-bakery"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
        onBackgroundClick={onBackgroundClick}
      />,
    );
    await flush();

    const app = pixiMock.__lastApplication();
    const worldRoot = app.stage.children[0] as unknown as FakeNode;
    const [terrainLayer] = worldRoot.children as unknown as FakeNode[];
    terrainLayer.emit("pointertap");

    expect(onBackgroundClick).toHaveBeenCalled();
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
        onBackgroundClick={() => {}}
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
        onBackgroundClick={() => {}}
      />,
    );
    await flush();

    const { worldRoot } = layers();
    act(() => pixiMock.__runTick());

    // Câmera focada tem zoom > 1 (FOCUS_ZOOM) — o worldRoot deve escalar de acordo.
    expect(worldRoot.scale.x).toBeGreaterThan(1);
  });

  it("returns the camera to the settlement's own overview zoom when focus clears (clicking street-view)", async () => {
    // AD-022: prédios espalhados podem exigir um overview zoom < 1 (fitZoom) pra caber tudo na
    // viewport — não é sempre exatamente 1. Captura o overview de verdade antes de focar, em vez
    // de assumir um valor fixo, pra continuar correto se o layout do fixture mudar de novo.
    const { rerender } = render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />,
    );
    await flush();
    act(() => pixiMock.__runTick());
    const { worldRoot } = layers();
    const overviewZoom = worldRoot.scale.x;

    rerender(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-corvin-bakery"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
        onBackgroundClick={() => {}}
      />,
    );
    act(() => pixiMock.__runTick());
    expect(worldRoot.scale.x).toBeGreaterThan(overviewZoom);

    rerender(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" focusBuildingId={null} onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />,
    );
    act(() => pixiMock.__runTick());

    expect(worldRoot.scale.x).toBeCloseTo(overviewZoom, 5);
  });

  it("builds room rectangles for the focused building's interior on mount (deep-link safe)", async () => {
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-valen-house"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
        onBackgroundClick={() => {}}
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
        onBackgroundClick={() => {}}
      />,
    );
    await flush();

    const { buildingLayer } = layers();
    const valenIndex = OAKBRIDGE.buildings.findIndex((b) => b.id === "bld-valen-house");
    const [valenRoof] = buildingLayer.children[valenIndex].children;

    for (let i = 0; i < 60; i += 1) act(() => pixiMock.__runTick());

    expect(valenRoof.alpha).toBeCloseTo(1, 1);
  });

});

describe("SettlementStage — camera follow (bug real: 'Follow' não seguia o NPC de verdade)", () => {
  afterEach(() => {
    for (const id of ["rowan", "mira-valen"]) {
      if (followStore.isFollowed(id)) followStore.toggleFollow(id);
    }
  });

  it("locks the camera onto a followed agent's live world position every tick", async () => {
    followStore.toggleFollow("rowan");
    render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />,
    );
    await flush();
    act(() => pixiMock.__runTick());

    const { worldRoot, agentLayer } = layers();
    const rowanIndex = OAKBRIDGE_AGENTS.findIndex((a) => a.id === "rowan");
    const rowanSprite = agentLayer.children[rowanIndex] as unknown as FakeNode;
    const zoom = worldRoot.scale.x;

    expect(worldRoot.position.x).toBeCloseTo(800 / 2 - rowanSprite.position.x * zoom, 5);
    expect(worldRoot.position.y).toBeCloseTo(600 / 2 - rowanSprite.position.y * zoom, 5);
  });

  it("tracks the LAST-activated followed agent, not the first one in fixture order (bug real: dois seguidos ao mesmo tempo travava sempre no primeiro do array)", async () => {
    // mira-valen aparece ANTES de rowan no fixture — seguir mira primeiro e rowan depois deve
    // travar a câmera em rowan (o mais recente), nunca voltar pra mira por causa da ordem do array.
    followStore.toggleFollow("mira-valen");
    followStore.toggleFollow("rowan");
    render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />,
    );
    await flush();
    act(() => pixiMock.__runTick());

    const { worldRoot, agentLayer } = layers();
    const rowanIndex = OAKBRIDGE_AGENTS.findIndex((a) => a.id === "rowan");
    const rowanSprite = agentLayer.children[rowanIndex] as unknown as FakeNode;
    const zoom = worldRoot.scale.x;

    expect(worldRoot.position.x).toBeCloseTo(800 / 2 - rowanSprite.position.x * zoom, 5);
    expect(worldRoot.position.y).toBeCloseTo(600 / 2 - rowanSprite.position.y * zoom, 5);
  });

  it("switching the active follow target with activate() re-tracks the camera without un-following the other one", async () => {
    followStore.toggleFollow("rowan");
    followStore.toggleFollow("mira-valen");
    render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />,
    );
    await flush();

    followStore.activate("rowan"); // como clicar no nome do Rowan na aba "Followed"
    act(() => pixiMock.__runTick());

    const { worldRoot, agentLayer } = layers();
    const rowanIndex = OAKBRIDGE_AGENTS.findIndex((a) => a.id === "rowan");
    const rowanSprite = agentLayer.children[rowanIndex] as unknown as FakeNode;
    const zoom = worldRoot.scale.x;

    expect(worldRoot.position.x).toBeCloseTo(800 / 2 - rowanSprite.position.x * zoom, 5);
    expect(followStore.isFollowed("mira-valen")).toBe(true);
  });

  // Pedido do usuário 2026-08-26: arrastar o mapa pra longe de quem a câmera segue deve
  // "desgrudar" — parar de travar nele — sem des-seguir. Só reata clicando o nome de novo
  // (`activate`) ou seguindo outro agent (`toggleFollow`).
  it("dragging the map away from the followed agent detaches the camera lock without un-following", async () => {
    followStore.toggleFollow("rowan");
    const { getByTestId } = render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />,
    );
    await flush();
    act(() => pixiMock.__runTick());

    const stage = getByTestId("settlement-stage").firstElementChild!;
    const dispatch = (type: string, x: number, y: number) => stage.dispatchEvent(new MouseEvent(type, { clientX: x, clientY: y, bubbles: true, cancelable: true }));
    dispatch("pointerdown", 100, 100);
    dispatch("pointermove", 300, 100); // bem além do CLICK_DRAG_THRESHOLD — arrasto de verdade
    dispatch("pointerup", 300, 100);

    act(() => pixiMock.__runTick());

    const { worldRoot, agentLayer } = layers();
    const rowanIndex = OAKBRIDGE_AGENTS.findIndex((a) => a.id === "rowan");
    const rowanSprite = agentLayer.children[rowanIndex] as unknown as FakeNode;
    const zoom = worldRoot.scale.x;

    expect(followStore.activeFollowId()).toBeNull();
    expect(followStore.isFollowed("rowan")).toBe(true); // continua seguido, só não trava mais
    expect(worldRoot.position.x).not.toBeCloseTo(800 / 2 - rowanSprite.position.x * zoom, 1);
  });

  it("does not move the camera for anyone when no one is followed", async () => {
    render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />,
    );
    await flush();
    act(() => pixiMock.__runTick());
    const { worldRoot } = layers();
    const before = { x: worldRoot.position.x, y: worldRoot.position.y };
    act(() => pixiMock.__runTick());

    expect(worldRoot.position.x).toBeCloseTo(before.x, 5);
    expect(worldRoot.position.y).toBeCloseTo(before.y, 5);
  });
});

describe("SettlementStage — floor selector overlay", () => {
  it("shows the floor selector overlay only for a multi-floor building, and the street-view button while focused", () => {
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-corvin-bakery"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
        onBackgroundClick={() => {}}
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
        onBackgroundClick={() => {}}
      />,
    );
    expect(screen.queryByTestId("floor-selector")).not.toBeInTheDocument();
  });

  it("clicking the street-view button calls onBackgroundClick", () => {
    const onBackgroundClick = vi.fn();
    render(
      <SettlementStage
        fixture={WORLD_FIXTURE}
        settlementId="oakbridge"
        focusBuildingId="bld-valen-house"
        onSelectAgent={() => {}}
        onFocusBuilding={() => {}}
        onBackgroundClick={onBackgroundClick}
      />,
    );
    screen.getByTestId("street-view-button").click();
    expect(onBackgroundClick).toHaveBeenCalled();
  });

  it("shows no overlay at all when nothing is focused", () => {
    render(
      <SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />,
    );
    expect(screen.queryByTestId("settlement-stage-overlay")).not.toBeInTheDocument();
  });
});

// Bug real achado ao vivo (não pego por nenhum teste anterior — eles disparam "pointertap"
// direto nos objetos Pixi mockados, nunca passam por PointerEvent nativo de verdade): capturar
// o pointer já no pointerdown redireciona o TARGET dos eventos seguintes pro container, então o
// listener do Pixi (que escuta direto no <canvas>) nunca via pointerup — clique em prédio/agent
// nunca disparava num mouse real, só em dispatch sintético direto no canvas (que não passa pela
// mesma redireção de capture do browser). Fix: só captura depois de confirmar arrasto de verdade.
describe("SettlementStage — pan vs. click (bug real: capture cedo demais quebrava clique)", () => {
  // `fireEvent.pointerDown/Move/Up` produz um `Event` genérico neste jsdom (sem `PointerEvent`
  // de verdade) — `clientX`/`clientY` saem `undefined`, o que mascararia o próprio bug que este
  // teste existe pra travar. `MouseEvent` com um `type` customizado carrega clientX/clientY de
  // verdade e o listener (que escuta pelo nome do tipo, não pela classe) ainda casa certinho.
  function dispatchPointer(target: Element, type: string, x: number, y: number) {
    target.dispatchEvent(new MouseEvent(type, { clientX: x, clientY: y, bubbles: true, cancelable: true }));
  }
  function firePointerSequence(target: Element, points: { x: number; y: number }[]) {
    dispatchPointer(target, "pointerdown", points[0].x, points[0].y);
    for (const point of points.slice(1)) {
      dispatchPointer(target, "pointermove", point.x, point.y);
    }
    const last = points[points.length - 1];
    dispatchPointer(target, "pointerup", last.x, last.y);
  }

  it("never captures the pointer for a plain click (movement under the drag threshold)", async () => {
    render(<SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />);
    await flush();

    // Os listeners de pointer ficam no nó interno exclusivo do Pixi (irmão do overlay, não mais
    // o wrapper com o testid) — ver comentário em SettlementStage.tsx sobre por que o canvas não
    // pode dividir nó com filhos renderizados pelo React.
    const stage = screen.getByTestId("settlement-stage").firstElementChild!;
    firePointerSequence(stage, [{ x: 100, y: 100 }, { x: 102, y: 101 }, { x: 100, y: 100 }]);

    expect(setPointerCaptureSpy).not.toHaveBeenCalled();
  });

  it("captures the pointer once movement crosses the drag threshold (real panning)", async () => {
    render(<SettlementStage fixture={WORLD_FIXTURE} settlementId="oakbridge" onSelectAgent={() => {}} onFocusBuilding={() => {}} onBackgroundClick={() => {}} />);
    await flush();

    const stage = screen.getByTestId("settlement-stage").firstElementChild!;
    firePointerSequence(stage, [{ x: 100, y: 100 }, { x: 160, y: 100 }]);

    expect(setPointerCaptureSpy).toHaveBeenCalledTimes(1);
  });
});
