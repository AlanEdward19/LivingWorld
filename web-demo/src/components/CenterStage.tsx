import { useEffect, useSyncExternalStore } from "react";
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

/** Rotas que "abrem em cima" do mapa (AD-021) em vez de substituí-lo — o usuário reportou que
 * perder a cidade/mundo de vista ao abrir Why?/Timeline/Life era desorientador. */
const OVERLAY_KINDS = new Set<Route["kind"]>(["causal", "timeline", "life", "feed", "threads", "thread"]);

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

/** Última rota espacial (world/settlement/household/agent/building) da pilha — o que o mapa
 * deve continuar mostrando embaixo de um overlay (causal/timeline/life/feed/threads/thread). */
function underlyingSpatialRoute(breadcrumb: Route[]): Route {
  for (let index = breadcrumb.length - 1; index >= 0; index -= 1) {
    if (!OVERLAY_KINDS.has(breadcrumb[index].kind)) return breadcrumb[index];
  }
  return { kind: "world" };
}

function SpatialLayer({ fixture, nav, route }: CenterStageProps) {
  if (route.kind === "world") {
    return (
      <SemanticZoomMap
        fixture={fixture}
        onSelectSettlement={(settlementId) => nav.push({ kind: "settlement", id: settlementId })}
        onSelectNpc={(agentId) => nav.push({ kind: "agent", id: agentId })}
      />
    );
  }

  const settlementId = settlementIdForRoute(fixture, route);
  if (!settlementId) return null;
  return (
    <SettlementStage
      fixture={fixture}
      settlementId={settlementId}
      focusBuildingId={route.kind === "building" ? route.id : null}
      onSelectAgent={(agentId) => nav.replace({ kind: "agent", id: agentId })}
      onFocusBuilding={(buildingId) => nav.replace({ kind: "building", id: buildingId })}
      onBackgroundClick={() => nav.replace({ kind: "settlement", id: settlementId })}
    />
  );
}

function OverlayContent({ fixture, nav, route }: CenterStageProps) {
  switch (route.kind) {
    case "causal":
      return <CausalExplorer fixture={fixture} nav={nav} eventId={route.eventId} />;
    case "timeline":
      return <Timeline fixture={fixture} scope={route.scope} />;
    case "life":
      return <LifeView fixture={fixture} agentId={route.agentId} />;
    case "feed":
      return <WorldFeed fixture={fixture} />;
    case "threads":
    case "thread":
      return <StoryThreads fixture={fixture} nav={nav} />;
    default:
      return null;
  }
}

/**
 * "World" — o centro do shell (doc §6/§92-96). O mapa (mundo ou settlement) fica SEMPRE
 * montado — Causal Explorer/Timeline/Life/Feed/Threads abrem como um painel POR CIMA dele
 * (AD-021), não substituem mais o centro: usuário reportou que perder a cidade/NPC de vista ao
 * checar "Why?"/Timeline era desorientador. Fecha com X, clique fora, ou Esc — todos chamam
 * `nav.back()` (essas rotas continuam empilhadas via `push`, então back as remove de novo).
 */
export function CenterStage({ fixture, nav, route }: CenterStageProps) {
  const breadcrumb = useSyncExternalStore(
    (listener) => nav.subscribe(listener),
    () => nav.breadcrumb(),
  );
  const isOverlay = OVERLAY_KINDS.has(route.kind);
  const spatialRoute = isOverlay ? underlyingSpatialRoute(breadcrumb) : route;

  useEffect(() => {
    if (!isOverlay) return undefined;
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") nav.back();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [isOverlay, nav]);

  return (
    <div data-testid="center-stage">
      <SpatialLayer fixture={fixture} nav={nav} route={spatialRoute} />
      {isOverlay && (
        <div data-testid="center-stage-overlay-backdrop" onClick={() => nav.back()}>
          <div data-testid="center-stage-overlay-panel" onClick={(event) => event.stopPropagation()}>
            <button type="button" data-testid="center-stage-overlay-close" aria-label="Close" onClick={() => nav.back()}>
              ×
            </button>
            <OverlayContent fixture={fixture} nav={nav} route={route} />
          </div>
        </div>
      )}
    </div>
  );
}
