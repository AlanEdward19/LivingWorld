import { useState } from "react";
import type { WorldFixture } from "../fixture/types";
import type { NavigationStore, Route } from "../nav/NavigationStore";
import { SemanticZoomMap, type ZoomLevel } from "../map/SemanticZoomMap";
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

function SettlementMap({ fixture, nav, settlementId }: { fixture: WorldFixture; nav: NavigationStore; settlementId: string }) {
  const [level, setLevel] = useState<Extract<ZoomLevel, "district" | "agent">>("district");
  return (
    <div data-testid="settlement-map">
      <div data-testid="map-level-toggle">
        <button type="button" onClick={() => setLevel("district")} aria-pressed={level === "district"}>
          District view
        </button>
        <button type="button" onClick={() => setLevel("agent")} aria-pressed={level === "agent"}>
          Agent view
        </button>
      </div>
      <SemanticZoomMap
        fixture={fixture}
        level={level}
        settlementId={settlementId}
        onSelectSettlement={() => {}}
        onSelectNpc={(agentId) => nav.push({ kind: "agent", id: agentId })}
      />
    </div>
  );
}

/**
 * "World" — o centro do shell (doc §5/§92-96). Por padrão é o mapa vivo, escopado pela seleção
 * atual: nível "mundo" na raiz, "distrito"/"agente" (toggle local) dentro de um settlement, e o
 * mesmo mapa do settlement continua visível ao inspecionar um household/agent dele (doc §74:
 * "Ao selecionar Oakbridge, o centro muda... não necessariamente abre página nova").
 *
 * Causal Explorer, Timeline, Life View, World Feed e Story Threads SUBSTITUEM o mapa
 * temporariamente quando abertos (doc §66 pro Causal Explorer, §87 "fullscreen center
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
            onSelectNpc={() => {}}
          />
        </div>
      );
    case "settlement":
      return (
        <div data-testid="center-stage">
          <SettlementMap fixture={fixture} nav={nav} settlementId={route.id} />
        </div>
      );
    case "household": {
      const household = fixture.households.find((h) => h.id === route.id);
      if (!household) return <div data-testid="center-stage" />;
      return (
        <div data-testid="center-stage">
          <SettlementMap fixture={fixture} nav={nav} settlementId={household.settlementId} />
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
            level="agent"
            settlementId={agent.settlementId}
            onSelectSettlement={() => {}}
            onSelectNpc={(agentId) => nav.push({ kind: "agent", id: agentId })}
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
