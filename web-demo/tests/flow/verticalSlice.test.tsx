import { useSyncExternalStore } from "react";
import { describe, expect, it } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { NavigationStore } from "../../src/nav/NavigationStore";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";
import { WorldView } from "../../src/views/WorldView";
import { SettlementView } from "../../src/views/SettlementView";
import { HouseholdView } from "../../src/views/HouseholdView";
import { AgentView } from "../../src/views/AgentView";
import { CausalExplorer } from "../../src/views/CausalExplorer";
import { Breadcrumb } from "../../src/components/Breadcrumb";

// Harness mínimo de roteamento — troca de view por `nav.current().kind`. Só existe pra este
// teste de integração; a app "real" (main.tsx) ainda não monta este switch (fica pra fechamento
// da demo). "Timeline" ainda não tem view própria (T21, Fase 5) — aqui é um stub que só prova
// que a navegação chegou lá sem quebrar (spec P1 Independent Test, tasks.md T20).
function TestHarness({ nav }: { nav: NavigationStore }) {
  const route = useSyncExternalStore(
    (listener) => nav.subscribe(listener),
    () => nav.current(),
  );

  return (
    <div>
      <Breadcrumb fixture={WORLD_FIXTURE} nav={nav} />
      {route.kind === "world" && <WorldView fixture={WORLD_FIXTURE} nav={nav} />}
      {route.kind === "settlement" && <SettlementView fixture={WORLD_FIXTURE} nav={nav} settlementId={route.id} />}
      {route.kind === "household" && <HouseholdView fixture={WORLD_FIXTURE} nav={nav} householdId={route.id} />}
      {route.kind === "agent" && <AgentView fixture={WORLD_FIXTURE} nav={nav} agentId={route.id} />}
      {route.kind === "causal" && <CausalExplorer fixture={WORLD_FIXTURE} nav={nav} eventId={route.eventId} />}
      {route.kind === "timeline" && <div data-testid="timeline-stub">Timeline: {JSON.stringify(route.scope)}</div>}
    </div>
  );
}

describe("Vertical slice — World → Settlement → Household → Agent → Why → CausalExplorer → Timeline", () => {
  it("walks the full P1 flow click-by-click without any step breaking", () => {
    const nav = new NavigationStore(WORLD_FIXTURE);
    render(<TestHarness nav={nav} />);

    // 1. World View
    expect(screen.getByTestId("world-view")).toBeInTheDocument();
    fireEvent.click(within(screen.getByTestId("settlement-list")).getByText("Oakbridge"));

    // 2. Settlement View
    expect(screen.getByTestId("settlement-view")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Valen Household"));

    // 3. Household View
    expect(screen.getByTestId("household-view")).toBeInTheDocument();
    fireEvent.click(within(screen.getByTestId("household-members")).getByText("Mira Valen"));

    // 4. Agent View
    expect(screen.getByTestId("agent-view")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Why?"));

    // 5. Why panel
    expect(screen.getByTestId("why-panel")).toBeInTheDocument();
    fireEvent.click(screen.getByText("grain prices rose"));

    // 6. Causal Explorer
    expect(screen.getByTestId("causal-explorer")).toBeInTheDocument();
    fireEvent.click(screen.getByText("The Valen household reduced its grain purchases."));

    // 7. Timeline (stub)
    expect(screen.getByTestId("timeline-stub")).toBeInTheDocument();

    // Breadcrumb reflects the full path taken
    const crumbs = within(screen.getByTestId("breadcrumb"))
      .getAllByRole("listitem")
      .map((li) => li.textContent);
    expect(crumbs).toEqual(["World", "Oakbridge", "Valen Household", "Mira Valen", "Causal Explorer", "Timeline"]);
  });
});
