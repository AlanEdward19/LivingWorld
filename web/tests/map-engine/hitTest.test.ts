import { describe, expect, it } from "vitest";
import { Camera } from "../../src/map-engine/Camera";
import { hitTest } from "../../src/map-engine/hitTest";
import type { AuthoritativeEntity } from "../../src/map-engine/types";

const VIEWPORT = { width: 200, height: 200 };

function npc(id: string, x: number, y: number): AuthoritativeEntity {
  return {
    ref: { kind: "npc", id, space: { kind: "World" } },
    position: { x, y },
    size: { w: 1, h: 1 },
    sizeIsDerived: false,
    color: "#000",
  };
}

function city(id: string, x: number, y: number, w: number, h: number): AuthoritativeEntity {
  return {
    ref: { kind: "city", id, space: { kind: "World" } },
    position: { x, y },
    size: { w, h },
    sizeIsDerived: true,
    color: "#d9a94f",
  };
}

describe("hitTest", () => {
  it("hits the entity under the cursor at a low zoom level (scale=2)", () => {
    const camera = new Camera({ center: { x: 50, y: 50 }, scale: 2 }, VIEWPORT);
    const entity = npc("npc-1", 50, 50); // projeta exatamente no centro da tela
    const screenPoint = camera.worldToScreen(entity.position);

    const hit = hitTest(screenPoint, camera, [entity], 8);

    expect(hit).toEqual(entity.ref);
  });

  it("hits the entity under the cursor at a high zoom level (scale=8)", () => {
    const camera = new Camera({ center: { x: 50, y: 50 }, scale: 8 }, VIEWPORT);
    const entity = npc("npc-1", 50, 50);
    const screenPoint = camera.worldToScreen(entity.position);

    const hit = hitTest(screenPoint, camera, [entity], 8);

    expect(hit).toEqual(entity.ref);
  });

  it("returns null when clicking empty space", () => {
    const camera = new Camera({ center: { x: 50, y: 50 }, scale: 2 }, VIEWPORT);
    const entity = npc("npc-1", 50, 50);

    const hit = hitTest({ x: 0, y: 0 }, camera, [entity], 8);

    expect(hit).toBeNull();
  });

  it("returns the closest entity when two are within the hit radius", () => {
    const camera = new Camera({ center: { x: 50, y: 50 }, scale: 2 }, VIEWPORT);
    const near = npc("near", 50, 50);
    const far = npc("far", 52, 50); // mais longe do centro da tela, mesmo raio de acerto

    const hit = hitTest(camera.worldToScreen({ x: 50, y: 50 }), camera, [far, near], 20);

    expect(hit).toEqual(near.ref);
  });

  // Feedback do usuário (2026-08-07): cidade virou área real — clicar em QUALQUER ponto do
  // footprint precisa acertar, não só perto do canto (`position`).
  it("hits an area entity (city footprint) anywhere inside its bounds, not just near its corner", () => {
    const camera = new Camera({ center: { x: 50, y: 50 }, scale: 4 }, VIEWPORT);
    const footprint = city("city-a", 40, 40, 6, 6); // ocupa (40,40) a (46,46)

    // clique no CENTRO do footprint, longe do canto em (40,40)
    const centerScreenPoint = camera.worldToScreen({ x: 43, y: 43 });
    expect(hitTest(centerScreenPoint, camera, [footprint], 8)).toEqual(footprint.ref);
  });

  it("does not hit an area entity when the click falls outside its footprint", () => {
    const camera = new Camera({ center: { x: 50, y: 50 }, scale: 4 }, VIEWPORT);
    const footprint = city("city-a", 40, 40, 6, 6);

    const outsideScreenPoint = camera.worldToScreen({ x: 100, y: 100 });
    expect(hitTest(outsideScreenPoint, camera, [footprint], 8)).toBeNull();
  });

  // Feedback do usuário (2026-08-21): dois NPCs no mesmo tile são desenhados espalhados
  // (`fanOutOffsets`, renderer.ts) mas o hit-test comparava contra a posição crua — todos
  // colidiam no mesmo ponto e só o primeiro era clicável. hitTest precisa do MESMO deslocamento.
  it("hits each of two household-mates sharing the exact same tile at their own fanned-out spot", () => {
    const camera = new Camera({ center: { x: 50, y: 50 }, scale: 20 }, VIEWPORT);
    const a = npc("household-a", 50, 50);
    const b = npc("household-b", 50, 50);

    // Mesmo cálculo de `fanOutOffsets` (radius 0.34, 2 entidades por ordem de id): "household-a"
    // fica em +0.34 no eixo x, "household-b" em -0.34 — ambos a partir do centro da célula (+0.5).
    const hitA = hitTest(camera.worldToScreen({ x: 50.84, y: 50.5 }), camera, [a, b], 4);
    const hitB = hitTest(camera.worldToScreen({ x: 50.16, y: 50.5 }), camera, [a, b], 4);

    expect(hitA).toEqual(a.ref);
    expect(hitB).toEqual(b.ref);
  });
});
