import { describe, expect, it } from "vitest";
import { generateWorldRoads, settlementFootprintExtent } from "../../src/render/worldLayout";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("generateWorldRoads", () => {
  it("returns no segments for 0 or 1 settlements", () => {
    expect(generateWorldRoads([])).toEqual([]);
    expect(generateWorldRoads([{ gridPosition: { x: 0, y: 0 } }])).toEqual([]);
  });

  it("connects every settlement with exactly N-1 segments (a spanning tree, no cycles)", () => {
    const settlements = [
      { gridPosition: { x: 5, y: 5 } },
      { gridPosition: { x: 2, y: 2 } },
      { gridPosition: { x: 8, y: 3 } },
      { gridPosition: { x: 20, y: 20 } },
    ];
    expect(generateWorldRoads(settlements)).toHaveLength(settlements.length - 1);
  });

  it("connects each settlement to its nearest already-connected neighbor (greedy MST, not a hub)", () => {
    // A(0,0) — B(1,0) — C(2,0) em linha: B é o vizinho mais próximo de A E de C, então a MST
    // esperada é A-B e B-C, nunca A-C direto (mais longe que A-B).
    const a = { gridPosition: { x: 0, y: 0 } };
    const b = { gridPosition: { x: 1, y: 0 } };
    const c = { gridPosition: { x: 2, y: 0 } };
    const segments = generateWorldRoads([a, b, c]);
    const touches = (p: { x: number; y: number }) => segments.some((s) => (s.from === p || s.to === p) as boolean);
    expect(segments).toHaveLength(2);
    expect(touches(a.gridPosition)).toBe(true);
    expect(touches(c.gridPosition)).toBe(true);
    // Nenhum segmento liga A diretamente a C (distância 2) — a MST usa o caminho por B.
    const directAtoC = segments.some((s) => (s.from === a.gridPosition && s.to === c.gridPosition) || (s.from === c.gridPosition && s.to === a.gridPosition));
    expect(directAtoC).toBe(false);
  });

  it("is deterministic — same input, same output", () => {
    const settlements = [{ gridPosition: { x: 5, y: 5 } }, { gridPosition: { x: 2, y: 2 } }, { gridPosition: { x: 8, y: 3 } }];
    expect(generateWorldRoads(settlements)).toEqual(generateWorldRoads(settlements));
  });
});

// Pedido do usuário 2026-08-27: "uma cidade com 4 casas que ocupam 4x4 deve ocupar o mesmo
// terreno no mapa mundi" (footprint real) "e também" crescer/encolher com a população (fallback
// quando não há geometria real ainda, doc §90-91).
describe("settlementFootprintExtent", () => {
  it("derives a real bounding box from the buildings' own footprints, not just their center points", () => {
    const OAKBRIDGE = WORLD_FIXTURE.settlements.find((s) => s.id === "oakbridge")!;
    const extent = settlementFootprintExtent(OAKBRIDGE);
    // Prédios variam de x=0 a x=15 (gridPosition), mas cada um tem uma LARGURA própria além do
    // ponto central — a extensão real tem que ser MAIOR que só max(x)-min(x) dos centros.
    const centersOnlyWidth = Math.max(...OAKBRIDGE.buildings.map((b) => b.gridPosition.x)) - Math.min(...OAKBRIDGE.buildings.map((b) => b.gridPosition.x));
    expect(extent.width).toBeGreaterThan(centersOnlyWidth);
    expect(extent.height).toBeGreaterThan(0);
  });

  it("falls back to a population-derived size when there is no building data yet", () => {
    const millbrook = WORLD_FIXTURE.settlements.find((s) => s.id === "millbrook")!;
    expect(millbrook.buildings).toHaveLength(0);
    const extent = settlementFootprintExtent(millbrook);
    expect(extent.width).toBeGreaterThan(0);
    expect(extent.width).toBe(extent.height); // fallback é um quadrado, sem geometria real pra dar formato
  });

  it("the fallback grows with population — bigger settlements read as physically bigger", () => {
    const smaller = settlementFootprintExtent({ buildings: [], population: 10 });
    const bigger = settlementFootprintExtent({ buildings: [], population: 100 });
    expect(bigger.width).toBeGreaterThan(smaller.width);
  });

  it("is deterministic — same settlement, same extent every time", () => {
    const OAKBRIDGE = WORLD_FIXTURE.settlements.find((s) => s.id === "oakbridge")!;
    expect(settlementFootprintExtent(OAKBRIDGE)).toEqual(settlementFootprintExtent(OAKBRIDGE));
  });
});
