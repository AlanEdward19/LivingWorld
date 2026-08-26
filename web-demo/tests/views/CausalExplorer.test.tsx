import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { CausalExplorer } from "../../src/views/CausalExplorer";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("CausalExplorer", () => {
  it("shows the WHY? cause for the grain price rise event (Harvest below normal)", () => {
    render(
      <CausalExplorer fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} eventId="evt-grain-stock-declined" />,
    );
    const why = screen.getByTestId("why-section");
    expect(why).toHaveTextContent("The autumn harvest came in well below normal");
  });

  it("shows the consequences tree matching doc#117-118 (Valen reduced purchases → Mira very hungry → left work early; Baker reduced production; Migration pressure increased)", () => {
    render(
      <CausalExplorer fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} eventId="evt-grain-prices-rose" />,
    );
    const consequences = screen.getByTestId("consequences-section");
    expect(consequences).toHaveTextContent("The Valen household reduced its grain purchases.");
    expect(consequences).toHaveTextContent("Mira Valen became very hungry.");
    expect(consequences).toHaveTextContent("Mira Valen left work early.");
    expect(consequences).toHaveTextContent("Baker reduced production.");
    expect(consequences).toHaveTextContent("Migration pressure increased.");
  });

  it("lists the systems involved matching doc#118 (Agriculture/Economy/Household/Needs/Decision/Employment)", () => {
    render(
      <CausalExplorer fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} eventId="evt-grain-prices-rose" />,
    );
    const systems = screen.getByTestId("systems-involved");
    for (const system of ["Agriculture", "Economy", "Household", "Needs", "Decision", "Employment"]) {
      expect(systems).toHaveTextContent(system);
    }
  });

  it("shows 'no known earlier cause' for an event with no causeEventId (root of the chain)", () => {
    render(
      <CausalExplorer fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} eventId="evt-harvest-below-normal" />,
    );
    expect(screen.getByTestId("no-known-cause")).toBeInTheDocument();
  });

  it("clicking a consequence event navigates towards the Timeline", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CausalExplorer fixture={WORLD_FIXTURE} nav={nav} eventId="evt-grain-prices-rose" />);
    fireEvent.click(screen.getByText("Mira Valen became very hungry."));
    expect(nav.current()).toMatchObject({ kind: "timeline" });
  });

  it("returns nothing for an unknown eventId instead of throwing", () => {
    const onError = vi.fn();
    expect(() =>
      render(<CausalExplorer fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} eventId="does-not-exist" />),
    ).not.toThrow();
    expect(onError).not.toHaveBeenCalled();
  });
});
