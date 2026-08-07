import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { CityView } from "../src/components/CityView";
import type { CitySnapshot } from "../src/types";

const LOCAL_SIZE = 21; // mesmo valor de CityView.tsx

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

function clickCell(canvas: HTMLCanvasElement, x: number, y: number) {
  vi.spyOn(canvas, "getBoundingClientRect").mockReturnValue({
    left: 0,
    top: 0,
    width: canvas.width,
    height: canvas.height,
    right: canvas.width,
    bottom: canvas.height,
    x: 0,
    y: 0,
    toJSON: () => "",
  });
  const cellW = canvas.width / LOCAL_SIZE;
  const cellH = canvas.height / LOCAL_SIZE;
  fireEvent.click(canvas, { clientX: (x + 0.5) * cellW, clientY: (y + 0.5) * cellH });
}

describe("CityView", () => {
  beforeEach(() => {
    HTMLCanvasElement.prototype.getContext = () => null;
  });

  it("opens the side panel for a resident clicked at its relative grid position", () => {
    render(<CityView snapshot={makeSnapshot()} onSelectBuilding={() => {}} onBack={() => {}} />);

    // resident at (1,1), city at (0,0), local grid centers on 10 -> local (11,11)
    clickCell(screen.getByTestId("grid-canvas") as HTMLCanvasElement, 11, 11);

    expect(screen.getByText("NPC 3")).toBeInTheDocument();
  });

  it("calls onSelectBuilding with the clicked building id (drill-down to interior)", () => {
    const onSelectBuilding = vi.fn();
    render(<CityView snapshot={makeSnapshot()} onSelectBuilding={onSelectBuilding} onBack={() => {}} />);

    // single building sits on the ring at angle 0 -> local (14,10)
    clickCell(screen.getByTestId("grid-canvas") as HTMLCanvasElement, 14, 10);
    fireEvent.click(screen.getByRole("button", { name: "Entrar" }));

    expect(onSelectBuilding).toHaveBeenCalledWith("8");
  });

  it("calls onBack when the back button is clicked", () => {
    const onBack = vi.fn();
    render(<CityView snapshot={makeSnapshot()} onSelectBuilding={() => {}} onBack={onBack} />);

    fireEvent.click(screen.getByText(/mapa-múndi/));

    expect(onBack).toHaveBeenCalled();
  });
});
