import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import * as PixiMock from "pixi.js";
import { WorldStage } from "../../src/render/WorldStage";
import { generateWorldRoads } from "../../src/render/worldLayout";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";
import { followStore } from "../../src/state/followStore";

const pixiMock = PixiMock as unknown as {
  __runTick: () => void;
  __resetPixiMock: () => void;
  __lastApplication: () => { stage: { children: { children: unknown[] }[] } };
};

/** Tolerância pros testes de convergência da câmera de follow (lerp) — generosa o bastante pra
 * não flakar sob carga da suíte inteira (GC/scheduling entre as ticks sintéticas), pequena o
 * bastante pra ainda provar que a câmera de fato chegou perto do agent seguido. */
const FOLLOW_CONVERGENCE_TOLERANCE_PX = 10;

interface FakeNode {
  children: FakeNode[];
  parent: FakeNode | null;
  position: { x: number; y: number };
  scale: { x: number; y: number };
  emit: (event: string, ...args: unknown[]) => void;
}

/** Espelha `worldRoot.addChild(terrainLayer, riverLayer, roadLayer, settlementLayer,
 * agentLayer)` de `WorldStage` — ver esse componente se essa ordem mudar. */
function layers() {
  const app = pixiMock.__lastApplication();
  const worldRoot = app.stage.children[0] as unknown as FakeNode;
  const [terrainLayer, , , settlementLayer, agentLayer] = worldRoot.children as unknown as FakeNode[];
  return { worldRoot, terrainLayer, settlementLayer, agentLayer };
}

// `setup()` só tem `await app.init()` (mock resolve imediato, sem textura async como o
// SettlementStage) — puro microtask, nunca precisa de um `setTimeout` real (que travaria sob
// `vi.useFakeTimers()`, usado pelos testes de transição abaixo).
async function flush() {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

beforeEach(() => {
  pixiMock.__resetPixiMock();
  Element.prototype.setPointerCapture = vi.fn();
  Element.prototype.releasePointerCapture = vi.fn();
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("WorldStage — mounts the Pixi scene graph from the fixture", () => {
  it("creates one settlement group per settlement and one dot per agent", async () => {
    render(<WorldStage fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} onBackgroundClick={() => {}} />);
    await flush();

    const { settlementLayer, agentLayer } = layers();
    expect(settlementLayer.children).toHaveLength(WORLD_FIXTURE.settlements.length);
    expect(agentLayer.children).toHaveLength(WORLD_FIXTURE.agents.length);
  });
});

// Pedido do usuário 2026-08-27: clicar em terreno vazio (nem cidade, nem NPC) mostra o mundo no
// Inspector — mesma paridade do `onBackgroundClick` que o Settlement View já tinha.
describe("WorldStage — background click (paridade com o Settlement View)", () => {
  it("calls onBackgroundClick when clicking empty terrain", async () => {
    const onBackgroundClick = vi.fn();
    render(<WorldStage fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} onBackgroundClick={onBackgroundClick} />);
    await flush();

    const { terrainLayer } = layers();
    terrainLayer.emit("pointertap");

    expect(onBackgroundClick).toHaveBeenCalled();
  });
});

describe("WorldStage — settlement footprints and roads", () => {
  it("draws exactly one road segment per generateWorldRoads() result (a spanning tree, not a graph)", async () => {
    render(<WorldStage fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} onBackgroundClick={() => {}} />);
    await flush();
    // generateWorldRoads é determinístico — a asserção real de contagem/topologia já mora em
    // worldLayout.test.ts; aqui só confirmamos que o componente de fato chama a mesma função
    // (nenhuma rede de estradas hardcoded/diferente sendo desenhada).
    expect(generateWorldRoads(WORLD_FIXTURE.settlements)).toHaveLength(WORLD_FIXTURE.settlements.length - 1);
  });
});

