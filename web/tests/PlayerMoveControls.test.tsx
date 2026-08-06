import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { PlayerMoveControls } from "../src/components/PlayerMoveControls";
import type { CitySnapshot } from "../src/types";

function makeSnapshot(): CitySnapshot {
  return {
    id: { value: "city-1" },
    location: { x: 0, y: 0 },
    aggregatePool: { count: 0, wealthSum: 0, healthSum: 0 },
    residents: [{ id: { value: 5 }, location: { x: 2, y: 3 }, currentAction: null }],
    buildings: [],
    layers: {} as CitySnapshot["layers"],
  };
}

describe("PlayerMoveControls", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 200 })));
  });

  it("posts a move intent when a directional button is clicked", () => {
    render(<PlayerMoveControls snapshot={makeSnapshot()} playerNpcId={5} />);

    fireEvent.click(screen.getByLabelText("mover-cima"));

    expect(fetch).toHaveBeenCalledWith(
      "/visual/player/5/move",
      expect.objectContaining({
        body: JSON.stringify({ targetX: 2, targetY: 2, inputMode: "click" }),
      }),
    );
  });

  it("posts a move intent when a WASD key is pressed", () => {
    render(<PlayerMoveControls snapshot={makeSnapshot()} playerNpcId={5} />);

    fireEvent.keyDown(window, { key: "d" });

    expect(fetch).toHaveBeenCalledWith(
      "/visual/player/5/move",
      expect.objectContaining({
        body: JSON.stringify({ targetX: 3, targetY: 3, inputMode: "wasd" }),
      }),
    );
  });

  it("shows a note instead of controls when the player's own npc is not in this city's residents", () => {
    render(<PlayerMoveControls snapshot={makeSnapshot()} playerNpcId={999} />);

    expect(screen.getByRole("note")).toBeInTheDocument();
    expect(screen.queryByTestId("player-move-controls")).not.toBeInTheDocument();
  });
});
