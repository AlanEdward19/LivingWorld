import { afterEach, describe, expect, it } from "vitest";
import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { App } from "../src/App";
import { WORLD_FIXTURE } from "../src/fixture/oakbridge";
import { followStore } from "../src/state/followStore";

afterEach(() => {
  cleanup();
  act(() => {
    for (const id of followStore.followedIds()) followStore.toggleFollow(id);
  });
});

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

describe("App — keyboard shortcuts (doc §148)", () => {
  // `App`'s NavigationStore is a module-level singleton, so navigation state can leak between
  // tests in this file — every test resets to World first (via the "w" shortcut itself, once
  // proven to work) before setting up its own scenario.
  function renderAtWorld() {
    const utils = render(<App />);
    fireEvent.keyDown(window, { key: "w" });
    return utils;
  }

  it("'w' returns to the World View from anywhere", () => {
    renderAtWorld();
    const oakbridgeIndex = WORLD_FIXTURE.settlements.findIndex((s) => s.id === "oakbridge");
    fireEvent.click(screen.getAllByTestId("settlement-marker")[oakbridgeIndex]);
    expect(screen.getByTestId("settlement-view")).toBeInTheDocument();

    fireEvent.keyDown(window, { key: "w" });

    expect(screen.getByTestId("inspector-empty")).toBeInTheDocument();
    expect(screen.queryByTestId("settlement-view")).not.toBeInTheDocument();
  });

  it("'f' follows the currently selected settlement", () => {
    renderAtWorld();
    const oakbridgeIndex = WORLD_FIXTURE.settlements.findIndex((s) => s.id === "oakbridge");
    fireEvent.click(screen.getAllByTestId("settlement-marker")[oakbridgeIndex]);

    fireEvent.keyDown(window, { key: "f" });

    expect(followStore.isFollowed("oakbridge")).toBe(true);
  });

  it("'/' focuses the search input", () => {
    renderAtWorld();
    fireEvent.keyDown(window, { key: "/" });
    expect(document.activeElement).toBe(screen.getByTestId("search-input"));
  });

  it("'?' toggles the keyboard shortcuts help panel", () => {
    renderAtWorld();
    expect(screen.queryByTestId("keyboard-help")).not.toBeInTheDocument();
    fireEvent.keyDown(window, { key: "?" });
    expect(screen.getByTestId("keyboard-help")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Close"));
    expect(screen.queryByTestId("keyboard-help")).not.toBeInTheDocument();
  });

  it("ignores shortcuts while typing in the search input (no conflict with input text)", () => {
    renderAtWorld();
    const oakbridgeIndex = WORLD_FIXTURE.settlements.findIndex((s) => s.id === "oakbridge");
    fireEvent.click(screen.getAllByTestId("settlement-marker")[oakbridgeIndex]);

    const searchInput = screen.getByTestId("search-input");
    searchInput.focus();
    fireEvent.keyDown(searchInput, { key: "w" });

    expect(screen.getByTestId("settlement-view")).toBeInTheDocument(); // "w" did not navigate away
  });
});
