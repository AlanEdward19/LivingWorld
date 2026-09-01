import { afterEach, describe, expect, it } from "vitest";
import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { TopBar } from "../../src/components/TopBar";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";
import { followStore } from "../../src/state/followStore";

afterEach(() => {
  cleanup();
  act(() => {
    for (const id of followStore.followedIds()) followStore.toggleFollow(id);
  });
});

describe("TopBar", () => {
  it("renders logo, world selector, mode selector, breadcrumb, date and search", () => {
    render(<TopBar fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} />);
    expect(screen.getByTestId("logo")).toBeInTheDocument();
    expect(screen.getByTestId("world-selector")).toHaveTextContent(WORLD_FIXTURE.world.name);
    expect(screen.getByTestId("mode-selector")).toHaveTextContent("Observe");
    expect(screen.getByTestId("topbar-breadcrumb")).toBeInTheDocument();
    const lastEvent = WORLD_FIXTURE.events[WORLD_FIXTURE.events.length - 1];
    expect(screen.getByTestId("world-date")).toHaveTextContent(lastEvent.tick);
    expect(screen.getByTestId("topbar-search")).toBeInTheDocument();
  });

  it("simulation controls and settings are present but disabled (no real simulation in this demo)", () => {
    render(<TopBar fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} />);
    const simControls = within(screen.getByTestId("simulation-controls")).getAllByRole("button");
    for (const button of simControls) expect(button).toBeDisabled();
    expect(screen.getByTestId("settings-button")).toBeDisabled();
  });

  it("clicking the logo navigates to World View", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    nav.push({ kind: "settlement", id: "oakbridge" });
    render(<TopBar fixture={WORLD_FIXTURE} nav={nav} />);
    fireEvent.click(screen.getByTestId("logo"));
    expect(nav.current()).toEqual({ kind: "world" });
  });

  it("World Selector: 'World Details' navigates to World View, 'Switch World' is disabled", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    nav.push({ kind: "settlement", id: "oakbridge" });
    render(<TopBar fixture={WORLD_FIXTURE} nav={nav} />);
    fireEvent.click(within(screen.getByTestId("world-selector")).getByText(`${WORLD_FIXTURE.world.name} ▾`));
    expect(screen.getByText("Switch World")).toBeDisabled();
    fireEvent.click(screen.getByText("World Details"));
    expect(nav.current()).toEqual({ kind: "world" });
  });

  it("World Selector: clicking outside closes the open menu", () => {
    render(<TopBar fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} />);
    fireEvent.click(within(screen.getByTestId("world-selector")).getByText(`${WORLD_FIXTURE.world.name} ▾`));
    expect(screen.getByRole("menu")).toBeInTheDocument();
    fireEvent.mouseDown(document.body);
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });

  it("Mode Selector: Observe is the active mode, Table and Inhabit are disabled ('Coming'), not hidden", () => {
    render(<TopBar fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} />);
    fireEvent.click(within(screen.getByTestId("mode-selector")).getByText("Observe ▾"));
    const menu = within(screen.getByTestId("mode-selector")).getByRole("menu");
    expect(within(menu).getByText("Table")).toBeInTheDocument();
    expect(within(menu).getByText("Table").closest("button")).toBeDisabled();
    expect(within(menu).getByText("Inhabit").closest("button")).toBeDisabled();
  });

  it("shows no notifications badge when nothing is followed", () => {
    render(<TopBar fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} />);
    expect(screen.getByTestId("notifications-button")).toBeDisabled();
  });

  it("shows a real notification count for events affecting a followed agent, and clicking one opens the Causal Explorer there", () => {
    followStore.toggleFollow("mira-valen");
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<TopBar fixture={WORLD_FIXTURE} nav={nav} />);

    const expectedCount = WORLD_FIXTURE.events.filter((e) => e.affectedAgentIds.includes("mira-valen")).length;
    expect(screen.getByTestId("notifications-button")).toHaveTextContent(`● ${expectedCount}`);
    expect(screen.getByTestId("notifications-button")).not.toBeDisabled();

    fireEvent.click(screen.getByTestId("notifications-button"));
    fireEvent.click(screen.getByText("Mira Valen became very hungry."));
    expect(nav.current()).toEqual({ kind: "causal", eventId: "evt-mira-very-hungry" });
  });
});
