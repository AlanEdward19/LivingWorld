import { describe, expect, it } from "vitest";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { AgentView } from "../../src/views/AgentView";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";
import { modeStore } from "../../src/state/modeStore";

const MIRA = WORLD_FIXTURE.agents.find((a) => a.id === "mira-valen")!;

describe("AgentView", () => {
  it("shows Mira's identity, profession, intent, condition and body summary from the fixture", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    expect(screen.getByText("Mira Valen")).toBeInTheDocument();
    expect(screen.getByTestId("agent-age-profession")).toHaveTextContent(`${MIRA.age} · ${MIRA.profession}`);
    expect(screen.getByTestId("agent-intent")).toHaveTextContent(MIRA.currentIntent);
    const conditionChips = within(screen.getByTestId("agent-condition")).getAllByRole("listitem");
    expect(conditionChips).toHaveLength(MIRA.condition.length);
    for (const condition of MIRA.condition) {
      expect(screen.getByTestId("agent-condition")).toHaveTextContent(condition);
    }
    expect(screen.getByTestId("agent-body")).toHaveTextContent(MIRA.bodySummary.build);
  });

  it("'View details' expands Mira's full physical breakdown and what it affects (doc §51-52)", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    expect(screen.queryByTestId("agent-body-detail")).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId("toggle-body-detail"));

    const detail = screen.getByTestId("agent-body-detail");
    expect(detail).toHaveTextContent(MIRA.bodyDetail.height);
    expect(detail).toHaveTextContent(MIRA.bodyDetail.weight);
    expect(detail).toHaveTextContent(MIRA.bodyDetail.muscleMass);

    const affects = screen.getByTestId("agent-body-affects");
    for (const affect of MIRA.bodyDetail.affects) {
      expect(affects).toHaveTextContent(affect.trait);
      for (const effect of affect.effects) {
        expect(affects).toHaveTextContent(effect);
      }
    }
  });

  it("shows Mira's household and important relationships (Rowan, Corvin)", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    expect(screen.getByTestId("agent-household")).toHaveTextContent("Valen Household");
    const relationships = screen.getByTestId("agent-relationships");
    expect(relationships).toHaveTextContent("Rowan · trusted");
    expect(relationships).toHaveTextContent("Corvin · disliked employer");
  });

  it("shows Mira's recent life events", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    const events = screen.getByTestId("agent-recent-events");
    for (const event of MIRA.recentLifeEvents) {
      expect(events).toHaveTextContent(event);
    }
  });

  it("clicking Why? opens the WhyPanel with the agent's whyFactors", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    fireEvent.click(screen.getByText("Why?"));
    const panel = screen.getByTestId("why-panel");
    for (const factor of MIRA.whyFactors) {
      expect(panel).toHaveTextContent(factor.text);
    }
  });

  it("clicking Mira's household navigates to HouseholdView", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<AgentView fixture={WORLD_FIXTURE} nav={nav} agentId="mira-valen" />);
    fireEvent.click(screen.getByTestId("agent-household"));
    expect(nav.current()).toEqual({ kind: "household", id: "valen-household" });
  });

  it("clicking a Why factor ('grain prices rose') opens the Causal Explorer on the correct event", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<AgentView fixture={WORLD_FIXTURE} nav={nav} agentId="mira-valen" />);
    fireEvent.click(screen.getByText("Why?"));
    fireEvent.click(screen.getByText("grain prices rose"));
    expect(nav.current()).toEqual({ kind: "causal", eventId: "evt-grain-prices-rose" });
  });

  it("clicking 'View full life' navigates to Mira's LifeView (doc §61)", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<AgentView fixture={WORLD_FIXTURE} nav={nav} agentId="mira-valen" />);
    fireEvent.click(screen.getByTestId("view-full-life"));
    expect(nav.current()).toEqual({ kind: "life", agentId: "mira-valen" });
  });

  it("clicking View Timeline navigates to the agent-scoped Timeline (spec P2 AC1)", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<AgentView fixture={WORLD_FIXTURE} nav={nav} agentId="mira-valen" />);
    fireEvent.click(screen.getByTestId("view-timeline"));
    expect(nav.current()).toEqual({ kind: "timeline", scope: { type: "agent", id: "mira-valen" } });
  });

  it("Debug Mode shows technical event fields in the Why panel without losing the current selection (doc#116)", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    fireEvent.click(screen.getByText("Why?"));
    fireEvent.click(screen.getByTestId("toggle-mode"));

    expect(screen.getByText("Mira Valen")).toBeInTheDocument(); // seleção não mudou
    const panel = screen.getByTestId("why-panel");
    expect(panel).toHaveTextContent("evt-grain-prices-rose · GrainPriceIncreased · Economy");

    act(() => modeStore.toggleMode()); // volta pra Experience Mode
  });
});
