import { describe, expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { WorldView } from "../../src/views/WorldView";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("WorldView", () => {
  it("shows the total population across every settlement, not just one", () => {
    render(<WorldView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} />);
    const expected = WORLD_FIXTURE.settlements.reduce((sum, s) => sum + s.population, 0);
    expect(screen.getByTestId("world-pulse-population")).toHaveTextContent(String(expected));
  });

  it("lists every settlement and navigates to one on click", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<WorldView fixture={WORLD_FIXTURE} nav={nav} />);
    const list = within(screen.getByTestId("world-settlements"));
    for (const settlement of WORLD_FIXTURE.settlements) {
      expect(list.getByText(settlement.name)).toBeInTheDocument();
    }
    fireEvent.click(list.getByText("Oakbridge"));
    expect(nav.current()).toEqual({ kind: "settlement", id: "oakbridge" });
  });

  it("clicking View Timeline opens the world-scoped Timeline", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<WorldView fixture={WORLD_FIXTURE} nav={nav} />);
    fireEvent.click(screen.getByTestId("view-timeline"));
    expect(nav.current()).toEqual({ kind: "timeline", scope: { type: "world" } });
  });
});
