import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { CityView } from "../src/components/CityView";
import type { CitySnapshot } from "../src/types";

function makeSnapshot(): CitySnapshot {
  return {
    id: { value: "city-1" },
    location: { x: 0, y: 0 },
    aggregatePool: { count: 5, wealthSum: 500, healthSum: 400 },
    residents: [{ id: { value: 3 }, location: { x: 1, y: 1 }, currentAction: null }],
    buildings: [{ id: { value: 8 }, buildingTypeId: 2 }],
    layers: {} as CitySnapshot["layers"],
  };
}

describe("CityView", () => {
  it("renders visible residents", () => {
    render(<CityView snapshot={makeSnapshot()} onSelectBuilding={() => {}} onBack={() => {}} />);

    expect(screen.getByText(/npc 3 em/)).toBeInTheDocument();
  });

  it("calls onSelectBuilding with the clicked building id (drill-down to interior)", () => {
    const onSelectBuilding = vi.fn();
    render(<CityView snapshot={makeSnapshot()} onSelectBuilding={onSelectBuilding} onBack={() => {}} />);

    fireEvent.click(screen.getByText(/prédio 8/));

    expect(onSelectBuilding).toHaveBeenCalledWith("8");
  });

  it("calls onBack when the back button is clicked", () => {
    const onBack = vi.fn();
    render(<CityView snapshot={makeSnapshot()} onSelectBuilding={() => {}} onBack={onBack} />);

    fireEvent.click(screen.getByText(/mapa-múndi/));

    expect(onBack).toHaveBeenCalled();
  });
});
