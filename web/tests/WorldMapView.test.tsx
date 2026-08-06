import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { WorldMapView } from "../src/components/WorldMapView";
import type { GlobalSnapshot } from "../src/types";

function makeSnapshot(): GlobalSnapshot {
  return {
    cities: [{ id: { value: "city-1" }, location: { x: 3, y: 4 }, population: 42 }],
    externalNpcs: [{ id: { value: 9 }, location: { x: 1, y: 1 } }],
    activeEvents: [],
    layers: {
      Terrain: { isModeled: true, payload: [] },
      Biome: { isModeled: true, payload: [] },
      Rivers: { isModeled: true, payload: [] },
      Mountains: { isModeled: false, payload: null },
      Resources: { isModeled: true, payload: [] },
      Roads: { isModeled: false, payload: null },
      Borders: { isModeled: false, payload: null },
      Kingdoms: { isModeled: false, payload: null },
      Cities: { isModeled: false, payload: null },
      Villages: { isModeled: false, payload: null },
      Routes: { isModeled: false, payload: null },
      Migrations: { isModeled: false, payload: null },
      Conflicts: { isModeled: false, payload: null },
      Climate: { isModeled: false, payload: null },
    },
  };
}

describe("WorldMapView", () => {
  it("renders every city with its population", () => {
    render(<WorldMapView snapshot={makeSnapshot()} onSelectCity={() => {}} />);

    expect(screen.getByText(/pop\. 42/)).toBeInTheDocument();
  });

  it("calls onSelectCity with the clicked city id (drill-down)", () => {
    const onSelectCity = vi.fn();
    render(<WorldMapView snapshot={makeSnapshot()} onSelectCity={onSelectCity} />);

    fireEvent.click(screen.getByText(/pop\. 42/));

    expect(onSelectCity).toHaveBeenCalledWith("city-1");
  });

  it("labels not-yet-modeled layers distinctly from available ones", () => {
    render(<WorldMapView snapshot={makeSnapshot()} onSelectCity={() => {}} />);

    expect(screen.getByText(/Terrain: dispon/)).toBeInTheDocument();
    expect(screen.getByText(/Roads: ainda não modelada/)).toBeInTheDocument();
  });
});
