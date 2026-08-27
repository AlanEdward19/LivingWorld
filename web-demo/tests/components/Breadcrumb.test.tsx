import { describe, expect, it } from "vitest";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { Breadcrumb } from "../../src/components/Breadcrumb";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

describe("Breadcrumb", () => {
  it("shows the correct stack at each point of the P1 flow (World → Settlement → Household → Agent)", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<Breadcrumb fixture={WORLD_FIXTURE} nav={nav} />);
    const list = () => within(screen.getByTestId("breadcrumb")).getAllByRole("listitem").map((li) => li.textContent);

    expect(list()).toEqual(["World"]);

    act(() => nav.push({ kind: "settlement", id: "oakbridge" }));
    expect(list()).toEqual(["World", "Oakbridge"]);

    act(() => nav.push({ kind: "household", id: "valen-household" }));
    expect(list()).toEqual(["World", "Oakbridge", "Valen Household"]);

    act(() => nav.push({ kind: "agent", id: "mira-valen" }));
    expect(list()).toEqual(["World", "Oakbridge", "Valen Household", "Mira Valen"]);
  });

  it("does not show a Back button at the root World route", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<Breadcrumb fixture={WORLD_FIXTURE} nav={nav} />);
    expect(screen.queryByText("Back")).not.toBeInTheDocument();
  });

  it("Back button returns to the previous route, preserving state instead of resetting to World View", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    nav.push({ kind: "settlement", id: "oakbridge" });
    nav.push({ kind: "household", id: "valen-household" });
    render(<Breadcrumb fixture={WORLD_FIXTURE} nav={nav} />);

    fireEvent.click(screen.getByText("Back"));

    expect(nav.current()).toEqual({ kind: "settlement", id: "oakbridge" });
  });
});
