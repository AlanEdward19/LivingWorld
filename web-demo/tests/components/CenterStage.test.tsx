import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { CenterStage } from "../../src/components/CenterStage";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(0);
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("CenterStage — world route", () => {
  it("shows the world-level map; clicking Oakbridge's marker navigates to it", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "world" }} />);
    const oakbridgeIndex = WORLD_FIXTURE.settlements.findIndex((s) => s.id === "oakbridge");
    fireEvent.click(screen.getAllByTestId("settlement-marker")[oakbridgeIndex]);
    expect(nav.current()).toEqual({ kind: "settlement", id: "oakbridge" });
  });

  it("clicking an agent's dot at world level navigates to them too (AD-018: NPCs never disappear)", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "world" }} />);
    const miraIndex = WORLD_FIXTURE.agents.findIndex((a) => a.id === "mira-valen");
    fireEvent.click(screen.getAllByTestId("agent-marker")[miraIndex]);
    expect(nav.current()).toEqual({ kind: "agent", id: "mira-valen" });
  });
});

// Settlement/household/agent/building routes all mount the SAME `SettlementStage` (Canvas/Pixi,
// AD-020) scoped to the right settlement — deep Pixi-scene assertions (buildings/agents/roof
// cutaway/clicks) live in tests/render/SettlementStage.test.tsx, not here. CenterStage's own
// job is just "pick the right settlement id for this route", so that's all these test.
describe("CenterStage — settlement-scoped routes mount SettlementStage for the right settlement", () => {
  it("settlement route", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "settlement", id: "oakbridge" }} />);
    expect(screen.getByTestId("settlement-stage")).toBeInTheDocument();
  });

  it("household route resolves to the household's settlement", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    const household = WORLD_FIXTURE.households.find((h) => h.id === "valen-household")!;
    expect(household.settlementId).toBe("oakbridge");
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "household", id: "valen-household" }} />);
    expect(screen.getByTestId("settlement-stage")).toBeInTheDocument();
  });

  it("agent route resolves to the agent's own settlement", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "agent", id: "mira-valen" }} />);
    expect(screen.getByTestId("settlement-stage")).toBeInTheDocument();
  });

  it("building route resolves to the settlement that owns the building, focused on it", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "building", id: "bld-valen-house" }} />);
    expect(screen.getByTestId("settlement-stage-overlay")).toBeInTheDocument();
    expect(screen.getByTestId("focused-building-name")).toHaveTextContent("Valen House");
  });

  it("clicking the street-view button while on a building route returns to the settlement (AD-021: replace, not push)", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    nav.push({ kind: "settlement", id: "oakbridge" });
    nav.push({ kind: "building", id: "bld-valen-house" });
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={nav.current()} />);
    fireEvent.click(screen.getByTestId("street-view-button"));
    expect(nav.current()).toEqual({ kind: "settlement", id: "oakbridge" });
  });

  it("selecting an indoor agent's route keeps their building focused (regression: used to kick you back out to the street)", () => {
    // mira-valen mora/trabalha dentro de bld-corvin-bakery (indoorLocation no fixture) — rota
    // virando "agent" (como onSelectAgent faz ao clicar o sprite dela) não deveria derrubar o
    // foco no prédio onde ela está de verdade.
    const mira = WORLD_FIXTURE.agents.find((a) => a.id === "mira-valen")!;
    expect(mira.indoorLocation?.buildingId).toBe("bld-corvin-bakery");

    const nav = new NavigationStore(WORLD_FIXTURE);
    const { rerender } = render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "building", id: "bld-corvin-bakery" }} />);
    expect(screen.getByTestId("focused-building-name")).toHaveTextContent("Corvin's Bakery");

    rerender(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "agent", id: "mira-valen" }} />);

    expect(screen.getByTestId("settlement-stage-overlay")).toBeInTheDocument();
    expect(screen.getByTestId("focused-building-name")).toHaveTextContent("Corvin's Bakery");
  });

  it("selecting an outdoor-only agent's route (no indoorLocation) does unfocus the building — they aren't visually inside it", () => {
    const rowan = WORLD_FIXTURE.agents.find((a) => a.id === "rowan")!;
    expect(rowan.indoorLocation).toBeUndefined();

    const nav = new NavigationStore(WORLD_FIXTURE);
    const { rerender } = render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "building", id: "bld-corvin-bakery" }} />);
    expect(screen.getByTestId("focused-building-name")).toBeInTheDocument();

    rerender(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "agent", id: "rowan" }} />);

    expect(screen.queryByTestId("settlement-stage-overlay")).not.toBeInTheDocument();
  });

  it("clicking an OUTDOOR agent from the street does NOT auto-jump into their house (regression: fixing the bug above over-broadly once did exactly this)", () => {
    // Mira mora na padaria, mas ver o Inspector dela a partir da RUA (sem nenhum prédio focado
    // antes) não deve puxar a câmera pra dentro da casa dela — ela só "está lá dentro" quando o
    // prédio já estava focado ANTES de ela ser selecionada.
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "agent", id: "mira-valen" }} />);
    expect(screen.queryByTestId("settlement-stage-overlay")).not.toBeInTheDocument();
  });

  it("focus memory resets once you leave via the settlement route, so a later indoor-agent click doesn't stick around from a stale focus", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    const { rerender } = render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "building", id: "bld-corvin-bakery" }} />);
    expect(screen.getByTestId("settlement-stage-overlay")).toBeInTheDocument();

    rerender(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "settlement", id: "oakbridge" }} />);
    expect(screen.queryByTestId("settlement-stage-overlay")).not.toBeInTheDocument();

    rerender(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "agent", id: "mira-valen" }} />);
    expect(screen.queryByTestId("settlement-stage-overlay")).not.toBeInTheDocument();
  });
});

