import { describe, expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { SettlementView } from "../../src/views/SettlementView";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

const OAKBRIDGE = WORLD_FIXTURE.settlements.find((s) => s.id === "oakbridge")!;

describe("SettlementView", () => {
  it("displays the exact settlement pulse values from the fixture", () => {
    render(<SettlementView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} settlementId="oakbridge" />);
    expect(screen.getByTestId("pulse-population")).toHaveTextContent(String(OAKBRIDGE.population));
    expect(screen.getByTestId("pulse-population-trend")).toHaveTextContent(OAKBRIDGE.populationTrend);
    expect(screen.getByTestId("pulse-food")).toHaveTextContent(OAKBRIDGE.food);
    expect(screen.getByTestId("pulse-employment")).toHaveTextContent(OAKBRIDGE.employment);
    expect(screen.getByTestId("pulse-migration")).toHaveTextContent(OAKBRIDGE.migration);
    expect(screen.getByTestId("pulse-construction")).toHaveTextContent(String(OAKBRIDGE.construction));
  });

  it("clicking the Valen household navigates to HouseholdView", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<SettlementView fixture={WORLD_FIXTURE} nav={nav} settlementId="oakbridge" />);
    fireEvent.click(screen.getByText("Valen Household"));
    expect(nav.current()).toEqual({ kind: "household", id: "valen-household" });
  });

  it("clicking View Timeline navigates to the settlement-scoped Timeline (spec P2 AC1)", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<SettlementView fixture={WORLD_FIXTURE} nav={nav} settlementId="oakbridge" />);
    fireEvent.click(screen.getByTestId("view-timeline"));
    expect(nav.current()).toEqual({ kind: "timeline", scope: { type: "settlement", id: "oakbridge" } });
  });
});
