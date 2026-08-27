import { describe, expect, it } from "vitest";
import { patrolPositionAt } from "../../src/map/patrolMath";

describe("patrolPositionAt", () => {
  it("returns the origin when there are no points", () => {
    expect(patrolPositionAt([], 12345)).toEqual({ x: 0, y: 0 });
  });

  it("stays fixed at the single point regardless of time", () => {
    expect(patrolPositionAt([{ x: 3, y: 4 }], 999999)).toEqual({ x: 3, y: 4 });
  });

  it("starts exactly at the first point at t=0", () => {
    expect(patrolPositionAt([{ x: 0, y: 0 }, { x: 4, y: 0 }], 0)).toEqual({ x: 0, y: 0 });
  });

  it("interpolates halfway between two points at the midpoint of the step duration", () => {
    const result = patrolPositionAt([{ x: 0, y: 0 }, { x: 4, y: 0 }], 2000, 4000);
    expect(result.x).toBeCloseTo(2, 5);
    expect(result.y).toBeCloseTo(0, 5);
  });

  it("loops back towards the first point after reaching the second", () => {
    const atSecond = patrolPositionAt([{ x: 0, y: 0 }, { x: 4, y: 0 }], 4000, 4000);
    expect(atSecond.x).toBeCloseTo(4, 5);
    const returning = patrolPositionAt([{ x: 0, y: 0 }, { x: 4, y: 0 }], 6000, 4000);
    expect(returning.x).toBeCloseTo(2, 5);
  });

  it("supports a custom step duration independent of the 4000ms default", () => {
    const result = patrolPositionAt([{ x: 0, y: 0 }, { x: 10, y: 0 }], 500, 1000);
    expect(result.x).toBeCloseTo(5, 5);
  });
});
