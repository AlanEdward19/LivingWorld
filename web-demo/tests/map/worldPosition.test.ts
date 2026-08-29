import { describe, expect, it } from "vitest";
import { agentWorldPosition, LOCAL_UNITS_PER_WORLD_TILE, settlementWorldOrigin } from "../../src/map/worldPosition";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

const OAKBRIDGE = WORLD_FIXTURE.settlements.find((s) => s.id === "oakbridge")!;

describe("worldPosition", () => {
  it("settlementWorldOrigin() is exactly the settlement's own gridPosition", () => {
    expect(settlementWorldOrigin(OAKBRIDGE)).toEqual(OAKBRIDGE.gridPosition);
  });

  it("an outdoor agent's world position is the settlement origin plus their local patrol position, scaled down", () => {
    const rowan = WORLD_FIXTURE.agents.find((a) => a.id === "rowan")!;
    expect(rowan.indoorLocation).toBeUndefined();
    const now = 0; // patrolPositionAt(now=0) is deterministic — first point of the loop
    const pos = agentWorldPosition(WORLD_FIXTURE, rowan, now);
    const settlement = WORLD_FIXTURE.settlements.find((s) => s.id === rowan.settlementId)!;
    expect(pos.x).toBeCloseTo(settlement.gridPosition.x + rowan.patrolPoints[0].x / LOCAL_UNITS_PER_WORLD_TILE, 5);
    expect(pos.y).toBeCloseTo(settlement.gridPosition.y + rowan.patrolPoints[0].y / LOCAL_UNITS_PER_WORLD_TILE, 5);
  });

  it("an indoor agent's world position is the settlement origin plus their building's local position", () => {
    const mira = WORLD_FIXTURE.agents.find((a) => a.id === "mira-valen")!;
    expect(mira.indoorLocation).toBeDefined();
    const building = OAKBRIDGE.buildings.find((b) => b.id === mira.indoorLocation!.buildingId)!;
    const pos = agentWorldPosition(WORLD_FIXTURE, mira, 0);
    expect(pos.x).toBeCloseTo(OAKBRIDGE.gridPosition.x + building.gridPosition.x / LOCAL_UNITS_PER_WORLD_TILE, 5);
    expect(pos.y).toBeCloseTo(OAKBRIDGE.gridPosition.y + building.gridPosition.y / LOCAL_UNITS_PER_WORLD_TILE, 5);
  });

  it("stays close to its settlement's origin — local offsets never overflow into a neighboring settlement's territory", () => {
    // Millbrook está a ~4.2 unidades de mundo de Oakbridge (o mais próximo) — o offset local
    // máximo (prédio mais distante do centro, ~15 unidades locais) tem que ficar bem menor que
    // isso depois de escalado, senão dois settlements vizinhos se misturariam visualmente.
    for (const agent of WORLD_FIXTURE.agents) {
      const pos = agentWorldPosition(WORLD_FIXTURE, agent, 0);
      const settlement = WORLD_FIXTURE.settlements.find((s) => s.id === agent.settlementId)!;
      const dx = pos.x - settlement.gridPosition.x;
      const dy = pos.y - settlement.gridPosition.y;
      expect(Math.hypot(dx, dy)).toBeLessThan(1.5);
    }
  });
});
