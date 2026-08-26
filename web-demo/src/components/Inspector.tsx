import type { WorldFixture } from "../fixture/types";
import type { NavigationStore, Route } from "../nav/NavigationStore";
import { SettlementView } from "../views/SettlementView";
import { HouseholdView } from "../views/HouseholdView";
import { AgentView } from "../views/AgentView";

export interface InspectorProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  route: Route;
}

const CONTEXT_NOTE: Partial<Record<Route["kind"], string>> = {
  causal: "Exploring a causal chain in the center panel.",
  timeline: "Browsing the timeline in the center panel.",
  life: "Viewing a life story in the center panel.",
  feed: "Browsing the World Feed in the center panel.",
  threads: "Browsing Story Threads in the center panel.",
  thread: "Browsing Story Threads in the center panel.",
};

/**
 * Inspector (doc §47-48) — painel contextual da entidade selecionada, nunca uma página
 * genérica. Quando nada está selecionado (`world`) ou o centro está mostrando algo que não é
 * uma entidade única (Causal Explorer/Timeline/Life/Feed/Threads — doc §66/§87: esses
 * substituem o mapa e "tomam" o centro), o Inspector mostra um estado vazio/contextual em vez
 * de duplicar o que já está visível no centro.
 */
export function Inspector({ fixture, nav, route }: InspectorProps) {
  if (route.kind === "settlement") {
    return (
      <aside data-testid="inspector">
        <SettlementView fixture={fixture} nav={nav} settlementId={route.id} />
      </aside>
    );
  }
  if (route.kind === "building") {
    const building = fixture.settlements.flatMap((s) => s.buildings).find((b) => b.id === route.id);
    if (!building) return <aside data-testid="inspector" />;
    const occupants = fixture.agents.filter((a) => a.indoorLocation?.buildingId === building.id);
    return (
      <aside data-testid="inspector">
        <div data-testid="building-inspector">
          <h2>{building.name}</h2>
          <dl>
            <dt>Kind</dt>
            <dd>{building.kind}</dd>
            <dt>Floors</dt>
            <dd>{building.floors.length}</dd>
          </dl>
          <ul data-testid="building-inspector-people">
            {occupants.map((agent) => (
              <li key={agent.id}>
                <button type="button" onClick={() => nav.replace({ kind: "agent", id: agent.id })}>
                  {agent.name}
                </button>
              </li>
            ))}
          </ul>
        </div>
      </aside>
    );
  }
  if (route.kind === "household") {
    return (
      <aside data-testid="inspector">
        <HouseholdView fixture={fixture} nav={nav} householdId={route.id} />
      </aside>
    );
  }
  if (route.kind === "agent") {
    return (
      <aside data-testid="inspector">
        <AgentView fixture={fixture} nav={nav} agentId={route.id} />
      </aside>
    );
  }

  const note = CONTEXT_NOTE[route.kind];
  return (
    <aside data-testid="inspector">
      <div data-testid="inspector-empty">
        {note ? <p>{note}</p> : <p>No entity selected.</p>}
        <p>Click a person on the map or search by name.</p>
      </div>
    </aside>
  );
}
