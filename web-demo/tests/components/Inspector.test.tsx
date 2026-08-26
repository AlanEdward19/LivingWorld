import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { Inspector } from "../../src/components/Inspector";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("Inspector", () => {
  it("shows an explicit empty state at the World route (doc §144)", () => {
    render(<Inspector fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} route={{ kind: "world" }} />);
    expect(screen.getByTestId("inspector-empty")).toHaveTextContent("No entity selected.");
  });

  it("shows the Settlement Pulse when a settlement is selected", () => {
    render(<Inspector fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} route={{ kind: "settlement", id: "oakbridge" }} />);
    expect(screen.getByTestId("settlement-view")).toBeInTheDocument();
  });

  it("shows the Household detail when a household is selected", () => {
    render(<Inspector fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} route={{ kind: "household", id: "valen-household" }} />);
    expect(screen.getByTestId("household-view")).toBeInTheDocument();
  });

  it("shows the Agent detail when an agent is selected", () => {
    render(<Inspector fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} route={{ kind: "agent", id: "mira-valen" }} />);
    expect(screen.getByTestId("agent-view")).toBeInTheDocument();
  });

  it("shows a contextual note (not a duplicate) while the center is showing the Causal Explorer", () => {
    render(<Inspector fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} route={{ kind: "causal", eventId: "evt-grain-prices-rose" }} />);
    expect(screen.getByTestId("inspector-empty")).toHaveTextContent("Exploring a causal chain");
    expect(screen.queryByTestId("causal-explorer")).not.toBeInTheDocument();
  });
});
