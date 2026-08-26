import type { WorldFixture } from "../fixture/types";
import type { NavigationStore, Route } from "../nav/NavigationStore";
import { SemanticZoomMap } from "../map/SemanticZoomMap";
import { CausalExplorer } from "../views/CausalExplorer";
import { Timeline } from "../views/Timeline";
import { LifeView } from "../views/LifeView";
import { WorldFeed } from "../views/WorldFeed";
import { StoryThreads } from "../views/StoryThreads";
import { BuildingInterior } from "../views/BuildingInterior";

export interface CenterStageProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  route: Route;
}

/**
 * "World" — o centro do shell (doc §6/§92-96). Por padrão é o mapa vivo, escopado pela seleção
 * atual: nível "world" na raiz, "settlement" (prédios + NPCs juntos, sem toggle — AD-018: NPCs
 * nunca somem por causa do zoom) dentro de um settlement/household/agent — o mesmo mapa do
 * settlement continua visível ao inspecionar um household/agent dele (doc §22-23: "Ao
 * selecionar Oakbridge, o centro muda... a cidade em si é o mapa").
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
      return (
        <div data-testid="center-stage">
          <SemanticZoomMap
            fixture={fixture}
            level="settlement"
            settlementId={route.id}
            onSelectSettlement={() => {}}
            onSelectNpc={(agentId) => nav.push({ kind: "agent", id: agentId })}
            onSelectBuilding={(buildingId) => nav.push({ kind: "building", id: buildingId })}
          />
        </div>
      );
    case "building":
      return (
        <div data-testid="center-stage">
          <BuildingInterior fixture={fixture} nav={nav} buildingId={route.id} />
        </div>
      );
    case "household": {
      const household = fixture.households.find((h) => h.id === route.id);
      if (!household) return <div data-testid="center-stage" />;
      return (
        <div data-testid="center-stage">
          <SemanticZoomMap
            fixture={fixture}
            level="settlement"
            settlementId={household.settlementId}
            onSelectSettlement={() => {}}
            onSelectNpc={(agentId) => nav.push({ kind: "agent", id: agentId })}
            onSelectBuilding={(buildingId) => nav.push({ kind: "building", id: buildingId })}
          />
        </div>
      );
    }
    case "agent": {
      const agent = fixture.agents.find((a) => a.id === route.id);
      if (!agent) return <div data-testid="center-stage" />;
      return (
        <div data-testid="center-stage">
          <SemanticZoomMap
            fixture={fixture}
            level="settlement"
            settlementId={agent.settlementId}
            onSelectSettlement={() => {}}
            onSelectNpc={(agentId) => nav.push({ kind: "agent", id: agentId })}
            onSelectBuilding={(buildingId) => nav.push({ kind: "building", id: buildingId })}
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
