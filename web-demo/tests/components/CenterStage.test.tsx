import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { CenterStage } from "../../src/components/CenterStage";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

const OAKBRIDGE = WORLD_FIXTURE.settlements.find((s) => s.id === "oakbridge")!;
const OAKBRIDGE_AGENTS = WORLD_FIXTURE.agents.filter((a) => a.settlementId === "oakbridge");

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

describe("CenterStage — settlement route", () => {
  it("shows buildings AND every agent of the settlement together, no toggle between them", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    const { container } = render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "settlement", id: "oakbridge" }} />);
    expect(container.querySelectorAll("polygon")).toHaveLength(OAKBRIDGE.buildings.length * 3);
    expect(container.querySelectorAll("img")).toHaveLength(OAKBRIDGE_AGENTS.length);
    expect(screen.queryByText("Agent view")).not.toBeInTheDocument();
    expect(screen.queryByText("District view")).not.toBeInTheDocument();
  });

  it("clicking Mira navigates to her, same as any other path (spec P1b AC4)", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "settlement", id: "oakbridge" }} />);
    const miraIndex = OAKBRIDGE_AGENTS.findIndex((a) => a.id === "mira-valen");
    fireEvent.click(screen.getAllByTestId("agent-marker")[miraIndex]);
    expect(nav.current()).toEqual({ kind: "agent", id: "mira-valen" });
  });
});

describe("CenterStage — household route", () => {
  it("shows the household's settlement map with buildings and agents together", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    const { container } = render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "household", id: "valen-household" }} />);
    expect(container.querySelectorAll("polygon")).toHaveLength(OAKBRIDGE.buildings.length * 3);
    expect(container.querySelectorAll("img")).toHaveLength(OAKBRIDGE_AGENTS.length);
  });
});

describe("CenterStage — agent route", () => {
  it("shows the settlement map (buildings + agents) of the agent's own settlement", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    const { container } = render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "agent", id: "mira-valen" }} />);
    expect(container.querySelectorAll("img")).toHaveLength(OAKBRIDGE_AGENTS.length);
  });
});

describe("CenterStage — replaces the map for causal/timeline/life/feed/threads", () => {
  it("shows the Causal Explorer for a causal route", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<CenterStage fixture={WORLD_FIXTURE} nav={nav} route={{ kind: "causal", eventId: "evt-grain-prices-rose" }} />);
    expect(screen.getByTestId("causal-explorer")).toBeInTheDocument();
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
});
