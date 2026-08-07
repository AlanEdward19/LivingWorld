import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { LayerPanel } from "../src/components/LayerPanel";
import type { LayerBuildResult, VisualLayerName } from "../src/types";
import { LAYER_Z_ORDER } from "../src/map-engine/layers";

const ALL_LAYER_NAMES = LAYER_Z_ORDER;
const MODELED: ReadonlySet<VisualLayerName> = new Set(["Terrain", "Biome", "Rivers"]);

function buildLayers(): Record<VisualLayerName, LayerBuildResult> {
  const layers = {} as Record<VisualLayerName, LayerBuildResult>;
  for (const name of ALL_LAYER_NAMES) {
    layers[name] = { isModeled: MODELED.has(name), payload: null };
  }
  return layers;
}

describe("LayerPanel", () => {
  it("starts collapsed and opens on click", () => {
    render(<LayerPanel layers={buildLayers()} active={new Set()} onToggle={() => {}} />);
    expect(screen.queryByRole("list")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Camadas/ }));
    expect(screen.getByRole("list")).toBeInTheDocument();
  });

  it("renders a checkbox for every modeled layer, checked only when active", () => {
    render(<LayerPanel layers={buildLayers()} active={new Set(["Terrain"])} onToggle={() => {}} />);
    fireEvent.click(screen.getByRole("button", { name: /Camadas/ }));
    expect(screen.getByLabelText("toggle-Terrain")).toBeChecked();
    expect(screen.getByLabelText("toggle-Rivers")).not.toBeChecked();
  });

  it("calls onToggle with the layer name when its checkbox is clicked", () => {
    const onToggle = vi.fn();
    render(<LayerPanel layers={buildLayers()} active={new Set()} onToggle={onToggle} />);
    fireEvent.click(screen.getByRole("button", { name: /Camadas/ }));
    fireEvent.click(screen.getByLabelText("toggle-Rivers"));
    expect(onToggle).toHaveBeenCalledWith("Rivers");
    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it("renders not-yet-modeled layers disabled with a reason, never as a working toggle", () => {
    render(<LayerPanel layers={buildLayers()} active={new Set()} onToggle={() => {}} />);
    fireEvent.click(screen.getByRole("button", { name: /Camadas/ }));
    expect(screen.getByText(/Roads — ainda não modelada/)).toBeInTheDocument();
    const roadsRow = screen.getByText(/Roads — ainda não modelada/).closest("li");
    expect(roadsRow).toHaveClass("layer-not-modeled");
    expect(roadsRow?.querySelector("input")).toBeDisabled();
  });

  it("lists layers in the declared deterministic z-order, not object insertion order", () => {
    render(<LayerPanel layers={buildLayers()} active={new Set()} onToggle={() => {}} />);
    fireEvent.click(screen.getByRole("button", { name: /Camadas/ }));
    const items = screen.getAllByRole("listitem").map((li) => li.textContent);
    const positions = ALL_LAYER_NAMES.map((name) => items.findIndex((text) => text?.startsWith(name)));
    expect(positions).toEqual([...positions].sort((a, b) => a - b));
  });
});