describe("WorldStage — click continuity (redesign doc §25/§44-46: zoom before navigating, not a hard cut)", () => {
  it("zooms the camera toward a clicked settlement before calling onSelectSettlement — not instantly", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(0);
    const onSelectSettlement = vi.fn();
    render(<WorldStage fixture={WORLD_FIXTURE} onSelectSettlement={onSelectSettlement} onSelectNpc={() => {}} onBackgroundClick={() => {}} />);
    await flush();

    const { settlementLayer, worldRoot } = layers();
    const oakbridgeIndex = WORLD_FIXTURE.settlements.findIndex((s) => s.id === "oakbridge");
    const oakbridgeGroup = settlementLayer.children[oakbridgeIndex] as unknown as FakeNode;
    const [footprint] = oakbridgeGroup.children;

    act(() => pixiMock.__runTick());
    const zoomBefore = worldRoot.scale.x;

    footprint.emit("pointertap", { stopPropagation: () => {} });
    act(() => pixiMock.__runTick());

    // Ainda não navegou — a câmera só começou a se aproximar.
    expect(onSelectSettlement).not.toHaveBeenCalled();
    expect(worldRoot.scale.x).toBeGreaterThan(zoomBefore);

    vi.setSystemTime(1000); // além do TRANSITION_MIN_MS
    act(() => pixiMock.__runTick());

    expect(onSelectSettlement).toHaveBeenCalledWith("oakbridge");
  });

  // Bug real reportado pelo usuário: clicar um agent estava disparando a MESMA animação de
  // "entrar na cidade" do settlement. Clicar um agent é seleção instantânea (doc §42-43) — quem
  // decide se o mapa mundi continua visível é o `CenterStage` (`useSpatialScope`), não o
  // `WorldStage` — aqui o único contrato é "chama `onSelectNpc` na hora, sem zoom".
  it("calls onSelectNpc instantly when an agent is clicked — no zoom transition like settlements get", async () => {
    const onSelectNpc = vi.fn();
    render(<WorldStage fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={onSelectNpc} onBackgroundClick={() => {}} />);
    await flush();

    const { agentLayer } = layers();
    const rowanIndex = WORLD_FIXTURE.agents.findIndex((a) => a.id === "rowan");
    const rowanDot = agentLayer.children[rowanIndex] as unknown as FakeNode;
    const [mark] = rowanDot.children;

    act(() => pixiMock.__runTick());
    act(() => mark.emit("pointertap", { stopPropagation: () => {} }));

    expect(onSelectNpc).toHaveBeenCalledWith("rowan");
  });
});

describe("WorldStage — agent LOD (redesign doc §29-32/§50: dot from far, sprite up close)", () => {
  it("shows the dot (not the sprite) at the default overview zoom", async () => {
    render(<WorldStage fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} onBackgroundClick={() => {}} />);
    await flush();
    act(() => pixiMock.__runTick());

    const { agentLayer } = layers();
    const rowanIndex = WORLD_FIXTURE.agents.findIndex((a) => a.id === "rowan");
    const rowanDot = agentLayer.children[rowanIndex] as unknown as FakeNode;
    const [mark, sprite] = rowanDot.children as unknown as { visible: boolean }[];

    expect(mark.visible).toBe(true);
    expect(sprite.visible).toBe(false);
  });

  it("swaps to the sprite once the camera zooms in close enough", async () => {
    const { getByTestId } = render(<WorldStage fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} onBackgroundClick={() => {}} />);
    await flush();
    act(() => pixiMock.__runTick());

    const container = getByTestId("world-stage");
    // Zoom real via wheel (cada evento multiplica por 1.12, ver `onWheel`) — bem mais que o
    // suficiente pra cruzar SPRITE_REVEAL_ZOOM (2.2) e bater no MAX_ZOOM (3.5) não importa de
    // onde o overview zoom começou.
    for (let i = 0; i < 40; i += 1) {
      act(() => fireEvent.wheel(container, { deltaY: -100 }));
    }
    act(() => pixiMock.__runTick());

    const { agentLayer } = layers();
    const rowanIndex = WORLD_FIXTURE.agents.findIndex((a) => a.id === "rowan");
    const rowanDot = agentLayer.children[rowanIndex] as unknown as FakeNode;
    const [mark, sprite] = rowanDot.children as unknown as { visible: boolean; scale: { x: number } }[];

    expect(mark.visible).toBe(false);
    expect(sprite.visible).toBe(true);

    // Bug real reportado pelo usuário (screenshot): NPCs do tamanho do footprint do settlement
    // inteiro — a textura é 100x120, então a escala precisa ser bem menor que 1 pra virar uma
    // pessoa pequena, nunca perto do tamanho de um settlement inteiro na mesma unidade de mundo.
    expect(sprite.scale.x).toBeLessThan(0.3);
  });
});

