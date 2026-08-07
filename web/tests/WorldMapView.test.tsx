import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { WorldMapView } from "../src/components/WorldMapView";
import type { GlobalSnapshot } from "../src/types";

function makeSnapshot(): GlobalSnapshot {
  return {
    width: 10,
    height: 10,
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

// jsdom não faz layout — getBoundingClientRect finge o canvas ocupar exatamente width x height
// em pixels de tela (escala 1:1). Clica no centro da célula (x,y) usando o tamanho REAL do
// canvas (width/height já setados pelo GridCanvas em width*zoom) dividido pelas dimensões do
// grid — não depende de conhecer o zoom (que agora é calculado por fit-to-screen, não fixo).
function clickCell(canvas: HTMLCanvasElement, gridWidth: number, gridHeight: number, x: number, y: number) {
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
  const cellW = canvas.width / gridWidth;
  const cellH = canvas.height / gridHeight;
  fireEvent.click(canvas, { clientX: (x + 0.5) * cellW, clientY: (y + 0.5) * cellH });
}

describe("WorldMapView", () => {
  beforeEach(() => {
    HTMLCanvasElement.prototype.getContext = () => null;
  });

  it("opens the side panel with population when a city marker is clicked", () => {
    render(<WorldMapView snapshot={makeSnapshot()} onSelectCity={() => {}} />);

    clickCell(screen.getByTestId("grid-canvas") as HTMLCanvasElement, 10, 10, 3, 4);

    expect(screen.getByTestId("side-panel")).toBeInTheDocument();
    expect(screen.getByText(/População: 42/)).toBeInTheDocument();
  });

  it("calls onSelectCity when 'Entrar' is clicked on the city side panel", () => {
    const onSelectCity = vi.fn();
    render(<WorldMapView snapshot={makeSnapshot()} onSelectCity={onSelectCity} />);

    clickCell(screen.getByTestId("grid-canvas") as HTMLCanvasElement, 10, 10, 3, 4);
    fireEvent.click(screen.getByRole("button", { name: "Entrar" }));

    expect(onSelectCity).toHaveBeenCalledWith("city-1");
  });

  it("opens the side panel with position when an external npc marker is clicked", () => {
    render(<WorldMapView snapshot={makeSnapshot()} onSelectCity={() => {}} />);

    clickCell(screen.getByTestId("grid-canvas") as HTMLCanvasElement, 10, 10, 1, 1);

    expect(screen.getByText("NPC 9")).toBeInTheDocument();
  });

  it("labels not-yet-modeled layers distinctly from available ones, behind the collapsible legend", () => {
    render(<WorldMapView snapshot={makeSnapshot()} onSelectCity={() => {}} />);

    expect(screen.queryByText(/Terrain: dispon/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Camadas/ }));

    expect(screen.getByText(/Terrain: dispon/)).toBeInTheDocument();
    expect(screen.getByText(/Roads: ainda não modelada/)).toBeInTheDocument();
  });
});
