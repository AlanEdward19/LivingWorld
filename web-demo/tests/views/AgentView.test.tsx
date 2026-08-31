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

  it("'View physical details' opens a Popup (Nível 3, redesign doc §14/§19) with Mira's full physical breakdown", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    expect(screen.queryByTestId("popup-panel")).not.toBeInTheDocument();

    fireEvent.click(screen.getByText("View physical details →"));

    expect(screen.getByTestId("popup-panel")).toHaveTextContent("Physical details");
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

    fireEvent.click(screen.getByTestId("popup-close"));
    expect(screen.queryByTestId("popup-panel")).not.toBeInTheDocument();
  });

  it("'View skills' opens a Popup with Mira's skills and their levels", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    expect(screen.queryByTestId("popup-panel")).not.toBeInTheDocument();

    fireEvent.click(screen.getByText("View skills →"));

    expect(screen.getByTestId("popup-panel")).toHaveTextContent("Mira Valen's skills");
    const detail = screen.getByTestId("agent-skills-detail");
    for (const skill of MIRA.skills) {
      expect(detail).toHaveTextContent(skill.name);
      expect(detail).toHaveTextContent(String(Math.floor(skill.level)));
    }

    fireEvent.click(screen.getByTestId("popup-close"));
    expect(screen.queryByTestId("popup-panel")).not.toBeInTheDocument();
  });

  it("'View relationships' opens a Popup with Mira's full relationship list", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    fireEvent.click(screen.getByText("View relationships →"));
    const popup = screen.getByTestId("popup-panel");
    expect(popup).toHaveTextContent("Rowan");
    expect(popup).toHaveTextContent("Corvin");
  });

  it("shows Mira's household and her closest relationships (family surfaces first)", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    expect(screen.getByTestId("agent-household")).toHaveTextContent("Valen Household");
    const relationships = screen.getByTestId("agent-relationships");
    expect(relationships).toHaveTextContent("Tomas Valen · husband");
    expect(relationships).toHaveTextContent("Eli Valen · son");
    // Rowan/Corvin ainda existem, só não cabem no preview de 2 — cobertos por "View relationships".
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
    fireEvent.click(screen.getByText("Explain decision →"));
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
    fireEvent.click(screen.getByText("Explain decision →"));
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

  it("does not show a Back button at the root World route", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    expect(screen.queryByLabelText("Back")).not.toBeInTheDocument();
  });

  it("Back button returns to the previous route, preserving state instead of resetting to World View", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    nav.push({ kind: "settlement", id: "oakbridge" });
    nav.push({ kind: "agent", id: "mira-valen" });
    render(<AgentView fixture={WORLD_FIXTURE} nav={nav} agentId="mira-valen" />);

    fireEvent.click(screen.getByLabelText("Back"));

    expect(nav.current()).toEqual({ kind: "settlement", id: "oakbridge" });
  });

  it("Debug Mode shows technical event fields in the Why panel without losing the current selection (doc#116)", () => {
    render(<AgentView fixture={WORLD_FIXTURE} nav={new NavigationStore(WORLD_FIXTURE)} agentId="mira-valen" />);
    fireEvent.click(screen.getByText("Explain decision →"));
    fireEvent.click(screen.getByTestId("toggle-mode"));

    expect(screen.getByText("Mira Valen")).toBeInTheDocument(); // seleção não mudou
    const panel = screen.getByTestId("why-panel");
    expect(panel).toHaveTextContent("evt-grain-prices-rose · GrainPriceIncreased · Economy");

    act(() => modeStore.toggleMode()); // volta pra Experience Mode
  });
});
