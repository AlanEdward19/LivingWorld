import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MapGridEditor } from "../src/components/MapGridEditor";

const ZOOM = 16;

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
  fireEvent.click(canvas, { clientX: (x + 0.5) * ZOOM, clientY: (y + 0.5) * ZOOM });
}

describe("MapGridEditor", () => {
  beforeEach(() => {
    HTMLCanvasElement.prototype.getContext = () => null;
  });

  it("paints a cell with the selected terrain id on click", () => {
    const onCellsChange = vi.fn();
    render(
      <MapGridEditor
        width={10}
        height={10}
        terrainIds={[1, 2, 3]}
        biomeIds={[1]}
        cells={{}}
        onCellsChange={onCellsChange}
        settlements={[]}
        onSettlementsChange={() => {}}
      />,
    );

    fireEvent.change(screen.getByLabelText(/Terreno:/), { target: { value: "2" } });
    clickCell(screen.getByTestId("grid-canvas") as HTMLCanvasElement, 3, 4);

    expect(onCellsChange).toHaveBeenCalledWith({
      "3,4": { terrain: 2, biome: 1, altitude: 0, water: false },
    });
  });

  it("adds a settlement in settlement mode instead of painting", () => {
    const onSettlementsChange = vi.fn();
    render(
      <MapGridEditor
        width={10}
        height={10}
        terrainIds={[1]}
        biomeIds={[1]}
        cells={{}}
        onCellsChange={() => {}}
        settlements={[]}
        onSettlementsChange={onSettlementsChange}
      />,
    );

    fireEvent.change(screen.getByLabelText(/Modo:/), { target: { value: "settlement" } });
    clickCell(screen.getByTestId("grid-canvas") as HTMLCanvasElement, 2, 2);

    expect(onSettlementsChange).toHaveBeenCalledWith([{ name: "assentamento-1", x: 2, y: 2 }]);
  });

  it("erases a painted cell in erase mode", () => {
    const onCellsChange = vi.fn();
    render(
      <MapGridEditor
        width={10}
        height={10}
        terrainIds={[1]}
        biomeIds={[1]}
        cells={{ "3,4": { terrain: 1, biome: 1, altitude: 0, water: false } }}
        onCellsChange={onCellsChange}
        settlements={[]}
        onSettlementsChange={() => {}}
      />,
    );

    fireEvent.change(screen.getByLabelText(/Modo:/), { target: { value: "erase" } });
    clickCell(screen.getByTestId("grid-canvas") as HTMLCanvasElement, 3, 4);

    expect(onCellsChange).toHaveBeenCalledWith({});
  });
});
