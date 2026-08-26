import { describe, expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { App } from "../src/App";
import { WORLD_FIXTURE } from "../src/fixture/oakbridge";

describe("App", () => {
  it("mounts the full shell (Top Bar, Explorer, world map, Inspector, Timeline bar) at the World route by default", () => {
    render(<App />);
    expect(screen.getByTestId("top-bar")).toBeInTheDocument();
    expect(screen.getByTestId("explorer")).toBeInTheDocument();
    expect(screen.getByTestId("center-stage")).toBeInTheDocument();
    expect(screen.getByTestId("inspector-empty")).toBeInTheDocument();
    expect(screen.getByTestId("timeline-bar")).toBeInTheDocument();
    expect(screen.getAllByTestId("settlement-marker")).toHaveLength(WORLD_FIXTURE.settlements.length);
  });

  it("walks the full P1 flow end to end through the real shell: World map → Settlement → Household → Agent → Why → Causal Explorer", () => {
    render(<App />);

    // World: click Oakbridge on the map (center stage)
    const oakbridgeIndex = WORLD_FIXTURE.settlements.findIndex((s) => s.id === "oakbridge");
    fireEvent.click(screen.getAllByTestId("settlement-marker")[oakbridgeIndex]);

    // Settlement Pulse now lives in the Inspector
    expect(within(screen.getByTestId("inspector")).getByTestId("settlement-view")).toBeInTheDocument();
    fireEvent.click(within(screen.getByTestId("inspector")).getByText("Valen Household"));

    // Household detail
    expect(within(screen.getByTestId("inspector")).getByTestId("household-view")).toBeInTheDocument();
    fireEvent.click(within(screen.getByTestId("household-members")).getByText("Mira Valen"));

    // Agent detail
    const inspector = screen.getByTestId("inspector");
    expect(within(inspector).getByTestId("agent-view")).toBeInTheDocument();
    fireEvent.click(within(inspector).getByText("Why?"));
    fireEvent.click(within(inspector).getByText("grain prices rose"));

    // Causal Explorer takes over the center stage
    expect(within(screen.getByTestId("center-stage")).getByTestId("causal-explorer")).toBeInTheDocument();
    // Inspector shows a contextual note instead of duplicating the center
    expect(screen.getByTestId("inspector-empty")).toHaveTextContent("Exploring a causal chain");
  });
});