// AD-021: causal/timeline/life/feed/threads open as a panel OVER the map — the map (world or
// settlement) stays mounted underneath the whole time. User feedback: losing the city/NPC view
// to check "Why?"/Timeline was disorienting.
describe("CenterStage — causal/timeline/life/feed/threads open as an overlay, map stays visible", () => {
  it("shows the Causal Explorer for a causal route, with the world map still mounted underneath", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "causal", eventId: "evt-grain-prices-rose" }} />);
    expect(screen.getByTestId("causal-explorer")).toBeInTheDocument();
    expect(screen.getByTestId("semantic-zoom-map")).toBeInTheDocument();
    expect(screen.getByTestId("center-stage-overlay-backdrop")).toBeInTheDocument();
  });

  it("shows the settlement map underneath when the overlay was opened while a settlement was focused", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    nav.push({ kind: "settlement", id: "oakbridge" });
    nav.push({ kind: "causal", eventId: "evt-grain-prices-rose" });
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={nav.current()} />);
    expect(screen.getByTestId("causal-explorer")).toBeInTheDocument();
    expect(screen.getByTestId("settlement-stage")).toBeInTheDocument();
  });

  it("shows the Timeline for a timeline route", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "timeline", scope: { type: "world" } }} />);
    expect(screen.getByTestId("timeline-view")).toBeInTheDocument();
  });

  it("shows the Life View for a life route", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "life", agentId: "mira-valen" }} />);
    expect(screen.getByTestId("life-view")).toBeInTheDocument();
  });

  it("shows the World Feed for a feed route", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "feed" }} />);
    expect(screen.getByTestId("world-feed")).toBeInTheDocument();
  });

  it("shows Story Threads for a threads route", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "threads" }} />);
    expect(screen.getByTestId("story-threads")).toBeInTheDocument();
  });

  it("closing via the X button calls nav.back()", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    nav.push({ kind: "causal", eventId: "evt-grain-prices-rose" });
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={nav.current()} />);
    fireEvent.click(screen.getByTestId("center-stage-overlay-close"));
    expect(nav.current()).toEqual({ kind: "world" });
  });

  it("closing via a backdrop click calls nav.back()", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    nav.push({ kind: "causal", eventId: "evt-grain-prices-rose" });
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={nav.current()} />);
    fireEvent.click(screen.getByTestId("center-stage-overlay-backdrop"));
    expect(nav.current()).toEqual({ kind: "world" });
  });

  it("clicking inside the overlay panel itself does not close it", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    nav.push({ kind: "causal", eventId: "evt-grain-prices-rose" });
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={nav.current()} />);
    fireEvent.click(screen.getByTestId("center-stage-overlay-panel"));
    expect(nav.current()).toEqual({ kind: "causal", eventId: "evt-grain-prices-rose" });
  });

  it("pressing Escape closes the overlay", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    nav.push({ kind: "causal", eventId: "evt-grain-prices-rose" });
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={nav.current()} />);
    fireEvent.keyDown(window, { key: "Escape" });
    expect(nav.current()).toEqual({ kind: "world" });
  });

  it("does not show the overlay backdrop for spatial routes", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "world" }} />);
    expect(screen.queryByTestId("center-stage-overlay-backdrop")).not.toBeInTheDocument();
  });
});