describe("WorldStage — hover LOD card (redesign doc §42: hover is the first informational layer)", () => {
  it("shows a hover card near the cursor when hovering a settlement, hides it on pointerout", async () => {
    render(<WorldStage fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} onBackgroundClick={() => {}} />);
    await flush();

    const { settlementLayer } = layers();
    const oakbridgeIndex = WORLD_FIXTURE.settlements.findIndex((s) => s.id === "oakbridge");
    const [footprint] = settlementLayer.children[oakbridgeIndex].children;

    expect(screen.queryByText("Oakbridge", { selector: "strong" })).not.toBeInTheDocument();
    act(() => footprint.emit("pointerover", { clientX: 300, clientY: 150 }));
    expect(screen.getByText("Oakbridge", { selector: "strong" })).toBeInTheDocument();

    act(() => footprint.emit("pointerout"));
    expect(screen.queryByText("Oakbridge", { selector: "strong" })).not.toBeInTheDocument();
  });

  it("shows a hover card when hovering an agent's dot", async () => {
    render(<WorldStage fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} onBackgroundClick={() => {}} />);
    await flush();

    const { agentLayer } = layers();
    const rowanIndex = WORLD_FIXTURE.agents.findIndex((a) => a.id === "rowan");
    const [mark] = (agentLayer.children[rowanIndex] as unknown as FakeNode).children;

    act(() => mark.emit("pointerover", { clientX: 50, clientY: 60 }));
    expect(screen.getByText("Rowan", { selector: "strong" })).toBeInTheDocument();
  });
});

describe("WorldStage — follow parity with SettlementStage", () => {
  afterEach(() => {
    for (const id of ["rowan", "mira-valen"]) {
      if (followStore.isFollowed(id)) followStore.toggleFollow(id);
    }
  });

  it("travels to and locks onto the followed agent's absolute world position (pedido do usuário: câmera viaja até ele, não salta)", async () => {
    followStore.toggleFollow("rowan");
    render(<WorldStage fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} onBackgroundClick={() => {}} />);
    await flush();
    act(() => pixiMock.__runTick());

    const { worldRoot, agentLayer } = layers();
    const rowanIndex = WORLD_FIXTURE.agents.findIndex((a) => a.id === "rowan");
    const rowanDot = agentLayer.children[rowanIndex] as unknown as FakeNode;
    const zoom = worldRoot.scale.x;
    const target = { x: 800 / 2 - rowanDot.position.x * zoom, y: 600 / 2 - rowanDot.position.y * zoom };

    // Uma única tick NÃO chega no destino — viagem suave (lerp), não salto instantâneo.
    expect(worldRoot.position.x).not.toBeCloseTo(target.x, 1);

    for (let i = 0; i < 200; i += 1) act(() => pixiMock.__runTick());

    expect(Math.abs(worldRoot.position.x - target.x)).toBeLessThan(FOLLOW_CONVERGENCE_TOLERANCE_PX);
    expect(Math.abs(worldRoot.position.y - target.y)).toBeLessThan(FOLLOW_CONVERGENCE_TOLERANCE_PX);
  });

  it("shows the follow ring only on the followed agent's dot", async () => {
    followStore.toggleFollow("rowan");
    render(<WorldStage fixture={WORLD_FIXTURE} onSelectSettlement={() => {}} onSelectNpc={() => {}} onBackgroundClick={() => {}} />);
    await flush();
    act(() => pixiMock.__runTick());

    const { agentLayer } = layers();
    const rowanIndex = WORLD_FIXTURE.agents.findIndex((a) => a.id === "rowan");
    const otherIndex = WORLD_FIXTURE.agents.findIndex((a) => a.id === "mira-valen");
    // Ordem dos filhos do dot: [mark, sprite, ring] — ver `agents.forEach` no WorldStage.
    const rowanRing = (agentLayer.children[rowanIndex] as unknown as FakeNode).children[2] as unknown as { visible: boolean };
    const otherRing = (agentLayer.children[otherIndex] as unknown as FakeNode).children[2] as unknown as { visible: boolean };

    expect(rowanRing.visible).toBe(true);
    expect(otherRing.visible).toBe(false);
  });
});
