import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { GridCanvas } from "../src/components/GridCanvas";

function stubRect(canvas: HTMLCanvasElement) {
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
}

describe("GridCanvas", () => {
  beforeEach(() => {
    HTMLCanvasElement.prototype.getContext = () => null;
  });

  it("sizes the canvas to width*zoom by height*zoom", () => {
    render(<GridCanvas width={10} height={5} markers={[]} zoom={16} />);
    const canvas = screen.getByTestId("grid-canvas") as HTMLCanvasElement;
    expect(canvas.width).toBe(160);
    expect(canvas.height).toBe(80);
  });

  it("calls onMarkerClick when a click lands within a marker's hit radius", () => {
    const onMarkerClick = vi.fn();
    const onCellClick = vi.fn();
    render(
      <GridCanvas
        width={10}
        height={10}
        markers={[{ id: "m1", x: 3, y: 4, color: "red" }]}
        zoom={16}
        onMarkerClick={onMarkerClick}
        onCellClick={onCellClick}
      />,
    );
    const canvas = screen.getByTestId("grid-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.click(canvas, { clientX: 3.5 * 16, clientY: 4.5 * 16 });

    expect(onMarkerClick).toHaveBeenCalledWith("m1");
    expect(onCellClick).not.toHaveBeenCalled();
  });

  it("calls onCellClick with the cell coordinates when no marker is hit", () => {
    const onCellClick = vi.fn();
    render(<GridCanvas width={10} height={10} markers={[]} zoom={16} onCellClick={onCellClick} />);
    const canvas = screen.getByTestId("grid-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.click(canvas, { clientX: 2.5 * 16, clientY: 6.5 * 16 });

    expect(onCellClick).toHaveBeenCalledWith(2, 6);
  });

  it("ignores clicks entirely when readOnly", () => {
    const onCellClick = vi.fn();
    render(<GridCanvas width={10} height={10} markers={[]} zoom={16} onCellClick={onCellClick} readOnly />);
    const canvas = screen.getByTestId("grid-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.click(canvas, { clientX: 2.5 * 16, clientY: 6.5 * 16 });

    expect(onCellClick).not.toHaveBeenCalled();
  });

  it("renders zoom controls only when onZoomChange is provided", () => {
    const { rerender } = render(<GridCanvas width={5} height={5} markers={[]} zoom={16} />);
    expect(screen.queryByLabelText("zoom-in")).not.toBeInTheDocument();

    rerender(<GridCanvas width={5} height={5} markers={[]} zoom={16} onZoomChange={() => {}} />);
    expect(screen.getByLabelText("zoom-in")).toBeInTheDocument();
    expect(screen.getByLabelText("zoom-out")).toBeInTheDocument();
  });
});
