import { describe, expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { HouseholdView } from "../../src/views/HouseholdView";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("HouseholdView", () => {
  it("shows Mira, Tomas, Eli and Nora as members", () => {
    render(<HouseholdView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} householdId="valen-household" />);
    const members = within(screen.getByTestId("household-members"));
    expect(members.getByText("Mira Valen")).toBeInTheDocument();
    expect(members.getByText("Tomas Valen")).toBeInTheDocument();
    expect(members.getByText("Eli Valen")).toBeInTheDocument();
    expect(members.getByText("Nora Valen")).toBeInTheDocument();
  });

  it("clicking Mira navigates to AgentView", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<HouseholdView fixture={WORLD_FIXTURE} nav={nav} householdId="valen-household" />);
    fireEvent.click(within(screen.getByTestId("household-members")).getByText("Mira Valen"));
    expect(nav.current()).toEqual({ kind: "agent", id: "mira-valen" });
  });

  it("clicking View Timeline navigates to the household-scoped Timeline (spec P2 AC1)", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<HouseholdView fixture={WORLD_FIXTURE} nav={nav} householdId="valen-household" />);
    fireEvent.click(screen.getByTestId("view-timeline"));
    expect(nav.current()).toEqual({ kind: "timeline", scope: { type: "household", id: "valen-household" } });
  });
});
