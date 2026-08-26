import { describe, expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { BuildingInterior } from "../../src/views/BuildingInterior";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("BuildingInterior", () => {
  it("renders the building's rooms and furniture for a single-floor building", () => {
    render(<BuildingInterior fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} buildingId="bld-valen-house" />);
    expect(screen.getByText("Valen House")).toBeInTheDocument();
    expect(screen.getAllByTestId("interior-room")).toHaveLength(3);
    expect(screen.queryByTestId("floor-selector")).not.toBeInTheDocument();
  });

  it("shows a floor selector for a multi-floor building and switches rooms shown", () => {
    render(<BuildingInterior fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} buildingId="bld-corvin-bakery" />);
    expect(screen.getByTestId("floor-selector")).toBeInTheDocument();
    expect(screen.getByText("Bakery Kitchen")).toBeInTheDocument();

    fireEvent.click(screen.getByText("Floor 1"));
    expect(screen.queryByText("Bakery Kitchen")).not.toBeInTheDocument();
    expect(screen.getByText("Corvin's Quarters")).toBeInTheDocument();
  });

  it("lists people currently inside and navigates to an agent on click", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<BuildingInterior fixture={WORLD_FIXTURE} nav={nav} buildingId="bld-corvin-bakery" />);
    const people = screen.getByTestId("people-inside");
    expect(people).toHaveTextContent("Mira");
    fireEvent.click(within(people).getByRole("button", { name: /Mira/ }));
    expect(nav.current()).toEqual({ kind: "agent", id: "mira-valen" });
  });

  it("renders an empty placeholder for a building with no interior modeled (North Farm)", () => {
    render(<BuildingInterior fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} buildingId="bld-north-farm" />);
    expect(screen.getByTestId("building-interior-empty")).toBeInTheDocument();
  });
});
