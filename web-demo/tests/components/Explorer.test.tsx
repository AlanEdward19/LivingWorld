import { afterEach, describe, expect, it } from "vitest";
import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { Explorer } from "../../src/components/Explorer";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";
import { followStore } from "../../src/state/followStore";

afterEach(() => {
  cleanup();
  act(() => {
    for (const id of followStore.followedIds()) followStore.toggleFollow(id);
  });
});

function renderExplorer() {
  const nav = new NavigationStore(WORLD_FIXTURE);
  const utils = render(<Explorer fixture={WORLD_FIXTURE} nav={nav} />);
  return { nav, ...utils };
}

describe("Explorer — Overview tab (default)", () => {
  it("shows real World Pulse numbers derived from the fixture", () => {
    renderExplorer();
    const overview = screen.getByTestId("explorer-overview");
    const expectedPopulation = WORLD_FIXTURE.settlements.reduce((sum, s) => sum + s.population, 0);
    expect(overview).toHaveTextContent(String(expectedPopulation));
    expect(overview).toHaveTextContent(String(WORLD_FIXTURE.settlements.length));
  });

  it("clicking a recent event opens the Causal Explorer there", () => {
    const { nav } = renderExplorer();
    const lastEvent = WORLD_FIXTURE.events[WORLD_FIXTURE.events.length - 1];
    fireEvent.click(screen.getByText(lastEvent.summary));
    expect(nav.current()).toEqual({ kind: "causal", eventId: lastEvent.eventId });
  });
});

describe("Explorer — tab switching", () => {
  it("switching to Places lists every settlement, clicking one navigates", () => {
    const { nav } = renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Places" }));
    const places = screen.getByTestId("explorer-places");
    for (const settlement of WORLD_FIXTURE.settlements) {
      expect(places).toHaveTextContent(settlement.name);
    }
    fireEvent.click(within(places).getByText("Oakbridge"));
    expect(nav.current()).toEqual({ kind: "settlement", id: "oakbridge" });
  });

  it("switching to People lists every agent by default, clicking one navigates", () => {
    const { nav } = renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "People" }));
    const people = screen.getByTestId("explorer-people");
    expect(within(people).getAllByRole("listitem")).toHaveLength(WORLD_FIXTURE.agents.length);
    fireEvent.click(within(people).getByText("Mira Valen"));
    expect(nav.current()).toEqual({ kind: "agent", id: "mira-valen" });
  });

  it("People 'Followed' filter shows only followed agents", () => {
    act(() => followStore.toggleFollow("mira-valen"));
    renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "People" }));
    fireEvent.click(within(screen.getByTestId("people-filter")).getByText("Followed"));
    const people = screen.getByTestId("explorer-people");
    expect(within(people).getAllByRole("listitem")).toHaveLength(1);
    expect(people).toHaveTextContent("Mira Valen");
  });

  it("Organizations tab shows an explicit empty state (fixture models none)", () => {
    renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Organizations" }));
    expect(screen.getByTestId("explorer-organizations")).toHaveTextContent("No organizations in this world yet.");
  });

  it("Threads tab shows the Oakbridge Food Crisis card", () => {
    renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Threads" }));
    expect(screen.getByText("The Oakbridge Food Crisis")).toBeInTheDocument();
  });

  it("Events tab shows the World Feed", () => {
    renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Events" }));
    expect(screen.getByTestId("world-feed")).toBeInTheDocument();
  });
});

describe("Explorer — Followed tab", () => {
  it("shows an explicit empty state when nothing is followed", () => {
    renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Followed" }));
    expect(screen.getByTestId("explorer-followed")).toHaveTextContent("Nothing followed yet.");
  });

  it("lists a followed agent and navigates to them on click", () => {
    act(() => followStore.toggleFollow("mira-valen"));
    const { nav } = renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Followed" }));
    fireEvent.click(within(screen.getByTestId("explorer-followed")).getByText("Mira Valen"));
    expect(nav.current()).toEqual({ kind: "agent", id: "mira-valen" });
  });
});
