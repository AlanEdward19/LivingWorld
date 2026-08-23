import { afterEach, describe, expect, it, vi } from "vitest";
import { draw, type RenderFrame } from "../../src/map-engine/renderer";
import type { AuthoritativeEntity, CameraState } from "../../src/map-engine/types";
import type { LodThresholds } from "../../src/map-engine/lod";
import { SimulationStore } from "../../src/state/simulationStore";
import type { SnapshotSource, TickStreamSource } from "../../src/data/sources";
import { VisualScopeKind, ViewerMode } from "../../src/types";

const THRESHOLDS: LodThresholds = { aggregate: 4, token: 10, detail: 18 };
const WORLD = { kind: "World" as const };

function fakeCtx(canvas: { width: number; height: number }) {
  return {
    canvas,
    fillStyle: "",
    strokeStyle: "",
    lineWidth: 1,
    shadowColor: "",
    shadowBlur: 0,
    font: "",
    textAlign: "left",
    fillRect: vi.fn(),
    strokeRect: vi.fn(),
    beginPath: vi.fn(),
    moveTo: vi.fn(),
    lineTo: vi.fn(),
    closePath: vi.fn(),
    arc: vi.fn(),
    fill: vi.fn(),
    stroke: vi.fn(),
    fillText: vi.fn(),
    drawImage: vi.fn(),
    quadraticCurveTo: vi.fn(),
    setLineDash: vi.fn(),
    save: vi.fn(),
    restore: vi.fn(),
    translate: vi.fn(),
    rotate: vi.fn(),
  } as unknown as CanvasRenderingContext2D & {
    moveTo: ReturnType<typeof vi.fn>;
    lineTo: ReturnType<typeof vi.fn>;
    setLineDash: ReturnType<typeof vi.fn>;
    strokeStyle: string;
  };
}

function npc(overrides: Partial<AuthoritativeEntity> = {}): AuthoritativeEntity {
  return {
    ref: { kind: "npc", id: "1", space: WORLD },
    position: { x: 1, y: 2 },
    size: { w: 1, h: 1 },
    sizeIsDerived: false,
    color: "#abc",
    currentAction: 4,
    ...overrides,
  };
}

function frame(entities: AuthoritativeEntity[], camera: CameraState = { center: { x: 5, y: 5 }, scale: 20 }): RenderFrame {
  return {
    camera,
    cells: { width: 20, height: 20, colorAt: () => "#222" },
    layers: [],
    entities,
    lodThresholds: THRESHOLDS,
  };
}

describe("world-map migration route (LWV-04.7)", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("draws a dashed route from the traveler to the relocation destination", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const ctx = fakeCtx({ width: 400, height: 400 });
    const traveler = npc({ travelDestination: { x: 8, y: 2 } });

    draw(ctx, frame([traveler]));

    expect(ctx.setLineDash).toHaveBeenCalledWith([7, 5]);
    expect(ctx.moveTo).toHaveBeenCalledWith(130, 150);
    expect(ctx.lineTo).toHaveBeenCalledWith(270, 150);
  });

  it("does not draw an emigration route for intra-city Travel without a relocation destination", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const ctx = fakeCtx({ width: 400, height: 400 });
    const dashesBefore = 0;
    draw(ctx, frame([npc({ currentAction: 4 })]));
    const dashCalls = ctx.setLineDash.mock.calls.filter((call) => Array.isArray(call[0]) && call[0].length > 0);
    expect(dashCalls.length).toBe(dashesBefore);
  });

  it("maps living-state relocation fields onto world-map NPC entities", async () => {
    const source: SnapshotSource = {
      load: async () => ({
        scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
        mode: ViewerMode.Spectator,
        cursor: { tick: 0, scopeKey: "world", sequence: 0 },
        activeLayers: [],
        payload: {
          livingState: {
            npcs: [{
              id: { value: 11 },
              location: { x: 1, y: 0 },
              currentAction: 4,
              city: { value: "origin-city" },
              relocationDestination: { x: 8, y: 0 },
            }],
            cities: [],
            buildings: [],
            processes: [],
            indicators: [],
            events: [],
          },
        },
      }),
    };
    const ticks: TickStreamSource = { subscribe: () => () => {} };
    const store = new SimulationStore(source, ticks);
    await store.observeSpace(WORLD);

    const entity = store.entitiesOf(WORLD).find((item) => item.ref.id === "11");
    expect(entity?.travelDestination).toEqual({ x: 8, y: 0 });
    expect(entity?.cityId).toBe("origin-city");
    expect(entity?.currentAction).toBe(4);
  });

  it("applies destination city membership from an npc upsert after arrival", async () => {
    const source: SnapshotSource = {
      load: async () => ({
        scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
        mode: ViewerMode.Spectator,
        cursor: { tick: 0, scopeKey: "world", sequence: 0 },
        activeLayers: [],
        payload: {
          livingState: {
            npcs: [{
              id: { value: 11 },
              location: { x: 1, y: 0 },
              currentAction: 4,
              city: { value: "origin-city" },
              relocationDestination: { x: 8, y: 0 },
            }],
            cities: [],
            buildings: [],
            processes: [],
            indicators: [],
            events: [],
          },
        },
      }),
    };
    const ticks: TickStreamSource = { subscribe: () => () => {} };
    const store = new SimulationStore(source, ticks);
    await store.observeSpace(WORLD);

    store.applyDelta({
      tick: 40,
      moved: [],
      removed: [],
      npcUpserts: [{
        id: { value: 11 },
        location: { x: 8, y: 0 },
        currentAction: 5,
        city: { value: "dest-city" },
        relocationDestination: null,
      }],
    });

    const entity = store.entitiesOf(WORLD).find((item) => item.ref.id === "11");
    expect(entity?.cityId).toBe("dest-city");
    expect(entity?.travelDestination).toBeUndefined();
  });
});
