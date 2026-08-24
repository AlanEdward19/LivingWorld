import { afterEach, describe, expect, it, vi } from "vitest";
import { draw, type RenderFrame } from "../../src/map-engine/renderer";
import { tokenRadiusPx } from "../../src/map-engine/tokenSize";
import type { AuthoritativeEntity, CameraState } from "../../src/map-engine/types";
import type { LodThresholds } from "../../src/map-engine/lod";

const THRESHOLDS: LodThresholds = { aggregate: 4, token: 10, detail: 18 };

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
    fillRect: ReturnType<typeof vi.fn>;
    strokeRect: ReturnType<typeof vi.fn>;
    lineTo: ReturnType<typeof vi.fn>;
    arc: ReturnType<typeof vi.fn>;
    setLineDash: ReturnType<typeof vi.fn>;
    fillText: ReturnType<typeof vi.fn>;
    drawImage: ReturnType<typeof vi.fn>;
    rotate: ReturnType<typeof vi.fn>;
  };
}

function npc(
  id: string,
  x: number,
  y: number,
  sizeIsDerived = false,
  space: AuthoritativeEntity["ref"]["space"] = { kind: "World" },
): AuthoritativeEntity {
  return {
    ref: { kind: "npc", id, space },
    position: { x, y },
    size: { w: 1, h: 1 },
    sizeIsDerived,
    color: "#abc",
  };
}

function baseFrame(camera: CameraState, entities: AuthoritativeEntity[] = []): RenderFrame {
  return {
    camera,
    cells: { width: 1000, height: 1000, colorAt: () => "#222" },
    layers: [],
    entities,
    lodThresholds: THRESHOLDS,
  };
}

