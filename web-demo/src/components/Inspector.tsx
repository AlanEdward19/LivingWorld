import type { WorldFixture } from "../fixture/types";
import type { NavigationStore, Route } from "../nav/NavigationStore";
import { SettlementView } from "../views/SettlementView";
import { HouseholdView } from "../views/HouseholdView";
import { AgentView } from "../views/AgentView";
import { BuildingView } from "../views/BuildingView";
import { WorldView } from "../views/WorldView";

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
  // Pedido do usuário 2026-08-27: clicar em qualquer coisa que não seja casa/NPC no mapa mundi
  // (terreno vazio) mostra info do MUNDO — mesma paridade de "route.kind === settlement" já
  // mostrar o Settlement Inspector pra qualquer motivo de estar naquela rota (deep-link, clique
  // no mapa, clique de fundo dentro da cidade), não só um caso especial de "acabou de clicar".
  if (route.kind === "world") {
    return (
      <aside data-testid="inspector">
        <WorldView fixture={fixture} nav={nav} />
      </aside>
    );
  }
  if (route.kind === "settlement") {
    return (
      <aside data-testid="inspector">
        <SettlementView fixture={fixture} nav={nav} settlementId={route.id} />
      </aside>
    );
  }
  if (route.kind === "building") {
    return (
      <aside data-testid="inspector">
        <BuildingView fixture={fixture} nav={nav} buildingId={route.id} />
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
