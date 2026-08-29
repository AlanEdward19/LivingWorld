import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
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

  it("Places groups settlements by region (doc §42)", () => {
    renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Places" }));
    expect(screen.getByTestId("explorer-places")).toHaveTextContent(WORLD_FIXTURE.regions[0].name);
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

  it("People 'Notable' filter shows only agents flagged notable in the fixture", () => {
    renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "People" }));
    fireEvent.click(within(screen.getByTestId("people-filter")).getByText("Notable"));
    const expectedNotable = WORLD_FIXTURE.agents.filter((a) => a.notable);
    const people = screen.getByTestId("explorer-people");
    expect(within(people).getAllByRole("listitem")).toHaveLength(expectedNotable.length);
    expect(people).toHaveTextContent("Mira Valen"); // notable
    expect(people).not.toHaveTextContent("Eli Valen"); // not notable (child, no independent agency)
  });

  it("People 'Nearby' filter is disabled with nothing selected, and scopes to the current settlement once something is", () => {
    const { nav } = renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "People" }));
    expect(within(screen.getByTestId("people-filter")).getByText("Nearby")).toBeDisabled();

    act(() => nav.push({ kind: "agent", id: "mira-valen" }));
    const nearbyButton = within(screen.getByTestId("people-filter")).getByText("Nearby");
    expect(nearbyButton).not.toBeDisabled();
    fireEvent.click(nearbyButton);

    const expectedNearby = WORLD_FIXTURE.agents.filter((a) => a.settlementId === "oakbridge");
    const people = screen.getByTestId("explorer-people");
    expect(within(people).getAllByRole("listitem")).toHaveLength(expectedNearby.length);
  });

  it("Organizations tab shows Corvin's Bakery with its real members", () => {
    renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Organizations" }));
    const organizations = screen.getByTestId("explorer-organizations");
    expect(organizations).toHaveTextContent("Corvin's Bakery");
    expect(organizations).toHaveTextContent("Mira Valen");
  });

  it("clicking an organization member in Organizations navigates to their Agent View", () => {
    const { nav } = renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Organizations" }));
    fireEvent.click(within(screen.getByTestId("explorer-organizations")).getByText("Mira Valen"));
    expect(nav.current()).toEqual({ kind: "agent", id: "mira-valen" });
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
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

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

  // Pedido do usuário 2026-08-27: botão direito num item da lista tira ele do follow (com uma
  // animação de saída, ver `.explorer-followed-row--removing` em tokens.css), sem precisar abrir
  // a entidade pra desmarcar "Follow" de novo lá.
  it("right-clicking a followed agent plays a removal animation, then un-follows it without navigating", () => {
    act(() => followStore.toggleFollow("mira-valen"));
    const { nav } = renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Followed" }));
    const row = within(screen.getByTestId("explorer-followed")).getByText("Mira Valen").closest("li")!;
    fireEvent.contextMenu(within(screen.getByTestId("explorer-followed")).getByText("Mira Valen"));

    // Ainda seguido — a remoção de verdade só acontece depois da animação.
    expect(followStore.isFollowed("mira-valen")).toBe(true);
    expect(row).toHaveClass("explorer-followed-row--removing");

    act(() => vi.advanceTimersByTime(300));

    expect(followStore.isFollowed("mira-valen")).toBe(false);
    expect(screen.getByTestId("explorer-followed")).toHaveTextContent("Nothing followed yet.");
    expect(nav.current()).toEqual({ kind: "world" });
  });

  it("right-clicking a followed settlement/household/thread also un-follows it after the animation", () => {
    act(() => {
      followStore.toggleFollow("oakbridge");
      followStore.toggleFollow("valen-household");
      followStore.toggleFollow("oakbridge-food-crisis");
    });
    renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Followed" }));
    const list = screen.getByTestId("explorer-followed");

    fireEvent.contextMenu(within(list).getByText("Oakbridge"));
    fireEvent.contextMenu(within(list).getByText("Valen Household"));
    fireEvent.contextMenu(within(list).getByText("The Oakbridge Food Crisis"));
    act(() => vi.advanceTimersByTime(300));

    expect(followStore.isFollowed("oakbridge")).toBe(false);
    expect(followStore.isFollowed("valen-household")).toBe(false);
    expect(followStore.isFollowed("oakbridge-food-crisis")).toBe(false);
  });

  it("does not un-follow other entities when right-clicking one of them", () => {
    act(() => {
      followStore.toggleFollow("mira-valen");
      followStore.toggleFollow("rowan");
    });
    renderExplorer();
    fireEvent.click(screen.getByRole("tab", { name: "Followed" }));
    fireEvent.contextMenu(within(screen.getByTestId("explorer-followed")).getByText("Mira Valen"));
    act(() => vi.advanceTimersByTime(300));

    expect(followStore.isFollowed("mira-valen")).toBe(false);
    expect(followStore.isFollowed("rowan")).toBe(true);
  });
});
