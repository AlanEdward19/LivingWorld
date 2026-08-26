import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { usePatrolPosition } from "../../src/map/usePatrolPosition";

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(0);
});

afterEach(() => {
  vi.useRealTimers();
});

describe("usePatrolPosition", () => {
  it("returns the origin when there are no patrol points", () => {
    const { result } = renderHook(() => usePatrolPosition([]));
    expect(result.current).toEqual({ x: 0, y: 0 });
  });

  it("stays fixed at the single point when there is only one", () => {
    const { result } = renderHook(() => usePatrolPosition([{ x: 3, y: 4 }]));
    expect(result.current).toEqual({ x: 3, y: 4 });
    act(() => vi.advanceTimersByTime(10000));
    expect(result.current).toEqual({ x: 3, y: 4 });
  });

  it("starts exactly at the first point at t=0", () => {
    const { result } = renderHook(() => usePatrolPosition([{ x: 0, y: 0 }, { x: 4, y: 0 }]));
    expect(result.current).toEqual({ x: 0, y: 0 });
  });

  it("interpolates halfway between two points at the midpoint of the step duration", () => {
    const { result } = renderHook(() => usePatrolPosition([{ x: 0, y: 0 }, { x: 4, y: 0 }]));
    act(() => vi.advanceTimersByTime(2000)); // metade dos 4000ms de cada perna
    expect(result.current.x).toBeCloseTo(2, 5);
    expect(result.current.y).toBeCloseTo(0, 5);
  });

  it("reaches the second point and loops back towards the first", () => {
    const { result } = renderHook(() => usePatrolPosition([{ x: 0, y: 0 }, { x: 4, y: 0 }]));
    act(() => vi.advanceTimersByTime(4000)); // fim da 1ª perna = chega no 2º ponto
    expect(result.current.x).toBeCloseTo(4, 5);
    act(() => vi.advanceTimersByTime(2000)); // meio da 2ª perna, voltando
    expect(result.current.x).toBeCloseTo(2, 5);
  });
});
