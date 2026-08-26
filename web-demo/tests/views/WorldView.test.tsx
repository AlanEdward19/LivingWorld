import { describe, expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { WorldView } from "../../src/views/WorldView";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("WorldView", () => {
  it("renders the world summary derived from the fixture", () => {
    render(<WorldView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} />);
    expect(screen.getByTestId("world-summary")).toHaveTextContent(WORLD_FIXTURE.world.summary);
  });

  it("clicking Oakbridge in the settlement list pushes the settlement route", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<WorldView fixture={WORLD_FIXTURE} nav={nav} />);
    fireEvent.click(within(screen.getByTestId("settlement-list")).getByText("Oakbridge"));
    expect(nav.current()).toEqual({ kind: "settlement", id: "oakbridge" });
  });

  it("clicking Oakbridge's marker directly on the map pushes the same settlement route as the list (spec P1b AC4)", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<WorldView fixture={WORLD_FIXTURE} nav={nav} />);
    const oakbridgeIndex = WORLD_FIXTURE.settlements.findIndex((s) => s.id === "oakbridge");
    fireEvent.click(screen.getAllByTestId("settlement-marker")[oakbridgeIndex]);
    expect(nav.current()).toEqual({ kind: "settlement", id: "oakbridge" });
  });

  it("clicking View Timeline navigates to the world-scoped Timeline (spec P2 AC1)", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<WorldView fixture={WORLD_FIXTURE} nav={nav} />);
    fireEvent.click(screen.getByTestId("view-timeline"));
    expect(nav.current()).toEqual({ kind: "timeline", scope: { type: "world" } });
  });
});