describe("renderer.draw", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("draws the deterministic SVG pawn at token LOD when its cached image is ready", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const ctx = fakeCtx({ width: 100, height: 100 });

    draw(ctx, baseFrame({ center: { x: 2, y: 2 }, scale: 12 }, [npc("npc-svg-ready", 2, 2)]));

    expect(ctx.drawImage).toHaveBeenCalledOnce();
  });

  it("renders a river as a water tile instead of a tiny point", () => {
    const ctx = fakeCtx({ width: 100, height: 100 });
    const frame = baseFrame({ center: { x: 2, y: 2 }, scale: 10 });
    frame.cells = { width: 5, height: 5, colorAt: () => undefined };
    frame.layers = [{ id: "Rivers", overlayPoints: [{ x: 2, y: 2, color: "#3a7bd5" }] }];

    draw(ctx, frame);

    expect(ctx.fillRect.mock.calls.some(([, , width, height]) => width === 10 && height === 10)).toBe(true);
  });

  it("draws stable cloud puffs when the space enables atmosphere", () => {
    const ctx = fakeCtx({ width: 100, height: 100 });
    const frame = baseFrame({ center: { x: 2, y: 2 }, scale: 10 });
    frame.cells = { width: 5, height: 5, atmosphereSeed: "world", colorAt: () => undefined };

    draw(ctx, frame);

    expect(ctx.arc).toHaveBeenCalledTimes(15);
  });

  it("does not draw cell grid lines when the current space disables its grid", () => {
    const ctx = fakeCtx({ width: 100, height: 100 });
    const frame = baseFrame({ center: { x: 2, y: 2 }, scale: 12 });
    frame.cells = { width: 5, height: 5, showGrid: false, colorAt: () => "#567" };

    draw(ctx, frame);

    expect((ctx.stroke as unknown as ReturnType<typeof vi.fn>)).not.toHaveBeenCalled();
  });

  it("adds distinct top-down details to roof and door materials", () => {
    const ctx = fakeCtx({ width: 100, height: 100 });
    const building: AuthoritativeEntity = {
      ref: { kind: "building", id: "house", space: { kind: "City", cityId: "a" } },
      position: { x: 1, y: 1 },
      size: { w: 2, h: 1 },
      sizeIsDerived: true,
      color: "#765",
      footprintCells: [
        { x: 0, y: 0, color: "#765", material: "roof" },
        { x: 1, y: 0, color: "#432", material: "door" },
      ],
    };

    draw(ctx, baseFrame({ center: { x: 2, y: 2 }, scale: 12 }, [building]));

    expect(ctx.fillRect.mock.calls.length).toBeGreaterThanOrEqual(5);
    expect(ctx.arc).toHaveBeenCalledOnce();
  });

  it("renders agricultural buildings as cultivated rows instead of a house", () => {
    const ctx = fakeCtx({ width: 160, height: 120 });
    const farm: AuthoritativeEntity = {
      ref: { kind: "building", id: "farm", space: { kind: "City", cityId: "a" } },
      position: { x: 1, y: 1 }, size: { w: 4, h: 3 }, sizeIsDerived: false, color: "#765",
      buildingTypeId: 1,
      footprintCells: [{ x: 0, y: 0, color: "#765", material: "roof" }],
    };

    draw(ctx, baseFrame({ center: { x: 3, y: 2.5 }, scale: 12 }, [farm]));

    expect(ctx.lineTo.mock.calls.length).toBeGreaterThanOrEqual(3);
    expect(ctx.arc).not.toHaveBeenCalled();
  });

  it("renders forge buildings with a furnace cue distinct from the generic fallback", () => {
    const forgeCtx = fakeCtx({ width: 160, height: 120 });
    const genericCtx = fakeCtx({ width: 160, height: 120 });
    const building = (id: string, buildingTypeId: number): AuthoritativeEntity => ({
      ref: { kind: "building", id, space: { kind: "City", cityId: "a" } },
      position: { x: 1, y: 1 }, size: { w: 4, h: 3 }, sizeIsDerived: false, color: "#765",
      buildingTypeId,
      footprintCells: [
        { x: 0, y: 0, color: "#765", material: "roof" },
        { x: 1, y: 0, color: "#432", material: "door" },
      ],
    });

    draw(forgeCtx, baseFrame({ center: { x: 3, y: 2.5 }, scale: 12 }, [building("forge", 2)]));
    draw(genericCtx, baseFrame({ center: { x: 3, y: 2.5 }, scale: 12 }, [building("future", 77)]));

    expect(forgeCtx.arc.mock.calls.length).toBeGreaterThan(genericCtx.arc.mock.calls.length);
    expect(genericCtx.fillRect.mock.calls.length).toBeGreaterThan(1);
  });

  it("renders a city as a composed settlement instead of outlining every footprint tile", () => {
    const ctx = fakeCtx({ width: 300, height: 300 });
    const city: AuthoritativeEntity = {
      ref: { kind: "city", id: "city-a", space: { kind: "World" } },
      position: { x: 1, y: 1 }, size: { w: 10, h: 8 }, sizeIsDerived: false, color: "#999",
      footprintCells: Array.from({ length: 80 }, (_, index) => ({
        x: index % 10, y: Math.floor(index / 10), color: "#765", material: "roof" as const,
      })),
    };

    draw(ctx, baseFrame({ center: { x: 6, y: 5 }, scale: 12 }, [city]));

    expect(ctx.strokeRect.mock.calls.length).toBe(0);
    expect(ctx.arc.mock.calls.length).toBe(4);
  });

  it("renders an authoring settlement without the outer wall markers", () => {
    const ctx = fakeCtx({ width: 300, height: 300 });
    const city: AuthoritativeEntity = {
      ref: { kind: "city", id: "draft-city", space: { kind: "World" } },
      position: { x: 1, y: 1 }, size: { w: 4, h: 4 }, sizeIsDerived: false, color: "#999",
      showBoundary: false,
    };

    draw(ctx, baseFrame({ center: { x: 3, y: 3 }, scale: 24 }, [city]));

    expect(ctx.arc).not.toHaveBeenCalled();
    expect(ctx.strokeRect).not.toHaveBeenCalled();
  });

  it("rotates creator architecture around its visual center", () => {
    const ctx = fakeCtx({ width: 300, height: 300 });
    const city: AuthoritativeEntity = {
      ref: { kind: "city", id: "rotated-city", space: { kind: "World" } },
      position: { x: 1, y: 1 }, size: { w: 4, h: 4 }, sizeIsDerived: false, color: "#999",
      showBoundary: false, rotation: 90,
    };

    draw(ctx, baseFrame({ center: { x: 3, y: 3 }, scale: 24 }, [city]));

    expect(ctx.rotate).toHaveBeenCalledWith(Math.PI / 2);
  });

  it("only fills the cells covered by the camera's visible rect, not the whole 1000x1000 grid", () => {
    // scale=10 px/tile, viewport 100x100px -> visible world rect é ~10x10 tiles -> ~100 fillRect
    const camera: CameraState = { center: { x: 500, y: 500 }, scale: 10 };
    const ctx = fakeCtx({ width: 100, height: 100 });

    draw(ctx, baseFrame(camera));

    // 1 fillRect de fundo + no máximo 10*10=100 de célula — nunca perto de 1_000_000
    expect(ctx.fillRect.mock.calls.length).toBeGreaterThan(1);
    expect(ctx.fillRect.mock.calls.length).toBeLessThan(200);
  });

  it("returns early without touching the context when ctx is null (jsdom getContext('2d'))", () => {
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 10 };
    expect(() => draw(null, baseFrame(camera))).not.toThrow();
  });

  it("never reassigns canvas.width/height — sizing belongs to whoever mounts the canvas", () => {
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 10 };
    const canvas = { width: 333, height: 222 };
    let widthWrites = 0;
    Object.defineProperty(canvas, "width", {
      get: () => 333,
      set: () => {
        widthWrites += 1;
      },
    });
    const ctx = fakeCtx(canvas);

    draw(ctx, baseFrame(camera));

    expect(widthWrites).toBe(0);
  });

  it("marks a derived-size entity with a distinct (dashed) stroke, unlike an authored one", () => {
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 12 }; // >= token threshold
    const derived = npc("derived", 5, 5, true);
    const authored = npc("authored", 6, 5, false);
    const ctx = fakeCtx({ width: 400, height: 400 });

    draw(ctx, baseFrame(camera, [derived, authored]));

    const dashCalls = ctx.setLineDash.mock.calls.map((args: unknown[]) => args[0] as number[]);
    expect(dashCalls.some((pattern) => pattern.length > 0)).toBe(true); // o derivado usou tracejado
    expect(dashCalls.some((pattern) => pattern.length === 0)).toBe(true); // o autorado usou traço sólido
  });

  // Feedback do usuário (2026-08-21): o token ficava do mesmo tamanho de tela em qualquer zoom
  // ("dar zoom pra ver de perto não muda nada") — agora o raio de token acompanha `scale`
  // (`tokenRadiusPx`), com piso/teto só pra não sumir ou virar um círculo absurdo.
  it("grows the NPC token's screen size as the user zooms in, within tokenRadiusPx's bounds", () => {
    class PendingImage {
      complete = false;
      naturalWidth = 0;
      src = "";
    }
    vi.stubGlobal("Image", PendingImage);

    const radiusAtScale = (scale: number) => {
      const ctx = fakeCtx({ width: 400, height: 400 });
      draw(ctx, baseFrame({ center: { x: 5.5, y: 5.5 }, scale }, [npc("tiny-person", 5, 5)]));
      return ctx.arc.mock.calls[0]?.[2] as number;
    };

    expect(radiusAtScale(100)).toBeGreaterThan(radiusAtScale(20));
    expect(radiusAtScale(20)).toBe(tokenRadiusPx(20));
  });

  it("renders NPCs progressively larger in city and building spaces without changing world scale", () => {
    class PendingImage {
      complete = false;
      naturalWidth = 0;
      src = "";
    }
    vi.stubGlobal("Image", PendingImage);

    const radiusFor = (entity: AuthoritativeEntity) => {
      const ctx = fakeCtx({ width: 400, height: 400 });
      draw(ctx, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [entity]));
      return ctx.arc.mock.calls[0]?.[2] as number;
    };

    const worldRadius = radiusFor(npc("world-person", 5, 5));
    const cityRadius = radiusFor(npc("city-person", 5, 5, false, { kind: "City", cityId: "city-a" }));
    const buildingRadius = radiusFor(npc("home-person", 5, 5, false, {
      kind: "Building", buildingId: "home-a", cityId: "city-a",
    }));

    expect(worldRadius).toBe(tokenRadiusPx(20));
    expect(cityRadius).toBeGreaterThan(worldRadius);
    expect(buildingRadius).toBeGreaterThan(cityRadius);
  });

  // Feedback do usuário (2026-08-21): texto ilegível no badge -> ícone; e desenhado por cima do
  // pawn já carregado, nunca dentro da imagem cacheada (perf: ver `drawNpcPawn`).
  it("draws an action icon overlay next to a token whose action is known and not hidden", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const ctx = fakeCtx({ width: 400, height: 400 });
    const entity = { ...npc("sleepy", 5, 5), currentAction: 1 };

    draw(ctx, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [entity]));

    // pawn pronto: só o badge de ação desenha arcs (círculo do badge + lua) — >= 2.
    expect(ctx.arc.mock.calls.length).toBeGreaterThanOrEqual(2);
  });

  it("never draws an action icon for Travel (ActionType=4) — walking around isn't worth a badge", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const ctx = fakeCtx({ width: 400, height: 400 });
    const entity = { ...npc("walker", 5, 5), currentAction: 4 };

    draw(ctx, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [entity]));

    expect(ctx.arc).not.toHaveBeenCalled();
  });

  it("applies manifested extraordinary scale and tint to the NPC pawn", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const ctx = fakeCtx({ width: 400, height: 400 });
    const entity = {
      ...npc("manifested", 5, 5),
      extraordinary: {
        powerIds: ["power-a"], isManifested: true, manifestationState: "active",
        scaleMultiplier: 1.5, skinTint: "tint-token", movementTrail: "trail-token",
        needSubstitution: null, senescenceRateMultiplier: 1,
      },
    } satisfies AuthoritativeEntity;

    draw(ctx, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [entity]));

    expect(ctx.drawImage.mock.calls[0]?.[3]).toBe(tokenRadiusPx(20) * 2 * 1.5);
    expect(ctx.arc).toHaveBeenCalledOnce();
  });

  it("draws a manifestation trail only while the manifested NPC is travelling", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const make = (isManifested: boolean, currentAction: number) => ({
      ...npc(`trail-${isManifested}-${currentAction}`, 5, 5), currentAction,
      extraordinary: {
        powerIds: ["power-a"], isManifested, manifestationState: "active",
        scaleMultiplier: 1, skinTint: "tint-token", movementTrail: "trail-token",
        needSubstitution: null, senescenceRateMultiplier: 1,
      },
    } satisfies AuthoritativeEntity);
    const moving = fakeCtx({ width: 400, height: 400 });
    const idle = fakeCtx({ width: 400, height: 400 });
    const hidden = fakeCtx({ width: 400, height: 400 });

    draw(moving, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [make(true, 4)]));
    draw(idle, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [make(true, 5)]));
    draw(hidden, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [make(false, 4)]));

    expect(moving.lineTo.mock.calls.length).toBeGreaterThan(idle.lineTo.mock.calls.length);
    expect(hidden.lineTo.mock.calls.length).toBe(idle.lineTo.mock.calls.length);
  });

  it("keeps a non-manifested carrier visually identical to an ordinary NPC", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const ordinary = fakeCtx({ width: 400, height: 400 });
    const hidden = fakeCtx({ width: 400, height: 400 });
    const hiddenCarrier = {
      ...npc("hidden-carrier", 5, 5),
      extraordinary: {
        powerIds: ["power-a"], isManifested: false, manifestationState: "dormant",
        scaleMultiplier: 2, skinTint: "tint-token", movementTrail: "trail-token",
        needSubstitution: null, senescenceRateMultiplier: 0,
      },
    } satisfies AuthoritativeEntity;

    draw(ordinary, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [npc("ordinary", 5, 5)]));
    draw(hidden, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [hiddenCarrier]));

    expect(hidden.drawImage.mock.calls[0]?.slice(1)).toEqual(ordinary.drawImage.mock.calls[0]?.slice(1));
    expect(hidden.arc).not.toHaveBeenCalled();
    expect(hidden.lineTo.mock.calls.length).toBe(ordinary.lineTo.mock.calls.length);
  });

  it("culls entities outside the visible rect from drawing", () => {
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 10 };
    const inView = npc("in", 5, 5);
    const farAway = npc("far", 900, 900);
    const ctx = fakeCtx({ width: 100, height: 100 });

    draw(ctx, baseFrame(camera, [inView, farAway]));

    // token (scale 10 >= threshold): disco + glifo (cabeça + ombros) da entidade visível = 3
    // arcs; a distante é cullada antes de chegar em drawPointEntity.
    expect(ctx.arc.mock.calls.length).toBe(3);
  });

  it("aggregates entities into clusters below the aggregate threshold instead of drawing each one", () => {
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 2 }; // < THRESHOLDS.aggregate (4)
    const entities = [npc("a", 5, 5), npc("b", 5, 5), npc("c", 6, 5)];
    const ctx = fakeCtx({ width: 100, height: 100 });

    draw(ctx, baseFrame(camera, entities));

    // 3 entidades agregadas em no máximo 2 buckets -> bem menos arcs que 3 desenhos individuais
    expect(ctx.arc.mock.calls.length).toBeLessThan(entities.length);
    expect(ctx.arc.mock.calls.length).toBeGreaterThan(0);
  });

  it("fans out household-mates sharing the exact same cell instead of drawing them stacked", () => {
    class PendingImage {
      complete = false;
      naturalWidth = 0;
      src = "";
    }
    vi.stubGlobal("Image", PendingImage);
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 20 };
    const same = [npc("household-a", 5, 5), npc("household-b", 5, 5)];
    const ctx = fakeCtx({ width: 400, height: 400 });

    draw(ctx, baseFrame(camera, same));

    const centers = ctx.arc.mock.calls.map((args: unknown[]) => [args[0], args[1]]);
    // token com fallback de glifo desenha 2 arcs (disco + glifo) por NPC — o primeiro arc de
    // cada um é o disco; os dois discos não podem cair no mesmo centro de tela.
    expect(centers[0]).not.toEqual(centers[2]);
  });

  it("draws a single NPC alone in its cell at its true, unshifted position", () => {
    class PendingImage {
      complete = false;
      naturalWidth = 0;
      src = "";
    }
    vi.stubGlobal("Image", PendingImage);
    const camera: CameraState = { center: { x: 5.5, y: 5.5 }, scale: 20 };
    const ctx = fakeCtx({ width: 400, height: 400 });

    draw(ctx, baseFrame(camera, [npc("alone", 5, 5)]));

    expect(ctx.arc.mock.calls[0]?.[0]).toBeCloseTo(200);
    expect(ctx.arc.mock.calls[0]?.[1]).toBeCloseTo(200);
  });

  it("draws a construction scaffold with a progress cue at the site cell", () => {
    const ctx = fakeCtx({ width: 400, height: 400 });
    const site: AuthoritativeEntity = {
      ref: { kind: "building", id: "construction:0", space: { kind: "City", cityId: "a" } },
      position: { x: 5, y: 5 },
      size: { w: 2, h: 2 },
      sizeIsDerived: true,
      color: "#8a6a3a",
      label: "Obra 40%",
      process: { kind: "construction", progress: 0.4, accessibleLabel: "Construção em andamento, 40%" },
    };

    draw(ctx, baseFrame({ center: { x: 6, y: 6 }, scale: 20 }, [site]));

    expect(ctx.setLineDash.mock.calls.some((args) => (args[0] as number[]).length > 0)).toBe(true);
    expect(ctx.fillText.mock.calls.some((args) => String(args[0]).includes("40%"))).toBe(true);
  });

  it("draws a queued construction site even at zero progress", () => {
    const ctx = fakeCtx({ width: 400, height: 400 });
    const site: AuthoritativeEntity = {
      ref: { kind: "building", id: "construction:1", space: { kind: "City", cityId: "a" } },
      position: { x: 5, y: 5 },
      size: { w: 2, h: 2 },
      sizeIsDerived: true,
      color: "#8a6a3a",
      process: { kind: "construction", progress: 0, accessibleLabel: "Construção em andamento, 0%" },
    };

    draw(ctx, baseFrame({ center: { x: 6, y: 6 }, scale: 20 }, [site]));

    expect(ctx.strokeRect).toHaveBeenCalled();
    expect(ctx.fillText.mock.calls.some((args) => String(args[0]).includes("0%"))).toBe(true);
  });

  it("draws the NPC pawn at city token-detail LOD instead of a blank tile", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const ctx = fakeCtx({ width: 400, height: 400 });
    const cityNpc = npc("city-pawn", 5, 5, false, { kind: "City", cityId: "city-a" });
    const cityLod: LodThresholds = { aggregate: 4, token: 6, detail: 18 };

    draw(ctx, { ...baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [cityNpc]), lodThresholds: cityLod });

    expect(ctx.drawImage).toHaveBeenCalledOnce();
  });

  it("overlays work, rest, food, water, and crop cues on the NPC at city detail LOD", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const cityLod: LodThresholds = { aggregate: 4, token: 6, detail: 18 };
    const kinds = ["rest", "food", "water", "crop"] as const;
    for (const kind of kinds) {
      const ctx = fakeCtx({ width: 400, height: 400 });
      const entity = {
        ...npc("cue-npc", 5, 5, false, { kind: "City" as const, cityId: "city-a" }),
        currentAction: 4,
        process: { kind, progress: 0.5, accessibleLabel: kind },
      };
      draw(ctx, { ...baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [entity]), lodThresholds: cityLod });
      expect(ctx.arc.mock.calls.length, kind).toBeGreaterThanOrEqual(1);
    }
  });

  it("falls back to a static unknown-action icon, never a blank token", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const ctx = fakeCtx({ width: 400, height: 400 });
    const entity = { ...npc("mystery", 5, 5, false, { kind: "City", cityId: "city-a" }), currentAction: 99 };

    draw(ctx, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [entity]));

    expect(ctx.drawImage).toHaveBeenCalledOnce();
    expect(ctx.arc.mock.calls.length).toBeGreaterThanOrEqual(1);
  });

  it("keeps the action cue when reduced motion is preferred, without pulsing", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query.includes("prefers-reduced-motion"),
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
    const ctx = fakeCtx({ width: 400, height: 400 });
    const entity = { ...npc("sleeper", 5, 5, false, { kind: "City", cityId: "city-a" }), currentAction: 1 };

    draw(ctx, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [entity]));

    expect(ctx.drawImage).toHaveBeenCalledOnce();
    expect(ctx.arc.mock.calls.length).toBeGreaterThanOrEqual(2);
  });
});
