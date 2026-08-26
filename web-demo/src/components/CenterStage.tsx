import type { WorldFixture } from "../fixture/types";
import type { NavigationStore, Route } from "../nav/NavigationStore";
import { SemanticZoomMap } from "../map/SemanticZoomMap";
import { SettlementStage } from "../render/SettlementStage";
import { CausalExplorer } from "../views/CausalExplorer";
import { Timeline } from "../views/Timeline";
import { LifeView } from "../views/LifeView";
import { WorldFeed } from "../views/WorldFeed";
import { StoryThreads } from "../views/StoryThreads";

export interface CenterStageProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  route: Route;
}

/** Resolve o settlement a mostrar no Settlement View pra qualquer rota escopada a ele (doc
 * §22-23: "a cidade em si é o mapa" — household/agent/building continuam mostrando o MESMO
 * settlement, nunca uma página separada). */
function settlementIdForRoute(fixture: WorldFixture, route: Route): string | undefined {
  switch (route.kind) {
    case "settlement":
      return route.id;
    case "household":
      return fixture.households.find((h) => h.id === route.id)?.settlementId;
    case "agent":
      return fixture.agents.find((a) => a.id === route.id)?.settlementId;
    case "building":
      return fixture.settlements.find((s) => s.buildings.some((b) => b.id === route.id))?.id;
    default:
      return undefined;
  }
}

/**
 * "World" — o centro do shell (doc §6/§92-96). Nível "world" (mapa-múndi, SVG) na raiz;
 * settlement/household/agent/building montam o mesmo `SettlementStage` (Canvas/WebGL, AD-020)
 * escopado ao settlement relevante — entrar num prédio aproxima a câmera E revela o interior
 * NA MESMA cena (roof cutaway), nunca troca pra uma página/view separada (doc: "aproximar a
 * câmera" em vez de "navegar pra outra página").
 *
 * Causal Explorer, Timeline, Life View, World Feed e Story Threads SUBSTITUEM o mapa
 * temporariamente quando abertos (doc §49 pro Causal Explorer, §87 antigo "fullscreen center
 * experience, não Inspector estreito" pra Life View) — não ficam espremidos na largura do
 * Inspector (340px).
 */
export function CenterStage({ fixture, nav, route }: CenterStageProps) {
  switch (route.kind) {
    case "world":
      return (
        <div data-testid="center-stage">
          <SemanticZoomMap
            fixture={fixture}
            onSelectSettlement={(settlementId) => nav.push({ kind: "settlement", id: settlementId })}
            onSelectNpc={(agentId) => nav.push({ kind: "agent", id: agentId })}
          />
        </div>
      );
    case "settlement":
    case "household":
    case "agent":
    case "building": {
      const settlementId = settlementIdForRoute(fixture, route);
      if (!settlementId) return <div data-testid="center-stage" />;
      return (
        <div data-testid="center-stage">
          <SettlementStage
            fixture={fixture}
            settlementId={settlementId}
            focusBuildingId={route.kind === "building" ? route.id : null}
            onSelectAgent={(agentId) => nav.push({ kind: "agent", id: agentId })}
            onFocusBuilding={(buildingId) => (buildingId ? nav.push({ kind: "building", id: buildingId }) : nav.back())}
          />
        </div>
      );
    }
    case "causal":
      return (
        <div data-testid="center-stage">
          <CausalExplorer fixture={fixture} nav={nav} eventId={route.eventId} />
        </div>
      );
    case "timeline":
      return (
        <div data-testid="center-stage">
          <Timeline fixture={fixture} scope={route.scope} />
        </div>
      );
    case "life":
      return (
        <div data-testid="center-stage">
          <LifeView fixture={fixture} agentId={route.agentId} />
        </div>
      );
    case "feed":
      return (
        <div data-testid="center-stage">
          <WorldFeed fixture={fixture} />
        </div>
      );
    case "threads":
    case "thread":
      return (
        <div data-testid="center-stage">
          <StoryThreads fixture={fixture} nav={nav} />
        </div>
      );
  }
}
